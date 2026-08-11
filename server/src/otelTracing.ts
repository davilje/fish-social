import { randomBytes } from 'crypto';
import { context, trace, SpanStatusCode, TraceFlags, type Span } from '@opentelemetry/api';

export interface RecordedSpan {
  name: string;
  correlationId: string;
  traceId: string;
  spanId: string;
  parentSpanId?: string;
  startedAt: number;
  durationMs: number;
  attributes: Record<string, string | number | boolean>;
  status: 'ok' | 'error';
}

const OTEL_ENABLED = process.env.OTEL_ENABLED === 'true';
const MAX_RECORDED_SPANS = Number(process.env.OTEL_SPAN_BUFFER_SIZE ?? 2000);

const recordedSpans: RecordedSpan[] = [];
let sdk: { shutdown: () => Promise<void> } | null = null;
let tracer = trace.getTracer('fish-social-server');

function randomSpanId(): string {
  return randomBytes(8).toString('hex');
}

export function correlationToTraceId(correlationId: string): string {
  const hex = correlationId.replace(/-/g, '').toLowerCase();
  if (/^[0-9a-f]{32}$/.test(hex)) return hex;
  return randomBytes(16).toString('hex');
}

function pushRecorded(span: RecordedSpan): void {
  recordedSpans.push(span);
  if (recordedSpans.length > MAX_RECORDED_SPANS) {
    recordedSpans.splice(0, recordedSpans.length - MAX_RECORDED_SPANS);
  }
}

function cleanAttributes(
  attrs: Record<string, string | number | boolean | undefined | null>,
): Record<string, string | number | boolean> {
  const out: Record<string, string | number | boolean> = {};
  for (const [key, value] of Object.entries(attrs)) {
    if (value === undefined || value === null) continue;
    out[key] = value;
  }
  return out;
}

export async function initOtelTracing(): Promise<void> {
  if (!OTEL_ENABLED || sdk) return;
  const [{ NodeSDK }, { OTLPTraceExporter }, { resourceFromAttributes }, { ATTR_SERVICE_NAME }] =
    await Promise.all([
      import('@opentelemetry/sdk-node'),
      import('@opentelemetry/exporter-trace-otlp-http'),
      import('@opentelemetry/resources'),
      import('@opentelemetry/semantic-conventions'),
    ]);
  const endpoint = process.env.OTEL_EXPORTER_OTLP_ENDPOINT ?? 'http://127.0.0.1:4318/v1/traces';
  const nodeSdk = new NodeSDK({
    resource: resourceFromAttributes({
      [ATTR_SERVICE_NAME]: process.env.OTEL_SERVICE_NAME ?? 'fish-social-server',
    }),
    traceExporter: new OTLPTraceExporter({ url: endpoint }),
  });
  nodeSdk.start();
  sdk = nodeSdk;
  tracer = trace.getTracer('fish-social-server');
}

export async function shutdownOtelTracing(): Promise<void> {
  if (!sdk) return;
  await sdk.shutdown();
  sdk = null;
}

export function isOtelEnabled(): boolean {
  return OTEL_ENABLED;
}

export function withTraceSpan<T>(
  name: string,
  correlationId: string | undefined,
  attributes: Record<string, string | number | boolean | undefined | null>,
  fn: () => T,
): T {
  if (!correlationId) return fn();

  const traceId = correlationToTraceId(correlationId);
  const spanId = randomSpanId();
  const attrs = cleanAttributes({ ...attributes, 'correlation.id': correlationId });
  const parentCtx = trace.setSpanContext(context.active(), {
    traceId,
    spanId,
    traceFlags: TraceFlags.SAMPLED,
    isRemote: true,
  });

  return context.with(parentCtx, () =>
    tracer.startActiveSpan(name, { attributes: attrs }, (span: Span) => {
      const startedAt = Date.now();
      try {
        const result = fn();
        span.setStatus({ code: SpanStatusCode.OK });
        pushRecorded({
          name,
          correlationId,
          traceId,
          spanId,
          startedAt,
          durationMs: Date.now() - startedAt,
          attributes: attrs,
          status: 'ok',
        });
        return result;
      } catch (err) {
        span.setStatus({
          code: SpanStatusCode.ERROR,
          message: err instanceof Error ? err.message : String(err),
        });
        pushRecorded({
          name,
          correlationId,
          traceId,
          spanId,
          startedAt,
          durationMs: Date.now() - startedAt,
          attributes: attrs,
          status: 'error',
        });
        throw err;
      } finally {
        span.end();
      }
    }),
  );
}

export function listSpansByCorrelationId(correlationId: string, limit = 200): RecordedSpan[] {
  return recordedSpans
    .filter((s) => s.correlationId === correlationId)
    .sort((a, b) => a.startedAt - b.startedAt)
    .slice(-limit);
}

export function clearRecordedSpans(): void {
  recordedSpans.length = 0;
}
