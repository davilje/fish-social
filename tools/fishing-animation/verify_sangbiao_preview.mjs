import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { fileURLToPath, pathToFileURL } from 'node:url';

const require = createRequire(import.meta.url);
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const art = path.join(root, 'docs/art/cat-skins/sangbiao');
const browser = await chromium.launch({ executablePath: process.env.CHROME_BIN || '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome', headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 1024 } });
const failures = [];
page.on('pageerror', error => failures.push(error.message));
page.on('requestfailed', request => failures.push(request.url()));
try {
  await page.goto(pathToFileURL(path.join(art, 'index.html')).href);
  const clips = await page.evaluate(() => Object.entries(window.sangbiaoPreview.clips).map(([id, frames]) => ({ id, sources: frames.map(frame => frame.source) })));
  assert.equal(clips.length, 16);
  assert.equal(await page.locator('#layers input').count(), 9);
  let checkedFrames = 0;
  for (const clip of clips) {
    await page.selectOption('#clip', clip.id);
    for (const [index, source] of clip.sources.entries()) {
      await page.waitForFunction(expected => document.querySelector('#status').textContent.startsWith(expected), `${index + 1}/${clip.sources.length} · ${source}`);
      assert.equal(await page.locator('#error').textContent(), '');
      checkedFrames++;
      if (index + 1 < clip.sources.length) await page.click('#next');
    }
  }
  assert.equal(checkedFrames, 74);
  await page.selectOption('#clip', 'fishing');
  await page.waitForFunction(() => document.querySelector('#status').textContent.includes('wait-00'));
  await page.selectOption('#background', '#24272c');
  await page.screenshot({ path: path.join(art, 'browser-review.png') });
  for (const layer of await page.locator('#layers input').all()) {
    await layer.uncheck(); await layer.check();
  }
  await page.selectOption('#clip', 'reel');
  await page.selectOption('#speed', '0.25');
  await page.click('#play');
  await page.waitForFunction(() => document.querySelector('#status').textContent.startsWith('2/'));
  await page.click('#play');
  assert.equal(await page.locator('#error').textContent(), '');
  assert.deepEqual(failures, []);
  const report = { result: 'passed', runtimeClips: clips.length, frameSlotsRendered: checkedFrames, layers: 9, slowPlayback: true, layerToggles: true, loadingErrors: failures, unityPlayerTested: false, windowsPlayerTested: false };
  fs.writeFileSync(path.join(art, 'browser-validation.json'), JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify(report, null, 2));
} finally {
  await browser.close();
}
