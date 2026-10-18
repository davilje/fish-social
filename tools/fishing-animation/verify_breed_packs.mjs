import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const study = path.join(root, 'docs/art/cat-skins');
const unity = path.join(root, 'fish-social-unity/Assets/StreamingAssets/Fishing');
const overlay = path.join(root, 'desktop-overlay/OverlayResources/fishing');
const original = JSON.parse(fs.readFileSync(path.join(unity, 'orange/manifest.json')));
const breeds = JSON.parse(fs.readFileSync(path.join(study, 'breed-definitions.json'))).breeds;
const targets = process.argv[2] ? breeds.filter(breed => breed.id === process.argv[2]) : breeds;
assert(targets.length > 0);
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const results = [];
for (const breed of targets) {
  const artworkFile = path.join(study, `breeds/${breed.id}/painted-poses.json`);
  const artwork = JSON.parse(fs.readFileSync(artworkFile));
  const manifest = JSON.parse(fs.readFileSync(path.join(unity, breed.id, 'manifest.json')));
  assert.equal(artwork.poses.length, 44);
  assert.equal(new Set(artwork.poses.map(pose => pose.sourceSha256)).size, 44);
  for (const pose of artwork.poses) {
    assert.equal(hash(fs.readFileSync(path.join(study, pose.file))), pose.sha256);
    assert(pose.overlap >= 0.9, breed.id + '/' + pose.id);
  }
  assert.equal(manifest.skinId, breed.id);
  assert.equal(manifest.skinKind, 'painted-pose-assets-v4');
  assert.equal(manifest.artworkManifestSha256, hash(fs.readFileSync(artworkFile)));
  assert.deepEqual(manifest.clips, original.clips);
  assert.deepEqual(manifest.canvas, original.canvas);
  assert.deepEqual(manifest.layers, original.layers);
  assert.deepEqual(manifest, JSON.parse(fs.readFileSync(path.join(overlay, breed.id, 'manifest.json'))));
  let layers = 0;
  for (const [clip, definition] of Object.entries(manifest.clips)) {
    for (const [index] of definition.sourceFrames.entries()) {
      for (const layer of manifest.bodyLayers) {
        const relative = `${breed.id}/body/${clip}/${index}/${layer}.png`;
        const actual = fs.readFileSync(path.join(unity, relative));
        assert(actual.equals(fs.readFileSync(path.join(overlay, relative))), relative);
        assert(actual.equals(fs.readFileSync(path.join(study, 'painted-export', relative))), relative);
        assert.equal(actual.readUInt32BE(16), 640);
        assert.equal(actual.readUInt32BE(20), 512);
        const meta = fs.readFileSync(path.join(unity, relative + '.meta'), 'utf8');
        assert(/^guid: [a-f0-9]{32}$/m.test(meta), relative);
        layers++;
      }
    }
  }
  assert.equal(layers, 296);
  const avatar = fs.readFileSync(path.join(root, `image/profile/cat_avatar_${breed.id}.png`));
  assert(avatar.equals(fs.readFileSync(path.join(root, `fish-social-unity/Assets/Resources/Desktop/Theme/Cozy/characters/avatars/${breed.id}.png`))));
  assert.equal(avatar.readUInt32BE(16), 256);
  assert.equal(avatar.readUInt32BE(20), 256);
  assert(fs.existsSync(path.join(root, `image/fishing_photos/cat_fishing_${breed.id}.png`)));
  results.push({ skin: breed.id, uniqueDrawings: 44, runtimeLayers: layers, clientsIdentical: true,
    minimumSilhouetteOverlap: Math.min(...artwork.poses.map(pose => pose.overlap)) });
}
console.log(JSON.stringify({ results, scope: 'Completeness, geometry, metadata, identity and client parity; not visual continuity certification.' }, null, 2));
