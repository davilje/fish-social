// Run against the repository HTTP server (default: python3 -m http.server 8769).
const assert = require('node:assert/strict');
const { execFileSync } = require('node:child_process');
const { chromium } = require('playwright');
const base = process.env.PREVIEW_BASE_URL || 'http://127.0.0.1:8769';
const historical = process.env.PREVIEW_GIT_REF;

// Count actual content replacements, including boot before functions can be wrapped.
function observeRenders() {
  window.contentRenders = 0;
  const html = Object.getOwnPropertyDescriptor(Element.prototype, 'innerHTML');
  Object.defineProperty(Element.prototype, 'innerHTML', {
    ...html,
    set(value) {
      if (this.classList.contains('content')) window.contentRenders++;
      html.set.call(this, value);
    },
  });
}

(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  let checks = 0;
  try {
    for (const preview of ['cozy-journal', 'journal-workspace']) {
      const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
      const errors = [];
      page.on('pageerror', error => errors.push(error.message));
      await page.addInitScript(observeRenders);
      if (historical) {
        const body = execFileSync('git', ['show', `${historical}:docs/设计/界面/原型/手账/app.js`], { encoding: 'utf8' });
        await page.route('**/cozy-journal/app.js', route => route.fulfill({ body, contentType: 'text/javascript' }));
      }
      await page.goto(`${base}/docs/设计/界面/原型/${preview}/index.html`);
      await page.waitForFunction(() => document.querySelectorAll('.navtab').length > 0);
      await page.waitForTimeout(800);
      assert.equal(await page.evaluate(() => contentRenders), 1, `${preview}: boot renders once`);

      const count = await page.locator('.navtab').count();
      for (let i = 0; i < count; i++) {
        const result = await page.evaluate(async index => {
          await closePage();
          const before = contentRenders;
          const samples = [];
          const nav = document.querySelectorAll('.navtab')[index];
          nav.click();
          nav.click(); // A second click during opening must not restart it.
          const start = performance.now();
          await new Promise(resolve => {
            function frame() {
              const content = document.querySelector('.content');
              const paper = document.querySelector('#page');
              const elements = [...content.querySelectorAll('.sticker, .aside, .mid-h, .rail .tab')];
              samples.push(elements.map(el => {
                const style = getComputedStyle(el);
                return style.visibility === 'hidden' ? 0 : Number(style.opacity) * Number(getComputedStyle(paper).opacity);
              }));
              if (performance.now() - start < 750) requestAnimationFrame(frame);
              else resolve();
            }
            requestAnimationFrame(frame);
          });
          return { renders: contentRenders - before, samples, label: nav.textContent, visible: getComputedStyle(document.querySelector('.content')).visibility };
        }, i);
        assert.equal(result.renders, 1, `${preview}/${result.label}: one render`);
        assert.equal(result.visible, 'visible');
        result.samples.slice(1).forEach((sample, frame) => sample.forEach((alpha, el) => {
          assert(alpha + .05 >= result.samples[frame][el], `${preview}/${result.label}: element ${el} disappears again at frame ${frame}`);
        }));
        const before = await page.evaluate(() => contentRenders);
        await page.locator('.navtab').nth(i).click();
        assert.equal(await page.evaluate(() => contentRenders), before, 'Clicking the current page does not render again');
        const next = (i + 1) % count;
        await page.locator('.navtab').nth(next).click();
        await page.waitForTimeout(450);
        assert.equal(await page.evaluate(() => contentRenders), before + 1, 'Switching to another page renders once');
        checks++;
      }

      // Slow playback and reduced motion must also leave no cover/lock behind.
      for (const mode of ['slow', 'reduced']) {
        await page.evaluate(async mode => {
          await closePage();
          reduced = mode === 'reduced';
          setRate(mode === 'slow' ? .5 : 1);
          await go('bag');
        }, mode);
        assert.equal(await page.locator('.content').evaluate(el => getComputedStyle(el).visibility), 'visible');
        assert.equal(await page.evaluate(() => switching), false);
        checks++;
      }
      assert.deepEqual(errors, []);
      await page.close();
    }

    const page = await browser.newPage();
    await page.addInitScript(observeRenders);
    // Simulate a warm font cache and a slower workspace script: boot must wait.
    await page.addInitScript(() => { document.fonts.load = () => Promise.resolve([]); });
    await page.route('**/journal-workspace/workspace.js', async route => {
      await new Promise(resolve => setTimeout(resolve, 100));
      await route.continue();
    });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    for (const mode of ['profile', 'edit', 'compose', 'settlement']) {
      await page.goto('about:blank');
      await page.goto(`${base}/docs/设计/界面/原型/手账工作台/index.html#${mode}`);
      await page.waitForFunction(() => document.querySelectorAll('.navtab').length > 0);
      await page.waitForTimeout(750);
      assert.equal(await page.evaluate(() => contentRenders), 1, `${mode}: boot renders once`);
      const selector = { profile: '.ws-profile', edit: '#ws-name', compose: '#ws-post-text', settlement: '#ws-record-count' }[mode];
      if (mode === 'profile') assert.equal(await page.evaluate(() => S.page), 'profile');
      else assert(await page.locator(selector).isVisible(), `${mode}: initial target visible`);
      const renders = await page.evaluate(() => contentRenders);
      await page.locator('[data-workspace="edit"]').click();
      await page.waitForTimeout(650);
      assert.equal(await page.evaluate(() => contentRenders), renders + 1, `${mode}: editing opens directly, without first rendering the profile`);
      assert(await page.locator('#ws-name').isVisible());
      checks++;
    }
    assert.deepEqual(errors, []);
    console.log(`PASS: ${checks} navigation cases; both previews, frame opacity, render counts, repeated clicks, slow/reduced motion, workspace routes.`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
