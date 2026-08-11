/**
 * Data Platform Phase 2 DP-B 验收：D-L2-10 幂等 · D-L2-14 admin-web · D-L2-04 MetricsStore
 * 运行: npm run verify:data-platform-dp-b
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { randomUUID } from 'crypto';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function read(relPath: string): string {
  return fs.readFileSync(path.join(ROOT, relPath), 'utf8');
}

async function testEventIdIdempotency(): Promise<void> {
  console.log('\n=== TC: D-L2-10 eventId idempotency ===');
  const metrics = await import('../server/src/fishingMetrics.js');
  const { db } = await import('../server/src/db.js');

  const tableInfo = db.prepare("PRAGMA table_info('fishing_metrics')").all() as Array<{ name: string }>;
  assert(tableInfo.some((c) => c.name === 'dedup_key'), 'fishing_metrics has dedup_key column');

  const fm = read('server/src/fishingMetrics.ts');
  assert(fm.includes('resolveDedupKey'), 'resolveDedupKey helper exists');
  assert(fm.includes('IDEMPOTENT_EVENTS'), 'idempotent event set defined');
  assert(fm.includes('countDistinctCatchEvents'), 'summary uses DISTINCT catch count');

  const handler = read('server/src/socketPondHandlers.ts');
  assert(handler.includes('eventId: catchId'), 'accept_catch passes eventId=catchId');

  metrics.resetMetricsDedupCacheForTest();
  const playerId = `test-idem-${randomUUID().slice(0, 8)}`;
  const catchId = `catch-${randomUUID()}`;
  const sinceBefore = Date.now();

  metrics.recordFishingMetric('pending_catch_accept', {
    playerId,
    pondId: 'pond-calm',
    payload: { eventId: catchId, speciesId: 'carp' },
  });
  metrics.recordFishingMetric('pending_catch_accept', {
    playerId,
    pondId: 'pond-calm',
    payload: { eventId: catchId, speciesId: 'carp' },
  });
  metrics.flushFishingMetricsQueue();

  const { SqliteMetricsStore } = await import('../server/src/sqliteMetricsStore.js');
  const store = new SqliteMetricsStore(db);
  const count = store.countDistinctCatchEvents(sinceBefore);
  assert(count === 1, `duplicate accept with same eventId counts once (got ${count})`);
}

function testMetricsStoreAbstraction(): void {
  console.log('\n=== TC: D-L2-04 MetricsStore abstraction ===');
  assert(fs.existsSync(path.join(ROOT, 'server/src/metricsStore.ts')), 'metricsStore.ts exists');
  assert(fs.existsSync(path.join(ROOT, 'server/src/sqliteMetricsStore.ts')), 'sqliteMetricsStore.ts exists');
  assert(fs.existsSync(path.join(ROOT, 'server/src/postgresMetricsStore.ts')), 'postgresMetricsStore.ts exists');

  const iface = read('server/src/metricsStore.ts');
  assert(iface.includes('interface MetricsStore'), 'MetricsStore interface defined');
  assert(iface.includes('DualWriteMetricsStore'), 'dual-write store exists');
  assert(iface.includes('initMetricsStores'), 'store factory exists');

  const fm = read('server/src/fishingMetrics.ts');
  assert(fm.includes('getMetricsWriteStore'), 'fishingMetrics uses write store');
  assert(fm.includes('getMetricsReadStore'), 'fishingMetrics uses read store');

  const env = read('.env.example');
  assert(env.includes('METRICS_PG_URL'), '.env.example documents METRICS_PG_URL');
  assert(env.includes('METRICS_DUAL_WRITE'), '.env.example documents METRICS_DUAL_WRITE');
}

function testAdminWeb(): void {
  console.log('\n=== TC: D-L2-14 admin-web ===');
  assert(fs.existsSync(path.join(ROOT, 'admin-web/package.json')), 'admin-web package exists');
  assert(fs.existsSync(path.join(ROOT, 'admin-web/vite.config.ts')), 'vite config exists');

  const app = read('admin-web/src/App.tsx');
  assert(app.includes('TimelinePage'), 'timeline page wired');
  assert(app.includes('FishingDebugPage'), 'fishing-debug page wired');
  assert(app.includes('LiveInspectorPage'), 'live inspector page wired');
  assert(app.includes('BusinessHealthPage'), 'business health page wired');

  const api = read('admin-web/src/api.ts');
  assert(api.includes('/api/admin/metrics/fishing/player'), 'player timeline API client');
  assert(api.includes('/api/admin/metrics/business-health'), 'business health API client');
  assert(api.includes('/api/admin/ponds'), 'ponds API client');

  const pkg = read('package.json');
  assert(pkg.includes('admin-web'), 'root workspaces include admin-web');
}

async function main(): Promise<void> {
  console.log('verify-data-platform-dp-b');
  await testEventIdIdempotency();
  testMetricsStoreAbstraction();
  testAdminWeb();
  console.log('\nAll DP-B checks passed.');
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
});
