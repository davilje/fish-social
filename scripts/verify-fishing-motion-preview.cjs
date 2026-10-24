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
    const clips = await page.locator('#clip option').evaluateAll(options => options.map(option => option.value));
    let checked = 0;
    for (const pet of pets) {
      await page.selectOption('#pet', pet);
      for (const clip of clips) {
        await page.selectOption('#clip', clip);
        for (const time of [0, 210, 550, 3250]) {
          await page.evaluate(async value => { elapsed = value; await render(); }, time);
          assert.match(await page.locator('#after-info').textContent(), /采样/);
          checked++;
        }
        if (pet === 'orange' && ['cast','reel','groundbait','hooked','catch','sit'].includes(clip)) {
          await page.evaluate(async value => { elapsed = value; await render(); }, clip === 'groundbait' ? 650 : clip === 'catch' ? 600 : 210);
          await page.screenshot({ path: path.join(folder, `${clip}-comparison.png`) });
        }
      }
    }
    for (const clip of ['bite','hooked','effort']) {
      await page.selectOption('#clip',clip);
      const depth=await page.evaluate(()=>{
        const current=config(),sample=current.samples[0],pose=contactPose(current,sample,0,true);
        const before=contactPose(current,null,0,false),frame=data.contact[current.clip][0];
        return {top:frame.FishTop+pose.fish,surface:pose.surface,change:pose.fish-before.fish};
      });
      assert.ok(depth.top>=depth.surface+64-.001);
      assert.ok(depth.change>=50-.001);
    }
    await page.selectOption('#clip','groundbait');
    const drops=await page.evaluate(()=>{
      const current=config();
      return [2,3,4,5,5.66,6].map(position=>contactPose(current,{...current.samples[0],Source:Math.floor(position),Position:position,Contact:data.contact.groundbait[Math.floor(position)]},Math.floor(position),true).bait);
    });
    assert.equal(drops[0],0);assert.equal(drops[1],0);assert.equal(drops[5],0);
    assert.ok(drops[2]>0 && drops[3]>drops[2] && drops[4]>drops[3]);
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
    const restPets = await page.evaluate(() => data.sleepHeads || []);
    assert.ok(restPets.includes('orange'), 'sleep face missing');
    for (const pet of restPets) {
      await page.selectOption('#pet', pet);
      await page.selectOption('#clip', 'fishing');
      await page.click('#sleep');
      await page.evaluate(async () => { elapsed += 1600; await render(); });
      const pose = await page.evaluate(() => {
        const current=config(),sample=current.samples[0],rest=restingPose(current,sample),water=contactPose(current,sample,0,true,rest);
        const matrix=rest.matrix;
        return {foot:[matrix[0]*264+matrix[2]*450+matrix[4],matrix[1]*264+matrix[3]*450+matrix[5]],blend:rest.blend,line:water.lineRest};
      });
      assert.ok(pose.foot.every((value,index)=>Math.abs(value-[264,450][index])<1e-8));
      assert.equal(pose.blend,1);assert.ok(pose.line);
      await page.click('#bite-test');
      assert.match(await page.locator('#rest-status').textContent(),/直接脱钩.*\+0/);
      if(pet==='orange')await page.screenshot({path:path.join(folder,'sleep-comparison.png')});
      await page.click('#after');
      await page.evaluate(async () => { elapsed += 900; await render(); });
      assert.equal(await page.evaluate(()=>sleepBlend()),0);
      assert.match(await page.locator('#rest-status').textContent(),/重新计时 30/);
    }
    assert.deepEqual(errors, []);
    const result = { date: '2026-10-24', pets, clips, sampledRenders: checked, restPets, restAndWake:'passed', errors, depthAndDrop: 'passed', groundbaitFinishAndStepControls: 'passed', nativeWindows: 'not-run' };
    fs.writeFileSync(path.join(folder, 'browser-verification.json'), JSON.stringify(result, null, 2) + '\n');
    console.log(JSON.stringify(result));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
