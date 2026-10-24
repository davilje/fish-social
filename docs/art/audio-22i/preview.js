const $ = id => document.getElementById(id);
const policy = await fetch('policy.json').then(r => r.json());
const bundled = 'baseline-v1/';
const proposed = new Set(['bite', 'reel', 'escape', 'groundbait']);
const scenarios = {
  fishing: {duration: 7.5, description: '自己的抛竿 → 入水 → 等待 → 咬钩 → 收杆 → 鱼获入包。庆祝后再轻喵一声。', events: [
    [0, 'ui_click'], [.8, 'cast'], [1.4, 'splash'], [3.5, 'bite', 'bite:1'], [4.7, 'reel'], [5.4, 'catch', 'catch:1'], [6.1, 'meow', 'voice:1', 'self', true]]},
  miss: {duration: 5, description: '咬钩后脱钩，只用轻短的落水尾音提示；主动停钓播放收杆，不播放失败或收获庆祝。', events: [[0, 'bite', 'bite:1'], [1.4, 'escape', 'miss:1'], [3.3, 'reel']]},
  groundbait: {duration: 7.5, description: '撒饵只在入水时轻响；720 毫秒动作循环每三轮最多响一次。取消后，后续撒饵声音立即停止。', events: [
    [0, 'ui_click'], ...Array.from({length: 9}, (_, i) => [.35 + i * .72, 'groundbait', 'throw:'+i]), [5.5, 'cancel'], [5.6, 'ui_close']]},
  rapid: {duration: 3.5, description: '连点、连续开关和反复成功回调：点击共用 90 毫秒间隔；界面音一次只播一个，成功提示只响一次。', events: [
    ...Array.from({length: 20}, (_, i) => [i*.04, ['ui_click','ui_open','ui_close','ui_toggle'][i%4]]), [1.3, 'ui_success', 'purchase:1'], [1.8, 'ui_success', 'purchase:1'], [2.6, 'ui_error', 'purchase:2']]},
  duplicate: {duration: 7.5, description: '同一条咬钩或收获消息即使隔一秒重发也只响一次。重连首个快照只恢复画面，新的钓鱼事件再发声。', events: [
    [0, 'bite', 'bite:1'], [.4, 'bite', 'bite:1'], [1.2, 'bite', 'bite:1'], [2.2, 'catch', 'catch:1'], [2.6, 'catch', 'catch:1'], [2.9, 'meow', 'voice:1', 'self', true], [3.4, 'catch', 'catch:1'], [4.1, 'snapshot'], [5.5, 'bite', 'bite:2']]},
  crowd: {duration: 5, description: '8 位玩家一起抛竿和咬钩，只播放自己的声音。背景玩家的动画照常进行，不增加声场噪声。', events: [
    ...Array.from({length:8}, (_,i) => [i*.03, 'cast', 'cast:'+i, i===0?'self':'other']), [1,'splash','water:1'],
    ...Array.from({length:8}, (_,i) => [2+i*.03, 'bite', 'bite:'+i, i===0?'self':'other']), [3.5,'catch','catch:1']]}
};
Object.values(scenarios).forEach(s => s.events.sort((a,b) => a[0]-b[0]));

// This executable proposal is deliberately isolated from game playback.
class AudioPolicy {
  constructor() { this.reset(); }
  reset() { this.seen = new Set(); this.last = {}; this.lastUi = -Infinity; this.nextMeow=0; this.nextAutoMeow=0; this.cancelled=false; }
  decide(cue, time, id='', owner='self', automatic=false) {
    if (cue==='snapshot') return {allow:false, reason:'重连快照，不重播旧动作'};
    if (cue==='cancel') { this.cancelled=true; return {allow:false, reason:'已取消打窝'}; }
    const p = policy.cues[cue];
    if (!p) return {allow:false, reason:'未知音效'};
    if (owner!=='self' && p.group!=='ui') return {allow:false, reason:'其他玩家，保持安静'};
    if (this.cancelled && cue==='groundbait') return {allow:false, reason:'打窝已结束'};
    if (id && this.seen.has(id)) return {allow:false, reason:'同一事件，已播放'};
    if (time-(this.last[cue]??-Infinity)<p.cooldown-1e-6) return {allow:false, reason:'间隔内，省略重复声'};
    if (p.group==='ui' && p.priority<2 && time-this.lastUi<policy.uiGlobalCooldown-1e-6) return {allow:false, reason:'连续点击，合并反馈'};
    if (cue==='meow' && (time<this.nextMeow || (automatic && time<this.nextAutoMeow))) return {allow:false, reason:'猫咪正在休息'};
    if (id) this.seen.add(id);
    this.last[cue]=time;
    if (p.group==='ui') this.lastUi=time;
    if (cue==='meow') { this.nextMeow=time+policy.meowCooldown; if(automatic) this.nextAutoMeow=time+policy.autoMeowCooldown; }
    return {allow:true, ...p};
  }
}

let context, loadPromise, musicSource, musicGain, musicMaster, sfxMaster, master;
const buffers=new Map(), active=new Set(), timers=new Set();
const gate=new AudioPolicy();
let selection='fishing', generation=0, frame=0, start=0, playing=false, duckUntil=0, lastMeow=-1;
let counters={played:0,skipped:0}, observedPeak=0;
function later(fn, delay) { const id=setTimeout(()=>{timers.delete(id); fn();},delay);timers.add(id);return id; }
function updateVolumes() {
  $('musicValue').value=$('music').value+'%'; $('effectsValue').value=$('effects').value+'%';
  if(context) { musicMaster.gain.setTargetAtTime(Number($('music').value)/100,context.currentTime,.035); sfxMaster.gain.setTargetAtTime(Number($('effects').value)/100,context.currentTime,.035); }
}
async function ready() {
  if(!context) {
    context=new AudioContext();
    // Soft limiter protects the preview if several manually auditioned cues overlap.
    const limiter=context.createDynamicsCompressor();limiter.threshold.value=-8;limiter.knee.value=10;limiter.ratio.value=6;limiter.attack.value=.003;limiter.release.value=.12;limiter.connect(context.destination);
    master=context.createGain();master.connect(limiter);
    musicMaster=context.createGain();musicMaster.connect(master);
    sfxMaster=context.createGain();sfxMaster.connect(master);updateVolumes();
  }
  await context.resume();
  if(!loadPromise) loadPromise=Promise.all([
    ...Object.keys(policy.cues).filter(x=>x!=='meow').map(c=>[c,proposed.has(c)?'assets/'+c+'.wav':bundled+'sfx/'+c+'.wav']),
    ...[1,2,3].map(i=>['meow_0'+i,bundled+'sfx/meow_0'+i+'.wav']),['music',bundled+'music/pond_daylight.wav']
  ].map(async ([id,path])=>{const r=await fetch(path);if(!r.ok)throw Error(path+' '+r.status);buffers.set(id,await context.decodeAudioData(await r.arrayBuffer()));})).catch(e=>{loadPromise=null;throw e;});
  await loadPromise;
}
function counts() { $('played').textContent=counters.played; $('skipped').textContent=counters.skipped; $('active').textContent=active.size; observedPeak=Math.max(observedPeak,active.size); }
function log(cue, reason, skipped=false) {
  const li=document.createElement('li');li.classList.toggle('muted',skipped);
  const time=document.createElement('time'); time.textContent=playing?((context.currentTime-start).toFixed(1)+'s'):'试听';
  const text=document.createElement('span');text.textContent=(policy.cues[cue]?.label??'场景')+' · '+reason;
  li.append(time,text);$('events').prepend(li);while($('events').children.length>45)$('events').lastChild.remove();
}
function stopNode(node) { node.source.stop();active.delete(node); }
function duck(seconds) {
  if(!musicGain)return;duckUntil=Math.max(duckUntil,context.currentTime+seconds);
  musicGain.gain.cancelScheduledValues(context.currentTime);musicGain.gain.setTargetAtTime(.5,context.currentTime,.045);
  later(()=>{if(musicGain&&context.currentTime>=duckUntil-.02)musicGain.gain.setTargetAtTime(1,context.currentTime,.22);},seconds*1000+25);
}
function emit(cue,id='',owner='self',automatic=false) {
  const time=context.currentTime;
  const rule=gate.decide(cue,time,id,owner,automatic);
  if(!rule.allow) { if(cue==='cancel') for(const node of [...active]) if(node.cue==='groundbait')stopNode(node);counters.skipped++;log(cue,rule.reason,true);counts();return false; }
  const channel=[...active].filter(n=>n.group===rule.group);
  if(channel.length>=policy.groups[rule.group]) {
    const replace=channel.filter(n=>n.priority<=rule.priority).sort((a,b)=>a.priority-b.priority||a.started-b.started)[0];
    if(!replace) { counters.skipped++;log(cue,'保留更重要的提示',true);counts();return false; }
    stopNode(replace);
  }
  if(Number($('effects').value)===0){counters.skipped++;log(cue,'已静音',true);counts();return false;}
  let key=cue;
  if(cue==='meow') {lastMeow=(lastMeow+1+Math.floor(Math.random()*2))%3;key='meow_0'+(lastMeow+1);}
  const source=context.createBufferSource(),gain=context.createGain();source.buffer=buffers.get(key);gain.gain.value=rule.gain;source.connect(gain);gain.connect(sfxMaster);
  const node={source,gain,cue,group:rule.group,priority:rule.priority,started:time};active.add(node);
  source.onended=()=>{active.delete(node);source.disconnect();gain.disconnect();counts();};source.start();
  if(rule.duck)duck(rule.duck);
  counters.played++;counts();log(cue,'播放');$('sceneLabel').textContent=rule.label;
  if(['bite','splash','groundbait','catch'].includes(cue)) { const scene=document.querySelector('.scene');scene.classList.remove('flash');void scene.offsetWidth;scene.classList.add('flash'); }
  return true;
}
function music() {
  if(!$('withMusic').checked)return;
  musicSource=context.createBufferSource();musicSource.buffer=buffers.get('music');musicSource.loop=true;
  musicGain=context.createGain();musicGain.gain.setValueAtTime(0,context.currentTime);musicGain.gain.linearRampToValueAtTime(1,context.currentTime+.5);
  musicSource.connect(musicGain);musicGain.connect(musicMaster);musicSource.start();
}
function stop(resetProgress=true) {
  generation++;playing=false;for(const id of timers)clearTimeout(id);timers.clear();cancelAnimationFrame(frame);
  for(const node of [...active])stopNode(node);
  if(musicSource){musicSource.stop();musicSource.disconnect();musicSource=null;}if(musicGain){musicGain.disconnect();musicGain=null;}
  duckUntil=0;gate.reset();counts();$('status').textContent='已停止';$('play').disabled=false;if(resetProgress)$('progress').style.width='0';
}
async function play() {
  stop();const token=generation;$('play').disabled=true;$('status').textContent='准备声音…';
  try {await ready();if(token!==generation)return;counters={played:0,skipped:0};observedPeak=0;$('events').replaceChildren();gate.reset();start=context.currentTime;playing=true;counts();music();
    $('status').textContent='正在试听';const s=scenarios[selection];
    const delay=Number($('delay').value);
    s.events.forEach(([at,cue,id,owner,auto])=>later(()=>emit(cue,id,owner,auto),at*1000+(['cast','splash','reel','groundbait'].includes(cue)?delay:0)));
    function tick(){if(!playing)return;$('progress').style.width=Math.min(100,(context.currentTime-start)/s.duration*100)+'%';frame=requestAnimationFrame(tick);}tick();
    later(()=>{stop(false);$('progress').style.width='100%';$('status').textContent='片段结束，可切换场景比较';},s.duration*1000);
  }catch(e){$('status').textContent='声音加载失败，请重新点击播放';$('play').disabled=false;console.error(e);}
}
function select(id){stop();selection=id;$('description').textContent=scenarios[id].description;document.querySelectorAll('[data-scenario]').forEach(b=>{b.classList.toggle('selected',b.dataset.scenario===id);b.setAttribute('aria-pressed',String(b.dataset.scenario===id));});$('status').textContent='选择场景后开始';}
$('scenarios').addEventListener('click',e=>{const b=e.target.closest('[data-scenario]');if(b)select(b.dataset.scenario);});
$('play').addEventListener('click',play);$('stop').addEventListener('click',()=>stop());
for(const id of ['music','effects'])$(id).addEventListener('input',updateVolumes);
$('withMusic').addEventListener('change',()=>{if(musicSource){musicSource.stop();musicSource.disconnect();musicSource=null;musicGain.disconnect();musicGain=null;}if(playing&&$('withMusic').checked)music();});
for(const [cue,p] of Object.entries(policy.cues)){
  const b=document.createElement('button');b.textContent='▷ '+p.label;b.dataset.cue=cue;
  if(proposed.has(cue)){const small=document.createElement('small');small.textContent='新音色';b.append(small);}
  b.addEventListener('click',async()=>{const token=generation;try{await ready();if(token===generation)emit(cue);}catch(e){$('status').textContent='声音加载失败';console.error(e);}});$('samples').append(b);
}
document.addEventListener('visibilitychange',()=>{if(document.hidden)stop();});
window.addEventListener('pagehide',()=>stop());
// Expose the scenario policy for browser verification, never game integration.
window.audioPreview={AudioPolicy,policy,scenarios,emit,ready,stop,get stats(){return {...counters,active:active.size,peak:observedPeak};}};
select(selection);
