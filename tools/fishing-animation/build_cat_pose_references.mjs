import fs from 'node:fs';
import path from 'node:path';

const sourceRoot = '/Users/red/Desktop/fish/output/animation-studio';
const repoRoot = path.resolve(new URL('.', import.meta.url).pathname, '../..');
const destinationRoot = path.join(repoRoot, 'docs/art/cat-skins');
const data = JSON.parse(fs.readFileSync(path.join(sourceRoot, 'animation.json'), 'utf8'));
const phases = data.phaseOrder;
const frames = [];

for (const clip of phases) {
  for (const frame of data.clips[clip].frames) {
    const id = `${clip}-${String(frame.index).padStart(2, '0')}`;
    const destination = path.join(destinationRoot, 'reference', id);
    fs.mkdirSync(destination, { recursive: true });
    fs.copyFileSync(path.join(sourceRoot, frame.sourceCharacter), path.join(destination, 'cat.png'));
    for (const layer of data.renderLayerOrder) {
      const source = path.join(sourceRoot, frame.layers[layer]);
      if (fs.existsSync(source)) fs.copyFileSync(source, path.join(destination, `${layer}.png`));
    }
    const layers = {};
    for (const layer of data.renderLayerOrder) {
      if (fs.existsSync(path.join(sourceRoot, frame.layers[layer]))) {
        layers[layer] = `reference/${id}/${layer}.png`;
      }
    }
    frames.push({
      id,
      label: frame.stage || `${clip} ${frame.index + 1}`,
      clip,
      index: frame.index,
      atlasIndex: frame.atlasIndex,
      durationMs: frame.durationMs,
      sourceCharacter: `reference/${id}/cat.png`,
      layers,
      renderLayerOrder: frame.renderLayerOrder,
    });
  }
}

const manifest = {
  version: 2,
  method: 'whole-pose-painted-frames',
  skinId: 'calico',
  sourceVersion: data.version,
  canvas: [640, 512],
  bodyLayers: ['cat'],
  renderLayerOrder: data.renderLayerOrder,
  frames,
};
fs.writeFileSync(path.join(destinationRoot, 'full-pose-manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`);
fs.writeFileSync(path.join(destinationRoot, 'full-pose-data.js'), `window.poseStudy = ${JSON.stringify(manifest)};\n`);
console.log(JSON.stringify({ frames: frames.length, actions: phases.length }));
