import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../docs/美术/猫咪形象');
const poseStudy = JSON.parse(fs.readFileSync(path.join(root, 'full-pose-manifest.json')));
const originals = JSON.parse(fs.readFileSync(path.join(root, 'painted-poses.json'))).poses;
const byHash = new Map(originals.map(pose => [pose.sourceSha256, pose.id]));
const frames = poseStudy.frames.map(frame => ({ ...frame,
  drawing: byHash.get(crypto.createHash('sha256').update(fs.readFileSync(path.join(root, frame.sourceCharacter))).digest('hex')) }));
const definitions = JSON.parse(fs.readFileSync(path.join(root, 'breed-definitions.json'))).breeds;
const breeds = [
  { id: 'orange', name: '橘猫', installed: true, status: '原版动作基准', available: originals.map(pose => pose.id) },
  { id: 'calico', name: '三花猫', installed: true, status: '上一轮修订，保留对照', available: originals.map(pose => pose.id) },
  ...definitions.map(breed => {
    const available = originals.filter(pose => fs.existsSync(path.join(root, `breeds/${breed.id}/painted/${pose.id}.json`))).map(pose => pose.id);
    const reportPath = path.join(root, `breeds/${breed.id}/validation.json`);
    const installed = fs.existsSync(reportPath) && JSON.parse(fs.readFileSync(reportPath)).installed;
    return { id: breed.id, name: breed.name, installed, available,
      status: installed ? '完整绘制包已接入，待美术终验' : `绘制中：${available.length} / ${originals.length}` };
  }),
];
fs.writeFileSync(path.join(root, 'breed-review-data.js'), 'window.breedReview = ' + JSON.stringify({ frames, breeds }) + ';\n');
console.log(JSON.stringify(breeds.map(({ id, status }) => ({ id, status }))));
