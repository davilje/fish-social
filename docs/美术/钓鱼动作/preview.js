const data = window.fishingMotionPreview;
const element = id => document.getElementById(id);
const names = {orange:'橘猫',black:'黑猫',white:'白猫',cow:'奶牛猫',calico:'三花猫',blue:'蓝猫',brown:'棕猫',cream:'奶油猫',silver:'银渐层',dafeiyu:'大肥鱼',sangbiao:'狸花猫 · 丧彪'};
const clips = {sit:'站立到入座',bait:'挂饵',cast:'抛竿',fishing:'等待',bite:'咬钩后仰',effort:'持续发力',hooked:'咬钩 → 循环发力',reel:'收杆出水',reel_empty:'空军收杆',catch:'展示收获',reset_catch:'收获后复位',reset_empty:'空军后复位',groundbait:'持续打窝',leave:'离席'};
Object.keys(data.motion.Clips).forEach(id => element('clip').add(new Option(clips[id] || id, id)));
const requestedClip = new URLSearchParams(location.search).get('clip');
element('clip').value=Object.hasOwn(data.motion.Clips,requestedClip)?requestedClip:'fishing';
Object.keys(data.pets).forEach(id => element('pet').add(new Option(names[id] || id, id)));
element('pet').value = 'orange';
const images = new Map();
let elapsed = 0, playing = true, previousTime = performance.now(), stopAt = Infinity, renderVersion = 0;
let asleep = false, restChangedAt = 0, restFrom = 0;
function sleepBlend() {
  const progress = Math.max(0, Math.min(1, (elapsed-restChangedAt)/800));
  return restFrom + ((asleep?1:0)-restFrom)*progress*progress*(3-2*progress);
}
function restingPose(current, sample, legacy=false) {
  if (!sample || current.clip !== 'fishing' || !element('breathing').checked) return null;
  const blend=sleepBlend(), awakeWave=Math.sin(elapsed*Math.PI*2/3200), sleepWave=Math.sin(elapsed*Math.PI*2/4600);
  const strength=Number(element('breath-strength').value);
  const growth=1+(legacy ? .006 : .022*strength)*awakeWave*(1-blend)+(legacy ? .009 : .028*strength)*sleepWave*blend;
  const angle=(2.1+.9*Math.sin(elapsed*Math.PI*2/5800))*blend*Math.PI/180,cosine=Math.cos(angle),sine=Math.sin(angle);
  const matrix=[cosine,sine,-sine*growth,cosine*growth,264-cosine*264+sine*growth*450,450-sine*264-cosine*growth*450];
  const nod=legacy?0:restStudy.nod(elapsed)*blend*Number(element('nod-strength').value);
  const rod=restStudy.rod(nod);
  return {matrix,rod,rodMatrix:restStudy.multiply(matrix,rod),nod,blend,opacity:blend*(.65+.25*sleepWave),rise:3*(1+sleepWave)};
}
function setSleep(value) {
  if(element('clip').value!=='fishing'){element('clip').value='fishing';restart();}
  restFrom=sleepBlend();restChangedAt=elapsed;asleep=value;element('breathing').checked=true;
  element('rest-status').textContent=value?'模拟：30 分钟无人照看，已睡着。点击右侧猫咪可唤醒。':'模拟：已触碰照看，重新计时 30 分钟。';
  render();
}
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
function contactPose(current,sample,sourceIndex,updated,rest) {
  const position=sample?.Position??sourceIndex,count=current.source.frames.length;
  const frame=sample && (sample.Line || sample.Position!==sample.Source)?sample.Contact:data.contact[current.clip][sourceIndex];
  const settings=data.motion.WaterContact,surface=Number(element('surface').value),reference=settings.ReferenceSurfaceY;
  const progress=count>1?Math.max(0,Math.min(1,position/(count-1))):1;
  const landing=count>=6?Math.max(0,Math.min(1,position-(count-2))):progress;
  const weight=['fishing','hooked','bite','effort'].includes(current.clip)?1:current.clip==='cast'?landing**2:['reel','reel_empty'].includes(current.clip)?(1-progress)**2*Math.max(0,Math.min(1,2-position)):0;
  const delta=Math.max(0,surface-reference),bobber=Math.max(0,surface-(frame.BobberY>=0?frame.BobberY:reference))*weight;
  const fish=(frame.FishTop>=0?Math.max(delta,surface+(updated?settings.FishDepth:14)-frame.FishTop):delta)*weight;
  let lineRest=null,lineTop=frame.LineTop;
  if(rest){
    const [tipX,tipY]=sample?.Line||[541.5,149],matrix=rest.rodMatrix,bottom=frame.LineBottom,length=bottom-tipY;
    const dx=matrix[0]*tipX+matrix[2]*tipY+matrix[4]-tipX,dy=matrix[1]*tipX+matrix[3]*tipY+matrix[5]-tipY;
    if(length>1)lineRest=[1,0,-dx/length,1-dy/length,dx*bottom/length,dy*bottom/length];
    lineTop=tipY+dy;
  }
  const length=frame.LineBottom-lineTop;
  const lineScale=length>1?1+(frame.FishTop>=0?fish:bobber)/length:1;
  const bait=current.clip==='reel_empty' && position>=2?bobber:current.clip==='groundbait' && updated && position>=3 && position<6
    ?(delta+settings.GroundbaitDrop)*((position-3)/3)**2:delta*weight;
  return {lineScale,linePivot:lineTop,lineRest,bobber,fish,bait,surface};
}
async function draw(canvas, current, sample, sourceIndex, version, legacy=false) {
  const order=current.source.frames[sourceIndex];
  const rest=restingPose(current,sample,legacy);
  const assets=await Promise.all(current.manifest.layers.map(layer => !sample && ['reel','reel_empty'].includes(current.clip) && sourceIndex>=2 && ['line','bait'].includes(layer)
    ?image(`line-review/original/${current.clip}/${sourceIndex}/${layer}.png`):image('../../../desktop-overlay/OverlayResources/fishing/'+
    (current.manifest.bodyLayers.includes(layer)?`${current.pet}/body/`:'shared/')+`${current.clip}/${sourceIndex}/${layer==='head' && rest && asleep && data.sleepHeads?.includes(current.pet)?'head-sleep':layer}.png`)));
  if(version!==renderVersion)return;
  const context=canvas.getContext('2d');context.clearRect(0,0,canvas.width,canvas.height);
  const pose=contactPose(current,sample,sourceIndex,!!sample,rest);
  context.fillStyle='#a9d9de55';context.fillRect(0,pose.surface,canvas.width,canvas.height-pose.surface);
  context.strokeStyle='#70aeb7';context.setLineDash([8,6]);context.beginPath();context.moveTo(0,pose.surface);context.lineTo(canvas.width,pose.surface);context.stroke();context.setLineDash([]);
  context.fillStyle='#50818b';context.font='14px system-ui';context.fillText('水面',12,pose.surface-8);
  for(const layer of order) {
    const index=current.manifest.layers.indexOf(layer), asset=assets[index];
    context.save();
    if(layer==='line'){context.translate(0,pose.linePivot);context.scale(1,pose.lineScale);context.translate(0,-pose.linePivot);}
    else if(['fish','bobber','bait'].includes(layer))context.translate(0,pose[layer]);
    if(rest && ['body','head','far_hand','hands','rod'].includes(layer))context.transform(...rest.matrix);
    if(rest && layer==='rod')context.transform(...rest.rod);
    if(layer==='line' && pose.lineRest)context.transform(...pose.lineRest);
    if(sample?.Root)context.transform(...sample.Root);
    if(sample) context.transform(...sample.Transforms[index]);
    if(layer==='line' && sample?.Line){const points=sample.Line;context.beginPath();context.moveTo(points[0],points[1]);context.quadraticCurveTo(points[2],points[3],points[4],points[5]);context.strokeStyle='#3e3628';context.lineWidth=1.4;context.stroke();}
    else if(rest && current.manifest.bodyLayers.includes(layer))restStudy.draw(context,asset,rest.nod);
    else context.drawImage(asset,0,0,640,512);context.restore();
  }
  if(rest && rest.blend>.01){context.save();context.globalAlpha=rest.opacity;context.fillStyle='#75563f';context.font='bold 24px system-ui';context.fillText('Z z z',325,170-rest.rise);context.restore();}
}
async function render() {
  const version=++renderVersion,current=config(),duration=current.source.durationsMs.reduce((sum,value)=>sum+value,0);
  const originalTime=current.clip==='groundbait'?elapsed:timeline(elapsed,current);
  const original=atFrame(current.source.durationsMs,originalTime);
  const time=timeline(elapsed,current), index=atFrame(current.samples.map(frame=>frame.DurationMs),time),sample=current.samples[index];
  const comparing=current.clip==='fishing';
  await Promise.all([draw(element('before'),current,comparing?sample:null,comparing?sample.Source:original,version,true),draw(element('after'),current,sample,sample.Source,version)]);
  if(version!==renderVersion)return;
  element('before-title').textContent=comparing?'原版 · 轻微呼吸 / 睡姿':'关键帧播放';
  element('after-title').textContent=comparing?'已采用 · 加强呼吸 / 低头再抬头':'连续轨迹采样';
  element('before-info').textContent=comparing?`原版呼吸 ±0.6% / 睡着 ±0.9% · ${Math.round(elapsed)} ms`:`关键帧 ${original+1}/${current.source.frames.length} · ${Math.round(elapsed)} ms`;
  element('after-info').textContent=`采样 ${index+1}/${current.samples.length} · 源姿势 ${sample.Source+1} · `+
    (current.clip==='groundbait'?(elapsed>=current.end?'收尾 / 准备':elapsed<100?'起手':'循环打窝'):current.source.playback==='intro-loop'||data.motion.Clips[current.clip].Loop?'循环衔接':'总时长不变');
  element('summary').textContent=comparing?`当前预览：呼吸幅度 ${(2.2*Number(element('breath-strength').value)).toFixed(1)}%，睡着 ${(2.8*Number(element('breath-strength').value)).toFixed(1)}%；6.4 秒一轮瞌睡，8 个动作阶段：放松 → 缓慢低头 → 低头停顿 → 抬头 → 轻回落 → 恢复。复用原图做连续形变，并非新绘制的角色帧；脚底固定，身体尺寸每轮回到原值。默认两项“明显”档位对应正式参数。`:`${current.source.frames.length} 张关键姿势 → ${current.samples.length} 个运动采样，单段 ${duration} ms。`;
  element('finish').disabled=current.clip!=='groundbait';
  element('scrub').max=comparing?restStudy.duration:current.clip==='groundbait'?current.end+280:duration;
  element('scrub').value=comparing?elapsed%restStudy.duration:current.loop?time:elapsed;
}
function restart(){elapsed=0;stopAt=Infinity;asleep=false;restChangedAt=0;restFrom=0;render();}
['pet','clip','duration'].forEach(id=>element(id).addEventListener('change',restart));
element('surface').addEventListener('input',()=>render());
element('play').onclick=()=>{playing=!playing;element('play').textContent=playing?'暂停':'播放';};
element('restart').onclick=restart;
element('finish').onclick=()=>{stopAt=elapsed;render();};
element('scrub').oninput=()=>{playing=false;element('play').textContent='播放';elapsed=Number(element('scrub').value);if(element('clip').value==='fishing'){restFrom=asleep?1:0;restChangedAt=elapsed;}render();};
function step(direction){playing=false;element('play').textContent='播放';const current=config();if(current.clip==='fishing'){elapsed=Math.max(0,elapsed+direction*100);render();return;}const boundaries=[0];current.samples.forEach(frame=>boundaries.push(boundaries.at(-1)+frame.DurationMs));const local=timeline(elapsed,current),target=direction>0?(boundaries.find(value=>value>local+.1)??boundaries.at(-1)):([...boundaries].reverse().find(value=>value<local-.1)??0);elapsed=Math.max(0,elapsed+target-local);render();}
element('previous').onclick=()=>step(-1);element('next').onclick=()=>step(1);
element('breathing').onchange=()=>render();
['breath-strength','nod-strength'].forEach(id=>element(id).oninput=()=>render());
element('sleep').onclick=()=>setSleep(true);
element('wake').onclick=()=>setSleep(false);
element('after').onclick=()=>{if(element('clip').value==='fishing')setSleep(false);};
element('bite-test').onclick=()=>{element('rest-status').textContent=asleep?'模拟咬钩：直接脱钩，鱼获 +0，继续睡着。':'模拟咬钩：清醒，可进入原有咬钩和收杆流程。';};
function tick(now){const delta=Math.min(now-previousTime,100);previousTime=now;if(playing){elapsed+=delta*Number(element('speed').value);const current=config(),limit=current.clip==='groundbait'?current.end+1160:current.loop?Infinity:current.source.durationsMs.reduce((sum,value)=>sum+value,0)+800;if(elapsed>limit)restart();else render();}requestAnimationFrame(tick);}
render();requestAnimationFrame(tick);
