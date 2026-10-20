import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { inside } from './geometry.mjs';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const folder = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(folder, '../../../..');
const catRoot = path.join(root, 'docs/art/cat-skins');
const fishing = path.join(root, 'fish-social-unity/Assets/StreamingAssets/Fishing');
const plan = JSON.parse(fs.readFileSync(path.join(folder, '../full-v1/plan.json')));
const sourceRegions = JSON.parse(fs.readFileSync(path.join(folder, 'layer-regions.json')));
const partial = process.argv.includes('--partial');
const bodyLayers = ['body', 'head', 'far_hand', 'hands'];
const sharedLayers = ['rod', 'line', 'fish', 'bobber', 'bait'];
const clear = '#00000000';
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const saveJson = (file, value) => fs.writeFileSync(path.join(folder, file), JSON.stringify(value, null, 2) + '\n');
const drawings = new Map();
const registrations = JSON.parse(fs.readFileSync(path.join(folder, 'registration.json')));
const regions = {};
fs.mkdirSync(path.join(folder, 'registered'), { recursive: true });
for (const frame of plan.frames) {
  if (drawings.has(frame.drawing)) continue;
  const input = path.join(folder, 'registered', frame.drawing + '.png');
  if (partial && !fs.existsSync(input)) continue;
  const pixels = await sharp(input).ensureAlpha().raw().toBuffer();
  assert.equal(pixels.length, 1280 * 1024 * 4, frame.drawing + ': HD dimensions');
  regions[frame.drawing] = sourceRegions[frame.drawing];
  const parts = Object.fromEntries(bodyLayers.map(layer => [layer, Buffer.alloc(pixels.length)]));
  for (let vertical = 0; vertical < 1024; vertical++) for (let horizontal = 0; horizontal < 1280; horizontal++) {
    const offset = (vertical * 1280 + horizontal) * 4;
    if (!pixels[offset + 3]) continue;
    let owner = 'body';
    for (const layer of ['head', 'far_hand', 'hands']) if (inside((horizontal + 0.5) / 2, (vertical + 0.5) / 2, regions[frame.drawing][layer])) owner = layer;
    pixels.copy(parts[owner], offset, offset, offset + 4);
  }
  for (let offset = 0; offset < pixels.length; offset += 4) {
    if (!pixels[offset + 3]) continue;
    const owners = bodyLayers.filter(layer => parts[layer][offset + 3] > 0);
    assert.equal(owners.length, 1, frame.drawing + ': duplicate or missing ownership');
    assert(parts[owners[0]].subarray(offset, offset + 4).equals(pixels.subarray(offset, offset + 4)));
  }
  drawings.set(frame.drawing, { pixels, parts });
}
const manifest = { version: 2, characterId: 'dafeiyu', name: '大肥鱼', status: 'hd-unified-scale',
  canvas: [640, 512], textureCanvas: [1280, 1024], bodyTextureScale: 2, reviewCanvas: [640, 600], rodOcclusionModel: 'head-layer-order-v1',
  bodyLayers, sharedLayers, seat: { file: 'shared/seat-stool-v3.png', rect: [118, 380, 280, 170] },
  uniqueDrawings: drawings.size, sourceFrameCount: plan.frames.length,
  installed: fs.existsSync(path.join(folder, 'installation.json')), frames: [] };
fs.mkdirSync(path.join(folder, 'shared'), { recursive: true });
fs.copyFileSync(path.join(folder, '../full-v1', manifest.seat.file), path.join(folder, manifest.seat.file));
const bench = await sharp(path.join(folder, manifest.seat.file)).resize(560, 340, { fit: 'fill' }).png().toBuffer();
for (const frame of plan.frames) {
  if (!drawings.has(frame.drawing)) continue;
  const output = path.join(folder, 'frames', frame.id);
  fs.mkdirSync(output, { recursive: true });
  const drawing = drawings.get(frame.drawing);
  await sharp(drawing.pixels, { raw: { width: 1280, height: 1024, channels: 4 } }).png().toFile(path.join(output, 'character.png'));
  for (const layer of bodyLayers) await sharp(drawing.parts[layer], { raw: { width: 1280, height: 1024, channels: 4 } }).png().toFile(path.join(output, layer + '.png'));
  const originalPropHashes = {};
  for (const layer of sharedLayers) {
    const original = path.join(catRoot, frame.layers[layer]);
    fs.copyFileSync(original, path.join(output, layer + '.png'));
    originalPropHashes[layer] = hash(fs.readFileSync(original));
  }
  const composite = await sharp({ create: { width: 1280, height: 1024, channels: 4, background: clear } })
    .composite(await Promise.all(frame.renderLayerOrder.map(async layer => ({ input: sharedLayers.includes(layer)
      ? await sharp(path.join(output, layer + '.png')).resize(1280, 1024).png().toBuffer()
      : path.join(output, layer + '.png') })))).png().toBuffer();
  fs.writeFileSync(path.join(output, 'composite.png'), composite);
  const showSeat = !['sit-00', 'sit-01'].includes(frame.drawing);
  await sharp({ create: { width: 1280, height: 1200, channels: 4, background: '#eef0f4' } })
    .composite([...(showSeat ? [{ input: bench, left: 236, top: 760 }] : []), { input: composite }]).png().toFile(path.join(output, 'review.png'));
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
  runtime.maskModel = 'dafeiyu-pose-regions-hd-v2';
  runtime.bodyTextureScale = 2;
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
      strip.push({ input: await sharp(path.join(folder, frame.composite)).resize(640, 512).png().toBuffer(), left: index * 640, top: 0 });
    }
    fs.mkdirSync(path.join(folder, 'atlases'), { recursive: true });
    await sharp({ create: { width: strip.length * 640, height: 512, channels: 4, background: clear } })
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
