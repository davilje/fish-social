import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { test } from 'node:test';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { defaultArtDirectory, dafeiyuCatalog, dafeiyuPortrait, validateDafeiyuSources } from './install_dafeiyu.mjs';
import { compositeCatFrame } from './cat_atlas.mjs';

const require = createRequire(import.meta.url);
const ts = require(process.env.TYPESCRIPT_MODULE || 'typescript');
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const catalog = JSON.parse(fs.readFileSync(path.join(root, 'image/cats/catalog.json')));
const layers = ['body', 'head', 'far_hand', 'rod', 'line', 'fish', 'hands', 'bobber', 'bait'];
const bodyLayers = ['body', 'head', 'far_hand', 'hands'];
const runtime = { version: 2, sourceVersion: 19, canvas: [640, 512], bodyTextureScale: 2, layers, bodyLayers,
  clips: Object.fromEntries(Object.entries(catalog.clips).map(([clip, definition]) => [clip, {
    playback: definition.playback, loopStart: definition.loopStart, durationsMs: definition.durationsMs,
    nextClip: definition.next, frames: Array.from({ length: definition.count }, () => layers),
  }])) };

function pngHeader(width, height) {
  const bytes = Buffer.alloc(24);
  Buffer.from('89504e470d0a1a0a', 'hex').copy(bytes);
  bytes.writeUInt32BE(13, 8);
  bytes.write('IHDR', 12);
  bytes.writeUInt32BE(width, 16);
  bytes.writeUInt32BE(height, 20);
  return bytes;
}

function sources() {
  const files = new Map();
  const art = '/test-art';
  const destinations = ['/test-unity', '/test-overlay'];
  const manifest = { characterId: 'dafeiyu', uniqueDrawings: 44,
    frames: Array.from({ length: 63 }, (_, index) => ({ composite: `frames/${index}/composite.png` })) };
  files.set(art + '/manifest.json', Buffer.from(JSON.stringify(manifest)));
  files.set(art + '/pack/dafeiyu/manifest.json', Buffer.from(JSON.stringify(runtime)));
  for (const frame of [...manifest.frames, { composite: 'frames/catch-04/composite.png' }])
    files.set(art + '/' + frame.composite, pngHeader(1280, 1024));
  for (const [clip, definition] of Object.entries(runtime.clips)) {
    files.set(`${art}/atlases/${clip}.png`, pngHeader(640 * definition.frames.length, 512));
    for (const [index] of definition.frames.entries()) {
      for (const layer of layers) {
        const body = bodyLayers.includes(layer);
        const relative = body ? `dafeiyu/body/${clip}/${index}/${layer}.png` : `shared/${clip}/${index}/${layer}.png`;
        const bytes = pngHeader(body ? 1280 : 640, body ? 1024 : 512);
        files.set(`${art}/pack/${relative}`, bytes);
        if (!body) for (const destination of destinations) files.set(`${destination}/${relative}`, bytes);
      }
    }
  }
  return { files, check: () => validateDafeiyuSources(art, destinations, [catalog, catalog], filename => {
    assert(files.has(filename), `Missing file: ${filename}`);
    return files.get(filename);
  }) };
}

function evaluate(file, dependencies) {
  const exports = {};
  const result = ts.transpileModule(fs.readFileSync(path.join(root, file), 'utf8'), {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS,
      jsx: ts.JsxEmit.ReactJSX, esModuleInterop: true }, reportDiagnostics: true,
  });
  assert.equal(result.diagnostics.length, 0);
  new Function('require', 'exports', result.outputText)(specifier => {
    assert(Object.hasOwn(dependencies, specifier), `Unexpected import: ${specifier}`);
    return dependencies[specifier];
  }, exports);
  return exports;
}

test('HD preflight accepts all 16 atlases and 296 body layers without installing or changing its inputs', () => {
  assert.equal(defaultArtDirectory, path.join(root, 'docs/美术/大肥鱼/hd-v2'));
  const fixture = sources();
  const before = new Map([...fixture.files].map(([file, bytes]) => [file, Buffer.from(bytes)]));
  const result = fixture.check();
  assert.equal(result.sharedLayersChecked, 370);
  assert.equal(result.updatedCatalogs[0].atlasFrameScale.dafeiyu, 2);
  assert.equal(result.artworkManifestSha256.length, 64);
  assert.deepEqual(fixture.files, before);
});

test('catalog update changes only Dafeiyu metadata and is idempotent', () => {
  const legacy = structuredClone(catalog);
  delete legacy.atlasFrameScale;
  const before = structuredClone(legacy);
  const updated = dafeiyuCatalog(legacy, runtime);
  assert.deepEqual(legacy, before);
  assert.deepEqual(updated, { ...before, atlasFrameScale: { dafeiyu: 2 } });
  assert.deepEqual(dafeiyuCatalog(updated, runtime), updated);
  assert.throws(() => dafeiyuCatalog({ ...legacy, atlasFrameScale: { orange: 2 } }, runtime));
});

for (const value of [undefined, null, 0, 1, 3, 4, 2.5, '2']) {
  test(`HD installer rejects bodyTextureScale ${String(value)}`, () => {
    const fixture = sources();
    fixture.files.set('/test-art/pack/dafeiyu/manifest.json', Buffer.from(JSON.stringify({ ...runtime, bodyTextureScale: value })));
    assert.throws(fixture.check);
  });
}

for (const layer of bodyLayers) {
  test(`HD installer rejects missing or low-resolution ${layer}`, () => {
    const fixture = sources();
    const file = `/test-art/pack/dafeiyu/body/ready/0/${layer}.png`;
    fixture.files.set(file, pngHeader(640, 512));
    assert.throws(fixture.check, /width/);
    fixture.files.delete(file);
    assert.throws(fixture.check, /Missing file/);
  });
}

for (const [file, width, height] of [
  ['atlases/hooked.png', 3200, 256], ['frames/0/composite.png', 640, 512],
  ['frames/catch-04/composite.png', 640, 512], ['pack/shared/ready/0/rod.png', 1280, 1024],
]) {
  test(`HD installer rejects incorrect dimensions: ${file}`, () => {
    const fixture = sources();
    fixture.files.set('/test-art/' + file, pngHeader(width, height));
    assert.throws(fixture.check, /width/);
  });
}

test('HD installer rejects altered shared artwork before writes', () => {
  const fixture = sources();
  const file = '/test-art/pack/shared/ready/0/rod.png';
  fixture.files.set(file, Buffer.concat([fixture.files.get(file), Buffer.from('changed')]));
  assert.throws(fixture.check, /shared\/ready\/0\/rod.png/);
});

test('512 portrait preserves the existing relative padding and rejects an empty head', async () => {
  const head = await sharp({ create: { width: 1280, height: 1024, channels: 4, background: '#00000000' } })
    .composite([{ input: await sharp({ create: { width: 200, height: 200, channels: 4, background: '#ff0000ff' } })
      .png().toBuffer(), left: 600, top: 200 }]).png().toBuffer();
  const portrait = await dafeiyuPortrait(sharp, head);
  const { data, info } = await sharp(portrait).raw().toBuffer({ resolveWithObject: true });
  assert.equal(info.width, 512);
  assert.equal(info.height, 512);
  assert.equal(data[(15 * 512 + 256) * 4 + 3], 0);
  assert.equal(data[(16 * 512 + 256) * 4 + 3], 255);
  assert.equal(data[(495 * 512 + 256) * 4 + 3], 255);
  assert.equal(data[(496 * 512 + 256) * 4 + 3], 0);
  const empty = await sharp({ create: { width: 2, height: 2, channels: 4, background: '#00000000' } }).png().toBuffer();
  await assert.rejects(() => dafeiyuPortrait(sharp, empty), /Empty portrait/);
});

test('all atlas frames keep identical display dimensions and offsets across legacy and HD catalogs', () => {
  const legacy = { ...catalog, atlasFrameScale: undefined };
  const hd = dafeiyuCatalog(legacy, runtime);
  const { catAtlasLayout } = evaluate('mobile/lib/catAnimation.ts', { '../assets/cats/catalog.json': hd });
  for (const skin of catalog.skins) {
    for (const clip of Object.values(catalog.clips)) {
      for (let frame = 0; frame < clip.count; frame++) {
        const before = catAtlasLayout(skin, clip.count, frame, 120, legacy);
        const after = catAtlasLayout(skin, clip.count, frame);
        const multiplier = skin === 'dafeiyu' ? 2 : 1;
        assert.deepEqual(after, { ...before, sourceWidth: before.sourceWidth * multiplier, sourceHeight: before.sourceHeight * multiplier });
      }
    }
  }
  for (const value of [0, 3, 2.5, '2'])
    assert.throws(() => catAtlasLayout('dafeiyu', 10, 9, 120, { ...hd, atlasFrameScale: { dafeiyu: value } }));
});

test('shared Web/native component renders the last HD atlas frame at the original UI size and foot position', () => {
  const hd = dafeiyuCatalog(catalog, runtime);
  const animation = evaluate('mobile/lib/catAnimation.ts', { '../assets/cats/catalog.json': hd });
  for (const skin of ['dafeiyu', 'orange']) {
    const uri = `/image/cats/${skin}/hooked.png`;
    const { CatAnimation } = evaluate('mobile/components/CatAnimation.tsx', {
      'react': { useEffect() {}, useMemo: callback => callback(),
        useState: initial => [typeof initial === 'object' ? { clip: 'hooked', frame: 9 } : uri, () => {}] },
      'react-native': { Image: 'Image', View: 'View' },
      'react/jsx-runtime': { jsx: (type, props) => ({ type, props }), jsxs: (type, props) => ({ type, props }) },
      '@fish-social/shared': { resolveCatSkinId: () => skin },
      '../assets/cats/catalog.json': hd,
      '../lib/catAnimation': animation,
      '../lib/avatarUrl': { resolveAssetUrl: value => value },
    });
    const tree = CatAnimation({ user: { id: 'test' }, faceLeft: false });
    const viewport = tree.props.children[1];
    const image = viewport.props.children;
    assert.deepEqual(viewport.props.style, { position: 'absolute', left: -13.5, top: -36.375, width: 120, height: 96, overflow: 'hidden' });
    assert.deepEqual(image.props.style, { width: 1200, height: 96, opacity: 1, transform: [{ translateX: -1080 }] });
    assert.equal(image.props.source.uri, uri);
  }
});

test('export compositor preserves logical prop positions and frame layer order at both resolutions', async () => {
  for (const scale of [1, 2]) {
    const body = await sharp({ create: { width: 640 * scale, height: 512 * scale, channels: 4, background: '#ff0000ff' } })
      .png().toBuffer();
    const prop = await sharp({ create: { width: 640, height: 512, channels: 4, background: '#00000000' } })
      .composite([{ input: await sharp({ create: { width: 40, height: 40, channels: 4, background: '#0000ffff' } })
        .png().toBuffer(), left: 100, top: 80 }]).png().toBuffer();
    const manifest = { canvas: [640, 512], bodyLayers: ['body'], ...(scale > 1 ? { bodyTextureScale: scale } : {}) };
    const readLayer = layer => layer === 'body' ? body : prop;
    for (const order of [['body', 'rod'], ['rod', 'body']]) {
      const png = await compositeCatFrame(sharp, manifest, order, readLayer);
      const { data, info } = await sharp(png).raw().toBuffer({ resolveWithObject: true });
      assert.equal(info.width, 640 * scale);
      assert.equal(info.height, 512 * scale);
      const pixel = (column, row) => [...data.subarray((row * info.width + column) * 4, (row * info.width + column) * 4 + 4)];
      assert.deepEqual(pixel(120 * scale, 100 * scale), order[1] === 'rod' ? [0, 0, 255, 255] : [255, 0, 0, 255]);
      assert.deepEqual(pixel(200 * scale, 100 * scale), [255, 0, 0, 255]);
    }
  }
});

test('export compositor rejects low-resolution HD body layers and scaled shared props', async () => {
  const low = await sharp({ create: { width: 640, height: 512, channels: 4, background: '#00000000' } }).png().toBuffer();
  const high = await sharp({ create: { width: 1280, height: 1024, channels: 4, background: '#00000000' } }).png().toBuffer();
  const manifest = { canvas: [640, 512], bodyLayers: ['body'], bodyTextureScale: 2 };
  await assert.rejects(() => compositeCatFrame(sharp, manifest, ['body'], () => low), /body: source width/);
  await assert.rejects(() => compositeCatFrame(sharp, manifest, ['rod'], () => high), /rod: source width/);
});
