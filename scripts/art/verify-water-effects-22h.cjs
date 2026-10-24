/** Run after build-water-effects-22h.cjs. Starts a private, read-only HTTP server. */
const fs=require('fs'),path=require('path'),crypto=require('crypto'),http=require('http'),assert=require('assert/strict');
const sharp=require('sharp'),{chromium}=require('playwright');
const art=process.argv.includes('--v2')?'v2':'v4';
const root=path.resolve(__dirname,'../..'),out=path.join(root,`desktop-overlay/OverlayResources/effects/fishing/${art}`),mirror=path.join(root,`fish-social-unity/Assets/StreamingAssets/Effects/Fishing/${art}`),doc=path.join(root,'docs/art/water-effects-22h');
const nativeMirror=path.join(root,`fish-social-unity/FishSocialOverlay/OverlayResources/effects/fishing/${art}`);
const sha=data=>crypto.createHash('sha256').update(data).digest('hex');
async function main(){
 const manifest=JSON.parse(fs.readFileSync(path.join(out,'manifest.json'))),effects=JSON.parse(fs.readFileSync(path.join(out,'effects.json'))),assets=[];
 assert.equal(manifest.assets.length,18);assert.equal(manifest.effectsSha256,sha(fs.readFileSync(path.join(out,'effects.json'))));
 assert.deepEqual(effects.cues.map(c=>c.id),(art==='v4'?['FX01','FX02','FX03','FX04','FX05','FX08','FX09']:['FX01','FX02','FX03','FX04','FX05','FX08']));assert.equal(effects.assets.filter(a=>a.kind==='splash').length,8);assert.equal(effects.assets.filter(a=>a.kind==='ripple').length,8);
 for(const a of manifest.assets){const bytes=fs.readFileSync(path.join(out,a.file)),meta=await sharp(bytes).metadata();assert.equal(sha(bytes),a.sha256);assert.equal(meta.width,a.kind==='drop'?64:128);assert.equal(meta.height,64);assert(meta.hasAlpha);assert.equal(sha(fs.readFileSync(a.sourceFile?path.join(doc,a.sourceFile):path.join(doc,'sources',a.id+'.svg'))),a.sourceSha256);assert.deepEqual(bytes,fs.readFileSync(path.join(mirror,a.file)));
  assert.deepEqual(bytes,fs.readFileSync(path.join(nativeMirror,a.file)));
  const {data,info}=await sharp(bytes).ensureAlpha().raw().toBuffer({resolveWithObject:true});let l=info.width,r=-1,t=info.height,b=-1,visible=0;
  for(let y=0;y<info.height;y++)for(let x=0;x<info.width;x++)if(data[(y*info.width+x)*4+3]){l=Math.min(l,x);r=Math.max(r,x);t=Math.min(t,y);b=Math.max(b,y);visible++;}
  assert(visible>10,a.id+' empty');assert(l>=8&&r<info.width-8&&t>=8&&b<56,a.id+' needs 8px transparent margin');assets.push({id:a.id,alphaBounds:[l,t,r+1,b+1],visiblePixels:visible,sha256:a.sha256});
 }
 assert.equal(new Set(manifest.assets.map(a=>a.sha256)).size,18);assert.equal(manifest.decodedBytes,16*128*64*4+2*64*64*4);assert(manifest.decodedBytes<=4194304);
 for(const f of ['manifest.json','effects.json'])for(const target of [mirror,nativeMirror])assert.deepEqual(fs.readFileSync(path.join(out,f)),fs.readFileSync(path.join(target,f)));
 const sourceMarker=fs.readFileSync(path.join(root,'desktop-overlay/OverlayResources/fishing/source-water-markers.json'));
 for(const target of ['fish-social-unity/Assets/StreamingAssets/Fishing/source-water-markers.json','fish-social-unity/FishSocialOverlay/OverlayResources/fishing/source-water-markers.json'])assert.deepEqual(sourceMarker,fs.readFileSync(path.join(root,target)));
 const server=http.createServer((req,res)=>{const file=path.resolve(root,'.'+decodeURIComponent(req.url.split('?')[0]));if(!file.startsWith(root+path.sep)){res.writeHead(403).end();return;}const target=req.url.split('?')[0].endsWith('/')?path.join(file,'index.html'):file;fs.readFile(target,(err,bytes)=>{if(err){res.writeHead(404).end();return;}res.setHeader('Content-Type',({'.html':'text/html','.js':'text/javascript','.css':'text/css','.png':'image/png','.json':'application/json'})[path.extname(target)]||'application/octet-stream');res.end(bytes);});});
 await new Promise(r=>server.listen(0,'127.0.0.1',r));let browser;
 const browserErrors=[],failedRequests=[],checks=[];
 try{
  browser=await chromium.launch({headless:true,...(process.env.CHROME_EXECUTABLE?{executablePath:process.env.CHROME_EXECUTABLE}:fs.existsSync('/Applications/Google Chrome.app/Contents/MacOS/Google Chrome')?{executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome'}:{})});
  const page=await browser.newPage({viewport:{width:1440,height:1050},deviceScaleFactor:1});page.on('pageerror',e=>browserErrors.push(e.message));page.on('response',r=>{if(r.status()>=400&&!r.url().endsWith('favicon.ico'))failedRequests.push({url:r.url(),status:r.status()});});
  await page.goto(`http://127.0.0.1:${server.address().port}/docs/art/water-effects-22h/`);await page.waitForFunction(()=>window.waterPreviewStats);await page.click('#play');
  async function state(values){const previous=await page.evaluate(()=>window.waterPreviewStats.renderId);await page.evaluate(v=>{for(const [id,value]of Object.entries(v)){const el=document.getElementById(id);el.value=value;el.dispatchEvent(new Event(id==='scrub'?'input':'change'));}},values);await page.waitForFunction(({values,previous})=>{const s=window.waterPreviewStats;return s.renderId>previous&&Object.entries(values).every(([key,value])=>key==='scrub'?Math.abs(s.elapsed-Number(value))<.01:String(s[key])===String(value));},{values,previous});const stats=await page.evaluate(()=>window.waterPreviewStats);assert(stats.instances<=24&&stats.groups<=8);assert.deepEqual(stats.errors,[]);if(stats.quality==='off')assert.equal(stats.instances,0);if(stats.quality==='reduced')assert(stats.instances<=2);checks.push(stats);return stats;}
  const pets=await page.locator('#pet option').evaluateAll(els=>els.map(e=>e.value));assert.equal(pets.length,11);
  for(const pet of pets)for(const slot of ['96','128','192'])for(const quality of ['standard','reduced','off'])for(const water of ['light','dark'])await state({pet,slot,quality,water});
  for(const cue of effects.cues.map(c=>c.id))for(const variant of ['a','b'])await state({cue,variant,quality:'standard',water:'dark',slot:'128',pet:'orange',scrub:String(({FX01:420,FX08:620,FX05:67})[cue]+166.667||166.667)});
  await state({cue:'FX03',variant:'a',quality:'standard',water:'light',slot:'128',scrub:'166.667'});await page.screenshot({path:path.join(doc,art==='v4'?'magic-v4/production-light.png':'preview-light.png'),fullPage:true});
  await state({cue:'FX05',variant:'b',quality:'standard',water:'dark',slot:'192',scrub:'166.667'});await page.screenshot({path:path.join(doc,art==='v4'?'magic-v4/production-dark-xl.png':'preview-dark-xl.png'),fullPage:true});
  await state({cue:'FX03',quality:'reduced',water:'dark',slot:'96',scrub:'100'});await page.screenshot({path:path.join(doc,art==='v4'?'magic-v4/production-reduced.png':'preview-reduced.png'),fullPage:true});
  if(art==='v4'){
   for(const id of ['FX03','FX04']){const s=await state({cue:id,quality:'standard',slot:'192',scrub:'160'});for(const d of Object.values(s.layers)){assert(d.ripples>0);assert.equal(d.splashes,0);assert.equal(d.drops,0);assert.equal(d.ripplePixelsAbove,0);assert(d.rippleTop>=s.waterline);}}
   for(const [id,start]of [['FX01',420],['FX08',620],['FX09',0],['FX05',67]])for(const slot of ['96','128','192']){
    if(start){const before=await state({cue:id,quality:'standard',slot,scrub:String(start-1)});assert.equal(before.instances,0,id+' emitted before entry');}
    const a=await state({cue:id,quality:'standard',slot,scrub:String(start+80)}),b=await state({cue:id,quality:'standard',slot,scrub:String(start+180)});
    assert(a.impactBounds.top<=a.waterline+Number(slot)/128&&a.impactBounds.bottom>=a.waterline-Number(slot)/128,id+' source layer does not touch water at emission');
    assert.deepEqual(a.impactAnchors,b.impactAnchors,id+' impact moved after entry');assert.equal(a.impactFrame,id==='FX01'?3:id==='FX08'?5:0);
    for(const d of Object.values(a.layers)){assert(d.splashes>0&&d.drops>0,id+' missing physical splash');assert.equal(d.ripplePixelsAbove,0);assert(d.rippleTop>=a.waterline);assert(d.splashTop<a.waterline,id+' water crown incorrectly clipped to water');}
   }
   const s=await state({cue:'FX05',slot:'128',quality:'standard',scrub:'120'});assert(s.layers.XL.splashTop<s.layers.S.splashTop,'fish exit must scale to visible size');
   await state({cue:'FX08',slot:'192',quality:'standard',water:'light',scrub:'700'});await page.screenshot({path:path.join(doc,'magic-v4/production-groundbait-contact.png'),fullPage:true});
   await state({cue:'FX01',slot:'192',quality:'standard',water:'dark',scrub:'500'});await page.screenshot({path:path.join(doc,'magic-v4/production-cast-contact.png'),fullPage:true});
  }
  const before=await page.evaluate(()=>window.waterPreviewStats.elapsed);await page.click('#next');await page.waitForFunction(v=>window.waterPreviewStats.elapsed>v,before);const next=await page.evaluate(()=>window.waterPreviewStats.elapsed);assert(Math.abs(next-before-1000/60)<.1);await page.click('#previous');await page.waitForFunction(v=>Math.abs(window.waterPreviewStats.elapsed-v)<.1,before);
  const paused=await page.evaluate(()=>window.waterPreviewStats.elapsed);await page.waitForTimeout(120);assert.equal(await page.evaluate(()=>window.waterPreviewStats.elapsed),paused);await page.click('#play');await page.waitForFunction(v=>window.waterPreviewStats.elapsed>v,paused);await page.click('#play');
  assert.deepEqual(browserErrors,[]);assert.deepEqual(failedRequests,[]);
 }finally{if(browser)await browser.close();await new Promise(r=>server.close(r));}
 const report={artVersion:manifest.version,task:'STEAM-DESKTOP-22H',date:'2026-10-24',behaviorRevision:effects.behaviorRevision,contactRules:art==='v4'?'Bite/struggle ripple only; real cast/bait/retrieve entry at held source contact; full ripples below waterline, crown/drop above allowed; fish exit scales.':undefined,scope:'Resource integrity and Web preview only; no Windows runtime performance claim. v4 style approval is recorded separately.',assets,decodedBytes:manifest.decodedBytes,mirrorIdentical:true,nativeOverlayMirrorIdentical:true,sourceMarkerMirrorsIdentical:true,sourceMarkerSha256:sha(sourceMarker),browser:'Installed Google Chrome via Playwright',controlChecks:checks.length,catCount:11,slotSizes:[96,128,192],qualities:['standard','reduced','off'],water:['light','dark'],maxPreviewInstances:Math.max(...checks.map(s=>s.instances)),maxPreviewGroups:Math.max(...checks.map(s=>s.groups)),pageErrors:browserErrors,failedRequests,frameStepMs:1000/60,result:'passed'};
 fs.writeFileSync(path.join(doc,art==='v4'?'verification-v4.json':'verification.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify({...report,assets:report.assets.length},null,2));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
