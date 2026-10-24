/** Package the user-approved raster artwork; no new artwork is drawn here. */
'use strict';
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'../..'),doc=path.join(root,'docs/art/water-effects-22h');
const sourceFile='magic-v4/water-soft-atlas.png',source=path.join(doc,sourceFile);
const out=path.join(root,'desktop-overlay/OverlayResources/effects/fishing/v4');
const mirror=path.join(root,'fish-social-unity/Assets/StreamingAssets/Effects/Fishing/v4');
const nativeMirror=path.join(root,'fish-social-unity/FishSocialOverlay/OverlayResources/effects/fishing/v4');
const sha=x=>crypto.createHash('sha256').update(x).digest('hex');
const write=(p,x)=>{fs.mkdirSync(path.dirname(p),{recursive:true});fs.writeFileSync(p,x);};
const json=x=>JSON.stringify(x,null,2)+'\n';

// Technical sprite extraction: retain the painted silhouettes, remove isolated
// atlas-neighbour fragments, and preserve the nearby antialiased edge pixels.
function components(data,width,height,rect){
 const {left,top,width:w,height:h}=rect,seen=new Uint8Array(w*h),found=[];
 const alpha=p=>data[((top+Math.floor(p/w))*width+left+p%w)*4+3];
 for(let start=0;start<w*h;start++){
  if(seen[start]||alpha(start)<32)continue;
  const pixels=[start];seen[start]=1;let l=w,t=h,r=0,b=0;
  for(let i=0;i<pixels.length;i++){
   const p=pixels[i],x=p%w,y=Math.floor(p/w);l=Math.min(l,x);r=Math.max(r,x);t=Math.min(t,y);b=Math.max(b,y);
   for(let yy=Math.max(0,y-1);yy<=Math.min(h-1,y+1);yy++)for(let xx=Math.max(0,x-1);xx<=Math.min(w-1,x+1);xx++){
    const n=yy*w+xx;if(!seen[n]&&alpha(n)>=32){seen[n]=1;pixels.push(n);}
   }
  }
  if(pixels.length>=32)found.push({pixels,bounds:[l,t,r+1,b+1]});
 }
 return found.sort((a,b)=>b.pixels.length-a.pixels.length);
}
function extract(data,width,height,rect,selected){
 const {left,top,width:w,height:h}=rect,mask=new Uint8Array(w*h),rgba=Buffer.alloc(w*h*4);
 for(const c of selected)for(const p of c.pixels){const x=p%w,y=Math.floor(p/w);for(let yy=Math.max(0,y-2);yy<=Math.min(h-1,y+2);yy++)for(let xx=Math.max(0,x-2);xx<=Math.min(w-1,x+2);xx++)mask[yy*w+xx]=1;}
 let l=w,t=h,r=-1,b=-1;
 for(let p=0;p<mask.length;p++)if(mask[p]){
  const x=p%w,y=Math.floor(p/w),offset=((top+y)*width+left+x)*4;
  if(data[offset+3]<=8)continue;
  data.copy(rgba,p*4,offset,offset+4);l=Math.min(l,x);r=Math.max(r,x);t=Math.min(t,y);b=Math.max(b,y);
 }
 if(r<l)throw Error('Empty sprite extraction');
 return {rgba,width:w,height:h,crop:{left:l,top:t,width:r-l+1,height:b-t+1},sourceRect:rect,
  components:selected.map(c=>({pixels:c.pixels.length,bounds:c.bounds}))};
}
async function main(){
 const bytes=fs.readFileSync(source),sourceSha256=sha(bytes),{data,info}=await sharp(bytes).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const previous=JSON.parse(fs.readFileSync(path.join(root,'desktop-overlay/OverlayResources/effects/fishing/v2/effects.json')));
 const effects=JSON.parse(JSON.stringify(previous));effects.artVersion='22h-water-v4-approved-style';
 effects.behaviorRevision='22h-contact-rules-2026-10-24';
 effects.withdrawn=['FX06','FX07'];
 for(const c of effects.cues){
  c.renderKind=['FX02','FX03','FX04'].includes(c.id)?'ripple':'splash';
  if(c.id==='FX08')c.spawnCount=4;
  if(c.id==='FX05')c.allowedPhases=['hooked','effort','resolving','stopping'];
  if(['FX01','FX08'].includes(c.id)){c.requiresSourceMarker=true;c.requiresContact=true;}
 }
 effects.cues.push({id:'FX09',cue:'retrieve.contact',anchor:'retrieve.waterline-intersection',sizeTier:'object',durationMs:300,spawnCount:3,priority:65,reducedVariant:'arcs',allowedPhases:['resolving','stopping'],cooldownMs:0,requiresSourceMarker:true,requiresContact:true,renderKind:'splash'});
 for(const tier of Object.values(effects.tiers)){tier.dropCount=2;tier.spawnCount=tier.dropCount+tier.arcCount+tier.crownCount;}
 const assets=[];
 function cell(row,col){const left=Math.round(col*info.width/4),top=Math.round(row*info.height/4);return {left,top,width:Math.round((col+1)*info.width/4)-left,height:Math.round((row+1)*info.height/4)-top};}
 async function save(id,cut,kind,sizeTier,variant,paintWidth,paintHeight){
  const width=kind==='drop'?64:128;
  const sprite=await sharp(cut.rgba,{raw:{width:cut.width,height:cut.height,channels:4}}).extract(cut.crop).resize(paintWidth,paintHeight,{fit:'fill',kernel:'lanczos3'}).png().toBuffer();
  const left=Math.round((width-paintWidth)/2),top=kind==='splash'?46-paintHeight:kind==='drop'?44-Math.floor(paintHeight/2):44-Math.floor(paintHeight/2);
  const output=await sharp({create:{width,height:64,channels:4,background:{r:0,g:0,b:0,alpha:0}}}).composite([{input:sprite,left,top}]).png({compressionLevel:9}).toBuffer();
  write(path.join(out,id+'.png'),output);
  assets.push({id,file:id+'.png',kind,...(sizeTier?{sizeTier,variant}:{}),layer:kind==='drop'?'front':'back',width,height:64,anchor:[width/2,44],decodedBytes:width*64*4,sha256:sha(output),sourceFile,sourceSha256,sourceRect:cut.sourceRect,sourceBounds:cut.crop,retainedComponents:cut.components,targetPaintSize:[paintWidth,paintHeight]});
 }
 for(const [row,tier]of Object.keys(effects.tiers).entries())for(const [col,kind,variant]of [[0,'splash','a'],[1,'splash','b'],[2,'ripple','a'],[3,'ripple','b']]){
  const rect=cell(row,col),all=components(data,info.width,info.height,rect);
  const selected=all.filter((c,i)=>i===0||c.pixels.length>=Math.max(50,all[0].pixels.length*.003)&&
   (c.bounds[0]+c.bounds[2])/2>rect.width*.04&&(c.bounds[0]+c.bounds[2])/2<rect.width*.96);
  const cut=extract(data,info.width,info.height,rect,selected),t=effects.tiers[tier];
  await save(`${kind}-${tier.toLowerCase()}-${variant}`,cut,kind,tier,variant,t.width,kind==='splash'?t.height:Math.round(t.width*.2));
 }
 const rect=cell(0,1),droplets=components(data,info.width,info.height,rect).slice(1);
 if(droplets.length<2)throw Error('Approved artwork must supply two isolated droplets');
 for(const [index,id,w,h]of [[0,'drop-round',8,8],[1,'drop-short',9,6]])await save(id,extract(data,info.width,info.height,rect,[droplets[index]]),'drop',null,null,w,h);
 effects.assets=assets.map(({id,file,kind,sizeTier,variant,layer,anchor,width,height})=>({id,file,kind,...(sizeTier?{sizeTier,variant}:{}),layer,anchor,width,height}));
 write(path.join(out,'effects.json'),json(effects));
 const manifest={schemaVersion:2,version:effects.artVersion,status:'style-approved-runtime-acceptance-pending',styleApproval:{date:'2026-10-24',userStatement:'该风格可以',scope:'Simplified v4 water-magic visual style; Windows acceptance remains separate.'},
  source:'Built-in OpenAI image_gen edit of v3 using the actual cat-game preview as style reference; original generated alpha retained in source atlas.',license:'AI-generated project artwork; not covered by the v2 SVG CC0 declaration.',sourceScript:'scripts/art/build-water-effects-22h-v4.cjs',prompt:'docs/art/water-effects-22h/magic-v4/prompt.txt',sourceFile,sourceSha256,
  processing:'Connected-component sprite extraction and removal of unrelated neighbouring fragments; preserve antialias edges; resize to authored tier dimensions and pad to 8px minimum transparent margin.',
  decodedBytes:assets.reduce((n,a)=>n+a.decodedBytes,0),effectsSha256:sha(fs.readFileSync(path.join(out,'effects.json'))),assets};
 write(path.join(out,'manifest.json'),json(manifest));
 function meta(p,folder=false){const guid=sha('fish-social-22h:'+path.relative(root,p).split(path.sep).join('/')).slice(0,32);write(p+'.meta',`fileFormatVersion: 2\nguid: ${guid}\n${folder?'folderAsset: yes\n':''}DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);}
 for(const target of [mirror,nativeMirror]){fs.mkdirSync(target,{recursive:true});for(const file of fs.readdirSync(out))fs.copyFileSync(path.join(out,file),path.join(target,file));}
 meta(mirror,true);for(const file of fs.readdirSync(out))meta(path.join(mirror,file));
 const pets={},fishing=path.join(root,'desktop-overlay/OverlayResources/fishing');for(const dir of fs.readdirSync(fishing)){const file=path.join(fishing,dir,'manifest.json');if(fs.existsSync(file))pets[dir]=JSON.parse(fs.readFileSync(file));}
 const waterSource=JSON.parse(fs.readFileSync(path.join(fishing,'source-water-markers.json'))),waterContact=JSON.parse(fs.readFileSync(path.join(fishing,'water-contact.json'))),motion=JSON.parse(fs.readFileSync(path.join(fishing,'motion-samples.json'))).Clips;
 for(const target of ['fish-social-unity/Assets/StreamingAssets/Fishing','fish-social-unity/FishSocialOverlay/OverlayResources/fishing'])fs.copyFileSync(path.join(fishing,'source-water-markers.json'),path.join(root,target,'source-water-markers.json'));
 write(path.join(doc,'preview-data.js'),'window.waterPreviewData = '+json({effects,legacyEffects:previous,pets,waterSource,waterContact,motion:Object.fromEntries(['cast','groundbait','reel','reel_empty','bite','effort','fishing'].map(c=>[c,motion[c]]))})+';\n');
 console.log(json({assets:assets.length,decodedBytes:manifest.decodedBytes,style:manifest.version,mirrors:2}));
}
main().catch(e=>{console.error(e);process.exitCode=1;});
