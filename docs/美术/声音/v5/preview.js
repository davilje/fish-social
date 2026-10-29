const $ = id => document.getElementById(id);
const ROOT = '../../../../fish-social-unity/Assets/StreamingAssets/Audio/';
const LABELS = {ui_click:'轻触纸面',ui_open:'翻开书页',ui_close:'合上纸本',ui_toggle:'拨动纸签',ui_tick:'铅笔短擦',ui_success:'轻轻盖章',ui_error:'纸片退回',cast:'挥竿',splash:'浮漂入水',bite:'咬钩',water_struggle:'扰水',water_slap:'拍水',reel:'收线',escape:'逃脱触水',groundbait:'窝料入水',catch:'纸面记录',meow:'猫咪回应'};
const family = cue => cue.replace(/_(s|m|l|xl)$/, '');
const marker = cue => ({bite:'bite.onset',catch:'result.presented',escape:'escape.contact',water_struggle:'fish.surface',water_slap:'fish.slap',splash:'float.contact',cast:'cast.release',reel:'retrieve.start',groundbait:'groundbait.contact'})[family(cue)] || family(cue);
const clamp = value => Math.min(1, Math.max(0, Number(value) || 0));
let config, ambience;
try {
  [config,ambience] = await Promise.all(['cue-config.json','ambience-manifest.json'].map(path=>fetch(ROOT+path,{cache:'no-store'}).then(r=>{if(!r.ok)throw Error(path+' '+r.status);return r.json();})));
  if(ambience.version!==2 || ambience.loops?.length!==3 || new Set(ambience.loops.map(x=>x.id)).size!==3 || !['water','wind','leaves'].every(id=>ambience.loops.some(x=>x.id===id)) || !ambience.loops.every(loop=>ambience.assets.some(a=>a.path===loop.path)))throw Error('三层环境声清单无效');
} catch (error) { $('status').textContent='配置未能加载，请从仓库 HTTP 服务打开本页：'+error.message; throw error; }
const TRACK_LABELS={water:'水声',wind:'风声',leaves:'树叶沙沙声'};
const cues = new Map(config.cues.map(c=>[c.cue,c]));

// Demonstration policy; production code is separately verified in Unity/WPF.
export class PreviewPolicy {
  constructor(){this.reset();}
  reset(){this.seen=new Set();this.lastUi=-Infinity;this.lastTick=-Infinity;this.lastWater=-Infinity;this.lastBait=-Infinity;this.lastMeow=-Infinity;this.lastAuto=-Infinity;this.cancelled=new Set();this.lastWaterAction='';this.lastWaterFamily='';}
  decide(event,time,effects=1,visible=true,background=true){
    const p=cues.get(event.cue), kind=family(event.cue);
    if(!p)return {allow:false,reason:'资源未定义'};
    if(event.owner && event.owner!=='self')return {allow:false,reason:'其他玩家静默'};
    if(['bite','catch','escape'].includes(kind) && (event.source==='prediction'||event.authoritative===false))return {allow:false,reason:'结果必须来自权威事件'};
    const key=[event.epoch??0,event.owner||'self',event.id||event.action||'',marker(event.cue),event.occurrence||0].join('|');
    if(event.id || event.action){
      if(this.seen.has(key))return {allow:false,reason:'相同事件已消费'};
      this.seen.add(key);
      if(this.seen.size>512)this.seen.delete(this.seen.values().next().value);
    }
    if(event.source==='snapshot')return {allow:false,reason:'快照静默设基线'};
    if(event.expired)return {allow:false,reason:'事件已过期'};
    if(this.cancelled.has(event.action))return {allow:false,reason:'旧动作已取消'};
    if(p.group==='ui')return {allow:false,reason:'钓鱼场景按钮音关闭'};
    if(!visible && !(kind==='bite' && event.authoritative!==false && background))return {allow:false,reason:'全部窗口隐藏'};
    if(effects<=0)return {allow:false,reason:'静音仍消费事件'};
    if(p.group==='ui' && time-this.lastUi<config.uiInterval-1e-6)return {allow:false,reason:'UI 90ms 合并'};
    if(kind==='ui_tick' && time-this.lastTick<config.tickInterval-1e-6)return {allow:false,reason:'滑杆 180ms 合并'};
    if(kind==='groundbait' && (time-this.lastBait<config.groundbaitInterval-1e-6 || ((event.occurrence||0)%3)!==0))return {allow:false,reason:'撒饵每三轮／2.16s'};
    if(kind==='water_struggle' && ((event.occurrence||0)%3)!==0)return {allow:false,reason:'扰水每三次一次'};
    const replacesWeak=kind==='water_slap' && this.lastWaterFamily==='water_struggle' && this.lastWaterAction===event.action && time-this.lastWater<config.waterInterval;
    if((kind==='water_struggle'||kind==='water_slap') && time-this.lastWater<config.waterInterval-1e-6 && !replacesWeak)return {allow:false,reason:'水面动作 900ms 合并'};
    if(kind==='meow' && (time-this.lastMeow<config.manualMeowInterval-1e-6 || (event.automatic && time-this.lastAuto<config.autoMeowInterval-1e-6)))return {allow:false,reason:'猫咪休息间隔'};
    if(p.group==='ui')this.lastUi=time;
    if(kind==='ui_tick')this.lastTick=time;
    if(kind==='groundbait')this.lastBait=time;
    if(kind==='water_struggle'||kind==='water_slap'){this.lastWater=time;this.lastWaterAction=event.action;this.lastWaterFamily=kind;}
    if(kind==='meow'){this.lastMeow=time;if(event.automatic)this.lastAuto=time;}
    return {allow:true,...p,replacesWeak};
  }
}

const scenarios = {
  fishing:{name:'正常钓鱼',duration:7.3,description:'自然水波持续铺底，挥竿、浮漂、咬钩与猫叫随动作响起。BGM 与按钮音始终关闭。',events:[
    [0,'ui_click'],[.4,'cast',{action:'cast1'}],[1,'splash',{action:'cast1'}],[2.3,'bite',{id:'fish1'}],[3.1,'water_struggle',{action:'fish1',occurrence:0}],[4.2,'reel',{action:'fish1'}],[5,'catch',{id:'fish1',celebrate:true}]]},
  miss:{name:'脱钩／停钓',duration:5.3,description:'先演示有实际接触的权威逃脱；普通停钓只收线，不虚构脱钩或庆祝。',events:[[0,'bite',{id:'miss1'}],[1.1,'escape',{id:'miss1'}],[2.4,'reel',{action:'stop1'}],[3,'cancel',{action:'stop1'}],[3.8,'escape',{id:'not-a-result',action:'stop1'}]]},
  groundbait:{name:'持续打窝',duration:7.5,description:'720ms 一轮的撒饵动作，每三轮最多一次且间隔至少 2.16s。结束后的旧接触被丢弃。',events:[...Array.from({length:11},(_,i)=>[i*.72,'groundbait',{action:'bait1',occurrence:i}]),[5.4,'cancel',{action:'bait1'}]]},
  rapid:{name:'按钮保持安静',duration:3.1,description:'连续点击、开合、切换和确认都保持静默，只留下水边的自然环境声。',events:[...Array.from({length:18},(_,i)=>[i*.04,['ui_open','ui_close','ui_toggle'][i%3],{id:'nav'+i}]),[1.2,'ui_error',{id:'purchase1'}],[1.8,'ui_error',{id:'purchase1'}],[2.2,'ui_success',{id:'purchase2'}]]},
  duplicate:{name:'重复消息',duration:5.4,description:'咬钩重发、ACK＋结算复用同一身份；快照只设静默基线，之后的新事件才可响。',events:[[0,'bite',{id:'dup1'}],[.25,'bite',{id:'dup1'}],[1,'catch',{id:'dup1'}],[1.4,'catch',{id:'dup1'}],[2,'bite',{id:'baseline',source:'snapshot'}],[2.4,'bite',{id:'baseline'}],[3.6,'bite',{id:'fresh'}]]},
  crowd:{name:'8 人同塘',duration:5.2,description:'其他七位玩家的自动声音为零；自己的浮漂、咬钩与结果保留。',events:[...Array.from({length:8},(_,i)=>[i*.02,'cast',{action:'cast'+i,owner:i?'other'+i:'self'}]),[.9,'splash',{action:'cast0'}],...Array.from({length:8},(_,i)=>[2+i*.02,'bite',{id:'crowd'+i,owner:i?'other'+i:'self'}]),[3.6,'catch',{id:'crowd0'}]]},
  adjacent:{name:'相邻两个鱼获',duration:3.2,description:'两个不同 catchId 仅隔 160ms，各有一次确认。重发第一个结果仍保持静默；不使用旧版 1.2s cue 冷却。',events:[[0,'catch',{id:'adjacent1'}],[.16,'catch',{id:'adjacent2'}],[.5,'catch',{id:'adjacent1'}],[1.2,'bite',{id:'adjacent3'}],[1.36,'bite',{id:'adjacent4'}]]},
  hidden:{name:'全部窗口隐藏',duration:4.8,description:'隐藏时略过普通水声，仍有效的全新权威咬钩可响一次；恢复窗口的同一标记不重播。',events:[[0,'visibility',{value:false}],[.3,'splash',{action:'hidden-cast'}],[1,'bite',{id:'hidden-fresh'}],[1.5,'bite',{id:'hidden-expired',expired:true}],[2.3,'visibility',{value:true}],[2.5,'bite',{id:'hidden-fresh'}],[3.4,'catch',{id:'hidden-fresh'}]]},
  muteDuck:{name:'环境声静音恢复',duration:4.6,description:'中途静音会立即停下环境声与动作声，恢复时环境声重新淡入；静音期间的结果不会补播。',events:[[0,'bite',{id:'mute1'}],[.35,'splash',{action:'mute-water'}],[1.2,'effectsMute',{value:true}],[1.4,'catch',{id:'muted-catch'}],[1.7,'effectsMute',{value:false}],[1.9,'catch',{id:'muted-catch'}],[2.6,'catch',{id:'new-catch'}]]}
};
Object.values(scenarios).forEach(s=>s.events.sort((a,b)=>a[0]-b[0]));

class PreviewEngine {
  constructor(){
    this.policy=new PreviewPolicy();this.buffers=new Map();this.slots={ui:[null],fishing:[null,null],voice:[null]};
    this.epoch=0;this.timers=new Set();this.played=[];this.skipped=[];this.maxVoices=0;this.activeSources=0;
    this.visible=true;this.effectsMuted=false;this.lastVariants=new Map();this.celebration=0;
    this.effectsVolume=.7;this.background=true;this.stopUntil=0;this.visibilityEpoch=0;
    this.tracks=new Map(ambience.loops.map(loop=>[loop.id,{...loop,level:1,requested:false,wanted:false,request:0,intent:0}]));
  }
  async ready(){
    if(!this.context){
      this.context=new AudioContext();this.sfx=this.context.createGain();this.sfx.connect(this.context.destination);
      this.environmentMaster=this.context.createGain();this.environmentMaster.connect(this.context.destination);
      for(const track of this.tracks.values()){
        track.levelGain=this.context.createGain();track.levelGain.connect(this.environmentMaster);
        track.fade=this.context.createGain();track.fade.gain.value=0;track.fade.connect(track.levelGain);
        track.element=new Audio();track.element.crossOrigin='anonymous';track.element.preload='none';
        track.element.loop=true;track.element.src=ROOT+track.path;
        track.source=this.context.createMediaElementSource(track.element);track.source.connect(track.fade);
      }
      this.updateVolumes();
    }
    await this.context.resume();
  }
  // Legacy water aliases point to the same single element/node, never another source.
  get environmentElement(){return this.tracks.get('water').element;}
  get environmentFade(){return this.tracks.get('water').fade;}
  get environmentRequested(){return [...this.tracks.values()].some(t=>t.requested);}
  get environmentWanted(){return [...this.tracks.values()].some(t=>t.wanted);}
  updateVolumes(){
    if(!this.context)return;
    const now=this.context.currentTime,gain=this.effectsMuted?0:this.effectsVolume;
    this.sfx.gain.cancelScheduledValues(now);this.sfx.gain.setValueAtTime(gain,now);
    this.environmentMaster.gain.setValueAtTime(gain,now);
    for(const track of this.tracks.values())track.levelGain.gain.setValueAtTime(track.gain*track.level,now);
    this.syncEnvironment(gain===0);
  }
  setTrackLevel(id,value){
    const track=this.tracks.get(id);if(!track)return;
    track.level=clamp(value);
    if(this.context){track.levelGain.gain.setValueAtTime(track.gain*track.level,this.context.currentTime);this.syncTrack(track,track.level===0);}
  }
  fadeTrack(track,target,immediate=false){
    const now=this.context.currentTime,fade=track.fade.gain;fade.cancelAndHoldAtTime(now);
    if(immediate){fade.setValueAtTime(target,now);return;}
    const from=fade.value,curve=Float32Array.from({length:128},(_,i)=>{const x=i/127;return from+(target-from)*x*x*(3-2*x);});
    fade.setValueCurveAtTime(curve,now,track.fadeSeconds);
  }
  syncTrack(track,immediate=false){
    if(!this.context)return;
    const wanted=track.requested&&this.visible&&!document.hidden&&!this.effectsMuted&&this.effectsVolume>0&&track.level>0;
    if(wanted===track.wanted&&!immediate)return;
    track.wanted=wanted;const request=++track.request;clearTimeout(track.stopTimer);
    if(!wanted){
      this.fadeTrack(track,0,immediate);
      if(immediate)track.element.pause();
      else track.stopTimer=setTimeout(()=>{if(request===track.request&&!track.wanted)track.element.pause();},track.fadeSeconds*1000+30);
      return;
    }
    if(track.element.paused){track.fade.gain.cancelScheduledValues(this.context.currentTime);track.fade.gain.setValueAtTime(0,this.context.currentTime);}
    track.playPromise=track.element.play().then(()=>{
      if(request!==track.request){if(!track.wanted)track.element.pause();return;}
      this.fadeTrack(track,1);
    }).catch(error=>{if(request===track.request){track.wanted=false;report(error);}});
  }
  syncEnvironment(immediate=false){for(const track of this.tracks.values())this.syncTrack(track,immediate);}
  async startTrack(id){
    const track=this.tracks.get(id);if(!track)return;
    const epoch=this.epoch,intent=++track.intent;await this.ready();
    if(epoch!==this.epoch||intent!==track.intent)return;
    track.requested=true;this.syncTrack(track);await track.playPromise;
  }
  stopTrack(id,immediate=false){const track=this.tracks.get(id);if(!track)return;track.intent++;track.requested=false;this.syncTrack(track,immediate);}
  async startEnvironment(){await Promise.all([...this.tracks.keys()].map(id=>this.startTrack(id)));}
  stopEnvironment(){for(const id of this.tracks.keys())this.stopTrack(id);}
  async load(path){if(this.buffers.has(path))return this.buffers.get(path);const promise=fetch(path).then(r=>{if(!r.ok)throw Error(path+' ('+r.status+')');return r.arrayBuffer();}).then(b=>this.context.decodeAudioData(b));this.buffers.set(path,promise);try{return await promise;}catch(e){this.buffers.delete(path);throw e;}}
  later(fn,ms){const id=setTimeout(()=>{this.timers.delete(id);fn();},ms);this.timers.add(id);return id;}
  async prepare(){await this.ready();await Promise.all(config.cues.filter(c=>c.group!=='ui').flatMap(c=>c.paths.map(p=>this.load(ROOT+p))));}
  stop(){
    this.epoch++;this.celebration++;if(this.context)this.stopUntil=this.context.currentTime+.06;
    for(const timer of this.timers)clearTimeout(timer);this.timers.clear();
    for(const slots of Object.values(this.slots))for(let i=0;i<slots.length;i++){
      const slot=slots[i];if(slot?.source){slot.gain.gain.cancelScheduledValues(this.context.currentTime);slot.gain.gain.setTargetAtTime(0,this.context.currentTime,.012);slot.source.stop(this.context.currentTime+.05);}slots[i]=null;
    }
    for(const id of this.tracks.keys())this.stopTrack(id,true);
    this.effectsMuted=false;this.visible=true;this.updateVolumes();
  }
  reset(){this.stop();this.policy.reset();this.played=[];this.skipped=[];this.maxVoices=this.activeSources;this.lastVariants.clear();$('log').replaceChildren();}
  log(event,allowed,reason){const line={cue:event.cue,id:event.id||event.action||'',reason,time:this.context?.currentTime||0};(allowed?this.played:this.skipped).push(line);const li=document.createElement('li');li.textContent=(allowed?'播放 ':'省略 ')+(LABELS[family(event.cue)]||event.cue)+(line.id?' · '+line.id:'')+' · '+reason;if(!allowed)li.className='skipped';$('log').prepend(li);while($('log').children.length>90)$('log').lastChild.remove();}
  selectVariant(cue,event){const paths=cues.get(cue).paths;if(event.variant!=null)return event.variant%paths.length;let hash=0;for(const char of (event.id||event.action||String(this.context.currentTime)))hash=(hash*31+char.charCodeAt(0))>>>0;let index=hash%paths.length;if(index===this.lastVariants.get(cue))index=(index+1)%paths.length;this.lastVariants.set(cue,index);return index;}
  tick(){
    $('voices').textContent=this.activeSources+' / 4';$('played').textContent=this.played.length;$('skipped').textContent=this.skipped.length;
    let playing=0;
    for(const track of this.tracks.values()){
      const state=track.wanted?'循环中':track.element&&!track.element.paused?'淡出中':track.requested?'已静音':'未播放';
      if(track.wanted)playing++;
      const label=$('track-'+track.id+'-state');if(label)label.textContent=state;
      const play=$('track-'+track.id+'-play');if(play)play.setAttribute('aria-pressed',String(track.requested));
    }
    $('environmentState').textContent=playing?playing+' 层循环中':this.environmentRequested?'已静音':'未播放';
    $('environmentLabel').textContent=playing?'水边自然声 · '+playing+' 层叠加播放':'各层独立控制，也可一起播放。';
  }
  async emit(event){const epoch=this.epoch,visibilityEpoch=this.visibilityEpoch;await this.ready();if(epoch!==this.epoch)return false;event={...event,epoch};const p=this.policy.decide(event,this.context.currentTime,this.effectsMuted?0:this.effectsVolume,this.visible,this.background);if(family(event.cue)==='bite' && (p.allow||p.reason==='静音仍消费事件'||p.reason==='全部窗口隐藏'))this.celebration++;if(!p.allow){this.log(event,false,p.reason);return false;}
    const index=this.selectVariant(event.cue,event);let buffer;try{buffer=await this.load(ROOT+p.paths[index]);}catch(e){this.log(event,false,'缺少资源，静默');$('status').textContent='资源尚未就绪：'+e.message;return false;}if(epoch!==this.epoch || this.policy.cancelled.has(event.action) || (family(event.cue)!=='bite' && visibilityEpoch!==this.visibilityEpoch)){this.log(event,false,'加载完成时动作已失效');return false;}
    const slots=this.slots[p.group];let slotIndex=slots.findIndex(x=>!x);let replaced=null;
    if(p.replacesWeak){const weak=slots.findIndex(x=>x?.action===event.action && family(x.cue)==='water_struggle');if(weak>=0)slotIndex=weak;}
    if(slotIndex<0){if(event.automatic && p.group==='voice'){this.log(event,false,'猫咪声部忙，放弃庆祝');return false;}let lowest=0;for(let i=1;i<slots.length;i++)if(slots[i].priority<slots[lowest].priority || (slots[i].priority===slots[lowest].priority && slots[i].started<slots[lowest].started))lowest=i;const old=slots[lowest];if(p.priority<old.priority || (p.priority===old.priority && !['bite','catch','escape'].includes(family(event.cue)))){this.log(event,false,'声部已满，不补播');return false;}slotIndex=lowest;}
    replaced=slots[slotIndex];const token={priority:p.priority,started:this.context.currentTime,cue:event.cue,action:event.action,source:null,availableAt:Math.max(this.stopUntil,replaced?.availableAt||0)};slots[slotIndex]=token;
    let wait=Math.max(0,(token.availableAt-this.context.currentTime)*1000);if(replaced?.source){const t=this.context.currentTime;replaced.gain.gain.cancelAndHoldAtTime(t);replaced.gain.gain.linearRampToValueAtTime(0,t+.04);replaced.source.stop(t+.04);token.availableAt=t+.045;wait=Math.max(wait,45);}
    const start=()=>{if(epoch!==this.epoch || slots[slotIndex]!==token || token.cancelled || this.policy.cancelled.has(event.action)){if(slots[slotIndex]===token)slots[slotIndex]=null;return;}const source=this.context.createBufferSource(),gain=this.context.createGain();source.buffer=buffer;gain.gain.value=p.gain;source.connect(gain);gain.connect(this.sfx);Object.assign(token,{source,gain});source.onended=()=>{this.activeSources--;source.disconnect();gain.disconnect();if(slots[slotIndex]===token)slots[slotIndex]=null;};this.activeSources++;this.maxVoices=Math.max(this.maxVoices,this.activeSources);source.start();this.log(event,true,'已采用音效'+' / 变体 '+(index+1));this.visual(event);
      if(event.celebrate){const celebration=++this.celebration;this.later(()=>{if(celebration===this.celebration && epoch===this.epoch)this.emit({cue:'meow',id:event.id,automatic:true});},(buffer.duration+.12)*1000);}
    };if(wait)this.later(start,wait);else start();return true;
  }
  visual(event){const kind=family(event.cue);const concealed=event.blind && $('reveal').dataset.revealed!=='true';$('sceneNote').textContent=concealed?'盲听片段':LABELS[kind]||kind;if(['splash','bite','water_struggle','water_slap','escape','groundbait'].includes(kind)){$('ripple').dataset.tier=concealed?'s':event.cue.match(/_(s|m|l|xl)$/)?.[1]||'s';$('ripple').classList.remove('animate');void $('ripple').offsetWidth;$('ripple').classList.add('animate');}}
  command(event){if(event.cue==='visibility'){this.visible=event.value;if(!event.value){this.visibilityEpoch++;this.celebration++;for(const slots of Object.values(this.slots))for(let i=0;i<slots.length;i++){const slot=slots[i];if(!slot || (family(slot.cue)==='bite' && this.background))continue;slot.cancelled=true;if(slot.source){const t=this.context.currentTime;slot.gain.gain.cancelAndHoldAtTime(t);slot.gain.gain.linearRampToValueAtTime(0,t+.05);slot.source.stop(t+.05);}else slots[i]=null;}}this.syncEnvironment();$('sceneNote').textContent=event.value?'恢复显示：旧咬钩不重播':'模拟：全部窗口隐藏';return;}if(event.cue==='effectsMute'){this.effectsMuted=event.value;this.updateVolumes();return;}if(event.cue==='cancel'){this.policy.cancelled.add(event.action);this.celebration++;for(const slot of this.slots.fishing){if(slot?.source && slot.action===event.action){const t=this.context.currentTime;slot.gain.gain.cancelAndHoldAtTime(t);slot.gain.gain.linearRampToValueAtTime(0,t+.05);slot.source.stop(t+.05);}}$('sceneNote').textContent='动作已结束';return;}const cue=['bite','water_struggle','water_slap'].includes(event.cue)?event.cue+'_'+$('tier').value:event.cue;return this.emit({...event,cue});}
  async run(name){this.reset();const epoch=this.epoch;$('status').textContent='预载实际音频，完成后开始演示…';await this.prepare();if(epoch!==this.epoch)return;await this.startEnvironment();if(epoch!==this.epoch)return;$('status').textContent='演示中 · '+scenarios[name].name;for(const [at,cue,data] of scenarios[name].events)this.later(()=>this.command({cue,id:'demo:'+cue+':'+at,...data}),at*1000);this.later(()=>{this.visible=true;this.effectsMuted=false;this.updateVolumes();$('status').textContent='演示完成；自然环境声继续循环。可单独试听水花与猫叫，或点击停止全部。';},scenarios[name].duration*1000);}
}
const engine=new PreviewEngine();let selection='fishing',manualCounter=0;
function report(error){$('status').textContent='尚未播放：'+error.message;console.error(error);}
function updateVolumes(save=true){
  engine.effectsVolume=Number($('effects').value)/100;engine.background=$('background').checked;
  $('effectsValue').value=$('effects').value+'%';engine.updateVolumes();
  if(save)localStorage.setItem('audio22i-pond-asmr',JSON.stringify({effects:$('effects').value,background:engine.background,trackLevels:Object.fromEntries([...engine.tracks].map(([id,t])=>[id,t.level]))}));
}
try{const saved=JSON.parse(localStorage.getItem('audio22i-pond-asmr')||localStorage.getItem('audio22i-v5')||'null');if(saved){$('effects').value=clamp(Number(saved.effects)/100)*100;$('background').checked=saved.background!==false;for(const [id,track] of engine.tracks)if(saved.trackLevels?.[id]!=null)track.level=clamp(saved.trackLevels[id]);}}catch{}
updateVolumes(false);
$('effects').addEventListener('input',()=>updateVolumes());$('background').addEventListener('change',()=>updateVolumes());
$('environmentPlay').onclick=()=>{engine.startEnvironment().then(()=>{if(engine.environmentRequested)$('status').textContent='只听水边 · 自然环境声循环中。';}).catch(report);};
$('environmentStop').onclick=()=>engine.stopEnvironment();
for(const [id,track] of engine.tracks){
  const asset=ambience.assets.find(a=>a.path===track.path),input=$('track-'+id+'-level'),output=$('track-'+id+'-value');
  input.value=Math.round(track.level*100);output.value=input.value+'%';
  $('track-'+id+'-facts').textContent=asset.duration+' 秒 · '+(asset.processing?.sourceChannelsIdentical?'双单声道实录':'原始立体声')+' · 连续循环';
  input.addEventListener('input',()=>{engine.setTrackLevel(id,Number(input.value)/100);output.value=input.value+'%';updateVolumes();});
  $('track-'+id+'-play').onclick=()=>engine.startTrack(id).catch(report);
  $('track-'+id+'-stop').onclick=()=>engine.stopTrack(id);
}
document.addEventListener('visibilitychange',()=>engine.syncEnvironment());

for(const [name,s] of Object.entries(scenarios)){const b=document.createElement('button');b.textContent=s.name;b.dataset.scenario=name;b.setAttribute('aria-pressed',String(name===selection));b.onclick=()=>{selection=name;for(const other of $('scenarios').children)other.setAttribute('aria-pressed',String(other===b));$('description').textContent=s.description;};$('scenarios').append(b);}
$('description').textContent=scenarios[selection].description;$('play').disabled=false;$('play').onclick=()=>engine.run(selection).catch(report);$('stop').onclick=()=>{engine.stop();$('status').textContent='已停止；未完成的旧动作不再发声。';};$('meow').onclick=()=>engine.emit({cue:'meow',id:'pet:'+manualCounter++}).catch(report);$('cat').onclick=$('meow').onclick;
function audition(cue,variant=0){return engine.emit({cue,variant,id:'audition:'+manualCounter++,action:'audition:'+manualCounter,occurrence:0}).catch(report);}
$('waterCurrent').onclick=()=>audition($('waterType').value+'_'+$('waterTier').value);
let blind=[...cues.keys()].filter(c=>/^(bite|water_struggle|water_slap)_/.test(c));for(let i=blind.length-1;i>0;i--){const values=new Uint32Array(1);crypto.getRandomValues(values);const j=values[0]%(i+1);[blind[i],blind[j]]=[blind[j],blind[i]];}
for(const [i,cue] of blind.entries()){const card=document.createElement('div');card.className='water-card';const h=document.createElement('h3');h.textContent=String.fromCharCode(65+i);const p=document.createElement('p');p.textContent='标签暂时隐藏';p.dataset.answer=LABELS[family(cue)]+' · '+cue.split('_').at(-1).toUpperCase();card.append(h,p);for(let variant=0;variant<2;variant++){const b=document.createElement('button');b.textContent='变体 '+(variant+1);b.onclick=()=>engine.emit({cue,variant,blind:true,id:'blind:'+manualCounter++,action:'blind:'+manualCounter,occurrence:0}).catch(report);card.append(b);}$('water').append(card);}
$('reveal').onclick=()=>{$('reveal').dataset.revealed='true';for(const p of document.querySelectorAll('[data-answer]'))p.textContent=p.dataset.answer;$('reveal').textContent='标签已揭示';};for(const b of document.querySelectorAll('[data-cue]'))b.onclick=()=>engine.emit({cue:b.dataset.cue,variant:Number(b.dataset.variant),id:'object:'+manualCounter++}).catch(report);
async function provenance(){const sources=[[ROOT+'manifest.json','current'],[ROOT+'ambience-manifest.json','ASMR 环境声']];for(const [url,version] of sources){try{const response=await fetch(url);if(!response.ok)continue;const manifest=await response.json();for(const asset of manifest.assets||[]){if(asset.path.startsWith('music/')||asset.path.startsWith('sfx/ui_'))continue;const versionLabel=version==='current'?'v3 音效 · 已采用':version;const sourceKeys=Array.isArray(asset.source)?asset.source:[asset.source];const sourceLabels=sourceKeys.filter(Boolean).map(key=>{const source=manifest.sources?.find(x=>x.key===key);return source?source.author+' / '+source.license:String(key);});const tr=document.createElement('tr');for(const value of [asset.path,versionLabel+' · '+(sourceLabels.join('；')||'详见 CREDITS'),asset.sha256||'待构建']){const td=document.createElement('td');td.textContent=value;tr.append(td);}$('assets').append(tr);}}catch{}}}
provenance();function paint(){engine.tick();requestAnimationFrame(paint);}paint();$('status').textContent='环境声已就绪 · 钓鱼场景 BGM／按钮音关闭。点击播放环境声或选中场景。';
window.AudioLab={engine,PreviewPolicy,scenarios,config,ambience,cues,ROOT,audition,ready:true};
