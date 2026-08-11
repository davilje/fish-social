import type { Server } from 'socket.io';
import { POND_ECOSYSTEM_TICK_MS, PONDS, type FishingPhase, type ClientToServerEvents, type ServerToClientEvents } from '@fish-social/shared';
import {
  getEnrichedUsersByIds,
  getWaitingUserIds,
  listUsersInPond,
  enrichPondUser,
  consumeDirtyUsers,
  syncHumanQuotaAndEmit,
} from './gameState.js';
import { startBotLoop } from './bots.js';
import { getPondEcologySummary, tickAllPonds } from './pondEcology.js';
import { getBiteCheckMs, scheduleRuntimeInterval, applyRuntimeConfigFromDb } from './runtimeConfig.js';
import { processWaitingBiteTick, tickFishingPhases } from './fishingStateMachine.js';
import { logStructuredEvent, shouldLogPerf, shouldLogFanoutInfo } from './fishingObservability.js';
import { resolveSocketByUser } from './sessionRegistry.js';
import {
  biteCheckDurationHistogram, biteEventsCounter,
  ecologyTickDurationHistogram, fishingPhaseTickDurationHistogram,
  onlinePlayersGauge, botCountGauge, metricsQueueDepthGauge,
  pondOccupancyGauge, socketBroadcastFanoutCounter,
} from './metricsPrometheus.js';
import { getPendingMetricsCount } from './fishingMetrics.js';

interface LoopDeps {
  io: Server<ClientToServerEvents, ServerToClientEvents>;
  roomFanoutCount: (pondId: string) => number;
}

const perfLogState = { lastTickAt: 0, lastBiteLoopAt: 0, lastEcologyAt: 0 };
/** BUG-15：额度日切 / 脏内存对齐扫描间隔 */
const QUOTA_DAY_SYNC_MS = 30_000;
const SESSION_TIMER_PHASES: FishingPhase[] = ['baiting', 'casting', 'waiting', 'hooked', 'resolving', 'stopping'];
const stopFns: Array<() => void> = [];
let started = false;

export function startLoops({ io, roomFanoutCount }: LoopDeps): void {
  if (started) return;
  started = true;

  const phaseTimer = setInterval(() => {
    const startedAt = Date.now();
    let snapshotCalls = 0;
    tickFishingPhases(io, {
      get(userId: string) {
        return resolveSocketByUser(userId);
      },
    });
    const durationMs = Date.now() - startedAt;
    fishingPhaseTickDurationHistogram.observe(durationMs);
    if (shouldLogPerf(perfLogState.lastTickAt, durationMs)) {
      perfLogState.lastTickAt = Date.now();
      const activeUsers = PONDS.reduce((sum, pond) => sum + listUsersInPond(pond.id).length, 0);
      logStructuredEvent('perf', 'tick_fishing_phases_duration_ms', {
        eventType: 'tick_fishing_phases_duration_ms',
        durationMs,
        pondCount: PONDS.length,
        activeUsers,
        snapshotCalls,
      });
    }
  }, 200);
  stopFns.push(() => clearInterval(phaseTimer));

  // PERF-03 / PERF-03b: lightweight session duration ticks (never consumeDirtyUsers — BUG-07)
  const sessionTimer = setInterval(() => {
    for (const pond of PONDS) {
      const fishingUsers = listUsersInPond(pond.id)
        .map(enrichPondUser)
        .filter((u) => u.fishingPhase && SESSION_TIMER_PHASES.includes(u.fishingPhase));
      if (fishingUsers.length === 0) continue;
      for (const user of fishingUsers) {
        io.to(pond.id).emit('session_timer_tick', {
          userId: user.id,
          sessionFishingMs: user.sessionFishingMs ?? 0,
        });
      }
    }
  }, 1000);
  stopFns.push(() => clearInterval(sessionTimer));

  // BUG-15：每 30s 扫描在塘人类 — 跨日 rollover + 闲置额度与 DB 对齐并推送
  const quotaDayTimer = setInterval(() => {
    try {
      syncHumanQuotaAndEmit(io);
    } catch (err) {
      console.error('[loops] syncHumanQuotaAndEmit failed', err);
    }
  }, QUOTA_DAY_SYNC_MS);
  stopFns.push(() => clearInterval(quotaDayTimer));

  const stopBiteLoop = scheduleRuntimeInterval(() => {
    const startedAt = Date.now();
    let candidates = 0;
    let triggered = 0;
    let snapshotCalls = 0;
    for (const pond of PONDS) {
      const waitingUserIds = getWaitingUserIds(pond.id);
      if (waitingUserIds.length === 0) continue;
      const users = getEnrichedUsersByIds(pond.id, waitingUserIds);
      for (const user of users) {
        if (user.isBot || !user.spotId || user.fishingPhase !== 'waiting') continue;
        candidates += 1;
        const socketId = resolveSocketByUser(user.id);
        if (!socketId || !user.playerId) continue;
        if (processWaitingBiteTick(io, pond.id, user.id, user.playerId, user.spotId, socketId)) {
          triggered += 1;
        }
      }
    }
    const loopDurationMs = Date.now() - startedAt;
    biteCheckDurationHistogram.observe(loopDurationMs);
    if (shouldLogPerf(perfLogState.lastBiteLoopAt, loopDurationMs)) {
      perfLogState.lastBiteLoopAt = Date.now();
      logStructuredEvent('perf', 'bite_check_loop_duration_ms', {
        eventType: 'bite_check_loop_duration_ms',
        durationMs: loopDurationMs,
        pondCount: PONDS.length,
        candidates,
        triggered,
        snapshotCalls,
      });
    }
  }, getBiteCheckMs);
  stopFns.push(stopBiteLoop);

  const ecologyTimer = setInterval(() => {
    const startedAt = Date.now();
    tickAllPonds();
    for (const pond of PONDS) {
      // PERF-01: emit ecology without buildSnapshot (no full user list)
      const ecology = getPondEcologySummary(pond.id);
      if (!ecology) continue;
      io.to(pond.id).emit('pond_ecology_updated', ecology);
      const fanoutCount = roomFanoutCount(pond.id);
      // OBS-LOG-1: metric always; info only when FANOUT_LOG_INFO=1
      socketBroadcastFanoutCounter.inc({ channel: 'pond_ecology_updated', pondId: pond.id });
      if (shouldLogFanoutInfo()) {
        logStructuredEvent('socket_broadcast_fanout', 'socket_broadcast_fanout', {
          eventType: 'socket_broadcast_fanout',
          channel: 'pond_ecology_updated',
          pondId: pond.id,
          fanoutCount,
        });
      }
    }
    const ecoDurationMs = Date.now() - startedAt;
    ecologyTickDurationHistogram.observe(ecoDurationMs);
    if (shouldLogPerf(perfLogState.lastEcologyAt, ecoDurationMs)) {
      perfLogState.lastEcologyAt = Date.now();
      logStructuredEvent('perf', 'ecology_tick_duration_ms', {
        eventType: 'ecology_tick_duration_ms',
        durationMs: ecoDurationMs,
        pondCount: PONDS.length,
      });
    }
  }, POND_ECOSYSTEM_TICK_MS);
  stopFns.push(() => clearInterval(ecologyTimer));

  stopFns.push(startBotLoop(io));

  // Periodic Prometheus business metrics + PERF-04 dirty drain (no broadcast)
  const metricsTimer = setInterval(() => {
    try {
      const totalUsers = PONDS.reduce((sum, pond) => sum + listUsersInPond(pond.id).length, 0);
      onlinePlayersGauge.set(totalUsers);
      metricsQueueDepthGauge.set(getPendingMetricsCount());
      for (const pond of PONDS) {
        const users = listUsersInPond(pond.id);
        pondOccupancyGauge.set({ pondId: pond.id }, users.length);
        // Drain unused dirty index (live pushes use emitUserUpdated / session_timer_tick)
        consumeDirtyUsers(pond.id);
      }
    } catch {
      // best effort metrics update
    }
  }, 5000);
  stopFns.push(() => clearInterval(metricsTimer));
  const runtimeSyncTimer = setInterval(() => applyRuntimeConfigFromDb(), 5000);
  stopFns.push(() => clearInterval(runtimeSyncTimer));
}

export function stopLoops(): void {
  while (stopFns.length > 0) {
    const stop = stopFns.pop();
    try {
      stop?.();
    } catch {
      // best effort
    }
  }
  started = false;
}
