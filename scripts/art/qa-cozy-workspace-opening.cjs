// Samples the actual Web Animations timeline; no edits to the design preview.
const {chromium}=require('playwright');
const fs=require('node:fs');
const path=require('node:path');
const assert=require('node:assert/strict');
const output=path.resolve(__dirname,'../../docs/art/cozy-journal/production/workspace-motion/web-opening-timing.json');
(async()=>{const browser=await chromium.launch({channel:'chrome',headless:true});try{
const page=await browser.newPage({viewport:{width:1800,height:1100}});
await page.goto((process.env.PREVIEW_BASE_URL || 'http://127.0.0.1:8769')+'/docs/design/ui/prototype/journal-workspace/index.html#profile');
await page.waitForFunction(()=>document.querySelectorAll('.navtab').length>0);
await page.waitForTimeout(700);
const results=[];
for(const id of ['profile','edit','shop','rank','feed','ponds','map']){
const result=await page.evaluate(async id=>{
 await closePage();
 J.editing=id==='edit'; if(J.editing)wsPrepareEdit();
 const saved=play,jobs=[];
 play=(el,frames,opts={})=>{const a=saved(el,frames,opts);if(el&&a.pause){a.pause();a.currentTime=0;jobs.push({el,a,opts});}return a;};
 const opening=go(id==='edit'?'profile':id);
 const paperJob=jobs.find(j=>j.el.id==='page');
 const pick=(sel)=>{const el=document.querySelector(sel);if(!el)return null;let alpha=1;for(let n=el;n;n=n.parentElement){const st=getComputedStyle(n);if(st.visibility==='hidden')alpha=0;alpha*=Number(st.opacity);}return {alpha,opacity:Number(getComputedStyle(el).opacity),translate:getComputedStyle(el).translate};};
 const samples=[];
 for(const ms of [0,100,199]){paperJob.a.currentTime=ms;samples.push({ms,paper:pick('#page'),heading:pick('.mid-h'),aside:pick('.aside'),left:pick('.ws-edit-left'),right:pick('.ws-edit-right'),sections:[...document.querySelectorAll('.ws-profile > section, .ws-profile > .ws-metrics')].map(el=>({alpha:Number(getComputedStyle(el).opacity),translate:getComputedStyle(el).translate}))});}
 paperJob.a.finish();await opening;
 const stickers=document.querySelectorAll('.content .sticker').length;
 for(const ms of [200,240,280,300,360,400,440,600]){
  for(const j of jobs)if(j!==paperJob)j.a.currentTime=ms-200;
  samples.push({ms,paper:pick('#page'),heading:pick('.mid-h'),aside:pick('.aside'),left:pick('.ws-edit-left'),right:pick('.ws-edit-right'),sections:[...document.querySelectorAll('.ws-profile > section, .ws-profile > .ws-metrics')].map(el=>({alpha:Number(getComputedStyle(el).opacity),translate:getComputedStyle(el).translate}))});
 }
 const animations=jobs.filter(j=>j.el.closest('.content')).map(j=>({target:j.el.className,duration:j.opts.duration,delay:j.opts.delay||0}));
 for(const j of jobs)j.a.cancel();play=saved;
 return {id,stickers,animations,samples};
},id);
const aside=result.animations.find(a=>String(a.target).split(' ').includes('aside'));
if(id==='edit') {
 assert.equal(aside,undefined);
 assert.equal(result.samples.find(s=>s.ms===200).right.alpha,0);
 assert.equal(result.samples.find(s=>s.ms===200).left.alpha,0);
} else {
 assert.equal(aside.duration,160);
 assert.equal(aside.delay,80+Math.min(8,result.stickers)*20);
 for(const sample of result.samples)assert(Math.abs(sample.aside.alpha-Math.min(1,Math.max(0,(sample.ms-200-aside.delay)/160)))<.00001);
}
assert.equal(result.samples.find(s=>s.ms===100).heading.alpha,0);
assert.equal(result.samples.find(s=>s.ms===300).heading.opacity,.625);
if(id==='profile') {
 const sections=result.animations.filter(a=>['ws-card ws-identity','ws-metrics','ws-card ws-progress'].includes(a.target));
 assert.deepEqual(sections.map(a=>[a.duration,a.delay]),[[240,0],[240,40],[240,80]]);
 const values=result.samples.find(s=>s.ms===300).sections.map(s=>s.alpha);
 assert(values[0]>values[1]&&values[1]>values[2]&&values[2]>0);
}
results.push(result);
console.log(id,JSON.stringify({stickers:result.stickers,aside:result.animations.find(a=>String(a.target).split(' ').includes('aside')),alpha300:result.samples.find(s=>s.ms===300).aside,edit300:result.samples.find(s=>s.ms===300).right}));
}
// The in-page Edit/Cancel buttons use jRefresh, unlike the design toolbar's go().
await page.evaluate(async()=>{J.editing=false;await go('profile',{tab:0});});
await page.waitForTimeout(450);
for(const action of ['edit','cancel-edit']) {
 const result=await page.evaluate(async action=>{
  const saved=play,jobs=[];
  play=(el,frames,opts={})=>{const a=saved(el,frames,opts);if(el&&a.pause){a.pause();a.currentTime=0;jobs.push({el,a,opts});}return a;};
  document.querySelector('[data-ws="'+action+'"]').click();
  const exit=jobs.slice();
  for(const j of exit)j.a.currentTime=50;
  const exitAlpha=Number(getComputedStyle(document.querySelector('.ws-edit, .ws-profile')).opacity);
  for(const j of exit)j.a.finish();
  await new Promise(resolve=>setTimeout(resolve,0));
  const samples=[];
  for(const ms of [100,180,200,220,260,340,420]) {
   for(const j of jobs)if(!exit.includes(j))j.a.currentTime=ms-100;
   const alpha=sel=>{const el=document.querySelector(sel);return el?Number(getComputedStyle(el).opacity):null;};
   samples.push({ms,heading:alpha('.mid-h'),aside:alpha('.aside'),left:alpha('.ws-edit-left'),right:alpha('.ws-edit-right')});
  }
  const animations=jobs.map(j=>({target:j.el.id||j.el.className,duration:j.opts.duration,delay:j.opts.delay||0}));
  for(const j of jobs)j.a.cancel();play=saved;
  return {id:action==='edit'?'profile-to-edit':'edit-to-profile',kind:'refresh',exitAlpha,samples,animations};
 },action);
 assert(!result.animations.some(a=>a.target==='page'||a.target==='content'||String(a.target).includes('tab ')));
 assert.equal(result.exitAlpha,.5);
 assert.equal(result.samples.find(s=>s.ms===200).heading,.625);
 if(action==='edit'){assert.equal(result.samples[0].right,0);assert(result.samples.find(s=>s.ms===220).left>result.samples.find(s=>s.ms===220).right);}
 else assert.equal(result.samples.find(s=>s.ms===200).aside,.125);
 results.push(result);
}
// Repeated clicks, save validation, draft retention, and the reduced-motion path.
for(const reduce of [false,true]) {
 await page.evaluate(reduce=>{reduced=reduce;setRate(reduce?1:.5);},reduce);
 await page.evaluate(()=>{const button=document.querySelector('[data-ws="edit"]');button.click();button.click();});
 await page.waitForFunction(()=>!!document.querySelector('#ws-name')&&!switching);
 await page.locator('#ws-name').fill('');
 await page.locator('[data-ws="save-profile"]').click();
 assert(await page.locator('#ws-edit-error').textContent());
 await page.locator('#ws-name').fill('湖畔小橘');
 await page.locator('[data-edit-tab="showcase"]').click();
 assert.equal(await page.locator('#ws-name').inputValue(),'湖畔小橘');
 await page.locator('[data-ws="save-profile"]').click();
 await page.waitForFunction(()=>!!document.querySelector('.ws-profile')&&!switching);
 assert((await page.locator('.ws-identity h2').textContent()).includes('湖畔小橘'));
 if(reduce) {
  const movement=await page.locator('.ws-identity').evaluate(el=>getComputedStyle(el).translate);
  assert.equal(movement,'none');
 }
}
await page.evaluate(()=>{reduced=false;setRate(1);});
fs.writeFileSync(output,JSON.stringify(results,null,2)+'\n');
console.log('PASS: 7 openings, 2 Edit/Cancel timelines, rapid clicks, draft/save and reduced motion.');
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exit(1)});
