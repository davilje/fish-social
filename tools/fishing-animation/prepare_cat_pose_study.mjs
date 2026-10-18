import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const output = path.join(repo, 'docs/art/cat-skins');
const pack = path.join(repo, 'fish-social-unity/Assets/StreamingAssets/Fishing');
const manifest = JSON.parse(fs.readFileSync(path.join(pack, 'orange/manifest.json'), 'utf8'));
const characterLayers = new Set(manifest.bodyLayers);
const config = JSON.parse(fs.readFileSync(path.join(output, 'sources.json'), 'utf8'));
const sha256 = filename => crypto.createHash('sha256').update(fs.readFileSync(filename)).digest('hex');
const frames = config.frames.map(frame => {
  const original = `reference/${frame.id}/cat.png`;
  const candidate = `calico/${frame.id}.png`;
  for (const asset of [original, candidate]) {
    if (!fs.existsSync(path.join(output, asset))) throw new Error(`Missing ${asset}`);
  }
  const order = manifest.clips[frame.clip].frames[frame.index];
  const layers = {};
  for (const layer of order) {
    const source = characterLayers.has(layer)
      ? path.join(pack, 'orange/body', frame.clip, String(frame.index), `${layer}.png`)
      : path.join(pack, 'shared', frame.clip, String(frame.index), `${layer}.png`);
    const relative = `reference/${frame.id}/${layer}.png`;
    fs.mkdirSync(path.dirname(path.join(output, relative)), { recursive: true });
    fs.copyFileSync(source, path.join(output, relative));
    layers[layer] = relative;
  }
  return { ...frame, original, candidate, order, layers, hashes: {
    original: sha256(path.join(output, original)),
    candidate: sha256(path.join(output, candidate)),
  }};
});
const coverage = new Map();
for (const [clip, definition] of Object.entries(manifest.clips)) {
  for (const [index, source] of definition.sourceFrames.entries()) {
    const key = `${source.clip}/${source.index}`;
    if (!coverage.has(key)) coverage.set(key, {
      sourceClip: source.clip, sourceIndex: source.index, atlasIndex: source.atlasIndex,
      runtime: [], state: 'not-drawn',
    });
    const entry = coverage.get(key);
    entry.runtime.push({ clip, index });
    const draft = frames.find(frame => frame.clip === clip && frame.index === index);
    if (draft) { entry.state = 'draft-unapproved'; entry.draft = draft.candidate; }
  }
}
const data = { version: 2, method: 'whole-pose-painted-frames', canvas: manifest.canvas,
  skinId: 'calico', productionReady: false, bodyLayers: manifest.bodyLayers,
  frames, coverage: [...coverage.values()].sort((first, second) => first.atlasIndex - second.atlasIndex) };
fs.writeFileSync(path.join(output, 'manifest.json'), JSON.stringify(data, null, 2) + '\n');
fs.writeFileSync(path.join(output, 'data.js'), `window.poseStudy = ${JSON.stringify(data)};\n`);
const embedded = {};
for (const frame of frames) {
  for (const asset of [frame.original, frame.candidate, ...manifest.bodyLayers.map(layer => frame.layers[layer])]) {
    embedded[asset] = 'data:image/png;base64,' + fs.readFileSync(path.join(output, asset)).toString('base64');
  }
}
fs.writeFileSync(path.join(output, 'embedded.js'), `window.poseImages = ${JSON.stringify(embedded)};\n`);
console.log(JSON.stringify({ drafts: frames.length, sourceFrames: coverage.size, productionReady: false }));
