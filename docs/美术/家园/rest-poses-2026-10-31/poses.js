const $=id=>document.getElementById(id);
const names={orange:'橘猫',calico:'三花猫',blue:'英短蓝猫'};
const poses=[['蜷身睡','身体团成一圈，头靠尾巴'],['侧卧睡','身体侧躺，四肢放松'],['枕爪趴睡','下巴枕着前爪，安心打盹'],['收爪蹲坐','缩短身形，前爪贴着胸口'],['低身蹲伏','前爪落地，轻轻观察'],['放松蹲坐','后腿收好，半眯眼休息'],['四脚朝天睡','仰躺熟睡，四爪自然蜷起'],['肚皮朝上伸懒腰','前后肢舒展开，露出软软的肚皮']];
// Art-directed ground contacts, no per-pose bounding-box rescale.
const feet={orange:[451,439,442,426,409,433,350,374],calico:[483,458,468,402,412,425,356,366],blue:[487,486,487,415,416,426,355,364]};
const sheets={},supine={};let roomArt,selected=6,ready=false;
function sheetFile(){return $('breed').value+(selected>=6?'-supine.png':'-poses.png');}
// Separate the two source drawings along the transparent gap, preserving tails that
// cross the geometric midpoint. The original PNG and alpha are left untouched.
function isolateSupine(img,breed,index){
 const c=document.createElement('canvas');c.width=c.height=1024;const ctx=c.getContext('2d');
 const boundary=breed==='calico'?[[887,0],[887,440],[810,530],[810,887]]:[[breed==='blue'?868:887,0],[breed==='blue'?868:887,887]];
 ctx.translate(index===0?40:-790,0);ctx.beginPath();
 if(index===0){ctx.moveTo(0,0);boundary.forEach(p=>ctx.lineTo(...p));ctx.lineTo(0,img.height);}
 else{ctx.moveTo(img.width,0);ctx.lineTo(img.width,img.height);[...boundary].reverse().forEach(p=>ctx.lineTo(...p));}
 ctx.closePath();ctx.clip();ctx.drawImage(img,0,0);return c;
}
function cell(ctx,img,index,x=0,y=0,size=512){if(index>=6){ctx.drawImage(supine[$('breed').value][index-6],x,y,size,size);return;}ctx.drawImage(img,index%3*512,Math.floor(index/3)*512,512,512,x,y,size,size);}
function render(){if(!ready)return;const breed=$('breed').value,img=sheets[breed];
 const c=$('room').getContext('2d');c.clearRect(0,0,1200,675);c.drawImage(roomArt,0,0,1200,675);
 c.fillStyle='#6c54331a';c.beginPath();c.ellipse(615,567,86,10,0,0,Math.PI*2);c.fill();
 const scale=.43;cell(c,img,selected,615-256*scale,565-feet[breed][selected]*scale,512*scale);
 $('caption').textContent=names[breed]+' · '+poses[selected][0];$('download').href=sheetFile();
 document.querySelectorAll('.card').forEach((b,i)=>{b.setAttribute('aria-pressed',String(i===selected));const ctx=b.querySelector('canvas').getContext('2d');ctx.clearRect(0,0,512,512);cell(ctx,img,i);});
}
poses.forEach(([title,desc],i)=>{const b=document.createElement('button');b.className='card';b.setAttribute('aria-label',title);b.innerHTML='<canvas width="512" height="512"></canvas><strong>'+title+'</strong><span>'+desc+'</span>';b.onclick=()=>{selected=i;render();};$('poses').append(b);});
$('breed').onchange=render;
function zoom(whole){if(!ready)return;$('detail').hidden=whole;$('whole').hidden=!whole;$('whole').style.display=whole?'block':'none';$('zoomTitle').textContent=names[$('breed').value]+' · '+(whole?(selected>=6?'仰躺与舒展补充原画':'六种基础姿势'):poses[selected][0]);
 if(whole)$('whole').src=sheetFile();else{const ctx=$('detail').getContext('2d');ctx.clearRect(0,0,512,512);cell(ctx,sheets[$('breed').value],selected);}$('zoom').showModal();}
$('enlarge').onclick=()=>zoom(false);$('sheet').onclick=()=>zoom(true);$('close').onclick=()=>$('zoom').close();
$('interaction').onclick=()=>{if(parent!==window)parent.location.hash='home-interaction';else location.href='../../../../现行预览.html#home-interaction';};
function load(url){return new Promise((resolve,reject)=>{const i=new Image();i.onload=()=>resolve(i);i.onerror=()=>reject(Error('素材加载失败：'+url));i.src=url;});}
(async()=>{try{roomArt=await load('../pastel-interior-2026-10-31/room-pastel-v5.png');await Promise.all(Object.keys(names).map(async k=>{const [base,extra]=await Promise.all([load(k+'-poses.png'),load(k+'-supine.png')]);sheets[k]=base;supine[k]=[0,1].map(i=>isolateSupine(extra,k,i));}));ready=true;render();}catch(e){$('caption').textContent=e.message;}})();
window.restPoseStudy={get ready(){return ready;},get selected(){return selected;},names,poses};
