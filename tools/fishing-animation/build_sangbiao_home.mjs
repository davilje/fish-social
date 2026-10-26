import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const art = path.join(root, 'docs/美术/猫咪形象/sangbiao');
const roots = ['fish-social-unity/Assets/Resources/Desktop/Theme/Cozy', 'desktop-overlay/OverlayResources/hud/cozy', 'fish-social-unity/FishSocialOverlay/OverlayResources/hud/cozy'].map(directory => path.join(root, directory));
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const guid = file => crypto.createHash('md5').update('sangbiao-home/' + file).digest('hex');
const write = (file, bytes) => { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, bytes); };
const save = (file, value) => write(file, JSON.stringify(value, null, 2) + '\n');
const install = process.argv.includes('--install');
const assets = [];
for (const [pose, sourceId, meaning] of [['rest', 'sit-03', 'standing'], ['sleep', 'sit-00', 'seated; legacy resource key, not sleeping artwork']]) {
  const source = path.join(art, 'painted', sourceId + '.png');
  const record = JSON.parse(fs.readFileSync(path.join(art, 'records', sourceId + '.json')));
  assert.equal(hash(fs.readFileSync(source)), record.sha256);
  const { data, info } = await sharp(source).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  let left = info.width, top = info.height, right = 0, bottom = 0;
  for (let row = 0; row < info.height; row++) for (let column = 0; column < info.width; column++) {
    if (data[(row * info.width + column) * 4 + 3] < 8) continue;
    left = Math.min(left, column); top = Math.min(top, row); right = Math.max(right, column); bottom = Math.max(bottom, row);
  }
  const targetWidth = Math.round((right - left + 1) * .9), targetHeight = Math.round((bottom - top + 1) * .9);
  assert(targetWidth <= 384 && targetHeight <= 295);
  const crop = await sharp(source).extract({ left, top, width: right - left + 1, height: bottom - top + 1 }).resize(targetWidth, targetHeight).png().toBuffer();
  const bytes = await sharp({ create: { width: 384, height: 328, channels: 4, background: '#00000000' } }).composite([{ input: crop, left: Math.round((384 - targetWidth) / 2), top: 295 - targetHeight }]).png().toBuffer();
  const relativePath = `journal/characters/sangbiao/${pose}.png`;
  write(path.join(art, 'home', pose + '.png'), bytes);
  const entry = { skin: 'sangbiao', pose, meaning, relativePath, resourcesPath: relativePath.replace(/\.png$/, '').replace(/^/, 'Desktop/Theme/Cozy/'), width: 384, height: 328, pivot: [.5, .1], baseline: 295, source: path.relative(root, source), sourceSha256: record.sha256, exportScale: .9, generation: 'reuse of built-in image_gen fishing artwork; uniform export only', sha256: hash(bytes), guid: guid(relativePath), status: 'static-resource-ready' };
  assets.push(entry);
  if (install) for (const [index, directory] of roots.entries()) {
    const file = path.join(directory, relativePath);
    write(file, bytes);
    if (index === 0) {
      const folder = path.dirname(file);
      if (!fs.existsSync(folder + '.meta')) write(folder + '.meta', `fileFormatVersion: 2\nguid: ${guid('folder')}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
      const template = fs.readFileSync(path.join(directory, 'journal/characters/calico', pose + '.png.meta'), 'utf8');
      if (!fs.existsSync(file + '.meta')) write(file + '.meta', template.replace(/^guid: .*$/m, 'guid: ' + entry.guid).replace(/spriteID: [a-f0-9]+/, 'spriteID: ' + guid(relativePath + '/sprite')));
    }
    const manifestFile = path.join(directory, 'asset-manifest.json');
    if (fs.existsSync(manifestFile)) {
      const manifest = JSON.parse(fs.readFileSync(manifestFile));
      manifest.spriteAssets[relativePath] = { guid: entry.guid, fileID: 21300000, size: [384, 328], border: [0, 0, 0, 0], resourcesPath: entry.resourcesPath };
      save(manifestFile, manifest);
    }
  }
}
const coverage = { skin: 'sangbiao', installed: install, assets, standing: 'painted sit-03', seated: 'painted sit-00', orangeFallbackPrevented: install,
  sleepCurl: 'missing; no home-room/poses/sangbiao-sleep.png installed; current home retains same-breed static pose at bed',
  animations: { walk: false, eat: false, petRoll: false },
  behavior: 'CozyHomeAnimation.Find returns null for missing same-skin actions. CanWalk and CanFeed remain false. Static movement remains supported; no other breed clips registered.',
  requiredNewArtwork: ['registered curled sleeping pose', 'walk-start/walk/walk-stop left and right', 'eating left and right with mouth contacts', 'pet-roll sequence'],
  journalCoverage: 'Only rest and legacy sleep/seated keys provided; six-pose journal set incomplete.',
  unityPlayerTested: false };
save(path.join(art, 'home-coverage.json'), coverage);
console.log(JSON.stringify(coverage, null, 2));
