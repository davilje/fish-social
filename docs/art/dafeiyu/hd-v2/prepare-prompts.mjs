import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const folder = path.dirname(fileURLToPath(import.meta.url));
const plan = JSON.parse(fs.readFileSync(path.join(folder, '../../../archive/2026-10-26/superseded/art/dafeiyu/full-v1/plan.json')));
const studies = JSON.parse(fs.readFileSync(path.join(folder, '../../../archive/2026-10-26/superseded/art/whale-maid/template-study-v1/templates.json')));
const poses = [...new Set(plan.frames.map(frame => frame.drawing))];
fs.mkdirSync(path.join(folder, 'prompts'), { recursive: true });
const jobs = [];
for (const id of poses) {
  if (id === 'sit-03') continue;
  const frame = plan.frames.find(frame => frame.drawing === id);
  const source = plan.pairs.flatMap(pair => pair.poses).find(pose => pose.id === id) ||
    studies.poses.find(pose => pose.id === plan.existing[id]);
  const center = box => [(box[0] + box[2]) / 2, (box[1] + box[3]) / 2];
  const near = center(source.bounds.hands), far = center(source.bounds.far_hand);
  const empty = id.startsWith('retrieve_empty') || id.startsWith('reset_empty');
  const standing = id === 'sit-00', squat = id === 'sit-01';
  const prompt = `Use case: identity-preserve. Make ONE high resolution transparent game sprite of Dafeiyu. Image1 is the definitive new crisp HD MASTER for her identity, proportions and outfit. Image2 is only the gesture/expression reference for this particular frame; its body size is inconsistent and MUST be corrected to the master. Redraw, do not merely upscale the old pixels.
Keep EXACT master head/body ratio, head volume, shoulder width, tiny torso and limb thickness. Preserve blue-gradient flowing long hair, ahoge, white frilled headband, side blue bows/fin ornaments, big blue eyes, navy long sleeve maid dress, white bib/cuffs, white round apron with blue whale emblem, stockings and dark shoes, navy whale tail behind on LEFT. Identical crisp dark outlines and clean cel-shading; finely resolved eyes, eyelashes, hair and frills. No blur, noisy texture, glow or white halo.
POSE ${id}: ${frame.label}. Follow Image2's arm direction, head tilt, body lean and expression, but maintain Image1's body proportions. ${standing ? 'Standing upright with empty hands hanging at sides, feet together.' : squat ? 'Bending both knees into a squat, hands hanging, no rod.' : 'Seated on an invisible low stool with bent knees; feet resting on a fixed ground plane.'} ${empty ? 'Disappointed sad expression; no smile.' : ''}
The hand placement is dictated by an existing fishing animation. In a logical 640x512 frame, soles are y=450, near-hand center=(${near.map(Math.round)}), far-hand center=(${far.map(Math.round)}). The face skin width is about92 logical pixels: relative to that width, the near hand must be ${((450 - near[1]) / 92).toFixed(2)} face-widths above the soles and the far hand ${((450 - far[1]) / 92).toFixed(2)} face-widths above the soles. Distance between hands is ${(Math.hypot(near[0] - far[0], near[1] - far[1]) / 92).toFixed(2)} face-widths. Adjust elbow/arm gesture to meet these offsets; never shrink the whole figure to make the hands fit. Hands are small human hands with frilled cuffs, visibly separate from face and each other.
One entire character only, large on the canvas to preserve detail, fully within frame including hair tip/tail/shoes, with 5% transparent margin on every side. 1280x1024 transparent canvas, no clipping. Use the same orthographic 3/4 view facing RIGHT as the master, never mirror the outfit. NO fishing rod, fish, fishing line, bait, bench, environment, shadow, text, watermark, frame or second pose. The existing game will add all props as separate layers. Preserve genuinely transparent alpha background.`;
  fs.writeFileSync(path.join(folder, 'prompts', id + '.txt'), prompt + '\n');
  jobs.push({ id, prompt: 'prompts/' + id + '.txt', near, far, pose: frame.label,
    reference: '../../../archive/2026-10-26/superseded/art/dafeiyu/full-v1/painted/' + id + '.png' });
}
fs.writeFileSync(path.join(folder, 'jobs.json'), JSON.stringify(jobs, null, 2) + '\n');
console.log(JSON.stringify({ jobs: jobs.length }));
