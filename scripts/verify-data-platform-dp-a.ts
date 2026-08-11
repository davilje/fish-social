/**
 * Data Platform Phase 2 DP-A 验收：D-L1-10 OTel · D-L1-12 Socket Tap · D-L2-09 业务健康看板
 * 运行: npm run verify:data-platform-dp-a
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

async function testOtelTracing(): Promise<void> {
  console.log('\n=== TC: D-L1-10 OpenTelemetry tracing ===');
  const {
    clearRecordedSpans,
    correlationToTraceId,
    listSpansByCorrelationId,
    withTraceSpan,
  } = await import('../server/src/otelTracing.js');

  clearRecordedSpans();
  const correlationId = randomUUID();
  withTraceSpan('join_pond', correlationId, { pondId: 'pond-calm' }, () => {
    withTraceSpan('bite_check.hit', correlationId, { speciesId: 'carp' }, () => undefined);
  });
  withTraceSpan('socket.disconnect', correlationId, { reason: 'transport close' }, () => undefined);

  const spans = listSpansByCorrelationId(correlationId);
  assert(spans.length === 3, 'recorded join→bite→disconnect span chain');
  assert(spans.some((s) => s.name === 'join_pond'), 'join_pond span present');
  assert(spans.some((s) => s.name === 'bite_check.hit'), 'bite_check.hit span present');
  assert(spans.some((s) => s.name === 'socket.disconnect'), 'disconnect span present');
  assert(correlationToTraceId(correlationId).length === 32, 'trace_id derived from correlationId');

  const otel = read('server/src/otelTracing.ts');
  assert(otel.includes('OTLPTraceExporter'), 'OTLP exporter wired');
  assert(otel.includes('listSpansByCorrelationId'), 'admin span buffer API exists');
  assert(read('server/src/index.ts').includes('initOtelTracing'), 'server boot initializes OTel');
}

function testSocketEventTapWiring(): void {
  console.log('\n=== TC: D-L1-12 Socket event tap ===');
  const tap = read('server/src/socketEventTap.ts');
  assert(tap.includes('onAny'), 'socket onAny tap registered');
  assert(tap.includes('onAnyOutgoing'), 'socket onAnyOutgoing tap registered');
  assert(tap.includes('SOCKET_TAP_SAMPLE_RATE'), 'production sample rate configurable');
  assert(tap.includes('isDebugSampled'), 'debug target 100% sampling');
  assert(tap.includes('socket_tap_unknown'), 'unknown events logged as warn path');

  const lifecycle = read('server/src/socketLifecycle.ts');
  assert(lifecycle.includes('registerSocketEventTap'), 'socketLifecycle installs event tap');
}

async function testBusinessHealth(): Promise<void> {
  console.log('\n=== TC: D-L2-09 business health dashboard ===');
  const { getBusinessHealthTrend } = await import('../server/src/businessHealth.js');
  const trend = getBusinessHealthTrend(7);
  assert(trend.days === 7, 'returns 7-day window');
  assert(trend.daily.length === 7, 'daily series has 7 entries');
  assert(typeof trend.totals.catchCount === 'number', 'totals.catchCount numeric');
  assert(trend.daily.every((d) => Array.isArray(d.ponds)), 'each day has ponds array');

  const admin = read('server/src/admin.ts');
  assert(admin.includes('/api/admin/metrics/business-health'), 'admin business-health API');
  assert(admin.includes('/api/admin/traces'), 'admin traces API by correlationId');

  const panel = read('mobile/components/AdminMetricsPanel.tsx');
  assert(panel.includes('getBusinessHealth') || panel.includes('业务健康'), 'admin UI shows business health');
}

function testDeployArtifacts(): void {
  console.log('\n=== TC: DP-A deploy artifacts ===');
  assert(fs.existsSync(path.join(ROOT, 'docker/docker-compose.otel.yml')), 'otel compose file exists');
  const env = read('.env.example');
  assert(env.includes('OTEL_ENABLED'), '.env.example documents OTEL_ENABLED');
  assert(env.includes('SOCKET_TAP_SAMPLE_RATE'), '.env.example documents SOCKET_TAP_SAMPLE_RATE');
}

async function main(): Promise<void> {
  console.log('verify-data-platform-dp-a');
  await testOtelTracing();
  testSocketEventTapWiring();
  await testBusinessHealth();
  testDeployArtifacts();
  console.log('\nAll DP-A checks passed.');
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : err);
  process.exit(1);
});
