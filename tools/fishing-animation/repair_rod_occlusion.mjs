import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const study = path.join(root, 'docs/art/cat-skins');
const fishing = path.join(root, 'fish-social-unity/Assets/StreamingAssets/Fishing');
const overlay = path.join(root, 'desktop-overlay/OverlayResources/fishing');
const rawRoot = path.join(study, 'rod-source');
const recovered = JSON.parse(fs.readFileSync(path.join(rawRoot, 'manifest.json')));
const poseFile = path.join(study, 'full-pose-manifest.json');
const poses = JSON.parse(fs.readFileSync(poseFile));
const skins = ['orange', 'calico', 'silver', 'brown', 'cream', 'blue', 'black', 'white', 'cow'];
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const saveJson = (filename, value) => fs.writeFileSync(filename, JSON.stringify(value, null, 2) + '\n');
const byId = new Map(recovered.frames.map(frame => [frame.id, frame]));
const runtime = JSON.parse(fs.readFileSync(path.join(fishing, 'orange/manifest.json')));
const aliases = new Map();
const beforeCharacterHashes = new Map();
let restoredPixels = 0;
const sourceOption = process.argv.indexOf('--source-animation');
assert(sourceOption < 0 || process.argv[sourceOption + 1], '--source-animation requires animation.json');
const sourceFile = sourceOption < 0 ? null : path.resolve(process.argv[sourceOption + 1]);
const sourceBytes = sourceFile ? fs.readFileSync(sourceFile) : null;
const sourceDocument = sourceBytes ? JSON.parse(sourceBytes) : null;
if (sourceDocument) {
  assert.equal(sourceDocument.rodOcclusionModel, recovered.model);
  for (const frame of recovered.frames) {
    const authored = sourceDocument.clips[frame.clip].frames[frame.index];
    assert.deepEqual(authored.renderLayerOrder, frame.renderLayerOrder);
    assert.equal(hash(fs.readFileSync(path.resolve(path.dirname(sourceFile), authored.layers.rod))), frame.sha256);
  }
}
const reportFile = path.join(study, 'rod-occlusion-repair.json');
const previousReport = fs.existsSync(reportFile) ? JSON.parse(fs.readFileSync(reportFile)) : {};

assert.equal(recovered.model, 'head-layer-order-v1');
assert.equal(recovered.characterGeometryUnchanged, true);
assert.equal(byId.size, 63);
for (const frame of recovered.frames) {
  assert.equal(hash(fs.readFileSync(path.join(rawRoot, frame.file))), frame.sha256);
}

for (const skin of skins) {
  const manifest = JSON.parse(fs.readFileSync(path.join(fishing, skin, 'manifest.json')));
  for (const [clip, definition] of Object.entries(manifest.clips)) {
    for (const [index] of definition.frames.entries()) {
      for (const layer of manifest.bodyLayers) {
        const relative = `${skin}/body/${clip}/${index}/${layer}.png`;
        beforeCharacterHashes.set(relative, hash(fs.readFileSync(path.join(fishing, relative))));
      }
    }
  }
}

for (const [clip, definition] of Object.entries(runtime.clips)) {
  for (const [index, source] of definition.sourceFrames.entries()) {
    const id = source.clip + '-' + String(source.index).padStart(2, '0');
    const frame = byId.get(id);
    assert(frame, id);
    if (!aliases.has(id)) aliases.set(id, { clip, index });
    const relative = `shared/${clip}/${index}/rod.png`;
    const previous = await sharp(path.join(fishing, relative)).ensureAlpha().raw().toBuffer();
    const completeFile = path.join(rawRoot, frame.file);
    const complete = await sharp(completeFile).ensureAlpha().raw().toBuffer();
    for (let offset = 3; offset < previous.length; offset += 4) {
      if (complete[offset] > previous[offset]) restoredPixels++;
    }
    for (const destination of [fishing, overlay]) fs.copyFileSync(completeFile, path.join(destination, relative));
    definition.frames[index] = frame.renderLayerOrder;
  }
}

for (const skin of skins) {
  for (const destination of [fishing, overlay, path.join(study, 'painted-export')]) {
    const filename = path.join(destination, skin, 'manifest.json');
    if (!fs.existsSync(filename)) continue;
    const manifest = JSON.parse(fs.readFileSync(filename));
    manifest.rodOcclusionModel = recovered.model;
    manifest.rodSourceManifestSha256 = hash(fs.readFileSync(path.join(rawRoot, 'manifest.json')));
    if (sourceBytes) manifest.sourceManifestSha256 = hash(sourceBytes);
    for (const [clip, definition] of Object.entries(manifest.clips)) definition.frames = runtime.clips[clip].frames;
    saveJson(filename, manifest);
  }
}

for (const frame of poses.frames) {
  const complete = byId.get(frame.id);
  fs.copyFileSync(path.join(rawRoot, complete.file), path.join(study, frame.layers.rod));
  frame.renderLayerOrder = complete.renderLayerOrder;
}
poses.rodOcclusionModel = recovered.model;
saveJson(poseFile, poses);
fs.writeFileSync(path.join(study, 'full-pose-data.js'), 'window.poseStudy = ' + JSON.stringify(poses) + ';\n');

for (const destination of [fishing, overlay]) {
  const sourceFile = path.join(destination, 'source-animation-v19.json');
  if (fs.existsSync(sourceFile)) {
    const document = sourceDocument ? structuredClone(sourceDocument) : JSON.parse(fs.readFileSync(sourceFile));
    document.rodOcclusionModel = recovered.model;
    for (const [clip, definition] of Object.entries(document.clips)) {
      for (const frame of definition.frames) {
        const complete = byId.get(clip + '-' + String(frame.index).padStart(2, '0'));
        frame.renderLayerOrder = complete.renderLayerOrder;
        frame.rig = complete.rig;
      }
    }
    if (sourceBytes) fs.writeFileSync(sourceFile, sourceBytes);
    else saveJson(sourceFile, document);
  }
  const inventoryFile = path.join(destination, 'inventory.json');
  if (fs.existsSync(inventoryFile)) {
    const inventory = JSON.parse(fs.readFileSync(inventoryFile));
    inventory.rodOcclusionModel = recovered.model;
    saveJson(inventoryFile, inventory);
  }
}

for (const skin of skins) {
  const manifest = JSON.parse(fs.readFileSync(path.join(fishing, skin, 'manifest.json')));
  const output = path.join(study, skin + '-full');
  fs.mkdirSync(output, { recursive: true });
  for (const frame of poses.frames) {
    const { clip, index } = aliases.get(frame.id);
    const images = manifest.clips[clip].frames[index].map(layer => ({ input: path.join(fishing,
      manifest.bodyLayers.includes(layer) ? `${skin}/body/${clip}/${index}/${layer}.png` : `shared/${clip}/${index}/${layer}.png`) }));
    await sharp({ create: { width: 640, height: 512, channels: 4, background: '#00000000' } })
      .composite(images).png().toFile(path.join(output, frame.id + '.png'));
  }
  fs.copyFileSync(path.join(output, 'catch-04.png'), path.join(root, `image/fishing_photos/cat_fishing_${skin}.png`));
}

for (const [relative, previous] of beforeCharacterHashes) {
  assert.equal(hash(fs.readFileSync(path.join(fishing, relative))), previous, 'Character artwork changed: ' + relative);
}
saveJson(reportFile, {
  model: recovered.model, skins, sourceFrames: recovered.frames.length,
  runtimeFramesPerSkin: Object.values(runtime.clips).reduce((total, clip) => total + clip.frames.length, 0),
  characterLayersCheckedUnchanged: beforeCharacterHashes.size,
  characterArtworkChanged: false, sharedRodFrames: Object.values(runtime.clips).reduce((total, clip) => total + clip.frames.length, 0),
  restoredPixelsThisRun: restoredPixels,
  restoredPixelsTotal: (previousReport.restoredPixelsTotal ?? previousReport.restoredPixelsThisRun ?? 0) + restoredPixels,
  rule: 'Keep complete shared rods. For gripping frames draw far_hand < rod < head < hands; each character head supplies its own occlusion.',
});
console.log(JSON.stringify({ skins: skins.length, sourceFrames: byId.size, characterLayersUnchanged: beforeCharacterHashes.size, restoredPixels }));
