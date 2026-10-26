import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const folder=path.dirname(fileURLToPath(import.meta.url));
const reference=path.resolve(folder,'../room-runtime/food-bowls.png');
const jobs=[];
const pour=[
  'A pale wooden scoop filled with brown kibble enters from upper right, almost outside frame; no falling grains.',
  'The filled scoop moves toward the upper center; no falling grains.',
  'The scoop pauses high above the target, still upright.',
  'The scoop tilts slightly downward toward left; first two pellets falling.',
  'The scoop tilts further; small stream of pellets begins.',
  'Steady falling stream, scoop held at about forty-five degrees.',
  'Steady stream with pellets in different positions along the falling arc.',
  'Steady stream, fewer pellets remain in scoop.',
  'Last small stream falls from nearly empty scoop.',
  'Two last pellets fall; empty scoop starts to rotate upright.',
  'Empty scoop withdraws up and right; no falling pellets.',
  'Only the very end of the scoop remains near the top right boundary, no pellets.'
];
for(let start=0;start<12;start+=4){
  const id='shared-food-pour-'+String(start).padStart(2,'0');
  const prompt=`Use case: illustration-story. Draw a production animation sprite sheet of a pale wooden kibble scoop pouring pet food, matching the hand-painted warm cartoon style of the attached bowl reference. The reference is STYLE and contact-layout only: DO NOT DRAW ANY BOWL, TRAY, WATER, CAT, HAND, floor or shadows. Only the wooden scoop and suspended falling brown pellets are visible on genuine transparent RGBA. Exactly FOUR equal cells, TWO columns TWO rows, native request 1920x1600, aspect 6:5. No labels or grid. Each cell logical 480x400. In every cell the invisible landing point is x=172,y=344; the scoop tip while pouring is x=200,y=140. The scoop stays in upper half, particles fall toward that single same landing point; do not draw landed food. Same scoop size in every frame, clean outlined hand-drawn cel shading. Sequential poses left-right then next row:\n${pour.slice(start,start+4).map((pose,index)=>`${index+1}. ${pose}`).join('\n')}\nUniform lighting, no scenery, leave clean transparent margins. No repeated identical drawings.`;
  jobs.push({id,skinId:'shared',clip:'food-pour',direction:'neutral',start,count:4,columns:2,rows:2,canvas:[480,400],references:[reference],prompt});
}
const id='shared-bowl-food-states';
const prompt='Use case: illustration-story. Transparent sprite sheet containing ONLY hand-painted brown cat kibble clusters, matching the attached green food bowl style. Do not draw the bowl, tray, water, cat, text, shadows or background. EXACTLY FOUR equal cells TWO columns TWO rows, wide landscape aspect 27:8.8, native request 2160x704. Each cell represents a 270x88 logical canvas. In each cell the ONLY artwork is a small shallow elliptical pile of brown kibble centered at logical x=76,y=29, width approximately 76, height approximately 25. Do not center the pile in the canvas: it belongs in the LEFT HALF because the right half is the water bowl and must be fully transparent. Row one: (left) full mound slightly domed, (right) two-thirds mound same position with fewer pellets. Row two: (left) one-third mound, same position, (right) tiny remaining few crumbs at same position. All individual grains round with hand-drawn dark brown outlines and warm highlights, preserve same grain size. Native genuine transparent alpha. No extra objects. Keep all piles below logical y=45 and above y=12.';
jobs.push({id,skinId:'shared',clip:'bowl-food',direction:'neutral',start:0,count:4,columns:2,rows:2,canvas:[270,88],references:[reference],prompt});
for(const job of jobs)fs.writeFileSync(path.join(folder,'prompts',job.id+'.txt'),job.prompt+'\n');
fs.writeFileSync(path.join(folder,'shared-jobs.json'),JSON.stringify(jobs,null,2)+'\n');
