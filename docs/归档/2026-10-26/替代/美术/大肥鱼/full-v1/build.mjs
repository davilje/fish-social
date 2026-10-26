import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { skinParts, inside, polygonsFor } from './geometry.mjs';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const folder = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(folder, '../../../..');
const catRoot = path.join(root, 'docs/美术/猫咪形象');
const trial = path.join(root, 'docs/归档/2026-10-26/替代/美术/whale-maid/template-study-v1');
const fishing = path.join(root, 'fish-social-unity/Assets/StreamingAssets/Fishing');
const plan = JSON.parse(fs.readFileSync(path.join(folder, 'plan.json')));
const trialRegions = JSON.parse(fs.readFileSync(path.join(trial, 'layer-regions.json')));
const partial = process.argv.includes('--partial');
const bodyLayers = ['body', 'head', 'far_hand', 'hands'];
const sharedLayers = ['rod', 'line', 'fish', 'bobber', 'bait'];
const clear = '#00000000';
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const saveJson = (file, value) => fs.writeFileSync(path.join(folder, file), JSON.stringify(value, null, 2) + '\n');
const drawings = new Map();
const registrations = [];
const regions = {};
fs.mkdirSync(path.join(folder, 'registered'), { recursive: true });
for (const frame of plan.frames) {
  if (drawings.has(frame.drawing)) continue;
  const input = path.join(folder, 'painted', frame.drawing + '.png');
  if (partial && !fs.existsSync(input)) continue;
  let pixels = await sharp(input).ensureAlpha().raw().toBuffer();
  let registration = { scale: 1, offset: [0, 0], reused: plan.existing[frame.drawing] ?? null };
  if (!registration.reused) {
    const parts = skinParts(pixels, frame.drawing);
    let bottom = 0;
    for (let index = 0; index < pixels.length; index += 4) if (pixels[index + 3] >= 128) bottom = Math.max(bottom, Math.floor(index / 4 / 640));
    const pose = plan.pairs.flatMap(pair => pair.poses).find(pose => pose.id === frame.drawing);
    const center = box => [(box[0] + box[2]) / 2, (box[1] + box[3]) / 2];
    const targetNear = center(pose.bounds.hands), targetFar = center(pose.bounds.far_hand);
    const actual = [(parts.near.center[0] + parts.far.center[0]) / 2, (parts.near.center[1] + parts.far.center[1]) / 2];
    const target = [(targetNear[0] + targetFar[0]) / 2, (targetNear[1] + targetFar[1]) / 2];
    const unarmed = ['sit-00', 'sit-01', 'sit-02'].includes(frame.drawing);
    const scale = unarmed ? 87 / (parts.face.box[2] - parts.face.box[0] + 1) : (450 - target[1]) / (bottom - actual[1]);
    assert(scale >= 0.5 && scale <= 1.4, frame.drawing + ': unreasonable scale ' + scale);
    const left = Math.round(target[0] - actual[0] * scale);
    const top = Math.round(450 - bottom * scale);
    const resized = await sharp(input).resize(Math.round(640 * scale), Math.round(512 * scale)).png().toBuffer();
    const sourceLeft = Math.max(0, -left), sourceTop = Math.max(0, -top);
    const destinationLeft = Math.max(0, left), destinationTop = Math.max(0, top);
    const piece = await sharp(resized).extract({ left: sourceLeft, top: sourceTop,
      width: Math.min(Math.round(640 * scale) - sourceLeft, 640 - destinationLeft),
      height: Math.min(Math.round(512 * scale) - sourceTop, 512 - destinationTop) }).png().toBuffer();
    pixels = await sharp({ create: { width: 640, height: 512, channels: 4, background: clear } })
      .composite([{ input: piece, left: destinationLeft, top: destinationTop }]).raw().toBuffer();
    const measured = skinParts(pixels, frame.drawing + ' registered');
    registration = { scale, offset: [left, top], reused: null,
      nearHandError: Math.hypot(measured.near.center[0] - targetNear[0], measured.near.center[1] - targetNear[1]),
      farHandError: Math.hypot(measured.far.center[0] - targetFar[0], measured.far.center[1] - targetFar[1]) };
    regions[frame.drawing] = polygonsFor(measured);
  } else regions[frame.drawing] = trialRegions[registration.reused];
  await sharp(pixels, { raw: { width: 640, height: 512, channels: 4 } }).png().toFile(path.join(folder, 'registered', frame.drawing + '.png'));
  const parts = Object.fromEntries(bodyLayers.map(layer => [layer, Buffer.alloc(pixels.length)]));
  for (let vertical = 0; vertical < 512; vertical++) for (let horizontal = 0; horizontal < 640; horizontal++) {
    const offset = (vertical * 640 + horizontal) * 4;
    if (!pixels[offset + 3]) continue;
    let owner = 'body';
    for (const layer of ['head', 'far_hand', 'hands']) if (inside(horizontal + 0.5, vertical + 0.5, regions[frame.drawing][layer])) owner = layer;
    pixels.copy(parts[owner], offset, offset, offset + 4);
  }
  for (let offset = 0; offset < pixels.length; offset += 4) {
    if (!pixels[offset + 3]) continue;
    const owners = bodyLayers.filter(layer => parts[layer][offset + 3] > 0);
    assert.equal(owners.length, 1, frame.drawing + ': duplicate or missing ownership');
    assert(parts[owners[0]].subarray(offset, offset + 4).equals(pixels.subarray(offset, offset + 4)));
  }
  drawings.set(frame.drawing, { pixels, parts });
  registrations.push({ id: frame.drawing, ...registration });
}
const manifest = { version: 1, characterId: 'dafeiyu', name: '大肥鱼', status: 'full-frame-art-review',
  canvas: [640, 512], reviewCanvas: [640, 600], rodOcclusionModel: 'head-layer-order-v1',
  bodyLayers, sharedLayers, seat: { file: 'shared/seat-stool-v3.png', rect: [118, 380, 280, 170] },
  uniqueDrawings: drawings.size, sourceFrameCount: plan.frames.length,
  installed: fs.existsSync(path.join(folder, 'installation.json')), frames: [] };
const bench = await sharp(path.join(folder, manifest.seat.file)).resize(280, 170, { fit: 'fill' }).png().toBuffer();
for (const frame of plan.frames) {
  if (!drawings.has(frame.drawing)) continue;
  const output = path.join(folder, 'frames', frame.id);
  fs.mkdirSync(output, { recursive: true });
  const drawing = drawings.get(frame.drawing);
  await sharp(drawing.pixels, { raw: { width: 640, height: 512, channels: 4 } }).png().toFile(path.join(output, 'character.png'));
  for (const layer of bodyLayers) await sharp(drawing.parts[layer], { raw: { width: 640, height: 512, channels: 4 } }).png().toFile(path.join(output, layer + '.png'));
  const originalPropHashes = {};
  for (const layer of sharedLayers) {
    const original = path.join(catRoot, frame.layers[layer]);
    fs.copyFileSync(original, path.join(output, layer + '.png'));
    originalPropHashes[layer] = hash(fs.readFileSync(original));
  }
  const composite = await sharp({ create: { width: 640, height: 512, channels: 4, background: clear } })
    .composite(frame.renderLayerOrder.map(layer => ({ input: path.join(output, layer + '.png') }))).png().toBuffer();
  fs.writeFileSync(path.join(output, 'composite.png'), composite);
  const showSeat = !['sit-00', 'sit-01'].includes(frame.drawing);
  await sharp({ create: { width: 640, height: 600, channels: 4, background: '#eef0f4' } })
    .composite([...(showSeat ? [{ input: bench, left: 118, top: 380 }] : []), { input: composite }]).png().toFile(path.join(output, 'review.png'));
  manifest.frames.push({ id: frame.id, drawing: frame.drawing, clip: frame.clip, label: frame.label,
    durationMs: frame.durationMs, showSeat, renderLayerOrder: frame.renderLayerOrder, layerFolder: 'frames/' + frame.id,
    character: 'frames/' + frame.id + '/character.png', composite: 'frames/' + frame.id + '/composite.png',
    review: 'frames/' + frame.id + '/review.png', partitionExact: true, originalPropHashes });
}
saveJson('manifest.json', manifest);
saveJson('registration.json', registrations);
saveJson('layer-regions.json', regions);
fs.writeFileSync(path.join(folder, 'data.js'), 'window.dafeiyu = ' + JSON.stringify(manifest) + ';\n');
if (!partial) {
  assert.equal(drawings.size, 44); assert.equal(manifest.frames.length, 63);
  const runtime = JSON.parse(fs.readFileSync(path.join(fishing, 'orange/manifest.json')));
  const frameById = new Map(manifest.frames.map(frame => [frame.id, frame]));
  const pack = path.join(folder, 'pack');
  runtime.skinId = 'dafeiyu'; runtime.skinName = '大肥鱼'; runtime.skinKind = 'painted-humanoid-character';
  runtime.maskModel = 'dafeiyu-pose-regions-v1';
  runtime.reviewStatus = 'art-review';
  runtime.characterArtworkManifestSha256 = hash(fs.readFileSync(path.join(folder, 'manifest.json')));
  for (const [clip, definition] of Object.entries(runtime.clips)) {
    const strip = [];
    definition.seatVisible = [];
    for (const [index, origin] of definition.sourceFrames.entries()) {
      const id = origin.clip + '-' + String(origin.index).padStart(2, '0');
      const frame = frameById.get(id);
      definition.seatVisible.push(frame.showSeat);
      for (const layer of [...bodyLayers, ...sharedLayers]) {
        const destination = path.join(pack, bodyLayers.includes(layer) ? 'dafeiyu/body' : 'shared', clip, String(index));
        fs.mkdirSync(destination, { recursive: true });
        fs.copyFileSync(path.join(folder, frame.layerFolder, layer + '.png'), path.join(destination, layer + '.png'));
      }
      strip.push({ input: await sharp(path.join(folder, frame.composite)).resize(320, 256).png().toBuffer(), left: index * 320, top: 0 });
    }
    fs.mkdirSync(path.join(folder, 'atlases'), { recursive: true });
    await sharp({ create: { width: strip.length * 320, height: 256, channels: 4, background: clear } })
      .composite(strip).png().toFile(path.join(folder, 'atlases', clip + '.png'));
  }
  fs.writeFileSync(path.join(pack, 'dafeiyu/manifest.json'), JSON.stringify(runtime, null, 2) + '\n');
}
const previewFrames = manifest.frames;
const previews = [];
for (const [index, frame] of previewFrames.entries()) previews.push({
  input: await sharp(path.join(folder, frame.review)).resize(320, 300).png().toBuffer(), left: index % 7 * 320, top: Math.floor(index / 7) * 300 });
await sharp({ create: { width: 2240, height: Math.ceil(previews.length / 7) * 300, channels: 4, background: '#eef0f4' } })
  .composite(previews).png().toFile(path.join(folder, 'preview.png'));
console.log(JSON.stringify({ name: manifest.name, drawings: drawings.size, sourceFrames: manifest.frames.length,
  complete: !partial, maxHandError: Math.max(...registrations.filter(frame => frame.nearHandError).map(frame => Math.max(frame.nearHandError, frame.farHandError))) }));
