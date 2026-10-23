import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const folder=path.dirname(fileURLToPath(import.meta.url));
const reference=path.resolve(folder,'../room-cat-standing.png');
const seated=path.resolve(folder,'../room-cat-sitting.png');
const sequences={
  'pet-roll':[
    'Stand upright on hind paws, arms relaxed, matching the reference standing pose.',
    'Bend knees and lower hips slightly; both front paws reach down.',
    'Deep squat, front paws touch the floor, head tilts gently.',
    'Lower right shoulder toward the floor, hips begin leaning sideways.',
    'Side lying, head to viewer right, body low, front paws curling.',
    'Roll shoulder backward, belly halfway toward viewer, paws lifted.',
    'Lying on back, white belly faces viewer, all four paws bent above belly.',
    'On back with head relaxed and smiling eyes closed, rear paws slightly extend.',
    'On back shifting toward opposite side, paws gather, tail follows the roll.',
    'Three-quarter turn onto the other side, belly turns away, paws uncurl.',
    'Lying on opposite flank, head facing viewer left, front paws reach down.',
    'Chest rotates toward floor, both front paws plant, hindquarters still low.',
    'Push chest up on front paws, rear paws tuck under, turn toward reference right-facing direction.',
    'Crouching on four paws, raise head, prepare to stand upright.',
    'Rise onto hind paws, front paws release floor and hang down.',
    'Upright resting stance, same as first reference pose; relaxed and satisfied.'
  ],
  walk:[
    'Four-legged walk: near front paw forward at contact, near hind paw back at contact, far front lifting.',
    'Weight on near front paw, far front paw swings forward, near hind heel lifts.',
    'Far front paw reaches forward midair, near hind paw swings under belly.',
    'Far front paw contacts floor ahead, near front paw begins trailing backward.',
    'Far front supports weight, near hind paw lands under hip, near front paw lifts.',
    'Near front paw passes under chest in air, far hind paw pushes backward.',
    'Near front paw extended forward in air, far hind paw lifts behind.',
    'Near front paw contacts forward, far hind paw swings under belly.',
    'Near front supports chest, far hind lands under hip, far front paw lifts.',
    'Far front passes forward under chest, near hind pushes backward.',
    'Near hind lifts and begins swing; near front still supports body.',
    'Near hind extends back toward the opening contact; far front begins its next swing. Almost first pose, not identical.'
  ],
  'walk-start':[
    'Upright two-legged standing pose as reference; front paws hang down.',
    'Knees bend, hips lower, front paws reach toward floor.',
    'Front paws plant, torso extends into short natural four-legged stance.',
    'Begin walking: near front paw forward at contact and near hind paw back, same opening walk pose.'
  ],
  'walk-stop':[
    'Finish four-legged step with front paws planted, rear paw coming under hips.',
    'Bring all four paws under torso, lower hips into squat.',
    'Straighten hind legs, lift chest and front paws away from floor.',
    'Upright two-legged standing pose matching the reference, relaxed front paws down.'
  ],
  eat:[
    'Stable four-legged stance, head turned toward imaginary food bowl, sniffing with nose slightly forward.',
    'Bend neck downward half way, nose approaching imaginary bowl lip.',
    'Lower muzzle to imaginary bowl lip, ears relaxed, paws stay firmly planted.',
    'Eating loop: head low, mouth at fixed contact, jaw lightly open.',
    'Jaw closes gently, muzzle compresses slightly, eyes relaxed.',
    'Small tongue reaches down beyond lower lip, ears tilt a little outward.',
    'Tongue retracts, mouth half closed, ears return.',
    'Small chewing motion, cheeks subtly rounded, paws unchanged.',
    'Mouth opens just a little, nose almost fixed, one ear flicks slightly.',
    'Short tongue lick downward, head rises only very slightly.',
    'Return to low head nibble pose, jaw half open, ready to loop to first eating pose.',
    'Stop nibbling, mouth closes; start raising head from bowl.',
    'Head halfway raised, tongue licks upper lip, content expression.',
    'Head upright in stable four-legged stance, paws stay in the same places, ready to walk.'
  ]
};
const jobs=[];
for(const [clip,poses] of Object.entries(sequences)){
  for(const direction of clip==='pet-roll'?['neutral']:['right','left']){
    for(let start=0;start<poses.length;start+=4){
      const selected=poses.slice(start,start+4), columns=2, rows=selected.length/2;
      const canvas=clip.startsWith('walk')?[512,512]:[512,384];
      const id=`orange-${clip}-${direction}-${String(start).padStart(2,'0')}`;
      const prompt=`Use case: illustration-story. Asset: transparent hand-drawn game animation sprite sheet. Reference images show the EXACT orange cat identity to preserve: rounded triangular head and short muzzle, black oval eyes with small highlights (not oversized anime eyes), white muzzle blaze and chest/belly/paw tips, orange tabby stripes, pink ears, short soft tail, dark fine outlines and warm soft painted shading. No clothing, no fishing rod, no furniture. Draw real changing anatomy, not rotated or stretched copies.\nProduce EXACTLY ${selected.length} isolated sequential poses in a strict ${columns} column by ${rows} row equal-cell grid, read left to right then next row. Overall aspect ${columns*canvas[0]}:${rows*canvas[1]}; request native canvas ${columns*canvas[0]*2}x${rows*canvas[1]*2} pixels. No labels, letters, numbers, borders, shadows, floor, or checkerboard; genuine transparent RGBA. Each cell contains one complete cat with ample transparent margins and no neighboring overlap.\nWithin every cell use the SAME character scale and head size. Face width about 26% of cell width. Ground baseline at 91.7% of cell height for roll/eat, or 93.75% for walking; horizontal body center at 50%. Do not enlarge crouched or lying poses to fill the cell. Entire tail and paws visible. ${direction==='neutral'?'Rolling begins and ends facing right as the reference.':`The cat always faces ${direction.toUpperCase()} in a three-quarter side view; nose toward ${direction==='left'?'LEFT edge, tail behind toward RIGHT':'RIGHT edge, tail behind toward LEFT'}. Keep camera direction fixed through all cells.`}\nAction ${clip}. Poses in exact grid order:\n${selected.map((pose,index)=>`${index+1}. ${pose}`).join('\n')}\n${clip==='eat'?'Do NOT draw a bowl or food. Keep all paw contacts fixed, lower head toward a contact point near the nose-side edge, at 78% cell height. Chewing must change mouth shape; no full-body scale changes.':''}\nPreserve the reference look precisely and keep markings attached to the same anatomical side. Clean crisp edges, uniform ambient light, no external cast shadows. Each cell must be an individually drawn consecutive animation pose, not a composition illustration.`;
      fs.writeFileSync(path.join(folder,'prompts',id+'.txt'),prompt+'\n');
      jobs.push({id,skinId:'orange',clip,direction,start,count:selected.length,columns,rows,canvas,references:[reference,seated],prompt});
    }
  }
}
fs.writeFileSync(path.join(folder,'jobs.json'),JSON.stringify(jobs,null,2)+'\n');
console.log(JSON.stringify({sheets:jobs.length,frames:jobs.reduce((sum,job)=>sum+job.count,0)}));
