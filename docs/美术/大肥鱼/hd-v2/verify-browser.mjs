import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const folder = path.dirname(fileURLToPath(import.meta.url));
const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH || '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: true });
const page = await browser.newPage({ viewport: { width: 1520, height: 1200 }, deviceScaleFactor: 2 });
const errors = [];
page.on('pageerror', error => errors.push(error.message));
try {
  await page.goto('file://' + path.join(folder, 'index.html'));
  await page.evaluate(() => window.dafeiyuReview.ready);
  const frames = await page.evaluate(() => window.dafeiyu.frames.map(frame => frame.id));
  const measurements = [];
  for (const id of frames) {
    await page.evaluate(id => window.dafeiyuReview.select(id), id);
    measurements.push(await page.evaluate(() => ({
      id: window.dafeiyuReview.state().id,
      error: document.querySelector('#error').textContent,
      layers: [...document.querySelectorAll('#stage .layer')].map(image => ({
        source: image.src.split('/').pop(), size: [image.naturalWidth, image.naturalHeight],
        rect: [image.getBoundingClientRect().width, image.getBoundingClientRect().height],
        loaded: image.complete && image.naturalWidth > 0,
      })),
    })));
  }
  assert.equal(frames.length, 63);
  for (const frame of measurements) {
    assert.equal(frame.error, '', frame.id);
    assert.equal(frame.layers.length, 9);
    for (const layer of frame.layers) {
      assert(layer.loaded, frame.id + '/' + layer.source);
      assert.deepEqual(layer.size, ['body.png', 'head.png', 'far_hand.png', 'hands.png'].includes(layer.source) ? [1280, 1024] : [640, 512]);
      assert.deepEqual(layer.rect, measurements[0].layers[0].rect, frame.id + ': display size changed');
    }
  }
  await page.selectOption('#comparison-mode', 'cat');
  await page.evaluate(() => window.dafeiyuReview.select('ready-00'));
  assert.equal(await page.locator('#error').textContent(), '');
  await page.selectOption('#comparison-mode', 'old');
  await page.evaluate(() => window.dafeiyuReview.select('ready-00'));
  await page.screenshot({ path: path.join(folder, 'browser-review.png'), fullPage: true });
  await page.click('#success');
  assert.equal(await page.evaluate(() => window.dafeiyuReview.state().running), true);
  await page.evaluate(() => window.dafeiyuReview.stop());
  await page.click('#empty');
  assert.equal(await page.evaluate(() => window.dafeiyuReview.state().running), true);
  await page.evaluate(() => window.dafeiyuReview.stop());
  assert.deepEqual(errors, []);
  const result = { status: 'passed', checkedFrames: frames.length, deviceScaleFactor: 2, errors, measurements };
  fs.writeFileSync(path.join(folder, 'browser-verification.json'), JSON.stringify(result, null, 2) + '\n');
  console.log(JSON.stringify({ status: result.status, checkedFrames: frames.length, physicalCharacterTexture: [1280, 1024], displayRect: measurements[0].layers[0].rect }));
} finally {
  await browser.close();
}
