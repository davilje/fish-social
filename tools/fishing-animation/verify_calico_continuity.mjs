import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const study = path.join(root, 'docs/art/cat-skins');
const unity = path.join(root, 'fish-social-unity/Assets/StreamingAssets/Fishing');
const overlay = path.join(root, 'desktop-overlay/OverlayResources/fishing');
const artwork = JSON.parse(fs.readFileSync(path.join(study, 'painted-poses.json')));
const manifest = JSON.parse(fs.readFileSync(path.join(unity, 'calico/manifest.json')));
const original = JSON.parse(fs.readFileSync(path.join(unity, 'orange/manifest.json')));
assert.deepEqual(manifest.clips, original.clips, 'Action timings and ordering must not change');
assert.deepEqual(manifest, JSON.parse(fs.readFileSync(path.join(overlay, 'calico/manifest.json'))));

let layerCount = 0;
for (const [clip, definition] of Object.entries(manifest.clips)) {
  for (const [index] of definition.sourceFrames.entries()) {
    for (const layer of manifest.bodyLayers) {
      const relative = `calico/body/${clip}/${index}/${layer}.png`;
      const actual = fs.readFileSync(path.join(unity, relative));
      assert(actual.equals(fs.readFileSync(path.join(overlay, relative))), relative);
      assert(actual.equals(fs.readFileSync(path.join(study, 'painted-export', relative))), relative);
      layerCount++;
    }
  }
}
assert.equal(layerCount, 296);

const earChecks = [];
for (const [id, left, right] of [
  ['catch-01', [228, 246], [320, 265]],
  ['reset_catch-02', [240, 308], [330, 288]],
]) {
  const pixels = await sharp(path.join(study, 'calico-character', id + '.png')).ensureAlpha().raw().toBuffer();
  const read = ([column, row]) => [...pixels.subarray((row * 640 + column) * 4, (row * 640 + column) * 4 + 4)];
  const orange = read(left);
  const charcoal = read(right);
  assert(orange[3] > 240 && orange[0] > 180 && orange[0] - orange[2] > 100, id + ' left ear patch must be orange');
  assert(charcoal[3] > 240 && Math.max(...charcoal.slice(0, 3)) < 110, id + ' right ear patch must be charcoal');
  earChecks.push({ id, leftPatch: 'orange', rightPatch: 'charcoal' });
}

let minimumPoseOverlap = 1;
for (const pose of artwork.poses.filter(pose => pose.previousFile)) {
  const raw = file => sharp(path.join(study, file)).resize(640, 512, { fit: 'fill' }).ensureAlpha().raw().toBuffer();
  const [before, after] = await Promise.all([raw(pose.previousFile), raw(pose.file)]);
  let intersection = 0;
  let union = 0;
  for (let offset = 3; offset < before.length; offset += 4) {
    if (before[offset] >= 128 || after[offset] >= 128) union++;
    if (before[offset] >= 128 && after[offset] >= 128) intersection++;
  }
  minimumPoseOverlap = Math.min(minimumPoseOverlap, intersection / union);
  assert(intersection / union >= 0.95, pose.id + ' geometry drift after coat retouch');
}
console.log(JSON.stringify({ layerCount, runtimeAssetsIdentical: true, minimumPoseOverlap, earChecks,
  scope: 'Asset regressions only; silhouette overlap does not certify temporal coat continuity.' }, null, 2));
