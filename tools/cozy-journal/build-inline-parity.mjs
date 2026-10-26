// Export the exact miniFish() SVG from the original interactive prototype.
import fs from 'node:fs';
import crypto from 'node:crypto';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
let sharp;try{sharp=require('sharp')}catch{sharp=require('/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp')}
const app=fs.readFileSync('docs/设计/界面/原型/手账/app.js','utf8');
const svg=app.match(/function miniFish\(\)\s*\{\s*return `([^`]+)`;/)?.[1];
if(!svg)throw new Error('Original miniFish SVG missing');
const source='tools/cozy-journal/inline-mini-fish.svg';fs.writeFileSync(source,svg+'\n');
const base='fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/journal/';
const file=base+'motion/inline-mini-fish.png';
await sharp(Buffer.from(svg.replace('<svg ','<svg xmlns="http://www.w3.org/2000/svg" '))).resize(60,44).png().toFile(file);
if(!fs.existsSync(file+'.meta')){
 const template=fs.readFileSync(base+'motion/bobber.png.meta','utf8');
 fs.writeFileSync(file+'.meta',template.replace(/guid: [a-f0-9]+/,`guid: ${crypto.randomBytes(16).toString('hex')}`));
}
const guid=fs.readFileSync(file+'.meta','utf8').match(/^guid: (\w+)/m)[1];
const manifest=JSON.parse(fs.readFileSync(base+'ui-manifest.json','utf8'));
const item={id:'inline-mini-fish',name:'motion/inline-mini-fish',relativePath:'journal/motion/inline-mini-fish.png',resourcesPath:'Desktop/Theme/Cozy/journal/motion/inline-mini-fish',width:60,height:44,pixelsPerUnit:200,border:[0,0,0,0],pivot:[.5,.5],wrapMode:'Clamp',imageType:'Simple',contentInsets:[0,0,0,0],source,guid,status:'reference-aligned',notes:'Exact miniFish() path from original app.js; 30 x 22 logical pixels.'};
const index=manifest.assets.findIndex(x=>x.name===item.name);if(index<0)manifest.assets.push(item);else manifest.assets[index]=item;
fs.writeFileSync(base+'ui-manifest.json',JSON.stringify(manifest,null,2)+'\n');
