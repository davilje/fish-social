import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';

const require=createRequire(import.meta.url);
const sharp=require(process.env.SHARP_MODULE||'sharp');
const root=path.dirname(fileURLToPath(import.meta.url));
const manifest=JSON.parse(fs.readFileSync(path.join(root,'manifest.json'),'utf8'));
const audits=JSON.parse(fs.readFileSync(path.join(root,'export-audit.json'),'utf8')).frames;
assert.equal(manifest.clips.filter(clip=>clip.skinId==='orange').reduce((sum,clip)=>sum+clip.frames.length,0),84);
assert(!manifest.skins.some(skin=>skin.id==='dafeiyu'));
assert.equal(new Set(audits.filter(frame=>!frame.scaleCalibration).map(frame=>frame.scale)).size,1);
for(const master of new Set(audits.map(frame=>frame.master))) assert.equal(new Set(audits.filter(frame=>frame.master===master).map(frame=>frame.scale)).size,1,`Inconsistent sheet scale: ${master}`);
const files=new Set();
fs.mkdirSync(path.join(root,'review'),{recursive:true});
for(const clip of manifest.clips) {
  const thumbs=[];
  if(clip.loop) assert(clip.loop[0]>=0&&clip.loop[1]<=clip.frames.length);
  for(const [index,frame] of clip.frames.entries()) {
    const file=path.join(root,frame.file),metadata=await sharp(file).metadata();
    assert.deepEqual([metadata.width,metadata.height],clip.canvas,frame.file);
    assert(metadata.hasAlpha,frame.file);
    const {data,info}=await sharp(file).raw().ensureAlpha().toBuffer({resolveWithObject:true});
    let visible=0;
    for(let row=0;row<info.height;row++)for(let column=0;column<info.width;column++) {
      const alpha=data[(row*info.width+column)*4+3];
      if(alpha<8)continue;
      visible++;
      if(clip.skinId==='orange') assert(column>0&&column<info.width-1&&row>0&&row<info.height-1,`Frame touches border: ${frame.file}`);
      if(['food-fill','bowl-food'].includes(clip.action))assert(column<135,`Food overlaps water bowl: ${frame.file}`);
    }
    const allowedBlank=frame.state==='empty'||frame.state==='finished'||clip.action==='food-pour'&&index===11;
    assert(allowedBlank||visible>0,`Unexpected empty frame: ${frame.file}`);
    files.add(frame.file);
    const width=Math.round(clip.canvas[0]/2),height=Math.round(clip.canvas[1]/2);
    thumbs.push({input:await sharp(file).resize(width,height).toBuffer(),left:index%4*width,top:Math.floor(index/4)*height});
  }
  const width=Math.round(clip.canvas[0]/2),height=Math.round(clip.canvas[1]/2);
  await sharp({create:{width:width*4,height:Math.ceil(clip.frames.length/4)*height,channels:4,background:'#fff2df'}}).composite(thumbs).png().toFile(path.join(root,'review',clip.id+'.png'));
}
const summary={result:'passed',clips:manifest.clips.length,uniquePngFiles:files.size,catFrames:84,checks:['RGBA dimensions','cat safe borders','food stays in left half','fixed scale within each calibrated source sheet','loop ranges','no dafeiyu'],artAcceptance:'pending',contactCalibration:'pending'};
fs.writeFileSync(path.join(root,'verification.json'),JSON.stringify(summary,null,2)+'\n');
console.log(JSON.stringify(summary));
