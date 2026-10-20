import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const folder = path.dirname(new URL(import.meta.url).pathname);
const templates = JSON.parse(fs.readFileSync(path.join(folder, 'templates.json')));
const regions = JSON.parse(fs.readFileSync(path.join(folder, 'layer-regions.json')));
const clear = { r: 0, g: 0, b: 0, alpha: 0 };
const labels = {
  'ready-00': '准备握竿', 'bait-02': '挂饵', 'bite-03': '咬钩后仰',
  'effort-03': '持续发力', 'catch-00': '收获起势', 'catch-04': '展示收获',
};
const groups = [['ready-00', 'bait-02'], ['bite-03', 'effort-03'], ['catch-00', 'catch-04']];
const manifest = { version: 1, characterId: 'dafeiyu', name: '大肥鱼', status: 'keypose-study', canvas: [640, 512], reviewCanvas: [640, 600], rodOcclusionModel: 'head-layer-order-v1',
  bodyLayers: ['body', 'head', 'far_hand', 'hands'],
  sharedLayers: ['rod', 'line', 'fish', 'bobber', 'bait'],
  sourceManifest: '../../cat-skins/full-pose-manifest.json',
  mode: 'builtin-imagegen-template-edit',
  groups, seat: { file: 'shared/seat-stool-v3.png', rect: [118, 380, 280, 170], placement: 'review-only' },
  limitations: ['仅六个关键姿势试绘，不是完整动画包', '手部与原道具的亚像素接触、头部及长发的运动连续性仍需逐帧精修', '拆层是静态像素分区，不含转动肢体后会露出的隐藏补画'], frames: [] };
const hash = input => crypto.createHash('sha256').update(input).digest('hex');

function inside(horizontal, vertical, polygon) {
  let result = false;
  for (let current = 0, previous = polygon.length - 1; current < polygon.length; previous = current++) {
    const [currentX, currentY] = polygon[current];
    const [previousX, previousY] = polygon[previous];
    if ((currentY > vertical) !== (previousY > vertical) && horizontal <
      (previousX - currentX) * (vertical - currentY) / (previousY - currentY) + currentX) result = !result;
  }
  return result;
}

for (const frame of templates.poses) {
  const destination = path.join(folder, 'frames', frame.id);
  fs.mkdirSync(destination, { recursive: true });
  const source = path.join(folder, 'generated', frame.id + '.png');
  const normalized = await sharp(source).resize(1536, 1024, { fit: 'fill' }).png().toBuffer();
  const registered = await sharp(normalized)
    .extract({ left: 128, top: 0, width: 1280, height: 1024 }).resize(640, 512).ensureAlpha().raw().toBuffer();
  const pixels = Object.fromEntries(manifest.bodyLayers.map(layer => [layer, Buffer.alloc(registered.length)]));
  const counts = Object.fromEntries(manifest.bodyLayers.map(layer => [layer, 0]));
  for (let vertical = 0; vertical < 512; vertical++) {
    for (let horizontal = 0; horizontal < 640; horizontal++) {
      const offset = (vertical * 640 + horizontal) * 4;
      if (!registered[offset + 3]) continue;
      let owner = 'body';
      for (const layer of ['head', 'far_hand', 'hands']) {
        if (inside(horizontal + 0.5, vertical + 0.5, regions[frame.id][layer])) owner = layer;
      }
      registered.copy(pixels[owner], offset, offset, offset + 4);
      counts[owner]++;
    }
  }
  const partition = Buffer.alloc(registered.length);
  for (const layer of manifest.bodyLayers) {
    assert(counts[layer] > 100, frame.id + ' empty ' + layer);
    for (let offset = 0; offset < registered.length; offset += 4) {
      if (!pixels[layer][offset + 3]) continue;
      assert.equal(partition[offset + 3], 0, 'Overlapping pixel ownership');
      pixels[layer].copy(partition, offset, offset, offset + 4);
    }
    await sharp(pixels[layer], { raw: { width: 640, height: 512, channels: 4 } }).png().toFile(path.join(destination, layer + '.png'));
  }
  for (let offset = 0; offset < registered.length; offset += 4) {
    if (registered[offset + 3]) assert(partition.subarray(offset, offset + 4).equals(registered.subarray(offset, offset + 4)), 'Partition lost a visible pixel');
  }
  await sharp(registered, { raw: { width: 640, height: 512, channels: 4 } }).png().toFile(path.join(destination, 'character.png'));
  const props = {};
  for (const layer of manifest.sharedLayers) {
    const input = path.join(folder, 'templates', frame.id, layer + '.png');
    const target = path.join(destination, layer + '.png');
    fs.copyFileSync(input, target);
    assert.equal(hash(fs.readFileSync(target)), hash(fs.readFileSync(input)));
    props[layer] = hash(fs.readFileSync(input));
  }
  const composite = frame.renderLayerOrder.map(layer => ({ input: path.join(destination, layer + '.png') }));
  const combined = await sharp({ create: { width: 640, height: 512, channels: 4, background: clear } })
    .composite(composite).png().toBuffer();
  fs.writeFileSync(path.join(destination, 'composite.png'), combined);
  const bench = await sharp(path.join(folder, manifest.seat.file)).resize(280, 170, { fit: 'fill' }).png().toBuffer();
  for (const kind of ['whale', 'cat']) {
    const input = kind === 'whale' ? combined : path.join(folder, 'templates', frame.id, 'composite.png');
    await sharp({ create: { width: 640, height: 600, channels: 4, background: clear } })
      .composite([{ input: bench, left: 118, top: 380 }, { input, left: 0, top: 0 }])
      .png().toFile(path.join(destination, kind + '-with-seat.png'));
  }
  await sharp(path.join(destination, 'whale-with-seat.png')).flatten({ background: '#f1ede5' })
    .png().toFile(path.join(destination, 'review.png'));
  manifest.frames.push({ id: frame.id, label: labels[frame.id], durationMs: frame.durationMs,
    template: 'templates/' + frame.id + '/composite.png', character: 'frames/' + frame.id + '/character.png',
    composite: 'frames/' + frame.id + '/composite.png', review: 'frames/' + frame.id + '/review.png',
    layerFolder: 'frames/' + frame.id, renderLayerOrder: frame.renderLayerOrder,
    partitionPixels: counts, partitionExact: true, originalPropHashes: props,
    prompt: 'prompts/' + frame.id + '.txt', generation: 'generated/' + frame.id + '.png' });
}

fs.writeFileSync(path.join(folder, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
fs.writeFileSync(path.join(folder, 'data.js'), 'window.whaleStudy = ' + JSON.stringify(manifest) + ';\n');
const montage = manifest.frames.map((frame, index) => ({ input: path.join(folder, frame.review), left: (index % 3) * 640, top: Math.floor(index / 3) * 600 }));
await sharp({ create: { width: 1920, height: 1200, channels: 4, background: '#f1ede5' } }).composite(montage).png().toFile(path.join(folder, 'preview.png'));
console.log(JSON.stringify({ frames: manifest.frames.length, characterLayers: manifest.frames.length * 4,
  reusedProps: manifest.frames.length * 5, partitionExact: true, installed: false }));
