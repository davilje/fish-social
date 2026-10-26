import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const folder = path.dirname(fileURLToPath(import.meta.url));
const { chromium } = createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE || 'playwright');
const manifest = JSON.parse(fs.readFileSync(path.join(folder, 'manifest.json')));
const errors = [];
const browser = await chromium.launch({ headless: true, args: ['--allow-file-access-from-files'],
  ...(process.env.CHROME_PATH ? { executablePath: process.env.CHROME_PATH } : {}) });
try {
  const page = await browser.newPage({ viewport: { width: 1460, height: 1120 } });
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(pathToFileURL(path.join(folder, 'index.html')).href);
  await page.evaluate(() => window.dafeiyuReview.ready);
  assert((await page.title()).includes('大肥鱼'));
  assert.equal(await page.locator('#clip option').count(), 14);
  for (const frame of manifest.frames) {
    await page.evaluate(id => window.dafeiyuReview.select(id), frame.id);
    assert.equal(await page.locator('#error').textContent(), '');
    const layers = await page.locator('#stage .layer').evaluateAll(images => images.map(image => image.src.split('/').at(-1).replace('.png', '')));
    assert.deepEqual(layers, frame.renderLayerOrder);
    assert.equal(await page.locator('#stage .bench').count(), frame.showSeat ? 1 : 0);
    const difference = await page.evaluate(async file => {
      const expected = new Image(); expected.src = file; await expected.decode();
      const canvas = document.createElement('canvas'); canvas.width = 640; canvas.height = 512;
      const context = canvas.getContext('2d');
      context.drawImage(expected, 0, 0);
      const source = context.getImageData(0, 0, 640, 512).data;
      context.clearRect(0, 0, 640, 512);
      for (const image of document.querySelectorAll('#stage .layer')) context.drawImage(image, 0, 0, 640, 512);
      const actual = context.getImageData(0, 0, 640, 512).data;
      let maximum = 0;
      for (let offset = 0; offset < source.length; offset += 4) {
        if (source[offset + 3] < 100 && actual[offset + 3] < 100) continue;
        for (let channel = 0; channel < 4; channel++) maximum = Math.max(maximum, Math.abs(source[offset + channel] - actual[offset + channel]));
      }
      return maximum;
    }, frame.composite);
    assert(difference <= 8, frame.id + ': browser layer/composite difference ' + difference);
  }
  await page.evaluate(() => window.dafeiyuReview.select('ready-00'));
  for (const layer of [...manifest.bodyLayers, 'line', 'fish', 'bobber', 'bait']) await page.locator(`#layers input[data-layer="${layer}"]`).uncheck();
  assert.equal(await page.locator('#stage .layer').count(), 1);
  assert((await page.locator('#stage .layer').getAttribute('src')).endsWith('/rod.png'));
  for (const layer of [...manifest.bodyLayers, 'line', 'fish', 'bobber', 'bait']) await page.locator(`#layers input[data-layer="${layer}"]`).check();
  await page.selectOption('#speed', '2');
  await page.fill('#effort-duration', '1');
  for (const outcome of ['catch', 'empty']) {
    await page.locator(outcome === 'catch' ? '#success' : '#empty').click();
    if (outcome === 'catch') await page.waitForFunction(() => window.dafeiyuReview.state().clip === 'effort');
    await page.waitForFunction(expected => window.dafeiyuReview.state().clip === expected, outcome === 'catch' ? 'catch' : 'retrieve_empty');
    await page.waitForFunction(() => !window.dafeiyuReview.state().running && window.dafeiyuReview.state().id === 'ready-00');
  }
  await page.evaluate(() => window.dafeiyuReview.select('retrieve-02'));
  await page.screenshot({ path: path.join(folder, 'browser-review.png'), fullPage: true });
  assert.deepEqual(errors, []);
  const report = { name: '大肥鱼', frames: 63, actions: 14, isolatedRod: 'passed',
    fullSuccessSequence: 'passed', fullEmptySequence: 'passed', pageErrors: errors.length, result: 'passed' };
  fs.writeFileSync(path.join(folder, 'browser-verification.json'), JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify(report));
} finally {
  await browser.close();
}
