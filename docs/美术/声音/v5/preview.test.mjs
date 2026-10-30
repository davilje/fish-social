// Browser simulation and real media checks, separate from Unity/Windows evidence.
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {mkdirSync,writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {join,resolve} from 'node:path';
const require=createRequire(import.meta.url);
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const evidenceDir=process.env.AUDIO_EVIDENCE_DIR?resolve(process.env.AUDIO_EVIDENCE_DIR):fileURLToPath(new URL('../../../工程/声音/soft-breeze-2026-10-30/preview/',import.meta.url));
mkdirSync(evidenceDir,{recursive:true});
const url=process.env.AUDIO_PREVIEW_URL||'http://127.0.0.1:8891/docs/美术/声音/v5/';
const browser=await chromium.launch({headless:true,...(process.env.CHROME_PATH?{executablePath:process.env.CHROME_PATH}:{}),args:['--autoplay-policy=no-user-gesture-required']});
const page=await browser.newPage({viewport:{width:1400,height:1100}});
const errors=[],results=[],requests=[],failedResponses=[];
function observe(target){target.on('pageerror',e=>errors.push(e.message));target.on('request',r=>requests.push(r.url()));target.on('response',r=>{if(r.status()>=400)failedResponses.push({url:r.url(),status:r.status()});});}
observe(page);
const loaded=()=>page.waitForFunction(()=>window.AudioLab?.ready);
const layerPaused=()=>page.evaluate(()=>[...AudioLab.engine.tracks.values()].every(t=>t.element.paused)&&AudioLab.engine.tracks.get('water').media.every(s=>s.element.paused));
try{
  await page.goto(url);await loaded();
  assert.equal(await page.locator('[data-scenario]').count(),9);assert.equal(await page.locator('.water-card button').count(),24);
  assert.equal(await page.locator('[data-music],[data-paper-cue],#music').count(),0);
  assert.equal(await page.inputValue('#effects'),'70');assert.equal(await page.locator('#pondScene option').count(),21);
  assert.equal(await page.locator('[data-water-profile]').count(),3);
  assert.equal(await page.evaluate(()=>AudioLab.engine.context===undefined),true);
  assert.match(await page.locator('header').textContent(),/BGM 关闭，按钮音关闭/);
  results.push('No autoplay/BGM/UI audio; nine scenarios,24 water-card buttons,21 pond choices and3 water-profile auditions');

  const identity=await page.evaluate(async()=>{
    const json=path=>fetch(path,{cache:'no-store'}).then(r=>{if(!r.ok)throw Error(path+' '+r.status);return r.json();});
    const sha=async path=>Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',await fetch(path,{cache:'no-store'}).then(r=>{if(!r.ok)throw Error(path+' '+r.status);return r.arrayBuffer();}))),x=>x.toString(16).padStart(2,'0')).join('');
    const [current,adoption,selection]=await Promise.all([json(AudioLab.ROOT+'manifest.json'),json('../../../工程/声音/audio-22i-adoption.json'),json('../../../工程/声音/2026-10-30-close-cue-selection.json')]);
    const effects=await Promise.all(current.assets.filter(a=>!a.path.startsWith('music/')).map(async a=>({path:a.path,manifest:a.sha256,adopted:adoption.assets.find(x=>x.path===a.path)?.sha256,actual:await sha(AudioLab.ROOT+a.path)})));
    return {effects,ambience:await Promise.all(AudioLab.ambience.assets.map(async a=>({...a,actualSha256:await sha(AudioLab.ROOT+a.path)}))),loops:AudioLab.ambience.loops,profiles:AudioLab.ambience.waterProfiles,scenes:AudioLab.ambience.scenes,gusts:AudioLab.ambience.gusts,cueConfigHash:await sha(AudioLab.ROOT+'cue-config.json'),adoptedCueConfigHash:adoption.cueConfigSha256,selection,closePaths:AudioLab.config.cues.find(c=>c.cue==='ui_close').paths};
  });
  assert.equal(identity.effects.length,46);assert.ok(identity.effects.every(a=>a.actual===a.manifest&&a.actual===a.adopted));
  assert.equal(identity.cueConfigHash,identity.selection.cueConfigSha256);assert.equal(identity.adoptedCueConfigHash,identity.selection.baselineCueConfigSha256);assert.deepEqual(identity.closePaths,['sfx/ui_close_01.wav']);
  assert.equal(identity.ambience.length,5);assert.ok(identity.ambience.every(a=>a.actualSha256===a.sha256));
  assert.deepEqual(identity.profiles.map(p=>p.id),['calm','ripples','shore']);
  assert.equal(identity.loops.find(l=>l.id==='wind').path,'ambience/pond_wind_open.wav');
  assert.equal(identity.loops.find(l=>l.id==='wind').gain,.18);assert.equal(identity.loops.find(l=>l.id==='leaves').gain,.65);
  assert.deepEqual(identity.gusts,{initialDelayMinSeconds:6,initialDelayMaxSeconds:12,gapMinSeconds:20,gapMaxSeconds:38,windMinSeconds:3,windMaxSeconds:4.5,leavesDelayMinSeconds:.8,leavesDelayMaxSeconds:1.4,leavesMinSeconds:3,leavesMaxSeconds:4.8,attackSeconds:.7,releaseSeconds:1.5});
  assert.equal(identity.scenes.length,21);assert.ok(identity.scenes.every(s=>identity.profiles.some(p=>p.id===s.waterProfile)));
  await page.waitForFunction(()=>['pond_lake_calm.wav','pond_lake_ripples.wav','pond_wind_open.wav','pond_shore.wav','pond_leaves.wav'].every(p=>document.querySelector('#assets').textContent.includes(p)));
  results.push('Five exact project ambience hashes/credits/configs and21 pond mappings; accepted46 effects unchanged');

  // Run the actual exported scheduler on an injected clock. No production times are shortened.
  const scheduler=await page.evaluate(async()=>{
    class Clock{
      constructor(){this.time=0;this.next=0;this.jobs=new Map();}
      now=()=>this.time;set=(fn,seconds)=>{const id=++this.next;this.jobs.set(id,{fn,at:this.time+seconds});return id;};clear=id=>this.jobs.delete(id);
      async advance(seconds){const target=this.time+seconds;let guard=0;while(true){const pair=[...this.jobs].sort((a,b)=>a[1].at-b[1].at).find(([,j])=>j.at<=target);if(!pair)break;if(++guard>1000)throw Error('unbounded scheduler');this.jobs.delete(pair[0]);this.time=pair[1].at;pair[1].fn();for(let i=0;i<8;i++)await Promise.resolve();}this.time=target;}
    }
    const clock=new Clock(),events=[];let serial=0;
    const scheduler=new AudioLab.GustScheduler({config:AudioLab.ambience.gusts,clock,random:()=>((serial++*37)%101)/100,onWind:p=>{events.push({type:'wind',time:clock.now(),...p});return true;},onLeaves:()=>events.push({type:'leaves',time:clock.now()}),onStop:id=>events.push({type:'stop-'+id,time:clock.now()})});
    scheduler.startAuto();const first=scheduler.nextAt;await clock.advance(first-.001);const before=events.filter(e=>e.type==='wind').length;await clock.advance(.001);for(let i=0;i<100;i++)await clock.advance(1);
    const history=scheduler.history.slice(),wind=events.filter(e=>e.type==='wind');
    const waiting=[...clock.jobs.values()].map(j=>j.fn);scheduler.cancel();const count=events.filter(e=>e.type==='wind').length;for(const callback of waiting)callback();await clock.advance(100);
    const cancelled=events.filter(e=>e.type==='wind').length===count&&clock.jobs.size===0;
    // A cancelled asynchronous wind start cannot install its delayed leaves or future gap.
    let release;const late=new AudioLab.GustScheduler({config:AudioLab.ambience.gusts,clock,onWind:()=>new Promise(r=>release=r)});
    const pending=late.begin(true);late.cancel();release(true);await pending;
    return {first,before,history,wind,cancelled,pendingCancelled:clock.jobs.size===0,events};
  });
  assert.ok(scheduler.first>=6&&scheduler.first<=12);assert.equal(scheduler.before,0);assert.ok(scheduler.wind.length>=2);
  for(const w of scheduler.wind){assert.ok(w.windSeconds>=3&&w.windSeconds<=4.5);assert.ok(w.leavesDelay>=.8&&w.leavesDelay<=1.4);assert.ok(w.leavesSeconds>=3&&w.leavesSeconds<=4.8);assert.ok(w.endSeconds>=w.windSeconds+1-1e-6);}
  for(const gap of scheduler.history.filter(e=>e.type==='quiet-gap'))assert.ok(gap.seconds>=20&&gap.seconds<=38);
  for(let i=1;i<scheduler.wind.length;i++){const old=scheduler.wind[i-1],next=scheduler.wind[i];assert.ok(next.time-(old.time+old.endSeconds)>=20-1e-6);assert.ok(next.time-(old.time+old.endSeconds)<=38+1e-6);}
  for(const w of scheduler.wind){const leaf=scheduler.events.find(e=>e.type==='leaves'&&e.time>=w.time&&e.time<=w.time+1.4);if(leaf)assert.ok(Math.abs(leaf.time-w.time-w.leavesDelay)<1e-6);}
  assert.equal(scheduler.cancelled,true);assert.equal(scheduler.pendingCancelled,true);
  results.push('Controlled-clock scheduler verifies6–12s first wait,3–4.5s wind,0.8–1.4s leaf lead-in,3–4.8s leaves,wind ends first,20–38s quiet gap and cancelled async starts');

  await page.locator('[data-water-profile="calm"]').click();
  await page.waitForFunction(()=>AudioLab.engine.environmentElement&&!AudioLab.engine.environmentElement.paused&&AudioLab.engine.environmentElement.currentSrc.endsWith('pond_lake_calm.wav'));
  await page.waitForTimeout(2200);
  const waterNodes=await page.evaluate(()=>{const e=AudioLab.engine;window.originalWaterNodes=e.tracks.get('water').media.map(x=>({source:x.source,element:x.element}));return e.tracks.get('water').media.length;});assert.equal(waterNodes,2);
  for(const id of ['ripples','shore','calm']){
    const oldTime=await page.evaluate(()=>AudioLab.engine.environmentElement.currentTime);
    await page.locator('[data-water-profile="'+id+'"]').click();
    await page.waitForFunction(id=>{const e=AudioLab.engine;return e.waterProfile===id&&!e.environmentElement.paused&&e.environmentElement.currentSrc.endsWith(e.profile().path);},id);
    const crossfade=await page.evaluate(()=>{const e=AudioLab.engine,w=e.tracks.get('water');return {playing:w.media.filter(s=>!s.element.paused).length,loops:w.media.map(s=>s.element.loop),profileGain:w.media[e.waterActive].profileGain.gain.value};});
    assert.equal(crossfade.playing,2);assert.ok(crossfade.loops.every(Boolean));assert.ok(Math.abs(crossfade.profileGain-identity.profiles.find(p=>p.id===id).gain)<.0001);
    await page.waitForTimeout(2150);assert.equal(await page.evaluate(()=>AudioLab.engine.tracks.get('water').media.filter(s=>!s.element.paused).length),1);
    await page.evaluate(()=>{const a=AudioLab.engine.environmentElement;a.currentTime=a.duration-.12;});await page.waitForFunction(()=>AudioLab.engine.environmentElement.currentTime<2&&!AudioLab.engine.environmentElement.paused);
  }
  // All real scene controls must select the configured water, not only change a label.
  for(const scene of identity.scenes){await page.locator('#pondScene').selectOption(scene.pondId);assert.equal(await page.inputValue('#waterProfile'),scene.waterProfile);}
  await page.waitForFunction(()=>{const e=AudioLab.engine;return !e.environmentElement.paused&&e.environmentElement.currentSrc.endsWith(e.profile().path);});
  await page.evaluate(async()=>{const e=AudioLab.engine;await Promise.all(['calm','ripples','shore','ripples','calm'].map(p=>e.selectWaterProfile(p)));});
  await page.waitForTimeout(2200);
  assert.equal(await page.evaluate(()=>AudioLab.engine.environmentElement.currentSrc.endsWith('pond_lake_calm.wav')),true);
  assert.equal(await page.evaluate(()=>AudioLab.engine.tracks.get('water').media.every((s,i)=>s.element===window.originalWaterNodes[i].element&&s.source===window.originalWaterNodes[i].source)),true);
  assert.equal(await page.evaluate(()=>AudioLab.engine.tracks.get('water').media.filter(s=>!s.element.paused).length),1);
  results.push('Three real water recordings loop/wrap; switches crossfade with at most2 fixed media sources; rapid AB and21 pond scene mappings converge');

  // Exercise the shortest real excerpt: the0.7s attack and1.5s release must leave a clean audible plateau.
  await page.evaluate(()=>{window.originalGustRandom=AudioLab.engine.gusts.random;AudioLab.engine.gusts.random=()=>0;});
  await page.locator('#track-wind-play').click();await page.waitForFunction(()=>!AudioLab.engine.tracks.get('wind').element.paused);
  const solo=await page.evaluate(()=>{const e=AudioLab.engine,w=e.tracks.get('wind');window.originalWind=w.element;window.originalLeaves=e.tracks.get('leaves').element;return {loop:w.element.loop,shot:w.lastShot,duration:w.element.duration,water:!e.environmentElement.paused,leaves:e.tracks.get('leaves').element.paused};});
  assert.equal(solo.loop,false);assert.equal(solo.water,true);assert.equal(solo.leaves,true);assert.ok(solo.shot.end<=solo.duration);
  assert.equal(solo.shot.duration,3);await page.waitForTimeout(850);
  const envelope={plateau:await page.evaluate(()=>{const t=AudioLab.engine.tracks.get('wind');return {gain:t.fade.gain.value,paused:t.element.paused};})};
  assert.equal(envelope.plateau.paused,false);assert.ok(envelope.plateau.gain>.95);
  await page.waitForTimeout(1000);envelope.release=await page.evaluate(()=>{const t=AudioLab.engine.tracks.get('wind');return {gain:t.fade.gain.value,paused:t.element.paused};});
  assert.equal(envelope.release.paused,false);assert.ok(envelope.release.gain>0&&envelope.release.gain<.95);
  await page.waitForFunction(()=>AudioLab.engine.tracks.get('wind').element.paused);assert.equal(await page.evaluate(()=>AudioLab.engine.environmentElement.paused),false);
  await page.evaluate(()=>AudioLab.engine.gusts.random=window.originalGustRandom);
  await page.locator('#gustNow').click();await page.waitForFunction(()=>AudioLab.engine.gusts.phase==='wind');
  assert.equal(await page.evaluate(()=>AudioLab.engine.tracks.get('leaves').element.paused),true);
  await page.waitForFunction(()=>AudioLab.engine.tracks.get('leaves').wanted&&!AudioLab.engine.tracks.get('leaves').element.paused,{timeout:6000});
  const gust=await page.evaluate(()=>{const e=AudioLab.engine;return {plan:e.gusts.plan,wind:{loop:e.tracks.get('wind').element.loop,...e.tracks.get('wind').lastShot,duration:e.tracks.get('wind').element.duration},leaves:{loop:e.tracks.get('leaves').element.loop,...e.tracks.get('leaves').lastShot,duration:e.tracks.get('leaves').element.duration},water:!e.environmentElement.paused};});
  const leafVersion=await page.evaluate(()=>new URL(AudioLab.engine.tracks.get('leaves').element.currentSrc).searchParams.get('v'));assert.equal(leafVersion,identity.ambience.find(a=>a.path==='ambience/pond_leaves.wav').sha256.slice(0,12));
  assert.equal(gust.water,true);for(const track of [gust.wind,gust.leaves]){assert.equal(track.loop,false);assert.ok(track.offset>=0&&track.end<=track.duration);}
  await page.waitForFunction(()=>AudioLab.engine.gusts.phase==='leaf-tail',{timeout:12000});
  assert.equal(await page.evaluate(()=>AudioLab.engine.tracks.get('wind').element.paused),true);assert.equal(await page.evaluate(()=>AudioLab.engine.tracks.get('leaves').element.paused),false);
  await page.waitForFunction(()=>AudioLab.engine.gusts.phase==='idle',{timeout:8000});
  assert.equal(await page.evaluate(()=>AudioLab.engine.tracks.get('leaves').element.paused),true);assert.equal(await page.evaluate(()=>AudioLab.engine.environmentElement.paused),false);
  results.push('Real3s wind envelope reaches plateau and releases before natural stop; immediate gust has0.8–1.4s leaf lead-in,leaf tail and unchanged water; leaf URL matches current asset hash');

  await page.locator('#environmentPlay').click();await page.waitForFunction(()=>AudioLab.engine.gusts.phase==='quiet');
  assert.equal(await page.evaluate(()=>AudioLab.engine.autoRequested),true);assert.equal(await page.evaluate(()=>AudioLab.engine.tracks.get('wind').element.paused&&AudioLab.engine.tracks.get('leaves').element.paused),true);
  await page.locator('#track-water-level').fill('83');await page.locator('#track-water-level').dispatchEvent('input');
  await page.locator('#track-wind-level').fill('42');await page.locator('#track-wind-level').dispatchEvent('input');
  await page.locator('#track-leaves-level').fill('26');await page.locator('#track-leaves-level').dispatchEvent('input');
  await page.waitForFunction(()=>Math.abs(AudioLab.engine.tracks.get('wind').levelGain.gain.value-.42*.18)<.0001&&Math.abs(AudioLab.engine.tracks.get('leaves').levelGain.gain.value-.26*.65)<.0001);
  const layerGains=await page.evaluate(()=>({wind:AudioLab.engine.tracks.get('wind').levelGain.gain.value,leaves:AudioLab.engine.tracks.get('leaves').levelGain.gain.value}));assert.ok(Math.abs(layerGains.wind-.42*.18)<.0001);assert.ok(Math.abs(layerGains.leaves-.26*.65)<.0001);
  await page.locator('#effects').fill('0');await page.locator('#effects').dispatchEvent('input');assert.equal(await layerPaused(),true);assert.equal(await page.evaluate(()=>AudioLab.engine.gusts.timers.size),0);
  await page.locator('#effects').fill('61');await page.locator('#effects').dispatchEvent('input');await page.waitForFunction(()=>!AudioLab.engine.environmentElement.paused);
  assert.equal(await page.evaluate(()=>AudioLab.engine.gusts.phase==='quiet'&&AudioLab.engine.gusts.nextAt-AudioLab.engine.gusts.clock.now()>5),true);
  await page.locator('#gustNow').click();await page.waitForFunction(()=>AudioLab.engine.gusts.phase==='wind');
  await page.evaluate(()=>AudioLab.engine.command({cue:'visibility',value:false}));assert.equal(await page.evaluate(()=>AudioLab.engine.gusts.timers.size),0);
  await page.waitForTimeout(2150);assert.equal(await layerPaused(),true);
  await page.evaluate(()=>AudioLab.engine.command({cue:'visibility',value:true}));await page.waitForFunction(()=>!AudioLab.engine.environmentElement.paused);
  assert.equal(await page.evaluate(()=>AudioLab.engine.gusts.phase==='quiet'&&AudioLab.engine.tracks.get('wind').element.paused&&AudioLab.engine.tracks.get('leaves').element.paused),true);
  await page.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,value:true});document.dispatchEvent(new Event('visibilitychange'));});
  assert.equal(await page.evaluate(()=>AudioLab.engine.gusts.timers.size),0);
  await page.evaluate(()=>{delete document.hidden;document.dispatchEvent(new Event('visibilitychange'));});
  assert.equal(await page.evaluate(()=>AudioLab.engine.gusts.phase), 'quiet');
  results.push('Global0/mute/hide cancel gusts and pending leaves; restore restarts a fresh first wait,never an old gust; all per-layer volumes independent');

  for(const action of ['stop','mute','hide']){
    const state=await page.evaluate(async action=>{
      const e=AudioLab.engine;e.stop();const wind=e.tracks.get('wind'),original=wind.element.play.bind(wind.element);let release;
      wind.element.play=()=>original().then(()=>new Promise(resolve=>release=resolve));
      const pending=e.playGust();for(let i=0;i<100&&!release;i++)await new Promise(r=>setTimeout(r,10));
      if(!release)throw Error('No actual pending wind play');
      if(action==='stop')e.stop();else if(action==='mute')e.command({cue:'effectsMute',value:true});else e.command({cue:'visibility',value:false});
      release();wind.element.play=original;await pending;
      return {paused:wind.element.paused,leaves:e.tracks.get('leaves').element.paused,timers:e.gusts.timers.size};
    },action);assert.deepEqual(state,{paused:true,leaves:true,timers:0});
  }
  await page.evaluate(()=>AudioLab.engine.stop());
  const pendingReady=await page.evaluate(async()=>{const e=AudioLab.engine,ready=e.ready.bind(e);let release;e.ready=()=>new Promise(resolve=>release=resolve);const p=e.playGust();e.stop();e.ready=ready;release();await p;return e.gusts.timers.size===0&&e.tracks.get('wind').element.paused;});assert.equal(pendingReady,true);
  await page.evaluate(()=>AudioLab.engine.run('rapid'));await page.waitForTimeout(3300);assert.equal(await page.evaluate(()=>AudioLab.engine.played.length),0);
  await page.evaluate(()=>AudioLab.engine.run('fishing'));await page.waitForTimeout(7600);
  const mix=await page.evaluate(()=>({played:AudioLab.engine.played,maxVoices:AudioLab.engine.maxVoices,water:!AudioLab.engine.environmentElement.paused,slots:Object.values(AudioLab.engine.slots).reduce((n,x)=>n+x.length,0)}));
  assert.equal(mix.played.length,7);assert.equal(mix.played.filter(x=>x.cue==='meow').length,1);assert.equal(mix.played.filter(x=>x.cue==='catch').length,1);assert.ok(mix.played.every(x=>!x.cue.startsWith('ui_')));assert.ok(mix.maxVoices<=4);assert.equal(mix.slots,4);assert.equal(mix.water,true);
  await page.locator('#stop').click();await page.waitForTimeout(100);assert.equal(await layerPaused(),true);assert.equal(await page.evaluate(()=>AudioLab.engine.activeSources),0);
  results.push('Delayed media/context starts lose to stop/mute/hide; short cues preserve4 slots and7 normal-fishing events with silent UI');

  await page.reload();await loaded();assert.equal(await page.inputValue('#effects'),'61');for(const [id,v] of [['water','83'],['wind','42'],['leaves','26']])assert.equal(await page.inputValue('#track-'+id+'-level'),v);
  assert.equal(await page.evaluate(()=>AudioLab.engine.context===undefined),true);
  await page.evaluate(()=>{localStorage.removeItem('audio22i-pond-asmr');localStorage.removeItem('audio22i-v5');});await page.reload();await loaded();await page.evaluate(()=>document.fonts.ready);
  await page.screenshot({path:join(evidenceDir,'preview-desktop.png'),fullPage:true,animations:'disabled'});
  const mobile=await browser.newPage({viewport:{width:390,height:844}});observe(mobile);await mobile.goto(url);await mobile.waitForFunction(()=>window.AudioLab?.ready);await mobile.evaluate(()=>document.fonts.ready);assert.equal(await mobile.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);await mobile.screenshot({path:join(evidenceDir,'preview-mobile.png'),fullPage:true,animations:'disabled'});await mobile.close();
  const musicRequests=requests.filter(url=>/\/Audio\/music\//.test(new URL(url).pathname));assert.deepEqual(musicRequests,[]);assert.deepEqual(failedResponses,[]);assert.deepEqual(errors,[]);
  results.push('Saved levels survive reload without autoplay; desktop/mobile fit; zero BGM requests,HTTP or JS errors');
  const report={status:'PASS',date:'2026-10-30',scope:'Browser lake profiles and intermittent real wind/leaf media; controlled scheduler timing is separate from Windows hardware evidence',browser:await browser.version(),results,identity,scheduler,solo,envelope,gust,layerGains,mix,musicRequests,failedResponses,errors};writeFileSync(join(evidenceDir,'browser-validation.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify({status:report.status,results},null,2));
}finally{await browser.close();}
