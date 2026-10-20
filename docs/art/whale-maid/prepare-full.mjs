import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const sharp = require(process.env.SHARP_MODULE || 'sharp');
const folder = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(folder, '../../..');
const cats = path.join(root, 'docs/art/cat-skins');
const output = path.join(root, 'docs/art/dafeiyu/full-v1');
const trial = path.join(folder, 'template-study-v1');
const source = JSON.parse(fs.readFileSync(path.join(cats, 'full-pose-manifest.json')));
const original = JSON.parse(fs.readFileSync(path.join(cats, 'painted-poses.json'))).poses;
const hash = filename => crypto.createHash('sha256').update(fs.readFileSync(filename)).digest('hex');
const byHash = new Map(original.map(frame => [frame.sourceSha256, frame.id]));
const existing = new Map(['ready-00', 'bait-02', 'bite-03', 'effort-03', 'catch-00', 'catch-04'].map(id => {
  const frame = source.frames.find(frame => frame.id === id);
  return [byHash.get(hash(path.join(cats, frame.sourceCharacter))), id];
}));
const unique = new Map();
const frames = source.frames.map(frame => {
  const drawing = byHash.get(hash(path.join(cats, frame.sourceCharacter)));
  if (!unique.has(drawing)) unique.set(drawing, frame);
  return { ...frame, drawing };
});
const fresh = [...unique].filter(([id]) => !existing.has(id));
for (const subfolder of ['templates', 'prompts', 'generated', 'painted', 'frames', 'shared']) {
  fs.mkdirSync(path.join(output, subfolder), { recursive: true });
}
for (const [drawing, id] of existing) fs.copyFileSync(path.join(trial, 'frames', id, 'character.png'), path.join(output, 'painted', drawing + '.png'));
for (const file of ['identity.png', 'celebration.png', 'seat-stool-v3.png']) fs.copyFileSync(path.join(trial, 'shared', file), path.join(output, 'shared', file));
const bounds = async filename => {
  const { data } = await sharp(filename).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  let left = 640, top = 512, right = -1, bottom = -1;
  for (let vertical = 0; vertical < 512; vertical++) for (let horizontal = 0; horizontal < 640; horizontal++) {
    if (data[(vertical * 640 + horizontal) * 4 + 3] < 128) continue;
    left = Math.min(left, horizontal); right = Math.max(right, horizontal);
    top = Math.min(top, vertical); bottom = Math.max(bottom, vertical);
  }
  return [left, top, right, bottom];
};
const pairs = [];
for (let index = 0; index < fresh.length; index += 2) {
  const pair = fresh.slice(index, index + 2);
  const id = 'pair-' + String(index / 2 + 1).padStart(2, '0');
  const images = [];
  const poses = [];
  for (const [slot, [drawing, frame]] of pair.entries()) {
    const combined = await sharp({ create: { width: 640, height: 512, channels: 4, background: '#00000000' } })
      .composite(frame.renderLayerOrder.map(layer => ({ input: path.join(cats, frame.layers[layer]) })))
      .png().toBuffer();
    const composite = await sharp(combined).resize(1280, 1024).png().toBuffer();
    images.push({ input: composite, left: slot * 1280, top: 0 });
    const regions = {};
    for (const layer of ['head', 'far_hand', 'hands']) regions[layer] = await bounds(path.join(cats, frame.layers[layer]));
    poses.push({ id: drawing, templateFrame: frame.id, label: frame.label, bounds: regions });
  }
  const template = `templates/${id}.png`;
  await sharp({ create: { width: 2560, height: 1024, channels: 4, background: '#00000000' } })
    .composite(images).png().toFile(path.join(output, template));
  const prompt = `Use case: identity-preserve. Asset: a TWO-FRAME transparent sprite sheet for the game character 大肥鱼 (Dafeiyu).\nImage 1 is the exact two-pose CAT PLACEMENT/GESTURE template, two equal panels of 1280x1024, total 2560x1024. Image 2 is the previously drawn Dafeiyu fishing character, the identity and proportion/style MASTER. Image 3 is the original blue-haired whale maid appearance reference; use only the first open-eyed girl, not the other two expressions.\nReplace BOTH cats with the SAME tiny chibi human whale maid girl matching Image 2. Exact blue gradient long wavy hair, white ruffled maid headband, blue side bows, one ahoge, blue eyes, navy dress, white bib and round white apron with small dark-blue whale emblem, white stockings/navy shoes, one navy whale tail pointing screen LEFT behind her. No animal ears. Same large head and tiny body and outline/cel shading as image 2. Keep skirt/hair/accessory design identical across BOTH frames.\nOUTPUT: EXACTLY TWO CHARACTERS in the same two panels and at the EXACT TEMPLATE LOCATIONS, 2560x1024 transparent canvas. No panel borders, text, labels, shadows, background or seat. Do not center or enlarge. Source logical canvas in EACH panel is 640x512 scaled x2: use the locations below in logical coordinates, then double them. For RIGHT panel additionally add 1280 to output x. Keep each figure fully inside its own panel. Soles around y=450 logical, hips close to y=405 except standing/squatting actions.\nCRITICAL: draw ONLY the CHARACTERS. OMIT all rod/fish/line/float/bait/bench props visible in the template. Props are already separate game layers. Tiny HUMAN hands with white frilled cuffs must EXACTLY surround the existing invisible rod grip at template hand anchors. Do not draw a replacement rod, even a small piece. Preserve the cat template pose, leaning angle, gesture and expression, adapted to human anatomy, 3/4 facing right; do not substitute a generic sitting pose. Long hair remains behind arms and torso, tail on left.\n` + poses.map((pose, slot) => {
    const grip = layer => { const [left, top, right, bottom] = pose.bounds[layer]; return `(${Math.round((left + right) / 2)},${Math.round((top + bottom) / 2)})`; };
    const expression = pose.id.startsWith('retrieve_empty') || pose.id.startsWith('reset_empty') ? 'disappointed expression, downturned mouth, drooping sad eyes; empty catch' : pose.id.startsWith('effort') || pose.id.startsWith('bite') ? 'straining determined expression, squinting eyes, pulling hard' : pose.id.startsWith('catch') ? 'happy proud open-mouth smile while showing catch with empty hands; fish will be composited later' : 'gentle open blue eyes, small smile';
    const standing = pose.id === 'sit-00' ? 'STANDING upright with BOTH ARMS HANGING DOWN at sides, no rod, tiny straight legs.' : pose.id === 'sit-01' ? 'CROUCHING halfway down from standing to seated, both hands down, knees bending. No rod.' : pose.id === 'sit-02' ? 'Seated with arms lowered, relaxed empty hands. No rod.' : 'Seated on an invisible low bench with natural bent legs. Follow template hands and torso orientation precisely.';
    return `${slot === 0 ? 'LEFT' : 'RIGHT'} panel ${pose.id}: ${pose.label}. ${standing} ${expression}. Logical near-hand CENTER=${grip('hands')}, far-hand CENTER=${grip('far_hand')}. Logical head bounding region=${JSON.stringify(pose.bounds.head)}; preserve face scale and head location relative to template. `;
  }).join('\n');
  fs.writeFileSync(path.join(output, 'prompts', id + '.txt'), prompt + '\n');
  pairs.push({ id, poses, template, prompt: 'prompts/' + id + '.txt' });
}
fs.writeFileSync(path.join(output, 'plan.json'), JSON.stringify({ characterId: 'dafeiyu', name: '大肥鱼',
  canvas: [640, 512], sheetCanvas: [2560, 1024], method: 'builtin-imagegen-pose-template',
  sourceFrameCount: frames.length, uniqueDrawingCount: unique.size, existing: Object.fromEntries(existing),
  frames, pairs }, null, 2) + '\n');
console.log(JSON.stringify({ sourceFrames: frames.length, drawings: unique.size, retained: existing.size, newDrawings: fresh.length, sheets: pairs.length }));
