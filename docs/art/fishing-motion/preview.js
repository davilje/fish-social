const data = window.fishingMotionPreview;
const element = id => document.getElementById(id);
const names = {orange:'橘猫',black:'黑猫',white:'白猫',cow:'奶牛猫',calico:'三花猫',blue:'蓝猫',brown:'棕猫',cream:'奶油猫',silver:'银渐层',dafeiyu:'大肥鱼'};
const clips = {sit:'站立到入座',bait:'挂饵',cast:'抛竿',fishing:'等待',bite:'咬钩后仰',effort:'持续发力',hooked:'咬钩 → 循环发力',reel:'收杆出水',reel_empty:'空军收杆',catch:'展示收获',reset_catch:'收获后复位',reset_empty:'空军后复位',groundbait:'持续打窝',leave:'离席'};
Object.keys(data.motion.Clips).forEach(id => element('clip').add(new Option(clips[id] || id, id)));
element('clip').value='cast';
Object.keys(data.pets).forEach(id => element('pet').add(new Option(names[id] || id, id)));
element('pet').value = 'orange';
const images = new Map();
let elapsed = 0, playing = true, previousTime = performance.now(), stopAt = Infinity, renderVersion = 0;
function image(path) {
  if (!images.has(path)) images.set(path, new Promise((resolve, reject) => {
    const asset = new Image(); asset.onload = () => resolve(asset); asset.onerror = reject; asset.src = path;
  }));
  return images.get(path);
}
function config() {
  const pet = element('pet').value, clip = element('clip').value, manifest = data.pets[pet], source = manifest.clips[clip];
  const samples = data.motion.Clips[clip].Frames;
  const duration = Number(element('duration').value) * 1000;
  return {pet,clip,manifest,source,samples,loop:source.playback==='intro-loop'||data.motion.Clips[clip].Loop,end:Math.min(Number.isFinite(duration) ? Math.max(100,duration) : 5000,stopAt)};
}
function atFrame(durations, time) {
  for(let index=0;index<durations.length-1;index++) { if(time<durations[index]) return index; time-=durations[index]; }
  return durations.length-1;
}
function timeline(time, current) {
  if(current.clip !== 'groundbait') {
    if(current.source.playback==='intro-loop' || data.motion.Clips[current.clip].Loop){
      const start=current.source.loopStart||0, durations=current.source.durationsMs;
      const intro=durations.slice(0,start).reduce((sum,value)=>sum+value,0),loop=durations.slice(start).reduce((sum,value)=>sum+value,0);
      return time<intro?time:intro+(time-intro)%loop;
    }
    return time;
  }
  const durations=current.source.durationsMs, intro=durations[0], loop=durations.slice(1,7).reduce((sum,value)=>sum+value,0);
  if(time>=current.end) return durations.slice(0,6).reduce((sum,value)=>sum+value,0)+(time-current.end);
  return time<intro?time:intro+(time-intro)%loop;
}
function contactPose(current,sample,sourceIndex,updated) {
  const position=sample?.Position??sourceIndex,count=current.source.frames.length;
  const frame=sample && sample.Position!==sample.Source?sample.Contact:data.contact[current.clip][sourceIndex];
  const settings=data.motion.WaterContact,surface=Number(element('surface').value),reference=settings.ReferenceSurfaceY;
  const progress=count>1?Math.max(0,Math.min(1,position/(count-1))):1;
  const weight=['fishing','hooked','bite','effort'].includes(current.clip)?1:current.clip==='cast'?progress**2:['reel','reel_empty'].includes(current.clip)?(1-progress)**2:0;
  const delta=Math.max(0,surface-reference),bobber=Math.max(0,surface-(frame.BobberY>=0?frame.BobberY:reference))*weight;
  const fish=(frame.FishTop>=0?Math.max(delta,surface+(updated?settings.FishDepth:14)-frame.FishTop):delta)*weight;
  const length=frame.LineBottom-frame.LineTop;
  const lineScale=length>1?1+(frame.FishTop>=0?fish:bobber)/length:1;
  const bait=current.clip==='groundbait' && updated && position>=3 && position<6
    ?(delta+settings.GroundbaitDrop)*((position-3)/3)**2:delta*weight;
  return {lineScale,linePivot:frame.LineTop,bobber,fish,bait,surface};
}
async function draw(canvas, current, sample, sourceIndex, version) {
  const order=current.source.frames[sourceIndex];
  const assets=await Promise.all(current.manifest.layers.map(layer => image('../../../desktop-overlay/OverlayResources/fishing/'+
    (current.manifest.bodyLayers.includes(layer)?`${current.pet}/body/`:'shared/')+`${current.clip}/${sourceIndex}/${layer}.png`)));
  if(version!==renderVersion)return;
  const context=canvas.getContext('2d');context.clearRect(0,0,canvas.width,canvas.height);
  const pose=contactPose(current,sample,sourceIndex,!!sample);
  context.fillStyle='#a9d9de55';context.fillRect(0,pose.surface,canvas.width,canvas.height-pose.surface);
  context.strokeStyle='#70aeb7';context.setLineDash([8,6]);context.beginPath();context.moveTo(0,pose.surface);context.lineTo(canvas.width,pose.surface);context.stroke();context.setLineDash([]);
  context.fillStyle='#50818b';context.font='14px system-ui';context.fillText('水面',12,pose.surface-8);
  for(const layer of order) {
    const index=current.manifest.layers.indexOf(layer), asset=assets[index];
    context.save();
    if(layer==='line'){context.translate(0,pose.linePivot);context.scale(1,pose.lineScale);context.translate(0,-pose.linePivot);}
    else if(['fish','bobber','bait'].includes(layer))context.translate(0,pose[layer]);
    if(sample?.Root)context.transform(...sample.Root);
    if(sample) context.transform(...sample.Transforms[index]);
    if(layer==='line' && sample?.Line){const points=sample.Line;context.beginPath();context.moveTo(points[0],points[1]);context.quadraticCurveTo(points[2],points[3],points[4],points[5]);context.strokeStyle='#3e3628';context.lineWidth=1.4;context.stroke();}
    else context.drawImage(asset,0,0,640,512);context.restore();
  }
}
async function render() {
  const version=++renderVersion,current=config(),duration=current.source.durationsMs.reduce((sum,value)=>sum+value,0);
  const originalTime=current.clip==='groundbait'?elapsed:timeline(elapsed,current);
  const original=atFrame(current.source.durationsMs,originalTime);
  const time=timeline(elapsed,current), index=atFrame(current.samples.map(frame=>frame.DurationMs),time),sample=current.samples[index];
  await Promise.all([draw(element('before'),current,null,original,version),draw(element('after'),current,sample,sample.Source,version)]);
  if(version!==renderVersion)return;
  element('before-info').textContent=`原关键帧 ${original+1}/${current.source.frames.length} · ${Math.round(elapsed)} ms`;
  element('after-info').textContent=`采样 ${index+1}/${current.samples.length} · 源姿势 ${sample.Source+1} · `+
    (current.clip==='groundbait'?(elapsed>=current.end?'收尾 / 准备':elapsed<100?'起手':'循环打窝'):current.source.playback==='intro-loop'||data.motion.Clips[current.clip].Loop?'循环衔接':'总时长不变');
  element('summary').textContent=`${current.source.frames.length} 张关键姿势 → ${current.samples.length} 个运动采样，单段 ${duration} ms。新增解码贴图：0。姿势变化仍使用原画；入座、离席、挂饵和收获使用脚底固定的整体微转，手持鱼随角色一起运动。`;
  element('finish').disabled=current.clip!=='groundbait';
  element('scrub').max=current.clip==='groundbait'?current.end+280:duration;
  element('scrub').value=current.loop?time:elapsed;
}
function restart(){elapsed=0;stopAt=Infinity;render();}
['pet','clip','duration'].forEach(id=>element(id).addEventListener('change',restart));
element('surface').addEventListener('input',()=>render());
element('play').onclick=()=>{playing=!playing;element('play').textContent=playing?'暂停':'播放';};
element('restart').onclick=restart;
element('finish').onclick=()=>{stopAt=elapsed;render();};
element('scrub').oninput=()=>{playing=false;element('play').textContent='播放';elapsed=Number(element('scrub').value);render();};
function step(direction){playing=false;element('play').textContent='播放';const current=config();const boundaries=[0];current.samples.forEach(frame=>boundaries.push(boundaries.at(-1)+frame.DurationMs));const local=timeline(elapsed,current),target=direction>0?(boundaries.find(value=>value>local+.1)??boundaries.at(-1)):([...boundaries].reverse().find(value=>value<local-.1)??0);elapsed=Math.max(0,elapsed+target-local);render();}
element('previous').onclick=()=>step(-1);element('next').onclick=()=>step(1);
function tick(now){const delta=Math.min(now-previousTime,100);previousTime=now;if(playing){elapsed+=delta*Number(element('speed').value);const current=config(),limit=current.clip==='groundbait'?current.end+1160:current.loop?Infinity:current.source.durationsMs.reduce((sum,value)=>sum+value,0)+800;if(elapsed>limit)restart();else render();}requestAnimationFrame(tick);}
render();requestAnimationFrame(tick);
