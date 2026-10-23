const {chromium}=require('playwright');
const assert=require('node:assert/strict');
const fs=require('node:fs');
(async()=>{
 const browser=await chromium.launch({channel:'chrome',headless:true});
 try {
  const page=await browser.newPage({viewport:{width:1800,height:1100}}),errors=[],results=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.goto((process.env.PREVIEW_BASE_URL||'http://127.0.0.1:8769')+'/docs/design/ui/prototype/journal-workspace/index.html#edit');
  await page.waitForSelector('#ws-name');await page.waitForTimeout(750);
  const save=await page.evaluate(async()=>{
   const original=play,jobs=[];
   play=(el,frames,opts={})=>{const a=original(el,frames,opts);a.pause();a.currentTime=0;jobs.push({el,a,opts});return a;};
   document.querySelector('[data-ws="save-profile"]').click();
   document.querySelector('[data-ws="save-profile"]').click();
   const confirm=jobs[0];confirm.a.currentTime=120;
   const before={label:confirm.el.textContent,alpha:+getComputedStyle(confirm.el).opacity,editing:!!document.querySelector('.ws-edit'),disabled:document.querySelector('[data-ws="save-profile"]').disabled,jobs:jobs.length};
   confirm.a.finish();await new Promise(r=>setTimeout(r,0));
   const exits=jobs.slice(1);exits.forEach(j=>j.a.currentTime=50);
   const exitAlpha=+getComputedStyle(document.querySelector('.ws-edit')).opacity;
   exits.forEach(j=>j.a.finish());await new Promise(r=>setTimeout(r,0));
   const after={profile:!!document.querySelector('.ws-profile'),bodyGroups:jobs.filter(j=>j.opts.duration===240&&j.el.parentElement?.classList.contains('ws-profile')).length};
   jobs.forEach(j=>j.a.finish());play=original;
   return {before,exitAlpha,after};
  });
  assert.equal(save.before.label,'✓ 已保存');assert(save.before.alpha>.35&&save.before.alpha<1);assert(save.before.editing&&save.before.disabled);assert.equal(save.before.jobs,1);assert.equal(save.exitAlpha,.5);assert(save.after.profile);assert.equal(save.after.bodyGroups,3);results.push({save});
  const settlement=await page.evaluate(()=>{
   const original=play,jobs=[];
   play=(el,frames,opts={})=>{const a=original(el,frames,opts);a.pause();a.currentTime=0;jobs.push({el,a,opts});return a;};
   wsSettlement();
   const sections=jobs.filter(j=>j.opts.duration===240);
   const samples=[0,200,300,400,680].map(ms=>{jobs.forEach(j=>j.a.currentTime=ms);return {ms,sections:sections.map(j=>+getComputedStyle(j.el).opacity)};});
   const delays=sections.map(j=>j.opts.delay);jobs.forEach(j=>j.a.finish());play=original;
   return {delays,samples};
  });
  assert.deepEqual(settlement.delays,[200,240,280,320,360,400,440]);
  assert(settlement.samples[1].sections.every(a=>a===0));
  assert(settlement.samples[2].sections[0]>settlement.samples[2].sections[1]);
  assert(settlement.samples[4].sections.every(a=>a===1));results.push({settlement});
  // Actual close button and Escape, including closing before entrance has finished.
  for(const reducedMode of [false,true]) {
   await page.evaluate(async reduce=>{await jCloseDialog();reduced=reduce;wsSettlement();},reducedMode);
   await page.waitForTimeout(50);
   const close=await page.evaluate(async()=>{
    const modal=document.querySelector('#j-dialog');let resolved=false;
    const first=jCloseDialog(),second=jCloseDialog();first.then(()=>resolved=true);
    const during={retained:document.querySelector('#j-dialog')===modal,same:first===second,blocked:[...document.querySelectorAll('#stage > [data-j-inert]')].every(el=>el.inert)};
    await first;return {during,resolved,removed:!document.querySelector('#j-dialog'),unblocked:!document.querySelector('#stage > [data-j-inert]')};
   });
   assert(close.during.retained&&close.during.same&&close.during.blocked&&close.resolved&&close.removed&&close.unblocked);
   await page.evaluate(()=>wsSettlement());await page.keyboard.press('Escape');await page.waitForSelector('#j-dialog',{state:'detached'});
   results.push({reducedMode,close});
  }
  assert.deepEqual(errors,[]);
  fs.writeFileSync('docs/art/cozy-journal/production/workspace-motion/web-completion-timing.json',JSON.stringify({result:'PASS',results,errors},null,2)+'\n');
  console.log('PASS: save confirmation/return, settlement 7-section timeline, early/repeated close, Escape and reduced motion.');
 } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exit(1);});
