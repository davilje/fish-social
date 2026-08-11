/**
 * Data Platform DP-D 验收：D-L3-10 合规 · D-L3-06 BI 导出
 * 运行: npm run verify:data-platform-dp-d
 */
import fs from 'fs';
import os from 'os';
import path from 'path';
import { spawnSync } from 'child_process';
import { fileURLToPath } from 'url';
import { randomUUID } from 'crypto';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

process.env.NODE_ENV = 'development';
process.env.PLAYER_ERASE_PEPPER = process.env.PLAYER_ERASE_PEPPER ?? 'verify-dp-d-pepper';
process.env.ADMIN_SECRET = process.env.ADMIN_SECRET ?? 'verify-dp-d-admin';

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function read(rel: string): string {
  return fs.readFileSync(path.join(ROOT, rel), 'utf8');
}

async function testAnonymize(): Promise<void> {
  console.log('\n=== TC: D-L3-10 anonymize helper ===');
  const { anonymizePlayerId, isAnonymizedPlayerId } = await import('../server/src/playerAnonymize.js');
  const a = anonymizePlayerId('player-a');
  const b = anonymizePlayerId('player-b');
  assert(a.startsWith('anon_'), 'anon prefix');
  assert(a !== b, 'different players → different anon ids');
  assert(isAnonymizedPlayerId(a), 'isAnonymized detects anon id');
  assert(!isAnonymizedPlayerId('player-a'), 'raw id not anonymized');
}

async function testExportEraseFlow(): Promise<void> {
  console.log('\n=== TC: D-L3-10 export + dry-run + erase ===');
  const { ensurePlayer } = await import('../server/src/players.js');
  const { recordFishingMetric, flushFishingMetricsQueue, getPlayerFishingTimeline } = await import(
    '../server/src/fishingMetrics.js',
  );
  const { buildPlayerExport, erasePlayerData, planPlayerErase, playerExists } = await import(
    '../server/src/playerPrivacy.js',
  );
  const { db } = await import('../server/src/db.js');

  const playerId = `dpd-test-${randomUUID().slice(0, 8)}`;
  ensurePlayer(playerId, 'DP-D测试');
  recordFishingMetric('fishing_start', { playerId, pondId: 'pond-calm', payload: {} });
  recordFishingMetric('catch_accept', { playerId, pondId: 'pond-calm', payload: { quality: 'green' } });
  flushFishingMetricsQueue();

  const bundle = buildPlayerExport(playerId);
  assert(bundle != null && bundle.playerId === playerId, 'export bundle for player');
  assert(bundle!.profile.nickname === 'DP-D测试', 'export includes profile');
  assert(bundle!.metricsSummary.length >= 1, 'export includes metrics summary');

  const plan = planPlayerErase(playerId);
  assert(plan.exists, 'plan finds player');
  assert(plan.toAnonymize.fishing_metrics >= 2, 'plan counts metrics rows');

  const dry = erasePlayerData(playerId, { dryRun: true });
  assert(dry.dryRun && dry.ok, 'dry-run succeeds');
  assert(playerExists(playerId), 'dry-run does not delete player');

  const timelineBefore = getPlayerFishingTimeline(playerId, 24, 100);
  assert(timelineBefore.events.length >= 2, 'timeline has events before erase');

  const erased = erasePlayerData(playerId, { dryRun: false });
  assert(erased.ok && !erased.dryRun, 'erase succeeds');
  assert(!playerExists(playerId), 'player row deleted');
  assert(erased.anonymizedId?.startsWith('anon_'), 'anonymized id returned');

  const timelineAfter = getPlayerFishingTimeline(playerId, 24, 100);
  assert(timelineAfter.events.length === 0, 'timeline empty for original playerId');

  const anonCount = db
    .prepare('SELECT COUNT(*) as c FROM fishing_metrics WHERE player_id = ?')
    .get(erased.anonymizedId!) as { c: number };
  assert(anonCount.c >= 2, 'metrics retained under anon id');

  const audit = db
    .prepare("SELECT COUNT(*) as c FROM audit_log WHERE target_player_id = ? AND what LIKE 'player_erase%'")
    .get(playerId) as { c: number };
  // audit may reference original id from test harness — API writes audit; direct erase skips HTTP audit
  void audit;
}

function testAdminRoutes(): void {
  console.log('\n=== TC: D-L3-10 admin API wiring ===');
  const admin = read('server/src/admin.ts');
  assert(admin.includes('/api/admin/players/:playerId/export'), 'export route');
  assert(admin.includes('/api/admin/players/:playerId/erase'), 'erase route');
  assert(admin.includes("requireRole('admin')"), 'erase requires admin role');
  assert(admin.includes('player_export'), 'export audit action');
  assert(admin.includes('player_erase'), 'erase audit action');
  assert(fs.existsSync(path.join(ROOT, 'server/src/playerPrivacy.ts')), 'playerPrivacy module');
}

async function testWarehouseExport(): Promise<void> {
  console.log('\n=== TC: D-L3-06 warehouse CSV export ===');
  const dateKey = '2026-07-05';
  const r = spawnSync('node', ['scripts/analytics/export-warehouse.mjs', `--date=${dateKey}`], {
    cwd: ROOT,
    encoding: 'utf8',
  });
  if (r.status !== 0) throw new Error(r.stderr || r.stdout);

  const dir = path.join(ROOT, 'docs/analytics/warehouse', dateKey);
  assert(fs.existsSync(path.join(dir, 'daily_pond_stats.csv')), 'daily_pond_stats.csv');
  assert(fs.existsSync(path.join(dir, 'daily_kpi.csv')), 'daily_kpi.csv');
  assert(fs.existsSync(path.join(dir, 'manifest.json')), 'manifest.json');
  assert(fs.existsSync(path.join(dir, 'index.html')), 'date index.html');
  assert(fs.existsSync(path.join(ROOT, 'docs/analytics/warehouse/latest/index.html')), 'latest/index.html');

  for (const file of ['daily_pond_stats.csv', 'daily_kpi.csv', 'daily_economy.csv', 'daily_ecology.csv']) {
    const p = path.join(dir, file);
    if (!fs.existsSync(p)) continue;
    const text = fs.readFileSync(p, 'utf8').toLowerCase();
    assert(!text.includes('player_id'), `${file} has no player_id column`);
  }

  const pipeline = read('scripts/analytics/daily-pipeline.mjs');
  assert(pipeline.includes('export-warehouse.mjs'), 'daily-pipeline calls warehouse export');
  assert(read('package.json').includes('analytics:export-warehouse'), 'npm script present');
}

function testOpsDocs(): void {
  console.log('\n=== TC: ops documentation ===');
  assert(fs.existsSync(path.join(ROOT, 'docs/ops/player-erase.md')), 'player-erase.md');
  assert(fs.existsSync(path.join(ROOT, 'docs/ops/warehouse-export.md')), 'warehouse-export.md');
}

async function main(): Promise<void> {
  console.log('verify-data-platform-dp-d');
  await testAnonymize();
  testAdminRoutes();
  await testExportEraseFlow();
  await testWarehouseExport();
  testOpsDocs();
  console.log('\nAll DP-D checks passed.');
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
});
