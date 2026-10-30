const $ = id => document.getElementById(id);
const ROOT = '../../../../fish-social-unity/Assets/StreamingAssets/Audio/';
const effectsOnly=new URLSearchParams(location.search).get('effectsOnly')==='1';
const LABELS = {ui_click:'轻触纸面',ui_open:'翻开书页',ui_close:'合上纸本',ui_toggle:'拨动纸签',ui_tick:'铅笔短擦',ui_success:'轻轻盖章',ui_error:'纸片退回',cast:'挥竿',splash:'浮漂入水',bite:'咬钩',water_struggle:'扰水',water_slap:'拍水',reel:'收线',escape:'逃脱触水',groundbait:'窝料入水',catch:'纸面记录',meow:'猫咪回应'};
const family = cue => cue.replace(/_(s|m|l|xl)$/, '');
const marker = cue => ({bite:'bite.onset',catch:'result.presented',escape:'escape.contact',water_struggle:'fish.surface',water_slap:'fish.slap',splash:'float.contact',cast:'cast.release',reel:'retrieve.start',groundbait:'groundbait.contact'})[family(cue)] || family(cue);
const clamp = value => Math.min(1, Math.max(0, Number(value) || 0));
let config, ambience;
try {
  [config,ambience] = await Promise.all(['cue-config.json','ambience-manifest.json'].map(path=>fetch(ROOT+path,{cache:'no-store'}).then(r=>{if(!r.ok)throw Error(path+' '+r.status);return r.json();})));
  if(ambience.version===4&&!effectsOnly){window.top.location.replace(new URL('../../../../现行预览.html?audio=20261030-flowing#audio',location.href));await new Promise(()=>{});}
  if((ambience.version!==3&&!(effectsOnly&&ambience.version===4)) || ambience.loops?.length!==3 || new Set(ambience.loops.map(x=>x.id)).size!==3 || !['water','wind','leaves'].every(id=>ambience.loops.some(x=>x.id===id)) || !ambience.loops.every(loop=>ambience.assets.some(a=>a.path===loop.path)) || ambience.waterProfiles?.length!==3 || !ambience.waterProfiles.every(p=>ambience.assets.some(a=>a.path===p.path)) || (!ambience.gusts&&!effectsOnly) || !ambience.scenes?.length)throw Error('三层环境声清单无效');
} catch (error) { $('status').textContent='配置未能加载，请从仓库 HTTP 服务打开本页：'+error.message; throw error; }
// The effects-only embed keeps its old scheduler inert; the public bank remains the actual v4 bank.
const gustConfig=ambience.gusts||{initialDelayMinSeconds:6,initialDelayMaxSeconds:12,gapMinSeconds:20,gapMaxSeconds:38,windMinSeconds:3,windMaxSeconds:4.5,leavesDelayMinSeconds:.8,leavesDelayMaxSeconds:1.4,leavesMinSeconds:3,leavesMaxSeconds:4.8,attackSeconds:.7,releaseSeconds:1.5};
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

// Clock injection keeps the long quiet intervals testable without changing their real durations.
export class GustScheduler {
  constructor({config,clock,random=Math.random,onWind=()=>true,onLeaves=()=>{},onStop=()=>{}}){
    Object.assign(this,{config,clock:clock||{now:()=>performance.now()/1000,set:(fn,s)=>setTimeout(fn,s*1000),clear:id=>clearTimeout(id)},random,onWind,onLeaves,onStop});
    this.timers=new Set();this.generation=0;this.phase='idle';this.history=[];this.automatic=false;
  }
  range(low,high){return low+(high-low)*clamp(this.random());}
  schedule(fn,seconds){const id=this.clock.set(()=>{this.timers.delete(id);fn();},seconds);this.timers.add(id);return id;}
  record(type,extra={}){this.history.push({type,time:this.clock.now(),...extra});if(this.history.length>100)this.history.shift();}
  cancel(immediate=true){this.generation++;for(const id of this.timers)this.clock.clear(id);this.timers.clear();this.automatic=false;this.phase='idle';this.nextAt=null;this.onStop('wind',immediate);this.onStop('leaves',immediate);}
  wait(initial){
    const c=this.config,delay=initial?this.range(c.initialDelayMinSeconds,c.initialDelayMaxSeconds):this.range(c.gapMinSeconds,c.gapMaxSeconds);
    this.phase='quiet';this.nextAt=this.clock.now()+delay;const token=this.generation;
    this.record(initial?'initial-wait':'quiet-gap',{seconds:delay});
    this.schedule(()=>{if(token===this.generation&&this.automatic)this.begin(true).catch(report);},delay);
  }
  startAuto(){this.cancel();this.automatic=true;this.wait(true);}
  async begin(automatic=this.automatic){
    this.cancel();this.automatic=automatic;const token=this.generation,c=this.config;
    const windSeconds=this.range(c.windMinSeconds,c.windMaxSeconds),leavesDelay=this.range(c.leavesDelayMinSeconds,c.leavesDelayMaxSeconds);
    const leavesSeconds=Math.min(c.leavesMaxSeconds,Math.max(this.range(c.leavesMinSeconds,c.leavesMaxSeconds),windSeconds-leavesDelay+1));
    const plan={windSeconds,leavesDelay,leavesSeconds,endSeconds:leavesDelay+leavesSeconds};
    this.plan=plan;this.phase='starting';this.nextAt=null;
    const started=await this.onWind(plan);
    if(token!==this.generation)return;
    if(started===false){this.phase='idle';if(this.automatic)this.wait(true);return;}
    this.startedAt=this.clock.now();this.phase='wind';this.record('wind',{...plan});
    this.schedule(()=>{if(token!==this.generation)return;this.phase='leaves';this.record('leaves');Promise.resolve(this.onLeaves(plan)).catch(report);},leavesDelay);
    this.schedule(()=>{if(token!==this.generation)return;this.phase='leaf-tail';this.onStop('wind');this.record('wind-end');},windSeconds);
    this.schedule(()=>{if(token!==this.generation)return;this.onStop('leaves');this.record('leaves-end');this.phase='idle';if(this.automatic)this.wait(false);},plan.endSeconds);
  }
}

class PreviewEngine {
  constructor(){
    this.policy=new PreviewPolicy();this.buffers=new Map();this.slots={ui:[null],fishing:[null,null],voice:[null]};
    this.epoch=0;this.timers=new Set();this.played=[];this.skipped=[];this.maxVoices=0;this.activeSources=0;
    this.visible=true;this.effectsMuted=false;this.lastVariants=new Map();this.celebration=0;
    this.effectsVolume=.7;this.background=true;this.stopUntil=0;this.visibilityEpoch=0;
    this.tracks=new Map(ambience.loops.map(loop=>[loop.id,{...loop,level:1,requested:false,wanted:false,request:0,intent:0}]));
    this.waterProfile=ambience.defaultWaterProfile;this.waterActive=0;this.environmentIntent=0;this.gustIntent=0;this.autoRequested=false;this.audibleState=true;
    this.gusts=new GustScheduler({config:gustConfig,
      onWind:async plan=>{plan.intents=Object.fromEntries(['wind','leaves'].map(id=>[id,this.tracks.get(id).intent]));return this.playShot('wind',plan.windSeconds,plan.intents.wind);},
      onLeaves:plan=>this.playShot('leaves',plan.leavesSeconds,plan.intents.leaves),onStop:(id,immediate=true)=>this.stopShot(id,immediate)});
  }
  createMedia(parent,loop){
    const fade=this.context.createGain();fade.gain.value=0;fade.connect(parent);
    const element=new Audio();element.crossOrigin='anonymous';element.preload='none';element.loop=loop;
    const source=this.context.createMediaElementSource(element);source.connect(fade);
    return {element,source,fade,retire:0,path:null};
  }
  async ready(){
    if(!this.context){
      this.context=new AudioContext();this.sfx=this.context.createGain();this.sfx.connect(this.context.destination);
      this.environmentMaster=this.context.createGain();this.environmentMaster.connect(this.context.destination);
      for(const [id,track] of this.tracks){
        track.levelGain=this.context.createGain();track.levelGain.connect(this.environmentMaster);
        if(id==='water'){
          track.media=[0,1].map(()=>{const profileGain=this.context.createGain();profileGain.connect(track.levelGain);return {...this.createMedia(profileGain,true),profileGain};});
          this.bindWater(track.media[0]);
        }else{const path=track.path;Object.assign(track,this.createMedia(track.levelGain,false),{path});}
      }
      this.updateVolumes();
    }
    await this.context.resume();
  }
  bindWater(slot){const water=this.tracks.get('water');water.element=slot.element;water.fade=slot.fade;water.source=slot.source;}
  get environmentElement(){return this.tracks.get('water').element;}
  get environmentFade(){return this.tracks.get('water').fade;}
  get environmentRequested(){return this.tracks.get('water').requested||this.autoRequested;}
  get environmentWanted(){return this.tracks.get('water').wanted||['wind','leaves'].some(id=>this.tracks.get(id).wanted);}
  audible(){return this.visible&&!document.hidden&&!this.effectsMuted&&this.effectsVolume>0;}
  profile(){return ambience.waterProfiles.find(p=>p.id===this.waterProfile)||ambience.waterProfiles[0];}
  mediaReady(media,path){
    if(media.path===path){if(media.element.readyState>=1)return Promise.resolve(true);if(media.loading)return media.loading;}
    media.abortLoad?.();media.path=path;
    media.loading=new Promise(resolve=>{
      const clean=()=>{media.element.removeEventListener('loadedmetadata',ok);media.element.removeEventListener('error',bad);media.abortLoad=null;media.loading=null;};
      const ok=()=>{clean();resolve(true);},bad=()=>{clean();report(Error('无法读取环境声 '+path));resolve(false);};
      media.abortLoad=()=>{clean();resolve(false);};media.element.addEventListener('loadedmetadata',ok);media.element.addEventListener('error',bad);
      const leafVersion=path==='ambience/pond_leaves.wav'?ambience.assets.find(asset=>asset.path===path)?.sha256?.slice(0,12):null;
      media.element.src=ROOT+path+(leafVersion?'?v='+leafVersion:'');media.element.load();
    });
    return media.loading;
  }
  fadeMedia(media,target,seconds=2,immediate=false){
    const now=this.context.currentTime,gain=media.fade.gain;gain.cancelAndHoldAtTime(now);
    if(immediate){gain.setValueAtTime(target,now);return;}
    const from=gain.value,curve=Float32Array.from({length:128},(_,i)=>{const t=i/127;return from+(target-from)*t*t*(3-2*t);});
    gain.setValueCurveAtTime(curve,now,seconds);
  }
  retireWater(slot,immediate=false){
    clearTimeout(slot.stopTimer);const token=++slot.retire;this.fadeMedia(slot,0,this.profile().fadeSeconds,immediate);
    if(immediate)slot.element.pause();else slot.stopTimer=setTimeout(()=>{if(token===slot.retire)slot.element.pause();},this.profile().fadeSeconds*1000+30);
  }
  async syncWater(){
    if(effectsOnly)return;
    if(!this.context)return;
    const water=this.tracks.get('water'),intent=++water.intent;
    if(!water.requested||!this.audible()||water.level<=0){water.wanted=false;for(const slot of water.media)this.retireWater(slot,this.effectsMuted||this.effectsVolume<=0||water.level<=0);return;}
    const profile=this.profile();let target=water.media[this.waterActive];
    if(target.path!==profile.path)target=water.media[1-this.waterActive];
    // Keep the currently audible water until the next recording can actually play.
    if(target.path!==profile.path){this.retireWater(target,true);if(!await this.mediaReady(target,profile.path))return;}
    if(intent!==water.intent||!water.requested||!this.audible()||water.level<=0)return;
    target.profileGain.gain.setValueAtTime(profile.gain,this.context.currentTime);
    const retire=++target.retire;clearTimeout(target.stopTimer);
    try{await target.element.play();}catch(error){if(intent===water.intent)report(error);return;}
    if(intent!==water.intent||!water.requested||!this.audible()||water.level<=0){if(!water.requested||!this.audible()||water.level<=0)target.element.pause();return;}
    if(retire!==target.retire)return;
    this.waterActive=water.media.indexOf(target);this.bindWater(target);water.wanted=true;
    this.fadeMedia(target,1,profile.fadeSeconds);
    for(const slot of water.media)if(slot!==target)this.retireWater(slot);
  }
  updateVolumes(){
    if(!this.context)return;
    const now=this.context.currentTime,gain=this.effectsMuted?0:this.effectsVolume;
    this.sfx.gain.cancelScheduledValues(now);this.sfx.gain.setValueAtTime(gain,now);this.environmentMaster.gain.setValueAtTime(gain,now);
    for(const [id,track] of this.tracks)track.levelGain.gain.setValueAtTime(track.level*(id==='water'?1:track.gain),now);
    this.syncEnvironment();
  }
  syncEnvironment(){
    if(effectsOnly)return;
    if(!this.context)return;
    const audible=this.audible();
    if(!audible){this.gustIntent++;this.gusts.cancel();}
    else if(!this.audibleState&&this.autoRequested)this.gusts.startAuto();
    this.audibleState=audible;this.syncWater();
  }
  setTrackLevel(id,value){
    const track=this.tracks.get(id);if(!track)return;track.level=clamp(value);
    if(track.level===0&&id!=='water')this.stopShot(id,true);
    if(this.context){track.levelGain.gain.setValueAtTime(track.level*(id==='water'?1:track.gain),this.context.currentTime);if(id==='water')this.syncWater();}
  }
  async selectWaterProfile(id){
    if(!ambience.waterProfiles.some(p=>p.id===id))return;
    this.waterProfile=id;paintWaterSelection();await this.syncWater();
  }
  async startTrack(id){
    if(effectsOnly)return;
    if(id!=='water')return this.auditionShot(id);
    const request=++this.environmentIntent,epoch=this.epoch;await this.ready();if(request!==this.environmentIntent||epoch!==this.epoch)return;
    this.tracks.get('water').requested=true;await this.syncWater();
  }
  stopTrack(id,immediate=false){
    if(id!=='water'){this.stopShot(id,immediate);return;}
    this.environmentIntent++;const water=this.tracks.get('water');water.requested=false;water.wanted=false;water.intent++;
    if(this.context)for(const slot of water.media)this.retireWater(slot,immediate);
  }
  async prepareGust(){return (await Promise.all(['wind','leaves'].map(id=>this.mediaReady(this.tracks.get(id),this.tracks.get(id).path)))).every(Boolean);}
  async playShot(id,duration,intent){
    if(effectsOnly)return;
    const track=this.tracks.get(id);
    if(intent!==track.intent||!this.audible())return false;
    if(track.level<=0)return true;
    if(!await this.mediaReady(track,track.path)||intent!==track.intent||!this.audible())return false;
    duration=Math.min(duration,track.element.duration-.1);
    const offset=Math.random()*Math.max(0,track.element.duration-duration-.05);
    track.element.currentTime=offset;track.lastShot={offset,duration,end:offset+duration};
    track.requested=true;track.wanted=true;
    track.fade.gain.cancelScheduledValues(this.context.currentTime);track.fade.gain.setValueAtTime(0,this.context.currentTime);
    try{await track.element.play();}catch(error){if(intent===track.intent)report(error);return false;}
    if(intent!==track.intent||!this.audible()){if(!track.wanted||!this.audible())track.element.pause();return false;}
    const now=this.context.currentTime,attack=gustConfig.attackSeconds,release=gustConfig.releaseSeconds,gain=track.fade.gain;
    gain.setValueAtTime(0,now);gain.linearRampToValueAtTime(1,now+attack);gain.setValueAtTime(1,now+Math.max(attack,duration-release));gain.linearRampToValueAtTime(0,now+duration);
    track.element.onended=()=>{if(intent===track.intent)this.stopShot(id,true);};
    return true;
  }
  stopShot(id,immediate=true){
    const track=this.tracks.get(id);track.intent++;track.wanted=track.requested=false;clearTimeout(track.stopTimer);track.element&&(track.element.onended=null);
    if(!this.context)return;
    const token=track.intent;this.fadeMedia(track,0,gustConfig.releaseSeconds,immediate);
    if(immediate)track.element.pause();else track.stopTimer=setTimeout(()=>{if(token===track.intent)track.element.pause();},gustConfig.releaseSeconds*1000+30);
  }
  async auditionShot(id){
    if(effectsOnly)return;
    this.autoRequested=false;this.gusts.cancel();const request=++this.gustIntent,epoch=this.epoch;await this.ready();
    if(request!==this.gustIntent||epoch!==this.epoch||!this.audible())return;
    const track=this.tracks.get(id),intent=track.intent,c=gustConfig;
    if(!await this.mediaReady(track,track.path)||request!==this.gustIntent)return;
    const duration=id==='wind'?this.gusts.range(c.windMinSeconds,c.windMaxSeconds):this.gusts.range(c.leavesMinSeconds,c.leavesMaxSeconds);
    if(await this.playShot(id,duration,intent))track.stopTimer=setTimeout(()=>{if(intent===track.intent)this.stopShot(id,true);},duration*1000);
  }
  async playGust(){
    if(effectsOnly)return;
    this.gusts.cancel();const request=++this.gustIntent,epoch=this.epoch;await this.ready();
    if(request!==this.gustIntent||epoch!==this.epoch||!this.audible()||!await this.prepareGust()||request!==this.gustIntent)return;
    await this.gusts.begin(this.autoRequested);
  }
  async startEnvironment(){
    if(effectsOnly)return;
    const request=++this.environmentIntent,epoch=this.epoch;await this.ready();if(request!==this.environmentIntent||epoch!==this.epoch)return;
    this.tracks.get('water').requested=true;this.autoRequested=true;await this.syncWater();
    if(request!==this.environmentIntent||epoch!==this.epoch||!this.audible())return;
    if(await this.prepareGust()&&request===this.environmentIntent&&this.audible()&&this.autoRequested)this.gusts.startAuto();
  }
  stopEnvironment(immediate=false){this.environmentIntent++;this.gustIntent++;this.autoRequested=false;this.gusts.cancel(immediate);this.stopTrack('water',immediate);}
  async load(path){if(this.buffers.has(path))return this.buffers.get(path);const promise=fetch(path).then(r=>{if(!r.ok)throw Error(path+' ('+r.status+')');return r.arrayBuffer();}).then(b=>this.context.decodeAudioData(b));this.buffers.set(path,promise);try{return await promise;}catch(e){this.buffers.delete(path);throw e;}}
  later(fn,ms){const id=setTimeout(()=>{this.timers.delete(id);fn();},ms);this.timers.add(id);return id;}
  async prepare(){await this.ready();await Promise.all(config.cues.filter(c=>c.group!=='ui').flatMap(c=>c.paths.map(p=>this.load(ROOT+p))));}
  stop(){
    this.epoch++;this.celebration++;if(this.context)this.stopUntil=this.context.currentTime+.06;
    for(const timer of this.timers)clearTimeout(timer);this.timers.clear();
    for(const slots of Object.values(this.slots))for(let i=0;i<slots.length;i++){
      const slot=slots[i];if(slot?.source){slot.gain.gain.cancelScheduledValues(this.context.currentTime);slot.gain.gain.setTargetAtTime(0,this.context.currentTime,.012);slot.source.stop(this.context.currentTime+.05);}slots[i]=null;
    }
    this.stopEnvironment(true);
    this.effectsMuted=false;this.visible=true;this.updateVolumes();
  }
  reset(){this.stop();this.policy.reset();this.played=[];this.skipped=[];this.maxVoices=this.activeSources;this.lastVariants.clear();$('log').replaceChildren();}
  log(event,allowed,reason){const line={cue:event.cue,id:event.id||event.action||'',reason,time:this.context?.currentTime||0};(allowed?this.played:this.skipped).push(line);const li=document.createElement('li');li.textContent=(allowed?'播放 ':'省略 ')+(LABELS[family(event.cue)]||event.cue)+(line.id?' · '+line.id:'')+' · '+reason;if(!allowed)li.className='skipped';$('log').prepend(li);while($('log').children.length>90)$('log').lastChild.remove();}
  selectVariant(cue,event){const paths=cues.get(cue).paths;if(event.variant!=null)return event.variant%paths.length;let hash=0;for(const char of (event.id||event.action||String(this.context.currentTime)))hash=(hash*31+char.charCodeAt(0))>>>0;let index=hash%paths.length;if(index===this.lastVariants.get(cue))index=(index+1)%paths.length;this.lastVariants.set(cue,index);return index;}
  tick(){
    $('voices').textContent=this.activeSources+' / 4';$('played').textContent=this.played.length;$('skipped').textContent=this.skipped.length;
    const water=this.tracks.get('water'),profile=this.profile();
    for(const [id,track] of this.tracks){
      const playing=track.element&&!track.element.paused;
      const state=id==='water'?(track.wanted?'循环中':playing?'渐渐安静':track.requested?'已静音':'未播放'):(track.wanted?'一阵'+TRACK_LABELS[id]:playing?'渐渐安静':'安静中');
      $('track-'+id+'-state').textContent=state;
      $('track-'+id+'-play').setAttribute('aria-pressed',String(track.wanted));
    }
    const phase=this.gusts.phase;
    const mood=phase==='quiet'?'风歇了 · 约 '+Math.max(0,Math.ceil(this.gusts.nextAt-this.gusts.clock.now()))+' 秒后再起':phase==='wind'?'风轻轻吹来':phase==='leaves'?'风起，树叶轻响':phase==='leaf-tail'?'风已歇，叶声慢慢落下':phase==='starting'?'听风吹来…':'风叶安静中';
    $('environmentState').textContent=water.wanted?profile.label:'水声未播放';
    $('environmentLabel').textContent=mood+(this.autoRequested?' · 自动环境':'');

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
  async run(name){this.reset();const epoch=this.epoch;$('status').textContent='预载实际音频，完成后开始演示…';await this.prepare();if(epoch!==this.epoch)return;await this.startEnvironment();if(epoch!==this.epoch)return;$('status').textContent='演示中 · '+scenarios[name].name;for(const [at,cue,data] of scenarios[name].events)this.later(()=>this.command({cue,id:'demo:'+cue+':'+at,...data}),at*1000);this.later(()=>{this.visible=true;this.effectsMuted=false;this.updateVolumes();$('status').textContent='演示完成；水声继续，风叶间歇响起。可单独试听水花与猫叫，或点击停止全部。';},scenarios[name].duration*1000);}
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
$('environmentPlay').onclick=()=>engine.startEnvironment().then(()=>{$('status').textContent='自动环境 · 水声常在，风起叶响后留一会儿安静。';}).catch(report);
$('environmentStop').onclick=()=>engine.stopEnvironment();
$('gustNow').onclick=()=>engine.playGust().catch(report);
for(const [id,track] of engine.tracks){
  const asset=ambience.assets.find(a=>a.path===(id==='water'?engine.profile().path:track.path)),input=$('track-'+id+'-level'),output=$('track-'+id+'-value');
  input.value=Math.round(track.level*100);output.value=input.value+'%';
  $('track-'+id+'-facts').textContent=asset.duration+' 秒 · '+(asset.processing?.sourceChannelsIdentical?'双单声道实录':'原始立体声')+(id==='water'?' · 连续循环':' · 每次只听一阵');
  input.addEventListener('input',()=>{engine.setTrackLevel(id,Number(input.value)/100);output.value=input.value+'%';updateVolumes();});
  $('track-'+id+'-play').onclick=()=>engine.startTrack(id).catch(report);
  $('track-'+id+'-stop').onclick=()=>engine.stopTrack(id);
}
function paintWaterSelection(){
  const profile=engine.profile();$('waterProfile').value=profile.id;
  for(const button of document.querySelectorAll('[data-water-profile]'))button.setAttribute('aria-pressed',String(button.dataset.waterProfile===profile.id));
  const asset=ambience.assets.find(a=>a.path===profile.path);$('track-water-facts').textContent=profile.label+' · '+asset.duration+' 秒 · 连续循环';
}
for(const profile of ambience.waterProfiles){
  const option=document.createElement('option');option.value=profile.id;option.textContent=profile.label;$('waterProfile').append(option);
  const button=document.createElement('button');button.dataset.waterProfile=profile.id;button.textContent=profile.label;
  button.onclick=()=>engine.selectWaterProfile(profile.id).then(()=>engine.startTrack('water')).catch(report);$('waterProfiles').append(button);
}
for(const scene of ambience.scenes){const option=document.createElement('option');option.value=scene.pondId;option.textContent=scene.label||scene.name||scene.pondId;$('pondScene').append(option);}
$('pondScene').onchange=()=>{const scene=ambience.scenes.find(s=>s.pondId===$('pondScene').value);engine.selectWaterProfile(scene.waterProfile).catch(report);};
$('waterProfile').onchange=()=>engine.selectWaterProfile($('waterProfile').value).catch(report);
const initialScene=ambience.scenes.find(s=>s.waterProfile===engine.waterProfile);if(initialScene)$('pondScene').value=initialScene.pondId;
paintWaterSelection();
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
if(effectsOnly)document.querySelector('.ambience-panel').style.display='none';
window.AudioLab={engine,PreviewPolicy,GustScheduler,scenarios,config,ambience,cues,ROOT,audition,effectsOnly,ready:true};
