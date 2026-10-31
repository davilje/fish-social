// Narrow final render check: bytes, decode, chapter seek before playback, and loop seam.
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {writeFileSync} from 'node:fs';
const require=createRequire(import.meta.url),{chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
const browser=await chromium.launch({headless:true,...(process.env.CHROME_PATH?{executablePath:process.env.CHROME_PATH}:{}),args:['--autoplay-policy=no-user-gesture-required']});
try{
 const p=await browser.newPage({viewport:{width:1440,height:1100}}),errors=[];p.on('pageerror',e=>errors.push(e.message));
 await p.goto('http://127.0.0.1:8891/现行预览.html?audio=20261031-sunroom#audio');await p.waitForFunction(()=>document.querySelector('#stage')?.contentWindow?.HomeMusicLab?.score&&document.querySelector('#stage')?.contentWindow?.HomeMusicLab?.ready);
 const f=p.locator('#stage').contentFrame();
 const assets=await f.locator('body').evaluate(async()=>{
  const manifest=await (await fetch('../home-sunroom/assets/manifest.json',{cache:'no-store'})).json(),ctx=new AudioContext(),out=[];
  for(const asset of manifest.assets){
   const res=await fetch('../home-sunroom/assets/'+asset.path,{cache:'no-store'});if(!res.ok)throw Error('HTTP '+res.status);
   const bytes=await res.arrayBuffer(),header=new DataView(bytes),hash=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',bytes)),v=>v.toString(16).padStart(2,'0')).join(''),buffer=await ctx.decodeAudioData(bytes.slice(0));
   out.push({path:asset.path,hash,expectedHash:asset.sha256,duration:buffer.duration,channels:buffer.numberOfChannels,encodedSampleRate:header.getUint32(24,true),decodedSampleRate:buffer.sampleRate});
  }
  await ctx.close();return out;
 });
 for(const asset of assets){assert.equal(asset.hash,asset.expectedHash);assert.equal(asset.channels,2);assert.equal(asset.encodedSampleRate,44100);}
 assert.equal(await f.locator('#musicChapters button').count(),4);const chapters=[];
 const expected=await f.locator('body').evaluate(()=>HomeMusicLab.score.chapters.map(c=>c.startSeconds));
 for(let i=0;i<4;i++){
  await f.locator('#musicChapters button').nth(i).click();
  await f.locator('#homeMusic').evaluate(async(e,target)=>{const start=performance.now();while(Math.abs(e.currentTime-target)>.001){if(performance.now()-start>5000)throw Error('Chapter seek failed');await new Promise(r=>setTimeout(r,20));}},expected[i]);
  chapters.push(await f.locator('#homeMusic').evaluate(e=>e.currentTime));
 }
 await f.locator('#homeMusic').evaluate(async e=>{e.currentTime=0;await e.play();});await p.waitForTimeout(500);assert.equal(await f.locator('#homeMusic').evaluate(e=>e.paused),false);
 await f.locator('#homeMusic').evaluate(e=>e.currentTime=e.duration-.25);assert.ok(await f.locator('#homeMusic').evaluate(e=>e.currentTime>e.duration-1));await p.waitForTimeout(1000);
 const playback=await f.locator('#homeMusic').evaluate(e=>({time:e.currentTime,paused:e.paused,duration:e.duration}));assert.equal(playback.paused,false);assert.ok(playback.time<2);
 await f.locator('#musicStop').click();assert.deepEqual(errors,[]);
 const result={status:'PASS',date:'2026-10-31',scope:'Final rendered bytes plus native browser decode/play/loop and four chapter jumps before first playback',assets,chapterSeekSeconds:chapters,playback,errors};
 writeFileSync('docs/工程/声音/home-sunroom-2026-10-31/preview/final-audio-smoke.json',JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify(result,null,2));
}finally{await browser.close();}
