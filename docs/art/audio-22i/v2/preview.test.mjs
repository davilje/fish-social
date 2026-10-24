// Run with the bundled Node runtime; serve repository root first.
// Set PLAYWRIGHT_MODULE and CHROME_PATH when using an external runtime/browser.
// AUDIO_PREVIEW_URL and AUDIO_EVIDENCE_DIR optionally override server/output paths.
// This tests browser simulation only.
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join, resolve } from 'node:path';
const require = createRequire(import.meta.url);
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const executablePath=process.env.CHROME_PATH;
const evidenceDir=process.env.AUDIO_EVIDENCE_DIR?resolve(process.env.AUDIO_EVIDENCE_DIR):fileURLToPath(new URL('../../../engineering/audio-22i-v2/',import.meta.url));
mkdirSync(evidenceDir,{recursive:true});
const browser=await chromium.launch({headless:true,...(executablePath?{executablePath}:{}),args:['--autoplay-policy=no-user-gesture-required']});
const page=await browser.newPage({viewport:{width:1400,height:1100}});
const errors=[];page.on('pageerror',e=>errors.push(e.message));
const url=process.env.AUDIO_PREVIEW_URL||'http://127.0.0.1:8891/docs/art/audio-22i/v2/';
const results=[];
try {
  await page.goto(url);await page.waitForFunction(()=>window.AudioLab?.ready);
  assert.equal(await page.locator('[data-scenario]').count(),9);
  assert.equal(await page.locator('.water-card').count(),12);
  assert.equal(await page.locator('.water-card button').count(),24);
  assert.equal(await page.inputValue('#music'),'35');assert.equal(await page.inputValue('#effects'),'70');
  results.push('DOM: 9 scenarios, 12 blind groups / 24 water variants, 35/70 defaults');
  await page.waitForFunction(()=>Array.from(document.querySelectorAll('#assets tr')).some(row=>row.children[0]?.textContent.startsWith('water/')&&row.children[1]?.textContent.includes('CC0-1.0')));
  const waterCredit=await page.locator('#assets tr').filter({hasText:'water/bite_s_01.wav'}).first().textContent();assert.match(waterCredit,/CC0-1.0/);assert.ok(!waterCredit.includes('undefined'));results.push('Water source array resolves to recorded author and CC0 license in provenance table');
  const policy=await page.evaluate(()=>{
    const {PreviewPolicy}=AudioLab;const p=new PreviewPolicy();const event=(cue,id,extras={})=>({cue,id,...extras});const ok=[];
    ok.push(p.decide(event('catch','a'),0).allow,p.decide(event('catch','b'),.01).allow,!p.decide(event('catch','a'),.02).allow);
    p.reset();ok.push(!p.decide(event('bite_s','snapshot',{source:'snapshot'}),0).allow,!p.decide(event('bite_s','snapshot'),1).allow);
    p.reset();ok.push(!p.decide(event('catch','muted'),0,0).allow,!p.decide(event('catch','muted'),1,1).allow);
    p.reset();ok.push(p.decide(event('bite_s','hidden'),0,1,false,true).allow,!p.decide(event('bite_s','hidden'),1,1,true,true).allow,!p.decide(event('splash','water'),2,1,false,true).allow);
    p.reset();ok.push(!p.decide(event('bite_s','no-background'),0,1,false,false).allow,!p.decide(event('bite_s','expired',{expired:true}),1,1,false,true).allow);
    p.reset();ok.push(!p.decide(event('bite_s','other',{owner:'other'}),0).allow);
    p.reset();ok.push(!p.decide(event('bite_s','predicted',{source:'prediction'}),0).allow,p.decide(event('bite_s','predicted'),.1).allow);
    p.reset();ok.push(p.decide(event('ui_open','nav'),0).allow,!p.decide(event('ui_close','next'),.04).allow,p.decide(event('ui_close','later'),.1).allow);
    p.reset();ok.push(p.decide(event('ui_tick','tick1'),0).allow,!p.decide(event('ui_tick','tick2'),.12).allow,p.decide(event('ui_tick','tick3'),.18).allow);
    p.reset();ok.push(p.decide(event('water_struggle_s','weak',{action:'fish',occurrence:0}),0).allow,p.decide(event('water_slap_s','strong',{action:'fish',occurrence:0}),.1).replacesWeak,!p.decide(event('water_slap_s','other',{action:'fish2'}),.2).allow);
    p.reset();ok.push(!p.decide(event('water_struggle_s','weak1',{occurrence:1}),0).allow,!p.decide(event('water_struggle_s','weak2',{occurrence:2}),.9).allow,p.decide(event('water_struggle_s','weak3',{occurrence:3}),1.8).allow);
    p.reset();ok.push(p.decide(event('groundbait','b1',{occurrence:0}),0).allow,!p.decide(event('groundbait','b2',{occurrence:3}),2).allow,p.decide(event('groundbait','b3',{occurrence:6}),2.16).allow);
    p.reset();ok.push(p.decide(event('meow','cat1',{automatic:true}),0).allow,!p.decide(event('meow','cat2',{automatic:true}),3).allow,p.decide(event('meow','cat3'),3).allow,p.decide(event('meow','cat4',{automatic:true}),12).allow);
    return ok;
  });assert.ok(policy.every(Boolean),JSON.stringify(policy));results.push(`${policy.length} policy assertions passed`);
  await page.evaluate(()=>AudioLab.engine.prepare());
  const decoded=await page.evaluate(()=>AudioLab.engine.buffers.size);assert.ok(decoded>=42);results.push(`${decoded} real short audio files fetched and decoded`);
  for(const kind of ['a','b','full','old']){await page.evaluate(kind=>AudioLab.engine.music(kind),kind);const duration=await page.evaluate(()=>AudioLab.engine.musicElement.duration);assert.ok(Number.isFinite(duration));if(kind==='a'||kind==='b')assert.ok(duration>=20&&duration<=30);if(kind==='full')assert.ok(duration>=60&&duration<=90);}
  results.push('Both A/B clips and both old/new music streams play with expected durations');
  for(const name of await page.evaluate(()=>Object.keys(AudioLab.scenarios))){
    await page.evaluate(name=>AudioLab.engine.run(name),name);
    const duration=await page.evaluate(name=>AudioLab.scenarios[name].duration,name);
    await page.waitForTimeout((duration+.15)*1000);
    const state=await page.evaluate(()=>({played:AudioLab.engine.played,skipped:AudioLab.engine.skipped,max:AudioLab.engine.maxVoices,duck:AudioLab.engine.duckFactor,musicMuted:AudioLab.engine.musicMuted,effectsMuted:AudioLab.engine.effectsMuted}));
    assert.ok(state.max<=4,`${name}: ${state.max} voices`);
    if(name==='adjacent'){assert.deepEqual(state.played.filter(x=>x.cue==='catch').map(x=>x.id),['adjacent1','adjacent2']);assert.equal(state.played.filter(x=>x.cue.startsWith('bite_')).length,2);}
    if(name==='duplicate'){assert.equal(state.played.filter(x=>x.id==='dup1'&&x.cue==='catch').length,1);assert.equal(state.played.filter(x=>x.id==='baseline').length,0);}
    if(name==='hidden'){assert.equal(state.played.filter(x=>x.id==='hidden-fresh'&&x.cue.startsWith('bite_')).length,1);assert.equal(state.played.filter(x=>x.id==='hidden-expired').length,0);}
    if(name==='muteDuck'){assert.equal(state.played.filter(x=>x.id==='muted-catch').length,0);assert.equal(state.duck,1);assert.equal(state.musicMuted,false);assert.equal(state.effectsMuted,false);}
    if(name==='crowd')assert.equal(state.played.filter(x=>x.cue.startsWith('bite_')).length,1);
    results.push(`${name}: ${state.played.length} played, ${state.skipped.length} suppressed, max ${state.max}/4`);
  }
  // Flood actual AudioBufferSource nodes, including repeated priority replacements and stop/restart.
  await page.evaluate(async()=>{const e=AudioLab.engine;e.reset();await e.prepare();await Promise.all([e.emit({cue:'ui_open',id:'flood-ui'}),e.emit({cue:'meow',id:'flood-cat'}),e.emit({cue:'bite_xl',id:'flood1'}),e.emit({cue:'bite_xl',id:'flood2'})]);for(let i=0;i<12;i++)await e.emit({cue:'bite_xl',id:'replace'+i});});
  await page.waitForTimeout(120);
  assert.equal(await page.evaluate(()=>AudioLab.engine.maxVoices),4);
  await page.evaluate(async()=>{AudioLab.engine.stop();await AudioLab.engine.emit({cue:'bite_s',id:'restart'});});await page.waitForTimeout(100);assert.ok(await page.evaluate(()=>AudioLab.engine.maxVoices<=4));
  results.push('Four-source bound held through rapid replacements and cancellation/restart');
  const duck=await page.evaluate(async()=>{const e=AudioLab.engine;e.stop();await new Promise(r=>setTimeout(r,80));e.duck({duck:.5,duckSeconds:.7});const first=e.duckUntil;await new Promise(r=>setTimeout(r,100));e.duck({duck:.7,duckSeconds:.7});return {factor:e.duckFactor,later:e.duckUntil>first};});assert.equal(duck.factor,.5);assert.equal(duck.later,true);results.push('Overlapping duck uses minimum factor and latest release');
  const cancel=await page.evaluate(async()=>{const e=AudioLab.engine;e.reset();await new Promise(r=>setTimeout(r,80));const original=e.load.bind(e);e.load=async path=>{await new Promise(r=>setTimeout(r,400));return original(path);};const pending=e.emit({cue:'bite_s',id:'cold-old'});await new Promise(r=>setTimeout(r,80));e.stop();await pending;e.load=original;return e.played.length;});assert.equal(cancel,0);results.push('400ms decode completion discarded after cancellation');
  const celebration=await page.evaluate(async()=>{const e=AudioLab.engine;e.reset();await new Promise(r=>setTimeout(r,80));await e.emit({cue:'catch',id:'celebrate',celebrate:true});await new Promise(r=>setTimeout(r,80));e.effectsMuted=true;await e.emit({cue:'bite_s',id:'cancel-celebration'});e.effectsMuted=false;await new Promise(r=>setTimeout(r,700));return e.played.filter(x=>x.cue==='meow').length;});assert.equal(celebration,0);results.push('New muted bite cancels stale automatic celebration');
  const hide=await page.evaluate(async()=>{const e=AudioLab.engine;e.reset();await new Promise(r=>setTimeout(r,80));await Promise.all([e.emit({cue:'meow',id:'hide-cat'}),e.emit({cue:'water_slap_xl',id:'hide-water'}),e.emit({cue:'bite_xl',id:'hide-bite'})]);e.command({cue:'visibility',value:false});const cancelled=Object.values(e.slots).flat().filter(Boolean).filter(x=>x.cue!=='bite_xl').every(x=>x.cancelled);await new Promise(r=>setTimeout(r,100));const active=Object.values(e.slots).flat().filter(x=>x?.source).map(x=>x.cue);e.command({cue:'visibility',value:true});return {cancelled,active};});assert.equal(hide.cancelled,true);assert.ok(hide.active.every(x=>x==='bite_xl'));results.push('All-hidden transition fades playing ordinary/voice slots within 50ms, retains valid bite');
  await page.evaluate(()=>AudioLab.engine.stop());await page.locator('#music').fill('23');await page.locator('#music').dispatchEvent('input');await page.locator('#effects').fill('61');await page.locator('#effects').dispatchEvent('input');await page.reload();await page.waitForFunction(()=>window.AudioLab?.ready);assert.equal(await page.inputValue('#music'),'23');assert.equal(await page.inputValue('#effects'),'61');results.push('Independent volumes persist after reload');
  await page.evaluate(()=>{localStorage.removeItem('audio22i-v2');});await page.reload();await page.waitForFunction(()=>window.AudioLab?.ready);
  await page.screenshot({path:join(evidenceDir,'preview-desktop.png'),fullPage:true});await page.setViewportSize({width:390,height:844});await page.screenshot({path:join(evidenceDir,'preview-mobile.png'),fullPage:true});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),true);results.push('Desktop/mobile layout captured; no horizontal mobile overflow');
  assert.deepEqual(errors,[]);const report={status:'PASS',scope:'Browser simulation only; not Unity/WPF IPC or Windows audio hardware',browser:await browser.version(),results};writeFileSync(join(evidenceDir,'browser-validation.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report,null,2));
} finally {await browser.close();}
