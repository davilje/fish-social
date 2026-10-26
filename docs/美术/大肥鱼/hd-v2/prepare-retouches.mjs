import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const folder = path.dirname(fileURLToPath(import.meta.url));
const frames = JSON.parse(fs.readFileSync(path.join(folder, 'registration.json')));
fs.mkdirSync(path.join(folder, 'prompts/retouches'), { recursive: true });
const selected = process.argv.slice(2);
for (const frame of frames.filter(item => selected.includes(item.id))) {
  const target = point => point.map((value, axis) => Math.round((value - frame.offset[axis]) / frame.scale * frame.sourceSize[axis] / [640, 512][axis]));
  const near = target(frame.targetNear), far = target(frame.targetFar);
  const prompt = `Use case: precise-object-edit. Edit this single high resolution Dafeiyu animation sprite. Change ONLY the arms, sleeve folds, cuffs and hands so they reach the required gripping locations. The current hand positions are incorrect. Keep her exact head, face, expression, hairstyle, maid headband, head size, tiny torso proportions, skirt, knees, tail, shoes, foot positions and the framing UNCHANGED. No whole-body resize or translation. Preserve transparent background and crisp outline/cel-shading quality. Output same ${frame.sourceSize.join('x')} canvas.
The desired hand CENTERS in this image's native pixel coordinates are: viewer-left/near hand (${near}), viewer-right/far hand (${far}). Origin is top left. That is near hand x=${(100 * near[0] / frame.sourceSize[0]).toFixed(1)}% y=${(100 * near[1] / frame.sourceSize[1]).toFixed(1)}%, far hand x=${(100 * far[0] / frame.sourceSize[0]).toFixed(1)}% y=${(100 * far[1] / frame.sourceSize[1]).toFixed(1)}%. Bend/extend the elbows naturally to reach these locations. Both hands must be small, separate, with natural small closed fingers and white frilled cuffs. Erase the old misplaced hands completely and correctly restore the dress/hair underneath. Do not add a rod, fish, hook, bench, shadow or text. Do not change the canvas framing or the rest of the character. This is pose ${frame.id}.`;
  fs.writeFileSync(path.join(folder, 'prompts/retouches', frame.id + '.txt'), prompt + '\n');
}
