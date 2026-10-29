const { chromium } = require('/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'});
 const page=await browser.newPage({viewport:{width:1600,height:1000}});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 const root='http://127.0.0.1:8772/';
 const hub=root+encodeURI('现行预览.html');
 const selected=root+encodeURI('docs/美术/登录/cats-selected-2026-10-29/index.html');
 async function loaded(){
  await page.waitForURL(u=>decodeURI(u.pathname)==='/现行预览.html'&&u.hash==='#login');
  await page.waitForFunction(()=>document.querySelector('#stage').contentDocument?.querySelector('#scene')?.complete && document.querySelector('#stage').contentDocument.querySelector('#scene').naturalWidth>0);
  const current=await page.locator('button[aria-current=true]').textContent();
  if(current!=='五猫完整窗口')throw Error('Wrong selection: '+current);
 }
 await page.goto(hub+'#login');await loaded();
 for(const size of ['1920x1080','1280x720','1600x900','2560x1440','1280x800','1024x768','2560x1080']){
  const f=page.frameLocator('#stage');await f.locator('#resolution').selectOption(size);
  await page.waitForFunction(size=>{const img=document.querySelector('#stage').contentDocument.querySelector('#scene');return img.complete&&img.naturalWidth>0&&img.getAttribute('src')===`unity-login-${size}.png`;},size);
 }
 for(const mode of ['art','before','unity']){
  await page.frameLocator('#stage').locator(`[data-mode=${mode}]`).click();
  await page.waitForFunction(()=>{const img=document.querySelector('#stage').contentDocument.querySelector('#scene');return img.complete&&img.naturalWidth>0;});
 }
 await page.frameLocator('#stage').locator('#resolution').selectOption('1920x1080');
 await page.screenshot({path:'/Users/davilje/fish-social/output/login-preview-integration-2026-10-30/unified-login.png',fullPage:true});
 for(const version of ['cats-v4','cats-v5','cats-v6','cats-selected-2026-10-29']){
  await page.goto(root+encodeURI(`docs/美术/登录/${version}/index.html`));await loaded();
 }
 await page.evaluate(()=>sessionStorage.setItem('preview-src','docs/美术/登录/cats-v4/index.html'));
 await page.goto(hub);await loaded();
 await page.locator('button[data-src*="page=bag"]').click();
 await page.waitForFunction(()=>document.querySelector('#stage').getAttribute('src').includes('page=bag'));
 if(new URL(page.url()).hash)throw Error('Stale login hash after changing tab');
 await page.locator('button[data-route=login]').click();await loaded();
 if(errors.length)throw Error(errors.join('\n'));
 console.log('PASS: unified login, 7 resolutions, 3 modes, 4 legacy URLs, saved v4 migration, tab navigation; page errors: 0');
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
