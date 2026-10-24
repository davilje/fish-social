/** STEAM-DESKTOP-22H original vector artwork, rasterized deterministically.
 * NODE_PATH may point to the bundled workspace dependency node_modules. */
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('sharp');
const root = path.resolve(__dirname, '../..');
const out = path.join(root, 'desktop-overlay/OverlayResources/effects/fishing/v2');
const mirror = path.join(root, 'fish-social-unity/Assets/StreamingAssets/Effects/Fishing/v2');
const nativeMirror = path.join(root, 'fish-social-unity/FishSocialOverlay/OverlayResources/effects/fishing/v2');
const doc = path.join(root, 'docs/art/water-effects-22h');
const sha = data => crypto.createHash('sha256').update(data).digest('hex');
const write = (file, data) => { fs.mkdirSync(path.dirname(file), {recursive:true}); fs.writeFileSync(file, data); };
const json = data => JSON.stringify(data, null, 2) + '\n';
const tiers = {
 S:{width:24,height:8,durationMs:280,dropCount:2,arcCount:1,crownCount:1,spawnCount:4},
 M:{width:40,height:15,durationMs:360,dropCount:3,arcCount:2,crownCount:1,spawnCount:6},
 L:{width:60,height:24,durationMs:480,dropCount:4,arcCount:2,crownCount:2,spawnCount:8},
 XL:{width:84,height:35,durationMs:620,dropCount:6,arcCount:2,crownCount:2,spawnCount:10}
};
// The empty centre keeps the existing fish visible; broad rounded lobes replace spikes.
const splashes = {
 's-a':'M52 44 Q50 41 53 39 Q56 38 57 41 Q59 44 61 44 M67 44 Q69 41 71 39 Q74 38 76 41 Q78 44 73 45',
 's-b':'M52 44 C49 40 52 37 55 39 C57 40 57 43 61 44 M67 44 C71 44 69 37 73 37 C77 37 77 42 75 44',
 'm-a':'M44 44 C40 39 44 34 48 37 C50 39 53 40 51 34 C49 28 55 27 57 32 C59 35 57 40 61 44 M67 44 C72 42 70 35 73 33 C77 30 81 34 78 38 C76 42 82 38 84 41 Q87 46 76 46',
 'm-b':'M44 44 C40 40 40 35 44 35 C49 34 49 41 54 41 C52 36 56 32 59 36 Q62 40 61 44 M67 44 C70 42 69 32 74 30 C81 28 81 35 77 38 C75 41 84 40 84 44',
 'l-a':'M34 44 C29 40 30 35 35 35 C40 34 40 40 46 40 C41 35 39 27 44 26 C51 24 50 36 55 39 C51 31 54 20 59 21 C64 22 57 36 61 44 M67 44 C73 40 69 29 74 27 C80 25 81 33 77 39 C83 36 83 27 89 29 C94 32 89 39 94 40 Q100 44 91 46',
 'l-b':'M34 44 C29 42 27 36 32 34 C37 32 40 40 46 40 C39 33 41 29 45 28 C51 26 50 38 60 44 M68 44 C71 39 67 22 73 20 C80 18 81 28 77 34 C83 30 84 22 89 25 C95 28 86 37 90 39 C100 34 100 43 94 44',
 'xl-a':'M22 44 C17 41 18 35 23 34 C28 33 30 40 35 39 C27 32 31 25 37 26 C44 27 40 36 48 38 C41 28 42 17 48 17 C56 17 51 33 60 44 M68 44 C75 40 69 16 75 11 C83 7 88 18 81 29 C88 26 87 17 93 18 C103 20 91 33 95 36 C100 29 110 31 108 37 C106 40 102 42 106 44',
 'xl-b':'M22 44 C15 40 15 33 20 31 C27 28 27 38 35 39 C31 32 25 24 31 22 C38 19 40 30 42 33 C40 23 43 14 49 16 C56 19 49 34 60 44 M68 44 C75 39 70 31 74 27 C80 20 84 32 81 36 C88 33 86 17 94 18 C104 19 95 34 97 37 C103 31 112 36 107 42 Q104 46 95 46'
};
const variants = [];
function wrapper(content,width=128) {return `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="64" viewBox="0 0 ${width} 64">${content}</svg>`;}
function ripple(tier, variant) {
 const w=tiers[tier].width, a=w/2, b=w*.165, c=variant==='a'?0:2;
 // Broken ellipses differ in arc direction, spacing and trailing short wakes.
 const d=variant==='a'
 ? `M${64-a} 44 C${64-a-1} ${44-b} ${64+a-2} ${44-b-1} ${64+a} 43 M${64+a-2} 47 C${64+a-5} ${44+b} ${64-a+4} ${44+b+1} ${64-a+1} 46`
 : `M${64-a+2} 46 C${64-a-3} ${44-b-1} ${64+a-6} ${44-b-2} ${64+a-1} 42 M${64-a+5} 49 C${64-a+10} ${44+b+1} ${64+a-6} ${44+b-1} ${64+a} 45`;
 let tail=tier==='S'?'': `<path d="M${64-a+5} ${Math.min(53,48+b+c)} Q${64-a+13} ${Math.min(55,51+b+c)} ${64-a+21} ${Math.min(53,49+b+c)}"/>`;
 if(tier==='XL')tail+=`<path d="M${64+a-22} 53 Q${64+a-9} 55 ${64+a-3} 52"/>`;
 return wrapper(`<g transform="translate(0 ${tier==='XL'?-1:0})" fill="none" stroke-linecap="round" stroke-linejoin="round"><g stroke="#a39280" stroke-opacity=".38" stroke-width="2.1"><path d="${d}"/>${tail}</g><g stroke="#79aeb6" stroke-opacity=".55" stroke-width="1.35"><path d="${d}"/>${tail}</g><path d="M${64-a+4} 43 Q${64} ${44-b+1} ${64+a-5} 42" stroke="#fff5da" stroke-opacity=".5" stroke-width=".8"/></g>`);
}
async function asset(id, svg, extra) {
 const bytes=await sharp(Buffer.from(svg)).png({compressionLevel:9}).toBuffer();
 const width=extra.kind==='drop'?64:128,anchor=[width/2,44];
 write(path.join(out,id+'.png'), bytes); write(path.join(doc,'sources',id+'.svg'),svg+'\n');
 variants.push({id,file:id+'.png',width,height:64,anchor,decodedBytes:width*64*4,sha256:sha(bytes),sourceSha256:sha(svg+'\n'),...extra});
}
async function main(){
 for (const tier of Object.keys(tiers))for(const variant of ['a','b']) {
  const key=tier.toLowerCase()+'-'+variant;
  await asset('ripple-'+key,ripple(tier,variant),{kind:'ripple',sizeTier:tier,variant,layer:'back'});
  const d=splashes[key];
  const highlight=tier==='S'?'M53 41 Q55 39 56 41':tier==='M'?'M46 39 Q48 38 50 40':tier==='L'?'M35 38 Q38 37 41 40':'M22 37 Q26 35 29 39';
  await asset('splash-'+key,wrapper(`<path d="${d}" fill="#8fc2c9" fill-opacity=".46" stroke="#a39280" stroke-opacity=".5" stroke-width="1.1" stroke-linecap="round" stroke-linejoin="round"/><path d="${highlight}" fill="none" stroke="#fff4dc" stroke-opacity=".55" stroke-width="1.5" stroke-linecap="round"/>`),{kind:'splash',sizeTier:tier,variant,layer:'back'});
 }
 for(const [id,d] of Object.entries({'drop-round':'M61 44 C59 40 61 37 64 37 C68 37 69 42 66 44 Q64 46 61 44','drop-short':'M61 42 C58 40 58 37 61 37 C64 37 68 40 68 42 Q65 45 61 42'}))await asset(id,wrapper(`<g transform="translate(32 44) scale(.8) translate(-64 -44)"><path d="${d}" fill="#8fc2c9" fill-opacity=".5" stroke="#a39280" stroke-opacity=".45" stroke-width=".7"/><path d="M61 39 Q62 38 63 39" stroke="#fff4dc" stroke-opacity=".55" stroke-linecap="round" fill="none"/></g>`,64),{kind:'drop',layer:'front'});
 const cues=[
 {id:'FX01',cue:'float.contact',anchor:'bobber.contact',sizeTier:'object',durationMs:320,spawnCount:3,priority:65,reducedVariant:'arcs',allowedPhases:['casting','waiting'],cooldownMs:0},
 {id:'FX02',cue:'waiting.ripple',anchor:'bobber.contact',sizeTier:'object',durationMs:850,spawnCount:1,priority:10,reducedVariant:'none',allowedPhases:['waiting'],cooldownMs:2400},
 {id:'FX03',cue:'bite.onset',anchor:'fish.contact',sizeTier:'shared',durationMs:0,spawnCount:0,priority:100,reducedVariant:'arcs',allowedPhases:['hooked','effort'],cooldownMs:0},
 {id:'FX04',cue:'fish.struggle',anchor:'fish.contact',sizeTier:'shared',durationMs:0,spawnCount:0,priority:60,reducedVariant:'none',allowedPhases:['hooked','effort'],cooldownMs:700},
 {id:'FX05',cue:'fish.surface',markers:['fish.surface','fish.slap'],anchor:'fish.contact',sizeTier:'shared',durationMs:0,spawnCount:0,priority:90,reducedVariant:'arcs',allowedPhases:['hooked','effort'],cooldownMs:0,requiresSourceMarker:true,requiresContact:true},
 {id:'FX08',cue:'groundbait.contact',anchor:'bait.contact',sizeTier:'object',durationMs:280,spawnCount:3,priority:50,reducedVariant:'none',allowedPhases:['groundbaiting'],cooldownMs:720}
 ];
 const effects={schemaVersion:2,artVersion:'22h-water-v2.1',logicalCanvas:[128,64],anchor:[64,44],logicalSlot:128,variantSelection:'stable-event-key-plus-occurrence-parity',sizeThresholdsMeters:[.3,.8,1.5],visibleBodyThresholdsPixels:[16,32,56],unknownSizeTier:'S',objectSizeTier:'S',tiers,cues,budget:{instances:24,groups:8,groupsPerActor:2,selfReservedGroups:2,selfReservedInstances:12,maxDecodedBytes:4194304},reduced:{durationMs:200,arcCount:2,dropCount:0},endedFadeMs:100,withdrawn:['FX06','FX07','FX09'],assets:variants.map(({id,file,kind,sizeTier,variant,layer,anchor,width,height})=>({id,file,kind,sizeTier,variant,layer,anchor,width,height}))};
 write(path.join(out,'effects.json'),json(effects));
 const manifest={schemaVersion:2,version:'22h-water-v2.1',status:'production-candidate-awaiting-human-visual-review',source:'Original code-native SVG artwork authored for Fish Social; no external image inputs.',license:'CC0-1.0',sourceScript:'scripts/art/build-water-effects-22h.cjs',reference:'Current approved fishing v19 cat manifests, unchanged.',decodedBytes:variants.reduce((a,b)=>a+b.decodedBytes,0),effectsSha256:sha(fs.readFileSync(path.join(out,'effects.json'))),assets:variants};
 write(path.join(out,'manifest.json'),json(manifest));
 for(const target of [mirror,nativeMirror]){fs.mkdirSync(target,{recursive:true});for(const file of fs.readdirSync(out))fs.copyFileSync(path.join(out,file),path.join(target,file));}
 // StreamingAssets are raw files. Keep deterministic Unity GUIDs for reproducible imports.
 const unityRoot=path.join(root,'fish-social-unity/Assets/StreamingAssets');
 function meta(file,folder=false){const relative=path.relative(root,file).split(path.sep).join('/'),guid=sha('fish-social-22h:'+relative).slice(0,32);write(file+'.meta',`fileFormatVersion: 2\nguid: ${guid}\n${folder?'folderAsset: yes\n':''}DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);}
 for(const dir of ['Effects','Effects/Fishing','Effects/Fishing/v2'])meta(path.join(unityRoot,dir),true);
 for(const file of fs.readdirSync(out))meta(path.join(mirror,file));
 const sourceMarkers=path.join(root,'desktop-overlay/OverlayResources/fishing/source-water-markers.json');
 if(fs.existsSync(sourceMarkers)){
  const streamingMarker=path.join(unityRoot,'Fishing/source-water-markers.json');
  fs.copyFileSync(sourceMarkers,streamingMarker);meta(streamingMarker);
  fs.copyFileSync(sourceMarkers,path.join(root,'fish-social-unity/FishSocialOverlay/OverlayResources/fishing/source-water-markers.json'));
 }
 const fishing=path.join(root,'desktop-overlay/OverlayResources/fishing');
 const pets={};for(const dir of fs.readdirSync(fishing)){const file=path.join(fishing,dir,'manifest.json');if(fs.existsSync(file))pets[dir]=JSON.parse(fs.readFileSync(file,'utf8'));}
 const activeV4=path.join(root,'desktop-overlay/OverlayResources/effects/fishing/v4/effects.json');
 const activeEffects=fs.existsSync(activeV4)?JSON.parse(fs.readFileSync(activeV4,'utf8')):effects;
 const waterContact=JSON.parse(fs.readFileSync(path.join(fishing,'water-contact.json'))),motion=JSON.parse(fs.readFileSync(path.join(fishing,'motion-samples.json'))).Clips;
 write(path.join(doc,'preview-data.js'),'window.waterPreviewData = '+json({effects:activeEffects,legacyEffects:effects,pets,waterContact,motion:Object.fromEntries(['cast','groundbait','reel','reel_empty','bite','effort','fishing'].map(c=>[c,motion[c]]))})+';\n');
 console.log(`Built ${variants.length} textures; decoded ${manifest.decodedBytes} bytes; ${Object.keys(pets).length} approved cats; Unity mirror identical.`);
}
main().catch(e=>{console.error(e);process.exitCode=1;});
