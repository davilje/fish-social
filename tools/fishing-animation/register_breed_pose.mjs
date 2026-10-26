import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../docs/美术/猫咪形象');
const [skin, id, generatedPath, promptPath] = process.argv.slice(2);
const breed = JSON.parse(fs.readFileSync(path.join(root, 'breed-definitions.json'))).breeds.find(entry => entry.id === skin);
const original = JSON.parse(fs.readFileSync(path.join(root, 'painted-poses.json'))).poses.find(entry => entry.id === id);
if (!breed || !original) throw new Error('Unknown breed or pose');
const destination = `breeds/${skin}/painted/${id}.png`;
fs.mkdirSync(path.dirname(path.join(root, destination)), { recursive: true });
const drawing = await sharp(generatedPath).resize(640, 512, { fit: 'fill' }).ensureAlpha().raw().toBuffer();
const source = await sharp(path.join(root, 'reference', id, 'cat.png')).ensureAlpha().raw().toBuffer();
let union = 0;
let intersection = 0;
for (let offset = 3; offset < source.length; offset += 4) {
  if (source[offset] >= 128 || drawing[offset] >= 128) union++;
  if (source[offset] >= 128 && drawing[offset] >= 128) intersection++;
}
const overlap = intersection / union;
fs.copyFileSync(generatedPath, path.join(root, destination));
const record = { id, file: destination, sourceSha256: original.sourceSha256,
  sha256: crypto.createHash('sha256').update(fs.readFileSync(generatedPath)).digest('hex'),
  method: 'imagegen-pose-edit', review: 'pending-visual-review', overlap,
  key: id === 'sit-03', generatedPath, prompt: promptPath ? fs.readFileSync(promptPath, 'utf8') : '' };
fs.writeFileSync(path.join(root, `breeds/${skin}/painted/${id}.json`), JSON.stringify(record, null, 2) + '\n');
console.log(JSON.stringify({ skin, id, overlap }));
