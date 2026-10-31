const assert=require('node:assert/strict');
const {chromium}=require('/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const base='http://127.0.0.1:8772/';
const expected={rod:[754.07,272.42],tank:[1243.97,276.18],sleep:[190.61,640.35],food:[1500,658],go:[1591,470]};
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'});
 try {
  const page=await browser.newPage({viewport:{width:1640,height:1240}});
  const errors=[],missing=[],requests=[];
  page.on('pageerror',e=>errors.push(e.message));
  page.on('response',r=>{if(r.status()>=400)missing.push(r.url())});
  page.on('request',r=>requests.push(r.url()));
  await page.goto(base+encodeURI('现行预览.html')+'?v=20261031-position-03#home-controls');
  async function home(){
   await page.waitForFunction(()=>document.querySelector('#stage').contentDocument?.querySelector('#home-base')?.dataset.ready==='true');
   return page.frames().find(f=>decodeURI(f.url()).includes('家园与浮层按钮'));
  }
  async function positions(frame){
   assert.deepEqual(await frame.locator('.home-action').evaluateAll(nodes=>Object.fromEntries(nodes.map(b=>[b.dataset.action,[parseFloat(getComputedStyle(b).left),parseFloat(getComputedStyle(b).top)]]))),expected);
   assert.match(await frame.locator('.edition').innerText(),/标注定位 03/);
   const centers=await frame.locator('.home-action').evaluateAll(nodes=>Object.fromEntries(nodes.map(b=>{const r=b.getBoundingClientRect(),s=b.closest('.home-scene').getBoundingClientRect();return [b.dataset.action,[(r.left+r.width/2-s.left)/s.width,(r.top+r.height/2-s.top)/s.height]]})));
   for(const [name,point] of Object.entries({rod:[.451,.315],tank:[.744,.319],sleep:[.114,.706]})){assert(Math.abs(centers[name][0]-point[0])<.00002,name+' x missed annotation');assert(Math.abs(centers[name][1]-point[1])<.00002,name+' y missed annotation');}
  }
  let frame=await home();await positions(frame);
  assert.equal(new URL(frame.url()).searchParams.get('v'),'20261031-position-03');
  assert(requests.some(url=>url.includes('style.css?v=20261031-position-03')));
  assert(requests.some(url=>url.includes('app.js?v=20261031-position-03')));
  // A real same-tab refresh must replace the child document and bypass its cached URL.
  await frame.evaluate(()=>document.querySelector('[data-action="sleep"]').style.setProperty('--x','327'));
  const oldFrame=frame;await page.locator('#reload-preview').click();
  await page.waitForFunction(()=>document.querySelector('#stage').src.includes('_refresh='));
  await page.waitForFunction(()=>document.querySelector('#stage').contentDocument?.querySelector('.home-action')?.style.getPropertyValue('--x')==='754.07' && document.querySelector('#stage').contentDocument?.querySelector('[data-action="sleep"]')?.style.getPropertyValue('--x')==='190.61');
  frame=await home();await positions(frame);assert(new URL(frame.url()).searchParams.has('_refresh'));
  assert.equal(await page.evaluate(()=>location.hash),'#home-controls');
  assert.equal(await page.evaluate(()=>sessionStorage.getItem('preview-src')),'docs/设计/界面/原型/家园与浮层按钮/index.html#home');
  await frame.locator('.runtime-shots').evaluate(el=>el.open=true);
  await frame.locator('.runtime-shots img').first().scrollIntoViewIfNeeded();
  await frame.waitForFunction(()=>[...document.querySelectorAll('.runtime-shots img')].every(img=>img.complete&&img.naturalWidth>0));
  assert(requests.some(url=>url.includes('runtime-home.webp?v=20261031-position-03')));
  await frame.locator('.runtime-shots').evaluate(el=>el.open=false);
  await frame.locator('.home-wrap').scrollIntoViewIfNeeded();
  await page.mouse.move(10,10);await frame.locator('.home-wrap').screenshot({path:'/Users/davilje/fish-social/output/home-controls-annotations-2026-10-31/home-current.png'});
  await page.locator('[data-route="overlay-controls"]').click();
  await page.waitForFunction(()=>document.querySelector('#stage').contentDocument?.querySelector('#overlay-panel')?.hidden===false);
  assert.equal(new URL(await page.locator('#stage').getAttribute('src')).hash,'#overlay');
  await page.locator('[data-route="home-controls"]').click();frame=await home();await positions(frame);
  // Stable legacy selection survives the revision change even without a hash.
  await page.goto(base+encodeURI('现行预览.html'));frame=await home();await positions(frame);
  assert.equal(await page.evaluate(()=>location.hash),'#home-controls');
  assert.deepEqual(errors,[]);assert.deepEqual(missing,[]);
  console.log('PASS: versioned iframe/CSS/JS/screenshots; same-tab forced refresh replaces stale layout; 5 computed positions; stable routes and saved selection; no JS errors or 404.');
 } finally {await browser.close()}
})().catch(e=>{console.error(e);process.exit(1)});
