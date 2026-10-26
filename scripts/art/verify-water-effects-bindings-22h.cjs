/** Source/configuration consistency check. Does not execute WPF or replace policy tests. */
const fs=require('fs'),path=require('path'),assert=require('assert/strict'),crypto=require('crypto');
const art=process.argv.includes('--v2')?'v2':'v4';
const root=path.resolve(__dirname,'../..');
const read=p=>fs.readFileSync(path.join(root,p),'utf8');
const effects=JSON.parse(read(`desktop-overlay/OverlayResources/effects/fishing/${art}/effects.json`));
const policy=read('desktop-overlay/OverlayWaterEffectsPolicy.cs'),renderer=read('desktop-overlay/OverlayWaterEffects.cs');
if(art==='v4'){
 assert(renderer.includes('"fishing", "v4"'),'WPF must load the verified v4 pack');
 assert(read('fish-social-unity/Assets/Scripts/Desktop/Pet/UnityFishingWaterEffects.cs').includes('"Fishing", "v4"'),'Unity must load the verified v4 pack');
 assert(read('desktop-overlay/tests/FishSocialOverlay.WaterEffectsTests.csproj').includes('fishing/v4/effects.json'),'Policy tests must load the active v4 configuration');
}
const allowedSource=policy.slice(policy.indexOf('public static bool Allowed('),policy.indexOf('public static bool Ending('));
assert(allowedSource.includes('default: return false;'),'Policy must deny unbound cues');
const allowed=new Map();for(const match of allowedSource.matchAll(/((?:case\s+"[^"]+"\s*:\s*)+)return\s+([^;]+);/g)){
 const phases=[...match[2].matchAll(/phase\s*==\s*"([^"]+)"/g)].map(m=>m[1]);
 for(const marker of match[1].matchAll(/case\s+"([^"]+)"/g))allowed.set(marker[1],phases);
}
assert(allowed.size>0,'Could not parse actual policy switch');
const lookup=renderer.match(/foreach\s*\(var item in pack\.Manifest\.Cues\)([\s\S]*?)if\s*\(config == null/);
assert(lookup,'Renderer must find cue in actual manifest');
assert(/item\.Cue\s*==\s*cue/.test(lookup[1]),'Renderer must use configured primary cue');
const markersAccepted=/item\.Markers/.test(lookup[1])&&/cue/.test(lookup[1]);
assert(renderer.includes('"effects.json"'),'Renderer must load production configuration');
assert(renderer.includes('config.AllowedPhases'),'Renderer must enforce configured phase whitelist');
assert(/canvasWidth\s*=\s*particle.Kind\s*==\s*"drop"\s*\?\s*64\s*:\s*128/.test(renderer),'Renderer must draw 64px drops independently');
assert(renderer.includes('canvasAnchorX = canvasWidth / 2')&&renderer.includes('particle.Scale.CenterY = 44 * scale'),'Renderer pivots must match resource metadata');
assert(renderer.includes('anchor.Y - top * scale')&&renderer.includes('particle.Kind == "ripple" && pack.OpaqueTop'),'Ripple images must compensate transparent padding to put their visible upper edge at the waterline');
assert(renderer.includes('_surfaceY - Canvas.GetTop(p.Image)')&&renderer.includes('p.ComponentClip.Rect'),'Ripple clip must be inverted through the actual animation transform');
for(const asset of effects.assets){assert.equal(asset.width,asset.kind==='drop'?64:128);assert.equal(asset.height,64);assert.deepEqual(asset.anchor,[asset.width/2,44]);}
const checks=[];
for(const cue of effects.cues){for(const marker of new Set([cue.cue,...cue.markers||[]])){
 assert(allowed.has(marker),`${cue.id} ${marker} is absent from actual policy`);
 const policyPhases=[...allowed.get(marker)].sort();
 if(marker==='fish.slap'&&art==='v4')assert.deepEqual(policyPhases,['effort','hooked']);
 else assert.deepEqual(policyPhases,[...cue.allowedPhases].sort(),`${cue.id} ${marker} phase/config drift`);
 assert(policyPhases.every(phase=>cue.allowedPhases.includes(phase)),'configuration must include every actual policy phase');
 if(marker!==cue.cue)assert(markersAccepted,`${cue.id} alias ${marker} has no renderer config lookup`);
 checks.push({id:cue.id,marker,policyPhases:allowed.get(marker),rendererLookup:marker===cue.cue?'Cue':'Markers alias',result:'passed'});
}}
for(const id of (art==='v4'?['FX06','FX07']:['FX06','FX07','FX09']))assert(!effects.cues.some(c=>c.id===id),id+' must be withdrawn');
for(const marker of ['retrieve.start','result.presented','catch.confirm','fish.escape','reel.start'])assert(!allowed.has(marker)&&!effects.cues.some(c=>c.cue===marker||(c.markers||[]).includes(marker)),marker+' must have no H binding');
for(const marker of allowed.keys())assert(effects.cues.some(c=>c.cue===marker||(c.markers||[]).includes(marker)),marker+' policy has no configured effect');
const sha=s=>crypto.createHash('sha256').update(s).digest('hex');
const report={artVersion:effects.artVersion,task:'STEAM-DESKTOP-22H',date:'2026-10-24',scope:'Static actual Overlay source/configuration binding consistency; WPF execution and pure policy runtime tests are separate.',policySha256:sha(policy),rendererSha256:sha(renderer),checks,withdrawn:effects.withdrawn,forbiddenMarkersUnbound:true,rendererSizingMatchesAssetMetadata:true,result:'passed'};
fs.writeFileSync(path.join(root,art==='v4'?'docs/美术/水面特效/bindings-verification-v4.json':'docs/美术/水面特效/bindings-verification.json'),JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify(report,null,2));
