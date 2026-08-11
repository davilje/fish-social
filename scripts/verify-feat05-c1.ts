/**
 * FEAT-05 C1 + C7 验收
 * 运行: npm run verify:feat05-c1
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function read(relPath: string): string {
  return fs.readFileSync(path.join(ROOT, relPath), 'utf8');
}

async function testC1HotReload(): Promise<void> {
  console.log('\n=== TC: C1 hot-reload config ===');
  const cfg = read('server/src/gameConfig.ts');
  assert(cfg.includes('HOOK_DURATION_SCALE'), 'HOOK_DURATION_SCALE in config defaults');
  assert(cfg.includes('GRAY_RELEASE_PERCENT'), 'GRAY_RELEASE_PERCENT in config defaults');
  assert(cfg.includes('approveConfigChange'), 'dual-approval workflow');
  assert(read('server/src/serverLoops.ts').includes('applyRuntimeConfigFromDb'), '5s runtime refresh in serverLoops');
  assert(read('server/src/admin.ts').includes('/api/admin/config/rollback'), 'config rollback API');

  assert(read('server/src/runtimeConfig.ts').includes('getHookDurationScale'), 'hook duration scale runtime getter');
  assert(read('server/src/runtimeConfig.ts').includes('isPlayerInGrayRelease'), 'gray release cohort gate');
}

async function testC7GrayMetrics(): Promise<void> {
  console.log('\n=== TC: C7 gray metrics dashboard ===');
  assert(fs.existsSync(path.join(ROOT, 'server/src/grayMetrics.ts')), 'grayMetrics module');
  assert(read('server/src/admin.ts').includes('/api/admin/metrics/gray-dashboard'), 'gray dashboard API');
  assert(fs.existsSync(path.join(ROOT, 'scripts/analytics/weekly-gray-report.mjs')), 'weekly gray report script');

  const { getGrayMetricsDashboard } = await import('../server/src/grayMetrics.js');
  const dash = getGrayMetricsDashboard(24);
  assert(typeof dash.abandonRate === 'number', 'abandonRate in dashboard');
  assert(typeof dash.faucetSinkRatio === 'number', 'faucetSinkRatio in dashboard');
  assert(dash.businessHealth7d.days === 7, '7-day business health embedded');
}

async function main(): Promise<void> {
  console.log('verify-feat05-c1');
  await testC1HotReload();
  await testC7GrayMetrics();
  console.log('\nAll C1+C7 checks passed.');
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
});
