#!/usr/bin/env node
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const source = path.join(repo, 'docs/design/ui/prototype/cozy-journal/assets/home-animation-v1');
const room = path.join(repo, 'fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/home-room');
const target = path.join(room, 'animations');
const sourceManifestBytes = fs.readFileSync(path.join(source, 'manifest.json'));
const sourceManifest = JSON.parse(sourceManifestBytes);
const texturePattern = fs.readFileSync(path.join(room, 'cat-standing.png.meta'), 'utf8');
const pngSignature = Buffer.from('89504e470d0a1a0a', 'hex');
const assets = new Map();
const clipIds = new Set();

function digest(bytes) {
  return createHash('sha256').update(bytes).digest('hex');
}

function guid(assetPath) {
  return createHash('md5').update(`fish-social:home-room-animation:${path.relative(repo, assetPath).split(path.sep).join('/')}`).digest('hex');
}

function pair(value, label) {
  assert(Array.isArray(value) && value.length === 2 && value.every(Number.isInteger), `${label} must contain two integers`);
  return value;
}

function text(value, label) {
  assert(typeof value === 'string' && value.length > 0, `${label} must be a nonempty string`);
  return value;
}

function positive(value, label) {
  assert(Number.isFinite(value) && value > 0, `${label} must be positive`);
  return value;
}

assert(Number.isInteger(sourceManifest.version), 'Missing integer manifest version');
text(sourceManifest.revision, 'revision');
assert(Array.isArray(sourceManifest.clips) && sourceManifest.clips.length > 0, 'Missing clips');
const clips = sourceManifest.clips.map(clip => {
  text(clip.id, 'clip.id');
  assert(!clipIds.has(clip.id), `Duplicate clip: ${clip.id}`);
  clipIds.add(clip.id);
  const canvas = pair(clip.canvas, `${clip.id}.canvas`);
  assert(canvas.every(size => size > 0 && size <= 2048), `Unsupported canvas: ${clip.id}`);
  const registration = pair(clip.registration, `${clip.id}.registration`);
  assert(Array.isArray(clip.frames) && clip.frames.length > 0, `Missing frames: ${clip.id}`);
  const loop = clip.loop ?? [];
  assert(Array.isArray(loop), `Invalid loop: ${clip.id}`);
  if (loop.length > 0) {
    pair(loop, `${clip.id}.loop`);
    assert(loop[0] >= 0 && loop[0] < loop[1] && loop[1] <= clip.frames.length, `Invalid loop range: ${clip.id}`);
  }
  const frames = clip.frames.map(frame => {
    const file = text(frame.file, `${clip.id}.frame.file`);
    assert(/^(orange|shared)\/(?:[a-z0-9-]+\/)*[a-z0-9-]+\.png$/.test(file), `Invalid runtime PNG path: ${file}`);
    if (!assets.has(file)) {
      const bytes = fs.readFileSync(path.join(source, file));
      assert(bytes.length >= 33 && bytes.subarray(0, 8).equals(pngSignature), `Invalid PNG: ${file}`);
      assert.equal(bytes.toString('ascii', 12, 16), 'IHDR', `Missing PNG header: ${file}`);
      assets.set(file, { bytes, hash: digest(bytes), width: bytes.readUInt32BE(16), height: bytes.readUInt32BE(20) });
    }
    const asset = assets.get(file);
    assert.deepEqual([asset.width, asset.height], canvas, `Canvas mismatch: ${file}`);
    const result = { file, durationMs: positive(frame.durationMs, `${file}.durationMs`) };
    if (frame.state !== undefined) result.state = text(frame.state, `${file}.state`);
    if (frame.hold !== undefined) {
      assert.equal(typeof frame.hold, 'boolean', `Invalid hold: ${file}`);
      result.hold = frame.hold;
    }
    return result;
  });
  return {
    id: clip.id,
    skinId: text(clip.skinId, `${clip.id}.skinId`),
    action: text(clip.action, `${clip.id}.action`),
    direction: text(clip.direction, `${clip.id}.direction`),
    canvas,
    registration,
    displayScale: positive(clip.displayScale, `${clip.id}.displayScale`),
    fps: positive(clip.fps, `${clip.id}.fps`),
    loop,
    frames,
    events: (clip.events ?? []).map(event => {
      assert(Number.isInteger(event.frame) && event.frame >= 0 && event.frame < frames.length, `Invalid event frame: ${clip.id}`);
      return { frame: event.frame, type: text(event.type, `${clip.id}.event.type`) };
    }),
  };
});

const output = new Map();
const folders = new Set([target]);
function addAsset(relativePath, bytes, importer) {
  const assetPath = path.join(target, relativePath);
  output.set(assetPath, Buffer.isBuffer(bytes) ? bytes : Buffer.from(bytes));
  output.set(`${assetPath}.meta`, Buffer.from(importer));
  for (let folder = path.dirname(assetPath); folder !== target; folder = path.dirname(folder)) folders.add(folder);
}

for (const [file, asset] of assets) {
  const assetPath = path.join(target, file);
  const meta = texturePattern
    .replace(/^guid: .+$/m, `guid: ${guid(assetPath)}`)
    .replace(/^(\s*textureCompression:) \d+$/gm, '$1 0')
    .replace(/^(\s*spriteID:) .+$/m, `$1 ${guid(`${assetPath}:sprite`)}`);
  for (const [key, value] of Object.entries({ enableMipMap: 0, maxTextureSize: 2048, wrapU: 1, wrapV: 1, wrapW: 1, nPOTScale: 0, spriteMode: 1, spriteMeshType: 0, alphaUsage: 1, alphaIsTransparency: 1, textureType: 8, textureCompression: 0, crunchedCompression: 0, overridden: 0 })) {
    const settings = [...meta.matchAll(new RegExp(`^\\s*${key}: (\\d+)$`, 'gm'))];
    assert(settings.length > 0 && settings.every(setting => Number(setting[1]) === value), `Unexpected texture pattern setting: ${key}`);
  }
  addAsset(file, asset.bytes, meta);
}

const manifest = { version: sourceManifest.version, revision: sourceManifest.revision, clips };
addAsset('manifest.json', `${JSON.stringify(manifest)}\n`, `fileFormatVersion: 2\nguid: ${guid(path.join(target, 'manifest.json'))}\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
for (const folder of folders) {
  output.set(`${folder}.meta`, Buffer.from(`fileFormatVersion: 2\nguid: ${guid(folder)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`));
}

function checkTree(folder) {
  if (!fs.existsSync(folder)) return;
  for (const entry of fs.readdirSync(folder, { withFileTypes: true })) {
    const entryPath = path.join(folder, entry.name);
    if (entry.isDirectory()) {
      assert(folders.has(entryPath), `Unexpected output directory: ${entryPath}`);
      checkTree(entryPath);
    } else {
      assert(entry.isFile() && output.has(entryPath), `Unexpected output file: ${entryPath}`);
    }
  }
}

checkTree(target);
let writtenFiles = 0;
for (const [file, bytes] of output) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  if (!fs.existsSync(file) || !fs.readFileSync(file).equals(bytes)) {
    fs.writeFileSync(file, bytes);
    writtenFiles += 1;
  }
  assert.equal(digest(fs.readFileSync(file)), digest(bytes), `Output verification failed: ${file}`);
}
for (const [file, asset] of assets) {
  assert.equal(digest(fs.readFileSync(path.join(source, file))), asset.hash, `Source changed during export: ${file}`);
}
assert(fs.readFileSync(path.join(source, 'manifest.json')).equals(sourceManifestBytes), 'Source manifest changed during export');
checkTree(target);
console.log(JSON.stringify({
  revision: manifest.revision,
  clips: clips.length,
  frameReferences: clips.reduce((count, clip) => count + clip.frames.length, 0),
  pngs: assets.size,
  folders: folders.size,
  metaFiles: assets.size + 1 + folders.size,
  pngBytes: [...assets.values()].reduce((count, asset) => count + asset.bytes.length, 0),
  manifestSha256: digest(output.get(path.join(target, 'manifest.json'))),
  pngSetSha256: digest([...assets].sort(([left], [right]) => left.localeCompare(right, 'en')).map(([file, asset]) => `${file}\t${asset.hash}\n`).join('')),
  writtenFiles,
  output: target,
}, null, 2));
