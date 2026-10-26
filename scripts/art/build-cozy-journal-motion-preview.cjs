const fs=require('node:fs'),path=require('node:path');
const {chromium}=require('playwright'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..'),out=path.join(root,'docs/美术/手账/production'),native=path.join(root,'fish-social-unity/Library/CozyRedesignPreviews');
const cases=[{id:'index',title:'左侧页签选中',page:'shop',times:[0,30,60,100,160,200]},{id:'rank',title:'排行榜选择钓友',page:'rank',times:[0,60,110,180,220]}];
(async()=>{
 fs.mkdirSync(out,{recursive:true});
 const browser=await chromium.launch({headless:true,channel:'chrome'});
 const page=await browser.newPage({viewport:{width:1672,height:941},deviceScaleFactor:2});
 await page.goto('http://127.0.0.1:8769/docs/设计/界面/原型/手账/index.html');
 await page.evaluate(async()=>{await document.fonts.ready;document.getAnimations().forEach(a=>a.cancel());});
 await page.addStyleTag({content:'#toolbar,#caption{display:none!important}#viewport{inset:0;display:block}#stage{left:0!important;top:0!important;transform:none!important;border-radius:0}*{caret-color:transparent!important}'});
 const evidence=[];
 for(const c of cases) for(const ms of c.times){
  await page.evaluate(async({kind,ms})=>{
   document.getAnimations().forEach(a=>a.cancel());
   jCloseDialog(); S.page=kind; S.open=true; S.tab[kind]=kind==='shop'?1:0; J.rankSelected='cat-flower';
   applyHeader(kind); render(kind); document.querySelector('#page').style.opacity=1;
   document.querySelectorAll('.navtab').forEach((el,i)=>el.classList.toggle('on',NAV[i].id===kind));
   const nav=NAV.findIndex(x=>x.id===kind);document.querySelector('#bobber').style.translate=`${tabCenter(nav)}px 0`;
   await document.fonts.ready;await Promise.all([...document.images].map(i=>i.decode().catch(()=>{})));
   document.getAnimations().forEach(a=>{a.pause();a.currentTime=a.effect.getTiming().iterations===Infinity?0:a.effect.getComputedTiming().endTime;});
   await new Promise(r=>requestAnimationFrame(()=>requestAnimationFrame(r)));
   window.__motionBefore=new Set(document.getAnimations());
   if(kind==='shop'){
    const tabs=[...document.querySelectorAll('.rail .tab')];
    tabs[1].classList.remove('sel');tabs[0].classList.add('sel');
    getComputedStyle(tabs[0]).translate;
   }else jSelect('rank','cat-snow');
   // Pause every new CSS transition and WAAPI animation on the same logical time.
   document.getAnimations().forEach(a=>{if(!window.__motionBefore.has(a)){a.pause();a.currentTime=ms;}});
   await Promise.all([...document.images].map(i=>i.decode().catch(()=>{})));
  },{kind:c.page,ms});
  const id=`motion-${c.id}-${String(ms).padStart(3,'0')}`;
  const screenshot=await page.screenshot();
  await sharp(screenshot).resize(1672,941).webp({quality:95}).toFile(path.join(out,id+'-web.webp'));
  await sharp(path.join(native,id+'.png')).webp({quality:95}).toFile(path.join(out,id+'.webp'));
  const source=await page.evaluate(()=>[...document.querySelectorAll('.rail .tab,.mid .sticker.sel,.aside .polaroid')].map(el=>{const c=getComputedStyle(el);return {id:el.dataset.id||el.textContent.trim().slice(0,18),translate:c.translate,scale:c.scale,rotate:c.rotate,opacity:c.opacity,color:c.color};}));
  const unity=JSON.parse(fs.readFileSync(path.join(native,id+'.geometry.json'))).nodes.filter(n=>n.active && (/(Tabs\/[^/]+$)|(JournalPhoto$)/.test(n.path)||n.tapeReveal<1));
  evidence.push({id,case:c.id,ms,source,unity});
 }
 await browser.close();
 fs.writeFileSync(path.join(out,'motion-evidence.json'),JSON.stringify({generatedAt:new Date().toISOString(),method:'Original CSS/WAAPI paused at currentTime; Unity production prefabs driven through the same runtime selection handlers with deterministic time. Index isolates selection from the concurrent category content swap.',frames:evidence},null,2)+'\n');
 fs.writeFileSync(path.join(out,'motion.html'),`<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>网页与 Unity · 动效逐帧对照</title><style>*{box-sizing:border-box}body{margin:0;background:#29231f;color:#faefdf;font:16px system-ui}header{padding:20px 24px}h1{font-size:24px}p{color:#cbbca7;line-height:1.7}a{color:#ffbc7a}button,select{font:inherit;background:#49392d;color:#fff4e5;border:1px solid #896b51;border-radius:8px;padding:8px 14px;margin-right:12px}input{width:320px;vertical-align:middle}.pair{display:grid;grid-template-columns:1fr 1fr;gap:14px;padding:0 16px 24px}.pair img{width:100%}.pair p{margin:8px 0}body.large .pair{grid-template-columns:1fr}label{margin-right:20px}@media(max-width:900px){.pair{grid-template-columns:1fr}}</style><header><h1>网页与 Unity · 动效逐帧对照</h1><p>同一次选择操作，按毫秒暂停。左侧为原网页，右侧为 Unity 正式预制体。此处播放的是采样帧，不是网页模拟 Unity。</p><p><a href="parity.html">静态布局对照</a> · <a href="motion-evidence.json">时间与姿态记录</a></p><select id="case">${cases.map(c=>`<option value="${c.id}">${c.title}</option>`).join('')}</select><button id="play">循环播放</button><button id="large">大图查看</button><label><input id="time" type="range" min="0" max="5" value="0"> <b id="ms">0ms</b></label></header><main class="pair"><div><p>原网页</p><img id="web"></div><div><p>Unity 实际渲染</p><img id="unity"></div></main><script>const cases=${JSON.stringify(cases)};let selected=cases[0],frame=0,timer;const slider=document.getElementById('time');function show(){frame=Math.min(frame,selected.times.length-1);slider.max=selected.times.length-1;slider.value=frame;const ms=selected.times[frame],base='motion-'+selected.id+'-'+String(ms).padStart(3,'0');document.getElementById('ms').textContent=ms+'ms';document.getElementById('web').src=base+'-web.webp';document.getElementById('unity').src=base+'.webp'}slider.oninput=()=>{frame=+slider.value;show()};document.getElementById('case').onchange=e=>{selected=cases.find(x=>x.id===e.target.value);frame=0;show()};document.getElementById('play').onclick=e=>{if(timer){clearInterval(timer);timer=null;e.target.textContent='循环播放'}else{timer=setInterval(()=>{frame=(frame+1)%selected.times.length;show()},350);e.target.textContent='暂停'}};document.getElementById('large').onclick=()=>document.body.classList.toggle('large');show();</script></html>`);
 console.log('Built '+evidence.length+' original/native motion frame pairs');
})().catch(e=>{console.error(e);process.exitCode=1});
