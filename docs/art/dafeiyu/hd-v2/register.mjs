import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { skinParts, polygonsFor } from './geometry.mjs';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const folder = path.dirname(fileURLToPath(import.meta.url));
const plan = JSON.parse(fs.readFileSync(path.join(folder, '../full-v1/plan.json')));
const studies = JSON.parse(fs.readFileSync(path.join(folder, '../../whale-maid/template-study-v1/templates.json')));
const calibrationFile = path.join(folder, 'calibration.json');
const calibration = fs.existsSync(calibrationFile) ? JSON.parse(fs.readFileSync(calibrationFile)) : {};
const center = box => [(box[0] + box[2]) / 2, (box[1] + box[3]) / 2];
const measurements = [];
const regions = {};
fs.mkdirSync(path.join(folder, 'registered'), { recursive: true });
for (const id of new Set(plan.frames.map(frame => frame.drawing))) {
  const relativeSource = calibration[id]?.source ?? 'generated/' + id + '.png';
  const input = path.join(folder, relativeSource);
  if (!fs.existsSync(input) && process.argv.includes('--partial')) continue;
  const metadata = await sharp(input).metadata();
  assert(metadata.width >= 1280 && metadata.height >= 1024 && metadata.hasAlpha, id + ': HD transparent source required');
  const sampled = await sharp(input).resize(640, 512, { fit: 'fill' }).ensureAlpha().raw().toBuffer();
  const parts = skinParts(sampled, id);
  const source = plan.pairs.flatMap(pair => pair.poses).find(pose => pose.id === id) || studies.poses.find(pose => pose.id === plan.existing[id]);
  const targetNear = center(source.bounds.hands), targetFar = center(source.bounds.far_hand);
  let sole = 0;
  for (let index = 0; index < sampled.length; index += 4) if (sampled[index + 3] >= 128) sole = Math.max(sole, Math.floor(index / 4 / 640));
  const faceWidth = parts.face.box[2] - parts.face.box[0] + 1;
  const scale = (calibration[id]?.faceWidth ?? 92) / faceWidth;
  const actualMidX = (parts.near.center[0] + parts.far.center[0]) / 2;
  const targetMidX = (targetNear[0] + targetFar[0]) / 2;
  const left = Math.round((targetMidX - actualMidX * scale + (calibration[id]?.offsetX ?? 0)) * 2);
  const top = Math.round((450 - sole * scale + (calibration[id]?.offsetY ?? 0)) * 2);
  const width = Math.round(1280 * scale), height = Math.round(1024 * scale);
  const resized = await sharp(input).resize(width, height, { fit: 'fill', kernel: 'lanczos3' }).ensureAlpha().raw().toBuffer();
  for (let index = 0; index < resized.length; index += 4) {
    if (resized[index + 3] === 0) continue;
    const column = index / 4 % width + left;
    const row = Math.floor(index / 4 / width) + top;
    assert(column >= 0 && column < 1280 && row >= 0 && row < 1024, id + ': crop would clip artwork');
  }
  const cropLeft = Math.max(0, -left), cropTop = Math.max(0, -top);
  const piece = await sharp(resized, { raw: { width, height, channels: 4 } }).extract({
    left: cropLeft, top: cropTop, width: Math.min(width - cropLeft, 1280 - Math.max(0, left)),
    height: Math.min(height - cropTop, 1024 - Math.max(0, top)) }).png().toBuffer();
  const destination = path.join(folder, 'registered', id + '.png');
  await sharp({ create: { width: 1280, height: 1024, channels: 4, background: '#00000000' } })
    .composite([{ input: piece, left: Math.max(0, left), top: Math.max(0, top) }]).png().toFile(destination);
  const transformPart = part => ({ ...part,
    box: part.box.map((value, index) => value * scale + (index % 2 === 0 ? left : top) / 2),
    center: [part.center[0] * scale + left / 2, part.center[1] * scale + top / 2] });
  const final = { face: transformPart(parts.face), near: transformPart(parts.near), far: transformPart(parts.far) };
  regions[id] = polygonsFor(final);
  for (const [layer, polygon] of Object.entries(calibration[id]?.nativeRegions ?? {})) {
    assert(['head', 'far_hand', 'hands'].includes(layer), id + ': unknown region');
    regions[id][layer] = polygon.map(([horizontal, vertical]) => [
      horizontal / metadata.width * 640 * scale + left / 2,
      vertical / metadata.height * 512 * scale + top / 2]);
  }
  measurements.push({ id, source: relativeSource, sourceSize: [metadata.width, metadata.height], textureSize: [1280, 1024],
    scale, offset: [left / 2, top / 2], calibrationMetric: 'visible-face-width',
    faceWidth: final.face.box[2] - final.face.box[0] + 1, face: final.face.box,
    near: final.near.center, far: final.far.center, targetNear, targetFar,
    nearHandError: Math.hypot(final.near.center[0] - targetNear[0], final.near.center[1] - targetNear[1]),
    farHandError: Math.hypot(final.far.center[0] - targetFar[0], final.far.center[1] - targetFar[1]) });
}
fs.writeFileSync(path.join(folder, 'registration.json'), JSON.stringify(measurements, null, 2) + '\n');
fs.writeFileSync(path.join(folder, 'layer-regions.json'), JSON.stringify(regions, null, 2) + '\n');
console.log(JSON.stringify(measurements.map(frame => ({ id: frame.id, face: frame.faceWidth,
  handErrors: [frame.nearHandError, frame.farHandError].map(value => +value.toFixed(1)) })), null, 2));
