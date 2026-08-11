/**
 * Phase 1 Core Verification Script
 * Run: npm run verify:phase1-core
 */
import { randomUUID } from 'crypto';
import path from 'path';
import { fileURLToPath } from 'url';
import { db } from '../server/src/db.js';
import { recordFishingMetric, flushFishingMetricsQueue } from '../server/src/fishingMetrics.js';
import {
  startDebugSampling,
  stopDebugSampling,
  listActiveTargets,
  listHistory,
  stopCleanupLoop,
} from '../server/src/debugSampler.js';
import { getLokiStream, getLokiEnabled } from '../server/src/logTransportLoki.js';
import { requireRole, resolveRole } from '../server/src/adminRbac.js';
import { sendAlert } from '../server/src/alertWebhook.js';
import * as prom from '../server/src/metricsPrometheus.js';

// Import server modules
import '../server/src/db.js';

let passed = 0;
let failed = 0;

function assert(cond: boolean, msg: string): void {
  if (cond) {
    console.log('  OK: ' + msg);
    passed++;
  } else {
    console.error('  FAIL: ' + msg);
    failed++;
  }
}

// Clean up any leftover debug sampler state
stopCleanupLoop();

console.log('=== Phase 1 Core Verification ===\n');

// 1. fishing_metrics table has correlation_id column
console.log('Test 1: correlation_id column exists');
try {
  const tableInfo = db.prepare("PRAGMA table_info('fishing_metrics')").all() as Array<{ name: string }>;
  const hasCol = tableInfo.some((c) => c.name === 'correlation_id');
  assert(hasCol, 'fishing_metrics has correlation_id column');
} catch (e) {
  assert(false, 'fishing_metrics table exists: ' + String(e));
}

// 2. Write metric with correlationId and verify
console.log('\nTest 2: correlationId written to DB');
try {
  const testCorrId = 'test-corr-' + Date.now();
  recordFishingMetric('fishing_start', {
    playerId: 'verify-p1',
    payload: { correlationId: testCorrId },
  });
  flushFishingMetricsQueue();
  const rows = db.prepare('SELECT correlation_id FROM fishing_metrics WHERE correlation_id = ?').all(testCorrId) as Array<{ correlation_id: string | null }>;
  assert(rows.length > 0, 'Metric with correlationId found in DB');
} catch (e) {
  assert(false, 'write correlationId: ' + String(e));
}

// 3. Debug sample start
console.log('\nTest 3: Debug sampling');
try {
  const targetId = startDebugSampling('test-player-' + Date.now(), { reason: 'verify test' });
  assert(!!targetId, 'startDebugSampling returns targetId');
  const targets = listActiveTargets();
  assert(targets.length > 0, 'listActiveTargets returns active targets');
} catch (e) {
  assert(false, 'debug sampling: ' + String(e));
}

// 4. Debug sample stop
console.log('\nTest 4: Debug sample stop');
try {
  const playerId = 'test-player-stop-' + Date.now();
  startDebugSampling(playerId, { reason: 'verify test' });
  const stopped = stopDebugSampling(playerId);
  assert(stopped === true, 'stopDebugSampling returns true');
  const history = listHistory();
  assert(history.length > 0, 'History recorded after stop');
} catch (e) {
  assert(false, 'debug sampling stop: ' + String(e));
}

// 5. Loki does not block when disabled
console.log('\nTest 5: Loki initialization (disabled)');
try {
  const enabled = getLokiEnabled();
  console.log('  Loki enabled: ' + enabled);
  const stream = getLokiStream();
  console.log('  Loki stream: ' + (stream ? 'created' : 'null (expected when disabled)'));
  assert(true, 'Loki init does not crash when disabled');
} catch (e) {
  assert(false, 'Loki init: ' + String(e));
}

// 6. client_logs table exists
console.log('\nTest 6: client_logs table');
try {
  const exists = db.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name='client_logs'").get();
  assert(!!exists, 'client_logs table exists');
} catch (e) {
  assert(false, 'client_logs table: ' + String(e));
}

// 7. audit_log table exists
console.log('\nTest 7: audit_log table');
try {
  const exists = db.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name='audit_log'").get();
  assert(!!exists, 'audit_log table exists');
} catch (e) {
  assert(false, 'audit_log table: ' + String(e));
}

// 8. RBAC module loads
console.log('\nTest 8: RBAC module');
try {
  assert(typeof requireRole === 'function', 'requireRole is a function');
  assert(typeof resolveRole === 'function', 'resolveRole is a function');
} catch (e) {
  assert(false, 'RBAC module: ' + String(e));
}

// 9. Alert webhook module loads
console.log('\nTest 9: Alert webhook module');
try {
  assert(typeof sendAlert === 'function', 'sendAlert is a function');
} catch (e) {
  assert(false, 'alert webhook module: ' + String(e));
}

// 10. Metrics Prometheus module loads
console.log('\nTest 10: Prometheus metrics module');
try {
  assert(typeof prom.getMetricsContent === 'function', 'getMetricsContent is a function');
  assert(typeof prom.httpRequestCounter !== 'undefined', 'httpRequestCounter exists');
  assert(typeof prom.socketConnectionsGauge !== 'undefined', 'socketConnectionsGauge exists');
} catch (e) {
  assert(false, 'Prometheus module: ' + String(e));
}

// Clean up
stopCleanupLoop();

console.log('\n=== Results ===');
console.log('Passed: ' + passed);
console.log('Failed: ' + failed);
if (failed > 0) {
  console.error('Phase 1 Core Verification: SOME TESTS FAILED');
  process.exit(1);
} else {
  console.log('Phase 1 Core Verification: ALL PASSED');
}
