const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'playwright');
(async()=>{
  const root=path.resolve(__dirname,'..'),base=process.env.PREVIEW_BASE||'http://127.0.0.1:8769';
  const output=path.join(root,'output/rest-applied-2026-10-30');fs.mkdirSync(output,{recursive:true});
  const browser=await chromium.launch({headless:true,...(process.env.CHROME_PATH?{executablePath:process.env.CHROME_PATH}:{})});
  try{
    const page=await browser.newPage({viewport:{width:1600,height:1200}}),errors=[];
    page.on('pageerror',e=>errors.push(e.message));
    await page.goto(base+'/preview-current.html#rest');
    await page.waitForURL('**/*#rest');
    const stage=page.frameLocator('#stage');
    await stage.locator('#clip').waitFor();
    assert.equal(await stage.locator('#clip').inputValue(),'fishing');
    assert.match(await page.locator('button[aria-current]').textContent(),/呼吸与瞌睡/);
    const sources=await page.locator('button[data-src]').evaluateAll(items=>items.map(i=>i.dataset.src));
    assert.equal(sources.length,new Set(sources).size,'duplicate preview entry');
    for(const source of sources){
      assert.ok(fs.existsSync(path.join(root,decodeURI(source.split(/[?#]/)[0]))),'missing hub content '+source);
    }
    await stage.locator('#sleep').click();
    await page.screenshot({path:path.join(output,'preview-hub.png')});
    await page.reload();await stage.locator('#clip').waitFor();
    assert.equal(await stage.locator('#clip').inputValue(),'fishing');
    await page.locator('button[data-route="fishing"]').click();await stage.locator('#clip').waitFor();
    assert.equal(await stage.locator('#clip').inputValue(),'cast');
    assert.equal(new URL(page.url()).hash,'#fishing');
    await page.locator('button[data-route="rest"]').click();await stage.locator('#clip').waitFor();
    // All old routes remain addressable after generalizing stable hash selection.
    for(const route of ['login','audio','scene']){
      await page.goto(base+'/现行预览.html#'+route);
      await page.waitForFunction(()=>document.querySelector('#stage').getAttribute('src'));
      assert.equal(new URL(page.url()).hash,'#'+route);
    }
    assert.deepEqual(errors,[]);
    const result={entries:sources.length,restRoute:'passed',fishingRoute:'passed',legacyRoutes:'passed',allTargetsExist:true,errors};
    fs.writeFileSync(path.join(output,'hub-verification.json'),JSON.stringify(result,null,2)+'\n');console.log(JSON.stringify(result));
  }finally{await browser.close();}
})();
