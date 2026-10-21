/* Export actual prototype paint layers + editable text geometry, not a redrawn mockup.
 * Reference assets are confined to isolated Unity samples; application bindings stay separate.
 */
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const dep='/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/';
let chromium,sharp;try{({chromium}=require('playwright'));sharp=require('sharp')}catch{({chromium}=require(dep+'playwright'));sharp=require(dep+'sharp')}
const root=path.resolve(__dirname,'../..'),out=path.join(root,'fish-social-unity/Assets/Desktop/JournalSamples/ReferenceAssets');
const report=path.join(root,'docs/art/cozy-journal/previews'),source=path.join(root,'docs/design/ui/prototype/cozy-journal');
fs.mkdirSync(out,{recursive:true});fs.mkdirSync(report,{recursive:true});
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
(async()=>{const browser=await chromium.launch({executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true});
const page=await browser.newPage({viewport:{width:1672,height:941},deviceScaleFactor:2});
await page.goto('http://127.0.0.1:8769/docs/design/ui/prototype/cozy-journal/index.html');await page.evaluate(async()=>{await document.fonts.ready;document.getAnimations().forEach(a=>a.cancel());play=()=>DONE;});
await page.addStyleTag({content:`#toolbar,#caption{display:none!important}html,body,#viewport{background:transparent!important}#viewport{inset:0;display:block}#stage{position:absolute;left:0!important;top:0!important;transform:none!important;border-radius:0}#stage *{animation:none!important;transition:none!important}*{caret-color:transparent!important}#motion,#celebrate{display:none!important}`});
const result={schema:1,designResolution:[1672,941],exportScale:2,sourceFiles:['index.html','style.css','pages.css','app.js','pages.js'].map(f=>({path:path.relative(root,path.join(source,f)),sha256:hash(fs.readFileSync(path.join(source,f)))})),pages:[]};
for(const [kind,tab] of [['shop',1],['map',0],['settings',0],['settings-notify',1],['bag',0],['equip',0],['ach',0],['rank',0],['profile',0],['feed',0],['ponds',0]]){
const actual=kind.startsWith('settings')?'settings':kind;
await page.evaluate(({actual,tab})=>{S.page=actual;S.open=true;S.tab[actual]=tab;if(actual==='map')S.sel.map=PONDS.find(p=>!p.locked).id;applyHeader(actual);render(actual);document.querySelector('#page').style.opacity=1;document.querySelectorAll('.navtab').forEach((el,i)=>el.classList.toggle('on',NAV[i].id===actual));const n=NAV.findIndex(x=>x.id===actual);document.querySelector('#bobber').style.opacity=n<0?'0':'1';if(n>=0)document.querySelector('#bobber').style.translate=`${tabCenter(n)}px 0`;document.getAnimations().forEach(a=>{if(a.effect.getTiming().iterations===Infinity){a.pause();a.currentTime=0}else a.finish()});scale=1;}, {actual,tab});
await page.evaluate(async()=>{await Promise.all([...document.images].map(i=>i.decode().catch(()=>{})));await document.fonts.ready;});
await page.screenshot({path:path.join(report,kind+'-reference-2x.png')});
await sharp(path.join(report,kind+'-reference-2x.png')).resize(1672,941).png().toFile(path.join(report,kind+'-reference.png'));
const nodes=await page.evaluate(()=>{
const stage=document.querySelector('#stage'),els=[...stage.querySelectorAll('*')].filter(e=>!e.closest('svg')||e.tagName.toLowerCase()==='svg');
const visible=c=>c!=='rgba(0, 0, 0, 0)'&&c!=='transparent'&&c!=='';
let seq=0; const items=[];
function textLines(node,el,cs){
 const s=node.textContent;if(!s.trim())return[];const groups=[];
 for(let i=0;i<s.length;i++){let range=document.createRange();range.setStart(node,i);range.setEnd(node,i+1);let r=range.getBoundingClientRect();if(r.width<.01||r.height<.01)continue;const previous=groups.at(-1);if(previous&&Math.abs(previous.y-r.top)<4){previous.text+=s[i];previous.right=r.right;previous.height=Math.max(previous.height,r.height);}else groups.push({text:s[i],x:r.left,y:r.top,right:r.right,height:r.height});}
 let opacity=1,angle=0;for(let p=el;p&&p!==stage;p=p.parentElement){const st=getComputedStyle(p);opacity*=Number(st.opacity);angle+=parseFloat(st.rotate)||0;if(st.transform!=='none'){const m=new DOMMatrix(st.transform);angle+=Math.atan2(m.b,m.a)*180/Math.PI;}}
 let clip={x:0,y:0,right:1672,bottom:941};for(let p=el.parentElement;p&&p!==stage;p=p.parentElement){const c=getComputedStyle(p),r=p.getBoundingClientRect();if(/hidden|auto|scroll|clip/.test(c.overflowX)){clip.x=Math.max(clip.x,r.left);clip.right=Math.min(clip.right,r.right)}if(/hidden|auto|scroll|clip/.test(c.overflowY)){clip.y=Math.max(clip.y,r.top);clip.bottom=Math.min(clip.bottom,r.bottom)}}
 return groups.filter(g=>g.x<clip.right&&g.right>clip.x&&g.y<clip.bottom&&g.y+g.height>clip.y&&g.text.trim()&&g.x<1672&&g.y<941&&g.right>0&&g.y+g.height>0).map(g=>({...g,clip,width:g.right-g.x,size:parseFloat(cs.fontSize),weight:cs.fontWeight,color:cs.color,opacity,angle,fontFamily:cs.fontFamily}));
}
function walk(el){
 if(el!==stage&&el.closest('svg')&&el.tagName.toLowerCase()!=='svg')return;
 const cs=getComputedStyle(el),r=el.getBoundingClientRect();if(cs.display==='none'||cs.visibility==='hidden'||Number(cs.opacity)===0 )return;
 const id='ref-'+seq++;el.dataset.ref=id;
 const border=['Top','Right','Bottom','Left'].some(s=>parseFloat(cs['border'+s+'Width'])>0&&visible(cs['border'+s+'Color'])&&cs['border'+s+'Style']!=='none');
 const pseudo=['::before','::after'].some(p=>{const c=getComputedStyle(el,p);return c.content!=='none'&&c.display!=='none'&&c.visibility!=='hidden'});
 const tag=el.tagName.toLowerCase(),visual=r.width>0&&r.height>0&&(tag==='img'||tag==='svg'||visible(cs.backgroundColor)||cs.backgroundImage!=='none'||cs.boxShadow!=='none'||border||pseudo);
 const entry={id,name:el.id||[tag,...el.classList].join('.'),selector:el.id?'#'+el.id:tag+'.'+[...el.classList].join('.'),visual,tag,rect:{x:r.left,y:r.top,width:r.width,height:r.height},source:el.currentSrc||el.getAttribute('src')||null,styles:{background:cs.background,border:cs.border,borderRadius:cs.borderRadius,boxShadow:cs.boxShadow,padding:cs.padding,rotate:cs.rotate,transform:cs.transform,translate:cs.translate},interactive:el.matches('.btn,.tab,.navtab,.round,#close,.sticker,.seg button,.switch,.plus,.buoy,.claim'),texts:[]};
 items.push(entry);
 if(tag==='svg'||tag==='img')return;
 for(const child of el.childNodes)if(child.nodeType===Node.TEXT_NODE)entry.texts.push(...textLines(child,el,cs));
 if(tag==='input'&&(el.value||el.placeholder)){const size=parseFloat(cs.fontSize),height=size*1.2;entry.texts.push({text:el.value||el.placeholder,x:r.left+parseFloat(cs.paddingLeft)+parseFloat(cs.borderLeftWidth),y:r.top+(r.height-height)/2,width:r.width-parseFloat(cs.paddingLeft)-parseFloat(cs.paddingRight),height,size,weight:cs.fontWeight,color:el.value?cs.color:getComputedStyle(el,'::placeholder').color,opacity:1,angle:0,fontFamily:cs.fontFamily});}
 const stack=e=>{const c=getComputedStyle(e),z=parseInt(c.zIndex);return Number.isFinite(z)?(z<0?-10000+z:20000+z):(c.position==='static'?0:10000)};
 [...el.children].sort((a,b)=>stack(a)-stack(b)).forEach(walk);
}
walk(stage);return items;
});
await page.addStyleTag({content:`#stage,#stage *{visibility:hidden!important;-webkit-text-fill-color:transparent!important}#stage [data-capture],#stage [data-capture]::before,#stage [data-capture]::after,#stage svg[data-capture] *{visibility:visible!important}`});
let previous=null,visuals=0;
for(const item of nodes){
 if(!item.visual)continue;
 const r=item.rect,pad=64;const x=Math.max(0,Math.floor(r.x-pad)),y=Math.max(0,Math.floor(r.y-pad)),right=Math.min(1672,Math.ceil(r.x+r.width+pad)),bottom=Math.min(941,Math.ceil(r.y+r.height+pad));if(right<=x||bottom<=y)continue;
 await page.evaluate(({id,previous})=>{if(previous)document.querySelector(`[data-ref="${previous}"]`)?.removeAttribute('data-capture');document.querySelector(`[data-ref="${id}"]`).setAttribute('data-capture','')},{id:item.id,previous});previous=item.id;
 const png=await page.screenshot({omitBackground:true,clip:{x,y,width:right-x,height:bottom-y}}),raw=await sharp(png).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let minx=raw.info.width,miny=raw.info.height,maxx=-1,maxy=-1;
 for(let yy=0;yy<raw.info.height;yy++)for(let xx=0;xx<raw.info.width;xx++)if(raw.data[(yy*raw.info.width+xx)*4+3]>0){minx=Math.min(minx,xx);miny=Math.min(miny,yy);maxx=Math.max(maxx,xx);maxy=Math.max(maxy,yy)}
 if(maxx<0)continue;
 const cropped=await sharp(png).extract({left:minx,top:miny,width:maxx-minx+1,height:maxy-miny+1}).png().toBuffer();
 const file=hash(cropped).slice(0,20)+'.png';fs.writeFileSync(path.join(out,file),cropped);
 item.asset='Assets/Desktop/JournalSamples/ReferenceAssets/'+file;item.paintRect={x:x+minx/2,y:y+miny/2,width:(maxx-minx+1)/2,height:(maxy-miny+1)/2};visuals++;
}
await page.evaluate(()=>{[...document.head.querySelectorAll('style')].at(-1).remove();document.querySelector('[data-capture]')?.removeAttribute('data-capture');});
result.pages.push({id:kind,tab,nodes});fs.writeFileSync(path.join(out,'layouts.json'),JSON.stringify(result,null,2));
console.log(`${kind}: ${visuals} separate paint layers, ${nodes.reduce((n,x)=>n+x.texts.length,0)} editable text lines`);
}
const used=new Set(result.pages.flatMap(p=>p.nodes.filter(n=>n.asset).map(n=>path.basename(n.asset))));for(const file of fs.readdirSync(out)){if(/^[a-f0-9]{20}\.png$/.test(file)&&!used.has(file)){fs.unlinkSync(path.join(out,file));if(fs.existsSync(path.join(out,file+'.meta')))fs.unlinkSync(path.join(out,file+'.meta'));}}
await browser.close();console.log('Reference extraction complete.');
})().catch(e=>{console.error(e);process.exit(1)});
