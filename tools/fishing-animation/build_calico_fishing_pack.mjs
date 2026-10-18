import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const studyRoot = path.join(repoRoot, 'docs/art/cat-skins');
const unityRoot = path.join(repoRoot, 'fish-social-unity/Assets/StreamingAssets/Fishing');
const overlayRoot = path.join(repoRoot, 'desktop-overlay/OverlayResources/fishing');
const sourceManifest = JSON.parse(fs.readFileSync(path.join(unityRoot, 'orange/manifest.json')));
const poseManifest = JSON.parse(fs.readFileSync(path.join(studyRoot, 'full-pose-manifest.json')));
const artwork = JSON.parse(fs.readFileSync(path.join(studyRoot, 'painted-poses.json')));
const bodyLayers = sourceManifest.bodyLayers;
const [width, height] = sourceManifest.canvas;
const install = process.argv.includes('--install');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const poseByKey = new Map(poseManifest.frames.map(frame => [frame.clip + ':' + frame.index, frame]));
const groups = new Map();
const rendered = new Map();
const outputRoot = path.join(studyRoot, 'painted-export');
const records = [];

function saveJson(filename, value) {
  fs.mkdirSync(path.dirname(filename), { recursive: true });
  fs.writeFileSync(filename, JSON.stringify(value, null, 2) + '\n');
}

for (const frame of poseManifest.frames) {
  const sourceHash = hash(fs.readFileSync(path.join(studyRoot, frame.sourceCharacter)));
  if (!groups.has(sourceHash)) groups.set(sourceHash, []);
  groups.get(sourceHash).push(frame);
}

const entries = [...groups].map(([sourceHash, frames]) => {
  const entry = artwork.poses.find(pose => pose.sourceSha256 === sourceHash);
  if (!entry) throw new Error('Missing painted artwork: ' + frames.map(frame => frame.id).join(', '));
  const filename = path.join(studyRoot, entry.file);
  if (!fs.existsSync(filename)) throw new Error('Missing artwork: ' + entry.file);
  if (hash(fs.readFileSync(filename)) !== entry.sha256) throw new Error('Artwork changed: ' + entry.file);
  if (entry.method !== 'imagegen-pose-edit') throw new Error('Unknown provenance: ' + entry.file);
  if (install && !['geometry-reviewed', 'approved'].includes(entry.review)) throw new Error('Artwork needs pose review: ' + entry.file);
  return { sourceHash, frames, entry, filename };
});

async function readRaw(filename) {
  return sharp(filename).resize(width, height, { fit: 'fill' }).ensureAlpha().raw().toBuffer();
}

function partitionDrawing(drawing, masks) {
  const layers = bodyLayers.map(() => Buffer.alloc(drawing.length));
  for (let offset = 0; offset < drawing.length; offset += 4) {
    if (!drawing[offset + 3]) continue;
    let owner = 0;
    let weight = 0;
    for (let index = 0; index < masks.length; index++) {
      if (masks[index][offset + 3] >= weight && masks[index][offset + 3] > 0) {
        owner = index;
        weight = masks[index][offset + 3];
      }
    }
    if (!weight) {
      const pixel = offset / 4;
      const column = pixel % width;
      const row = Math.floor(pixel / width);
      let distance = Infinity;
      for (let vertical = -6; vertical <= 6; vertical++) {
        for (let horizontal = -6; horizontal <= 6; horizontal++) {
          const nextColumn = column + horizontal;
          const nextRow = row + vertical;
          const squared = horizontal * horizontal + vertical * vertical;
          if (nextColumn < 0 || nextColumn >= width || nextRow < 0 || nextRow >= height || squared >= distance) continue;
          const nextOffset = (nextRow * width + nextColumn) * 4 + 3;
          for (let index = 0; index < masks.length; index++) {
            if (masks[index][nextOffset] >= 128) {
              owner = index;
              distance = squared;
            }
          }
        }
      }
    }
    drawing.copy(layers[owner], offset, offset, offset + 4);
  }
  for (let offset = 0; offset < drawing.length; offset += 4) {
    const owners = layers.filter(layer => layer[offset + 3] > 0);
    if (drawing[offset + 3] && (owners.length !== 1 || !owners[0].subarray(offset, offset + 4).equals(drawing.subarray(offset, offset + 4)))) {
      throw new Error('Drawing lost or duplicated at pixel ' + offset / 4);
    }
  }
  return layers;
}

for (const { sourceHash, frames, entry, filename } of entries) {
  const drawing = await readRaw(filename);
  const original = await readRaw(path.join(studyRoot, frames[0].sourceCharacter));
  let union = 0;
  let intersection = 0;
  for (let offset = 3; offset < drawing.length; offset += 4) {
    if (drawing[offset] >= 128 || original[offset] >= 128) union++;
    if (drawing[offset] >= 128 && original[offset] >= 128) intersection++;
  }
  const overlap = intersection / union;
  if (install && overlap < 0.9) throw new Error('Pose geometry mismatch: ' + entry.file + ' ' + overlap);
  const png = await sharp(drawing, { raw: { width, height, channels: 4 } }).png().toBuffer();
  fs.mkdirSync(path.join(studyRoot, 'calico-character'), { recursive: true });
  for (const frame of frames) {
    const masks = await Promise.all(bodyLayers.map(layer => readRaw(path.join(studyRoot, frame.layers[layer]))));
    const pixels = partitionDrawing(drawing, masks);
    const layers = await Promise.all(pixels.map(layer => sharp(layer, { raw: { width, height, channels: 4 } }).png().toBuffer()));
    fs.writeFileSync(path.join(studyRoot, 'calico-character', frame.id + '.png'), png);
    rendered.set(frame.id, layers);
    records.push({ id: frame.id, artwork: entry.file, sourceHash, artworkHash: entry.sha256, overlap, review: entry.review });
  }
}

let runtimeFrames = 0;
for (const [clip, definition] of Object.entries(sourceManifest.clips)) {
  for (const [index, source] of definition.sourceFrames.entries()) {
    const frame = poseByKey.get(source.clip + ':' + source.index);
    if (!frame || !rendered.has(frame.id)) throw new Error('Missing source frame ' + clip + '/' + index);
    const layers = rendered.get(frame.id);
    for (const [layerIndex, layer] of bodyLayers.entries()) {
      const relative = path.join('calico/body', clip, String(index), layer + '.png');
      for (const root of install ? [outputRoot, unityRoot, overlayRoot] : [outputRoot]) {
        fs.mkdirSync(path.dirname(path.join(root, relative)), { recursive: true });
        fs.writeFileSync(path.join(root, relative), layers[layerIndex]);
      }
    }
    const composites = definition.frames[index].map(layer => ({ input: bodyLayers.includes(layer)
      ? layers[bodyLayers.indexOf(layer)]
      : path.join(unityRoot, 'shared', clip, String(index), layer + '.png') }));
    fs.mkdirSync(path.join(studyRoot, 'calico-full'), { recursive: true });
    await sharp({ create: { width, height, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
      .composite(composites).png().toFile(path.join(studyRoot, 'calico-full', frame.id + '.png'));
    runtimeFrames++;
  }
}
const manifest = { ...sourceManifest, skinId: 'calico', skinName: '三花猫', skinKind: 'painted-pose-assets-v' + artwork.version,
  artworkManifestSha256: hash(fs.readFileSync(path.join(studyRoot, 'painted-poses.json'))) };
for (const root of install ? [outputRoot, unityRoot, overlayRoot] : [outputRoot]) saveJson(path.join(root, 'calico/manifest.json'), manifest);
const report = { revision: artwork.version, status: artwork.status, knownLimitations: artwork.knownLimitations,
  sourceFrames: records.length, uniqueDrawings: entries.length, runtimeFrames, characterLayers: runtimeFrames * bodyLayers.length,
  originalGearUnchanged: true, installed: install, pixelPartitionVerified: true,
  productionReady: entries.every(({ entry }) => entry.review === 'approved'), frames: records };
saveJson(path.join(studyRoot, 'painted-validation.json'), report);
fs.writeFileSync(path.join(studyRoot, 'painted-review-data.js'), 'window.paintedReview = ' + JSON.stringify(report) + ';\n');
console.log(JSON.stringify({ ...report, frames: undefined }));
