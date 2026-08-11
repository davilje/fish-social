import { randomUUID } from 'crypto';
import type { Server } from 'socket.io';
import {
  getBait,
  PONDS,
  MAX_DAILY_FISHING_MS,
  isFishingActive,
  compactPhaseTransitionPayload,
  type ClientToServerEvents,
  type FishingMiss,
  type FishingPhase,
  type PendingFishCatch,
  type PondFishEntity,
  type PondUser,
  type ServerToClientEvents,
} from '@fish-social/shared';
import {
  updatePondUser,
  listUsersInPond,
  getUserById,
  flushFishingSessionToToday,
  settleFishingSession,
  emitPondUserUpdated,
  markUserDirty,
  setUserWaiting,
  getTodayFishingMs,
  initQuotaCheckpoint,
  clearQuotaCheckpoint,
} from './gameState.js';
import { ensureFishingStartedAt } from './fishingStartedAt.js';
import {
  applyTackleDurabilityOnEscape,
  consumeBaitAtBaitingStart,
  getPlayerGear,
  prepareGearForBiteTick,
} from './gear.js';
import { getConfigBool } from './gameConfig.js';
import { recordFishingMetric } from './fishingMetrics.js';
import { emitFishingFloatText, emitFishingMissFloatText, emitFishingCastFloatText } from './fishingFloatText.js';
import {
  buildUserFields,
  estimatePhaseElapsedMs,
  logStructuredEvent,
  recordPhaseTransition,
} from './fishingObservability.js';
import { withTraceSpan } from './otelTracing.js';
import { resolveCorrelationIdByPlayer } from './sessionRegistry.js';
import { notifyBotHookCatch } from './botHookCatch.js';
import {
  upsertPlayerPondSession,
  deletePlayerPondSession,
} from './playerPondSession.js';
import { register, cancelByKind, hasActiveTimer } from './timerRegistry.js';
import {
  rollBiteHook,
  type BiteHookEvent,
  type FisherGearContext,
} from './fishingSession.js';
import { getLockedPondFishIds, getPendingCatch, lockPendingCatch } from './inventory.js';
import { isCodexNewForPlayer } from './codex.js';
import { applyEscapeGrowthBonus } from './pondEcology.js';
import {
  biteSessionMetricPayload,
  clearBiteSessionCounters,
  isBiteTickPersistEnabled,
  noteBiteEscape,
  noteBiteHook,
  noteBiteMiss,
  resetBiteSessionCounters,
} from './biteSessionCounters.js';

const DISCONNECT_TIMEOUT_MS = 60_000;
const PHASE_MS = {
  baiting: 800,
  casting: 600,
  resolvingCatch: 800,
  resolvingEscape: 600,
  stopping: 200,
  minBaiting: 200,
} as const;

interface HookContext {
  fish: PondFishEntity;
  escaped: boolean;
  hookDurationMs: number;
  /** 断线时保存的上钩截止时间，供重连续接 */
  hookEndsAt?: number;
}

interface SessionFlags {
  leaveAfterResolve?: boolean;
  stopToSeated?: boolean;
}

const hookContextByUser = new Map<string, HookContext>();
const sessionFlagsByUser = new Map<string, SessionFlags>();
const VALID_PHASE_TRANSITIONS: Record<FishingPhase, FishingPhase[]> = {
  idle: ['seated', 'disconnected'],
  seated: ['baiting', 'idle', 'disconnected'],
  baiting: ['casting', 'seated', 'disconnected'],
  casting: ['waiting', 'disconnected'],
  waiting: ['hooked', 'stopping', 'disconnected'],
  hooked: ['resolving', 'waiting', 'disconnected'],
  resolving: ['baiting', 'seated', 'idle', 'disconnected'],
  stopping: ['seated', 'idle', 'disconnected'],
  disconnected: ['waiting', 'seated', 'idle', 'hooked', 'resolving'],
};

function now(): number {
  return Date.now();
}

function syncStatus(user: PondUser): PondUser {
  const phase = user.fishingPhase;
  user.status = isFishingActive(phase) ? 'fishing' : 'idle';
  if (user.status === 'idle') {
    user.fishingStartedAt = null;
    user.sessionStartedAt = null;
    return user;
  }
  // BUG-19：stopping 可能已 finalize 清空锚点，禁止 revive
  if (phase === 'stopping') {
    return user;
  }
  ensureFishingStartedAt(user);
  if (user.sessionStartedAt == null && user.fishingStartedAt != null) {
    user.sessionStartedAt = user.fishingStartedAt;
  } else if (user.fishingStartedAt == null && user.sessionStartedAt != null) {
    user.fishingStartedAt = user.sessionStartedAt;
  }
  return user;
}

function setPhase(
  user: PondUser,
  phase: FishingPhase,
  durationMs: number,
  context?: PondUser['phaseContext'],
): PondUser {
  user.fishingPhase = phase;
  user.phaseEndsAt = durationMs > 0 ? now() + durationMs : null;
  if (context !== undefined) user.phaseContext = context;
  syncStatus(user);
  return user;
}

function transitionPhase(
  user: PondUser,
  pondId: string,
  phase: FishingPhase,
  durationMs: number,
  cause: string,
  context?: PondUser['phaseContext'],
  extra?: { socketId?: string | null; fromPhase?: FishingPhase | null },
): PondUser {
  const fromPhase = extra?.fromPhase ?? (user.fishingPhase ?? null);
  if (fromPhase) {
    const allowed = VALID_PHASE_TRANSITIONS[fromPhase] ?? [];
    const valid = allowed.includes(phase);
    if (!valid) {
      const invalidPayload = {
        ...buildUserFields(user, pondId, { socketId: extra?.socketId ?? undefined }),
        fromPhase,
        toPhase: phase,
        cause,
        phaseElapsedMs: estimatePhaseElapsedMs(fromPhase, user.phaseEndsAt),
        phaseDeadlineTs: user.phaseEndsAt,
        isReconnectPath: fromPhase === 'disconnected',
      };
      const botPhaseMetricsOn =
        process.env.METRICS_BOT_PHASE === '1' || process.env.METRICS_BOT_PHASE === 'true';
      if (!user.isBot || botPhaseMetricsOn) {
        logStructuredEvent('phase_transition_invalid', 'phase_transition_invalid', invalidPayload);
        recordFishingMetric('phase_transition_invalid', {
          playerId: user.playerId,
          pondId,
          payload: compactPhaseTransitionPayload(fromPhase, phase, cause),
        });
      }
    }
  }
  const phaseElapsedMs = estimatePhaseElapsedMs(fromPhase, user.phaseEndsAt);
  const next = setPhase(user, phase, durationMs, context);
  markUserDirty(pondId, next.id);
  setUserWaiting(pondId, next.id, phase === 'waiting');
  recordPhaseTransition({
    ...buildUserFields(next, pondId, { socketId: extra?.socketId ?? undefined }),
    fromPhase,
    toPhase: phase,
    cause,
    phaseElapsedMs,
    phaseDeadlineTs: next.phaseEndsAt,
  });
  if (next.playerId && !next.isBot) {
    const hookEndsAt =
      phase === 'hooked' && next.phaseEndsAt != null
        ? next.phaseEndsAt
        : hookContextByUser.get(next.id)?.hookEndsAt ?? null;
    upsertPlayerPondSession(next, pondId, hookEndsAt);
  }
  return next;
}

function clearHook(userId: string): void {
  hookContextByUser.delete(userId);
}

function hasBaitForContinue(playerId: string): boolean {
  const gear = getPlayerGear(playerId);
  if (!gear) return false;
  const bait = getBait(gear.equippedBait);
  if (!bait) return true;
  if (!bait.consumed) return true;
  return (gear.baitInventory[gear.equippedBait] ?? 0) > 0;
}

function enterBaiting(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  user: PondUser,
  isRebait: boolean,
  socketId?: string | null,
): PondUser | null {
  if (!user.playerId) return null;

  const consume = consumeBaitAtBaitingStart(user.playerId);
  if (consume.depletedPreviousBaitId && socketId) {
    recordFishingMetric('bait_depleted', {
      playerId: user.playerId,
      pondId,
      payload: {
        userId: user.id,
        socketId,
        previousBaitId: consume.depletedPreviousBaitId,
        reason: 'auto_switch_after_depletion',
      },
    });
    io.to(socketId).emit('bait_depleted', { previousBaitId: consume.depletedPreviousBaitId });
    io.to(socketId).emit('gear_updated', consume.gear);
  } else if (socketId) {
    io.to(socketId).emit('gear_updated', consume.gear);
  }

  if (consume.insufficient) {
    recordFishingMetric('bait_depleted', {
      playerId: user.playerId,
      pondId,
      payload: {
        userId: user.id,
        socketId,
        equippedBaitId: consume.gear?.equippedBait,
        reason: 'bait_insufficient',
      },
    });
    flushFishingSessionToToday(user);
    const seated = transitionPhase(user, pondId, 'seated', 0, 'bait_insufficient', { isRebait }, { socketId });
    user.equippedBaitId = consume.gear?.equippedBait;
    emitPondUserUpdated(io, pondId, seated);
    return seated;
  }

  user.equippedBaitId = consume.gear.equippedBait;
  user.equippedTackleId = consume.gear.equippedTackle;
  const duration = Math.max(PHASE_MS.minBaiting, PHASE_MS.baiting);
  const next = transitionPhase(user, pondId, 'baiting', duration, isRebait ? 'auto_continue' : 'start_fishing', { isRebait }, { socketId });
  emitPondUserUpdated(io, pondId, next);
  return next;
}

export function beginFishingSequence(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  userId: string,
  socketId?: string,
): { ok: true; user: PondUser } | { ok: false; error: string } {
  const user = getUserById(pondId, userId);
  if (!user?.spotId) return { ok: false, error: '请先选择钓点' };
  if (user.fishingPhase !== 'seated') {
    return { ok: false, error: '当前状态无法开始钓鱼' };
  }
  if (user.playerId && getTodayFishingMs(user.playerId) >= MAX_DAILY_FISHING_MS) {
    return { ok: false, error: '今日钓鱼时长已用完' };
  }

  sessionFlagsByUser.delete(userId);
  clearHook(userId);
  const startedAt = now();
  user.sessionStartedAt = startedAt;
  user.fishingStartedAt = startedAt;
  initQuotaCheckpoint(userId, startedAt);
  resetBiteSessionCounters(userId, startedAt);
  const next = enterBaiting(io, pondId, user, false, socketId);
  if (!next) return { ok: false, error: '装饵失败' };
  recordFishingMetric('fishing_start', { playerId: user.playerId, pondId });
  return { ok: true, user: next };
}

export function handleStopFishing(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  userId: string,
): { ok: true; user: PondUser } | { ok: false; error: string } {
  const user = getUserById(pondId, userId);
  if (!user) return { ok: false, error: '用户不存在' };

  const phase = user.fishingPhase ?? 'idle';

  if (phase === 'hooked') {
    const ctx = hookContextByUser.get(userId);
    if (ctx) {
      ctx.escaped = true;
      applyEscapeGrowthBonus(ctx.fish.id);
      user.phaseContext = { outcome: 'escape' };
      const next = transitionPhase(user, pondId, 'resolving', PHASE_MS.resolvingEscape, 'stop_fishing_escape', { outcome: 'escape' });
      clearHook(userId);
      emitFishingFloatText(io, pondId, userId, 'escape', ctx.fish);
      emitPondUserUpdated(io, pondId, next);
      return { ok: true, user: next };
    }
  }

  if (phase === 'resolving') {
    sessionFlagsByUser.set(userId, { stopToSeated: true });
    return { ok: true, user };
  }

  if (phase === 'stopping') {
    // BUG-18/19: repeated stop — ensure finalized, do not restart timer.
    settleFishingSession(user, now(), 'stop_fishing_repeat', { mode: 'finalize' });
    return { ok: true, user };
  }

  if (!isFishingActive(phase) && phase !== 'seated') {
    return { ok: false, error: '当前未在钓鱼' };
  }

  // BUG-19：先 finalize 再进入 stopping 动画；syncStatus(stopping) 不得 revive 锚点
  clearHook(userId);
  settleFishingSession(user, now(), 'stop_fishing', { mode: 'finalize' });
  clearQuotaCheckpoint(userId);
  const next = transitionPhase(user, pondId, 'stopping', PHASE_MS.stopping, 'stop_fishing');
  emitPondUserUpdated(io, pondId, next);
  recordFishingMetric('fishing_stop', {
    playerId: user.playerId,
    pondId,
    payload: biteSessionMetricPayload(userId),
  });
  clearBiteSessionCounters(userId);
  return { ok: true, user: next };
}

export function handleDisconnect(
  pondId: string,
  userId: string,
  onTimeout: () => void,
): void {
  const user = getUserById(pondId, userId);
  if (!user) return;

  const correlationId = resolveCorrelationIdByPlayer(user.playerId);
  withTraceSpan('fishing.disconnect', correlationId, {
    pondId,
    userId,
    playerId: user.playerId,
    fishingPhase: user.fishingPhase ?? 'idle',
  }, () => {
  if (hasActiveTimer(userId, 'disconnect_grace')) {
    cancelByKind(userId, 'disconnect_grace');
  }

  if (user.fishingPhase === 'hooked') {
    const ctx = hookContextByUser.get(userId);
    if (ctx) {
      ctx.hookEndsAt = user.phaseEndsAt ?? undefined;
    }
  }

  const fromPhase = user.fishingPhase ?? 'idle';
  // BUG-16：在 syncStatus 清锚点之前先结算本局（finalize → fishingStartedAt=null）
  settleFishingSession(user, now(), 'disconnect', { mode: 'finalize' });
  user.disconnectedAt = now();
  transitionPhase(
    user,
    pondId,
    'disconnected',
    DISCONNECT_TIMEOUT_MS,
    'socket_disconnect',
    {
      ...user.phaseContext,
      disconnectedFromPhase: fromPhase,
    },
    { fromPhase },
  );
  updatePondUser(pondId, user);

  logStructuredEvent('socket_disconnect', 'socket_disconnect', {
    ...buildUserFields(user, pondId),
    reason: 'socket_disconnect',
  });
  recordFishingMetric('socket_disconnect', {
    playerId: user.playerId,
    pondId,
    payload: { userId, spotId: user.spotId, fishingPhase: user.fishingPhase, reason: 'socket_disconnect' },
  });

  if (user.playerId) {
    upsertPlayerPondSession(user, pondId, hookContextByUser.get(userId)?.hookEndsAt);
  }

  register({
    userId,
    kind: 'disconnect_grace',
    ms: DISCONNECT_TIMEOUT_MS,
    onFire: () => {
      const current = getUserById(pondId, userId);
      logStructuredEvent('socket_disconnect', 'disconnect_timeout', {
        playerId: current?.playerId ?? user.playerId,
        userId,
        pondId,
        spotId: current?.spotId ?? user.spotId,
        fishingPhase: current?.fishingPhase ?? user.fishingPhase ?? null,
        reason: 'disconnect_timeout',
      });
      recordFishingMetric('disconnect_timeout', {
        playerId: current?.playerId ?? user.playerId,
        pondId,
        payload: {
          userId,
          spotId: current?.spotId ?? user.spotId,
          fishingPhase: current?.fishingPhase ?? user.fishingPhase ?? null,
          reason: 'disconnect_timeout',
        },
      });
      if (current?.playerId) {
        deletePlayerPondSession(current.playerId, pondId);
      }
      onTimeout();
    },
  });
  });
}

export function cancelDisconnectTimer(userId: string): void {
  cancelByKind(userId, 'disconnect_grace');
}

export function hasPendingDisconnectTimer(userId: string): boolean {
  return hasActiveTimer(userId, 'disconnect_grace');
}

function restoreDefaultDisconnectedPhase(user: PondUser): FishingPhase {
  if (user.fishingPhase !== 'disconnected') return user.fishingPhase ?? 'idle';
  const disconnectedFromPhase = user.phaseContext?.disconnectedFromPhase;
  user.fishingPhase =
    disconnectedFromPhase && isFishingActive(disconnectedFromPhase)
      ? 'waiting'
      : user.spotId ? 'seated' : 'idle';
  user.phaseEndsAt = null;
  if (user.phaseContext) {
    delete user.phaseContext.disconnectedFromPhase;
  }
  syncStatus(user);
  return user.fishingPhase;
}

function logReconnect(
  user: PondUser,
  pondId?: string,
  extra?: Record<string, unknown>,
): void {
  logStructuredEvent('socket_reconnect', 'reconnect', {
    ...buildUserFields(user, pondId ?? '', {}),
    reason: 'socket_reconnect',
    ...extra,
  });
  recordFishingMetric('reconnect', {
    playerId: user.playerId,
    pondId,
    payload: { userId: user.id, spotId: user.spotId, fishingPhase: user.fishingPhase, reason: 'socket_reconnect', ...extra },
  });
}

export function restoreDisconnectedUser(user: PondUser, pondId?: string): PondUser {
  cancelDisconnectTimer(user.id);
  user.disconnectedAt = null;
  const fromPhase: FishingPhase = 'disconnected';
  const toPhase = restoreDefaultDisconnectedPhase(user);
  if (pondId) {
    recordPhaseTransition({
      ...buildUserFields(user, pondId),
      fromPhase,
      toPhase,
      cause: 'socket_reconnect',
      phaseDeadlineTs: user.phaseEndsAt,
    });
  }
  logReconnect(user, pondId);
  return user;
}

/** 重连后恢复 phase；hooked 时按 hookEndsAt 续接或立即进 resolving */
export function resumeAfterReconnect(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  user: PondUser,
  socketId: string | null,
): PondUser {
  cancelDisconnectTimer(user.id);
  user.disconnectedAt = null;

  let resumedHooked = false;
  let hookedExpired = false;
  let resumedToPhase: FishingPhase | null = null;

  if (user.fishingPhase === 'disconnected') {
    const ctx = hookContextByUser.get(user.id);
    const hookEndsAt = ctx?.hookEndsAt;
    const fromPhase: FishingPhase = 'disconnected';

    if (ctx && hookEndsAt != null) {
      resumedHooked = true;
      const next = transitionPhase(user, pondId, 'hooked', Math.max(0, hookEndsAt - now()), 'socket_reconnect', undefined, { socketId, fromPhase });
      next.phaseEndsAt = hookEndsAt;
      syncStatus(next);
      resumedToPhase = 'hooked';

      if (hookEndsAt <= now()) {
        hookedExpired = true;
        advanceFromHooked(io, pondId, user, socketId);
      } else {
        delete ctx.hookEndsAt;
        emitPondUserUpdated(io, pondId, user);
      }
    } else {
      const toPhase = restoreDefaultDisconnectedPhase(user);
      resumedToPhase = toPhase;
      recordPhaseTransition({
        ...buildUserFields(user, pondId, { socketId: socketId ?? undefined }),
        fromPhase,
        toPhase,
        cause: 'socket_reconnect',
        phaseElapsedMs: estimatePhaseElapsedMs(fromPhase, now() + DISCONNECT_TIMEOUT_MS),
        phaseDeadlineTs: user.phaseEndsAt,
      });
    }
  }

  logReconnect(user, pondId, { resumedHooked, hookedExpired, resumedToPhase });
  // BUG-16/19：断线期不计时；恢复活跃相位时展示锚点与 checkpoint 取 now
  if (isFishingActive(user.fishingPhase) && user.fishingPhase !== 'stopping') {
    const t = now();
    user.sessionStartedAt = t;
    user.fishingStartedAt = t;
    initQuotaCheckpoint(user.id, t);
    if (user.status !== 'fishing') user.status = 'fishing';
  } else if (user.fishingPhase !== 'stopping') {
    ensureFishingStartedAt(user);
    if (user.sessionStartedAt == null && user.fishingStartedAt != null) {
      user.sessionStartedAt = user.fishingStartedAt;
      initQuotaCheckpoint(user.id, user.fishingStartedAt);
    }
  }
  return user;
}

function advanceFromBaiting(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  user: PondUser,
): void {
  const isRebait = user.phaseContext?.isRebait === true;
  const skipCasting = isRebait && getConfigBool('C6_SKIP_CASTING_ON_REBATE', true);
  if (skipCasting) {
    const next = transitionPhase(user, pondId, 'waiting', 0, 'phase_timer_elapsed');
    emitPondUserUpdated(io, pondId, next);
    return;
  }
  const next = transitionPhase(user, pondId, 'casting', PHASE_MS.casting, 'phase_timer_elapsed');
  emitPondUserUpdated(io, pondId, next);
  emitFishingCastFloatText(io, pondId, user.id);
}

function advanceFromCasting(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  user: PondUser,
): void {
  const next = transitionPhase(user, pondId, 'waiting', 0, 'phase_timer_elapsed');
  emitPondUserUpdated(io, pondId, next);
}

function advanceFromHooked(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  user: PondUser,
  socketId: string | null,
): void {
  const ctx = hookContextByUser.get(user.id);
  if (!ctx) {
    const next = transitionPhase(user, pondId, 'waiting', 0, 'hook_timeout');
    emitPondUserUpdated(io, pondId, next);
    return;
  }

  const outcome = ctx.escaped ? 'escape' : 'catch';
  user.phaseContext = { outcome };
  const duration = outcome === 'catch' ? PHASE_MS.resolvingCatch : PHASE_MS.resolvingEscape;
  const next = transitionPhase(user, pondId, 'resolving', duration, ctx.escaped ? 'stop_fishing_escape' : 'hook_timeout', { outcome }, { socketId });
  emitPondUserUpdated(io, pondId, next);

  if (outcome === 'escape') {
    applyEscapeGrowthBonus(ctx.fish.id);
    emitFishingFloatText(io, pondId, user.id, 'escape', ctx.fish);
    if (user.playerId) {
      const counters = noteBiteEscape(user.id);
      recordFishingMetric('escape', {
        playerId: user.playerId,
        pondId,
        payload: {
          sessionHooks: counters.sessionHooks,
          sessionEscapes: counters.sessionEscapes,
          sessionMissTicks: counters.sessionMissTicks,
        },
      });
      applyTackleDurabilityOnEscape(user.playerId, io);
    }
    const miss: FishingMiss = { resultId: randomUUID(), reason: 'escaped' };
    if (socketId) io.to(socketId).emit('fish_miss', miss);
    else io.to(pondId).emit('fish_miss', miss);
  } else if (user.isBot) {
    notifyBotHookCatch(io, pondId, user, ctx.fish);
  } else if (socketId) {
    const isCodexNew =
      user.playerId !== undefined &&
      isCodexNewForPlayer(user.playerId, ctx.fish.speciesId);
    const catchData: PendingFishCatch = {
      catchId: randomUUID(),
      pondFishId: ctx.fish.id,
      speciesId: ctx.fish.speciesId,
      quality: ctx.fish.quality,
      sizeM: ctx.fish.sizeM,
      hookDurationMs: ctx.hookDurationMs,
      ...(isCodexNew ? { isCodexNew: true } : {}),
    };
    const locked = lockPendingCatch(user.id, catchData, {
      playerId: user.playerId,
      pondId,
    });
    if (locked) io.to(socketId).emit('fish_bite', locked);
  }

  hookContextByUser.delete(user.id);
}

function advanceFromResolving(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  user: PondUser,
  socketId: string | null,
): void {
  const flags = sessionFlagsByUser.get(user.id);
  if (flags?.leaveAfterResolve || flags?.stopToSeated) {
    sessionFlagsByUser.delete(user.id);
    flushFishingSessionToToday(user);
    const next = transitionPhase(
      user,
      pondId,
      flags.leaveAfterResolve ? 'idle' : 'seated',
      0,
      flags.leaveAfterResolve ? 'leave_pond' : 'stop_to_seated',
    );
    if (flags.leaveAfterResolve) user.spotId = null;
    emitPondUserUpdated(io, pondId, next);
    return;
  }

  if (!user.playerId || !hasBaitForContinue(user.playerId)) {
    flushFishingSessionToToday(user);
    const next = transitionPhase(user, pondId, 'seated', 0, 'bait_insufficient');
    emitPondUserUpdated(io, pondId, next);
    return;
  }

  enterBaiting(io, pondId, user, true, socketId);
}

function advanceFromStopping(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  user: PondUser,
): void {
  flushFishingSessionToToday(user);
  const next = transitionPhase(user, pondId, 'seated', 0, 'phase_timer_elapsed');
  emitPondUserUpdated(io, pondId, next);
}

export function scheduleHookFromBite(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  userId: string,
  socketId: string | null,
  hook: BiteHookEvent,
): void {
  const user = getUserById(pondId, userId);
  if (!user) return;

  hookContextByUser.set(userId, {
    fish: hook.fish,
    escaped: hook.escaped,
    hookDurationMs: hook.hookDurationMs,
  });

  emitFishingFloatText(io, pondId, userId, 'hook', hook.fish);
  const next = transitionPhase(user, pondId, 'hooked', hook.hookDurationMs, 'bite_hook', undefined, { socketId });
  emitPondUserUpdated(io, pondId, next);
  const { counters, waitingMsSinceLastHook } = noteBiteHook(userId);
  recordFishingMetric('bite_hook', {
    playerId: user.playerId,
    pondId,
    payload: {
      speciesId: hook.fish.speciesId,
      quality: hook.fish.quality,
      sessionHooks: counters.sessionHooks,
      sessionEscapes: counters.sessionEscapes,
      sessionMissTicks: counters.sessionMissTicks,
      missTicksSinceLastHook: counters.missTicksSinceLastHook,
      waitingMsSinceLastHook,
    },
  });
}

export function processWaitingBiteTick(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  userId: string,
  playerId: string,
  spotId: string,
  socketId: string | null,
): boolean {
  const user = getUserById(pondId, userId);
  if (!user || user.fishingPhase !== 'waiting') return false;
  if (getPendingCatch(userId)) return false;

  const gearState = prepareGearForBiteTick(playerId);
  const gear: FisherGearContext = {
    equippedBait: gearState.equippedBait,
    equippedTackle: gearState.equippedTackle,
  };

  const result = rollBiteHook(pondId, spotId, getLockedPondFishIds(), gear);
  if (result.outcome === 'miss') {
    noteBiteMiss(userId);
    if (isBiteTickPersistEnabled()) {
      recordFishingMetric('bite_tick_miss', {
        playerId,
        pondId,
        payload: {
          userId,
          spotId,
          reason: result.reason,
          sampledFishId: result.sampledFish?.id,
          sampledSpeciesId: result.sampledFish?.speciesId,
        },
      });
    }
    emitFishingMissFloatText(io, pondId, userId);
    return false;
  }

  if (isBiteTickPersistEnabled()) {
    recordFishingMetric('bite_tick_hit', {
      playerId,
      pondId,
      payload: {
        userId,
        spotId,
        speciesId: result.event.fish.speciesId,
        quality: result.event.fish.quality,
        sizeM: result.event.fish.sizeM,
        hookDurationMs: result.event.hookDurationMs,
      },
    });
  }
  const correlationId = resolveCorrelationIdByPlayer(playerId);
  withTraceSpan('bite_check.hit', correlationId, {
    pondId,
    userId,
    playerId,
    spotId,
    speciesId: result.event.fish.speciesId,
  }, () => {
    scheduleHookFromBite(io, pondId, userId, socketId, result.event);
  });
  return true;
}

export function tickFishingPhases(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  socketByUserId: { get(userId: string): string | undefined },
): void {
  const t = now();
  for (const pond of PONDS) {
    const pondId = pond.id;
    const users = listUsersInPond(pondId);
    for (const user of users) {
      if (user.fishingPhase === 'disconnected') continue;
      if (!user.phaseEndsAt || user.phaseEndsAt > t) continue;

      const socketId = socketByUserId.get(user.id) ?? null;

      switch (user.fishingPhase) {
        case 'baiting':
          advanceFromBaiting(io, pondId, user);
          break;
        case 'casting':
          advanceFromCasting(io, pondId, user);
          break;
        case 'hooked':
          advanceFromHooked(io, pondId, user, socketId);
          break;
        case 'resolving':
          advanceFromResolving(io, pondId, user, socketId);
          break;
        case 'stopping':
          advanceFromStopping(io, pondId, user);
          break;
        default:
          user.phaseEndsAt = null;
          break;
      }
      updatePondUser(pondId, user);
    }
  }
}

export function isUserWaitingForBite(user: PondUser): boolean {
  return user.fishingPhase === 'waiting' && !user.isBot;
}

export function getHookContext(userId: string): HookContext | undefined {
  return hookContextByUser.get(userId);
}

export function getPhaseEndsAt(pondId: string, userId: string): number | undefined {
  return getUserById(pondId, userId)?.phaseEndsAt ?? undefined;
}

export function initBotFishingPhase(user: PondUser): PondUser {
  user.fishingPhase = 'waiting';
  user.phaseEndsAt = null;
  user.phaseContext = { isRebait: false };
  // FISH-BOT-2：保留 startBotFishing 的 elapsed 回拨，勿覆盖为 now
  if (user.fishingStartedAt == null) {
    user.fishingStartedAt = now();
  }
  syncStatus(user);
  return user;
}
