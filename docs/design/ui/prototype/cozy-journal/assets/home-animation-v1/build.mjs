import fs from 'node:fs';
import path from 'node:path';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.dirname(fileURLToPath(import.meta.url));
const read = file => JSON.parse(fs.readFileSync(path.join(root, file), 'utf8'));
const labels = {'pet-roll':'抚摸打滚',walk:'四足行走','walk-start':'行走起步','walk-stop':'行走停步',eat:'低头进食','food-pour':'倒粮','food-fill':'粮食累积','bowl-food':'粮食状态'};
const clips = new Map();
const calibration=read('scale-calibration.json');
const audit = [];
const transparent = {r:0,g:0,b:0,alpha:0};
const blank = (width, height) => sharp({create:{width,height,channels:4,background:transparent}});
const pad = value => String(value).padStart(2, '0');
async function bounds(buffer) {
  const {data,info} = await sharp(buffer).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  let left=info.width,top=info.height,right=-1,bottom=-1,mass=0,weightedX=0;
  for(let row=0;row<info.height;row++) for(let column=0;column<info.width;column++) {
    const alpha=data[(row*info.width+column)*4+3];
    if(alpha<8) continue;
    left=Math.min(left,column);right=Math.max(right,column);top=Math.min(top,row);bottom=Math.max(bottom,row);
    mass+=alpha;weightedX+=column*alpha;
  }
  if(right<0) return null;
  return {left,top,width:right-left+1,height:bottom-top+1,centerX:weightedX/mass};
}
async function save(file,buffer) {
  const target=path.join(root,file);
  fs.mkdirSync(path.dirname(target),{recursive:true});
  await sharp(buffer).png().toFile(target);
}
async function rowBoundary(source,left,width,height) {
  const {data,info}=await sharp(source).extract({left,top:0,width,height}).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  let bestStart=0,bestLength=0,start=-1;
  for(let row=Math.floor(height*.32);row<Math.ceil(height*.68);row++) {
    let occupied=false;
    for(let column=0;column<info.width;column++) if(data[(row*info.width+column)*4+3]>=8){occupied=true;break;}
    if(!occupied&&start<0) start=row;
    if(occupied&&start>=0){if(row-start>bestLength){bestStart=start;bestLength=row-start;}start=-1;}
  }
  if(bestLength<3) throw Error(`No clean row separator: ${source}, column ${left}`);
  return Math.floor(bestStart+bestLength/2);
}
function clipFor(job) {
  const id=`${job.skinId}-${job.clip}-${job.direction}`;
  if(!clips.has(id)) {
    const registration=job.skinId==='orange'?[256,job.canvas[1]-32]:job.clip==='food-pour'?[172,344]:[76,29];
    const loop=job.clip==='walk'?[0,12]:job.clip==='eat'?[3,11]:null;
    clips.set(id,{id,skinId:job.skinId,action:job.clip,clipId:job.clip,label:labels[job.clip],direction:job.direction,canvas:job.canvas,canvasPx:job.canvas,registration,registrationPx:registration,displayScale:.5,fps:12,loop,loopStart:loop?.[0]??null,loopEndExclusive:loop?.[1]??null,mirrorAllowed:false,sourceFacing:job.direction,status:'review',calibrationStatus:'pending',frames:[],events:[]});
  }
  return clips.get(id);
}
for(const job of read('jobs.json')) {
  const replacement=`${job.id}-replacement.png`;
  const master=fs.existsSync(path.join(root,'masters',replacement))?replacement:`${job.id}.png`;
  const source=path.join(root,'masters',master);
  const meta=await sharp(source).metadata();
  const rows=master.includes('replacement')?2:job.rows;
  const boundaries=[];
  for(let column=0;column<2;column++) {
    const left=Math.floor(column*meta.width/2),right=Math.floor((column+1)*meta.width/2);
    boundaries.push(rows===2?await rowBoundary(source,left,right-left,meta.height):meta.height);
  }
  const clip=clipFor(job);
  for(let local=0;local<job.count;local++) {
    const column=local%2,row=Math.floor(local/2);
    const left=Math.floor(column*meta.width/2),right=Math.floor((column+1)*meta.width/2);
    const top=row===0?0:boundaries[column],bottom=row===0?boundaries[column]:meta.height;
    const cell=await sharp(source).extract({left,top,width:right-left,height:bottom-top}).png().toBuffer();
    const box=await bounds(cell);
    const sheetCalibration=calibration.sheets[job.id];
    const scale=calibration.baseScale*(sheetCalibration&&sheetCalibration.apply!==false?sheetCalibration.referenceHeadWidthPx/sheetCalibration.sourceHeadWidthPx:1);
    const width=Math.round(box.width*scale),height=Math.round(box.height*scale);
    const offsetX=Math.round(256-(box.centerX-box.left)*scale),offsetY=clip.registration[1]-height;
    if(offsetX<0||offsetY<0||offsetX+width>job.canvas[0]) throw Error(`Cat clipped: ${job.id}/${local}`);
    const cropped=await sharp(cell).extract({left:box.left,top:box.top,width:box.width,height:box.height}).resize(width,height).png().toBuffer();
    const buffer=await blank(...job.canvas).composite([{input:cropped,left:offsetX,top:offsetY}]).png().toBuffer();
    const frameIndex=job.start+local;
    const file=`orange/${job.clip}/${job.direction==='neutral'?'':job.direction+'/'}frame-${pad(frameIndex)}.png`;
    await save(file,buffer);
    clip.frames[frameIndex]={file,durationMs:1000/12,mouthAnchor:null,mouthAnchorPx:null,pawContacts:[],anchorStatus:'pending'};
    audit.push({file,master:`masters/${master}`,nativeSize:[meta.width,meta.height],sourceCell:{left,top,width:right-left,height:bottom-top},sourceBounds:box,scale,scaleCalibration:sheetCalibration?job.id:null,offset:[offsetX,offsetY],runtimeBounds:[width,height]});
  }
}
for(const job of read('shared-jobs.json').filter(entry=>entry.clip==='food-pour')) {
  const source=path.join(root,'masters',job.id+'.png');
  const meta=await sharp(source).metadata(),clip=clipFor(job);
  clip.pourLandingPx=null;clip.rimContactPx=null;clip.foodLayerOffsetPx=null;
  for(let local=0;local<job.count;local++) {
    const column=local%2,row=Math.floor(local/2);
    const left=Math.floor(column*meta.width/2),top=Math.floor(row*meta.height/2);
    const width=Math.floor((column+1)*meta.width/2)-left,height=Math.floor((row+1)*meta.height/2)-top;
    const file=`shared/food-pour/frame-${pad(job.start+local)}.png`;
    const buffer=job.start+local===11?await blank(...job.canvas).png().toBuffer():await sharp(source).extract({left,top,width,height}).resize(...job.canvas).png().toBuffer();
    await save(file,buffer);clip.frames.push({file,durationMs:1000/12,mouthAnchor:null});
  }
}
const statesSource=path.join(root,'masters/shared-bowl-food-states.png');
const stateMeta=await sharp(statesSource).metadata();
const stateBuffers={empty:await blank(270,88).png().toBuffer()};
for(const [index,name] of ['full','two-thirds','one-third','crumbs'].entries()) {
  const column=index%2,row=Math.floor(index/2);
  const left=Math.floor(column*stateMeta.width/2),top=Math.floor(row*stateMeta.height/2);
  const cell=await sharp(statesSource).extract({left,top,width:Math.floor(stateMeta.width/2),height:Math.floor(stateMeta.height/2)}).png().toBuffer();
  const box=await bounds(cell);
  const scale=.16,width=Math.round(box.width*scale),height=Math.round(box.height*scale);
  const crop=await sharp(cell).extract({left:box.left,top:box.top,width:box.width,height:box.height}).resize(width,height).png().toBuffer();
  stateBuffers[name]=await blank(270,88).composite([{input:crop,left:Math.round(76-width/2),top:Math.round(33-height)}]).png().toBuffer();
}
const statesClip=clipFor({skinId:'shared',clip:'bowl-food',direction:'neutral',canvas:[270,88]});
for(const [name,buffer] of Object.entries(stateBuffers).filter(([name])=>name!=='crumbs')) await save(`shared/bowl-food/${name}.png`,buffer);
for(const name of ['empty','full','two-thirds','one-third','finished']) statesClip.frames.push({file:`shared/bowl-food/${name==='finished'?'empty':name}.png`,state:name,durationMs:500,mouthAnchor:null});
{
  const fillClip=clipFor({skinId:'shared',clip:'food-fill',direction:'neutral',canvas:[270,88]});
  const stateSequence=['empty','empty','empty','crumbs','crumbs','one-third','one-third','two-thirds','two-thirds','full','full','full'];
  const sequence=stateSequence.map(name=>stateBuffers[name]);
  for(const [index,buffer] of sequence.entries()) {
    const file=`shared/food-fill/frame-${pad(index)}.png`;await save(file,buffer);fillClip.frames.push({file,durationMs:1000/12,mouthAnchor:null,state:stateSequence[index],hold:index>0&&stateSequence[index]===stateSequence[index-1]});
  }
  fillClip.events=[{frame:9,type:'food-full'}];
}
const skins=[['orange','橘猫'],['black','黑猫'],['blue','蓝猫'],['brown','棕猫'],['calico','三花猫'],['cow','奶牛猫'],['cream','奶油猫'],['silver','银渐层'],['white','白猫'],['shared','公共素材']].map(([id,name])=>({id,name,status:['orange','shared'].includes(id)?'review':'pending'}));
const result={version:1,revision:'2026-10-24-eat-scale-2',stage:'art-review',skins,clips:[...clips.values()],notes:['橘猫首版已接入 Unity 正式小屋，素材仍待美术验收；其余八猫待制作。','进食原稿按每批人工头宽标尺统一导出，批内倍率固定；不按每帧包围盒缩放，播放器不动态调整角色大小。','嘴部、落脚与碗沿接触点待逐帧标定；新动作尚未验收。','部分母版单格未达到运行画布 2 倍边长，保留原生分辨率，不放大伪装高清母版。']};
fs.writeFileSync(path.join(root,'manifest.json'),JSON.stringify(result,null,2)+'\n');
fs.writeFileSync(path.join(root,'data.js'),'window.homeAnimation = '+JSON.stringify(result,null,2)+';\n');
fs.writeFileSync(path.join(root,'export-audit.json'),JSON.stringify({version:1,frames:audit},null,2)+'\n');
console.log(JSON.stringify({clips:result.clips.length,catFrames:result.clips.filter(clip=>clip.skinId==='orange').reduce((sum,clip)=>sum+clip.frames.length,0),publicFrames:result.clips.filter(clip=>clip.skinId==='shared').reduce((sum,clip)=>sum+clip.frames.length,0)}));
