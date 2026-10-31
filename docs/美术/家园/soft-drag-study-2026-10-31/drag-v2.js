import {LayeredCat,partsSpec,clamp} from './physics-v2.js?v=joint-contact-2';
const $=id=>document.getElementById(id),canvas=$('pet'),fx=$('fx').getContext('2d');
const gl=canvas.getContext('webgl',{alpha:true,antialias:true,premultipliedAlpha:true});
const state={ready:false,cat:new LayeredCat(),breed:'orange',demo:null,pointerId:null,paused:false};
const names={orange:'橘猫',calico:'三花猫',blue:'英短蓝猫'},art={};
let program,buffer,rootFade,last=performance.now(),accumulator=0;
function shader(type,source){const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(s));return s;}
function initGL(){if(!gl)throw Error('此浏览器未启用 WebGL，请启用图形加速后刷新。');program=gl.createProgram();
 gl.attachShader(program,shader(gl.VERTEX_SHADER,'attribute vec4 vertex; varying vec2 uv; void main(){uv=vertex.zw;gl_Position=vec4(vertex.x/600.0-1.0,1.0-vertex.y/337.5,0.0,1.0);}'));
 gl.attachShader(program,shader(gl.FRAGMENT_SHADER,'precision mediump float; varying vec2 uv; uniform sampler2D art; uniform float rootFade; void main(){gl_FragColor=texture2D(art,uv)*mix(1.0,smoothstep(0.10,0.32,uv.y),rootFade);}'));
 gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(program));gl.useProgram(program);rootFade=gl.getUniformLocation(program,'rootFade');buffer=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,buffer);const loc=gl.getAttribLocation(program,'vertex');gl.enableVertexAttribArray(loc);gl.vertexAttribPointer(loc,4,gl.FLOAT,false,16,0);gl.uniform1i(gl.getUniformLocation(program,'art'),0);gl.enable(gl.BLEND);gl.blendFunc(gl.ONE,gl.ONE_MINUS_SRC_ALPHA);gl.clearColor(0,0,0,0);gl.viewport(0,0,1200,675);
}
function load(url){return new Promise((resolve,reject)=>{const i=new Image();i.onload=()=>resolve(i);i.onerror=()=>reject(Error('载入失败：'+url));i.src=url;});}
function makePart(image){
 const c=document.createElement('canvas');c.width=image.width;c.height=image.height;c.getContext('2d').drawImage(image,0,0);
 const texture=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,texture);gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL,true);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,c);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);
 return {canvas:c,texture,pixels:c.getContext('2d').getImageData(0,0,c.width,c.height).data};
}
function vertices(id){const a=[],nx=id==='torso'?8:1,ny=id==='torso'?24:1;const add=(u,v)=>a.push(...state.cat.partPoint(id,u,v),u,v);
 for(let j=0;j<ny;j++)for(let i=0;i<nx;i++){const u=i/nx,r=(i+1)/nx,v=j/ny,b=(j+1)/ny;add(u,v);add(r,v);add(r,b);add(u,v);add(r,b);add(u,b);}return new Float32Array(a);}
function render(){if(!state.ready)return;const cat=state.cat;gl.clear(gl.COLOR_BUFFER_BIT);fx.clearRect(0,0,1200,675);
 const height=Math.max(0,565-cat.supportBottom());fx.fillStyle=`rgba(87,67,43,${.16*Math.exp(-height/270)})`;fx.beginPath();fx.ellipse(cat.body(.5,.8)[0],567,65/(1+height/800),6,0,0,Math.PI*2);fx.fill();
 for(const item of cat.items()){
  gl.uniform1f(rootFade,item.id.endsWith('Arm')?1:0);gl.bindTexture(gl.TEXTURE_2D,art[state.breed][item.id].texture);const v=vertices(item.id);gl.bufferData(gl.ARRAY_BUFFER,v,gl.DYNAMIC_DRAW);gl.drawArrays(gl.TRIANGLES,0,v.length/4);
  if($('mesh').checked){fx.strokeStyle=item.id==='torso'?'#298174aa':'#ac664ba0';fx.lineWidth=1;fx.beginPath();item.points.forEach((p,i)=>i?fx.lineTo(...p):fx.moveTo(...p));fx.closePath();fx.stroke();if(item.id==='torso'){for(let j=0;j<=12;j++){fx.beginPath();fx.moveTo(...cat.body(0,j/12));fx.lineTo(...cat.body(1,j/12));fx.stroke();}}else{const s=partsSpec[item.id],p=cat.partPoint(item.id,...s.pivot);fx.fillStyle='#d27534';fx.beginPath();fx.arc(...p,3,0,Math.PI*2);fx.fill();}}
 }
 if(cat.grab){fx.strokeStyle='#fff9df';fx.lineWidth=2;fx.beginPath();fx.arc(cat.grab.x,cat.grab.y,10,0,Math.PI*2);fx.stroke();}
 $('phase').textContent=cat.grab?'拎起 · 躯干伸长 / 四肢独立下垂':cat.grounded?(cat.air>.05?'落地 · 收腿与余摆':'放松坐着'):'松手 · 惯性下落';$('status').textContent=names[state.breed]+' · '+(state.demo!==null?'演示中':'七部件独立');
}
function point(e){const r=canvas.getBoundingClientRect();return {x:(e.clientX-r.left)*1200/r.width,y:(e.clientY-r.top)*675/r.height};}
function pick(p){for(const {id} of [...state.cat.items()].reverse()){
 let u,v;if(id==='torso'){v=(p.y-state.cat.y)/state.cat.length.p;const center=state.cat.body(.5,v);u=.5+(p.x-center[0])/138;}
 else{const o=state.cat.partPoint(id,0,0),a=state.cat.partPoint(id,1,0),b=state.cat.partPoint(id,0,1),ax=a[0]-o[0],ay=a[1]-o[1],bx=b[0]-o[0],by=b[1]-o[1],dx=p.x-o[0],dy=p.y-o[1],det=ax*by-ay*bx;u=(dx*by-dy*bx)/det;v=(dy*ax-dx*ay)/det;}
 if(u<0||u>1||v<0||v>1||(id.endsWith('Arm')&&v<.21))continue;const a=art[state.breed][id],x=clamp(Math.floor(u*a.canvas.width),0,a.canvas.width-1),y=clamp(Math.floor(v*a.canvas.height),0,a.canvas.height-1);if(a.pixels[(y*a.canvas.width+x)*4+3]>100)return {id,u,v};
 }return null;}
function release(){state.cat.release();const id=state.pointerId;state.pointerId=null;if(id!==null&&canvas.hasPointerCapture(id))canvas.releasePointerCapture(id);canvas.classList.remove('dragging');}
canvas.onpointerdown=e=>{if(!state.ready||e.button!==0||state.pointerId!==null)return;const p=point(e),hit=pick(p);if(!hit)return;state.demo=null;state.pointerId=e.pointerId;state.cat.begin(p.x,p.y,hit);canvas.setPointerCapture(e.pointerId);canvas.classList.add('dragging');e.preventDefault();};
canvas.onpointermove=e=>{if(!state.ready)return;const p=point(e);if(e.pointerId===state.pointerId)state.cat.move(p.x,p.y);else canvas.style.cursor=pick(p)?'grab':'default';};
canvas.onpointerup=e=>{if(e.pointerId===state.pointerId)release();};canvas.onpointercancel=release;canvas.onlostpointercapture=()=>{if(state.pointerId!==null)release();};canvas.onkeydown=e=>{if(e.key==='Escape'){state.demo=null;release();}};
function reset(){state.demo=null;release();state.cat.reset();render();}
$('reset').onclick=reset;$('breed').onchange=()=>{if(!state.ready)return;reset();state.breed=$('breed').value;render();};
function softness(v){$('softness').value=v;$('softValue').textContent=v+'%';state.cat.soft=Number(v)/100;}
$('softness').oninput=()=>softness($('softness').value);document.querySelectorAll('[data-soft]').forEach(b=>b.onclick=()=>softness(b.dataset.soft));
$('demo').onclick=()=>{if(!state.ready)return;reset();state.demo=0;const hit={id:'head',u:.56,v:.36},p=state.cat.partPoint(hit.id,hit.u,hit.v);state.demoOrigin=p;state.cat.begin(...p,hit);};
function demo(dt){if(state.demo===null)return;state.demo+=dt;const t=state.demo;if(t<1){const f=t*t*(3-2*t);state.cat.move(state.demoOrigin[0],state.demoOrigin[1]*(1-f)+160*f);}else if(t<4.5){const q=t-1;state.cat.move(630+Math.sin(q*3.4)*205,160+Math.sin(q*2.6)*25);}else if(t<6){state.cat.move(630+Math.sin(3.5*3.4)*205,160+Math.sin(3.5*2.6)*25);}else{state.cat.release();state.demo=null;}}
function interrupt(){state.demo=null;release();accumulator=0;last=performance.now();}document.addEventListener('visibilitychange',interrupt);window.addEventListener('blur',interrupt);canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();state.paused=true;release();$('status').textContent='图形中断，请刷新预览';});
function tick(now){const dt=Math.min((now-last)/1000,.06);last=now;if(state.ready&&!document.hidden&&!state.paused){accumulator+=dt;while(accumulator>=1/120){demo(1/120);state.cat.step(1/120);accumulator-=1/120;}render();}requestAnimationFrame(tick);}
(async()=>{try{initGL();await Promise.all(Object.keys(names).map(async k=>{art[k]=Object.fromEntries(await Promise.all(['torso',...Object.keys(partsSpec)].map(async id=>[id,makePart(await load(`layers-v2/${k}/${id}.png`))])));}));state.cat.soft=Number($('softness').value)/100;state.ready=true;render();requestAnimationFrame(tick);}catch(e){$('status').textContent=e.message;}})();
window.softDragStudy={state,render,reset,pick,vertices,partsSpec};
