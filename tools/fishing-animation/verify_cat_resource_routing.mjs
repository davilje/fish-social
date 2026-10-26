import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { assertPngSize } from './install_dafeiyu.mjs';

const require = createRequire(import.meta.url);
const ts = require(process.env.TYPESCRIPT_MODULE || 'typescript');
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const catalog = JSON.parse(fs.readFileSync(path.join(root, 'image/cats/catalog.json')));
const read = relative => fs.readFileSync(path.join(root, relative));
const modules = new Map();
function evaluate(relative) {
  if (modules.has(relative)) return modules.get(relative);
  const exports = {};
  const compiled = ts.transpileModule(read(relative).toString(), {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS, esModuleInterop: true },
    reportDiagnostics: true,
  });
  assert.equal(compiled.diagnostics.length, 0, relative);
  new Function('require', 'exports', compiled.outputText)(specifier => {
    if (specifier === '@fish-social/shared') return { ...evaluate('shared/defaultAvatars.ts'), ...evaluate('shared/fishingPhotos.ts') };
    if (specifier.endsWith('catalog.json')) return catalog;
    return evaluate(path.posix.join(path.posix.dirname(relative), specifier + '.ts'));
  }, exports);
  modules.set(relative, exports);
  return exports;
}
const avatars = evaluate('shared/defaultAvatars.ts');
const photos = evaluate('shared/fishingPhotos.ts');
const { CatAnimationPlayer, catAtlasLayout } = evaluate('mobile/lib/catAnimation.ts');
assert.equal(avatars.DEFAULT_AVATARS.length, 10);
assert.equal(avatars.DEFAULT_AVATARS.find(avatar => avatar.id === 'dafeiyu').label, '大肥鱼');
evaluate('server/src/defaultAvatarCatalog.ts').assertDefaultAvatarCatalog();
assert.deepEqual(new Set(catalog.skins), new Set(avatars.DEFAULT_AVATARS.map(avatar => avatar.id)));
assert(read('image/cats/catalog.json').equals(read('mobile/assets/cats/catalog.json')));
assert.equal(catalog.frameWidth, 320);
assert.equal(catalog.frameHeight, 256);
assert.deepEqual(catalog.foot, [264, 450]);
for (const [skin, scale] of Object.entries(catalog.atlasFrameScale ?? {})) {
  assert(catalog.skins.includes(skin), skin);
  assert(skin === 'dafeiyu' ? scale === 1 || scale === 2 : scale === 1, skin + ': atlas scale');
}
const program = ts.createProgram([
  path.join(root, 'shared/defaultAvatars.ts'), path.join(root, 'shared/fishingPhotos.ts'),
  path.join(root, 'mobile/lib/catAnimation.ts'),
], { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS, moduleResolution: ts.ModuleResolutionKind.Node10,
  resolveJsonModule: true, esModuleInterop: true, strict: true, skipLibCheck: true, noEmit: true,
  baseUrl: root, paths: { '@fish-social/shared': ['shared/index.ts'] } });
const diagnostics = ts.getPreEmitDiagnostics(program);
assert.equal(diagnostics.length, 0, ts.formatDiagnosticsWithColorAndContext(diagnostics, {
  getCurrentDirectory: () => root, getCanonicalFileName: name => name, getNewLine: () => '\n',
}));
for (const file of ['mobile/components/CatAnimation.tsx', 'mobile/components/PondCharacter.tsx', 'server/src/createApp.ts']) {
  const result = ts.transpileModule(read(file).toString(), {
    compilerOptions: { target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX }, reportDiagnostics: true,
  });
  assert.equal(result.diagnostics.length, 0, file);
}
for (const retired of ['gray', 'siamese', 'tuxedo']) {
  const avatar = `/image/profile/cat_avatar_${retired}.png`;
  const photo = `/image/fishing_photos/cat_fishing_${retired}.png`;
  assert.equal(avatars.resolveCatSkinId(avatar, 'player'), 'orange');
  assert.equal(avatars.normalizeCatAssetPath(avatar), '/image/profile/cat_avatar_orange.png');
  assert.equal(photos.resolveFishingPhotoPath(photo), '/image/fishing_photos/cat_fishing_orange.png');
  assert.equal(photos.resolveFishingPhotoPath(avatar), '/image/fishing_photos/cat_fishing_orange.png');
  assert(!fs.existsSync(path.join(root, avatar.slice(1))));
  assert(!fs.existsSync(path.join(root, photo.slice(1))));
}
for (const player of ['player', 'angler-42', '小橘', '🐱', '']) {
  assert(catalog.skins.includes(avatars.resolveCatSkinId('data:image/png;base64,test', player)));
}
for (const avatar of avatars.DEFAULT_AVATARS) {
  const skin = avatar.id;
  assert(avatars.isDefaultAvatarPath(`/image/profile/${avatar.filename}`));
  assert.equal(avatars.resolveCatSkinId(`/image/profile/${avatar.filename}`), skin);
  assert.equal(photos.resolveFishingPhotoPath(`/image/profile/${avatar.filename}`), `/image/fishing_photos/cat_fishing_${skin}.png`);
  const image = read(`image/profile/${avatar.filename}`);
  assert(image.equals(read(`fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/characters/avatars/${skin}.png`)));
  assert(image.equals(read(`desktop-overlay/OverlayResources/hud/cozy/characters/avatars/${skin}.png`)));
  const source = `fish-social-unity/Assets/StreamingAssets/Fishing`;
  const manifest = JSON.parse(read(`${source}/${skin}/manifest.json`));
  assert.deepEqual(manifest, JSON.parse(read(`desktop-overlay/OverlayResources/fishing/${skin}/manifest.json`)));
  assert.deepEqual(manifest.canvas, [640, 512]);
  const bodyScale = Object.hasOwn(manifest, 'bodyTextureScale') ? manifest.bodyTextureScale : 1;
  assert(Number.isInteger(bodyScale) && [1, 2, 4].includes(bodyScale), skin + ': bodyTextureScale');
  const atlasScale = catalog.atlasFrameScale?.[skin] ?? 1;
  if (skin === 'dafeiyu') assert.equal(atlasScale, bodyScale > 1 ? 2 : 1, skin + ': atlas/body contract');
  else assert.equal(bodyScale, 1, skin + ': legacy body scale');
  if (skin === 'dafeiyu') assertPngSize(image, bodyScale > 1 ? 512 : 256, bodyScale > 1 ? 512 : 256, skin + ' avatar');
  for (const directory of ['fish-social-unity/Assets/Resources/Desktop/Theme/Cozy', 'desktop-overlay/OverlayResources/hud/cozy']) {
    const sprites = JSON.parse(read(`${directory}/asset-manifest.json`));
    assert.deepEqual(sprites.spriteAssets[`characters/avatars/${skin}.png`].size,
      [image.readUInt32BE(16), image.readUInt32BE(20)], skin + ': avatar manifest size');
  }
  const frameWidth = catalog.frameWidth * atlasScale;
  const frameHeight = catalog.frameHeight * atlasScale;
  for (const [clip, definition] of Object.entries(catalog.clips)) {
    const png = read(`image/cats/${skin}/${clip}.png`);
    assertPngSize(png, frameWidth * definition.count, frameHeight, skin + '/' + clip);
    if (skin === 'dafeiyu') assert(png.equals(read(`mobile/assets/cats/${skin}/${clip}.png`)), clip + ': mobile mirror');
    const layout = catAtlasLayout(skin, definition.count, definition.count - 1);
    assert.equal(layout.sourceWidth, frameWidth * definition.count);
    assert.equal(layout.sourceHeight, frameHeight);
    assert.equal(layout.width, 120);
    assert.equal(layout.height, 96);
    assert.equal(layout.atlasWidth, 120 * definition.count);
    assert.equal(layout.translateX, -(definition.count - 1) * 120);
    assert.deepEqual(definition.durationsMs, manifest.clips[clip].durationsMs);
    assert.equal(definition.next, manifest.clips[clip].nextClip ?? null);
    for (const [index] of manifest.clips[clip].frames.entries()) {
      for (const layer of manifest.layers) {
        const relative = manifest.bodyLayers.includes(layer) ? `${skin}/body/${clip}/${index}/${layer}.png`
          : `shared/${clip}/${index}/${layer}.png`;
        const bytes = read(`${source}/${relative}`);
        const scale = manifest.bodyLayers.includes(layer) ? bodyScale : 1;
        assertPngSize(bytes, 640 * scale, 512 * scale, relative);
        assert(bytes.equals(read(`desktop-overlay/OverlayResources/fishing/${relative}`)), relative);
      }
    }
  }
  for (const clip of ['ready', 'catch', 'reel_empty']) {
    const index = manifest.clips[clip].frames.length - 1;
    const layers = await Promise.all(manifest.clips[clip].frames[index].map(async layer => {
      const body = manifest.bodyLayers.includes(layer);
      let input = read(`${source}/` + (body ? `${skin}/body/${clip}/${index}/${layer}.png` : `shared/${clip}/${index}/${layer}.png`));
      if (!body && bodyScale > 1) input = await sharp(input).resize(640 * bodyScale, 512 * bodyScale).png().toBuffer();
      return { input, left: 0, top: 0 };
    }));
    const composed = await sharp({ create: { width: 640 * bodyScale, height: 512 * bodyScale, channels: 4, background: '#00000000' } })
      .composite(layers).png().toBuffer();
    const expected = await sharp(composed).resize(frameWidth, frameHeight).raw().toBuffer();
    const actual = await sharp(read(`image/cats/${skin}/${clip}.png`))
      .extract({ left: index * frameWidth, top: 0, width: frameWidth, height: frameHeight }).raw().toBuffer();
    for (let offset = 0; offset < expected.length; offset += 4) {
      assert.equal(expected[offset + 3], actual[offset + 3], skin + '/' + clip + ': alpha');
      for (let channel = 0; channel < 3; channel++) {
        const difference = Math.abs(expected[offset + channel] - actual[offset + channel]) * expected[offset + 3] / 255;
        assert(difference <= 1, skin + '/' + clip + ': visible atlas pixel differs from ordered layers');
      }
    }
  }
}
const player = new CatAnimationPlayer();
player.request('seated');
assert.equal(player.clip, 'sit');
player.advance(2000);
assert.equal(player.clip, 'ready');
player.request('hooked');
player.advance(10000);
assert.equal(player.clip, 'hooked');
assert(player.frame >= catalog.clips.hooked.loopStart);
player.request('resolving', 'catch');
player.request('seated');
assert.equal(player.clip, 'reel');
player.advance(10000);
assert.equal(player.clip, 'ready');
player.request('resolving', 'escape');
player.advance(10000);
assert.equal(player.clip, 'ready');
player.request('groundbaiting');
assert.equal(player.clip, 'groundbait');
player.advance(10000);
assert.equal(player.clip, 'ready');
player.request('stopping');
assert.equal(player.clip, 'leave');
player.advance(10000);
assert.equal(player.clip, 'idle');
for (const file of ['desktop-overlay/OverlayPetActor.cs', 'fish-social-unity/Assets/Scripts/Desktop/Pet/PetArtLoader.cs']) {
  const code = read(file).toString();
  assert(!code.includes('"pets"') && !code.includes('"Pet"') && !code.includes('BuildPlaceholder'), file);
}
const report = JSON.parse(read('docs/美术/猫咪形象/resource-cleanup.json'));
for (const removed of report.removed) assert(!fs.existsSync(path.join(root, removed.file)), removed.file);
console.log(JSON.stringify({ skins: avatars.DEFAULT_AVATARS.length,
  atlases: avatars.DEFAULT_AVATARS.length * Object.keys(catalog.clips).length,
  frameSlots: avatars.DEFAULT_AVATARS.length * Object.values(catalog.clips).reduce((sum, clip) => sum + clip.count, 0),
  sampledComposites: avatars.DEFAULT_AVATARS.length * 3,
  removedFiles: report.removed.length, result: 'passed' }, null, 2));
