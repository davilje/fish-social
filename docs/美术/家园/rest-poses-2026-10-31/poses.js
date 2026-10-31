const $=id=>document.getElementById(id);
const names={orange:'橘猫',calico:'三花猫',blue:'英短蓝猫'};
const poses=[['蜷身睡','身体团成一圈，头靠尾巴'],['侧卧睡','身体侧躺，四肢放松'],['枕爪趴睡','下巴枕着前爪，安心打盹'],['收爪蹲坐','缩短身形，前爪贴着胸口'],['低身蹲伏','前爪落地，轻轻观察'],['放松蹲坐','后腿收好，半眯眼休息']];
// Art-directed ground contacts, no per-pose bounding-box rescale.
const feet={orange:[451,439,442,426,409,433],calico:[483,458,468,402,412,425],blue:[487,486,487,415,416,426]};
const sheets={};let roomArt,selected=3,ready=false;
function cell(ctx,img,index,x=0,y=0,size=512){ctx.drawImage(img,index%3*512,Math.floor(index/3)*512,512,512,x,y,size,size);}
function render(){if(!ready)return;const breed=$('breed').value,img=sheets[breed];
 const c=$('room').getContext('2d');c.clearRect(0,0,1200,675);c.drawImage(roomArt,0,0,1200,675);
 c.fillStyle='#6c54331a';c.beginPath();c.ellipse(615,567,86,10,0,0,Math.PI*2);c.fill();
 const scale=.43;cell(c,img,selected,615-256*scale,565-feet[breed][selected]*scale,512*scale);
 $('caption').textContent=names[breed]+' · '+poses[selected][0];$('download').href=breed+'-poses.png';
 document.querySelectorAll('.card').forEach((b,i)=>{b.setAttribute('aria-pressed',String(i===selected));const ctx=b.querySelector('canvas').getContext('2d');ctx.clearRect(0,0,512,512);cell(ctx,img,i);});
}
poses.forEach(([title,desc],i)=>{const b=document.createElement('button');b.className='card';b.setAttribute('aria-label',title);b.innerHTML='<canvas width="512" height="512"></canvas><strong>'+title+'</strong><span>'+desc+'</span>';b.onclick=()=>{selected=i;render();};$('poses').append(b);});
$('breed').onchange=render;
function zoom(whole){if(!ready)return;$('detail').hidden=whole;$('whole').hidden=!whole;$('whole').style.display=whole?'block':'none';$('zoomTitle').textContent=names[$('breed').value]+' · '+(whole?'六种姿势':poses[selected][0]);
 if(whole)$('whole').src=$('breed').value+'-poses.png';else{const ctx=$('detail').getContext('2d');ctx.clearRect(0,0,512,512);cell(ctx,sheets[$('breed').value],selected);}$('zoom').showModal();}
$('enlarge').onclick=()=>zoom(false);$('sheet').onclick=()=>zoom(true);$('close').onclick=()=>$('zoom').close();
$('interaction').onclick=()=>{if(parent!==window)parent.location.hash='home-interaction';else location.href='../../../../现行预览.html#home-interaction';};
function load(url){return new Promise((resolve,reject)=>{const i=new Image();i.onload=()=>resolve(i);i.onerror=()=>reject(Error('素材加载失败：'+url));i.src=url;});}
(async()=>{try{roomArt=await load('../pastel-interior-2026-10-31/room-pastel-v5.png');await Promise.all(Object.keys(names).map(async k=>{sheets[k]=await load(k+'-poses.png');}));ready=true;render();}catch(e){$('caption').textContent=e.message;}})();
window.restPoseStudy={get ready(){return ready;},get selected(){return selected;},names,poses};
