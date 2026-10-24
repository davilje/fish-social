import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { compositeCatFrame } from './cat_atlas.mjs';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const study = path.join(root, 'docs/art/cat-skins');
const art = path.join(study, 'sangbiao');
const unity = path.join(root, 'fish-social-unity/Assets');
const fishingRoots = [path.join(unity, 'StreamingAssets/Fishing'),
  path.join(root, 'desktop-overlay/OverlayResources/fishing'),
  path.join(root, 'fish-social-unity/FishSocialOverlay/OverlayResources/fishing')];
const cozyRoots = [path.join(unity, 'Resources/Desktop/Theme/Cozy'),
  path.join(root, 'desktop-overlay/OverlayResources/hud/cozy'),
  path.join(root, 'fish-social-unity/FishSocialOverlay/OverlayResources/hud/cozy')];
const pack = path.join(art, 'pack/sangbiao');
const read = file => JSON.parse(fs.readFileSync(file, 'utf8'));
const save = (file, value) => write(file, JSON.stringify(value, null, 2) + '\n');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const guid = file => crypto.createHash('md5').update('fish-social/sangbiao/' + path.relative(unity, file)).digest('hex');
const source = read(path.join(fishingRoots[0], 'orange/manifest.json'));
const poses = read(path.join(art, 'source-index.json')).poses;
const frames = read(path.join(study, 'full-pose-manifest.json')).frames;
const [width, height] = source.canvas;
const install = process.argv.includes('--install');
const existingSleepFiles = fishingRoots.map(directory => path.join(directory, 'sangbiao/body/fishing/0/head-sleep.png'))
  .filter(file => fs.existsSync(file)).map(file => ({ file, sha256: hash(fs.readFileSync(file)) }));

function write(file, value) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, value);
}

function metadata(file, folder = false) {
  if (fs.existsSync(file + '.meta')) return;
  write(file + '.meta', `fileFormatVersion: 2\nguid: ${guid(file)}\n${folder ? 'folderAsset: yes\n' : ''}DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
}

function copyTree(input, output, withMeta = false) {
  fs.mkdirSync(output, { recursive: true });
  if (withMeta) metadata(output, true);
  for (const entry of fs.readdirSync(input, { withFileTypes: true })) {
    if (entry.name.endsWith('.meta')) continue;
    const target = path.join(output, entry.name);
    if (entry.isDirectory()) copyTree(path.join(input, entry.name), target, withMeta);
    else {
      fs.copyFileSync(path.join(input, entry.name), target);
      if (withMeta) metadata(target);
    }
  }
}

async function raw(file) {
  const info = await sharp(file).metadata();
  assert.equal(info.width, width, file + ': width');
  assert.equal(info.height, height, file + ': height');
  assert(info.hasAlpha, file + ': alpha');
  return sharp(file).ensureAlpha().raw().toBuffer();
}

function partition(drawing, masks) {
  const layers = masks.map(() => Buffer.alloc(drawing.length));
  for (let offset = 0; offset < drawing.length; offset += 4) {
    if (!drawing[offset + 3]) continue;
    let owner = 0, weight = 0;
    for (let index = 0; index < masks.length; index++) {
      if (masks[index][offset + 3] > 0 && masks[index][offset + 3] >= weight) {
        owner = index;
        weight = masks[index][offset + 3];
      }
    }
    if (!weight) {
      const column = offset / 4 % width;
      const row = Math.floor(offset / 4 / width);
      let distance = Infinity;
      for (let vertical = -6; vertical <= 6; vertical++) {
        for (let horizontal = -6; horizontal <= 6; horizontal++) {
          const nextColumn = column + horizontal, nextRow = row + vertical;
          const squared = horizontal * horizontal + vertical * vertical;
          if (nextColumn < 0 || nextColumn >= width || nextRow < 0 || nextRow >= height || squared >= distance) continue;
          const next = (nextRow * width + nextColumn) * 4 + 3;
          for (let index = 0; index < masks.length; index++) {
            if (masks[index][next] >= 128) { owner = index; distance = squared; }
          }
        }
      }
    }
    drawing.copy(layers[owner], offset, offset, offset + 4);
  }
  for (let offset = 0; offset < drawing.length; offset += 4) {
    if (!drawing[offset + 3]) continue;
    const owners = layers.filter(layer => layer[offset + 3] > 0);
    assert.equal(owners.length, 1, 'Layer partition duplicated or lost a pixel');
    assert(owners[0].subarray(offset, offset + 4).equals(drawing.subarray(offset, offset + 4)));
  }
  return layers;
}

const waitingRegistration = path.join(art, 'waiting-head-registration.json');
if (process.argv.includes('--waiting-head')) {
  assert(!install, 'Waiting-head registration does not install or advertise a full pack');
  const frame = frames.find(entry => entry.id === 'wait-00');
  const record = read(path.join(art, 'records/wait-00.json'));
  const painted = path.join(art, 'painted/wait-00.png');
  assert.equal(hash(fs.readFileSync(painted)), record.sha256);
  assert(record.overlap >= 0.9 && Math.abs(record.foot.deltaY) <= 6);
  const drawing = await raw(painted);
  const masks = await Promise.all(source.bodyLayers.map(layer => raw(path.join(study, frame.layers[layer]))));
  const pixels = partition(drawing, masks)[source.bodyLayers.indexOf('head')];
  const head = await sharp(pixels, { raw: { width, height, channels: 4 } }).png().toBuffer();
  if (fs.existsSync(waitingRegistration)) assert.equal(read(waitingRegistration).headSha256, hash(head), 'Registered waiting head changed; coordinate sleep artwork before replacement');
  write(path.join(art, 'frames/wait-00/head.png'), head);
  const targets = [];
  for (const [index, destination] of fishingRoots.entries()) {
    const folder = path.join(destination, 'sangbiao/body/fishing/0');
    const file = path.join(folder, 'head.png');
    write(file, head);
    if (index === 0) {
      for (let directory = folder; directory !== destination; directory = path.dirname(directory)) metadata(directory, true);
      metadata(file);
    }
    targets.push(path.relative(root, file));
  }
  save(waitingRegistration, { characterId: 'sangbiao', source: 'painted/wait-00.png',
    sourceSha256: record.sha256, headSha256: hash(head), canvas: [width, height],
    method: 'Same source masks and pixel partition as full export; locked against later replacement.',
    completePackInstalled: false, catalogRegistered: false, targets });
  console.log(JSON.stringify(read(waitingRegistration), null, 2));
  process.exit(0);
}

assert.deepEqual(source.canvas, [640, 512]);
assert.deepEqual(source.place.foot, [264, 450]);
assert.equal(poses.length, 44);
assert.equal(frames.length, 63);
const records = poses.map(pose => {
  const record = read(path.join(art, 'records', pose.id + '.json'));
  assert.equal(record.method, 'imagegen-pose-edit');
  assert.equal(record.sourceSha256, pose.sourceSha256);
  assert.equal(hash(fs.readFileSync(path.join(art, pose.output))), record.sha256);
  assert.equal(hash(fs.readFileSync(path.join(art, 'originals', pose.id + '.png'))), record.originalSha256);
  assert(record.prompt.trim().length > 100);
  assert(record.overlap >= 0.9, pose.id + ': silhouette IoU below 0.9');
  assert(Math.abs(record.foot.deltaY) <= 6, pose.id + ': foot bottom drift exceeds 6px');
  assert(record.transparentPixels > width * height / 2);
  return record;
});
const byHash = new Map(records.map(record => [record.sourceSha256, record]));
const byKey = new Map(frames.map(frame => [frame.clip + ':' + frame.index, frame]));
const rendered = new Map();
const frameReport = [];
for (const frame of frames) {
  const record = byHash.get(hash(fs.readFileSync(path.join(study, frame.sourceCharacter))));
  assert(record, 'Missing artwork for ' + frame.id);
  const drawing = await raw(path.join(art, 'painted', record.id + '.png'));
  const masks = await Promise.all(source.bodyLayers.map(layer => raw(path.join(study, frame.layers[layer]))));
  const layers = partition(drawing, masks);
  const pngs = await Promise.all(layers.map(layer => sharp(layer, { raw: { width, height, channels: 4 } }).png().toBuffer()));
  if (frame.id === 'wait-00' && fs.existsSync(waitingRegistration))
    assert.equal(hash(pngs[source.bodyLayers.indexOf('head')]), read(waitingRegistration).headSha256, 'Registered waiting head changed; preserve sleep artwork alignment');
  for (const [index, layer] of source.bodyLayers.entries()) write(path.join(art, 'frames', frame.id, layer + '.png'), pngs[index]);
  write(path.join(art, 'frames', frame.id, 'character.png'), fs.readFileSync(path.join(art, 'painted', record.id + '.png')));
  rendered.set(frame.id, pngs);
  frameReport.push({ id: frame.id, artwork: record.id, overlap: record.overlap, foot: record.foot });
}

let runtimeSlots = 0, sharedChecked = 0;
const runtime = { ...source, skinId: 'sangbiao', skinName: '狸花猫「丧彪」', skinKind: 'painted-pose-assets-v4',
  artworkManifestSha256: hash(Buffer.from(JSON.stringify(records))), bodyTextureScale: 1 };
const preview = { manifest: runtime, clips: {}, frames: frameReport };
const sharedHashes = {};
for (const [clip, definition] of Object.entries(runtime.clips)) {
  const atlasFrames = [];
  preview.clips[clip] = [];
  for (const [index, sourceFrame] of definition.sourceFrames.entries()) {
    const frame = byKey.get(sourceFrame.clip + ':' + sourceFrame.index);
    assert(frame, 'Unknown source frame');
    const pngs = rendered.get(frame.id);
    const order = definition.frames[index];
    assert.deepEqual([...order].sort(), [...runtime.layers].sort());
    for (const [layerIndex, layer] of runtime.bodyLayers.entries())
      write(path.join(pack, 'body', clip, String(index), layer + '.png'), pngs[layerIndex]);
    const image = await compositeCatFrame(sharp, runtime, order, async (layer, body) => {
      if (body) return pngs[runtime.bodyLayers.indexOf(layer)];
      const relative = `shared/${clip}/${index}/${layer}.png`;
      const bytes = fs.readFileSync(path.join(fishingRoots[0], relative));
      const digest = hash(bytes);
      for (const destination of fishingRoots) assert.equal(hash(fs.readFileSync(path.join(destination, relative))), digest, relative + ': shared assets differ');
      sharedHashes[relative] = digest;
      sharedChecked++;
      return bytes;
    });
    write(path.join(art, 'frames', frame.id, 'composite.png'), image);
    const layerPaths = order.map(layer => ({ name: layer, path: runtime.bodyLayers.includes(layer)
      ? `pack/sangbiao/body/${clip}/${index}/${layer}.png`
      : path.relative(art, path.join(fishingRoots[0], 'shared', clip, String(index), layer + '.png')).split(path.sep).join('/') }));
    preview.clips[clip].push({ source: frame.id, layers: layerPaths, image: `frames/${frame.id}/composite.png`, character: `frames/${frame.id}/character.png`,
      original: `../${frame.sourceCharacter}`, duration: definition.durationsMs[index] });
    atlasFrames.push({ input: await sharp(image).resize(320, 256).png().toBuffer(), left: index * 320, top: 0 });
    runtimeSlots++;
  }
  const atlas = await sharp({ create: { width: 320 * atlasFrames.length, height: 256, channels: 4, background: '#00000000' } })
    .composite(atlasFrames).png().toBuffer();
  write(path.join(art, 'atlases', clip + '.png'), atlas);
}
assert.equal(runtimeSlots, 74);
assert.equal(Object.keys(runtime.clips).length, 16);
save(path.join(pack, 'manifest.json'), runtime);
save(path.join(art, 'painted-poses.json'), { version: 4, canvas: [width, height], status: 'geometry-reviewed',
  productionReady: false, poses: records.map(record => ({ ...record, review: 'geometry-reviewed', visualReview: 'pending-platform-acceptance' })) });
write(path.join(art, 'preview-data.js'), 'window.sangbiaoPreview = ' + JSON.stringify(preview) + ';\n');
const contacts = await Promise.all(records.map(async (record, index) => ({
  input: await sharp(path.join(art, 'painted', record.id + '.png')).resize(256, 205).png().toBuffer(),
  left: index % 7 * 256, top: Math.floor(index / 7) * 205 })));
write(path.join(art, 'contact.png'), await sharp({ create: { width: 1792, height: 1435, channels: 4, background: '#e9e1d6' } }).composite(contacts).png().toBuffer());
const fullContacts = await Promise.all(frames.map(async (frame, index) => ({
  input: await sharp(path.join(art, 'frames', frame.id, 'composite.png')).resize(320, 256).png().toBuffer(),
  left: index % 7 * 320, top: Math.floor(index / 7) * 256 })));
write(path.join(art, 'full-contact.png'), await sharp({ create: { width: 2240, height: 2304, channels: 4, background: '#e9e1d6' } }).composite(fullContacts).png().toBuffer());

const head = path.join(pack, 'body/ready/0/head.png');
const headPixels = await raw(head);
let left = width, top = height, right = 0, bottom = 0;
for (let row = 0; row < height; row++) for (let column = 0; column < width; column++) {
  if (headPixels[(row * width + column) * 4 + 3] < 128) continue;
  left = Math.min(left, column); top = Math.min(top, row); right = Math.max(right, column); bottom = Math.max(bottom, row);
}
const portrait = await sharp(head).extract({ left, top, width: right - left + 1, height: bottom - top + 1 })
  .resize(240, 240, { fit: 'contain', background: '#00000000' })
  .extend({ top: 8, bottom: 8, left: 8, right: 8, background: '#00000000' }).png().toBuffer();
write(path.join(art, 'avatar.png'), portrait);
const installation = [];
if (install) {
  for (const [index, destination] of fishingRoots.entries()) {
    copyTree(pack, path.join(destination, 'sangbiao'), index === 0);
    const inventoryFile = path.join(destination, 'inventory.json');
    const inventory = read(inventoryFile);
    if (!inventory.skins.some(skin => skin.id === 'sangbiao')) inventory.skins.push({ id: 'sangbiao', name: '狸花猫「丧彪」' });
    save(inventoryFile, inventory);
    installation.push(path.relative(root, path.join(destination, 'sangbiao')));
  }
  for (const [index, destination] of cozyRoots.entries()) {
    const file = path.join(destination, 'characters/avatars/sangbiao.png');
    write(file, portrait);
    if (index === 0 && !fs.existsSync(file + '.meta')) {
      const template = fs.readFileSync(path.join(destination, 'characters/avatars/orange.png.meta'), 'utf8');
      write(file + '.meta', template.replace(/^guid: .*$/m, 'guid: ' + guid(file)).replace(/spriteID: [a-f0-9]+/, 'spriteID: ' + guid(file + '/sprite')));
    }
    const manifestFile = path.join(destination, 'asset-manifest.json');
    if (fs.existsSync(manifestFile)) {
      const assets = read(manifestFile);
      assets.spriteAssets['characters/avatars/sangbiao.png'] = { guid: guid(path.join(cozyRoots[0], 'characters/avatars/sangbiao.png')),
        fileID: 21300000, size: [256, 256], border: [0, 0, 0, 0], resourcesPath: 'Desktop/Theme/Cozy/characters/avatars/sangbiao' };
      save(manifestFile, assets);
    }
    installation.push(path.relative(root, file));
  }
  write(path.join(root, 'image/profile/cat_avatar_sangbiao.png'), portrait);
  write(path.join(root, 'image/fishing_photos/cat_fishing_sangbiao.png'), fs.readFileSync(path.join(art, 'frames/catch-04/composite.png')));
  for (const directory of ['image/cats', 'mobile/assets/cats']) {
    const destination = path.join(root, directory);
    copyTree(path.join(art, 'atlases'), path.join(destination, 'sangbiao'));
    const catalogFile = path.join(destination, 'catalog.json');
    const catalog = read(catalogFile);
    assert.deepEqual(catalog.foot, runtime.place.foot);
    assert.equal(catalog.frameWidth, 320);
    assert.equal(catalog.frameHeight, 256);
    for (const [clip, definition] of Object.entries(runtime.clips))
      assert.deepEqual(catalog.clips[clip], { count: definition.frames.length, playback: definition.playback,
        durationsMs: definition.durationsMs, loopStart: definition.loopStart ?? 0, next: definition.nextClip ?? null });
    if (!catalog.skins.includes('sangbiao')) catalog.skins.push('sangbiao');
    save(catalogFile, catalog);
    installation.push(directory + '/sangbiao');
  }
}

let installedFilesVerified = 0;
for (const [clip, definition] of Object.entries(runtime.clips)) {
  for (const [index] of definition.frames.entries()) for (const layer of runtime.bodyLayers) {
    const relative = `body/${clip}/${index}/${layer}.png`;
    const digest = hash(fs.readFileSync(path.join(pack, relative)));
    await raw(path.join(pack, relative));
    if (install) for (const destination of fishingRoots) {
      assert.equal(hash(fs.readFileSync(path.join(destination, 'sangbiao', relative))), digest);
      installedFilesVerified++;
    }
  }
}
for (const [relative, digest] of Object.entries(sharedHashes))
  for (const destination of fishingRoots) assert.equal(hash(fs.readFileSync(path.join(destination, relative))), digest);
for (const entry of existingSleepFiles) assert.equal(hash(fs.readFileSync(entry.file)), entry.sha256, 'Existing sleeping artwork changed');
const report = { characterId: 'sangbiao', installed: install, productionReady: false,
  sourceFrames: frames.length, uniqueDrawings: records.length, runtimeClips: 16, runtimeSlots,
  layers: runtime.layers, bodyLayers: runtime.bodyLayers, canvas: runtime.canvas, foot: runtime.place.foot,
  characterLayersPerClient: runtimeSlots * runtime.bodyLayers.length, installedFilesVerified,
  preservedSleepFiles: existingSleepFiles.map(entry => ({ ...entry, file: path.relative(root, entry.file) })),
  sharedLayersChecked: sharedChecked, sharedGearUnchanged: true, pixelPartitionVerified: true,
  minimumSilhouetteIoU: Math.min(...records.map(record => record.overlap)),
  maximumAbsoluteFootDelta: Math.max(...records.map(record => Math.abs(record.foot.deltaY))),
  installation, frames: frameReport,
  limitations: ['Geometry and file checks do not establish perfect stripe continuity or grip occlusion.',
    'Layer names are historical pixel ownership, not an anatomical rig; head can contain the full character.',
    'Unity Player and Windows Overlay visual acceptance still required.'] };
save(path.join(art, 'validation.json'), report);
console.log(JSON.stringify({ ...report, frames: undefined }, null, 2));
