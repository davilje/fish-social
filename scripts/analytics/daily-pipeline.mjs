/**
 * 运营日报 v1.1 流水线
 */
import Database from 'better-sqlite3';
import { spawnSync } from 'child_process';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { parseDateArg } from './date-utils.mjs';
import { computeDailySummary } from './compute-daily-summary.mjs';
import { writeDailyBatchStatus } from './write-daily-batch-status.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(__dirname, '../..');
const dataDir = path.join(projectRoot, 'data');
const dbPath = process.env.DB_PATH ?? path.join(dataDir, 'fish-social.db');
const reportDir = path.join(projectRoot, 'docs/analytics/daily');

const dateKey = parseDateArg();
const dateArg = `--date=${dateKey}`;
const startedAtMs = Date.now();

function failAndExit(exitCode, message) {
  try {
    writeDailyBatchStatus({
      dateKey,
      exitCode,
      startedAtMs,
      message,
    });
  } catch (e) {
    console.error('[daily-pipeline] failed to write batch status', e);
  }
  process.exit(exitCode);
}

function runStep(label, script, args = [], fatal = true) {
  const result = spawnSync('node', [script, ...args], {
    cwd: projectRoot,
    env: process.env,
    encoding: 'utf8',
    stdio: 'inherit',
  });
  if (result.status !== 0) {
    console.error(`[daily-pipeline] ${label} failed (exit ${result.status})`);
    if (fatal) failAndExit(result.status ?? 1, `${label} failed`);
    return false;
  }
  return true;
}

runStep('aggregate', 'scripts/aggregate-daily-metrics.mjs', [dateArg]);
runStep('ecology-snapshot', 'scripts/analytics/ecology-daily-snapshot.mjs', [dateArg]);

let summary;
try {
  const db = new Database(dbPath);
  db.pragma('journal_mode = WAL');
  summary = computeDailySummary(db, dateKey);
  db.close();
} catch (e) {
  console.error('[daily-pipeline] computeDailySummary failed', e);
  failAndExit(1, 'computeDailySummary failed');
}

const outDir = path.join(reportDir, dateKey);
fs.mkdirSync(outDir, { recursive: true });

const summaryPath = path.join(outDir, 'summary.json');
fs.writeFileSync(summaryPath, JSON.stringify(summary, null, 2), 'utf8');

const alerts = Array.isArray(summary.alerts) ? summary.alerts : [];
const alertBad = alerts.filter((a) => a.level === 'bad').length;
const alertWarn = alerts.filter((a) => a.level === 'warn').length;

const catchSec = summary.sections.catch || {};
const compact = {
  generatedAt: summary.meta.generatedAt,
  rulesVersion: summary.meta.rulesVersion,
  date: dateKey,
  totalCatches: summary.kpis.kpi_daily_catch.value ?? 0,
  totalCatchesHuman: summary.kpis.kpi_daily_catch_human?.value ?? catchSec.human ?? 0,
  totalCatchesBot: summary.kpis.kpi_daily_catch_bot?.value ?? catchSec.bot ?? 0,
  catchSource: catchSec.source ?? 'inventory',
  totalDisconnects: summary.sections.catch.totalDisconnects ?? 0,
  activePlayers: summary.kpis.kpi_dau.value ?? 0,
  pondCount: summary.sections.catch.byPond.length,
  ponds: summary.sections.catch.byPond.map((p) => ({
    pondId: p.pondId,
    catches: p.catches,
    disconnects: p.disconnects,
    biteTickHit: p.biteTickHit,
    biteTickMiss: p.biteTickMiss,
    avgPopulation: p.avgPopulation,
  })),
  alertCount: alerts.length,
  alertBad,
  alertWarn,
};
fs.writeFileSync(path.join(outDir, 'compact.json'), JSON.stringify(compact, null, 2), 'utf8');

runStep('report', 'scripts/analytics/generate-daily-ops-report.mjs', [summaryPath, path.join(outDir, 'report.html')]);
runStep('alert', 'scripts/analytics/send-daily-alert.mjs', [summaryPath], false);
runStep('index', 'scripts/analytics/build-index.mjs');

const warehouse = spawnSync('node', ['scripts/analytics/export-warehouse.mjs', dateArg], {
  cwd: projectRoot,
  env: process.env,
  encoding: 'utf8',
});
if (warehouse.status !== 0) {
  console.warn('[daily-pipeline] export-warehouse skipped or failed (non-fatal)');
}

const vsSim = spawnSync('node', ['scripts/analytics/build-live-vs-sim.mjs'], {
  cwd: projectRoot,
  env: process.env,
  encoding: 'utf8',
});
if (vsSim.status !== 0) {
  console.warn('[daily-pipeline] build-live-vs-sim skipped or failed (non-fatal)');
}

const growth = spawnSync('node', ['scripts/analytics/build-growth-dashboard.mjs'], {
  cwd: projectRoot,
  env: process.env,
  encoding: 'utf8',
});
if (growth.status !== 0) {
  console.warn('[daily-pipeline] build-growth-dashboard skipped or failed (non-fatal)');
}

writeDailyBatchStatus({
  dateKey,
  exitCode: 0,
  startedAtMs,
});
console.log(`[daily-pipeline] ${dateKey}: v1.1 complete → ${outDir}`);
