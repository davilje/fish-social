const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
(async()=>{
 const out=path.resolve(__dirname,'../output/home-soft-drag-study-2026-10-31');fs.mkdirSync(out,{recursive:true});
 const browser=await chromium.launch({headless:true,...(process.env.CHROME_PATH?{executablePath:process.env.CHROME_PATH}:{}),args:['--enable-unsafe-swiftshader']});
 try{
  const page=await browser.newPage({viewport:{width:1550,height:1150}}),errors=[];
  page.on('pageerror',e=>errors.push(e.message));
  await page.goto((process.env.PREVIEW_BASE||'http://127.0.0.1:8769')+'/现行预览.html#home-soft-drag');
  await page.waitForFunction(()=>document.querySelector('#stage').src.includes('soft-drag-study'));
  const frame=page.frames().find(f=>f.url().includes('soft-drag-study-2026-10-31/'));
  await frame.waitForFunction(()=>window.softDragStudy?.state.ready);
  const geometry=await frame.evaluate(()=>{
   const {state,partsSpec}=softDragStudy,Cat=state.cat.constructor;
   let maxLengthError=0,maxGripError=0,maxStretch=0,minArea=Infinity,maxIndependentSpread=0,residualSwing=0;
   for(const soft of [.3,.85,1])for(const hit of [{id:'head',u:.6,v:.3},{id:'torso',u:.5,v:.5},{id:'nearArm',u:.5,v:.7},{id:'nearLeg',u:.5,v:.7}]){
    const c=new Cat();c.soft=soft;c.begin(...c.partPoint(hit.id,hit.u,hit.v),hit);
    for(let i=0;i<720;i++){
     c.move(600+170*Math.sin(i/34),hit.id==='head'?180:330);c.step(1/120);
     const bounds=c.bounds(),p=c.partPoint(hit.id,hit.u,hit.v);
     if(bounds.t>12.1&&bounds.b<564.9&&bounds.l>12.1&&bounds.r<1187.9)maxGripError=Math.max(maxGripError,Math.hypot(p[0]-c.grab.x,p[1]-c.grab.y));
     for(const [id,sp] of Object.entries(partsSpec)){
      const a=c.partPoint(id,0,0),b=c.partPoint(id,0,1),d=c.partPoint(id,1,0);
      maxLengthError=Math.max(maxLengthError,Math.abs(Math.hypot(b[0]-a[0],b[1]-a[1])-sp.h),Math.abs(Math.hypot(d[0]-a[0],d[1]-a[1])-sp.w));
     }
     const angles=Object.values(c.joints).map(j=>j.p);maxIndependentSpread=Math.max(maxIndependentSpread,Math.max(...angles)-Math.min(...angles));
     for(let j=0;j<24;j++){const a=c.body(0,j/24),b=c.body(1,j/24),d=c.body(0,(j+1)/24);minArea=Math.min(minArea,(b[0]-a[0])*(d[1]-a[1]));}
     maxStretch=Math.max(maxStretch,c.length.p/95);
    }
    c.move(c.grab.x,c.grab.y);for(let i=0;i<60;i++)c.step(1/120);
    residualSwing=Math.max(residualSwing,...Object.values(c.joints).map(j=>Math.abs(j.v)));
    for(let i=0;i<1800;i++)c.step(1/120);
    if(c.air>.95&&Object.values(c.joints).some(j=>Math.abs(j.p)>.04))throw Error('limbs failed to hang down');
    c.move(-1000,1400);c.step(1/120);let bounds=c.bounds();if(bounds.l<11.9||bounds.r>1188.1||c.supportBottom()>565.1)throw Error('stage bounds');
    c.release();for(let i=0;i<2400;i++)c.step(1/120);
    // Transparent rotated corners can extend below the floor; visible paw soles must touch it.
    if(!c.grounded||Math.abs(c.supportBottom()-565)>.01)throw Error('did not settle');
   }
   return {maxLengthError,maxGripError,maxStretch,minArea,maxIndependentSpread,residualSwing};
  });
  assert.ok(geometry.maxLengthError<1e-8);assert.ok(geometry.maxGripError<.01);assert.ok(geometry.maxStretch>1.5);assert.ok(geometry.minArea>0);assert.ok(geometry.maxIndependentSpread>.15);assert.ok(geometry.residualSwing>.05);
  const canvas=frame.locator('#pet');await canvas.scrollIntoViewIfNeeded();
  const box=await canvas.boundingBox();const screen=(x,y)=>({x:box.x+x/1200*box.width,y:box.y+y/675*box.height});
  let p=screen(20,20);await page.mouse.click(p.x,p.y);assert.equal(await frame.evaluate(()=>softDragStudy.state.cat.grab),null);
  let q=await frame.evaluate(()=>softDragStudy.state.cat.partPoint('head',.6,.3));p=screen(...q);
  await page.mouse.move(p.x,p.y);await page.mouse.down();assert.ok(await frame.evaluate(()=>!!softDragStudy.state.cat.grab));
  p=screen(740,200);await page.mouse.move(p.x,p.y,{steps:18});await page.waitForTimeout(350);
  await page.screenshot({path:path.join(out,'held.png')});await page.mouse.up();
  await frame.waitForFunction(()=>softDragStudy.state.cat.grounded);await page.waitForTimeout(750);
  await page.screenshot({path:path.join(out,'landed.png')});
  for(const breed of ['calico','blue']){
   await frame.selectOption('#breed',breed);await frame.locator('#demo').click();await page.waitForTimeout(2500);
   assert.ok(await frame.evaluate(()=>!!softDragStudy.state.cat.grab));
   await page.screenshot({path:path.join(out,breed+'.png')});await frame.locator('#reset').click();
   assert.equal(await frame.evaluate(()=>softDragStudy.state.cat.grab),null);
  }
  await frame.locator('[data-soft="100"]').click();assert.equal(await frame.evaluate(()=>softDragStudy.state.cat.soft),1);
  await frame.locator('#mesh').check();
  q=await frame.evaluate(()=>softDragStudy.state.cat.partPoint('head',.6,.3));p=screen(...q);await page.mouse.move(p.x,p.y);await page.mouse.down();
  await canvas.dispatchEvent('pointercancel',{pointerId:1});await page.mouse.up();assert.equal(await frame.evaluate(()=>softDragStudy.state.cat.grab),null);
  await frame.locator('#reset').click();await frame.selectOption('#breed','orange');
  assert.deepEqual(errors,[]);
  const result={previewOnly:true,geometry,pointer:'passed',landing:'passed',resetAndCancel:'passed',breeds:3,errors};
  fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify(result));
 }finally{await browser.close();}
})();
