// Serve the repository root. Optional: PLAYWRIGHT_MODULE, CHROME_PATH,
// AUDIO_PREVIEW_URL, AUDIO_EVIDENCE_DIR. Browser simulation only.
// Adopted scheme regression: music transport / hashes, accepted SFX freeze,
// one normal fishing mix and independent volumes. Existing nine demos remain.
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {mkdirSync,writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {join,resolve} from 'node:path';
const require=createRequire(import.meta.url);
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const evidenceDir=process.env.AUDIO_EVIDENCE_DIR?resolve(process.env.AUDIO_EVIDENCE_DIR):fileURLToPath(new URL('../../../engineering/audio-22i-adoption/',import.meta.url));
mkdirSync(evidenceDir,{recursive:true});
const url=process.env.AUDIO_PREVIEW_URL||'http://127.0.0.1:8891/docs/art/audio-22i/v5/';
const browser=await chromium.launch({headless:true,...(process.env.CHROME_PATH?{executablePath:process.env.CHROME_PATH}:{}),args:['--autoplay-policy=no-user-gesture-required']});
const page=await browser.newPage({viewport:{width:1400,height:1100}});
const errors=[],results=[],musicPlayback=[],requests=[],failedResponses=[];
function observe(page){page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>requests.push(r.url()));page.on('response',r=>{if(r.status()>=400)failedResponses.push({url:r.url(),status:r.status()});});}observe(page);
try {
  await page.goto(url);await page.waitForFunction(()=>window.AudioLab?.ready);
  assert.match(await page.evaluate(()=>AudioLab.ROOT),/StreamingAssets\/Audio\//);
  assert.deepEqual(await page.locator('[data-music]').evaluateAll(nodes=>nodes.map(n=>n.dataset.music)),['theme','full']);
  assert.equal(await page.locator('[data-scenario]').count(),9);
  assert.equal(await page.locator('.water-card button').count(),24);
  assert.equal(await page.inputValue('#music'),'35');assert.equal(await page.inputValue('#effects'),'70');
  assert.match(await page.locator('header').textContent(),/已确认采用/);
  assert.doesNotMatch(await page.locator('body').textContent(),/待试听|待听感|旧 v[1-4]/);
  assert.equal(await page.locator('[data-paper-cue]').count(),7);assert.equal(await page.locator('[data-old],[data-compare-version]').count(),0);
  results.push('Two current music entries; nine demos and 24 blind water samples retained; default volumes 35/70');

  const identity=await page.evaluate(async()=>{
    const json=path=>fetch(path,{cache:'no-store'}).then(r=>{if(!r.ok)throw Error(path+' '+r.status);return r.json();});
    const [current,clips,accepted,adoption]=await Promise.all([json(AudioLab.ROOT+'manifest.json'),json('assets/manifest.json'),json('../../../engineering/audio-22i-sfx-acceptance.json'),json('../../../engineering/audio-22i-adoption.json')]);
    const sha=async path=>Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',await fetch(path,{cache:'no-store'}).then(r=>{if(!r.ok)throw Error(path+' '+r.status);return r.arrayBuffer();}))),x=>x.toString(16).padStart(2,'0')).join('');
    const effects=await Promise.all(accepted.accepted.map(async acceptedAsset=>{const asset=current.assets.find(x=>x.path===acceptedAsset.path);return {path:acceptedAsset.path,accepted:acceptedAsset.sha256,manifest:asset?.sha256,actual:await sha(AudioLab.ROOT+acceptedAsset.path),version:asset?.version};}));
    const music=[];for(const [kind,asset,root] of [['theme',clips.assets.find(x=>x.path==='sunlight-theme-preview.wav'),'assets/'],['full',current.assets.find(x=>x.path==='music/pond_daylight.wav'),AudioLab.ROOT]]){if(!asset)throw Error('Missing music manifest: '+kind);music.push({kind,path:root+asset.path,duration:asset.duration,version:asset.version,expected:asset.sha256,actual:await sha(root+asset.path)});}
    const clipAssets=await Promise.all(clips.assets.map(async asset=>({path:asset.path,expected:asset.sha256,actual:await sha('assets/'+asset.path)})));
    const currentAssets=await Promise.all(current.assets.map(async asset=>({path:asset.path,manifest:asset.sha256,adopted:adoption.assets.find(x=>x.path===asset.path)?.sha256,actual:await sha(AudioLab.ROOT+asset.path)})));
    return {versions:{current:current.version,clips:clips.version},manifestAcceptance:current.acceptance,adoption,effects,music,clipAssets,currentAssets,cueConfigHash:await sha(AudioLab.ROOT+'cue-config.json'),configuredPaths:AudioLab.config.cues.flatMap(x=>x.paths)};
  });
  assert.deepEqual(identity.versions,{current:5,clips:5});
  assert.equal(identity.effects.length,46);
  assert.ok(identity.effects.every(x=>x.version===3&&x.actual===x.accepted&&x.manifest===x.accepted));
  assert.deepEqual([...new Set(identity.configuredPaths)].sort(),identity.effects.map(x=>x.path).sort());
  assert.equal(identity.music.length,2);assert.ok(identity.music.every(x=>x.actual===x.expected));
  assert.equal(identity.music.find(x=>x.kind==='full').version,5);assert.equal(identity.music.find(x=>x.kind==='theme').version,5);
  assert.equal(identity.adoption.status,'adopted');assert.equal(identity.adoption.version,5);assert.equal(identity.manifestAcceptance.status,'adopted');
  assert.equal(identity.adoption.music.sha256,identity.music.find(x=>x.kind==='full').actual);
  assert.equal(identity.currentAssets.length,47);assert.ok(identity.currentAssets.every(x=>x.manifest===x.adopted&&x.actual===x.adopted));
  assert.equal(identity.cueConfigHash,identity.adoption.cueConfigSha256);assert.ok(identity.clipAssets.every(x=>x.actual===x.expected));
  results.push('All 46 configured non-music files match accepted v3 hashes; all 47 adopted production files, cue config and current preview assets match fresh hashes');

  await page.waitForFunction(()=>Array.from(document.querySelectorAll('#assets tr')).some(row=>row.children[0]?.textContent==='music/pond_daylight.wav'&&row.children[1]?.textContent.includes('v5 配乐')));
  assert.match(await page.locator('#assets tr').filter({hasText:'water/bite_s_01.wav'}).first().textContent(),/v3 音效 · 已采用.*CC0/);
  for(const kind of ['theme','full']){
    await page.locator(`[data-music=${kind}]`).click();
    await page.waitForFunction(kind=>AudioLab.engine.musicElement?.currentSrc===new URL({theme:'assets/sunlight-theme-preview.wav',full:AudioLab.ROOT+'music/pond_daylight.wav'}[kind],location.href).href&&!AudioLab.engine.musicElement.paused&&Number.isFinite(AudioLab.engine.musicElement.duration),kind);
    const state=await page.evaluate(()=>{const e=AudioLab.engine;const same=!window.testMusicElement||window.testMusicElement===e.musicElement;window.testMusicElement=e.musicElement;return {src:e.musicElement.currentSrc,duration:e.musicElement.duration,loop:e.musicElement.loop,same};});
    assert.equal(state.loop,kind!=='theme');assert.equal(state.same,true);
    assert.ok(Math.abs(state.duration-identity.music.find(x=>x.kind===kind).duration)<.02);
    musicPlayback.push({kind,...state});
  }
  // Seek near the boundary to verify browser loop transport, not acoustic seam quality.
  for(const kind of ['full']){await page.evaluate(async kind=>{await AudioLab.engine.music(kind);const a=AudioLab.engine.musicElement;a.currentTime=a.duration-.2;},kind);await page.waitForFunction(()=>AudioLab.engine.musicElement.currentTime<2&&!AudioLab.engine.musicElement.paused);}
  await page.locator('#stop').click();assert.equal(await page.evaluate(()=>AudioLab.engine.musicElement.paused),true);
  results.push('Theme/full playback switches on one music element; full wraps at boundary; stop pauses');

  await page.evaluate(()=>AudioLab.engine.reset());await page.waitForTimeout(70);
  await page.locator('[data-paper-cue=ui_open]').click();await page.waitForFunction(()=>AudioLab.engine.played.some(x=>x.cue==='ui_open'));
  await page.locator('#waterCurrent').click();await page.waitForFunction(()=>AudioLab.engine.played.some(x=>x.cue==='bite_m'));
  results.push('Current-only paper button and selected water action play their configured effects');

  await page.evaluate(()=>AudioLab.engine.run('fishing'));
  await page.waitForTimeout(7600);
  const mix=await page.evaluate(()=>({played:AudioLab.engine.played,maxVoices:AudioLab.engine.maxVoices,musicPlaying:!AudioLab.engine.musicElement.paused}));
  assert.equal(mix.played.length,8);assert.ok(mix.maxVoices<=4);assert.equal(mix.musicPlaying,true);
  assert.equal(mix.played.filter(x=>x.cue==='catch').length,1);assert.equal(mix.played.filter(x=>x.cue==='meow').length,1);
  results.push(`Normal fishing mix: ${mix.played.length} cues, ${mix.maxVoices}/4 maximum short voices, current full music continues`);
  await page.locator('#music').fill('23');await page.locator('#music').dispatchEvent('input');
  assert.equal(await page.evaluate(()=>AudioLab.engine.effectsVolume),.7);
  await page.locator('#effects').fill('61');await page.locator('#effects').dispatchEvent('input');
  assert.equal(await page.evaluate(()=>AudioLab.engine.musicVolume),.23);
  await page.waitForTimeout(120);
  const gains=await page.evaluate(()=>({music:AudioLab.engine.musicMaster.gain.value,effects:AudioLab.engine.sfx.gain.value}));
  assert.ok(Math.abs(gains.music-.23)<.002);assert.ok(Math.abs(gains.effects-.61)<.002);
  await page.locator('#stop').click();await page.reload();await page.waitForFunction(()=>window.AudioLab?.ready);
  assert.equal(await page.inputValue('#music'),'23');assert.equal(await page.inputValue('#effects'),'61');results.push('Independent music/effects gains and saved 23/61 settings verified');
  await page.evaluate(()=>localStorage.removeItem('audio22i-v5'));await page.reload();await page.waitForFunction(()=>window.AudioLab?.ready);await page.evaluate(()=>document.fonts.ready);
  await page.screenshot({path:join(evidenceDir,'preview-desktop.png'),fullPage:true,animations:'disabled'});
  const mobile=await browser.newPage({viewport:{width:390,height:844}});observe(mobile);await mobile.goto(url);await mobile.waitForFunction(()=>window.AudioLab?.ready);await mobile.evaluate(()=>document.fonts.ready);await mobile.waitForTimeout(250);
  assert.equal(await mobile.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);
  await mobile.screenshot({path:join(evidenceDir,'preview-mobile.png'),fullPage:true,animations:'disabled'});await mobile.close();results.push('Desktop/mobile screenshots captured; no horizontal overflow');
  const obsoleteRequests=requests.filter(url=>/\/audio-22i\/(?:baseline-v[1-4]|v[2-4])(?:\/|$)/.test(new URL(url).pathname));
  assert.deepEqual(obsoleteRequests,[]);assert.deepEqual(failedResponses,[]);assert.deepEqual(errors,[]);results.push('Zero obsolete directory requests, failed HTTP responses or browser errors');
  const report={status:'PASS',date:new Date().toISOString(),scope:'Adopted v5 assets and browser interaction regression; not Unity/WPF IPC or Windows audio hardware',browser:await browser.version(),results,identity,musicPlayback,mix,obsoleteRequests,failedResponses};
  writeFileSync(join(evidenceDir,'browser-validation.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify({status:report.status,results},null,2));
} finally {await browser.close();}
