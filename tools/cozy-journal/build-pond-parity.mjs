// Direct SVG equivalents of original style.css .seg/.buoy .label; no reference renders.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
const require=createRequire('/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/package.json');
const sharp=require('sharp');
const base='fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/journal/';
const specs=[
 ['pond-segment-track',326,48,16,'#f3e6cf',false],
 ['pond-segment-empty',157,40,12,'none',false],
 ['pond-segment-selected',157,43,12,'#5dade2',true],
 ['pond-marker-label',160,28,10,'#fffdf7',false],
 ['pond-access-badge',96,28,8,'#fff9ec',false],
 ['pond-map-clip',690,420,4,'#ffffff',false]
];
const manifest=JSON.parse(fs.readFileSync(base+'ui-manifest.json','utf8'));
const template=fs.readFileSync(base+'surfaces/category-chip.png.meta','utf8');
for(const [name,w,h,r,color,shadow] of specs){
 if(process.argv[2] && name!==process.argv[2])continue;
 const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="${w*2}" height="${h*2}" viewBox="0 0 ${w} ${h}">${shadow?`<rect y="3" width="${w}" height="40" rx="${r}" fill="#3f8fc4"/>`:''}<rect width="${w}" height="${shadow?40:h}" rx="${r}" fill="${color}"/></svg>`;
 fs.writeFileSync(`tools/cozy-journal/${name}.svg`,svg+'\n');
 const file=base+'surfaces/'+name+'.png';await sharp(Buffer.from(svg)).png().toFile(file);
 if(!fs.existsSync(file+'.meta'))fs.writeFileSync(file+'.meta',template.replace(/guid: [a-f0-9]+/,`guid: ${crypto.randomBytes(16).toString('hex')}`).replace(/spriteBorder: .*/,`spriteBorder: {x: ${r*2}, y: ${r*2}, z: ${r*2}, w: ${r*2}}`));
 const guid=fs.readFileSync(file+'.meta','utf8').match(/^guid: (\w+)/m)[1];
 const item={source:`tools/cozy-journal/${name}.svg`,guid,status:'reference-aligned',id:'pond-parity-'+name,name:'surfaces/'+name,relativePath:'journal/surfaces/'+name+'.png',resourcesPath:'Desktop/Theme/Cozy/journal/surfaces/'+name,width:w*2,height:h*2,pixelsPerUnit:200,border:[r*2,r*2,r*2,r*2],pivot:[.5,.5],wrapMode:'Clamp',imageType:'Sliced',contentInsets:[0,0,0,0]};
 const index=manifest.assets.findIndex(x=>x.name===item.name);if(index>=0)manifest.assets[index]=item;else manifest.assets.push(item);
}
fs.writeFileSync(base+'ui-manifest.json',JSON.stringify(manifest,null,2)+'\n');
