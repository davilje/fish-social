import type { FishingPhase, PondUser } from '@fish-social/shared';
import { compactPhaseTransitionPayload } from '@fish-social/shared';
import { AsyncLocalStorage } from 'node:async_hooks';
import { recordFishingMetric, type FishingMetricEvent } from './fishingMetrics.js';
import { resolveCorrelationIdBySocket } from './sessionRegistry.js';
import { logEvent } from './logger.js';

export interface ObservabilityBaseFields {
  playerId?: string;
  userId?: string;
  socketId?: string;
  correlationId?: string;
  pondId?: string;
  spotId?: string | null;
  fishingPhase?: FishingPhase | null;
  reason?: string;
  isBot?: boolean;
}

export interface PhaseTransitionFields extends ObservabilityBaseFields {
  fromPhase: FishingPhase | null;
  toPhase: FishingPhase;
  cause: string;
  phaseElapsedMs?: number;
  phaseDeadlineTs?: number | null;
}

/** 性能日志默认节流间隔（可用 PERF_LOG_INTERVAL_MS 覆盖） */
export const PERF_LOG_INTERVAL_MS = Number(process.env.PERF_LOG_INTERVAL_MS ?? 30_000);
/** OBS-LOG-1: 超过此耗时才打 perf info（PERF_LOG_INFO=1 时忽略阈值、仍受间隔节流） */
export const PERF_LOG_SLOW_MS = Number(process.env.PERF_LOG_SLOW_MS ?? 50);

let lastPerfLogAt = 0;
const correlationStorage = new AsyncLocalStorage<string>();

/**
 * OBS-LOG-1: Histogram 常开；info 仅慢路径或 PERF_LOG_INFO=1。
 * @param lastAt 上次打日志时间（节流）
 * @param durationMs 本次耗时；未传时仅在 PERF_LOG_INFO=1 下按间隔打
 */
export function shouldLogPerf(lastAt?: number, durationMs?: number): boolean {
  const forceInfo = process.env.PERF_LOG_INFO === '1' || process.env.PERF_LOG_INFO === 'true';
  if (!forceInfo) {
    if (durationMs == null || durationMs < PERF_LOG_SLOW_MS) return false;
  }
  const baseline = lastAt ?? lastPerfLogAt;
  return Date.now() - baseline >= PERF_LOG_INTERVAL_MS;
}

export function markPerfLogged(at = Date.now()): number {
  lastPerfLogAt = at;
  return lastPerfLogAt;
}

/** OBS-LOG-1: ecology console / structured supplement detail */
export function isEcologyVerbose(): boolean {
  return process.env.ECOLOGY_VERBOSE === '1' || process.env.ECOLOGY_VERBOSE === 'true';
}

/** OBS-LOG-1: restore fanout info logs when FANOUT_LOG_INFO=1 */
export function shouldLogFanoutInfo(): boolean {
  return process.env.FANOUT_LOG_INFO === '1' || process.env.FANOUT_LOG_INFO === 'true';
}

const TIMED_PHASE_DURATIONS: Partial<Record<FishingPhase, number>> = {
  baiting: 800,
  casting: 600,
  hooked: 0,
  resolving: 0,
  stopping: 200,
  disconnected: 60_000,
};

export function logStructuredEvent(
  prefix: string,
  eventType: string,
  fields: ObservabilityBaseFields & Record<string, unknown>,
): void {
  const socketId = typeof fields.socketId === 'string' ? fields.socketId : undefined;
  const correlationId =
    (typeof fields.correlationId === 'string' ? fields.correlationId : undefined) ??
    (socketId ? resolveCorrelationIdBySocket(socketId) : undefined) ??
    correlationStorage.getStore();
  logEvent(prefix, eventType, {
    ...(correlationId ? { correlationId } : {}),
    ...fields,
  });
}

export function runWithCorrelationId<T>(correlationId: string | undefined, fn: () => T): T {
  if (!correlationId) return fn();
  return correlationStorage.run(correlationId, fn);
}

export function recordStructuredMetric(
  eventType: FishingMetricEvent,
  fields: ObservabilityBaseFields & Record<string, unknown>,
): void {
  recordFishingMetric(eventType, {
    playerId: fields.playerId,
    pondId: fields.pondId,
    payload: {
      userId: fields.userId,
      socketId: fields.socketId,
      spotId: fields.spotId,
      fishingPhase: fields.fishingPhase,
      reason: fields.reason,
      isBot: fields.isBot,
      ...fields,
    },
  });
}

export function estimatePhaseElapsedMs(
  fromPhase: FishingPhase | null,
  phaseEndsAt: number | null | undefined,
): number | undefined {
  if (!fromPhase || phaseEndsAt == null) return undefined;
  const durationMs = TIMED_PHASE_DURATIONS[fromPhase];
  if (!durationMs || durationMs <= 0) return undefined;
  return Math.max(0, durationMs - Math.max(0, phaseEndsAt - Date.now()));
}

export function recordPhaseTransition(fields: PhaseTransitionFields): void {
  const isBot = !!fields.isBot || (typeof fields.playerId === 'string' && fields.playerId.startsWith('bot-'));
  const botPhaseMetricsOn =
    process.env.METRICS_BOT_PHASE === '1' || process.env.METRICS_BOT_PHASE === 'true';

  // ADMIN-OBS-1.3: default skip bot phase metrics + info logs (opt-in METRICS_BOT_PHASE=1)
  if (isBot && !botPhaseMetricsOn) {
    return;
  }

  // Logs keep full phase names for humans (D-L2-16)
  logStructuredEvent(
    'phase_transition',
    'fishing_phase_transition',
    fields as unknown as ObservabilityBaseFields & Record<string, unknown>,
  );
  // Metrics: only short codes { f, t, c }
  recordFishingMetric('fishing_phase_transition', {
    playerId: fields.playerId,
    pondId: fields.pondId,
    payload: compactPhaseTransitionPayload(fields.fromPhase, fields.toPhase, fields.cause),
  });
}

export function buildUserFields(
  user: PondUser,
  pondId: string,
  extra: Partial<ObservabilityBaseFields> = {},
): ObservabilityBaseFields {
  return {
    playerId: user.playerId,
    userId: user.id,
    pondId,
    spotId: user.spotId,
    fishingPhase: user.fishingPhase ?? null,
    isBot: user.isBot,
    ...extra,
  };
}

