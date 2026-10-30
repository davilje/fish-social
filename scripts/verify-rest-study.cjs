// Preview experiment only: no runtime assets or contracts are rewritten.
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const {pathToFileURL}=require('node:url');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
(async()=>{
  const root=path.resolve(__dirname,'..'),out=path.join(root,'output/rest-study-2026-10-30');
  fs.mkdirSync(out,{recursive:true});
  const browser=await chromium.launch({headless:true,...(process.env.CHROME_PATH?{executablePath:process.env.CHROME_PATH}:{})});
  try{
    const page=await browser.newPage({viewport:{width:1400,height:1500}}),errors=[];
    page.on('pageerror',e=>errors.push(e.message));
    await page.goto(pathToFileURL(path.join(root,'docs/美术/钓鱼动作/index.html')).href+'?clip=fishing');
    await page.evaluate(()=>{playing=false;});
    const math=await page.evaluate(()=>{
      const current=config(),sample=current.samples[0],r=restStudy;
      let footError=0,minJacobian=Infinity,maxNod=0,minNod=Infinity;
      asleep=true;restFrom=1;restChangedAt=-10000;
      for(let t=0;t<=6400;t+=100){
        elapsed=t;const pose=restingPose(current,sample);
        const foot=r.transform(pose.matrix,r.point(264,450,pose.nod));
        footError=Math.max(footError,Math.hypot(foot[0]-264,foot[1]-450));
        maxNod=Math.max(maxNod,pose.nod);minNod=Math.min(minNod,pose.nod);
      }
      for(let y=0;y<=510;y+=10)for(let x=0;x<=630;x+=10){
        const p=r.point(x,y,1.25),a=r.point(x+.1,y,1.25),b=r.point(x,y+.1,1.25);
        minJacobian=Math.min(minJacobian,((a[0]-p[0])*(b[1]-p[1])-(a[1]-p[1])*(b[0]-p[0]))/.01);
      }
      asleep=false;restFrom=0;elapsed=800;
      const stronger=restingPose(current,sample),legacy=restingPose(current,sample,true);
      const gain=(stronger.matrix[3]-1)/(legacy.matrix[3]-1);
      const before=stronger.matrix;elapsed+=3200*1000;const after=restingPose(current,sample).matrix;
      return {footError,minJacobian,maxNod,minNod,gain,drift:Math.max(...before.map((v,i)=>Math.abs(v-after[i]))),loop:r.nod(0)===r.nod(6400)};
    });
    assert.ok(math.footError<1e-8);assert.ok(math.minJacobian>.2);assert.equal(math.maxNod,1);assert.equal(math.minNod,0);
    assert.ok(math.gain>3.5);assert.ok(math.drift<1e-7);assert.ok(math.loop);
    const pets=await page.locator('#pet option').evaluateAll(options=>options.map(o=>o.value));
    let renders=0;
    for(const pet of pets){
      await page.selectOption('#pet',pet);
      for(const time of [0,900,1900,2700,3300,3900,4400,5200,6400]){
        await page.evaluate(async t=>{asleep=true;restFrom=1;restChangedAt=-10000;elapsed=t;await render();},time);renders++;
      }
    }
    await page.selectOption('#pet','orange');
    await page.evaluate(async()=>{asleep=false;restFrom=0;elapsed=800;await render();});
    await page.screenshot({path:path.join(out,'breathing.png')});
    const shots=[];
    for(const time of [900,2700,3900,5200]){
      await page.evaluate(async t=>{asleep=true;restFrom=1;restChangedAt=-10000;elapsed=t;await render();},time);
      shots.push((await page.locator('#after').screenshot()).toString('base64'));
    }
    await page.evaluate(async()=>{asleep=true;restFrom=1;restChangedAt=-10000;elapsed=2700;await render();});
    await page.screenshot({path:path.join(out,'sleep.png')});
    await page.locator('#scrub').fill('3900');await page.locator('#scrub').dispatchEvent('input');
    assert.equal(await page.evaluate(()=>sleepBlend()),1);
    await page.click('#wake');await page.evaluate(async()=>{elapsed+=800;await render();});
    assert.equal(await page.evaluate(()=>restingPose(config(),config().samples[0]).nod),0);
    assert.deepEqual(errors,[]);
    fs.writeFileSync(path.join(out,'poses.html'),`<!doctype html><meta charset="utf-8"><title>瞌睡阶段对照</title><style>body{background:#f6efe4;color:#624631;font:18px system-ui}main{display:flex}article{width:25%}img{width:100%}</style><h1>瞌睡：缓慢低头 → 低头停顿 → 抬头 → 恢复</h1><main>${shots.map((s,i)=>`<article><p>${['开始低头','低头停顿','抬头','恢复'][i]}</p><img src="data:image/png;base64,${s}"></article>`).join('')}</main>`);
    const report={previewOnly:true,pets,renders,math,errors,wakeAndScrub:'passed'};
    fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
  }finally{await browser.close();}
})();
