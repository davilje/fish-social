import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const folder = path.dirname(fileURLToPath(import.meta.url));
const plan = JSON.parse(fs.readFileSync(path.join(folder, 'plan.json')));
const partial = process.argv.includes('--partial');
const records = [];
for (const pair of plan.pairs) {
  const retouch = path.join(folder, 'generated', pair.id + '-retouch.png');
  const input = fs.existsSync(retouch) ? retouch : path.join(folder, 'generated', pair.id + '.png');
  if (partial && !fs.existsSync(input)) continue;
  const metadata = await sharp(input).metadata();
  if (!metadata.hasAlpha || Math.abs(metadata.width / metadata.height - 2.5) > 0.01) throw new Error(pair.id + ': invalid sheet');
  const normalized = await sharp(input).resize(2560, 1024, { fit: 'fill' }).png().toBuffer();
  for (const [index, pose] of pair.poses.entries()) {
    const file = path.join(folder, 'painted', pose.id + '.png');
    await sharp(normalized).extract({ left: index * 1280, top: 0, width: 1280, height: 1024 })
      .resize(640, 512).png().toFile(file);
    records.push({ id: pose.id, sheet: 'generated/' + path.basename(input), panel: index,
      method: 'builtin-imagegen-character-redraw', sha256: crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex') });
  }
}
fs.writeFileSync(path.join(folder, 'drawing-imports.json'), JSON.stringify({ name: '大肥鱼', mode: 'builtin-imagegen', drawings: records }, null, 2) + '\n');
const drawings = [...new Set(plan.frames.map(frame => frame.drawing))].filter(id => fs.existsSync(path.join(folder, 'painted', id + '.png')));
const composites = [];
for (const [index, id] of drawings.entries()) {
  const image = await sharp(path.join(folder, 'painted', id + '.png')).resize(320, 256).png().toBuffer();
  composites.push({ input: image, left: index % 6 * 320, top: Math.floor(index / 6) * 256 });
}
await sharp({ create: { width: 1920, height: Math.ceil(drawings.length / 6) * 256, channels: 4, background: '#eef0f4' } })
  .composite(composites).png().toFile(path.join(folder, 'drawing-review.png'));
console.log(JSON.stringify({ imported: records.length, drawings: drawings.length, complete: drawings.length === plan.uniqueDrawingCount }));
