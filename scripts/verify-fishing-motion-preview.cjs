const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { pathToFileURL } = require('node:url');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');

(async () => {
  const root = path.resolve(__dirname, '..');
  const folder = path.join(root, 'docs/art/fishing-motion');
  const browser = await chromium.launch({ headless: true, ...(process.env.CHROME_PATH ? { executablePath: process.env.CHROME_PATH } : {}) });
  try {
    const page = await browser.newPage({ viewport: { width: 1380, height: 1100 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(pathToFileURL(path.join(folder, 'index.html')).href);
    await page.click('#play');
    const pets = await page.locator('#pet option').evaluateAll(options => options.map(option => option.value));
    let checked = 0;
    for (const pet of pets) {
      await page.selectOption('#pet', pet);
      for (const clip of ['cast', 'reel', 'reel_empty', 'groundbait']) {
        await page.selectOption('#clip', clip);
        for (const time of [0, 210, 550, 3250]) {
          await page.evaluate(async value => { elapsed = value; await render(); }, time);
          assert.match(await page.locator('#after-info').textContent(), /采样/);
          checked++;
        }
        if (pet === 'orange' && clip !== 'reel_empty') {
          await page.evaluate(async value => { elapsed = value; await render(); }, clip === 'groundbait' ? 3250 : 210);
          await page.screenshot({ path: path.join(folder, `${clip}-comparison.png`) });
        }
      }
    }
    await page.selectOption('#pet', 'calico');
    await page.selectOption('#clip', 'groundbait');
    await page.evaluate(async () => { elapsed = 3250; await render(); });
    assert.match(await page.locator('#after-info').textContent(), /循环打窝/);
    await page.click('#finish');
    await page.evaluate(async () => { await render(); });
    assert.match(await page.locator('#after-info').textContent(), /源姿势 7.*收尾/);
    await page.evaluate(async () => { elapsed += 130; await render(); });
    assert.match(await page.locator('#after-info').textContent(), /源姿势 8.*收尾/);
    await page.click('#restart');
    await page.click('#next');
    assert.ok(Number(await page.locator('#scrub').inputValue()) > 0);
    await page.click('#previous');
    assert.equal(Number(await page.locator('#scrub').inputValue()), 0);
    assert.deepEqual(errors, []);
    const result = { date: '2026-10-24', pets, sampledRenders: checked, errors, groundbaitFinishAndStepControls: 'passed', nativeWindows: 'not-run' };
    fs.writeFileSync(path.join(folder, 'browser-verification.json'), JSON.stringify(result, null, 2) + '\n');
    console.log(JSON.stringify(result));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
