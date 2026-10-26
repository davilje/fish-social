const fs=require('node:fs'),path=require('node:path');let sharp;try{sharp=require('sharp')}catch{sharp=require('/Users/red/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp')}
const root=path.resolve(__dirname,'../..'),dir=path.join(root,'docs/美术/手账/previews');
(async()=>{const metrics=[];for(const name of JSON.parse(fs.readFileSync(path.join(root,'fish-social-unity/Assets/Desktop/JournalSamples/ReferenceAssets/layouts.json'))).pages.map(p=>p.id)){
await sharp(path.join(dir,name+'-unity-2x.png')).resize(1672,941).png().toFile(path.join(dir,name+'-unity.png'));
const a=await sharp(path.join(dir,name+'-reference.png')).removeAlpha().raw().toBuffer(),b=await sharp(path.join(dir,name+'-unity.png')).removeAlpha().raw().toBuffer();let sum=0,count=0;const diff=Buffer.alloc(a.length);
for(let i=0;i<a.length;i++){const d=Math.abs(a[i]-b[i]);sum+=d;diff[i]=Math.min(255,d*4);if(i%3===0&&Math.max(Math.abs(a[i]-b[i]),Math.abs(a[i+1]-b[i+1]),Math.abs(a[i+2]-b[i+2]))>20)count++;}
await sharp(diff,{raw:{width:1672,height:941,channels:3}}).png().toFile(path.join(dir,name+'-diff.png'));
metrics.push({page:name,meanAbsoluteChannelError:sum/a.length,pixelsOver20:count,totalPixels:1672*941,pixelsOver20Percent:100*count/(1672*941),note:'Whole-screen pixel statistics include backgrounds. Not an acceptance score. Diff image is amplified 4×.'});
}fs.writeFileSync(path.join(dir,'comparison-metrics.json'),JSON.stringify(metrics,null,2));console.log(metrics);})();
