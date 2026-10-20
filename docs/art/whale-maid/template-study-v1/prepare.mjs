import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const output = path.dirname(new URL(import.meta.url).pathname);
const repo = path.resolve(output, '../../../..');
const reference = path.join(repo, 'docs/art/cat-skins/reference');
const manifest = JSON.parse(fs.readFileSync(path.join(repo, 'docs/art/cat-skins/full-pose-manifest.json')));
const poses = ['ready-00', 'bait-02', 'bite-03', 'effort-03', 'catch-00', 'catch-04'];
const clear = { r: 0, g: 0, b: 0, alpha: 0 };
const records = [];

for (const id of poses) {
  const frame = manifest.frames.find(entry => entry.id === id);
  const folder = path.join(output, 'templates', id);
  fs.mkdirSync(folder, { recursive: true });
  const layers = [];
  for (const layer of frame.renderLayerOrder) {
    const input = path.join(reference, id, layer + '.png');
    fs.copyFileSync(input, path.join(folder, layer + '.png'));
    layers.push({ input });
  }
  fs.copyFileSync(path.join(reference, id, 'cat.png'), path.join(folder, 'character.png'));
  await sharp({ create: { width: 640, height: 512, channels: 4, background: clear } })
    .composite(layers).png().toFile(path.join(folder, 'composite.png'));
  const padded = await sharp(path.join(folder, 'composite.png')).resize(1280, 1024)
    .extend({ left: 128, right: 128, top: 0, bottom: 0, background: clear }).png().toBuffer();
  fs.writeFileSync(path.join(folder, 'edit-template.png'), padded);
  await sharp(padded).flatten({ background: '#f1ede5' }).png().toFile(path.join(folder, 'review.png'));
  const bounds = {};
  for (const layer of ['body', 'head', 'far_hand', 'hands']) {
    const { data, info } = await sharp(path.join(folder, layer + '.png')).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    let left = 640, top = 512, right = -1, bottom = -1;
    for (let vertical = 0; vertical < info.height; vertical++) {
      for (let horizontal = 0; horizontal < info.width; horizontal++) {
        if (data[(vertical * info.width + horizontal) * 4 + 3] < 128) continue;
        left = Math.min(left, horizontal); right = Math.max(right, horizontal);
        top = Math.min(top, vertical); bottom = Math.max(bottom, vertical);
      }
    }
    bounds[layer] = [left, top, right, bottom];
  }
  records.push({ ...frame, bounds });
}

const propFolder = path.join(output, 'shared');
fs.mkdirSync(propFolder, { recursive: true });
fs.copyFileSync(path.join(repo, 'desktop-overlay/OverlayResources/seats/seat-stool-v3.png'), path.join(propFolder, 'seat-stool-v3.png'));
fs.writeFileSync(path.join(output, 'templates.json'), JSON.stringify({ canvas: [640, 512], editCanvas: [1536, 1024], editCrop: [128, 0, 1280, 1024], poses: records }, null, 2) + '\n');
console.log(JSON.stringify(records.map(({ id, bounds }) => ({ id, bounds }))));
