import {poses,grips,sequences,anchor} from './grab-v4/poses.js';
const $=id=>document.getElementById(id),canvas=$('pet'),fx=$('fx').getContext('2d');
const gl=canvas.getContext('webgl',{alpha:true,antialias:true,premultipliedAlpha:true});
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v)),smooth=t=>(t=clamp(t,0,1))*t*(3-2*t),lerp=(a,b,t)=>a+(b-a)*t;
const names={orange:'橘猫',calico:'三花猫',blue:'英短蓝猫'},headSize=148,ground=567,cols=28,rows=40;
const art={},grid=[],indices=[],springs=Array.from({length:7},()=>({a:0,v:0}));
const state={ready:false,breed:'orange',rest:'sit',mode:null,held:false,pointer:null,phase:'rest',progress:0,position:[600,ground],velocity:[0,0],target:[600,ground],grabOffset:[0,0],angle:0,omega:0,time:0,soft:.85,paused:false,demo:null,rendered:[],minGain:1};
let program,buffer,opacity,last=performance.now(),acc=0;
for(let j=0;j<=rows;j++)for(let i=0;i<=cols;i++)grid.push([i/cols,j/rows]);
for(let j=0;j<rows;j++)for(let i=0;i<cols;i++){let a=j*(cols+1)+i,b=a+1,c=a+cols+1,d=c+1;indices.push(a,b,d,a,d,c);}
function shader(type,code){const s=gl.createShader(type);gl.shaderSource(s,code);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(s));return s;}
function init(){if(!gl)throw Error('需要启用图形加速');program=gl.createProgram();gl.attachShader(program,shader(gl.VERTEX_SHADER,'attribute vec4 vertex;varying vec2 uv;void main(){uv=vertex.zw;gl_Position=vec4(vertex.x/600.0-1.0,1.0-vertex.y/337.5,0.0,1.0);}'));gl.attachShader(program,shader(gl.FRAGMENT_SHADER,'precision mediump float;varying vec2 uv;uniform sampler2D art;uniform float opacity;void main(){gl_FragColor=texture2D(art,uv)*opacity;}'));gl.linkProgram(program);if(!gl.getProgramParameter(program,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(program));gl.useProgram(program);buffer=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,buffer);const at=gl.getAttribLocation(program,'vertex');gl.enableVertexAttribArray(at);gl.vertexAttribPointer(at,4,gl.FLOAT,false,16,0);opacity=gl.getUniformLocation(program,'opacity');gl.enable(gl.BLEND);gl.blendFunc(gl.ONE,gl.ONE_MINUS_SRC_ALPHA);gl.viewport(0,0,1200,675);gl.clearColor(0,0,0,0);}
async function load(breed,id){const def=poses[id],im=new Image();im.src='grab-v4/'+(id==='headHang'?'head-atlas-'+Object.keys(names).indexOf(breed):breed+'-'+def.file)+'.png';await im.decode();const c=document.createElement('canvas');c.width=im.width;c.height=im.height;c.getContext('2d').drawImage(im,0,0);const pixels=c.getContext('2d').getImageData(0,0,c.width,c.height).data,t=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,t);gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL,true);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,im);for(const param of [gl.TEXTURE_MIN_FILTER,gl.TEXTURE_MAG_FILTER])gl.texParameteri(gl.TEXTURE_2D,param,gl.LINEAR);for(const param of [gl.TEXTURE_WRAP_S,gl.TEXTURE_WRAP_T])gl.texParameteri(gl.TEXTURE_2D,param,gl.CLAMP_TO_EDGE);return {im,pixels,t};}
function texture(id){return art[state.breed][id];}
function scale(id){return headSize/poses[id].headWidth;}
function restOrigin(id=state.rest){const d=poses[id],s=scale(id);return [state.restX-d.body[0]*s,ground-d.size[1]*s];}
function restPoint(mode){const d=poses[state.rest],a=anchor(d,mode),o=restOrigin(),s=scale(state.rest);return [o[0]+a[0]*s,o[1]+a[1]*s];}
function reset(){releaseCapture();Object.assign(state,{phase:'rest',held:false,mode:null,progress:0,velocity:[0,0],angle:0,omega:0,demo:null,restX:600,landTime:0,minGain:1});for(const s of springs)s.a=s.v=0;if(state.ready)render();}
function entryList(){if(state.phase==='rest')return [{id:state.rest,alpha:1,rest:true}];
 const sequence=[state.rest,...sequences[state.mode]],f=smooth(state.poseFade/.10);
 const result=state.posePrevious===state.poseIndex||f>=1?[{id:sequence[state.poseIndex],alpha:1}]:[{id:sequence[state.posePrevious],alpha:1-f},{id:sequence[state.poseIndex],alpha:f}];
 if(state.phase==='landing'){const k=smooth(state.landTime/.32);for(const e of result)e.alpha*=1-k;result.push({id:state.rest,alpha:k,rest:true});}
 return result.filter(e=>e.alpha>.001);
}
function rot(p,a){return [p[0]*Math.cos(a)-p[1]*Math.sin(a),p[0]*Math.sin(a)+p[1]*Math.cos(a)];}
// Gentle continuous skinning. The silhouette is authored for each suspension mode;
// only the torso stretches. Head and four paws each use rigid rotation/translation.
function localDeform(id,p,gain=1){const d=poses[id],a=anchor(d,state.mode),s=scale(id),amount=state.phase==='rest'||id===state.rest?0:smooth(state.progress)*gain;
 if(!amount)return [(p[0]-a[0])*s,(p[1]-a[1])*s];
 const distance=Math.hypot(d.body[0]-a[0],d.body[1]-a[1])||1,axis=[(d.body[0]-a[0])/distance,(d.body[1]-a[1])/distance];
 const stretch=.055*state.soft+.025*Math.min(Math.abs(state.velocity[0])/400,1),base=q=>{const along=Math.max(0,(q[0]-a[0])*axis[0]+(q[1]-a[1])*axis[1]);return [q[0]+axis[0]*along*stretch,q[1]+axis[1]*along*stretch];};
 const regions=[['head',d.head,.49],['frontLeft',d.frontLeft,.14],['frontRight',d.frontRight,.14],['hindLeft',d.hindLeft,.15],['hindRight',d.hindRight,.15],['tail',d.tail,.19]];
 function deform(q){let target=base(q),weight=1,xx=target[0],yy=target[1];regions.forEach(([key,center,r],i)=>{const radius=d.headWidth*r,dist=Math.hypot(q[0]-center[0],q[1]-center[1]),w=Math.pow(clamp(1-(dist-radius*.65)/(radius*.65),0,1),2)*24;if(!w)return;
 const pivot=key==='head'?d.head:[lerp(center[0],d.body[0],.50),lerp(center[1],d.body[1],.50)],angle=key===state.mode?0:springs[i].a*(key==='head'?.38:1);const z=rot([q[0]-pivot[0],q[1]-pivot[1]],angle),b=base(pivot);xx+=(b[0]+z[0])*w;yy+=(b[1]+z[1])*w;weight+=w;});return [xx/weight,yy/weight];}
 const z=deform(p),pin=deform(a);return [((p[0]-a[0])+(z[0]-pin[0]-(p[0]-a[0]))*amount)*s,((p[1]-a[1])+(z[1]-pin[1]-(p[1]-a[1]))*amount)*s];
}
function equilibrium(){return {head:0,body:0,frontLeft:.27,frontRight:-.22,hindLeft:.24,hindRight:-.24}[state.mode]||0;}
function point(id,p,rest=false,gain=1){const s=scale(id);if(rest){const o=restOrigin(id);return [o[0]+p[0]*s,o[1]+p[1]*s];}
 const a=anchor(poses[id],state.mode),contact=[a[0]+state.grabOffset[0]/s,a[1]+state.grabOffset[1]/s],q=localDeform(id,p,gain),pin=localDeform(id,contact,gain),v=rot([q[0]-pin[0],q[1]-pin[1]],state.angle+equilibrium()*smooth(state.progress));return [state.position[0]+v[0],state.position[1]+v[1]];
}
function mesh(entry){const d=poses[entry.id],base=grid.map(([u,v])=>point(entry.id,[u*d.size[0],v*d.size[1]],entry.rest,0));let gain=1;
 const build=g=>grid.map(([u,v])=>point(entry.id,[u*d.size[0],v*d.size[1]],entry.rest,g));let pts=build(gain);
 // Reject local inversion before uploading a mesh, including extreme pointer velocity.
 for(let attempt=0;attempt<7;attempt++){let valid=true;for(let n=0;n<indices.length;n+=3){const [a,b,c]=indices.slice(n,n+3).map(i=>pts[i]),[x,y,z]=indices.slice(n,n+3).map(i=>base[i]);const area=(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]),original=(y[0]-x[0])*(z[1]-x[1])-(y[1]-x[1])*(z[0]-x[0]);if(area<original*.3){valid=false;break;}}if(valid)break;gain*=.5;pts=build(gain);}
 state.minGain=Math.min(state.minGain,gain);return {...entry,pts,gain};
}
function render(){if(!state.ready)return;gl.clear(gl.COLOR_BUFFER_BIT);fx.clearRect(0,0,1200,675);state.rendered=entryList().map(mesh);let center=state.phase==='rest'?state.restX:state.position[0],height=state.phase==='rest'?0:Math.max(0,ground-state.position[1]-190);
 fx.fillStyle=`rgba(68,61,43,${.13*Math.exp(-height/230)})`;fx.beginPath();fx.ellipse(center,ground+2,45/(1+height/500),5,0,0,Math.PI*2);fx.fill();
 for(const e of state.rendered){const data=new Float32Array(indices.length*4);let n=0;for(const ix of indices)data.set([...e.pts[ix],...grid[ix]],n++*4);gl.uniform1f(opacity,e.alpha);gl.bindTexture(gl.TEXTURE_2D,texture(e.id).t);gl.bufferData(gl.ARRAY_BUFFER,data,gl.DYNAMIC_DRAW);gl.drawArrays(gl.TRIANGLES,0,indices.length);
 if($('mesh').checked){fx.strokeStyle='#40847880';fx.lineWidth=.7;for(let y=0;y<=rows;y+=4){fx.beginPath();for(let x=0;x<=cols;x++){const p=e.pts[y*(cols+1)+x];x?fx.lineTo(...p):fx.moveTo(...p);}fx.stroke();}}
 }
 if(state.phase==='rest'&&$('hotspots').checked){const palette={head:'#b8874d',body:'#7c9460',frontLeft:'#4a86a6',frontRight:'#4a86a6',hindLeft:'#bb7863',hindRight:'#bb7863'};for(const [mode,label] of Object.entries(grips)){const p=restPoint(mode);fx.beginPath();fx.arc(...p,mode.includes('Left')||mode.includes('Right')?10:13,0,Math.PI*2);fx.fillStyle=palette[mode]+'cc';fx.fill();fx.strokeStyle='#fff8e8';fx.lineWidth=1.5;fx.stroke();fx.fillStyle='white';fx.font='bold 12px sans-serif';fx.textAlign='center';fx.textBaseline='middle';fx.fillText(mode.includes('Left')?'1':mode.includes('Right')?'2':mode==='head'?'头':'身',...p);}}
 if(state.held){fx.strokeStyle='#fff9e6';fx.lineWidth=2;fx.beginPath();fx.arc(...state.target,8,0,Math.PI*2);fx.stroke();}
 $('phase').textContent=state.phase==='rest'?{sit:'放松坐姿',crouch:'低身趴伏',supine:'四脚朝天'}[state.rest]:`${grips[state.mode]} · ${state.held?'提起与余摆':state.phase==='landing'?'缓冲落地':'松手回落'}`;
 $('status').textContent=names[state.breed]+' · 六点抓取';
}
function dominant(){return state.rendered.reduce((a,b)=>!a||b.alpha>a.alpha?b:a,null);}
function inverseHit(p){const e=dominant();if(!e)return null;const tex=texture(e.id),d=poses[e.id];for(let n=indices.length-3;n>=0;n-=3){const ids=indices.slice(n,n+3),[a,b,c]=ids.map(i=>e.pts[i]),dx=p[0]-a[0],dy=p[1]-a[1],bx=b[0]-a[0],by=b[1]-a[1],cx=c[0]-a[0],cy=c[1]-a[1],det=bx*cy-by*cx;if(Math.abs(det)<1e-8)continue;const s=(dx*cy-dy*cx)/det,t=(dy*bx-dx*by)/det;if(s<0||t<0||s+t>1)continue;const u=grid[ids[0]][0]+(grid[ids[1]][0]-grid[ids[0]][0])*s+(grid[ids[2]][0]-grid[ids[0]][0])*t,v=grid[ids[0]][1]+(grid[ids[1]][1]-grid[ids[0]][1])*s+(grid[ids[2]][1]-grid[ids[0]][1])*t,x=clamp(Math.floor(u*tex.im.width),0,tex.im.width-1),y=clamp(Math.floor(v*tex.im.height),0,tex.im.height-1);if(tex.pixels[(y*tex.im.width+x)*4+3]>100)return {entry:e,local:[u*d.size[0],v*d.size[1]]};}return null;}
function pick(p){if(state.phase!=='rest')return null;const hit=inverseHit(p);if(!hit)return null;const d=poses[state.rest],q=hit.local,s=scale(state.rest);let paw=null,best=Infinity;for(const m of ['frontLeft','frontRight','hindLeft','hindRight']){const dist=Math.hypot(q[0]-d[m][0],q[1]-d[m][1])*s;if(dist<best){best=dist;paw=m;}}
 if(best<=19)return paw;const hd=((q[0]-d.head[0])/(d.headWidth*.53))**2+((q[1]-d.head[1])/(d.headWidth*.38))**2;return hd<1?'head':'body';}
function begin(mode,p){if(state.phase!=='rest')return false;state.mode=mode;const a=restPoint(mode);state.grabOffset=[p[0]-a[0],p[1]-a[1]];state.position=[...p];state.target=[...p];state.start=[...p];state.lastTarget=[...p];state.velocity=[0,0];state.progress=0;state.poseIndex=state.posePrevious=0;state.poseFade=1;state.phase='lifting';state.held=true;state.liftAge=0;state.angle=state.omega=0;return true;}
function move(p){if(state.held)state.target=[clamp(p[0],65,1135),clamp(p[1],65,state.start[1])];}
function releaseCapture(){const id=state.pointer;state.pointer=null;if(id!==null&&canvas.hasPointerCapture(id))canvas.releasePointerCapture(id);canvas.classList.remove('dragging');}
function release(){if(!state.held){releaseCapture();return;}state.held=false;state.phase='falling';state.demo=null;releaseCapture();}
function beginLanding(){state.phase='landing';state.landTime=0;state.restX=clamp(state.position[0],180,1020);state.velocity=[0,0];}
function step(dt){state.time+=dt;if(state.phase==='rest')return;
 let ax=0;
 if(state.held){const vx=clamp((state.target[0]-state.position[0])/dt,-1100,1100),vy=clamp((state.target[1]-state.position[1])/dt,-1000,1000);ax=clamp((vx-state.velocity[0])/dt,-9000,9000);state.velocity=[lerp(state.velocity[0],vx,.22),lerp(state.velocity[1],vy,.22)];state.position=[...state.target];const raised=Math.max(0,state.start[1]-state.target[1]);if(raised>20)state.liftAge+=dt;
 // A low lift only reaches the preparatory pose. The full extension requires clearance.
 const required=state.mode==='head'?120:state.mode==='body'?145:290,goal=clamp(raised/required,0,1);state.progress+=clamp(goal-state.progress,-dt*4,dt*2.6);state.phase=state.progress>.999?'hanging':'lifting';
 const seq=sequences[state.mode];let desired=0;for(let i=0;i<seq.length;i++){const d=poses[seq[i]],a=anchor(d,state.mode),depth=(d.size[1]-a[1])*scale(seq[i]);const threshold=Math.max(24,state.start[1]+depth-ground+16);if(raised>=threshold)desired=i+1;}
 if(state.poseFade>=.10&&desired!==state.poseIndex){state.posePrevious=state.poseIndex;state.poseIndex+=Math.sign(desired-state.poseIndex);state.poseFade=0;}
 }else if(state.phase==='falling'){state.velocity[1]+=950*dt;state.position[0]=clamp(state.position[0]+state.velocity[0]*dt,100,1100);state.position[1]+=state.velocity[1]*dt;state.velocity[0]*=Math.exp(-2*dt);const entries=entryList();let bottom=-Infinity;for(const e of entries){const d=poses[e.id];for(let y=0;y<=12;y++)for(let x=0;x<=10;x++){const tex=texture(e.id),u=x/10,v=y/12,px=clamp(Math.round(u*(tex.im.width-1)),0,tex.im.width-1),py=clamp(Math.round(v*(tex.im.height-1)),0,tex.im.height-1);if(tex.pixels[(py*tex.im.width+px)*4+3]>100)bottom=Math.max(bottom,point(e.id,[u*d.size[0],v*d.size[1]])[1]);}}if(bottom>=ground){state.position[1]-=bottom-ground;beginLanding();}}
 else if(state.phase==='landing'){state.landTime+=dt;if(state.landTime>=.32){state.phase='rest';state.mode=null;state.progress=0;}}
 state.poseFade+=dt;
 const softness=state.soft;state.omega+=(-state.angle*20-state.omega*(4.6-2*softness)-ax*.0015)*dt;state.omega=clamp(state.omega,-3,3);state.angle=clamp(state.angle+state.omega*dt,-.5,.5);
 springs.forEach((s,i)=>{s.v+=(-s.a*(27+i*5)-s.v*(4.8-softness*2)-ax*.0005*(i%2?1:-1))*dt;s.a=clamp(s.a+s.v*dt,-.13,.13);});
}
function coords(e){const r=canvas.getBoundingClientRect();return [(e.clientX-r.left)*1200/r.width,(e.clientY-r.top)*675/r.height];}
canvas.onpointerdown=e=>{if(!state.ready||e.button!==0||state.pointer!==null)return;const p=coords(e),mode=pick(p);if(!mode)return;state.demo=null;begin(mode,p);state.pointer=e.pointerId;canvas.setPointerCapture(e.pointerId);canvas.classList.add('dragging');e.preventDefault();};
canvas.onpointermove=e=>{const p=coords(e);if(e.pointerId===state.pointer)move(p);else canvas.style.cursor=pick(p)?'grab':'default';};canvas.onpointerup=e=>{if(e.pointerId===state.pointer)release();};canvas.onpointercancel=release;canvas.onlostpointercapture=()=>{if(state.pointer!==null)release();};canvas.onkeydown=e=>{if(e.key==='Escape')release();};
$('breed').onchange=()=>{state.breed=$('breed').value;reset();};$('pose').onchange=()=>{state.rest=$('pose').value;reset();};$('reset').onclick=reset;
function softness(v){state.soft=Number(v)/100;$('softness').value=v;$('softValue').textContent=v+'%';}$('softness').oninput=()=>softness($('softness').value);document.querySelectorAll('[data-soft]').forEach(b=>b.onclick=()=>softness(b.dataset.soft));
$('demo').onclick=()=>{reset();const mode=$('grip').value;begin(mode,restPoint(mode));state.demo=0;};
function demo(dt){if(state.demo===null)return;state.demo+=dt;const t=state.demo,origin=state.start;if(t<1.5){const k=smooth(t/1.5);move([origin[0],lerp(origin[1],155,k)]);}else if(t<4.5){const q=t-1.5;move([origin[0]+Math.sin(q*2.1)*145,155+Math.sin(q*2)*9]);}else if(t<6.4){move([...state.target]);}else release();}
function interrupt(){state.demo=null;release();acc=0;last=performance.now();}window.addEventListener('blur',interrupt);document.addEventListener('visibilitychange',interrupt);canvas.addEventListener('webglcontextlost',e=>{e.preventDefault();state.paused=true;interrupt();$('status').textContent='图形中断，请刷新预览';});
function tick(now){const dt=Math.min((now-last)/1000,.06);last=now;if(state.ready&&!state.paused&&!document.hidden){acc+=dt;while(acc>=1/120){demo(1/120);step(1/120);acc-=1/120;}render();}requestAnimationFrame(tick);}
window.softDragStudy={state,poses,grips,art,reset,render,pick,begin,move,release,step,point,restPoint,entryList,indices,headSize};
(async()=>{try{init();await Promise.all(Object.keys(names).map(async breed=>{art[breed]={};await Promise.all(Object.keys(poses).map(async id=>art[breed][id]=await load(breed,id)));}));state.ready=true;reset();requestAnimationFrame(tick);}catch(e){$('status').textContent='载入失败：'+e.message;console.error(e);}})();
