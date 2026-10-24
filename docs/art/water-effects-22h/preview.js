'use strict';
const {effects,pets}=window.waterPreviewData;
const $=id=>document.getElementById(id), fishing='../../../desktop-overlay/OverlayResources/fishing/',fx='../../../desktop-overlay/OverlayResources/effects/fishing/v2/';
const names={orange:'橘猫',black:'黑猫',white:'白猫',cow:'奶牛猫',calico:'三花猫',blue:'蓝猫',brown:'棕猫',cream:'奶油猫',silver:'银渐层',dafeiyu:'大肥鱼',sangbiao:'狸花猫'};
for(const pet of Object.keys(pets))$('pet').add(new Option(names[pet]||pet,pet));$('pet').value='orange';
$('style').value=new URLSearchParams(location.search).get('style')==='magic'?'magic':'original';
const tiers=Object.keys(effects.tiers),canvases={};
for(const tier of tiers){const t=effects.tiers[tier],a=document.createElement('article');a.className='card';a.innerHTML=`<h2>${tier} <span>${{S:'轻点',M:'小冠',L:'扇溅',XL:'翻水'}[tier]}</span></h2><p>${t.width}px 展开 · ${t.height}px 最高点 · ${t.durationMs}ms</p><canvas width="320" height="250" aria-label="${tier} 猫咪与水花"></canvas>`;$('compare').append(a);canvases[tier]=a.querySelector('canvas');}

const images=new Map();let errors=[],elapsed=160,playing=true,previous=performance.now(),version=0;
function image(src){if(!images.has(src))images.set(src,new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=()=>{errors.push(src);reject(new Error(src));};im.src=src;}));return images.get(src);}
const magicAtlas='magic-v3/water-magic-atlas.png';
async function drawFxSprite(ctx,kind,tier,variant){
 if($('style').value!=='magic'){ctx.drawImage(await image(fx+`${kind}-${tier.toLowerCase()}-${variant}.png`),-64,-44);return;}
 const im=await image(magicAtlas),row=tiers.indexOf(tier),col=(kind==='ripple'?2:0)+(variant==='b'?1:0),w=im.width/4,h=im.height/4;
 // Use UV rectangles directly: preserve generated alpha and the original atlas file.
 ctx.drawImage(im,col*w,row*h,w,h,-64,-44,128,64);
}
async function renderAtlas(){
 const style=$('style').value;$('atlas').replaceChildren();
 if(style==='original'){for(const a of effects.assets){const div=document.createElement('div');div.className='texture';div.innerHTML=`<img src="${fx+a.file}" alt="${a.id}"><span>${a.id}</span>`;$('atlas').append(div);}return;}
 const im=await image(magicAtlas);if($('style').value!==style)return;
 for(const tier of tiers)for(const kind of ['splash','ripple'])for(const variant of ['a','b']){
  const div=document.createElement('div'),c=document.createElement('canvas'),span=document.createElement('span');div.className='texture';c.width=256;c.height=128;c.setAttribute('aria-label',`${tier} 水魔法 ${kind} ${variant}`);
  const row=tiers.indexOf(tier),col=(kind==='ripple'?2:0)+(variant==='b'?1:0);c.getContext('2d').drawImage(im,col*im.width/4,row*im.height/4,im.width/4,im.height/4,0,0,256,128);
  span.textContent=`水魔法 ${tier} ${kind} ${variant.toUpperCase()}`;div.append(c,span);$('atlas').append(div);
 }
}
function cue(){return effects.cues.find(c=>c.id===$('cue').value);}
function source(){const c=cue();return c.id==='FX08'?'groundbait':c.id==='FX01'?'cast':c.id==='FX02'?'fishing':c.id==='FX03'?'bite':'effort';}
function frameAt(clip,time){const ds=pets[$('pet').value].clips[clip].durationsMs;time%=ds.reduce((a,b)=>a+b,0);let i=0;while(i<ds.length-1&&time>=ds[i])time-=ds[i++];return i;}
async function catPack(pet,clip,frame){const m=pets[pet];return Promise.all(m.clips[clip].frames[frame].map(async layer=>({layer,im:await image(fishing+(m.bodyLayers.includes(layer)?pet+'/body/':'shared/')+`${clip}/${frame}/${layer}.png`)})));}
function water(ctx,width,height,y,dark){ctx.fillStyle=dark?'#2b424a':'#e6e8d6';ctx.fillRect(0,0,width,height);ctx.fillStyle=dark?'#344f58':'#bdd9d7';ctx.fillRect(0,y,width,height-y);ctx.strokeStyle=dark?'#76938c':'#90b4ac';ctx.lineWidth=.75;ctx.beginPath();ctx.moveTo(0,y);ctx.lineTo(width,y);ctx.stroke();for(let x=8;x<width;x+=64){ctx.beginPath();ctx.moveTo(x,y+22);ctx.quadraticCurveTo(x+12,y+24,x+25,y+22);ctx.stroke();}}
// Read the alpha centre of an existing shared contact layer once, never the cat centre.
const contacts=new Map();function alphaBounds(im){if(contacts.has(im.src))return contacts.get(im.src);const c=document.createElement('canvas');c.width=im.width;c.height=im.height;const x=c.getContext('2d',{willReadFrequently:true});x.drawImage(im,0,0);const p=x.getImageData(0,0,c.width,c.height).data;let l=c.width,r=-1,t=c.height,b=-1;for(let y=0;y<c.height;y++)for(let x=0;x<c.width;x++)if(p[(y*c.width+x)*4+3]>16){l=Math.min(l,x);r=Math.max(r,x);t=Math.min(t,y);b=Math.max(b,y);}const bounds=r<0?null:{x:(l+r)/2*640/im.width,top:t*512/im.height,bottom:b*512/im.height};contacts.set(im.src,bounds);return bounds;}
function drawCat(ctx,pack,x,y,slot,surface){const scale=slot/512;for(const {layer,im} of pack){ctx.save();let offset=0;const b=['fish','bobber'].includes(layer)?alphaBounds(im):null;if(b)offset=layer==='fish'?Math.max(0,surface+64*scale-(y+b.top*scale)):Math.max(0,surface-(y+(b.top+b.bottom)/2*scale));ctx.drawImage(im,x,y+offset,640*scale,512*scale);ctx.restore();}}
function anchor(pack,x,slot){const contact=pack.find(p=>p.layer===(cue().id==='FX08'?'bait':cue().sizeTier==='object'?'bobber':'fish'));const b=contact&&alphaBounds(contact.im);return b?x+b.x*slot/512:null;}
async function drawEffect(ctx,tier,x,y,slot,time,{other=false}={}){
 const mode=$('quality').value,c=cue();if(mode==='off'||(mode==='reduced'&&(other||c.reducedVariant==='none')))return 0;
 const useTier=c.sizeTier==='object'?'S':tier,t=effects.tiers[useTier],baseDuration=c.durationMs||t.durationMs,duration=mode==='reduced'?effects.reduced.durationMs:c.id==='FX04'?Math.max(300,Math.min(600,baseDuration)):baseDuration,p=time/duration;
 if(p<0||p>=1||x==null)return 0;
 const s=slot/128,variant=$('variant').value,alpha=Math.min(1,p*7)*Math.pow(1-p,.72),spread=.72+.28*Math.sin(p*Math.PI/2),rise=Math.sin(Math.PI*p)*(.8+.2*p),small=c.sizeTier==='object';
 let count=0;ctx.save();ctx.beginPath();ctx.rect(Math.max(0,x-58*s),y-48*s,116*s,80*s);ctx.clip();ctx.globalAlpha=alpha;
 const texture=async(kind,dx=0,dy=0,sx=spread,sy=1)=>{ctx.save();ctx.translate(x+dx,y+dy);ctx.scale(s*sx,s*sy);await drawFxSprite(ctx,kind,useTier,variant);ctx.restore();count++;};
 if(mode==='reduced'||other){await texture('ripple');if(mode==='reduced'&&t.width>28)await texture('ripple',0,3*s,.65,.65);ctx.restore();return count;}
 const rippleCount=small?(c.id==='FX08'?3:c.id==='FX01'?2:1):t.arcCount;
 for(let i=0;i<rippleCount;i++)await texture('ripple',small&&c.id==='FX08'?(i-1)*9*s:0,i*3*s,spread*(1-i*.18)*(c.id==='FX08'?.55:1),1-i*.13);
 if(!['FX02','FX08'].includes(c.id)){
  if(t.crownCount===1)await texture('splash',0,0,spread,Math.max(.08,rise));
  else {for(let side=0;side<2;side++){ctx.save();ctx.translate(x,y);ctx.scale(s*spread,s*Math.max(.08,rise));ctx.beginPath();ctx.rect(side===0?-64:0,-44,64,64);ctx.clip();await drawFxSprite(ctx,'splash',useTier,variant);ctx.restore();count++;}}
  const drops=small?0:t.dropCount;for(let i=0;i<drops;i++){const im=await image(fx+(i%2?'drop-short.png':'drop-round.png')),side=i%2?-1:1,v=.55+Math.floor(i/2)*.18,dx=side*t.width*.45*v*p*s,dy=-t.height*(.65+.25*v)*Math.sin(Math.PI*p)*s;ctx.save();ctx.translate(x+dx,y+dy);ctx.scale(s*(.48+v*.2),s*(.48+v*.2));if($('style').value==='magic'){ctx.shadowColor='#65eaff';ctx.shadowBlur=5;ctx.filter='brightness(1.7) saturate(1.4) hue-rotate(12deg)';}ctx.drawImage(im,-32,-44);ctx.restore();count++;}
 }
 ctx.restore();return count;
}
async function render(){const own=++version,pet=$('pet').value,clip=source(),slot=Number($('slot').value),pack=await catPack(pet,clip,frameAt(clip,elapsed)),dark=$('water').value==='dark';if(own!==version)return;
 const snapshots=[];for(const tier of tiers){const canvas=canvases[tier],buffer=document.createElement('canvas');buffer.width=canvas.width;buffer.height=canvas.height;const ctx=buffer.getContext('2d'),y=160,x=110-slot*.32;water(ctx,320,250,y,dark);drawCat(ctx,pack,x,y-slot*.88,slot,y);const px=anchor(pack,x,slot);const count=await drawEffect(ctx,tier,px,y,slot,elapsed);snapshots.push({canvas,buffer,count});}
 if(own!==version)return;for(const {canvas,buffer} of snapshots)canvas.getContext('2d').drawImage(buffer,0,0);
 const canvas=$('crowd'),buffer=document.createElement('canvas');buffer.width=1200;buffer.height=390;const ctx=buffer.getContext('2d');water(ctx,1200,390,165,dark);ctx.save();ctx.translate(0,195);water(ctx,1200,195,155,dark);ctx.restore();let instances=0,groups=0;
 for(let i=0;i<20;i++){const x=14+(i%10)*118,y=i<10?165:350,crowdSlot=96;drawCat(ctx,pack,x,y-crowdSlot*.88,crowdSlot,y);if(i<4){const count=await drawEffect(ctx,i===0?'XL':tiers[i],anchor(pack,x,crowdSlot),y,crowdSlot,elapsed,{other:i!==0});instances+=count;if(count)groups++;}ctx.fillStyle=dark?'#e1d6ba':'#5d716b';ctx.font='11px system-ui';ctx.fillText(i===0?'自己':'座位 '+(i+1),x+30,y+28);}
 if(own!==version)return;canvas.getContext('2d').drawImage(buffer,0,0);$('budget').textContent=`${instances} / 24 实例 · ${groups} / 8 组 · 自己预留 12 / 2`;$('time').textContent=`${Math.round(elapsed)} ms`;$('scrub').value=elapsed;window.waterPreviewStats={instances,groups,errors:[...errors],pet,slot,quality:$('quality').value,cue:cue().id,variant:$('variant').value,water:$('water').value,style:$('style').value,elapsed,renderId:own,frame:frameAt(clip,elapsed),textureCount:effects.assets.length+(images.has(magicAtlas)?1:0),decodedBytes:557056+(images.has(magicAtlas)?1774*887*4:0)};}
$('style').onchange=()=>{elapsed=160;renderAtlas();render();};
for(const id of ['pet','water','slot','cue','quality','variant'])$(id).onchange=()=>{elapsed=160;render();};$('play').onclick=()=>{playing=!playing;$('play').textContent=playing?'暂停':'播放';};function step(delta){playing=false;$('play').textContent='播放';elapsed=Math.max(0,Math.min(800,elapsed+delta));render();}$('previous').onclick=()=>step(-1000/60);$('next').onclick=()=>step(1000/60);$('scrub').oninput=()=>{playing=false;$('play').textContent='播放';elapsed=Number($('scrub').value);render();};
// Warm the 18 small textures once. Shared cat layers are cached by URL.
renderAtlas();
Promise.all(effects.assets.map(a=>image(fx+a.file))).then(()=>render()).catch(e=>console.error(e));
let drawing=false;async function tick(now){const delta=Math.min(now-previous,80);previous=now;if(playing&&!drawing){elapsed=(elapsed+delta)%1100;drawing=true;try{await render();}finally{drawing=false;}}requestAnimationFrame(tick);}requestAnimationFrame(tick);
