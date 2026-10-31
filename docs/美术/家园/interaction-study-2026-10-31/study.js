const $=id=>document.getElementById(id);
const root=new URL('../../../../fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/home-room/',location.href);
const actions={idle:{duration:6000,name:'放松坐姿'},attention:{duration:2200,name:'坐着歪头'},nuzzle:{duration:3400,name:'坐着蹭手'},roll:{duration:4200,name:'放松打滚'}};
const rollTimes=[320,200,160,180,200,240,400,480,430,350,240,190,200,190,180,240];
// Preserve the original roll artwork; replace the upright endpoints with sitting.
const rollKeys=['sit',1,2,3,4,5,6,7,8,9,10,11,12,13,2,'sit'];
const state={action:'idle',elapsed:0,clock:0,playing:true,ready:false,completed:false,pointer:null,look:0,lookTarget:0,press:null};
const images=[];let room,sitting,last=performance.now();
const clamp=(v,a=0,b=1)=>Math.max(a,Math.min(b,v));
const ease=t=>{t=clamp(t);return t*t*(3-2*t)};
function curve(t,keys){if(t<=keys[0][0])return keys[0][1];for(let i=1;i<keys.length;i++)if(t<keys[i][0])return keys[i-1][1]+(keys[i][1]-keys[i-1][1])*ease((t-keys[i-1][0])/(keys[i][0]-keys[i-1][0]));return keys.at(-1)[1];}
function frameAt(t,durations){for(let i=0;i<durations.length;i++){if(t<durations[i])return i;t-=durations[i];}return durations.length-1;}
function pose(action,t,clock,look=0){
  const p={tilt:look*1.4,lean:0,raise:0,tail:Math.sin(clock/1100)*2,breath:Math.sin(clock*Math.PI*2/3600)*.7};
  if(action==='attention'){
    p.tilt+=curve(t,[[0,0],[180,0],[560,-7],[1100,-7],[1500,-2],[2200,0]]);
    p.lean=curve(t,[[0,0],[280,0],[700,-2],[1250,-2],[2200,0]]);
    p.raise=curve(t,[[0,0],[250,1],[600,-1.5],[1150,-1.5],[2200,0]]);
  }else if(action==='nuzzle'){
    p.tilt+=curve(t,[[0,0],[360,2],[850,7],[1400,4],[1980,7],[2450,3],[3400,0]]);
    p.lean=curve(t,[[0,0],[500,2],[1050,5],[1750,4],[2250,5],[3400,0]]);
    p.raise=curve(t,[[0,0],[650,-5],[1150,-7],[1650,-3],[2150,-6],[2700,-1],[3400,0]]);
    p.tail+=curve(t,[[0,0],[800,-2],[2000,2],[3400,0]]);
  }
  return p;
}
// Continuous shared deformation: no head cutout and no accumulative scale.
function warp(x,y,p){
  const head=1-ease((y-218)/98),body=1-ease((y-270)/70);
  const a=p.tilt*Math.PI/180,dx=x-272,dy=y-242,c=Math.cos(a),s=Math.sin(a);
  let nx=x+head*(c*dx-s*dy-dx)+p.lean*body;
  let ny=y+head*(s*dx+c*dy-dy+p.raise)-p.breath*body;
  const tail=(1-ease((x-178)/35))*ease((y-260)/18)*(1-ease((y-310)/25));
  nx+=p.tail*tail;ny+=p.tail*.35*tail;
  return [nx,ny];
}
function triangle(ctx,image,src,dst){
  const [p,q,r]=src,[u,v,w]=dst,a=q[0]-p[0],b=q[1]-p[1],c=r[0]-p[0],d=r[1]-p[1],det=a*d-b*c;
  const x=v[0]-u[0],y=v[1]-u[1],z=w[0]-u[0],h=w[1]-u[1];
  const m=[(x*d-z*b)/det,(y*d-h*b)/det,(z*a-x*c)/det,(h*a-y*c)/det];
  ctx.save();ctx.beginPath();ctx.moveTo(...u);ctx.lineTo(...v);ctx.lineTo(...w);ctx.closePath();ctx.clip();
  ctx.transform(...m,u[0]-m[0]*p[0]-m[2]*p[1],u[1]-m[1]*p[0]-m[3]*p[1]);ctx.drawImage(image,0,0);ctx.restore();
}
function drawWarp(ctx,image,p){
  for(let y=0;y<384;y+=24)for(let x=0;x<512;x+=32){
    const a=[x,y],b=[x+32,y],c=[x+32,y+24],d=[x,y+24];
    const v=[a,b,c,d].map(q=>warp(...q,p));triangle(ctx,image,[a,b,c],v.slice(0,3));triangle(ctx,image,[a,c,d],[v[0],v[2],v[3]]);
  }
}
function phase(){const t=state.elapsed;if(state.completed)return '回应结束 · 回到坐姿';if(state.action==='attention')return t<180?'坐着注意你':t<700?'轻轻歪头':t<1250?'看着你':'慢慢回正';if(state.action==='nuzzle')return t<650?'坐着靠近手心':t<2300?'轻蹭两下':'放松回落';if(state.action==='roll')return t<780?'坐姿低身准备':t<1400?'慢慢侧躺':t<3000?'侧躺放松':'收腿坐好';return '放松坐着 · 轻呼吸 · 尾梢慢摆';}
function rollKey(t){return rollKeys[frameAt(t,rollTimes)];}
function draw(canvas,legacy=false){
  const ctx=canvas.getContext('2d');ctx.clearRect(0,0,1200,675);ctx.drawImage(room,0,0,1200,675);
  const moving=state.action!=='idle'&&!state.completed,t=state.elapsed;
  ctx.fillStyle='#63584424';ctx.beginPath();ctx.ellipse(615,567,82,12,0,0,Math.PI*2);ctx.fill();
  ctx.save();ctx.translate(615-256*.82,565-352*.82);ctx.scale(.82,.82);
  if(legacy)ctx.drawImage(images[state.action==='idle'?0:frameAt(t,Array(16).fill(1000/12))],0,0);
  else if(state.action==='roll'&&moving&&rollKey(t)!=='sit')ctx.drawImage(images[rollKey(t)],0,0);
  else drawWarp(ctx,sitting,pose(moving?state.action:'idle',t,state.clock,state.look));
  ctx.restore();
  if(!legacy&&state.press){ctx.strokeStyle='#fff7df';ctx.lineWidth=3;ctx.beginPath();ctx.arc(state.press.x,state.press.y,15,0,Math.PI*2);ctx.stroke();}
  if($('guides').checked){ctx.strokeStyle='#725340';ctx.setLineDash([5,5]);ctx.beginPath();ctx.moveTo(500,565);ctx.lineTo(735,565);ctx.stroke();ctx.setLineDash([]);}
}
function render(){if(!state.ready)return;draw($('after'));if($('compare').checked)draw($('before'),true);$('phase').textContent=phase();$('status').textContent=actions[state.action].name+(state.completed?' · 已结束':'');$('timeline').max=actions[state.action].duration;$('timeline').value=state.elapsed;$('time').textContent=(state.elapsed/1000).toFixed(2)+' s';}
function start(action,force=false){
  if(!state.ready)return false;
  if(!force&&state.action!=='idle'&&!state.completed&&state.elapsed<actions[state.action].duration)return false;
  state.action=action;state.elapsed=0;state.completed=false;state.playing=true;$('pause').textContent='暂停';
  document.querySelectorAll('[data-action]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.action===action)));render();return true;
}
document.querySelectorAll('[data-action]').forEach(b=>b.onclick=()=>start(b.dataset.action,true));
$('pause').onclick=()=>{state.playing=!state.playing;$('pause').textContent=state.playing?'暂停':'播放';};
$('replay').onclick=()=>start(state.action,true);
$('compare').onchange=()=>{$('views').classList.toggle('compare',$('compare').checked);render();};
$('guides').onchange=render;
function seek(t){state.playing=false;$('pause').textContent='播放';state.elapsed=clamp(t,0,actions[state.action].duration);state.clock=state.elapsed;state.completed=state.action!=='idle'&&state.elapsed>=actions[state.action].duration;render();}
$('timeline').oninput=()=>seek(Number($('timeline').value));$('previous').onclick=()=>seek(state.elapsed-100);$('next').onclick=()=>seek(state.elapsed+100);
function pointer(e){const r=$('after').getBoundingClientRect();return {x:(e.clientX-r.left)*1200/r.width,y:(e.clientY-r.top)*675/r.height};}
function hit(p){return p.x>514&&p.x<709&&p.y>348&&p.y<570;}
$('after').onpointermove=e=>{const p=pointer(e);state.pointer=p;state.lookTarget=hit(p)?clamp((p.x-615)/90,-1,1):0;if(state.press&&Math.hypot(p.x-state.press.x,p.y-state.press.y)>65)state.press.cancelled=true;};
$('after').onpointerleave=()=>{state.lookTarget=0;};
$('after').onpointerdown=e=>{if(e.button!==0)return;const p=pointer(e);if(!hit(p))return;state.press={...p,started:performance.now(),triggered:false,cancelled:false};$('after').setPointerCapture(e.pointerId);};
$('after').onpointerup=()=>{if(state.press&&!state.press.triggered&&!state.press.cancelled)start('attention');state.press=null;};
$('after').onpointercancel=()=>{state.press=null;state.lookTarget=0;};
$('after').onkeydown=e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();start('attention');}};
document.addEventListener('visibilitychange',()=>{last=performance.now();state.press=null;});
function tick(now){const delta=Math.min(50,now-last);last=now;
  if(state.press&&!state.press.cancelled&&!state.press.triggered&&state.press.y<450&&now-state.press.started>=400){state.press.triggered=true;start('nuzzle');}
  if(state.playing&&state.ready&&!document.hidden){const dt=delta*Number($('speed').value);state.clock+=dt;state.look+=(state.lookTarget-state.look)*(1-Math.exp(-dt/220));
    state.elapsed+=dt;const limit=actions[state.action].duration;
    if(state.action==='idle')state.elapsed%=limit;
    else if(state.elapsed>=limit){state.completed=true;state.elapsed=Math.min(state.elapsed,limit+800);if($('repeat').checked&&state.elapsed>=limit+800)start(state.action,true);}
    render();
  }requestAnimationFrame(tick);
}
function load(url){return new Promise((resolve,reject)=>{const image=new Image();image.onload=()=>resolve(image);image.onerror=()=>reject(new Error('素材加载失败：'+url));image.src=url;});}
(async()=>{try{
  room=await load('../pastel-interior-2026-10-31/room-pastel-v5.png');
  const seatedArt=await load(new URL('cat-sitting.png',root).href);
  sitting=document.createElement('canvas');sitting.width=512;sitting.height=384;
  // Native-size art, fixed ground baseline. Never stretch the seated body to standing height.
  sitting.getContext('2d').drawImage(seatedArt,137,352-seatedArt.height);
  images.push(...await Promise.all(Array.from({length:16},(_,i)=>load(new URL(`animations/orange/pet-roll/frame-${String(i).padStart(2,'0')}.png`,root).href))));
  state.ready=true;render();requestAnimationFrame(tick);
}catch(error){$('status').textContent=error.message;}})();
window.homeInteractionStudy={state,pose,warp,start,seek,frameAt,rollTimes,rollKeys,rollKey,actions,render};
