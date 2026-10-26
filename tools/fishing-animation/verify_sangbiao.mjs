import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { DEFAULT_AVATARS, isDefaultAvatarPath, resolveCatSkinId } from '../../shared/defaultAvatars.ts';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const art = path.join(root, 'docs/美术/猫咪形象/sangbiao');
const read = file => JSON.parse(fs.readFileSync(file));
const hash = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const unity = path.join(root, 'fish-social-unity/Assets');
const fishing = ['fish-social-unity/Assets/StreamingAssets/Fishing', 'desktop-overlay/OverlayResources/fishing',
  'fish-social-unity/FishSocialOverlay/OverlayResources/fishing'].map(relative => path.join(root, relative));
const runtime = read(path.join(fishing[0], 'sangbiao/manifest.json'));
const original = read(path.join(fishing[0], 'orange/manifest.json'));
assert.equal(runtime.skinId, 'sangbiao');
assert.deepEqual(runtime.canvas, [640, 512]);
assert.deepEqual(runtime.place, original.place);
assert.deepEqual(runtime.clips, original.clips);
assert.deepEqual(runtime.layers, original.layers);
assert.deepEqual(runtime.bodyLayers, original.bodyLayers);
assert.equal(runtime.bodyTextureScale, 1);
const avatarPath = '/image/profile/cat_avatar_sangbiao.png';
const photoPath = '/image/fishing_photos/cat_fishing_sangbiao.png';
assert(isDefaultAvatarPath(avatarPath));
assert.equal(resolveCatSkinId(avatarPath), 'sangbiao');
assert.equal(resolveCatSkinId(photoPath), 'sangbiao');
assert(DEFAULT_AVATARS.some(avatar => avatar.id === 'sangbiao' && avatar.label.includes('丧彪')));
const csharpCatalog = fs.readFileSync(path.join(unity, 'Scripts/Desktop/UI/DesktopDefaultAvatars.cs'), 'utf8');
assert(csharpCatalog.includes('new Entry("sangbiao", "cat_avatar_sangbiao.png", "狸花猫「丧彪」")'));
const csharpArt = fs.readFileSync(path.join(unity, 'Scripts/Desktop/Pet/FishingLayerArt.cs'), 'utf8');
assert(csharpArt.match(/SkinIds\s*=\s*\{[^}]+"sangbiao"/));
let files = 0, slotCount = 0;
for (const destination of fishing) {
  assert.equal(hash(path.join(destination, 'sangbiao/manifest.json')), hash(path.join(fishing[0], 'sangbiao/manifest.json')));
  assert(read(path.join(destination, 'inventory.json')).skins.some(skin => skin.id === 'sangbiao'));
  for (const [clip, definition] of Object.entries(runtime.clips)) for (const [index, order] of definition.frames.entries()) {
    assert.deepEqual([...order].sort(), [...runtime.layers].sort());
    for (const layer of order) {
      const body = runtime.bodyLayers.includes(layer);
      const relative = `${body ? 'sangbiao/body' : 'shared'}/${clip}/${index}/${layer}.png`;
      const file = path.join(destination, relative);
      const info = await sharp(file).metadata();
      assert.deepEqual([info.width, info.height, info.hasAlpha], [640, 512, true], relative);
      assert.equal(hash(file), hash(path.join(fishing[0], relative)), relative);
      if (body) assert.equal(hash(file), hash(path.join(art, 'pack', relative)));
      files++;
    }
    slotCount++;
  }
}
const avatarFiles = ['image/profile/cat_avatar_sangbiao.png',
  'fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/characters/avatars/sangbiao.png',
  'desktop-overlay/OverlayResources/hud/cozy/characters/avatars/sangbiao.png',
  'fish-social-unity/FishSocialOverlay/OverlayResources/hud/cozy/characters/avatars/sangbiao.png'];
for (const relative of avatarFiles) {
  const file = path.join(root, relative);
  assert.equal(hash(file), hash(path.join(art, 'avatar.png')));
  const info = await sharp(file).metadata();
  assert.deepEqual([info.width, info.height, info.hasAlpha], [256, 256, true]);
}
assert.equal(hash(path.join(root, photoPath)), hash(path.join(art, 'frames/catch-04/composite.png')));
for (const directory of ['image/cats', 'mobile/assets/cats']) {
  const catalog = read(path.join(root, directory, 'catalog.json'));
  assert(catalog.skins.includes('sangbiao'));
  assert.deepEqual(catalog.foot, [264, 450]);
  assert.equal(catalog.atlasFrameScale?.sangbiao ?? 1, 1);
  for (const [clip, definition] of Object.entries(runtime.clips)) {
    const file = path.join(root, directory, 'sangbiao', clip + '.png');
    const info = await sharp(file).metadata();
    assert.deepEqual([info.width, info.height], [definition.frames.length * 320, 256]);
    assert.equal(hash(file), hash(path.join(art, 'atlases', clip + '.png')));
  }
}
const guids = new Set();
let metadataCount = 0;
function verifyMeta(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    if (entry.name.endsWith('.meta')) continue;
    const file = path.join(directory, entry.name);
    const meta = fs.readFileSync(file + '.meta', 'utf8');
    const guid = meta.match(/^guid: ([a-f0-9]{32})$/m)?.[1];
    assert(guid && !guids.has(guid), file + ': missing/duplicate GUID');
    guids.add(guid); metadataCount++;
    if (entry.isDirectory()) verifyMeta(file);
  }
}
verifyMeta(path.join(fishing[0], 'sangbiao'));
const manifest = read(path.join(art, 'painted-poses.json'));
assert.equal(manifest.poses.length, 44);
assert.equal(new Set(manifest.poses.map(pose => pose.sourceSha256)).size, 44);
for (const pose of manifest.poses) {
  assert.equal(hash(path.join(art, 'painted', pose.id + '.png')), pose.sha256);
  assert.equal(hash(path.join(art, 'originals', pose.id + '.png')), pose.originalSha256);
}
const report = { result: 'passed', characterId: 'sangbiao', layerFilesChecked: files,
  slotsChecked: slotCount, clients: fishing.length, metadataChecked: metadataCount,
  sharedAndUnityIdentityRouting: true, avatarCopies: avatarFiles.length,
  atlasCopies: 32, sourceDrawings: 44, runtimeClips: Object.keys(runtime.clips).length,
  windowsPlayerTested: false, unityPlayerTested: false };
const home = read(path.join(art, 'home-coverage.json'));
assert(home.installed);
for (const asset of home.assets) {
  for (const directory of ['fish-social-unity/Assets/Resources/Desktop/Theme/Cozy', 'desktop-overlay/OverlayResources/hud/cozy', 'fish-social-unity/FishSocialOverlay/OverlayResources/hud/cozy']) {
    const file = path.join(root, directory, asset.relativePath);
    assert.equal(hash(file), asset.sha256);
    const info = await sharp(file).metadata();
    assert.deepEqual([info.width, info.height, info.hasAlpha], [384, 328, true]);
    if (directory.includes('/Assets/Resources/')) assert(fs.readFileSync(file + '.meta', 'utf8').includes('spriteMode: 1'));
  }
}
const homeAnimations = read(path.join(unity, 'Resources/Desktop/Theme/Cozy/home-room/animations/manifest.json'));
assert(!homeAnimations.clips.some(clip => clip.skinId === 'sangbiao'), 'Do not advertise unpainted home animations');
report.homeStaticCopies = 6;
report.homeAnimationCoverage = home.animations;
fs.writeFileSync(path.join(art, 'integration-validation.json'), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify(report, null, 2));
