// Serve the repository root. Browser simulation, separate from Unity/Windows evidence.
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {mkdirSync,writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {join,resolve} from 'node:path';
const require=createRequire(import.meta.url);
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const evidenceDir=process.env.AUDIO_EVIDENCE_DIR?resolve(process.env.AUDIO_EVIDENCE_DIR):fileURLToPath(new URL('../../../工程/声音/pond-wind-leaves-2026-10-30/preview/',import.meta.url));
mkdirSync(evidenceDir,{recursive:true});
const url=process.env.AUDIO_PREVIEW_URL||'http://127.0.0.1:8891/docs/美术/声音/v5/';
const browser=await chromium.launch({headless:true,...(process.env.CHROME_PATH?{executablePath:process.env.CHROME_PATH}:{}),args:['--autoplay-policy=no-user-gesture-required']});
const page=await browser.newPage({viewport:{width:1400,height:1100}});
const errors=[],results=[],requests=[],failedResponses=[];
function observe(target){target.on('pageerror',e=>errors.push(e.message));target.on('request',r=>requests.push(r.url()));target.on('response',r=>{if(r.status()>=400)failedResponses.push({url:r.url(),status:r.status()});});}
observe(page);
async function loaded(){await page.waitForFunction(()=>window.AudioLab?.ready);}
try {
  await page.goto(url);await loaded();
  assert.equal(await page.locator('[data-scenario]').count(),9);
  assert.equal(await page.locator('.water-card button').count(),24);
  assert.equal(await page.locator('[data-music],[data-paper-cue],#music').count(),0);
  assert.equal(await page.inputValue('#effects'),'70');
  assert.match(await page.locator('header').textContent(),/BGM 关闭，按钮音关闭/);
  assert.equal(await page.evaluate(()=>AudioLab.engine.context===undefined),true,'Never autoplay on page load');
  results.push('Current pond-ASMR page, nine scenarios, no BGM/button audition controls, no autoplay');

  const identity=await page.evaluate(async()=>{
    const json=path=>fetch(path,{cache:'no-store'}).then(r=>{if(!r.ok)throw Error(path+' '+r.status);return r.json();});
    const sha=async path=>Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',await fetch(path,{cache:'no-store'}).then(r=>{if(!r.ok)throw Error(path+' '+r.status);return r.arrayBuffer();}))),x=>x.toString(16).padStart(2,'0')).join('');
    const [current,adoption]=await Promise.all([json(AudioLab.ROOT+'manifest.json'),json('../../../工程/声音/audio-22i-adoption.json')]);
    const effects=await Promise.all(current.assets.filter(a=>!a.path.startsWith('music/')).map(async a=>({path:a.path,manifest:a.sha256,adopted:adoption.assets.find(x=>x.path===a.path)?.sha256,actual:await sha(AudioLab.ROOT+a.path)})));
    const tracks=await Promise.all(AudioLab.ambience.assets.map(async a=>({...a,actualSha256:await sha(AudioLab.ROOT+a.path)})));
    return {effects,cueConfigHash:await sha(AudioLab.ROOT+'cue-config.json'),adoptedCueConfigHash:adoption.cueConfigSha256,
      ambience:tracks,loops:AudioLab.ambience.loops};
  });
  assert.equal(identity.effects.length,46);assert.ok(identity.effects.every(x=>x.actual===x.manifest&&x.actual===x.adopted));
  assert.equal(identity.cueConfigHash,identity.adoptedCueConfigHash);
  assert.equal(identity.ambience.length,3);assert.ok(identity.ambience.every(a=>a.actualSha256===a.sha256&&a.channels===2&&a.duration>0));
  assert.deepEqual(identity.loops.map(({id,path,gain,fadeSeconds})=>({id,path,gain,fadeSeconds})),[{id:'water',path:'ambience/pond_shore.wav',gain:.65,fadeSeconds:2},{id:'wind',path:'ambience/pond_wind.wav',gain:.45,fadeSeconds:2},{id:'leaves',path:'ambience/pond_leaves.wav',gain:.4,fadeSeconds:2}]);
  await page.waitForFunction(()=>['pond_shore.wav','pond_wind.wav','pond_leaves.wav'].every(path=>document.querySelector('#assets').textContent.includes(path)));
  assert.equal(await page.locator('[data-track]').count(),3);
  assert.match(await page.locator('#track-wind-facts').textContent(),/56 秒.*双单声道实录/);
  assert.match(await page.locator('#track-leaves-facts').textContent(),/48 秒.*原始立体声/);
  results.push('Three project ambience layers have exact hashes, manifest gains/durations and source credits; accepted46 effects unchanged');

  await page.locator('#track-water-play').click();
  await page.waitForFunction(()=>!AudioLab.engine.tracks.get('water').element.paused);
  assert.equal(await page.evaluate(()=>AudioLab.engine.tracks.get('wind').element.paused),true);
  const waterTime=await page.evaluate(()=>AudioLab.engine.environmentElement.currentTime);
  await page.locator('#track-wind-play').click();await page.locator('#track-leaves-play').click();
  await page.waitForFunction(()=>[...AudioLab.engine.tracks.values()].every(t=>!t.element.paused&&Number.isFinite(t.element.duration)));
  await page.waitForTimeout(2200);
  const environment=await page.evaluate(()=>{
    const e=AudioLab.engine;window.originalTrackNodes=new Map([...e.tracks].map(([id,t])=>[id,{element:t.element,source:t.source}]));
    return [...e.tracks].map(([id,t])=>({id,src:t.element.currentSrc,duration:t.element.duration,loop:t.element.loop,fade:t.fade.gain.value,level:t.levelGain.gain.value,master:e.environmentMaster.gain.value}));
  });
  assert.ok(await page.evaluate(()=>AudioLab.engine.environmentElement.currentTime)>waterTime,'Starting another layer must not reset water');
  for(const track of environment){const config=identity.loops.find(x=>x.id===track.id),asset=identity.ambience.find(x=>x.path===config.path);assert.ok(track.src.endsWith('/Audio/'+config.path));assert.ok(Math.abs(track.duration-asset.duration)<.001);assert.equal(track.loop,true);assert.ok(Math.abs(track.fade-1)<.002);assert.ok(Math.abs(track.level-config.gain)<.002);assert.ok(Math.abs(track.master-.7)<.002);}
  for(const id of ['water','wind','leaves']){
    await page.evaluate(id=>{const a=AudioLab.engine.tracks.get(id).element;a.currentTime=a.duration-.15;},id);
    await page.waitForFunction(id=>{const a=AudioLab.engine.tracks.get(id).element;return a.currentTime<2&&!a.paused;},id);
  }
  results.push('Three independent real media elements overlap without reset; all three streamed loops seek and wrap');

  await page.locator('#track-wind-stop').click();await page.waitForTimeout(2200);
  assert.deepEqual(await page.evaluate(()=>[...AudioLab.engine.tracks.values()].map(t=>t.element.paused)),[false,true,false]);
  await page.locator('#track-water-level').fill('0');await page.locator('#track-water-level').dispatchEvent('input');
  assert.deepEqual(await page.evaluate(()=>[...AudioLab.engine.tracks.values()].map(t=>t.element.paused)),[true,true,false]);
  await page.locator('#track-water-level').fill('83');await page.locator('#track-water-level').dispatchEvent('input');
  await page.waitForFunction(()=>!AudioLab.engine.tracks.get('water').element.paused);
  await page.locator('#track-wind-level').fill('42');await page.locator('#track-wind-level').dispatchEvent('input');
  await page.locator('#track-leaves-level').fill('26');await page.locator('#track-leaves-level').dispatchEvent('input');
  assert.equal(await page.evaluate(()=>AudioLab.engine.tracks.get('wind').element.paused),true,'Level alone must not start stopped wind');
  await page.locator('#environmentPlay').click();await page.waitForFunction(()=>[...AudioLab.engine.tracks.values()].every(t=>!t.element.paused));
  await page.waitForTimeout(2200);
  assert.equal(await page.evaluate(()=>[...AudioLab.engine.tracks].every(([id,t])=>t.element===window.originalTrackNodes.get(id).element&&t.source===window.originalTrackNodes.get(id).source)),true);
  assert.equal(await page.evaluate(()=>new Set([...AudioLab.engine.tracks.values()].map(t=>t.source)).size),3);
  results.push('Individual stop and zero-level affect only their layer; independent0–100 levels and replay reuse exactly3 media sources');

  const beforeSlider=await page.evaluate(()=>AudioLab.engine.played.length);
  await page.locator('#effects').fill('0');await page.locator('#effects').dispatchEvent('input');
  assert.equal(await page.evaluate(()=>[...AudioLab.engine.tracks.values()].every(t=>t.element.paused)),true);
  assert.ok(Math.abs(await page.evaluate(()=>AudioLab.engine.environmentMaster.gain.value))<.0001);
  await page.locator('#effects').fill('61');await page.locator('#effects').dispatchEvent('input');
  await page.waitForFunction(()=>[...AudioLab.engine.tracks.values()].every(t=>!t.element.paused));await page.waitForTimeout(2200);
  assert.equal(await page.evaluate(()=>AudioLab.engine.played.length),beforeSlider,'Sliders must not emit button sounds');
  const levels=await page.evaluate(()=>[...AudioLab.engine.tracks.values()].map(t=>t.levelGain.gain.value));
  for(const [i,target] of [.65*.83,.45*.42,.4*.26].entries())assert.ok(Math.abs(levels[i]-target)<.002);
  assert.ok(Math.abs(await page.evaluate(()=>AudioLab.engine.environmentMaster.gain.value)-.61)<.002);
  await page.evaluate(()=>AudioLab.engine.command({cue:'visibility',value:false}));await page.waitForTimeout(2200);
  assert.equal(await page.evaluate(()=>[...AudioLab.engine.tracks.values()].every(t=>t.element.paused)),true);
  await page.evaluate(()=>AudioLab.engine.command({cue:'visibility',value:true}));await page.waitForFunction(()=>[...AudioLab.engine.tracks.values()].every(t=>!t.element.paused));
  // A quick restore must invalidate every prior layer's pending fade-out pause.
  await page.evaluate(()=>{const e=AudioLab.engine;e.command({cue:'visibility',value:false});e.command({cue:'visibility',value:true});});await page.waitForTimeout(2200);
  assert.equal(await page.evaluate(()=>[...AudioLab.engine.tracks.values()].every(t=>!t.element.paused)),true);
  await page.evaluate(()=>AudioLab.engine.command({cue:'effectsMute',value:true}));
  assert.equal(await page.evaluate(()=>[...AudioLab.engine.tracks.values()].every(t=>t.element.paused)),true);
  await page.evaluate(()=>AudioLab.engine.command({cue:'effectsMute',value:false}));await page.waitForFunction(()=>[...AudioLab.engine.tracks.values()].every(t=>!t.element.paused));
  await page.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,value:true});document.dispatchEvent(new Event('visibilitychange'));});
  await page.waitForTimeout(2200);
  assert.equal(await page.evaluate(()=>[...AudioLab.engine.tracks.values()].every(t=>t.element.paused)),true);
  await page.evaluate(()=>{delete document.hidden;document.dispatchEvent(new Event('visibilitychange'));});
  await page.waitForFunction(()=>[...AudioLab.engine.tracks.values()].every(t=>!t.element.paused));
  await page.locator('#environmentStop').click();await page.waitForTimeout(2200);
  assert.equal(await page.evaluate(()=>[...AudioLab.engine.tracks.values()].every(t=>t.element.paused&&!t.requested)),true);
  results.push('Global zero/mute pauses every layer immediately; hidden/show and rapid restoration invalidate stale fade timers; global fade pauses all');

  await page.evaluate(()=>AudioLab.engine.run('rapid'));await page.waitForTimeout(3300);
  assert.equal(await page.evaluate(()=>AudioLab.engine.played.length),0);
  assert.ok(await page.evaluate(()=>AudioLab.engine.skipped.some(x=>x.reason==='钓鱼场景按钮音关闭')));
  assert.equal(await page.evaluate(()=>AudioLab.engine.environmentElement.paused),false);
  await page.evaluate(()=>AudioLab.engine.run('fishing'));await page.waitForTimeout(7600);
  const mix=await page.evaluate(()=>({played:AudioLab.engine.played,skipped:AudioLab.engine.skipped,maxVoices:AudioLab.engine.maxVoices,ambiencePlaying:[...AudioLab.engine.tracks.values()].every(t=>!t.element.paused)}));
  assert.equal(mix.played.length,7);assert.ok(mix.played.every(x=>!x.cue.startsWith('ui_')));assert.ok(mix.maxVoices<=4);assert.equal(mix.ambiencePlaying,true);
  assert.equal(mix.played.filter(x=>x.cue==='catch').length,1);assert.equal(mix.played.filter(x=>x.cue==='meow').length,1);
  results.push(`Pond buttons stay silent; fishing plays7 action/cat cues over ambience with max${mix.maxVoices}/4 short voices and no BGM`);

  await page.locator('#stop').click();await page.waitForTimeout(100);
  assert.equal(await page.evaluate(()=>[...AudioLab.engine.tracks.values()].every(t=>t.element.paused&&!t.requested)),true);
  assert.equal(await page.evaluate(()=>AudioLab.engine.activeSources),0);
  // Delay real media play() fulfillment; stop/mute/hide must still win the race.
  for(const action of ['stop','mute','hide']){
    const result=await page.evaluate(async action=>{
      const e=AudioLab.engine;e.stop();
      for(const t of e.tracks.values()){const play=t.element.play.bind(t.element);t.realPlay=play;t.element.play=()=>play().then(()=>new Promise(resolve=>{t.release=resolve;}));}
      const pending=e.startEnvironment();await new Promise(r=>setTimeout(r,80));
      if(action==='stop')e.stop();else if(action==='mute')e.command({cue:'effectsMute',value:true});else e.command({cue:'visibility',value:false});
      for(const t of e.tracks.values()){t.release?.();t.element.play=t.realPlay;}
      await pending;await new Promise(r=>setTimeout(r,80));
      return [...e.tracks.values()].every(t=>t.element.paused);
    },action);
    assert.equal(result,true,'Delayed play must not revive after '+action);
  }
  const pendingReady=await page.evaluate(async()=>{
    const e=AudioLab.engine;e.stop();const ready=e.ready.bind(e);let release;
    e.ready=()=>new Promise(resolve=>{release=resolve;});
    const pending=e.startTrack('wind');e.stopTrack('wind');e.ready=ready;release();await pending;
    return !e.tracks.get('wind').requested&&e.tracks.get('wind').element.paused;
  });
  assert.equal(pendingReady,true,'Single-layer stop must invalidate an in-flight context readiness continuation');
  await page.evaluate(()=>AudioLab.engine.stop());
  await page.reload();await loaded();assert.equal(await page.inputValue('#effects'),'61');
  for(const [id,value] of [['water','83'],['wind','42'],['leaves','26']])assert.equal(await page.inputValue('#track-'+id+'-level'),value);
  assert.equal(await page.evaluate(()=>AudioLab.engine.context===undefined),true);
  results.push('Stop-all cancels short and all pending media; delayed play races lose to stop/mute/hide; three saved levels survive reload without autoplay');

  await page.evaluate(()=>{localStorage.removeItem('audio22i-pond-asmr');localStorage.removeItem('audio22i-v5');});
  await page.reload();await loaded();await page.evaluate(()=>document.fonts.ready);
  await page.screenshot({path:join(evidenceDir,'preview-desktop.png'),fullPage:true,animations:'disabled'});
  const mobile=await browser.newPage({viewport:{width:390,height:844}});observe(mobile);await mobile.goto(url);await mobile.waitForFunction(()=>window.AudioLab?.ready);await mobile.evaluate(()=>document.fonts.ready);
  assert.equal(await mobile.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
  await mobile.screenshot({path:join(evidenceDir,'preview-mobile.png'),fullPage:true,animations:'disabled'});await mobile.close();
  const musicRequests=requests.filter(url=>/\/Audio\/music\//.test(new URL(url).pathname));
  assert.deepEqual(musicRequests,[]);assert.deepEqual(failedResponses,[]);assert.deepEqual(errors,[]);
  results.push('Desktop/mobile layouts fit; zero BGM requests, HTTP errors or JS errors');
  const report={status:'PASS',date:'2026-10-30',scope:'Browser preview of three independent pond ambience layers; no Windows hardware claim',browser:await browser.version(),results,identity,environment,mix,musicRequests,failedResponses,errors};
  writeFileSync(join(evidenceDir,'browser-validation.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify({status:report.status,results},null,2));
} finally {await browser.close();}
