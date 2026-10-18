import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../docs/art/cat-skins');
const skin = process.argv[2];
const breed = JSON.parse(fs.readFileSync(path.join(root, 'breed-definitions.json'))).breeds.find(entry => entry.id === skin);
if (!breed) throw new Error('Unknown breed');
const ids = JSON.parse(fs.readFileSync(path.join(root, 'painted-poses.json'))).poses.map(entry => entry.id);
const records = [];
const missing = [];
const composites = [];
for (const [index, id] of ids.entries()) {
  const recordPath = path.join(root, `breeds/${skin}/painted/${id}.json`);
  if (!fs.existsSync(recordPath)) { missing.push(id); continue; }
  const entry = JSON.parse(fs.readFileSync(recordPath));
  records.push(entry);
  const input = await sharp(path.join(root, entry.file)).resize(256, 205).png().toBuffer();
  composites.push({ input, left: (index % 7) * 256, top: Math.floor(index / 7) * 205 });
}
await sharp({ create: { width: 1792, height: 1435, channels: 4, background: '#e9e1d6' } })
  .composite(composites).png().toFile(path.join(root, `breeds/${skin}/contact.png`));
const geometryFailures = records.filter(entry => entry.overlap < 0.9).map(entry => ({ id: entry.id, overlap: entry.overlap }));
const report = { skin, name: breed.name, expected: ids.length, present: records.length, missing, geometryFailures,
  reviewOrder: ids, status: 'pending-visual-review' };
fs.writeFileSync(path.join(root, `breeds/${skin}/review.json`), JSON.stringify(report, null, 2) + '\n');
if (process.argv.includes('--accept-geometry')) {
  if (missing.length || geometryFailures.length) throw new Error('Incomplete or misaligned breed; cannot install');
  const artwork = { version: 4, status: 'painted-geometry-reviewed', canvas: [640, 512],
    knownLimitations: ['逐帧绘制仍需检查花纹细微连续性；几何检查不等同美术终验。'],
    poses: records.map(entry => ({ ...entry, review: 'geometry-reviewed' })) };
  fs.writeFileSync(path.join(root, `breeds/${skin}/painted-poses.json`), JSON.stringify(artwork, null, 2) + '\n');
}
console.log(JSON.stringify(report));
