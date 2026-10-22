// Export the approved podium backgrounds as individual, editable production assets.
// This only updates these six entries; it never regenerates unrelated approved art.
const fs = require('node:fs'), path = require('node:path'), crypto = require('node:crypto');
let chromium; try { ({chromium}=require('playwright')); } catch { ({chromium}=require('/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright')); }
const root = path.resolve(__dirname, '../..');
const theme = path.join(root, 'fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/journal');
const manifestPath = path.join(theme, 'ui-manifest.json');
const manifest = JSON.parse(fs.readFileSync(manifestPath));
const guid = name => crypto.createHash('md5').update('cozy-journal-rank:' + name).digest('hex');
(async () => {
  const browser = await chromium.launch({executablePath:'/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',headless:true});
  try {
    const page = await browser.newPage({viewport:{width:600,height:600},deviceScaleFactor:2});
    await page.goto('http://127.0.0.1:8769/docs/design/ui/prototype/cozy-journal/index.html');
    const css = await page.locator('body').evaluate(() => {
      const sheet = [...document.styleSheets].find(s=>s.href?.endsWith('/pages.css'));
      return [...sheet.cssRules].filter(r=>r.selectorText?.includes('j-podium')).map(r=>r.cssText).join('\n');
    });
    for (let rank=1;rank<=3;rank++) for (const badge of [false,true]) {
      const name = (badge?'decor/rank-label-':'surfaces/podium-place-') + rank;
      const w=badge?69:208,h=badge?26:rank===1?192:165,pad=badge?0:8;
      const border=Array(4).fill(badge?16:48);
      const html=`<!doctype html><meta charset="utf-8"><style>*{box-sizing:border-box}html,body{margin:0;background:transparent}${css}
        #host{position:relative;width:${w+pad*2}px;height:${h+pad*2}px}
        #art{position:absolute;left:${pad}px;top:${pad}px;width:${w}px;height:${h}px;transform:none;translate:none;rotate:none;margin:0;padding:0;color:transparent;${badge?'border-radius:8px;':'border-radius:16px;border:0;box-shadow:0 0 0 5px #fff,0 0 0 7px rgba(117,71,46,.07);'}}
        </style><div id="host" class="place-${rank}"><div id="art" class="${badge?'j-podium-label':'j-podium-card place-'+rank}"></div></div>`;
      const source=path.join(root,'docs/art/cozy-journal/sources/css',name.replaceAll('/','-')+'.html');
      fs.writeFileSync(source,html);
      await page.setContent(html);
      const png=path.join(theme,name+'.png');
      await page.screenshot({path:png,omitBackground:true,clip:{x:0,y:0,width:w+pad*2,height:h+pad*2}});
      const metaTemplate=fs.readFileSync(path.join(theme,'surfaces/podium-plate.png.meta'),'utf8');
      const assetGuid=guid(name);
      fs.writeFileSync(png+'.meta',metaTemplate.replace(/^guid: .*$/m,'guid: '+assetGuid).replace(/spriteBorder: \{.*\}/,`spriteBorder: {x: ${border[0]}, y: ${border[1]}, z: ${border[2]}, w: ${border[3]}}`).replace(/spriteID: \w+/,'spriteID: '+guid(name+':sprite')));
      const entry={id:'RANK-'+rank+(badge?'-LABEL':'-PLATE'),name,relativePath:'journal/'+name+'.png',resourcesPath:'Desktop/Theme/Cozy/journal/'+name,width:(w+pad*2)*2,height:(h+pad*2)*2,pixelsPerUnit:200,border,pivot:[.5,.5],wrapMode:'Clamp',imageType:'Sliced',source:path.relative(root,source),guid:assetGuid,status:'reference-aligned',contentInsets:[pad,pad,pad,pad],contentSize:[w,h],visualBasis:{source:'docs/design/ui/prototype/cozy-journal/pages.css',selector:badge?'.place-'+rank+' .j-podium-label':'.j-podium-card.place-'+rank,method:'Original CSS rendered at 2x'},notes:'Keep the exact exported logical size so the sliced vertical gradient retains its original distribution. Hover/selection keeps the same surface.'};
      const i=manifest.assets.findIndex(a=>a.name===name);if(i<0)manifest.assets.push(entry);else manifest.assets[i]=entry;
    }
    manifest.refinement.aligned=manifest.assets.filter(a=>a.status==='reference-aligned').length;
    fs.writeFileSync(manifestPath,JSON.stringify(manifest,null,2));
    console.log('Exported 3 podium gradients and 3 rank labels from approved CSS.');
  } finally {await browser.close();}
})().catch(e=>{console.error(e);process.exit(1)});
