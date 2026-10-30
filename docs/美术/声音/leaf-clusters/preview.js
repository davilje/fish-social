import {configureBank,levels,auditionGain,waterProfiles,weather,Weather,seededRandom,makePlan,valueAt} from './flow.js?v=20261030-flowing-applied';
const $=id=>document.getElementById(id),ROOT='../../../../fish-social-unity/Assets/StreamingAssets/Audio/';
let bank;
try{const response=await fetch(ROOT+'ambience-manifest.json',{cache:'no-store'});if(!response.ok)throw Error();bank=await response.json();configureBank(bank);}catch{ $('status').textContent='环境声配置暂时无法读取，请稍后刷新。';throw Error('环境声配置暂时无法读取');}
const paths=Object.fromEntries(bank.loops.map(p=>[p.id,p.path]));
const offsets={water:weather.waterOffsetSeconds,wind:weather.windOffsetSeconds,leaves:weather.leavesOffsetSeconds};
const assetUrl=path=>ROOT+path+'?v='+bank.assets.find(a=>a.path===path).sha256.slice(0,12);
const format=seconds=>Math.floor(seconds/60)+':'+String(Math.floor(seconds%60)).padStart(2,'0');
class Audition{
  constructor(){this.token=0;this.voices=[];this.playing=false;this.buffers={};this.seed=weather.seed;this.waterProfile=bank.defaultWaterProfile;this.lastFadeDuration=0;}
  async ready(){
    if(!this.context){
      this.context=new AudioContext();this.master=this.context.createGain();this.master.gain.value=0;this.master.connect(this.context.destination);this.gains={};
      for(const id of Object.keys(paths)){const gain=this.context.createGain();gain.gain.value=levels[id];gain.connect(this.master);this.gains[id]=gain;}
    }
    await this.context.resume();
    if(!this.loading)this.loading=Promise.all([...new Set([...Object.values(paths),...Object.values(waterProfiles).map(p=>p.path)])].map(async path=>{
      const response=await fetch(assetUrl(path),{cache:'no-store'});if(!response.ok)throw Error('音源暂时无法读取');
      this.buffers[path]=await this.context.decodeAudioData(await response.arrayBuffer());
    }));
    await this.loading;
  }
  stopEffects(){try{this.effectsFrame?.contentWindow?.AudioLab?.engine.stop();}catch{}}
  updateLevels(){
    for(const id of ['master','water','wind','leaves'])$(id+'Value').value=$(id).value+'%';
    if(!this.context)return;const now=this.context.currentTime;
    for(const id of Object.keys(paths)){const p=this.gains[id].gain;p.cancelAndHoldAtTime(now);p.setTargetAtTime(levels[id]*Number($(id).value)/100,now,.06);}
    if(this.playing){const p=this.master.gain;p.cancelAndHoldAtTime(now);if(this.duration&&now>=this.startedAt+this.duration-weather.fadeOutSeconds)p.linearRampToValueAtTime(0,this.startedAt+this.duration);else p.setTargetAtTime(auditionGain*Number($('master').value)/100,now,.12);}
    if(this.effectsFrame?.contentWindow?.AudioLab){const e=this.effectsFrame.contentWindow.AudioLab.engine;e.effectsVolume=Number($('master').value)/100;e.updateVolumes();}
  }
  dispose(voices){for(const v of voices){clearTimeout(v.retire);try{v.source.stop();}catch{}v.source.disconnect();v.fade.disconnect();}this.voices=this.voices.filter(v=>!voices.includes(v));}
  stop(message='声音正慢慢淡去…',seconds=weather.fadeOutSeconds,pauseRecording=true){
    this.token++;clearInterval(this.timer);this.playing=false;this.stopEffects();if(pauseRecording)$('recording').pause();$('status').textContent=message;
    if(!this.context||!this.voices.length){return Promise.resolve();}
    if(seconds===0){this.stopImmediate(message,pauseRecording);return Promise.resolve();}
    if(this.stopPromise)return this.stopPromise;
    const now=this.context.currentTime,old=[...this.voices];this.lastFadeDuration=seconds;this.stopping=true;
    this.master.gain.cancelAndHoldAtTime(now);this.master.gain.linearRampToValueAtTime(0,now+seconds);
    for(const v of old)v.source.stop(now+seconds+.02);
    this.stopPromise=new Promise(resolve=>{this.stopResolve=resolve;this.stopTimer=setTimeout(()=>{this.dispose(old);this.stopping=false;this.stopPromise=null;this.stopResolve=null;$('status').textContent='已停止，可以再坐一会儿。';resolve();},seconds*1000+60);});return this.stopPromise;
  }
  stopImmediate(message='已静音。',pauseRecording=true,stopEffects=true){
    this.token++;clearInterval(this.timer);clearTimeout(this.stopTimer);this.playing=false;this.stopping=false;if(stopEffects)this.stopEffects();if(pauseRecording)$('recording').pause();
    if(this.context){this.master.gain.cancelScheduledValues(this.context.currentTime);this.master.gain.setValueAtTime(0,this.context.currentTime);}
    this.dispose([...this.voices]);const done=this.stopResolve;this.stopPromise=null;this.stopResolve=null;done?.();$('status').textContent=message;
  }
  newVoice(id,path,at,fadeIn=0){
    const source=this.context.createBufferSource(),fade=this.context.createGain();source.buffer=this.buffers[path];source.loop=true;source.connect(fade);fade.connect(this.gains[id]);
    const profileScale=id==='water'?waterProfiles[this.waterProfile].gain/levels.water:1;
    fade.gain.setValueAtTime(fadeIn?0:profileScale,at);if(fadeIn)fade.gain.linearRampToValueAtTime(profileScale,at+fadeIn);
    source.start(at,offsets[id]%source.buffer.duration);const voice={id,path,source,fade,startedAt:at};this.voices.push(voice);return voice;
  }
  queueBlock(){
    const block=this.weather.block(weather.blockSeconds);this.queuedEnd=block.start+block.duration;
    for(const id of ['wind','leaves']){const voice=this.voices.find(v=>v.id===id),points=block[id];if(!block.start)voice.fade.gain.setValueAtTime(points[0].gain,this.startedAt);for(const p of points.slice(1))voice.fade.gain.linearRampToValueAtTime(p.gain,this.startedAt+block.start+p.time);}
    this.plan.wind.push(...block.wind.slice(this.plan.wind.length?1:0).map(p=>({time:p.time+block.start,gain:p.gain})));
    this.plan.leaves.push(...block.leaves.slice(this.plan.leaves.length?1:0).map(p=>({time:p.time+block.start,gain:p.gain})));
    // Keep debug/preview inspection bounded even in continuous mode.
    if(this.plan.wind.length>4000){this.plan.wind=this.plan.wind.slice(-2400);this.plan.leaves=this.plan.leaves.slice(-2400);}
  }
  async play(seed=this.seed){
    const pending=this.stop('正在准备湖畔的声音…',.9),token=this.token;await pending;if(token!==this.token)return;
    try{await this.ready();}catch(error){if(token===this.token)$('status').textContent=error.message;this.loading=null;return;}
    if(token!==this.token||document.hidden)return;
    this.seed=seed;this.duration=Number($('duration').value);this.waterProfile=$('waterProfile').value;this.weather=new Weather(seededRandom(seed));
    const at=this.context.currentTime+.08;this.startedAt=at;this.plan={duration:this.duration,seed,wind:[],leaves:[]};this.queuedEnd=0;
    this.playing=true;this.stopping=false;this.ending=false;this.updateLevels();this.master.gain.cancelScheduledValues(this.context.currentTime);this.master.gain.setValueAtTime(0,this.context.currentTime);this.master.gain.linearRampToValueAtTime(auditionGain*Number($('master').value)/100,at+weather.fadeInSeconds);
    for(const id of Object.keys(paths))this.newVoice(id,id==='water'?waterProfiles[this.waterProfile].path:paths[id],at);
    this.queueBlock();this.queueBlock();
    if(this.duration){this.master.gain.setValueAtTime(auditionGain*Number($('master').value)/100,at+this.duration-weather.fadeOutSeconds);this.master.gain.linearRampToValueAtTime(0,at+this.duration);}
    $('play').textContent='重新试听';$('status').textContent='风和树叶相伴起伏，偶尔轻下来，再慢慢吹起。';
    this.timer=setInterval(()=>this.tick(),150);this.tick();
  }
  tick(){
    if(!this.playing)return;const elapsed=Math.max(0,this.context.currentTime-this.startedAt);
    if(!this.duration||elapsed<this.duration-weather.fadeOutSeconds){if(this.queuedEnd-elapsed<weather.blockSeconds+5)this.queueBlock();}
    $('progress').max=this.duration||60;$('progress').value=this.duration?Math.min(elapsed,this.duration):elapsed%60;
    $('progressLabel').textContent=format(elapsed)+(this.duration?' / '+format(this.duration):' · 持续');
    if(this.duration&&elapsed>=this.duration){this.stopImmediate('这一段听完了，湖边慢慢安静下来。');return;}
    // After a slider change, keep the finite session's final fade intact.
    if(this.duration&&elapsed>=this.duration-weather.fadeOutSeconds&&!this.ending){this.ending=true;this.master.gain.cancelAndHoldAtTime(this.context.currentTime);this.master.gain.linearRampToValueAtTime(0,this.startedAt+this.duration);}
  }
  async selectWaterProfile(id){
    this.waterProfile=id;if(!this.playing)return;
    const now=this.context.currentTime,path=waterProfiles[id].path;if(this.voices.some(v=>v.id==='water'&&!v.retiring&&v.path===path))return;
    for(const v of this.voices.filter(v=>v.id==='water'&&v.retiring))this.dispose([v]);
    const old=this.voices.find(v=>v.id==='water');if(old){old.retiring=true;old.fade.gain.cancelAndHoldAtTime(now);old.fade.gain.linearRampToValueAtTime(0,now+1.5);old.source.stop(now+1.55);old.retire=setTimeout(()=>this.dispose([old]),1600);}
    this.newVoice('water',path,now+.01,1.5);
  }
}
const engine=new Audition();
for(const id of ['master','water','wind','leaves'])$(id).addEventListener('input',()=>engine.updateLevels());
$('play').onclick=()=>{engine.ending=false;engine.play();};$('new').onclick=()=>{engine.ending=false;engine.play(crypto.getRandomValues(new Uint32Array(1))[0]);};
$('stop').onclick=()=>engine.stop();$('immediate').onclick=()=>engine.stopImmediate();
$('waterProfile').onchange=()=>engine.selectWaterProfile($('waterProfile').value);
$('recording').addEventListener('play',()=>engine.stopImmediate('正在听完整混音。',false));
$('loadEffects').onclick=()=>{
  if(engine.effectsFrame)return;
  const frame=document.createElement('iframe');engine.effectsFrame=frame;frame.title='已认可的动作与猫叫';frame.src='../v5/index.html?effectsOnly=1&v=20261030-flowing-applied';
  frame.onload=()=>{
    const doc=frame.contentDocument,e=frame.contentWindow.AudioLab?.engine;if(!e)return;
    // Reuse all accepted cue controls while preventing the previous ambience from playing.
    e.startEnvironment=async()=>{};e.syncEnvironment=()=>{};
    const style=doc.createElement('style');style.textContent='header,.ambience-panel{display:none!important}main{padding:12px!important}.columns{display:block!important}';doc.head.append(style);
    doc.addEventListener('pointerdown',()=>engine.stopImmediate('动作与猫叫试听中。',true,false),true);
    doc.addEventListener('click',()=>engine.stopImmediate('动作与猫叫试听中。',true,false),true);
    $('loadEffects').hidden=true;engine.updateLevels();
  };
  $('effectsHost').append(frame);
};
function silence(){engine.stopImmediate('已暂停，回来后可继续试听。');}
document.addEventListener('visibilitychange',()=>{if(document.hidden)silence();});
window.addEventListener('pagehide',()=>{silence();engine.context?.close();});
$('play').disabled=$('new').disabled=false;engine.updateLevels();$('status').textContent='准备好了，点击开始，听一段完整的湖畔环境。';
window.AudioClusterLab={engine,makePlan,valueAt,Weather,configureBank,bank,weather,levels,waterProfiles,auditionGain,paths,ROOT,ready:true};
