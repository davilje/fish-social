// Independent preview playback checks. This does not validate game integration or subjective sound quality.
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {mkdirSync,writeFileSync,readFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {resolve,join} from 'node:path';
const require=createRequire(import.meta.url);
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const evidenceDir=process.env.AUDIO_EVIDENCE_DIR?resolve(process.env.AUDIO_EVIDENCE_DIR):fileURLToPath(new URL('../../../工程/声音/flowing-adoption-2026-10-30/preview/',import.meta.url));
mkdirSync(evidenceDir,{recursive:true});
const url=process.env.AUDIO_PREVIEW_URL||'http://127.0.0.1:8891/现行预览.html?audio=20261030-flowing#audio';
const browser=await chromium.launch({headless:true,...(process.env.CHROME_PATH?{executablePath:process.env.CHROME_PATH}:{}),args:['--autoplay-policy=no-user-gesture-required']});
const page=await browser.newPage({viewport:{width:1440,height:1100}}),errors=[],failedResponses=[],results=[],requests=[];
page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>requests.push(r.url()));page.on('response',r=>{if(r.status()>=400)failedResponses.push({url:r.url(),status:r.status()});});
const frameReady=async()=>{await page.waitForFunction(()=>document.querySelector('#stage')?.contentWindow?.AudioClusterLab?.ready);return await page.locator('#stage').contentFrame();};
try{
 await page.goto(url);const frame=await frameReady();
 assert.equal(await frame.locator('#duration').inputValue(),'60');assert.equal(await frame.locator('#master').inputValue(),'70');for(const id of ['water','wind','leaves'])assert.equal(await frame.locator('#'+id).inputValue(),'100');
 assert.equal(await frame.locator('iframe').count(),0);assert.equal(await frame.locator('#water').getAttribute('type'),'range');assert.match(await frame.locator('h1').textContent(),/湖畔的风与叶/);
 assert.match(await frame.locator('.badge').last().textContent(),/已应用项目/);
 const identity=await frame.locator('body').evaluate(async()=>{
   const a=AudioClusterLab,bank=a.bank,asset=bank.assets.find(p=>p.path===a.paths.wind),response=await fetch(a.ROOT+asset.path,{cache:'no-store'}),bytes=await response.arrayBuffer();
   const actual=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',bytes)),v=>v.toString(16).padStart(2,'0')).join('');
   const modified=structuredClone(bank);Object.assign(modified.weather,{sampleIntervalSeconds:.1,initialWindGain:.25,windMinGain:.25,windMaxGain:.25,windAdjustmentThreshold:0,firstLullMinSeconds:999,firstLullMaxSeconds:999});modified.loops.find(p=>p.id==='wind').gain=.22;
   a.configureBank(modified);const configured=a.makePlan(2);a.configureBank(bank);
   return {version:bank.version,hasGusts:'gusts' in bank,weather:bank.weather,paths:a.paths,asset,actual,configured,levels:a.levels};
 });
 assert.equal(identity.version,4);assert.equal(identity.hasGusts,false);assert.equal(identity.paths.wind,'ambience/pond_wind_open_loop.wav');assert.equal(identity.actual,identity.asset.sha256);assert.equal(identity.asset.duration,12);
 assert.equal(identity.weather.monitorGain,5);assert.equal(identity.configured.wind.length,21);assert.ok(identity.configured.wind.every(p=>p.gain===.25));assert.equal(identity.configured.levels.wind,.22);
 results.push('Formal v4 manifest supplies paths,hashes,mix and weather;configured generator responds to changed parameters;official12s wind loop is used directly');
 assert.equal(await frame.locator('body').evaluate(()=>window.AudioClusterLab.engine.context===undefined),true);results.push('Unified preview opens complete environment controls;60s default,three layer controls and no autoplay or eager effects iframe');
 await frame.locator('#play').click();await frame.locator('body').evaluate(async()=>{while(!AudioClusterLab.engine.playing)await new Promise(r=>setTimeout(r,20));});
 let initial,continuity;
 if(process.env.AUDIO_REUSE_PLAYBACK==='1'){({initial,continuity}=JSON.parse(readFileSync(join(evidenceDir,'playback-observations.json'),'utf8')));await page.waitForTimeout(600);}
 else {
 initial=await frame.locator('body').evaluate(()=>{const e=AudioClusterLab.engine;window.flowSamples=[];window.flowProbes=e.voices.map(v=>{const a=e.context.createAnalyser();a.fftSize=1024;v.fade.connect(a);return {id:v.id,fade:v.fade,analyser:a};});window.flowTimer=setInterval(()=>{const sample={time:e.context.currentTime-e.startedAt};for(const p of window.flowProbes){const data=new Float32Array(p.analyser.fftSize);p.analyser.getFloatTimeDomainData(data);sample[p.id]={gain:p.fade.gain.value,pcmMax:Math.max(...data.map(Math.abs))};}window.flowSamples.push(sample);},100);return {duration:e.plan.duration,voices:e.voices.map(v=>({id:v.id,loop:v.source.loop,duration:v.source.buffer.duration})),plan:e.plan};});
 assert.ok(initial.voices.every(v=>v.loop));assert.equal(initial.voices.find(v=>v.id==='wind').duration,12);
 // One real run covers the wind loop and the leaf recording's first wrap, with no accelerated clock.
 await frame.locator('body').evaluate(async()=>{const e=AudioClusterLab.engine;while(e.context.currentTime-e.startedAt<29)await new Promise(r=>setTimeout(r,100));});
 continuity=await frame.locator('body').evaluate(()=>{clearInterval(window.flowTimer);const e=AudioClusterLab.engine;return {playing:e.playing,elapsed:e.context.currentTime-e.startedAt,samples:window.flowSamples};});}
 assert.equal(continuity.playing,true);assert.ok(continuity.elapsed>=29);
 const steady=continuity.samples.filter(s=>s.time>3&&s.time<continuity.elapsed-1),together=steady.filter(s=>s.wind?.gain>.015&&s.leaves?.gain>.015&&s.wind.pcmMax>0&&s.leaves.pcmMax>0);
 assert.ok(together.length>steady.length*.65);for(const id of ['wind','leaves']){const values=steady.map(s=>s[id].gain);assert.ok(Math.max(...values)-Math.min(...values)>.07);}
 results.push('Over29s actual loop playback:wind and leaves coexist most of the time with changing strength;real PCM observed through wind/leaf wrap');
 writeFileSync(join(evidenceDir,'playback-observations.json'),JSON.stringify({initial,continuity},null,2)+'\n');
 await frame.locator('#stop').click();await page.waitForTimeout(300);const fading=await frame.locator('body').evaluate(()=>({voices:AudioClusterLab.engine.voices.length,gain:AudioClusterLab.engine.master.gain.value,fade:AudioClusterLab.engine.lastFadeDuration}));assert.equal(fading.fade,1.2);assert.ok(fading.voices>0);assert.ok(fading.gain>0&&fading.gain<5.6);
 await page.waitForTimeout(1100);assert.equal(await frame.locator('body').evaluate(()=>AudioClusterLab.engine.voices.length),0);
 await frame.locator('#duration').selectOption('0');await frame.locator('#play').click();await page.waitForTimeout(200);await frame.locator('#immediate').click();assert.equal(await frame.locator('body').evaluate(()=>AudioClusterLab.engine.voices.length),0);
 await frame.locator('#duration').selectOption('180');await frame.locator('#new').click();await page.waitForTimeout(250);const changed=await frame.locator('body').evaluate(()=>AudioClusterLab.engine.plan);assert.notDeepEqual(changed.wind,initial.plan.wind);
 for(const [id,value] of [['master','45'],['water','62'],['wind','39'],['leaves','81']]){await frame.locator('#'+id).fill(value);await frame.locator('#'+id).dispatchEvent('input');assert.equal(await frame.locator('#'+id+'Value').textContent(),value+'%');}
 for(const profile of ['ripples','shore','calm']){await frame.locator('#waterProfile').selectOption(profile);await page.waitForTimeout(350);assert.equal(await frame.locator('#waterProfile').inputValue(),profile);}
 results.push('Soft stop keeps a1.2s fade;emergency stop cancels;continuous/180s selection,new wind,master/layer controls and all water selections respond');
 await frame.locator('body').evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,value:true});document.dispatchEvent(new Event('visibilitychange'));});assert.equal(await frame.locator('body').evaluate(()=>AudioClusterLab.engine.voices.length),0);await frame.locator('body').evaluate(()=>{delete document.hidden;document.dispatchEvent(new Event('visibilitychange'));});await page.waitForTimeout(150);assert.equal(await frame.locator('body').evaluate(()=>AudioClusterLab.engine.playing),false);
 await frame.locator('#recording').evaluate(e=>e.play());await page.waitForTimeout(300);const recording=await frame.locator('#recording').evaluate(e=>({paused:e.paused,time:e.currentTime,duration:e.duration}));assert.equal(recording.paused,false);assert.ok(recording.time>0);assert.equal(recording.duration,45);await frame.locator('#immediate').click();assert.equal(await frame.locator('#recording').evaluate(e=>e.paused),true);
 await frame.locator('details').evaluate(e=>e.open=true);await frame.locator('#loadEffects').click();await page.waitForTimeout(500);assert.equal(await frame.locator('#effectsHost iframe').count(),1);const effects=frame.locator('#effectsHost iframe').contentFrame();await effects.locator('#meow').click();await effects.locator('body').evaluate(async()=>{while(!AudioLab.engine.played.some(p=>p.cue==='meow'))await new Promise(r=>setTimeout(r,20));});
 const reused=await effects.locator('body').evaluate(async()=>{const e=AudioLab.engine;await e.startEnvironment();await e.playGust();await e.startTrack('water');await e.startTrack('wind');await e.startTrack('leaves');return {bankVersion:AudioLab.ambience.version,effectsOnly:AudioLab.effectsOnly,meow:e.played.some(p=>p.cue==='meow'),oldAmbiencePaused:['water','wind','leaves'].every(id=>e.tracks.get(id).element.paused)};});assert.equal(reused.bankVersion,4);assert.equal(reused.effectsOnly,true);assert.equal(reused.meow,true);assert.equal(reused.oldAmbiencePaused,true);assert.equal(await frame.locator('body').evaluate(()=>AudioClusterLab.engine.playing),false);
 results.push('Hide cancels without resuming;native45s recording plays;effects load only on request and actual meow leaves old wind/leaf paused');
 await frame.locator('#play').click();await page.waitForTimeout(250);await page.evaluate(()=>{window.previousAudioContext=document.querySelector('#stage').contentWindow.AudioClusterLab.engine.context;});await page.locator('nav button').first().click();await page.waitForTimeout(300);assert.equal(await page.evaluate(()=>window.previousAudioContext.state),'closed');results.push('Switching unified preview navigation closes the audio context');
 await page.locator('nav button[data-route="audio"]').click();await frameReady();await page.screenshot({path:join(evidenceDir,'preview-desktop.png'),fullPage:true});await page.setViewportSize({width:390,height:844});await page.reload();await frameReady();await page.waitForTimeout(200);assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);assert.equal(await page.locator('#stage').evaluate(e=>e.contentDocument.documentElement.scrollWidth<=e.contentWindow.innerWidth),true);await page.screenshot({path:join(evidenceDir,'preview-mobile.png'),fullPage:true});
 const windRequests=requests.filter(u=>u.includes('/Audio/ambience/pond_wind_open_loop.wav'));assert.ok(windRequests.some(u=>new URL(u).searchParams.get('v')===identity.asset.sha256.slice(0,12)));assert.ok(!requests.some(u=>/\/Audio\/ambience\/pond_wind_open\.wav/.test(u)));
 assert.deepEqual(errors,[]);assert.deepEqual(failedResponses,[]);results.push('Desktop/mobile unified preview fits;zero HTTP or JavaScript errors');
 const report={status:'PASS',date:'2026-10-30',scope:'Unified browser preview only;actual playback and envelope checks,not subjective acceptance or Windows hardware evidence',browser:await browser.version(),results,identity,reused,requests,longPlaybackEvidence:'playback-observations.json',initial,continuity,fading,recording,errors,failedResponses};writeFileSync(join(evidenceDir,'browser-validation.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify({status:report.status,results},null,2));
}finally{await browser.close();}
