/* Correct shared UI primitives against the actual approved CSS/SVG.
 * Run after build-cozy-journal-assets.cjs; source HTML and provenance remain editable.
 */
const fs=require('node:fs'),path=require('node:path');
const dep='/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/';
let chromium;try{({chromium}=require('playwright'))}catch{({chromium}=require(dep+'playwright'))}
const root=path.resolve(__dirname,'../..'),theme=path.join(root,'fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/journal'),sources=path.join(root,'docs/美术/手账/sources/css');
const manifest=JSON.parse(fs.readFileSync(path.join(theme,'ui-manifest.json')));fs.mkdirSync(sources,{recursive:true});
(async()=>{const browser=await chromium.launch({executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true});const page=await browser.newPage({viewport:{width:1000,height:1000},deviceScaleFactor:2});await page.goto('http://127.0.0.1:8769/docs/设计/界面/原型/手账/index.html');await page.evaluate(()=>document.fonts.ready);
await page.evaluate(()=>{S.page='shop';S.tab.shop=1;render('shop');document.getAnimations().forEach(a=>a.cancel())});
// Every recipe specifies CSS content dimensions, transparent padding, and fixed corner radius.
const recipes=[
 ['surfaces/paper-page','.journal',256,256,0,26,{background:'#fff7e9',boxShadow:'none'}],
 ['surfaces/paper-edge','.journal',256,256,12,26,{background:'transparent',border:'0',boxShadow:'0 9px 0 #ead8b9, 0 10px 0 #d9c29c'}],
 ['surfaces/paper-shadow','.journal',256,256,80,26,{background:'transparent',border:'0',boxShadow:'0 24px 44px rgba(70,40,15,.28)'}],
 ['surfaces/paper-rail','.rail',128,128,0,22,{boxShadow:'none'}],
 ['surfaces/paper-aside','.aside',128,128,0,22,{boxShadow:'none'}],
 ['surfaces/tab-index-default','.tab:not(.sel)',128,76,6,20,{}],
 ['surfaces/tab-index-selected','.tab.sel',128,76,6,20,{}],
 ['surfaces/button-quiet-default','#close',128,58,8,20,{borderRadius:'18px',boxShadow:'0 5px 0 #e3cfae'}],
 ['surfaces/button-primary-default','.btn.primary',128,58,32,20,{}],
 ['surfaces/header-pill','#coins',128,64,32,22,{}],
 ['surfaces/world-entry-plate','#world-entry',160,76,32,22,{}],
 ['surfaces/dock-board','#nav',256,108,0,24,{boxShadow:'none'}],
 ['surfaces/dock-edge','#nav',256,108,48,24,{background:'transparent',border:'0',boxShadow:'0 7px 0 #c99f6e, 0 8px 0 #a77d4f, 0 16px 26px rgba(70,40,15,.3)'}],
 ['surfaces/sticker-card','.sticker:not(.sel)',128,128,8,20,{boxShadow:'0 0 0 5px #fff, 0 0 0 7px rgba(117,71,46,.07)'}],
 ['surfaces/sticker-shadow','.sticker:not(.sel)',128,128,40,20,{background:'transparent',boxShadow:'0 8px 16px rgba(117,71,46,.16)'}],
 ['surfaces/sticker-shadow-lifted','.sticker.sel',128,128,56,20,{background:'transparent',boxShadow:'0 16px 26px rgba(117,71,46,.22)'}],
 ['surfaces/selection-outline','.sticker.sel',128,128,8,20,{background:'transparent',boxShadow:'0 0 0 5px #fff, 0 0 0 7px rgba(252,146,60,.55)'}],
 ['surfaces/polaroid-frame','.polaroid',160,128,0,8,{boxShadow:'none'}],
 ['surfaces/polaroid-shadow','.polaroid',160,128,48,8,{background:'transparent',boxShadow:'0 10px 22px rgba(80,50,20,.22)'}],
 ['decor/tape-pink-stripe','.sticker.sel .tape',104,26,0,0,{opacity:1}],
 ['decor/tape-gold-dots','.polaroid .tape',104,26,0,0,{opacity:1}],
 ['decor/tape-blue-stripe','.polaroid .tape',104,26,0,0,{opacity:1,background:'repeating-linear-gradient(90deg, rgba(93,173,226,.75) 0 9px, rgba(230,246,255,.85) 9px 18px)'}],
 ['decor/binder-ring','.ring',54,24,10,0,{},true],
 ['surfaces/progress-track','.xp',96,18,0,9,{}],
 ['surfaces/progress-fill','.xp i',96,18,0,9,{}],
];
async function capture(recipe){const [name,selector,w,h,pad,r,overrides={},pseudo=false]=recipe;
const data=await page.evaluate(({selector,w,h,pad,overrides,pseudo})=>{const target=document.querySelector(selector);if(!target)throw new Error('Missing '+selector);const cs=getComputedStyle(target),st={};for(const prop of cs)st[prop]=cs.getPropertyValue(prop);
const host=document.createElement('div');host.id='export-host';host.style.cssText=`position:fixed;left:0;top:0;width:${w+2*pad}px;height:${h+2*pad}px;z-index:9999;background:transparent;`;
const box=document.createElement('div');box.id='export-box';for(const [key,value] of Object.entries(st))box.style.setProperty(key,value);
Object.assign(box.style,{position:'absolute',left:pad+'px',top:pad+'px',right:'auto',bottom:'auto',width:w+'px',height:h+'px',minWidth:'0',maxWidth:'none',minHeight:'0',maxHeight:'none',margin:'0',transform:'none',translate:'none',rotate:'none',scale:'none',transition:'none',animation:'none',visibility:'visible',display:'block',opacity:'1',color:'transparent',zIndex:'0',...overrides});
let extra='';if(pseudo){for(const p of ['::before','::after']){const ps=getComputedStyle(target,p);if(ps.content==='none')continue;extra+='#export-box'+p+'{';for(const prop of ps)extra+=prop+':'+ps.getPropertyValue(prop)+';';extra+='}';}}
const style=document.createElement('style');style.textContent='body>*{visibility:hidden!important}#export-host,#export-box{visibility:visible!important}html,body{background:transparent!important}'+extra;
host.appendChild(box);document.body.appendChild(host);document.head.appendChild(style);return{css:box.style.cssText,extra,styleId:style.id='export-style'};},{selector,w,h,pad,overrides,pseudo});
const png=path.join(theme,name+'.png');await page.screenshot({path:png,omitBackground:true,clip:{x:0,y:0,width:w+pad*2,height:h+pad*2}});
await page.evaluate(()=>{document.querySelector('#export-host').remove();document.querySelector('#export-style').remove()});
const entry=manifest.assets.find(x=>x.name===name),border=r?Array(4).fill((r+pad)*2):[0,0,0,0];
const sourceFile=path.join(sources,name.replaceAll('/','-')+'.html');fs.writeFileSync(sourceFile,`<!doctype html><meta charset="utf-8"><style>*{box-sizing:border-box}html,body{margin:0;background:transparent}#export-box{${data.css}}${data.extra}</style><div id="export-box"></div>`);
Object.assign(entry,{width:(w+2*pad)*2,height:(h+2*pad)*2,border,source:path.relative(root,sourceFile),imageType:r?'Sliced':'Simple',contentInsets:[pad,pad,pad,pad],contentSize:[w,h],visualBasis:{selector,source:'docs/设计/界面/原型/手账/style.css',method:'actual-computed-css',overrides},status:'reference-aligned',notes:'Position outer Sprite at contentRect minus contentInsets. Do not double-add shadows; source HTML is authoritative.'});
let meta=fs.readFileSync(png+'.meta','utf8').replace(/spriteBorder: \{.*\}/,`spriteBorder: {x: ${border[0]}, y: ${border[1]}, z: ${border[2]}, w: ${border[3]}}`);fs.writeFileSync(png+'.meta',meta);
}
for(const r of recipes)await capture(r);
// States inherit the original surface; original hover/press response is transform driven.
for(const kind of ['quiet','primary'])for(const state of ['hover','pressed']){
 const base=manifest.assets.find(a=>a.name==='surfaces/button-'+kind+'-default'),entry=manifest.assets.find(a=>a.name==='surfaces/button-'+kind+'-'+state);
 const own={name:entry.name,relativePath:entry.relativePath,resourcesPath:entry.resourcesPath,guid:entry.guid};Object.assign(entry,JSON.parse(JSON.stringify(base)),own,{stateMotion:state==='pressed'?{scale:.96}:{translateY:-2},notes:'Same surface as original; animate the RectTransform, not the texture.'});fs.copyFileSync(path.join(theme,base.name+'.png'),path.join(theme,entry.name+'.png'));let m=fs.readFileSync(path.join(theme,entry.name+'.png.meta'),'utf8');const b=entry.border;fs.writeFileSync(path.join(theme,entry.name+'.png.meta'),m.replace(/spriteBorder: \{.*\}/,`spriteBorder: {x: ${b[0]}, y: ${b[1]}, z: ${b[2]}, w: ${b[3]}}`));
}
for(const kind of ['quiet','primary'])await capture(['surfaces/button-'+kind+'-disabled',kind==='primary'?'.btn.primary':'#close',128,58,32,20,{borderRadius:'18px',filter:'saturate(.25) brightness(1.08)',opacity:.75}]);
await capture(['surfaces/tab-index-hover','.tab:not(.sel)',128,76,6,20,{}]);
await capture(['textures/paper-dots','.journal',24,24,0,0,{backgroundColor:'transparent',border:'0',borderRadius:'0',boxShadow:'none'}]);
let dots=manifest.assets.find(a=>a.name==='textures/paper-dots');dots.imageType='Tiled';dots.wrapMode='Repeat';
await page.evaluate(()=>{const e=document.createElement('div');e.id='reference-examples';e.innerHTML='<div class="toast"><div class="note"></div></div><div class="toast error"><div class="note"></div></div><div class="j-dialog-paper"></div><div class="j-edit-mid"><input id="reference-input"></div><div class="stamp"></div><div class="ribbon"></div><div class="j-podium-label"></div>';document.body.append(e);});
await capture(['surfaces/toast-note','.toast:not(.error) .note',160,80,32,18,{}]);
await capture(['surfaces/toast-error-note','.toast.error .note',160,80,32,18,{}]);
await capture(['surfaces/modal-paper','.j-dialog-paper',256,256,80,24,{}]);
await capture(['surfaces/input-default','#reference-input',128,64,0,14,{}]);
await page.focus('#reference-input');await capture(['surfaces/input-focus','#reference-input',128,64,6,14,{}]);await page.locator('#reference-input').evaluate(e=>e.blur());
await capture(['decor/stamp-ring','.stamp',108,108,0,0,{borderColor:'white',background:'transparent',mixBlendMode:'normal'}]);
await capture(['decor/new-corner','.ribbon',60,28,4,0,{color:'transparent'}]);
await capture(['decor/rank-badge','.j-podium-label',96,26,0,0,{background:'white'}]);
await page.evaluate(()=>{S.sel.map=PONDS[0].id;render('map');document.getAnimations().forEach(a=>a.cancel())});
await capture(['surfaces/map-photo-frame','.map-frame',256,128,32,8,{}]);
await capture(['surfaces/category-chip','.chip',96,32,0,16,{}]);
// Preserve the actual original paper bitmap. PPU 50 makes the 256×128 source tile at 512×256 design units.
const grain=manifest.assets.find(a=>a.name==='textures/paper-grain');fs.copyFileSync(path.join(root,'docs/设计/界面/原型/手账/assets/paper.png'),path.join(theme,grain.name+'.png'));
Object.assign(grain,{width:256,height:128,pixelsPerUnit:50,border:[0,0,0,0],source:'docs/设计/界面/原型/手账/assets/paper.png',status:'reference-aligned',visualBasis:{method:'Reuse original bitmap',selector:'.journal::before'},notes:'Tile at 512×256 design units with opacity .32 and Multiply blend. Normal alpha compositing is not equivalent.'});
await page.evaluate(()=>{S.tab.settings=1;render('settings');document.getAnimations().forEach(a=>a.cancel())});
await capture(['surfaces/toggle-track','.switch',74,38,0,19,{background:'white'}]);
await capture(['decor/toggle-knob','.switch .knob',30,30,10,0,{}]);
// Original SVG geometry (the earlier manually redrawn bobber was one pixel off).
const svg=await page.evaluate(()=>bobberSvg());const sharp=require(dep+'sharp');
const variants={bobber:svg,'bobber-base':svg.replace(/<path d="M6 30a14 14 0 0 1 28 0z"[^>]*\/>/,'').replace(/<ellipse[^>]*\/>/,''),'bobber-color':'<svg viewBox="0 0 40 58"><path d="M6 30a14 14 0 0 1 28 0z" fill="white"/></svg>','bobber-highlight':'<svg viewBox="0 0 40 58"><ellipse cx="14" cy="23" rx="4" ry="2.6" fill="#fff" opacity=".65"/></svg>'};
for(const [name,body] of Object.entries(variants)){const xml=body.replace('<svg ', '<svg xmlns="http://www.w3.org/2000/svg" width="80" height="116" '),file=path.join(root,'docs/美术/手账/sources/ui/motion',name+'.svg');fs.writeFileSync(file,xml);await sharp(Buffer.from(xml)).png().toFile(path.join(theme,'motion',name+'.png'));const entry=manifest.assets.find(x=>x.name==='motion/'+name);entry.status='reference-aligned';entry.visualBasis={source:'docs/设计/界面/原型/手账/app.js',method:'bobberSvg geometry'};}
manifest.refinement={method:'Original CSS/SVG export; all remaining entries are supplemental designs, not yet exact-reference accepted',aligned:manifest.assets.filter(x=>x.status==='reference-aligned').length};fs.writeFileSync(path.join(theme,'ui-manifest.json'),JSON.stringify(manifest,null,2));await browser.close();console.log('Aligned '+manifest.refinement.aligned+' shared UI assets to original CSS/SVG.');
})().catch(e=>{console.error(e);process.exit(1)});
