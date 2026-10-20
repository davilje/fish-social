import assert from 'node:assert/strict';
import path from 'node:path';
import fs from 'node:fs';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const browser = await chromium.launch({ headless: true,
  args: ['--allow-file-access-from-files'],
  ...(process.env.CHROME_PATH ? { executablePath: process.env.CHROME_PATH } : {}) });
const errors = [];
try {
  const page = await browser.newPage({ viewport: { width: 1440, height: 1100 } });
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(pathToFileURL(path.join(root, 'docs/art/cat-skins/breeds.html')).href);
  const skins = ['orange', 'calico', 'silver', 'brown', 'cream', 'blue', 'black', 'white', 'cow'];
  let selectedFrames = 0;
  for (const skin of skins) {
    await page.selectOption('#breed', skin);
    for (const clip of ['ready', 'cast', 'retrieve', 'retrieve_empty', 'catch', 'reset_empty']) {
      await page.selectOption('#clip', clip);
      for (const boundary of ['first', 'last']) {
        const frames = page.locator('#grid button');
        const button = boundary === 'first' ? frames.first() : frames.last();
        const id = await button.getAttribute('data-id');
        await button.click();
        await page.waitForFunction(async ({ skin, id }) => {
          if (document.querySelector('#error').textContent) throw new Error(document.querySelector('#error').textContent);
          const expected = new Image();
          expected.src = `${skin}-full/${id}.png`;
          await expected.decode();
          const canvas = document.createElement('canvas');
          canvas.width = 640; canvas.height = 512;
          canvas.getContext('2d').drawImage(expected, 0, 0);
          return canvas.toDataURL() === document.querySelector('#current').toDataURL();
        }, { skin, id });
        selectedFrames++;
      }
    }
  }
  const whaleRoot = path.join(root, 'docs/art/whale-maid/template-study-v1');
  const whale = JSON.parse(fs.readFileSync(path.join(whaleRoot, 'manifest.json')));
  await page.goto(pathToFileURL(path.join(whaleRoot, 'index.html')).href);
  for (const frame of whale.frames) {
    await page.locator(`#poses [data-pose="${frame.id}"]`).click();
    await page.waitForFunction(() => [...document.querySelectorAll('.stage img')].every(image => image.complete && image.naturalWidth > 0));
    const visible = await page.locator('#whale-stage .layer').evaluateAll(images => images.map(image => image.src.split('/').at(-1).replace('.png', '')));
    assert.deepEqual(visible, frame.renderLayerOrder);
    for (const layer of ['body', 'head', 'far_hand', 'hands', 'line', 'fish', 'bobber', 'bait']) {
      await page.locator(`#layers input[data-layer="${layer}"]`).uncheck();
    }
    assert.deepEqual(await page.locator('#whale-stage .layer').evaluateAll(images => images.map(image => image.src.split('/').at(-1))), ['rod.png']);
    for (const layer of ['body', 'head', 'far_hand', 'hands', 'line', 'fish', 'bobber', 'bait']) {
      await page.locator(`#layers input[data-layer="${layer}"]`).check();
    }
  }
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ skins: skins.length, selectedFrames, whaleFrames: whale.frames.length,
    layerToggle: 'passed', pageErrors: errors.length, result: 'passed' }, null, 2));
} finally {
  await browser.close();
}
