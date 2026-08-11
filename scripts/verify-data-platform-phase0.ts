import { existsSync, readFileSync } from 'fs';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = resolve(__dirname, '..');

let failed = 0;
let passed = 0;

function assert(cond: boolean, msg: string): void {
  if (cond) {
    passed++;
    console.log(`  PASS: ${msg}`);
  } else {
    failed++;
    console.error(`  FAIL: ${msg}`);
  }
}

async function main() {
  console.log('=== Data Platform Phase 0 Verification ===\n');

  // 1. logger module exists
  const loggerPath = resolve(root, 'server/src/logger.ts');
  assert(existsSync(loggerPath), 'logger.ts exists');

  const loggerContent = readFileSync(loggerPath, 'utf8');
  assert(loggerContent.includes('logEvent'), 'logger.ts exports logEvent');
  assert(!loggerContent.includes('export type LogLevel'), 'logger.ts has no duplicate LogLevel export');

  // 2. fishingObservability no longer uses console.log directly
  const obsPath = resolve(root, 'server/src/fishingObservability.ts');
  const obsContent = readFileSync(obsPath, 'utf8');
  assert(obsContent.includes('logEvent'), 'fishingObservability.ts uses logEvent');
  assert(!obsContent.includes('console.log'), 'fishingObservability.ts has no console.log');

  // 3. errorLogs migration exists
  const errMigPath = resolve(root, 'server/src/migrations/error_logs.ts');
  assert(existsSync(errMigPath), 'error_logs migration exists');

  // 4. daily_stats migration exists
  const dsMigPath = resolve(root, 'server/src/migrations/daily_stats.ts');
  assert(existsSync(dsMigPath), 'daily_stats migration exists');

  // 5. aggregate-daily script exists
  const aggPath = resolve(root, 'scripts/aggregate-daily-metrics.mjs');
  assert(existsSync(aggPath), 'aggregate-daily-metrics.mjs exists');

  // 6. archive script exists
  const archPath = resolve(root, 'scripts/archive-metrics.mjs');
  assert(existsSync(archPath), 'archive-metrics.mjs exists');

  // 7. backup script exists
  const backupPath = resolve(root, 'scripts/backup-db.mjs');
  assert(existsSync(backupPath), 'backup-db.mjs exists');

  // 8. daily pipeline exists
  const pipePath = resolve(root, 'scripts/analytics/daily-pipeline.mjs');
  assert(existsSync(pipePath), 'daily-pipeline.mjs exists');

  // 9. health endpoint definition
  const appPath = resolve(root, 'server/src/createApp.ts');
  const appContent = readFileSync(appPath, 'utf8');
  assert(appContent.includes('/health'), 'createApp.ts has /health endpoint');
  assert(appContent.includes('/ready'), 'createApp.ts has /ready endpoint');

  // 10. metrics-schema exists
  const schemaPath = resolve(root, 'shared/metrics-schema.ts');
  assert(existsSync(schemaPath), 'metrics-schema.ts exists');

  // 11. .env.example has the new vars
  const envPath = resolve(root, '.env.example');
  if (existsSync(envPath)) {
    const envContent = readFileSync(envPath, 'utf8');
    assert(envContent.includes('LOG_LEVEL'), '.env.example has LOG_LEVEL');
    assert(envContent.includes('LOG_DIR'), '.env.example has LOG_DIR');
    assert(envContent.includes('ERROR_LOG_RETENTION_DAYS'), '.env.example has ERROR_LOG_RETENTION_DAYS');
    assert(envContent.includes('METRICS_RETENTION_DAYS'), '.env.example has METRICS_RETENTION_DAYS');
    assert(envContent.includes('DB_BACKUP_RETAIN'), '.env.example has DB_BACKUP_RETAIN');
  } else {
    console.error('  FAIL: .env.example missing');
    failed++;
  }

  // 12. getPendingMetricsCount exported
  const metricsPath = resolve(root, 'server/src/fishingMetrics.ts');
  const metricsContent = readFileSync(metricsPath, 'utf8');
  assert(metricsContent.includes('getPendingMetricsCount'), 'fishingMetrics.ts exports getPendingMetricsCount');

  // 13. admin.ts updated for DB error logs
  const adminPath = resolve(root, 'server/src/admin.ts');
  const adminContent = readFileSync(adminPath, 'utf8');
  assert(adminContent.includes('getErrorLogsSince'), 'admin.ts uses getErrorLogsSince');
  assert(adminContent.includes('getErrorLogsByContext'), 'admin.ts uses getErrorLogsByContext');

  console.log(`\n=== ${passed} passed, ${failed} failed ===`);
  process.exit(failed > 0 ? 1 : 0);
}

main().catch((e) => {
  console.error('Verification script crashed:', e);
  process.exit(1);
});
