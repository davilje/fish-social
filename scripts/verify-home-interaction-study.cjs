const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
(async()=>{
  const root=path.resolve(__dirname,'..'),out=path.join(root,'output/home-interaction-study-2026-10-31');fs.mkdirSync(out,{recursive:true});
  const browser=await chromium.launch({headless:true,...(process.env.CHROME_PATH?{executablePath:process.env.CHROME_PATH}:{})});
  try{
    const page=await browser.newPage({viewport:{width:1550,height:1250}}),errors=[];
    page.on('pageerror',e=>errors.push(e.message));
    await page.goto((process.env.PREVIEW_BASE||'http://127.0.0.1:8769')+'/现行预览.html#home-interaction');
    const child=page.frames().find(f=>f.url().includes('interaction-study'))||await new Promise(resolve=>page.on('framenavigated',f=>{if(f.url().includes('interaction-study'))resolve(f);}));
    await child.waitForFunction(()=>window.homeInteractionStudy?.state.ready);
    assert.match(await page.locator('button[aria-current=true]').textContent(),/猫咪交互/);
    const geometry=await child.evaluate(()=>{
      const s=homeInteractionStudy;let minJacobian=Infinity,maxFootError=0;
      for(const action of ['idle','attention','nuzzle'])for(let t=0;t<=s.actions[action].duration;t+=80){
        const p=s.pose(action,t,t,1);
        const foot=s.warp(256,352,p);maxFootError=Math.max(maxFootError,Math.hypot(foot[0]-256,foot[1]-352));
        for(let y=60;y<352;y+=16)for(let x=140;x<360;x+=16){
          const a=s.warp(x,y,p),b=s.warp(x+.1,y,p),c=s.warp(x,y+.1,p);
          minJacobian=Math.min(minJacobian,((b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]))/.01);
        }
      }
      const original=s.pose('idle',0,0);const last=s.pose('idle',3600000,3600000);
      return {minJacobian,maxFootError,rollDuration:s.rollTimes.reduce((a,b)=>a+b,0),rollFrames:new Set(s.rollTimes.map((_,i)=>i)).size};
    });
    assert.ok(geometry.minJacobian>.35);assert.ok(geometry.maxFootError<1e-8);assert.equal(geometry.rollDuration,4200);
    const rollKeys=await child.evaluate(()=>homeInteractionStudy.rollKeys);
    assert.equal(rollKeys[0],'sit');assert.equal(rollKeys.at(-1),'sit');
    assert.ok(!rollKeys.some(k=>[0,14,15].includes(k)),'upright pose remains in seated sequence');
    await child.evaluate(()=>{homeInteractionStudy.start('idle',true);homeInteractionStudy.seek(0);});
    const seatedPixels=await child.locator('#after').evaluate(c=>c.toDataURL());
    await child.evaluate(()=>{homeInteractionStudy.start('roll',true);homeInteractionStudy.seek(4200);homeInteractionStudy.state.clock=0;homeInteractionStudy.render();});
    assert.equal(await child.locator('#after').evaluate(c=>c.toDataURL()),seatedPixels,'roll does not return to identical seated pose');
    let rendered=0;
    for(const action of ['idle','attention','nuzzle','roll']){
      await child.locator(`[data-action="${action}"]`).click();
      for(const fraction of [0,.2,.5,.8,1]){await child.evaluate(f=>{homeInteractionStudy.seek(homeInteractionStudy.actions[homeInteractionStudy.state.action].duration*f);},fraction);rendered++;}
      await child.evaluate(()=>homeInteractionStudy.seek(homeInteractionStudy.actions[homeInteractionStudy.state.action].duration*.4));
      await page.screenshot({path:path.join(out,action+'.png'),fullPage:true});
    }
    // Real pointer input, including repeated clicks and held head contact.
    await child.locator('[data-action="idle"]').click();
    const canvas=child.locator('#after');await canvas.scrollIntoViewIfNeeded();
    const box=await canvas.boundingBox();const x=box.x+615/1200*box.width,y=box.y+385/675*box.height;
    await page.mouse.click(x,y);
    assert.equal(await child.evaluate(()=>homeInteractionStudy.state.action),'attention');
    await child.evaluate(()=>homeInteractionStudy.seek(800));await page.mouse.click(x,y);
    assert.equal(await child.evaluate(()=>homeInteractionStudy.state.elapsed),800,'rapid click restarted response');
    await child.locator('[data-action="idle"]').click();
    await page.mouse.move(x,y);await page.mouse.down();
    await child.waitForFunction(()=>homeInteractionStudy.state.action==='nuzzle');
    await page.mouse.up();assert.equal(await child.evaluate(()=>homeInteractionStudy.state.action),'nuzzle');
    await child.locator('#compare').check();await child.evaluate(()=>homeInteractionStudy.seek(1100));
    await page.screenshot({path:path.join(out,'comparison.png'),fullPage:true});
    await child.locator('#next').click();assert.equal(await child.evaluate(()=>homeInteractionStudy.state.elapsed),1200);
    await child.locator('#previous').click();assert.equal(await child.evaluate(()=>homeInteractionStudy.state.elapsed),1100);
    await child.locator('#repeat').check();await child.evaluate(()=>{homeInteractionStudy.seek(4190);homeInteractionStudy.start('roll',true);homeInteractionStudy.state.elapsed=4990;});
    await child.waitForFunction(()=>homeInteractionStudy.state.elapsed<500);
    await page.evaluate(()=>location.hash='#rest');
    await page.waitForFunction(()=>document.getElementById('stage').src.includes('clip=fishing'));
    await page.evaluate(()=>location.hash='#home-interaction');
    await page.waitForFunction(()=>document.getElementById('stage').src.includes('interaction-study'));
    assert.deepEqual(errors,[]);
    const result={previewOnly:true,rendered,geometry,clickAndHold:'passed',rapidClick:'does-not-restart',stepAndRepeat:'passed',hubRoutes:'passed',errors};
    fs.writeFileSync(path.join(out,'verification.json'),JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify(result));
  }finally{await browser.close();}
})();
