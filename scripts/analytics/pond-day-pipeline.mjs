/**
 * 鱼塘多日模拟完整分析流水线：data → compact → analysis → report → archive
 */
import fs from 'fs';
import path from 'path';
import { spawnSync } from 'child_process';
import { fileURLToPath } from 'url';
import {
  ANALYTICS_ROOT,
  computeAnalysis,
  dataToCompact,
  readJson,
  writeJson,
} from './lib.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const sourceDir = path.join(ANALYTICS_ROOT, 'pond-day-simulation');
const dataPath = path.join(sourceDir, 'data.json');

if (!fs.existsSync(dataPath)) {
  console.error('Run simulate:pond-day first. Missing', dataPath);
  process.exit(1);
}

const raw = readJson(dataPath);
const compact = dataToCompact(raw);
const analysis = computeAnalysis(compact);

writeJson(path.join(sourceDir, 'compact.json'), compact);
writeJson(path.join(sourceDir, 'analysis.json'), analysis);

const reportScript = path.join(__dirname, 'generate-pond-day-report.mjs');
spawnSync(process.execPath, [reportScript, path.join(sourceDir, 'compact.json'), path.join(sourceDir, 'report.html'), path.join(sourceDir, 'analysis.json')], { stdio: 'inherit' });

const archiveScript = path.join(__dirname, 'archive-run.mjs');
spawnSync(process.execPath, [archiveScript, 'pond-day'], { stdio: 'inherit' });

const indexScript = path.join(__dirname, 'build-index.mjs');
spawnSync(process.execPath, [indexScript], { stdio: 'inherit' });

spawnSync(process.execPath, [path.join(__dirname, 'generate-analysis-report.mjs')], { stdio: 'inherit' });

console.log('\nPipeline complete.');
console.log('  compact.json, analysis.json, report.html');
console.log('  docs/analytics/index.html — 归档索引');
console.log('  docs/analytics/compare.html — 对比查看');
