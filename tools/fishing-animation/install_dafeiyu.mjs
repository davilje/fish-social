import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const art = path.join(root, 'docs/art/dafeiyu/full-v1');
const unity = path.join(root, 'fish-social-unity/Assets');
const fishingRoots = [path.join(unity, 'StreamingAssets/Fishing'),
  path.join(root, 'desktop-overlay/OverlayResources/fishing')];
const cozyRoots = [path.join(unity, 'Resources/Desktop/Theme/Cozy'),
  path.join(root, 'desktop-overlay/OverlayResources/hud/cozy')];
const readJson = file => JSON.parse(fs.readFileSync(file, 'utf8'));
const writeJson = (file, value) => fs.writeFileSync(file, JSON.stringify(value, null, 2) + '\n');
const digest = file => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const guidFor = file => crypto.createHash('md5').update('fish-social/dafeiyu/' + path.relative(unity, file)).digest('hex');
const runtime = readJson(path.join(art, 'pack/dafeiyu/manifest.json'));
const manifest = readJson(path.join(art, 'manifest.json'));
assert.equal(manifest.characterId, 'dafeiyu');
assert.equal(manifest.uniqueDrawings, 44);
assert.equal(manifest.frames.length, 63);
assert.equal(Object.keys(runtime.clips).length, 16);
assert.equal(Object.values(runtime.clips).reduce((sum, clip) => sum + clip.frames.length, 0), 74);
let sharedLayersChecked = 0;
for (const [clip, definition] of Object.entries(runtime.clips)) {
  for (const [index] of definition.frames.entries()) {
    for (const layer of runtime.layers) {
      const relative = runtime.bodyLayers.includes(layer) ? `dafeiyu/body/${clip}/${index}/${layer}.png`
        : `shared/${clip}/${index}/${layer}.png`;
      const source = path.join(art, 'pack', relative);
      assert(fs.existsSync(source), relative);
      if (runtime.bodyLayers.includes(layer)) continue;
      for (const destination of fishingRoots) assert.equal(digest(source), digest(path.join(destination, relative)), relative);
      sharedLayersChecked++;
    }
  }
}

function ensureMeta(file, folder = false) {
  if (fs.existsSync(file + '.meta')) return;
  fs.writeFileSync(file + '.meta', `fileFormatVersion: 2\nguid: ${guidFor(file)}\n` +
    (folder ? 'folderAsset: yes\n' : '') +
    'DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n');
}

function copyCharacter(source, destination, withMeta) {
  fs.mkdirSync(destination, { recursive: true });
  if (withMeta) ensureMeta(destination, true);
  for (const entry of fs.readdirSync(source, { withFileTypes: true })) {
    if (entry.name.endsWith('.meta')) continue;
    const input = path.join(source, entry.name);
    const output = path.join(destination, entry.name);
    if (entry.isDirectory()) copyCharacter(input, output, withMeta);
    else {
      fs.copyFileSync(input, output);
      if (withMeta) ensureMeta(output);
    }
  }
}

const head = path.join(art, 'pack/dafeiyu/body/ready/0/head.png');
const { data, info } = await sharp(head).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
let left = info.width, top = info.height, right = -1, bottom = -1;
for (let row = 0; row < info.height; row++) {
  for (let column = 0; column < info.width; column++) {
    if (data[(row * info.width + column) * 4 + 3] < 128) continue;
    left = Math.min(left, column); right = Math.max(right, column);
    top = Math.min(top, row); bottom = Math.max(bottom, row);
  }
}
assert(right >= left && bottom >= top, 'Empty portrait');
const portrait = await sharp(head).extract({ left, top, width: right - left + 1, height: bottom - top + 1 })
  .resize(240, 240, { fit: 'contain', background: '#00000000' })
  .extend({ top: 8, bottom: 8, left: 8, right: 8, background: '#00000000' }).png().toBuffer();
for (const directory of cozyRoots) {
  const output = path.join(directory, 'characters/avatars/dafeiyu.png');
  fs.writeFileSync(output, portrait);
  if (directory === cozyRoots[0] && !fs.existsSync(output + '.meta')) {
    const template = fs.readFileSync(path.join(directory, 'characters/avatars/orange.png.meta'), 'utf8');
    fs.writeFileSync(output + '.meta', template.replace(/^guid: .*$/m, 'guid: ' + guidFor(output)));
  }
  const spriteManifestPath = path.join(directory, 'asset-manifest.json');
  const sprites = readJson(spriteManifestPath);
  const meta = fs.readFileSync(path.join(cozyRoots[0], 'characters/avatars/dafeiyu.png.meta'), 'utf8');
  sprites.spriteAssets['characters/avatars/dafeiyu.png'] = {
    guid: meta.match(/^guid: (\w+)/m)[1], fileID: 21300000, size: [256, 256], border: [0, 0, 0, 0],
    resourcesPath: 'Desktop/Theme/Cozy/characters/avatars/dafeiyu',
  };
  writeJson(spriteManifestPath, sprites);
}
fs.writeFileSync(path.join(root, 'image/profile/cat_avatar_dafeiyu.png'), portrait);
fs.copyFileSync(path.join(art, 'frames/catch-04/composite.png'), path.join(root, 'image/fishing_photos/cat_fishing_dafeiyu.png'));
const contractPath = path.join(root, 'desktop-overlay/tests/cozy_source_contract.json');
const contract = readJson(contractPath);
delete contract.png_sha256['characters/avatars/dafeiyu.png'];
contract.png_sha256['OverlayResources/hud/cozy/characters/avatars/dafeiyu.png'] = digest(path.join(cozyRoots[0], 'characters/avatars/dafeiyu.png'));
writeJson(contractPath, contract);

manifest.installed = true;
writeJson(path.join(art, 'manifest.json'), manifest);
fs.writeFileSync(path.join(art, 'data.js'), 'window.dafeiyu = ' + JSON.stringify(manifest) + ';\n');
runtime.characterArtworkManifestSha256 = digest(path.join(art, 'manifest.json'));
writeJson(path.join(art, 'pack/dafeiyu/manifest.json'), runtime);
for (const [index, directory] of fishingRoots.entries()) {
  copyCharacter(path.join(art, 'pack/dafeiyu'), path.join(directory, 'dafeiyu'), index === 0);
  const inventoryPath = path.join(directory, 'inventory.json');
  const inventory = readJson(inventoryPath);
  if (!inventory.skins.some(skin => skin.id === 'dafeiyu')) inventory.skins.push({ id: 'dafeiyu', name: '大肥鱼' });
  writeJson(inventoryPath, inventory);
}
for (const directory of ['image/cats', 'mobile/assets/cats']) {
  const output = path.join(root, directory, 'dafeiyu');
  fs.mkdirSync(output, { recursive: true });
  const catalogPath = path.join(root, directory, 'catalog.json');
  const catalog = readJson(catalogPath);
  if (!catalog.skins.includes('dafeiyu')) catalog.skins.push('dafeiyu');
  for (const [clip, definition] of Object.entries(runtime.clips)) {
    assert.equal(catalog.clips[clip].count, definition.frames.length, clip);
    fs.copyFileSync(path.join(art, 'atlases', clip + '.png'), path.join(output, clip + '.png'));
  }
  writeJson(catalogPath, catalog);
}
const record = { characterId: 'dafeiyu', name: '大肥鱼', installed: true,
  selection: '个人信息 → 编辑资料 → 选择角色 / 头像 → 大肥鱼 → 保存',
  avatarPath: '/image/profile/cat_avatar_dafeiyu.png', runtimeClips: 16, runtimeSlots: 74,
  bodyLayerFilesPerClient: 296, unchangedSharedLayersChecked: sharedLayersChecked,
  artworkManifestSha256: runtime.characterArtworkManifestSha256,
  targets: fishingRoots.map(directory => path.relative(root, directory)) };
writeJson(path.join(art, 'installation.json'), record);
console.log(JSON.stringify(record, null, 2));
