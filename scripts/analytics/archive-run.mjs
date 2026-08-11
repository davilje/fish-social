/**
 * 归档一次分析产出到 docs/analytics/runs/<runId>/
 * 用法: node scripts/analytics/archive-run.mjs <type> [runId] [sourceDir]
 */
import fs from 'fs';
import path from 'path';
import {
  ANALYTICS_ROOT,
  RUNS_DIR,
  buildManifest,
  computeAnalysis,
  dataToCompact,
  ensureDir,
  inferRulesVersion,
  makeRunId,
  readJson,
  writeJson,
} from './lib.mjs';

const type = process.argv[2] || 'pond-day';
const sourceDir =
  process.argv[4] ||
  (type === 'pond-ecology'
    ? path.join(ANALYTICS_ROOT, 'pond-ecology-initial')
    : path.join(ANALYTICS_ROOT, 'pond-day-simulation'));

let compact;
let analysis;
let meta;

if (type === 'pond-day') {
  const dataPath = path.join(sourceDir, 'data.json');
  if (!fs.existsSync(dataPath)) {
    console.error('Missing', dataPath);
    process.exit(1);
  }
  const raw = readJson(dataPath);
  compact = fs.existsSync(path.join(sourceDir, 'compact.json'))
    ? readJson(path.join(sourceDir, 'compact.json'))
    : dataToCompact(raw);
  analysis = computeAnalysis(compact);
  meta = {
    type: 'pond-day',
    title: `鱼塘 ${compact.simDays || raw.meta?.simDays || 1} 天生态模拟`,
    generatedAt: compact.generatedAt || raw.meta.generatedAt,
    seed: compact.seed,
    rulesVersion: inferRulesVersion(compact.rules),
    label: process.argv[3] ? undefined : inferRulesVersion(compact.rules),
  };
} else if (type === 'pond-ecology') {
  const dataPath = path.join(sourceDir, 'data.json');
  const data = readJson(dataPath);
  meta = {
    type: 'pond-ecology',
    title: '鱼塘初始生态分析',
    generatedAt: data.generatedAt,
    seed: null,
    rulesVersion: 'initial',
    monteCarloSamples: data.monteCarloSamples,
  };
  compact = null;
  analysis = { conclusions: ['初始种群约 90% 体长落在 0.05–0.2m。', '高品质鱼出生时体型更小，但长期成长空间更大。'] };
} else {
  console.error('Unknown type:', type);
  process.exit(1);
}

const runId = process.argv[3] || makeRunId(type, compact || { generatedAt: meta.generatedAt, rules: {} }, meta.rulesVersion?.replace(/\./g, ''));
const runDir = path.join(RUNS_DIR, runId);
ensureDir(runDir);

if (compact) {
  writeJson(path.join(runDir, 'compact.json'), compact);
  writeJson(path.join(runDir, 'analysis.json'), analysis);
}

if (type === 'pond-ecology') {
  fs.copyFileSync(path.join(sourceDir, 'data.json'), path.join(runDir, 'data.json'));
  if (fs.existsSync(path.join(sourceDir, 'report.html'))) {
    fs.copyFileSync(path.join(sourceDir, 'report.html'), path.join(runDir, 'report.html'));
  }
} else {
  const reportSrc = path.join(sourceDir, 'report.html');
  if (fs.existsSync(reportSrc)) {
    let reportHtml = fs.readFileSync(reportSrc, 'utf8');
    reportHtml = reportHtml.replace(/\.\.\/index\.html/g, '../../index.html');
    reportHtml = reportHtml.replace(/\.\.\/compare\.html/g, '../../compare.html');
    fs.writeFileSync(path.join(runDir, 'report.html'), reportHtml, 'utf8');
  }
}

writeJson(path.join(runDir, 'meta.json'), { id: runId, ...meta, analysis: analysis.scenarioRows ? { scenarioRows: analysis.scenarioRows } : undefined });

const manifest = buildManifest();
writeJson(path.join(ANALYTICS_ROOT, 'manifest.json'), manifest);

console.log('Archived →', runDir);
console.log('Manifest:', manifest.runs.length, 'runs');
