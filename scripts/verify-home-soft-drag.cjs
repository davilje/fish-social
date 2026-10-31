// Browser-level regression for the six actual grab regions, not only toolbar demos.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
(async()=>{
 const out=path.resolve(__dirname,'../output/home-soft-drag-grab-v4');fs.mkdirSync(out,{recursive:true});
 const browser=await chromium.launch({headless:true,...(process.env.CHROME_PATH?{executablePath:process.env.CHROME_PATH}:{}),args:['--enable-unsafe-swiftshader']});
 try{const page=await browser.newPage({viewport:{width:1500,height:1200}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto((process.env.PREVIEW_BASE||'http://127.0.0.1:8769')+'/现行预览.html#home-soft-drag');await page.waitForFunction(()=>document.querySelector('#stage').src.includes('soft-drag-study'));
 const frame=page.frames().find(f=>f.url().includes('/soft-drag-study-2026-10-31/'));await frame.waitForFunction(()=>window.softDragStudy?.state.ready);await frame.evaluate(()=>softDragStudy.state.paused=true);
 const canvas=frame.locator('#pet'),results=[];await canvas.scrollIntoViewIfNeeded();const box=await canvas.boundingBox(),screen=([x,y])=>[box.x+x/1200*box.width,box.y+y/675*box.height];
 for(const breed of ['orange','calico','blue']){await frame.selectOption('#breed',breed);
 for(const pose of ['sit','crouch','supine']){await frame.selectOption('#pose',pose);if(breed==='orange')await frame.locator('.stage').screenshot({path:path.join(out,pose+'.png')});
 for(const mode of ['head','body','frontLeft','frontRight','hindLeft','hindRight']){
 await frame.evaluate(()=>softDragStudy.reset());const p=await frame.evaluate(m=>softDragStudy.restPoint(m),mode);assert.equal(await frame.evaluate(p=>softDragStudy.pick(p),p),mode,`${breed}/${pose}/${mode}: hit`);
 await page.mouse.move(...screen(p));await page.mouse.down();assert.equal(await frame.evaluate(()=>softDragStudy.state.mode),mode,`${breed}/${pose}/${mode}: pointerdown`);
 await page.mouse.move(...screen([660,145]));const result=await frame.evaluate(({mode,breed,pose})=>{const s=softDragStudy;let error=0,folded=0;
 for(let i=0;i<180;i++){s.step(1/120);if(i%12)continue;s.render();for(const e of s.state.rendered){const d=s.poses[e.id],a=mode==='head'?(d.headGrab||d.head):d[mode],p=s.point(e.id,a,false,e.gain);error=Math.max(error,Math.hypot(p[0]-s.state.target[0],p[1]-s.state.target[1]));for(let j=0;j<s.indices.length;j+=3){const [a,b,c]=s.indices.slice(j,j+3).map(n=>e.pts[n]);if((b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])<=0)folded++;}}}
 s.render();return {breed,pose,mode,error,folded,progress:s.state.progress,held:s.state.held};},{mode,breed,pose});assert.ok(result.error<.01,JSON.stringify(result));assert.equal(result.folded,0);assert.equal(result.progress,1);assert.equal(result.held,true);
 // Crossing the head or another paw while holding must never change the locked part.
 await page.mouse.move(...screen([510,170]));assert.equal(await frame.evaluate(()=>softDragStudy.state.mode),mode);await frame.evaluate(()=>{for(let i=0;i<80;i++)softDragStudy.step(1/120);softDragStudy.render();});
 if(pose==='sit')await frame.locator('.stage').screenshot({path:path.join(out,breed+'-'+mode+'.png')});
 await page.mouse.up();assert.equal(await frame.evaluate(()=>softDragStudy.state.held),false);const landed=await frame.evaluate(()=>{const s=softDragStudy;for(let i=0;i<720;i++)s.step(1/120);s.render();return s.state.phase;});assert.equal(landed,'rest',`${breed}/${pose}/${mode}: landing`);results.push(result);
 }}}
 await frame.selectOption('#breed','orange');await frame.selectOption('#pose','sit');await page.mouse.move(...screen([80,80]));await page.mouse.down();assert.equal(await frame.evaluate(()=>softDragStudy.state.held),false);await page.mouse.up();
 const p=await frame.evaluate(()=>softDragStudy.restPoint('hindRight'));await page.mouse.move(...screen(p));await page.mouse.down();await canvas.dispatchEvent('pointercancel',{pointerId:1});await page.mouse.up();assert.equal(await frame.evaluate(()=>softDragStudy.state.held),false);
 await frame.evaluate(()=>softDragStudy.reset());await frame.selectOption('#grip','body');await frame.evaluate(()=>softDragStudy.state.paused=false);await frame.locator('#demo').click();await page.waitForTimeout(2500);assert.equal(await frame.evaluate(()=>softDragStudy.state.mode),'body');await frame.locator('.stage').screenshot({path:path.join(out,'body-demo.png')});await frame.locator('#reset').click();assert.equal(await frame.evaluate(()=>softDragStudy.state.held),false);
 assert.deepEqual(errors,[]);const report={version:'grab-v4',previewOnly:true,headWidth:148,all54PointerCases:results,transparentBackgroundMiss:'passed',cancelAndReset:'passed',demo:'passed',errors};fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify({cases:results.length,maxPinError:Math.max(...results.map(r=>r.error)),invertedTriangles:results.reduce((n,r)=>n+r.folded,0),errors,out}));
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
