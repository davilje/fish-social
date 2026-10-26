import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../docs/美术/猫咪形象');
const breeds = JSON.parse(fs.readFileSync(path.join(root, 'breed-definitions.json'))).breeds;
const summaries = breeds.map(breed => {
  const artwork = JSON.parse(fs.readFileSync(path.join(root, `breeds/${breed.id}/painted-poses.json`)));
  const validation = JSON.parse(fs.readFileSync(path.join(root, `breeds/${breed.id}/validation.json`)));
  return { id: breed.id, name: breed.name, uniqueDrawings: artwork.poses.length,
    sourceFrames: validation.sourceFrames, runtimeFrames: validation.runtimeFrames,
    characterLayersPerClient: validation.characterLayers, installed: validation.installed,
    minSilhouetteOverlap: Math.min(...artwork.poses.map(pose => pose.overlap)),
    visualReview: 'contact-sheet-reviewed', productionReady: false };
});
const report = { version: 4, baseline: ['orange', 'calico'], breeds: summaries,
  generatedUniqueDrawings: summaries.reduce((sum, breed) => sum + breed.uniqueDrawings, 0),
  sharedGearAndTimings: 'unchanged',
  knownLimitations: [
    '手绘逐帧仍有细微纹理、花斑边缘和色调波动；轮廓阈值不等同连续性终验。',
    '未在 Windows Player 或原生 WPF Overlay 实机执行视觉验收。',
    'gray、siamese、tuxedo 为历史兼容入口，仍使用各自旧素材，未冒充本轮九种猫。'
  ] };
fs.writeFileSync(path.join(root, 'breed-iteration-report.json'), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify(report));
