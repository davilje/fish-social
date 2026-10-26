// Preserve the original CSS shadow exactly, at the original fixed 500x380 design size.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const dep='/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/';
const {chromium}=require(dep+'playwright');
const root=path.resolve(__dirname,'../..');
(async()=>{
 const browser=await chromium.launch({executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true});
 const page=await browser.newPage({viewport:{width:660,height:540},deviceScaleFactor:2});
 await page.goto('http://127.0.0.1:8769/docs/设计/界面/原型/手账/index.html');
 await page.evaluate(()=>{
  document.getAnimations().forEach(a=>a.cancel());
  document.body.innerHTML='<div id="celebrate" class="show"><div class="card"></div></div>';
 });
 await page.addStyleTag({content:'html,body{margin:0;background:transparent!important}#celebrate{position:absolute;inset:0;opacity:1}#celebrate .card{left:80px;top:80px;rotate:none;opacity:1}'});
 const dir=path.join(root,'fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/journal/motion');
 const png=path.join(dir,'celebration-paper.png');
 await page.screenshot({path:png,omitBackground:true});
 if(!fs.existsSync(png+'.meta')){
  let meta=fs.readFileSync(path.join(dir,'bobber.png.meta'),'utf8');
  meta=meta.replace(/^guid: .+$/m,'guid: '+crypto.randomBytes(16).toString('hex')).replace(/spriteBorder: \{.*\}/,'spriteBorder: {x: 0, y: 0, z: 0, w: 0}');
  fs.writeFileSync(png+'.meta',meta);
 }
 const source=path.join(root,'docs/美术/手账/sources/ui/motion/celebration-paper.json');
 fs.writeFileSync(source,JSON.stringify({source:'docs/设计/界面/原型/手账/style.css',selector:'#celebrate .card',contentSize:[500,380],contentInsets:[80,80,80,80],exportScale:2,output:'journal/motion/celebration-paper.png',method:'Original CSS screenshot; no text, rotation or dynamic state',sha256:crypto.createHash('sha256').update(fs.readFileSync(png)).digest('hex')},null,2));
 await browser.close();console.log('Exported original celebration paper including white outline and 24px/40px shadow.');
})().catch(e=>{console.error(e);process.exit(1)});
