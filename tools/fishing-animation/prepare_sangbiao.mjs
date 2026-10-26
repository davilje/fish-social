import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const study = path.join(root, 'docs/美术/猫咪形象');
const art = path.join(study, 'sangbiao');
const poses = JSON.parse(fs.readFileSync(path.join(study, 'painted-poses.json'))).poses;
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const save = (file, value) => fs.writeFileSync(file, JSON.stringify(value, null, 2) + '\n');
for (const directory of ['prompts', 'originals', 'painted', 'records'])
  fs.mkdirSync(path.join(art, directory), { recursive: true });

const [command, id, generated] = process.argv.slice(2);
if (command === 'prepare') {
  const identity = 'NEW character Sangbiao (丧彪), Chinese Li Hua tabby. Warm gray-brown soft textured fur, dark brown-black M forehead marking, two cheek stripes per side, three tapered flank tiger stripes, three dark tail rings and dark tip. Cream muzzle and chest/belly bib, pink inner ears and nose. Yellow-green irises around black pupils when eyes are open. Slightly fierce but cute determined expression, subtle lowered upper eyelids; retain original action emotion and closed eyes when present. No hat, clothes or accessories.';
  for (const pose of poses) {
    const file = path.join(art, 'prompts', pose.id + '.txt');
    if (fs.existsSync(file)) continue;
    fs.writeFileSync(file, `Use case: precise-object-edit. Edit target is this exact orange cat PNG, pose ${pose.id}. Create a newly painted Chinese Li Hua tabby character variant by changing ONLY FUR and iris color, with all original silhouette, pose, head/body dimensions, facial expression, eye placement, hands and feet positions, black outlines, sweat droplets or motion marks and transparency unchanged. Maintain exact 5:4 canvas and subject placement; no recentering, resizing or anatomy redesign. No accessories, no fishing equipment, no background or text. Clean transparent alpha, opaque fur and paws. ${identity} Preserve this exact frame geometry: head tilt, extremities, outline, expressions, face angle, silhouette and placement. Only change fur and eye iris color; no pose changes. Do not turn original orange markings into new blush marks. Reference is the exact pose to preserve, not a pose suggestion. Output only this single exact animation frame.\n`);
  }
  save(path.join(art, 'source-index.json'), { characterId: 'sangbiao', name: '狸花猫「丧彪」', canvas: [640, 512],
    sourceFrames: 63, uniqueDrawings: poses.length, method: 'imagegen-pose-edit',
    sources: ['docs/美术/猫咪形象/full-pose-manifest.json', 'docs/美术/猫咪形象/reference',
      'tools/fishing-animation/build_calico_fishing_pack.mjs', 'tools/fishing-animation/install_breed_identity.mjs'],
    nativeAssets: 'Editable full-canvas RGBA PNG layers and JSON animation manifests; no PSD/KRA/ORA found in existing cat asset directories.',
    poses: poses.map(pose => ({ id: pose.id, source: `reference/${pose.id}/cat.png`, sourceSha256: pose.sourceSha256,
      prompt: `prompts/${pose.id}.txt`, output: `painted/${pose.id}.png` })) });
  console.log(JSON.stringify({ prepared: poses.length }));
} else if (command === 'import') {
  const pose = poses.find(entry => entry.id === id);
  assert(pose && generated, 'Expected import <pose> <generated PNG>');
  const bytes = fs.readFileSync(generated);
  const metadata = await sharp(bytes).metadata();
  assert(metadata.hasAlpha, 'Imagegen output must have real alpha');
  assert(Math.abs(metadata.width / metadata.height - 1.25) < 0.01, 'Wrong full-canvas aspect ratio');
  const drawing = await sharp(bytes).resize(640, 512, { fit: 'fill' }).ensureAlpha().raw().toBuffer();
  const sourceBytes = fs.readFileSync(path.join(study, 'reference', id, 'cat.png'));
  assert.equal(hash(sourceBytes), pose.sourceSha256);
  const source = await sharp(sourceBytes).ensureAlpha().raw().toBuffer();
  let union = 0, intersection = 0, transparent = 0, partial = 0;
  let sourceBottom = 0, drawingBottom = 0, sourceFootLeft = 640, sourceFootRight = 0, drawingFootLeft = 640, drawingFootRight = 0;
  for (let offset = 3; offset < drawing.length; offset += 4) {
    const row = Math.floor((offset - 3) / 4 / 640);
    const column = (offset - 3) / 4 % 640;
    if (drawing[offset] === 0) transparent++;
    else if (drawing[offset] < 255) partial++;
    if (drawing[offset] >= 128 || source[offset] >= 128) union++;
    if (drawing[offset] >= 128 && source[offset] >= 128) intersection++;
    if (source[offset] >= 128) sourceBottom = Math.max(sourceBottom, row);
    if (drawing[offset] >= 128) drawingBottom = Math.max(drawingBottom, row);
    if (row >= 432 && source[offset] >= 128) { sourceFootLeft = Math.min(sourceFootLeft, column); sourceFootRight = Math.max(sourceFootRight, column); }
    if (row >= 432 && drawing[offset] >= 128) { drawingFootLeft = Math.min(drawingFootLeft, column); drawingFootRight = Math.max(drawingFootRight, column); }
  }
  assert(transparent > 640 * 512 * 0.5, 'Expected transparent canvas margins');
  const png = await sharp(drawing, { raw: { width: 640, height: 512, channels: 4 } }).png().toBuffer();
  if (fs.existsSync(path.join(art, 'records', id + '.json'))) {
    const old = JSON.parse(fs.readFileSync(path.join(art, 'records', id + '.json')));
    if (old.originalSha256 !== hash(bytes)) {
      const archive = path.join(art, 'rejected', id + '-' + old.originalSha256.slice(0, 12));
      fs.mkdirSync(archive, { recursive: true });
      fs.copyFileSync(path.join(art, 'originals', id + '.png'), path.join(archive, 'original.png'));
      save(path.join(archive, 'record.json'), old);
    }
  }
  fs.writeFileSync(path.join(art, 'originals', id + '.png'), bytes);
  fs.writeFileSync(path.join(art, 'painted', id + '.png'), png);
  const record = { id, method: 'imagegen-pose-edit', review: 'pending-visual-review',
    sourceSha256: pose.sourceSha256, sha256: hash(png), originalSha256: hash(bytes),
    generatedPath: generated, originalSize: [metadata.width, metadata.height], canvas: [640, 512],
    normalization: 'Whole-canvas resize only; alpha preserved. No part transforms, procedural recolor, or vector replacements.',
    prompt: fs.readFileSync(path.join(art, 'prompts', id + '.txt'), 'utf8'),
    identityReference: null,
    overlap: intersection / union, transparentPixels: transparent, partialAlphaPixels: partial,
    foot: { sourceBottom, drawingBottom, deltaY: drawingBottom - sourceBottom,
      sourceSpanAt432: [sourceFootLeft, sourceFootRight], drawingSpanAt432: [drawingFootLeft, drawingFootRight] } };
  save(path.join(art, 'records', id + '.json'), record);
  console.log(JSON.stringify({ id, overlap: record.overlap, footDelta: record.foot.deltaY, originalSize: record.originalSize }));
} else {
  throw new Error('Use prepare or import <pose> <generated PNG>');
}
