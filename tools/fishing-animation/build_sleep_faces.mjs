import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const art = path.join(root, 'docs/art/fishing-rest');
const native = path.join(root, 'desktop-overlay/OverlayResources/fishing');
const mirror = path.join(root, 'fish-social-unity/FishSocialOverlay/OverlayResources/fishing');
const breeds = ['orange', 'calico', 'silver', 'brown', 'cream', 'blue', 'black', 'white', 'cow', 'dafeiyu'];
const source = breed => path.join(native, breed, 'body/fishing/0/head.png');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
fs.mkdirSync(art, { recursive: true });
const mode = process.argv[2];

if (mode === 'inspect') {
  const layers = [];
  for (const [index, breed] of breeds.entries()) {
    const input = await sharp(source(breed)).flatten({ background: '#deded8' }).resize(320, 256).png().toBuffer();
    layers.push({ input, left: index % 5 * 320, top: Math.floor(index / 5) * 256 });
  }
  await sharp({ create: { width: 1600, height: 512, channels: 3, background: '#deded8' } }).composite(layers).png().toFile(path.join(art, 'original-contact.png'));
}

if (mode === 'prepare-orange') {
  await sharp(source('orange')).extract({ left: 180, top: 164, width: 192, height: 192 }).resize(768, 768).png().toFile(path.join(art, 'orange-edit-input.png'));
}

if (mode === 'prepare-dafeiyu-eyes') {
  await sharp(source('dafeiyu')).extract({ left: 475, top: 520, width: 200, height: 100 }).resize(1200, 600).png().toFile(path.join(art, 'dafeiyu-eyes-edit-input.png'));
}

if (mode === 'prepare-sangbiao') {
  await sharp(source('sangbiao')).extract({ left: 180, top: 164, width: 192, height: 192 }).resize(768, 768).png().toFile(path.join(art, 'sangbiao-edit-input.png'));
}

if (mode === 'prepare-atlas') {
  const selected = breeds.slice(1, 9);
  const layers = [];
  for (const [index, breed] of selected.entries()) {
    const input = await sharp(source(breed)).extract({ left: 180, top: 164, width: 192, height: 192 }).resize(384, 384).png().toBuffer();
    layers.push({ input, left: index % 4 * 384, top: Math.floor(index / 4) * 384 });
  }
  await sharp({ create: { width: 1536, height: 768, channels: 4, background: '#00000000' } }).composite(layers).png().toFile(path.join(art, 'cats-edit-input.png'));
  await sharp(source('dafeiyu')).extract({ left: 300, top: 290, width: 460, height: 460 }).resize(920, 920).png().toFile(path.join(art, 'dafeiyu-edit-input.png'));
}

if (mode === 'build') {
  const config = JSON.parse(fs.readFileSync(path.join(art, 'patches.json')));
  const records = [];
  const reviews = [];
  const guides = [];
  for (const entry of config) {
    const originalBytes = fs.readFileSync(source(entry.breed));
    const snapshot = path.join(art, 'originals', `${entry.breed}.png`);
    if (fs.existsSync(snapshot) && !fs.readFileSync(snapshot).equals(originalBytes)) throw new Error(`Original changed since imagegen edit; regenerate before rebuilding: ${entry.breed}`);
    const { data: original, info } = await sharp(originalBytes).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    const generatedBytes = fs.readFileSync(path.join(art, entry.generated));
    let generated = sharp(generatedBytes);
    if (entry.atlas) generated = sharp(await generated.resize(entry.atlas.width, entry.atlas.height, { fit: 'fill' }).png().toBuffer()).extract(entry.atlas.tile);
    const edited = await generated.resize(entry.crop.width, entry.crop.height, { fit: 'fill' }).ensureAlpha().raw().toBuffer();
    const output = Buffer.from(original);
    const coverage = new Set();
    for (const box of entry.boxes) {
      for (let row = box.top; row < box.top + box.height; row++) {
        for (let column = box.left; column < box.left + box.width; column++) {
          const distance = Math.min(column - box.left, row - box.top, box.left + box.width - 1 - column, box.top + box.height - 1 - row);
          const weight = Math.min(1, distance / (entry.feather || 3));
          const offset = (row * info.width + column) * 4;
          const editOffset = ((row - entry.crop.top) * entry.crop.width + column - entry.crop.left) * 4;
          if (original[offset + 3] < 200) throw new Error(`Patch touches silhouette: ${entry.breed} ${column},${row}`);
          if (edited[editOffset + 3] < 200) throw new Error(`Generated patch is transparent: ${entry.breed}`);
          for (let channel = 0; channel < 3; channel++) output[offset + channel] = Math.round(original[offset + channel] * (1 - weight) + edited[editOffset + channel] * weight);
          coverage.add(offset / 4);
        }
      }
    }
    let changed = 0;
    let minX = info.width;
    let minY = info.height;
    let maxX = -1;
    let maxY = -1;
    for (let offset = 0; offset < output.length; offset += 4) {
      if (output[offset + 3] !== original[offset + 3]) throw new Error('Alpha changed');
      if (output.subarray(offset, offset + 4).equals(original.subarray(offset, offset + 4))) continue;
      if (!coverage.has(offset / 4)) throw new Error('Change outside eye patches');
      changed++;
      const column = offset / 4 % info.width;
      const row = Math.floor(offset / 4 / info.width);
      minX = Math.min(minX, column); minY = Math.min(minY, row);
      maxX = Math.max(maxX, column); maxY = Math.max(maxY, row);
    }
    const png = await sharp(output, { raw: info }).png().toBuffer();
    const filename = path.join(native, entry.breed, 'body/fishing/0/head-sleep.png');
    const mirrorFile = path.join(mirror, entry.breed, 'body/fishing/0/head-sleep.png');
    const mirrorOriginal = path.join(mirror, entry.breed, 'body/fishing/0/head.png');
    const mirrorSourceMatches = fs.existsSync(mirrorOriginal) ? fs.readFileSync(mirrorOriginal).equals(originalBytes) : null;
    fs.mkdirSync(path.dirname(mirrorFile), { recursive: true });
    fs.writeFileSync(filename, png);
    fs.writeFileSync(mirrorFile, png);
    fs.mkdirSync(path.join(art, 'originals'), { recursive: true });
    fs.writeFileSync(path.join(art, 'originals', `${entry.breed}.png`), originalBytes);
    const reviewCrop = entry.reviewCrop || entry.crop;
    const zoom = await sharp(png).extract(reviewCrop).flatten({ background: '#deded8' }).resize(576, 576).png().toBuffer();
    const before = await sharp(originalBytes).extract(reviewCrop).flatten({ background: '#deded8' }).resize(576, 576).png().toBuffer();
    await sharp({ create: { width: 1152, height: 576, channels: 3, background: '#deded8' } }).composite([{ input: before, left: 0, top: 0 }, { input: zoom, left: 576, top: 0 }]).png().toFile(path.join(art, `${entry.breed}-review.png`));
    const label = Buffer.from(`<svg width="576" height="28"><rect width="576" height="28" fill="#deded8"/><text x="12" y="20" font-family="sans-serif" font-size="16">${entry.breed}: original / sleep</text></svg>`);
    const comparison = await sharp(path.join(art, `${entry.breed}-review.png`)).resize(576, 288).extend({ top: 28, bottom: 0, left: 0, right: 0, background: '#deded8' }).composite([{ input: label, left: 0, top: 0 }]).png().toBuffer();
    reviews.push({ input: comparison, left: records.length % 2 * 576, top: Math.floor(records.length / 2) * 316 });
    const rectangles = entry.boxes.map(box => `<rect x="${box.left - reviewCrop.left}" y="${box.top - reviewCrop.top}" width="${box.width}" height="${box.height}" fill="none" stroke="#e03131" stroke-width="0.6"/>`).join('');
    const guide = Buffer.from(`<svg width="${reviewCrop.width}" height="${reviewCrop.height}">${rectangles}</svg>`);
    const guideCrop = await sharp(originalBytes).extract(reviewCrop).flatten({ background: '#deded8' }).composite([{ input: guide }]).png().toBuffer();
    const guideTile = await sharp(guideCrop).resize(288, 288).extend({ top: 28, bottom: 0, left: 0, right: 0, background: '#deded8' }).composite([{ input: Buffer.from(`<svg width="288" height="28"><text x="12" y="20" font-family="sans-serif" font-size="16">${entry.breed}: patch bounds</text></svg>`), top: 0, left: 0 }]).png().toBuffer();
    guides.push({ input: guideTile, left: records.length % 5 * 288, top: Math.floor(records.length / 5) * 316 });
    records.push({ breed: entry.breed, canvas: [info.width, info.height], sourceSha256: hash(originalBytes), generatedSha256: hash(generatedBytes), outputSha256: hash(png), output: path.relative(root, filename), mirror: path.relative(root, mirrorFile), boxes: entry.boxes, changedPixels: changed, canvasCoveragePercent: 100 * changed / (info.width * info.height), changedBbox: [minX, minY, maxX + 1, maxY + 1], outsidePatchChangedPixels: 0, alphaChangedPixels: 0, mirrorByteIdentical: true, mirrorSourceMatches });
  }
  fs.writeFileSync(path.join(art, 'verification.json'), JSON.stringify(records, null, 2) + '\n');
  await sharp({ create: { width: 1152, height: Math.ceil(records.length / 2) * 316, channels: 3, background: '#deded8' } }).composite(reviews).png().toFile(path.join(art, 'review-contact.png'));
  await sharp({ create: { width: 1440, height: Math.ceil(records.length / 5) * 316, channels: 3, background: '#deded8' } }).composite(guides).png().toFile(path.join(art, 'patch-bounds-contact.png'));
  console.log(JSON.stringify(records.map(({ breed, changedPixels, changedBbox, mirrorSourceMatches }) => ({ breed, changedPixels, changedBbox, mirrorSourceMatches })), null, 2));
}

if (mode === 'verify') {
  const records = JSON.parse(fs.readFileSync(path.join(art, 'verification.json')));
  for (const record of records) {
    const originalBytes = fs.readFileSync(source(record.breed));
    if (hash(originalBytes) !== record.sourceSha256) throw new Error(`Source changed: ${record.breed}`);
    const original = await sharp(originalBytes).ensureAlpha().raw().toBuffer();
    const filename = path.join(root, record.output);
    const outputBytes = fs.readFileSync(filename);
    if (hash(outputBytes) !== record.outputSha256) throw new Error(`Output changed: ${record.breed}`);
    if (!outputBytes.equals(fs.readFileSync(path.join(root, record.mirror)))) throw new Error(`Mirror mismatch: ${record.breed}`);
    const { data: output, info } = await sharp(outputBytes).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    if (info.width !== record.canvas[0] || info.height !== record.canvas[1]) throw new Error('Canvas changed');
    let changed = 0;
    for (let offset = 0; offset < output.length; offset += 4) {
      if (output[offset + 3] !== original[offset + 3]) throw new Error('Alpha changed');
      if (output.subarray(offset, offset + 4).equals(original.subarray(offset, offset + 4))) continue;
      const column = offset / 4 % info.width;
      const row = Math.floor(offset / 4 / info.width);
      if (!record.boxes.some(box => column >= box.left && column < box.left + box.width && row >= box.top && row < box.top + box.height)) throw new Error('Pixels outside eye patch changed');
      changed++;
    }
    if (changed !== record.changedPixels || !changed) throw new Error('Unexpected pixel difference count');
  }
  console.log(`PASS: ${records.length} decoded PNGs; exact original canvas/alpha; zero RGBA changes outside eye bounds; source/output SHA-256 and mirror bytes match.`);
}
