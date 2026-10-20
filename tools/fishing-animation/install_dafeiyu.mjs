import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
export const defaultArtDirectory = path.join(root, 'docs/art/dafeiyu/hd-v2');
const unity = path.join(root, 'fish-social-unity/Assets');
const fishingRoots = [path.join(unity, 'StreamingAssets/Fishing'),
  path.join(root, 'desktop-overlay/OverlayResources/fishing')];
const cozyRoots = [path.join(unity, 'Resources/Desktop/Theme/Cozy'),
  path.join(root, 'desktop-overlay/OverlayResources/hud/cozy')];
const readJson = file => JSON.parse(fs.readFileSync(file, 'utf8'));
const writeJson = (file, value) => fs.writeFileSync(file, JSON.stringify(value, null, 2) + '\n');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const guidFor = file => crypto.createHash('md5').update('fish-social/dafeiyu/' + path.relative(unity, file)).digest('hex');

export function assertPngSize(bytes, width, height, label) {
  assert(bytes.length >= 24 && bytes.subarray(0, 8).equals(Buffer.from('89504e470d0a1a0a', 'hex')) &&
    bytes.toString('ascii', 12, 16) === 'IHDR', `${label}: expected PNG`);
  assert.equal(bytes.readUInt32BE(16), width, `${label}: width`);
  assert.equal(bytes.readUInt32BE(20), height, `${label}: height`);
}

export function dafeiyuCatalog(catalog, runtime) {
  assert.equal(catalog.frameWidth, 320);
  assert.equal(catalog.frameHeight, 256);
  assert.deepEqual(catalog.foot, [264, 450]);
  for (const [skin, scale] of Object.entries(catalog.atlasFrameScale ?? {}))
    assert(skin === 'dafeiyu' || scale === 1, `${skin}: legacy atlas scale must stay 1`);
  assert.deepEqual(Object.keys(catalog.clips).sort(), Object.keys(runtime.clips).sort());
  for (const [clip, definition] of Object.entries(runtime.clips)) {
    assert.deepEqual(catalog.clips[clip], { count: definition.frames.length, playback: definition.playback,
      durationsMs: definition.durationsMs, loopStart: definition.loopStart ?? 0, next: definition.nextClip ?? null }, clip);
  }
  return { ...catalog, skins: [...new Set([...catalog.skins, 'dafeiyu'])],
    atlasFrameScale: { ...catalog.atlasFrameScale, dafeiyu: 2 } };
}

export function validateDafeiyuSources(art, destinations, catalogs, readFile = fs.readFileSync) {
  const read = relative => readFile(path.join(art, relative));
  const manifest = JSON.parse(read('manifest.json'));
  const runtime = JSON.parse(read('pack/dafeiyu/manifest.json'));
  assert.equal(manifest.characterId, 'dafeiyu');
  assert.equal(manifest.uniqueDrawings, 44);
  assert.equal(manifest.frames.length, 63);
  assert.equal(runtime.version, 2);
  assert.equal(runtime.sourceVersion, 19);
  assert.deepEqual(runtime.canvas, [640, 512]);
  assert.equal(runtime.bodyTextureScale, 2);
  assert.deepEqual(runtime.layers, ['body', 'head', 'far_hand', 'rod', 'line', 'fish', 'hands', 'bobber', 'bait']);
  assert.deepEqual(runtime.bodyLayers, ['body', 'head', 'far_hand', 'hands']);
  assert.equal(Object.keys(runtime.clips).length, 16);
  assert.equal(Object.values(runtime.clips).reduce((sum, clip) => sum + clip.frames.length, 0), 74);
  const updatedCatalogs = catalogs.map(catalog => dafeiyuCatalog(catalog, runtime));
  assert(updatedCatalogs.length > 0);
  for (const catalog of updatedCatalogs) assert.deepEqual(catalog, updatedCatalogs[0]);
  let sharedLayersChecked = 0;
  for (const [clip, definition] of Object.entries(runtime.clips)) {
    assertPngSize(read(`atlases/${clip}.png`), 640 * definition.frames.length, 512, clip + ' atlas');
    for (const [index, order] of definition.frames.entries()) {
      assert.deepEqual([...order].sort(), [...runtime.layers].sort(), `${clip}/${index}: layer order`);
      for (const layer of runtime.layers) {
        const body = runtime.bodyLayers.includes(layer);
        const relative = body ? `dafeiyu/body/${clip}/${index}/${layer}.png` : `shared/${clip}/${index}/${layer}.png`;
        const bytes = read('pack/' + relative);
        assertPngSize(bytes, body ? 1280 : 640, body ? 1024 : 512, relative);
        if (body) continue;
        for (const destination of destinations)
          assert.equal(hash(bytes), hash(readFile(path.join(destination, relative))), relative);
        sharedLayersChecked++;
      }
    }
  }
  for (const frame of manifest.frames) {
    assert.equal(typeof frame.composite, 'string', 'Missing frame composite');
    assertPngSize(read(frame.composite), 1280, 1024, frame.composite);
  }
  assertPngSize(read('frames/catch-04/composite.png'), 1280, 1024, 'fishing photo');
  return { runtime, updatedCatalogs, sharedLayersChecked, artworkManifestSha256: hash(read('manifest.json')) };
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

export async function dafeiyuPortrait(sharp, head) {
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
  return sharp(head).extract({ left, top, width: right - left + 1, height: bottom - top + 1 })
    .resize(480, 480, { fit: 'contain', background: '#00000000' })
    .extend({ top: 16, bottom: 16, left: 16, right: 16, background: '#00000000' }).png().toBuffer();
}

export async function installDafeiyu() {
  const require = createRequire(import.meta.url);
  const sharp = require(process.env.SHARP_MODULE || 'sharp');
  const art = defaultArtDirectory;
  const catalogPaths = ['image/cats', 'mobile/assets/cats'].map(directory => path.join(root, directory, 'catalog.json'));
  const { runtime, updatedCatalogs, sharedLayersChecked, artworkManifestSha256 } =
    validateDafeiyuSources(art, fishingRoots, catalogPaths.map(readJson));
  const portrait = await dafeiyuPortrait(sharp, path.join(art, 'pack/dafeiyu/body/ready/0/head.png'));
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
      guid: meta.match(/^guid: (\w+)/m)[1], fileID: 21300000, size: [512, 512], border: [0, 0, 0, 0],
      resourcesPath: 'Desktop/Theme/Cozy/characters/avatars/dafeiyu',
    };
    writeJson(spriteManifestPath, sprites);
  }
  fs.writeFileSync(path.join(root, 'image/profile/cat_avatar_dafeiyu.png'), portrait);
  fs.copyFileSync(path.join(art, 'frames/catch-04/composite.png'), path.join(root, 'image/fishing_photos/cat_fishing_dafeiyu.png'));
  const contractPath = path.join(root, 'desktop-overlay/tests/cozy_source_contract.json');
  const contract = readJson(contractPath);
  delete contract.png_sha256['characters/avatars/dafeiyu.png'];
  contract.png_sha256['OverlayResources/hud/cozy/characters/avatars/dafeiyu.png'] = hash(portrait);
  writeJson(contractPath, contract);

  runtime.characterArtworkManifestSha256 = artworkManifestSha256;
  for (const [index, directory] of fishingRoots.entries()) {
    copyCharacter(path.join(art, 'pack/dafeiyu'), path.join(directory, 'dafeiyu'), index === 0);
    writeJson(path.join(directory, 'dafeiyu/manifest.json'), runtime);
    const inventoryPath = path.join(directory, 'inventory.json');
    const inventory = readJson(inventoryPath);
    if (!inventory.skins.some(skin => skin.id === 'dafeiyu')) inventory.skins.push({ id: 'dafeiyu', name: '大肥鱼' });
    writeJson(inventoryPath, inventory);
  }
  for (const [index, catalogPath] of catalogPaths.entries()) {
    const output = path.join(path.dirname(catalogPath), 'dafeiyu');
    fs.mkdirSync(output, { recursive: true });
    for (const clip of Object.keys(runtime.clips))
      fs.copyFileSync(path.join(art, 'atlases', clip + '.png'), path.join(output, clip + '.png'));
    writeJson(catalogPath, updatedCatalogs[index]);
  }
  const record = { characterId: 'dafeiyu', name: '大肥鱼', installed: true,
    selection: '个人信息 → 编辑资料 → 选择角色 / 头像 → 大肥鱼 → 保存',
    avatarPath: '/image/profile/cat_avatar_dafeiyu.png', avatarSize: [512, 512],
    bodyTextureScale: 2, atlasFrameScale: 2, atlasFrameSize: [640, 512], runtimeClips: 16, runtimeSlots: 74,
    bodyLayerFilesPerClient: 296, unchangedSharedLayersChecked: sharedLayersChecked,
    artworkManifestSha256, source: path.relative(root, art),
    targets: fishingRoots.map(directory => path.relative(root, directory)) };
  console.log(JSON.stringify(record, null, 2));
  return record;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url))
  await installDafeiyu();
