import {SoftCat,clamp} from './physics.js';
const $=id=>document.getElementById(id),canvas=$('pet'),fx=$('fx').getContext('2d');
const gl=canvas.getContext('webgl',{alpha:true,antialias:true,premultipliedAlpha:true});
const state={ready:false,cat:new SoftCat(),breed:'orange',demo:null,pointerId:null,paused:false};
const textures={},names={orange:'橘猫',calico:'三花猫',blue:'英短蓝猫'};
let program,buffer,textureLoc,vertexLoc,last=performance.now(),accumulator=0;
const rows=[...new Set([...Array.from({length:33},(_,i)=>i/32),.57,.67,.78,.89])].sort((a,b)=>a-b),cols=24;
function shader(type,source){const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(s));return s;}
function initGL(){if(!gl)throw Error('此浏览器未启用 WebGL，无法显示软体试验。');program=gl.createProgram();
 gl.attachShader(program,shader(gl.VERTEX_SHADER,'attribute vec4 vertex; varying vec2 uv; void main(){uv=vertex.zw;gl_Position=vec4(vertex.x/600.0-1.0,1.0-vertex.y/337.5,0.0,1.0);}'));
 gl.attachShader(program,shader(gl.FRAGMENT_SHADER,'precision mediump float; varying vec2 uv; uniform sampler2D art; void main(){gl_FragColor=texture2D(art,uv);}'));
 gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(program));gl.useProgram(program);
 buffer=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,buffer);vertexLoc=gl.getAttribLocation(program,'vertex');gl.enableVertexAttribArray(vertexLoc);gl.vertexAttribPointer(vertexLoc,4,gl.FLOAT,false,16,0);textureLoc=gl.getUniformLocation(program,'art');gl.uniform1i(textureLoc,0);
 gl.enable(gl.BLEND);gl.blendFunc(gl.ONE,gl.ONE_MINUS_SRC_ALPHA);gl.clearColor(0,0,0,0);gl.viewport(0,0,1200,675);
}
function load(url){return new Promise((resolve,reject)=>{const i=new Image();i.onload=()=>resolve(i);i.onerror=()=>reject(Error('载入失败：'+url));i.src=url;});}
function makeArt(image){
 const source=document.createElement('canvas');source.width=source.height=512;const c=source.getContext('2d');
 // The neighbouring crouch drawing crosses the nominal sheet cell boundary.
 // Its nose lives in the first 80px; the seated cat starts beyond that gutter.
 c.drawImage(image,1104,512,432,512,80,0,432,512);
 const data=c.getImageData(0,0,512,512).data;let minX=512,minY=512,maxX=0,maxY=0;
 for(let y=0;y<512;y++)for(let x=0;x<512;x++)if(data[(y*512+x)*4+3]>150){minX=Math.min(minX,x);maxX=Math.max(maxX,x);minY=Math.min(minY,y);maxY=Math.max(maxY,y);}
 minX=Math.max(0,minX-3);minY=Math.max(0,minY-3);maxX=Math.min(511,maxX+3);maxY=Math.min(511,maxY+3);
 const art=document.createElement('canvas');art.width=maxX-minX+1;art.height=maxY-minY+1;art.getContext('2d').drawImage(source,minX,minY,art.width,art.height,0,0,art.width,art.height);
 const texture=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,texture);gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL,true);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,art);
 gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);
 return {texture,art,pixels:art.getContext('2d').getImageData(0,0,art.width,art.height).data};
}
function dimensions(){const a=textures[state.breed].art;state.cat=new SoftCat(230,230*a.height/a.width);state.cat.soft=Number($('softness').value)/100;}
function verts(){const c=state.cat,out=[];const add=(u,v)=>out.push(...c.point(u,v),u,v);
 for(let y=0;y<rows.length-1;y++)for(let x=0;x<cols;x++){const a=x/cols,b=(x+1)/cols,t=rows[y],d=rows[y+1];add(a,t);add(b,t);add(b,d);add(a,t);add(b,d);add(a,d);}return new Float32Array(out);}
function render(){if(!state.ready)return;const cat=state.cat;gl.clear(gl.COLOR_BUFFER_BIT);gl.bindTexture(gl.TEXTURE_2D,textures[state.breed].texture);const v=verts();gl.bufferData(gl.ARRAY_BUFFER,v,gl.DYNAMIC_DRAW);gl.drawArrays(gl.TRIANGLES,0,v.length/4);
 fx.clearRect(0,0,1200,675);const height=Math.max(0,565-cat.point(.5,1)[1]);fx.fillStyle=`rgba(87,67,43,${.16*Math.exp(-height/270)})`;fx.beginPath();fx.ellipse(cat.x,568,cat.w*.34/(1+height/800),9,0,0,Math.PI*2);fx.fill();
 if($('mesh').checked){fx.strokeStyle='#3a836e75';fx.lineWidth=.7;for(let y=0;y<rows.length;y++){fx.beginPath();for(let x=0;x<=cols;x++){const p=cat.point(x/cols,rows[y]);x?fx.lineTo(...p):fx.moveTo(...p);}fx.stroke();}for(let x=0;x<=cols;x++){fx.beginPath();rows.forEach((r,i)=>i?fx.lineTo(...cat.point(x/cols,r)):fx.moveTo(...cat.point(x/cols,r)));fx.stroke();}}
 if(cat.grab){fx.strokeStyle='#fff9e3';fx.lineWidth=2;fx.beginPath();fx.arc(cat.grab.x,cat.grab.y,11,0,Math.PI*2);fx.stroke();}
 $('phase').textContent=cat.grab?'拎着 · 身体柔软跟随':cat.grounded?(cat.impact>.04?'落地 · 轻轻回弹':'放松坐着'):'松手 · 正在落下';
 $('status').textContent=names[state.breed]+' · '+(state.demo!==null?'演示中':'可以拖动');
}
function point(e){const r=canvas.getBoundingClientRect();return {x:(e.clientX-r.left)/r.width*1200,y:(e.clientY-r.top)/r.height*675};}
function pick(p){const uv=state.cat.pick(p.x,p.y);if(!uv)return null;const a=textures[state.breed],x=clamp(Math.floor(uv.u*a.art.width),0,a.art.width-1),y=clamp(Math.floor(uv.v*a.art.height),0,a.art.height-1);return a.pixels[(y*a.art.width+x)*4+3]>100?uv:null;}
function release(){state.cat.release();if(state.pointerId!==null&&canvas.hasPointerCapture(state.pointerId))canvas.releasePointerCapture(state.pointerId);state.pointerId=null;canvas.classList.remove('dragging');}
canvas.onpointerdown=e=>{if(!state.ready||e.button!==0||state.pointerId!==null)return;const p=point(e),uv=pick(p);if(!uv)return;state.demo=null;state.pointerId=e.pointerId;state.cat.begin(p.x,p.y,uv);canvas.setPointerCapture(e.pointerId);canvas.classList.add('dragging');e.preventDefault();};
canvas.onpointermove=e=>{if(!state.ready)return;const p=point(e);if(e.pointerId===state.pointerId)state.cat.move(p.x,p.y);else canvas.style.cursor=pick(p)?'grab':'default';};
canvas.onpointerup=e=>{if(e.pointerId===state.pointerId)release();};canvas.onpointercancel=release;canvas.onlostpointercapture=()=>{if(state.pointerId!==null)release();};
canvas.onkeydown=e=>{if(e.key==='Escape'){state.demo=null;release();}};
function reset(){state.demo=null;release();state.cat.reset();render();}
$('reset').onclick=reset;
$('breed').onchange=()=>{if(!state.ready)return;reset();state.breed=$('breed').value;dimensions();render();};
function softness(value){$('softness').value=value;$('softValue').textContent=value+'%';state.cat.soft=Number(value)/100;}
$('softness').oninput=()=>softness($('softness').value);document.querySelectorAll('[data-soft]').forEach(b=>b.onclick=()=>softness(b.dataset.soft));
$('demo').onclick=()=>{if(!state.ready)return;reset();state.demo=0;const uv={u:.62,v:.27},p=state.cat.point(uv.u,uv.v);state.cat.begin(...p,uv);};
function demo(dt){if(state.demo===null)return;state.demo+=dt;const t=state.demo;
 if(t<.9){const f=t/.9,s=f*f*(3-2*f);state.cat.move(638,370-170*s);}
 else if(t<3.5){const q=t-.9;state.cat.move(638+Math.sin(q*3.7)*190,200+Math.sin(q*2.5)*25);}
 else{state.cat.release();state.demo=null;}}
document.addEventListener('visibilitychange',()=>{state.demo=null;release();accumulator=0;last=performance.now();});window.addEventListener('blur',()=>{state.demo=null;release();});
canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();state.paused=true;release();$('status').textContent='图形上下文中断，请刷新预览';});
function tick(now){const dt=Math.min((now-last)/1000,.06);last=now;if(state.ready&&!document.hidden&&!state.paused){accumulator+=dt;while(accumulator>=1/120){demo(1/120);state.cat.step(1/120);accumulator-=1/120;}render();}requestAnimationFrame(tick);}
(async()=>{try{initGL();await Promise.all(Object.keys(names).map(async k=>{textures[k]=makeArt(await load('../rest-poses-2026-10-31/'+k+'-poses.png'));}));dimensions();state.ready=true;render();requestAnimationFrame(tick);}catch(e){$('status').textContent=e.message;}})();
window.softDragStudy={state,render,reset,pick,verts,rows};
