'use strict';
const {effects:activeEffects,legacyEffects=activeEffects,pets,waterContact,motion}=window.waterPreviewData;
const $=id=>document.getElementById(id),fishing='../../../desktop-overlay/OverlayResources/fishing/',fx='../../../desktop-overlay/OverlayResources/effects/fishing/v2/';
const names={orange:'橘猫',black:'黑猫',white:'白猫',cow:'奶牛猫',calico:'三花猫',blue:'蓝猫',brown:'棕猫',cream:'奶油猫',silver:'银渐层',dafeiyu:'大肥鱼',sangbiao:'狸花猫'};
for(const pet of Object.keys(pets))$('pet').add(new Option(names[pet]||pet,pet));$('pet').value='orange';
$('style').value=new URLSearchParams(location.search).get('style')==='original'?'original':'magic';
let effects=$('style').value==='magic'?activeEffects:legacyEffects;
const tiers=Object.keys(effects.tiers),canvases={};
for(const tier of tiers){const t=effects.tiers[tier],a=document.createElement('article');a.className='card';a.innerHTML=`<h2>${tier} <span>${{S:'轻点',M:'小冠',L:'扇溅',XL:'翻水'}[tier]}</span></h2><p>${t.width}px 展开 · ${t.height}px 最高点 · ${t.durationMs}ms</p><canvas width="320" height="250" aria-label="${tier} 猫咪与水花"></canvas>`;$('compare').append(a);canvases[tier]=a.querySelector('canvas');}
const images=new Map(),contacts=new Map();let errors=[],elapsed=160,playing=true,previous=performance.now(),version=0;
function image(src){if(!images.has(src))images.set(src,new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=()=>{errors.push(src);reject(new Error(src));};im.src=src;}));return images.get(src);}
function effectBase(){return $('style').value==='magic'?fx.replace('/v2/','/v4/'):fx;}
function renderAtlas(){ $('atlas').replaceChildren();for(const a of effects.assets){const div=document.createElement('div');div.className='texture';div.innerHTML=`<img src="${effectBase()+a.file}" alt="${a.id}"><span>${a.id}</span>`;$('atlas').append(div);}}
function cue(){return effects.cues.find(c=>c.id===$('cue').value)||activeEffects.cues.find(c=>c.id===$('cue').value);}
function source(){return ({FX01:'cast',FX02:'fishing',FX03:'bite',FX04:'effort',FX05:'reel',FX08:'groundbait',FX09:'reel_empty'})[cue().id];}
function contactTime(){return ({FX01:420,FX08:620,FX05:67})[cue().id]||0;}
function sampleAt(clip,time){const m=motion[clip];if(m){const frames=m.Frames;if(m.Loop&&['fishing','bite','effort'].includes(clip))time%=frames.reduce((n,f)=>n+f.DurationMs,0);let i=0;while(i<frames.length-1&&time>=frames[i].DurationMs)time-=frames[i++].DurationMs;return frames[i];}return null;}
function frameAt(clip,time){return sampleAt(clip,time)?.Source||0;}
async function catPack(pet,clip,frame){const m=pets[pet];return Promise.all(m.clips[clip].frames[frame].map(async layer=>({layer,im:await image(fishing+(m.bodyLayers.includes(layer)?pet+'/body/':'shared/')+`${clip}/${frame}/${layer}.png`)})));}
function water(ctx,width,height,y,dark){ctx.fillStyle=dark?'#2b424a':'#e6e8d6';ctx.fillRect(0,0,width,height);ctx.fillStyle=dark?'#344f58':'#bdd9d7';ctx.fillRect(0,y,width,height-y);ctx.strokeStyle=dark?'#76938c':'#90b4ac';ctx.lineWidth=.75;ctx.beginPath();ctx.moveTo(0,y);ctx.lineTo(width,y);ctx.stroke();for(let x=8;x<width;x+=64){ctx.beginPath();ctx.moveTo(x,y+22);ctx.quadraticCurveTo(x+12,y+24,x+25,y+22);ctx.stroke();}}
// Source alpha contacts are cached; impact coordinates never follow later frames.
function alphaBounds(im){if(contacts.has(im.src))return contacts.get(im.src);const c=document.createElement('canvas');c.width=im.width;c.height=im.height;const ctx=c.getContext('2d',{willReadFrequently:true});ctx.drawImage(im,0,0);const p=ctx.getImageData(0,0,c.width,c.height).data;let l=c.width,r=-1,t=c.height,b=-1;const rows=[];for(let y=0;y<c.height;y++){let sum=0,n=0;for(let x=0;x<c.width;x++)if(p[(y*c.width+x)*4+3]>8){l=Math.min(l,x);r=Math.max(r,x);t=Math.min(t,y);b=Math.max(b,y);sum+=x;n++;}if(n)rows.push([y,sum/n]);}const bounds=r<0?null:{x:(l+r+1)/2,left:l,right:r+1,top:t,bottom:b+1,rows};contacts.set(im.src,bounds);return bounds;}
// Match the published 22B 128px actor: draw height139, waterline155.168 below source origin.
const sourceSurface=155.168*512/139;
function positionAt(clip,time){return sampleAt(clip,time)?.Position||0;}
const layerOrder=['body','head','far_hand','rod','line','fish','hands','bobber','bait'];
function pose(clip,frame,position=frame,sample){const f=sample&&sample.Position!==sample.Source?sample.Contact:waterContact[clip]?.[frame],count=pets[$('pet').value].clips[clip].durationsMs.length,t=position/Math.max(1,count-1),weight=['fishing','hooked','bite','effort'].includes(clip)?1:clip==='cast'?t*t:['reel','reel_empty'].includes(clip)?(1-t)**2:0;
 const delta=Math.max(0,sourceSurface-463.5),bobber=f?Math.max(0,sourceSurface-(f.BobberY>=0?f.BobberY:463.5))*weight:0,fish=f?Math.max(delta,f.FishTop>=0?sourceSurface+64-f.FishTop:delta)*weight:0,extension=f?.FishTop>=0?fish:bobber;
 return {bobber,fish,bait:clip==='groundbait'&&position>=3&&position<6?(delta+90)*((position-3)/3)**2:delta*weight,lineScale:f&&f.LineBottom-f.LineTop>1?1+extension/(f.LineBottom-f.LineTop):1,linePivot:f?.LineTop||0};}
function drawCat(ctx,pack,x,surface,slot,clip,frame,position,sample){const s=slot/128*139/512,y=surface-sourceSurface*s,p=pose(clip,frame,position,sample);for(const {layer,im}of pack){ctx.save();ctx.translate(x,y);ctx.scale(s,s);if(layer==='line'){ctx.translate(0,p.linePivot);ctx.scale(1,p.lineScale);ctx.translate(0,-p.linePivot);}else ctx.translate(0,p[layer]||0);if(sample){ctx.transform(...sample.Root);ctx.transform(...sample.Transforms[layerOrder.indexOf(layer)]);}ctx.drawImage(im,0,0,640,512);ctx.restore();}}
function contactBounds(pack,x,surface,slot,clip,frame,position,sample){const layer=cue().id==='FX08'?'bait':['FX01','FX02','FX09'].includes(cue().id)?'bobber':'fish',contact=pack.find(p=>p.layer===layer),b=contact&&alphaBounds(contact.im);if(!b)return null;
 const p=pose(clip,frame,position,sample),s=slot/128*139/512,points=[];
 const matrixPoint=(m,x,y)=>[m[0]*x+m[2]*y+m[4],m[1]*x+m[3]*y+m[5]];
 for(const xx of [b.left,b.right])for(const yy of [b.top,b.bottom]){let point=[xx,yy];if(sample){point=matrixPoint(sample.Transforms[layerOrder.indexOf(layer)],...point);point=matrixPoint(sample.Root,...point);}point[1]+=p[layer]||0;points.push([x+point[0]*s,surface+(point[1]-sourceSurface)*s]);}
 return {left:Math.min(...points.map(v=>v[0])),right:Math.max(...points.map(v=>v[0])),top:Math.min(...points.map(v=>v[1])),bottom:Math.max(...points.map(v=>v[1]))};}
function anchor(pack,x,slot,clip,frame,position,sample){const b=contactBounds(pack,x,160,slot,clip,frame,position,sample);return b?(b.left+b.right)/2:null;}
async function drawEffect(ctx,tier,x,y,slot,time,{other=false,measure=false}={}){
 const mode=$('quality').value,c=cue(),diagnostics={count:0,ripples:0,splashes:0,drops:0,ripplePixelsAbove:0,rippleTop:null,splashTop:null};if(mode==='off'||(mode==='reduced'&&(other||c.reducedVariant==='none')))return diagnostics;
 const useTier=c.sizeTier==='object'?'S':tier,t=effects.tiers[useTier],baseDuration=c.durationMs||t.durationMs,duration=mode==='reduced'?effects.reduced.durationMs:c.id==='FX04'?Math.max(300,Math.min(600,baseDuration)):baseDuration,p=time/duration;
 if(p<0||p>=1||x==null)return diagnostics;
 const s=slot/128,variant=$('variant').value,alpha=Math.min(1,p*7)*Math.pow(1-p,.72),spread=.72+.28*Math.sin(p*Math.PI/2),rise=Math.sin(Math.PI*p)*(.8+.2*p),small=c.sizeTier==='object';
 const rippleCanvas=document.createElement('canvas');rippleCanvas.width=ctx.canvas.width;rippleCanvas.height=ctx.canvas.height;const ripple=rippleCanvas.getContext('2d',{willReadFrequently:measure});
 ripple.beginPath();ripple.rect(Math.max(0,x-58*s),y,116*s,rippleCanvas.height-y);ripple.clip();ripple.globalAlpha=alpha;
 ctx.save();ctx.beginPath();ctx.rect(Math.max(0,x-58*s),y-48*s,116*s,80*s);ctx.clip();ctx.globalAlpha=alpha;
 async function texture(kind,dx=0,dy=0,sx=spread,sy=1){const im=await image(effectBase()+`${kind}-${useTier.toLowerCase()}-${variant}.png`),target=kind==='ripple'?ripple:ctx,b=alphaBounds(im);
  // Place the complete painted ellipse below the line, then clip as a backstop.
  if(kind==='ripple')dy+=Math.max(0,44-b.top)*s*sy+1;
  target.save();target.translate(x+dx,y+dy);target.scale(s*sx,s*sy);target.drawImage(im,-64,-44);target.restore();diagnostics.count++;diagnostics[kind==='ripple'?'ripples':'splashes']++;if(kind==='splash')diagnostics.splashTop=y+dy+(b.top-44)*s*sy;}
 if(mode==='reduced'||other){await texture('ripple');if(mode==='reduced'&&t.width>28)await texture('ripple',0,3*s,.65,.65);}
 else {
  const rippleCount=small?(c.id==='FX08'?2:1):t.arcCount;
  for(let i=0;i<rippleCount;i++)await texture('ripple',small&&c.id==='FX08'?(i-.5)*5*s:0,i*3*s,spread*(1-i*.18),1-i*.13);
  if(!['FX02','FX03','FX04'].includes(c.id)){
   if(t.crownCount===1)await texture('splash',0,0,spread,Math.max(.08,rise));
   else for(let side=0;side<2;side++){ctx.save();ctx.translate(x,y);ctx.scale(s*spread,s*Math.max(.08,rise));ctx.beginPath();ctx.rect(side===0?-64:0,-44,64,64);ctx.clip();const im=await image(effectBase()+`splash-${useTier.toLowerCase()}-${variant}.png`);ctx.drawImage(im,-64,-44);ctx.restore();diagnostics.count++;diagnostics.splashes++;diagnostics.splashTop=y-t.height*rise*s;}
   const drops=small?1:$('style').value==='magic'?Math.min(2,t.dropCount):t.dropCount;
   for(let i=0;i<drops;i++){const im=await image(effectBase()+(i%2?'drop-short.png':'drop-round.png')),side=i%2?-1:1,v=.55+Math.floor(i/2)*.18,dx=side*t.width*.45*v*p*s,dy=-t.height*(.65+.25*v)*Math.sin(Math.PI*p)*s;ctx.save();ctx.translate(x+dx,y+dy);ctx.scale(s*(.48+v*.2),s*(.48+v*.2));ctx.drawImage(im,-32,-44);ctx.restore();diagnostics.count++;diagnostics.drops++;}
  }
 }
 ctx.restore();ctx.drawImage(rippleCanvas,0,0);
 if(measure){const rgba=ripple.getImageData(0,0,rippleCanvas.width,rippleCanvas.height).data;for(let yy=0;yy<rippleCanvas.height;yy++)for(let xx=0;xx<rippleCanvas.width;xx++)if(rgba[(yy*rippleCanvas.width+xx)*4+3]){if(diagnostics.rippleTop==null)diagnostics.rippleTop=yy;if(yy<y)diagnostics.ripplePixelsAbove++;}}
 return diagnostics;
}
async function render(){const own=++version,pet=$('pet').value,clip=source(),slot=Number($('slot').value),frame=frameAt(clip,elapsed),position=positionAt(clip,elapsed),sample=sampleAt(clip,elapsed),impactSample=sampleAt(clip,contactTime()),impactFrame=frameAt(clip,contactTime()),impactPosition=positionAt(clip,contactTime()),[pack,impactPack]=await Promise.all([catPack(pet,clip,frame),catPack(pet,clip,impactFrame)]),dark=$('water').value==='dark';if(own!==version)return;
 const snapshots=[],impactAnchors={};for(const tier of tiers){const canvas=canvases[tier],buffer=document.createElement('canvas');buffer.width=canvas.width;buffer.height=canvas.height;const ctx=buffer.getContext('2d'),y=160,x=110-slot*.32;water(ctx,320,250,y,dark);drawCat(ctx,pack,x,y,slot,clip,frame,position,sample);const px=anchor(impactPack,x,slot,clip,impactFrame,impactPosition,impactSample),diagnostics=await drawEffect(ctx,tier,px,y,slot,elapsed-contactTime(),{measure:true});impactAnchors[tier]=px;snapshots.push({canvas,buffer,diagnostics,tier});}
 if(own!==version)return;for(const {canvas,buffer}of snapshots)canvas.getContext('2d').drawImage(buffer,0,0);
 const canvas=$('crowd'),buffer=document.createElement('canvas');buffer.width=1200;buffer.height=390;const ctx=buffer.getContext('2d');water(ctx,1200,390,165,dark);ctx.save();ctx.translate(0,195);water(ctx,1200,195,155,dark);ctx.restore();let instances=0,groups=0;
 for(let i=0;i<20;i++){const x=14+(i%10)*118,y=i<10?165:350,crowdSlot=96;drawCat(ctx,pack,x,y,crowdSlot,clip,frame,position,sample);if(i<4){const d=await drawEffect(ctx,i===0?'XL':tiers[i],anchor(impactPack,x,crowdSlot,clip,impactFrame,impactPosition,impactSample),y,crowdSlot,elapsed-contactTime(),{other:i!==0});instances+=d.count;if(d.count)groups++;}ctx.fillStyle=dark?'#e1d6ba':'#5d716b';ctx.font='11px system-ui';ctx.fillText(i===0?'自己':'座位 '+(i+1),x+30,y+28);}
 if(own!==version)return;canvas.getContext('2d').drawImage(buffer,0,0);$('budget').textContent=`${instances} / 24 实例 · ${groups} / 8 组 · 自己预留 12 / 2`;$('time').textContent=`${Math.round(elapsed)} ms`;$('scrub').value=elapsed;window.waterPreviewStats={instances,groups,errors:[...errors],pet,slot,quality:$('quality').value,cue:cue().id,variant:$('variant').value,water:$('water').value,style:$('style').value,elapsed,renderId:own,frame,position,impactFrame,impactPosition,impactTime:contactTime(),impactAnchors,impactBounds:contactBounds(impactPack,110-slot*.32,160,slot,clip,impactFrame,impactPosition,impactSample),waterline:160,layers:Object.fromEntries(snapshots.map(s=>[s.tier,s.diagnostics])),artVersion:effects.artVersion,behaviorRevision:activeEffects.behaviorRevision,textureCount:effects.assets.length,decodedBytes:557056,decodedBytesScope:'active water texture pack'};}
function resetTime(){elapsed=contactTime()+160;}
$('style').onchange=()=>{effects=$('style').value==='magic'?activeEffects:legacyEffects;resetTime();renderAtlas();render();};
for(const id of ['pet','water','slot','cue','quality','variant'])$(id).onchange=()=>{resetTime();render();};$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'暂停':'播放';};function step(delta){playing=false;$('play').textContent='播放';elapsed=Math.max(0,Math.min(1400,elapsed+delta));render();}$('previous').onclick=()=>step(-1000/60);$('next').onclick=()=>step(1000/60);$('scrub').oninput=()=>{playing=false;$('play').textContent='播放';elapsed=Number($('scrub').value);render();};
renderAtlas();Promise.all(effects.assets.map(a=>image(effectBase()+a.file))).then(()=>render()).catch(e=>console.error(e));
let drawing=false;async function tick(now){const delta=Math.min(now-previous,80);previous=now;if(playing&&!drawing){elapsed=(elapsed+delta)%1600;drawing=true;try{await render();}finally{drawing=false;}}requestAnimationFrame(tick);}requestAnimationFrame(tick);
