import { randomUUID } from 'crypto';

import { validateMetricPayload } from '@fish-social/shared/metrics-schema.js';

import { db } from './db.js';

import {

  getMetricsReadStore,

  getMetricsWriteStore,

  initMetricsStores,

  type MetricInsertRow,

} from './metricsStore.js';

import { SqliteMetricsStore } from './sqliteMetricsStore.js';

import { createPostgresMetricsStoreIfConfigured, assertMetricsReadFromSupported } from './postgresMetricsStore.js';

export type FishingMetricEvent =

  | 'fishing_start'

  | 'fishing_stop'

  | 'bite_hook'

  | 'catch_accept'

  | 'pending_catch_accept'

  | 'escape'

  | 'bait_buy'

  | 'tackle_buy'

  | 'tackle_repair'

  | 'gold_earn'

  | 'abandon_fishing'

  | 'escape_streak'

  | 'socket_connect'

  | 'socket_connect_error'

  | 'join_pond_attempt'

  | 'join_pond_success'

  | 'join_pond_fail'

  | 'spot_take_success'

  | 'spot_take_fail'

  | 'spot_release'

  | 'pond_full_reject'

  | 'bot_evicted_for_human'

  | 'disconnect'

  | 'socket_disconnect'

  | 'reconnect'

  | 'disconnect_timeout'

  | 'leave_pond'

  | 'fishing_phase_transition'

  | 'phase_transition_invalid'

  | 'bite_tick_miss'

  | 'bite_tick_hit'

  | 'pending_catch_created'

  | 'pending_catch_expired'

  | 'bait_depleted'

  | 'server_start'

  | 'server_stop'

  | 'admission_fee_charged'

  | 'fishing_stopped_insufficient_gold'

  | 'onboarding_completed'

  | 'pond_proficiency_capped'
  | 'bait_use'
  | 'rod_buy'
  | 'rod_broke'
  | 'vessel_buy'
  | 'forbidden_pond_fine'
  | 'forbidden_pond_escaped'
  | 'gameplay_debug_action'
  | 'fish_returned_to_pond';

const IDEMPOTENT_EVENTS = new Set<FishingMetricEvent>(['catch_accept', 'pending_catch_accept']);



const sqliteStore = new SqliteMetricsStore(db);

assertMetricsReadFromSupported();
const postgresStore = createPostgresMetricsStoreIfConfigured();

initMetricsStores(sqliteStore, postgresStore);



const escapeStreakByPlayer = new Map<string, number>();

const fishingStartAtByPlayer = new Map<string, number>();

const pendingDedupKeys = new Set<string>();



const METRIC_BATCH_FLUSH_MS = 1000;

const METRIC_BATCH_SIZE = 50;

const pendingRows: MetricInsertRow[] = [];

let flushTimer: NodeJS.Timeout | null = null;



function resolveDedupKey(eventType: FishingMetricEvent, payload: Record<string, unknown>): string | null {

  const eventId = payload.eventId;

  if (typeof eventId !== 'string' || !eventId || !IDEMPOTENT_EVENTS.has(eventType)) return null;

  return `${eventType}:${eventId}`;

}



function ensureFlushTimer(): void {

  if (flushTimer) return;

  flushTimer = setInterval(() => flushFishingMetricsQueue(), METRIC_BATCH_FLUSH_MS);

  flushTimer.unref();

}



export function flushFishingMetricsQueue(): number {

  if (pendingRows.length === 0) return 0;

  const rows = pendingRows.splice(0, pendingRows.length);

  const { inserted } = getMetricsWriteStore().insertBatch(rows);

  return inserted;

}



export function getPendingMetricsCount(): number {

  return pendingRows.length;

}



export function stopFishingMetricsQueue(): void {

  if (flushTimer) {

    clearInterval(flushTimer);

    flushTimer = null;

  }

  flushFishingMetricsQueue();

}



export function recordFishingMetric(

  eventType: FishingMetricEvent,

  opts: {

    playerId?: string;

    pondId?: string;

    payload?: Record<string, unknown>;

  } = {},

): void {

  // D-L2-16 P1: skip bot metrics when enabled (playerId starts with bot-)
  if (
    (process.env.METRICS_SKIP_BOTS === '1' || process.env.METRICS_SKIP_BOTS === 'true') &&
    typeof opts.playerId === 'string' &&
    opts.playerId.startsWith('bot-')
  ) {
    return;
  }

  if (process.env.NODE_ENV !== 'production') {

    const warnings = validateMetricPayload(eventType, {
      ...(opts.payload ?? {}),
      ...(opts.playerId != null ? { playerId: opts.playerId } : {}),
      ...(opts.pondId != null ? { pondId: opts.pondId } : {}),
    });

    for (const w of warnings) {

      try {

        process.stderr.write(`[metrics-validation] ${w}\n`);

      } catch {

        /* EPIPE when parent closed the pipe */

      }

    }

  }

  const payload = opts.payload ?? {};

  const dedupKey = resolveDedupKey(eventType, payload);

  if (dedupKey) {

    if (pendingDedupKeys.has(dedupKey)) return;

    pendingDedupKeys.add(dedupKey);

  }



  pendingRows.push({

    id: randomUUID(),

    eventType,

    playerId: opts.playerId ?? null,

    pondId: opts.pondId ?? null,

    payload: JSON.stringify(payload),

    correlationId: (payload.correlationId as string | undefined) ?? null,

    dedupKey,

    createdAt: Date.now(),

  });

  ensureFlushTimer();

  if (pendingRows.length >= METRIC_BATCH_SIZE) flushFishingMetricsQueue();



  if (opts.playerId) {

    if (eventType === 'fishing_start') {

      fishingStartAtByPlayer.set(opts.playerId, Date.now());

    }

    if (eventType === 'fishing_stop' && fishingStartAtByPlayer.has(opts.playerId)) {

      const started = fishingStartAtByPlayer.get(opts.playerId)!;

      if (Date.now() - started < 30_000) {

        recordFishingMetric('abandon_fishing', { playerId: opts.playerId, pondId: opts.pondId });

      }

      fishingStartAtByPlayer.delete(opts.playerId);

    }

    if (eventType === 'escape') {

      const streak = (escapeStreakByPlayer.get(opts.playerId) ?? 0) + 1;

      escapeStreakByPlayer.set(opts.playerId, streak);

      if (streak >= 3) {

        recordFishingMetric('escape_streak', {

          playerId: opts.playerId,

          pondId: opts.pondId,

          payload: { streak, eventId: `escape_streak:${opts.playerId}:${Date.now()}` },

        });

      }

    }

    if (eventType === 'catch_accept' || eventType === 'pending_catch_accept') {

      escapeStreakByPlayer.set(opts.playerId, 0);

    }

  }

}



export interface FishingMetricsSummary {

  periodHours: number;

  totalEvents: number;

  escapeStreakPlayers: number;

  abandonRate: number;

  catchCount: number;

  escapeCount: number;

  baitPurchases: Record<string, number>;

  tacklePurchases: Record<string, number>;

  faucetCoinsEstimate: number;

  sinkCoinsEstimate: number;

  alerts: string[];

}



export function getFishingMetricsSummary(hours = 168): FishingMetricsSummary {

  flushFishingMetricsQueue();

  const since = Date.now() - hours * 60 * 60 * 1000;

  const readStore = getMetricsReadStore();

  const rows = readStore.queryRawSince(since);



  const baitPurchases: Record<string, number> = {};

  const tacklePurchases: Record<string, number> = {};

  let escapeCount = 0;

  let abandonCount = 0;

  let startCount = 0;

  let faucet = 0;

  let sink = 0;

  const streakPlayers = new Set<string>();



  for (const row of rows) {

    let payload: Record<string, unknown> = {};

    try {

      payload = JSON.parse(row.payload);

    } catch {

      /* ignore */

    }

    switch (row.event_type) {

      case 'escape':

        escapeCount += 1;

        break;

      case 'fishing_start':

        startCount += 1;

        break;

      case 'abandon_fishing':

        abandonCount += 1;

        break;

      case 'escape_streak':

        if (row.player_id) streakPlayers.add(row.player_id);

        break;

      case 'bait_buy':

        baitPurchases[String(payload.baitId ?? 'unknown')] =

          (baitPurchases[String(payload.baitId ?? 'unknown')] ?? 0) + Number(payload.quantity ?? 1);

        sink += Number(payload.cost ?? 0);

        break;

      case 'tackle_buy':

        tacklePurchases[String(payload.tackleId ?? 'unknown')] =

          (tacklePurchases[String(payload.tackleId ?? 'unknown')] ?? 0) + 1;

        sink += Number(payload.cost ?? 0);

        break;

      case 'tackle_repair':

        sink += Number(payload.cost ?? 0);

        break;

      default:

        break;

    }

    if (payload.coinsEarned) faucet += Number(payload.coinsEarned);

  }



  const catchCount = readStore.countDistinctCatchEvents(since);

  const abandonRate = startCount > 0 ? abandonCount / startCount : 0;

  const streakRate = streakPlayers.size;

  const alerts: string[] = [];

  if (startCount > 0 && abandonRate >= 0.15) {

    alerts.push(`弃钓率 ${(abandonRate * 100).toFixed(1)}% ≥ 15% 预警阈值`);

  }

  if (streakRate >= 3) {

    alerts.push(`连续脱钩 3+ 次玩家 ${streakRate} 人（样本期内）`);

  }



  return {

    periodHours: hours,

    totalEvents: rows.length,

    escapeStreakPlayers: streakPlayers.size,

    abandonRate,

    catchCount,

    escapeCount,

    baitPurchases,

    tacklePurchases,

    faucetCoinsEstimate: faucet,

    sinkCoinsEstimate: sink,

    alerts,

  };

}



export interface PlayerFishingTimelineEvent {

  id: string;

  eventType: string;

  pondId: string | null;

  payload: Record<string, unknown>;

  createdAt: number;

}



export interface PlayerFishingTimelineSummary {

  socketConnectCount: number;

  socketConnectErrorCount: number;

  joinPondAttemptCount: number;

  joinPondSuccessCount: number;

  joinPondFailCount: number;

  spotTakeSuccessCount: number;

  spotTakeFailCount: number;

  spotReleaseCount: number;

  pondFullRejectCount: number;

  botEvictedForHumanCount: number;

  disconnectCount: number;

  reconnectCount: number;

  disconnectTimeoutCount: number;

  leavePondCount: number;

  fishingStartCount: number;

  biteHookCount: number;

  biteTickMissCount: number;

  biteTickHitCount: number;

  pendingCatchCreatedCount: number;

  pendingCatchExpiredCount: number;

  baitDepletedCount: number;

  phaseTransitionCount: number;

  phaseTransitionInvalidCount: number;

  catchAcceptCount: number;

  lastEventAt: number | null;

}



export interface PlayerFishingTimeline {

  playerId: string;

  hours: number;

  events: PlayerFishingTimelineEvent[];

  summary: PlayerFishingTimelineSummary;

}



export function getPlayerFishingTimeline(

  playerId: string,

  hours = 24,

  limit = 500,

): PlayerFishingTimeline {

  flushFishingMetricsQueue();

  const since = Date.now() - hours * 60 * 60 * 1000;

  const cappedLimit = Math.min(1000, Math.max(1, limit));



  const rows = getMetricsReadStore().queryPlayerTimeline(playerId, since, cappedLimit);



  const events: PlayerFishingTimelineEvent[] = rows.map((row) => {

    let payload: Record<string, unknown> = {};

    try {

      payload = JSON.parse(row.payload);

    } catch {

      /* ignore */

    }

    return {

      id: row.id,

      eventType: row.event_type,

      pondId: row.pond_id,

      payload,

      createdAt: row.created_at,

    };

  });



  const summary: PlayerFishingTimelineSummary = {

    socketConnectCount: 0,

    socketConnectErrorCount: 0,

    joinPondAttemptCount: 0,

    joinPondSuccessCount: 0,

    joinPondFailCount: 0,

    spotTakeSuccessCount: 0,

    spotTakeFailCount: 0,

    spotReleaseCount: 0,

    pondFullRejectCount: 0,

    botEvictedForHumanCount: 0,

    disconnectCount: 0,

    reconnectCount: 0,

    disconnectTimeoutCount: 0,

    leavePondCount: 0,

    fishingStartCount: 0,

    biteHookCount: 0,

    biteTickMissCount: 0,

    biteTickHitCount: 0,

    pendingCatchCreatedCount: 0,

    pendingCatchExpiredCount: 0,

    baitDepletedCount: 0,

    phaseTransitionCount: 0,

    phaseTransitionInvalidCount: 0,

    catchAcceptCount: 0,

    lastEventAt: events.length > 0 ? events[events.length - 1]!.createdAt : null,

  };



  for (const event of events) {

    switch (event.eventType) {

      case 'socket_connect':

        summary.socketConnectCount += 1;

        break;

      case 'socket_connect_error':

        summary.socketConnectErrorCount += 1;

        break;

      case 'join_pond_attempt':

        summary.joinPondAttemptCount += 1;

        break;

      case 'join_pond_success':

        summary.joinPondSuccessCount += 1;

        break;

      case 'join_pond_fail':

        summary.joinPondFailCount += 1;

        break;

      case 'spot_take_success':

        summary.spotTakeSuccessCount += 1;

        break;

      case 'spot_take_fail':

        summary.spotTakeFailCount += 1;

        break;

      case 'spot_release':

        summary.spotReleaseCount += 1;

        break;

      case 'pond_full_reject':

        summary.pondFullRejectCount += 1;

        break;

      case 'bot_evicted_for_human':

        summary.botEvictedForHumanCount += 1;

        break;

      case 'disconnect':

      case 'socket_disconnect':

        summary.disconnectCount += 1;

        break;

      case 'reconnect':

        summary.reconnectCount += 1;

        break;

      case 'disconnect_timeout':

        summary.disconnectTimeoutCount += 1;

        break;

      case 'leave_pond':

        summary.leavePondCount += 1;

        break;

      case 'fishing_start':

        summary.fishingStartCount += 1;

        break;

      case 'bite_hook':

        summary.biteHookCount += 1;

        break;

      case 'bite_tick_miss':

        summary.biteTickMissCount += 1;

        break;

      case 'bite_tick_hit':

        summary.biteTickHitCount += 1;

        break;

      case 'pending_catch_created':

        summary.pendingCatchCreatedCount += 1;

        break;

      case 'pending_catch_expired':

        summary.pendingCatchExpiredCount += 1;

        break;

      case 'catch_accept':

      case 'pending_catch_accept':

        summary.catchAcceptCount += 1;

        break;

      case 'bait_depleted':

        summary.baitDepletedCount += 1;

        break;

      case 'fishing_phase_transition':

        summary.phaseTransitionCount += 1;

        break;

      case 'phase_transition_invalid':

        summary.phaseTransitionInvalidCount += 1;

        break;

      default:

        break;

    }

  }



  return { playerId, hours, events, summary };

}



/** @internal test helper */

export function resetMetricsDedupCacheForTest(): void {

  pendingDedupKeys.clear();

}


