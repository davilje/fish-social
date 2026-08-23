import type { Socket } from 'socket.io';
import type {
  ClientToServerEvents,
  LeavePondPayload,
  LeavePondReason,
  ServerToClientEvents,
} from '@fish-social/shared';
import { MAX_DAILY_FISHING_MS } from '@fish-social/shared';
import {
  addChatMessage,
  buildSnapshot,
  enrichPondUser,
  findDisconnectedUserByPlayerId,
  getSession,
  getPondUser,
  joinPond,
  leavePond,
  leaveSpot,
  postAnnouncement,
  reconnectSession,
  removeDisconnectedUser,
  restoreUserFromCheckpoint,
  startFishing,
  updatePondUser,
  getTodayFishingMs,
  settleFishingSession,
  todayKey,
} from './gameState.js';
import { acceptCatch, getInventory } from './inventory.js';
import { tryAutoReturnFish } from './returnFish.js';
import { ensurePlayer } from './players.js';
import { checkJoinPondAccess } from './playerProgress.js';
import { getGamePondDef, resolvePondFeePer2h } from '@fish-social/shared';
import { checkForbiddenPondBan } from './forbiddenPolice.js';
import { autoShareEpicCatch } from './posts.js';
import { emitEvictedBots } from './bots.js';
import { beginFishingSequence, beginGroundbaitSequence, handleStopFishing, resumeAfterReconnect } from './fishingStateMachine.js';
import { recordFishingMetric } from './fishingMetrics.js';
import { recordCodexCatch, isCodexNewForPlayer } from './codex.js';
import { addAlbumCandidate } from './album.js';
import { tryUnlockAchievements } from './achievements.js';
import { getQualityInfo, getSpecies, isAnnounceQuality, type ServerToClientEvents as ServerEvents } from '@fish-social/shared';
import { logStructuredEvent, recordStructuredMetric } from './fishingObservability.js';
import { bindPlayer, bindPondUser, resolveBySocket, unbindSocket } from './sessionRegistry.js';
import { cancelBySocket, cancelByUser } from './timerRegistry.js';
import { withTraceSpan } from './otelTracing.js';
import { buildJoinFeeHint, ensurePlayerProgress } from './playerProgress.js';
import {
  biteSessionMetricPayload,
  clearBiteSessionCounters,
} from './biteSessionCounters.js';
import { shouldRejectHumanJoinPond } from './humanCapacity.js';
import { allowSocketEvent } from './socketEventRateLimit.js';
import { ensurePondEcologyCurrent } from './pondEcology.js';

interface PondHandlerDeps {
  io: import('socket.io').Server<ClientToServerEvents, ServerToClientEvents>;
  resolveSocketPlayerId: (socket: Socket<ClientToServerEvents, ServerToClientEvents>, payloadPlayerId?: string) => string | null;
  roomFanoutCount: (pondId: string) => number;
}

function parseLeavePondPayload(
  raw: string | LeavePondPayload,
): { pondId: string; reason: LeavePondReason } {
  if (typeof raw === 'string') {
    return { pondId: raw, reason: 'legacy_unknown' };
  }
  return { pondId: raw.pondId, reason: raw.reason ?? 'legacy_unknown' };
}

function buildJoinSuccessAck(
  playerId: string,
  pondId: string,
  userId: string,
  returnFeeMode?: 'sell_only' | 'auto_return',
) {
  const baseMs = getTodayFishingMs(playerId);
  const fee = buildJoinFeeHint(playerId, pondId);
  const progress = ensurePlayerProgress(playerId);
  return {
    ok: true as const,
    userId,
    todayFishingBaseMs: baseMs,
    todayRemainingMs: Math.max(0, MAX_DAILY_FISHING_MS - baseMs),
    quotaDateKey: todayKey(),
    returnFeeMode: returnFeeMode ?? 'sell_only',
    ...fee,
    onboardingCompleted: progress.onboardingCompleted,
    playerLevel: progress.level,
  };
}

export function registerSocketPondHandlers(
  socket: Socket<ClientToServerEvents, ServerEvents>,
  deps: PondHandlerDeps,
): void {
  const { io, resolveSocketPlayerId, roomFanoutCount } = deps;

  const rejectIfRateLimited = (
    event: string,
    ack?: (result: { ok: boolean; error?: string }) => void,
  ): boolean => {
    if (allowSocketEvent(socket.id, event)) return false;
    logStructuredEvent('security', 'socket_event_rate_limited', {
      eventType: 'socket_event_rate_limited',
      socketId: socket.id,
      event,
    });
    ack?.({ ok: false, error: 'rate_limited' });
    return true;
  };

  socket.on('join_pond', (payload, ack) => {
    if (rejectIfRateLimited('join_pond', ack)) return;
    const correlationId = socket.data.correlationId as string | undefined;
    withTraceSpan('join_pond', correlationId, {
      socketId: socket.id,
      pondId: payload.pondId,
      playerId: payload.playerId,
    }, () => {
    const authPlayerId = resolveSocketPlayerId(socket, payload.playerId);
    if (!authPlayerId) {
      ack?.({ ok: false, error: 'unauthorized' });
      return;
    }

    const capacity = shouldRejectHumanJoinPond(authPlayerId, payload.pondId);
    if (capacity.reject) {
      const errorCode = capacity.reason ?? 'human_socket_limit';
      logStructuredEvent('capacity', 'capacity_reject', {
        playerId: authPlayerId,
        socketId: socket.id,
        pondId: payload.pondId,
        current: capacity.current,
        limit: capacity.limit,
        reason: errorCode,
      });
      recordStructuredMetric('join_pond_fail', {
        playerId: authPlayerId,
        socketId: socket.id,
        pondId: payload.pondId,
        reason: errorCode,
      });
      ack?.({ ok: false, error: errorCode });
      return;
    }

    logStructuredEvent('join_pond', 'join_pond_attempt', {
      playerId: authPlayerId,
      socketId: socket.id,
      pondId: payload.pondId,
      reason: 'join_pond_attempt',
    });
    recordStructuredMetric('join_pond_attempt', {
      playerId: authPlayerId,
      socketId: socket.id,
      pondId: payload.pondId,
      reason: 'join_pond_attempt',
    });
    ensurePlayer(authPlayerId, payload.nickname);
    const access = checkJoinPondAccess(authPlayerId, payload.pondId);
    if (!access.ok) {
      logStructuredEvent('join_pond', 'join_pond_fail', {
        playerId: authPlayerId,
        socketId: socket.id,
        pondId: payload.pondId,
        reason: 'pond_access',
        ackError: access.error,
      });
      recordStructuredMetric('join_pond_fail', {
        playerId: authPlayerId,
        socketId: socket.id,
        pondId: payload.pondId,
        reason: 'pond_access',
        ackError: access.error,
      });
      ack?.({ ok: false, error: access.error ?? '无法进入该鱼塘' });
      return;
    }
    const ban = checkForbiddenPondBan(authPlayerId, payload.pondId);
    if (!ban.ok) {
      logStructuredEvent('join_pond', 'join_pond_fail', {
        playerId: authPlayerId,
        socketId: socket.id,
        pondId: payload.pondId,
        reason: 'police_ban',
        ackError: ban.error,
      });
      recordStructuredMetric('join_pond_fail', {
        playerId: authPlayerId,
        socketId: socket.id,
        pondId: payload.pondId,
        reason: 'police_ban',
        ackError: ban.error,
      });
      ack?.({ ok: false, error: ban.error });
      return;
    }
    try {
      ensurePondEcologyCurrent(payload.pondId);
    } catch {
      logStructuredEvent('pond_ecology', 'pond_ecology_catchup_failed', {
        eventType: 'pond_ecology_catchup_failed',
        pondId: payload.pondId,
        playerId: authPlayerId,
      });
      ack?.({ ok: false, error: '鱼塘生态同步失败，请稍后重试' });
      return;
    }
    bindPlayer(authPlayerId, socket.id, socket.data.correlationId as string | undefined);

    const disconnected = findDisconnectedUserByPlayerId(payload.pondId, authPlayerId);
    if (disconnected) {
      const user = reconnectSession(
        socket.id,
        payload.pondId,
        disconnected.id,
        authPlayerId,
        payload.nickname,
      );
      if (user) {
        resumeAfterReconnect(io, payload.pondId, user, socket.id);
        updatePondUser(payload.pondId, user);
        socket.join(payload.pondId);
        bindPondUser(user.id, socket.id, payload.pondId);
        logStructuredEvent('session_rebound', 'session_rebound', {
          playerId: authPlayerId,
          userId: user.id,
          socketId: socket.id,
          pondId: payload.pondId,
          reason: 'join_pond_reconnect',
        });
        const snapshot = buildSnapshot(payload.pondId, authPlayerId);
        if (snapshot) {
          socket.emit('pond_snapshot', snapshot);
          socket.emit('inventory_updated', getInventory(authPlayerId));
        }
        io.to(payload.pondId).emit('pond_user_updated', enrichPondUser(user));
        logStructuredEvent('socket_broadcast_fanout', 'socket_broadcast_fanout', {
          eventType: 'socket_broadcast_fanout',
          channel: 'pond_user_updated',
          pondId: payload.pondId,
          fanoutCount: roomFanoutCount(payload.pondId),
        });
        logStructuredEvent('join_pond', 'join_pond_success', {
          playerId: authPlayerId,
          userId: user.id,
          socketId: socket.id,
          pondId: payload.pondId,
          spotId: user.spotId,
          fishingPhase: user.fishingPhase,
          reason: 'join_pond_success',
          joinKind: 'reconnect',
          disconnectDurationMs:
            disconnected.disconnectedAt != null ? Date.now() - disconnected.disconnectedAt : undefined,
        });
        recordStructuredMetric('join_pond_success', {
          playerId: authPlayerId,
          userId: user.id,
          socketId: socket.id,
          pondId: payload.pondId,
          spotId: user.spotId,
          fishingPhase: user.fishingPhase,
          reason: 'join_pond_success',
          joinKind: 'reconnect',
          disconnectDurationMs:
            disconnected.disconnectedAt != null ? Date.now() - disconnected.disconnectedAt : undefined,
        });
        ack?.(buildJoinSuccessAck(authPlayerId, payload.pondId, user.id));
        return;
      }
    }

    const checkpointUser = restoreUserFromCheckpoint(
      socket.id,
      payload.pondId,
      authPlayerId,
      payload.nickname,
    );
    if (checkpointUser) {
      resumeAfterReconnect(io, payload.pondId, checkpointUser, socket.id);
      updatePondUser(payload.pondId, checkpointUser);
      socket.join(payload.pondId);
      bindPondUser(checkpointUser.id, socket.id, payload.pondId);
      const snapshot = buildSnapshot(payload.pondId, authPlayerId);
      if (snapshot) {
        socket.emit('pond_snapshot', snapshot);
        socket.emit('inventory_updated', getInventory(authPlayerId));
      }
      io.to(payload.pondId).emit('pond_user_updated', enrichPondUser(checkpointUser));
      logStructuredEvent('join_pond', 'join_pond_success', {
        playerId: authPlayerId,
        userId: checkpointUser.id,
        socketId: socket.id,
        pondId: payload.pondId,
        spotId: checkpointUser.spotId,
        fishingPhase: checkpointUser.fishingPhase,
        reason: 'join_pond_success',
        joinKind: 'checkpoint_restore',
      });
      recordStructuredMetric('join_pond_success', {
        playerId: authPlayerId,
        userId: checkpointUser.id,
        socketId: socket.id,
        pondId: payload.pondId,
        spotId: checkpointUser.spotId,
        fishingPhase: checkpointUser.fishingPhase,
        reason: 'join_pond_success',
        joinKind: 'checkpoint_restore',
      });
      ack?.(buildJoinSuccessAck(authPlayerId, payload.pondId, checkpointUser.id));
      return;
    }

    const result = joinPond(
      socket.id,
      payload.pondId,
      payload.nickname,
      authPlayerId,
      payload.returnFeeMode,
    );
    if (!result.ok) {
      logStructuredEvent('join_pond', 'join_pond_fail', {
        playerId: authPlayerId,
        socketId: socket.id,
        pondId: payload.pondId,
        reason: 'join_pond_fail',
        ackError: result.error,
      });
      recordStructuredMetric('join_pond_fail', {
        playerId: authPlayerId,
        socketId: socket.id,
        pondId: payload.pondId,
        reason: 'join_pond_fail',
        ackError: result.error,
      });
      ack?.({ ok: false, error: result.error });
      socket.emit('error', result.error);
      return;
    }

    emitEvictedBots(io, payload.pondId, result.evictedUserIds);
    socket.join(payload.pondId);
    bindPondUser(result.user.id, socket.id, payload.pondId);
    const snapshot = buildSnapshot(payload.pondId, authPlayerId);
    if (snapshot) {
      socket.emit('pond_snapshot', snapshot);
      socket.emit('inventory_updated', getInventory(authPlayerId));
    }
    const enrichedJoin = enrichPondUser(result.user);
    socket.to(payload.pondId).emit('pond_user_joined', enrichedJoin);
    // 进塘本人也推一次额度，避免未选钓点时底栏仍显示满额 8h
    socket.emit('pond_user_updated', enrichedJoin);
    logStructuredEvent('socket_broadcast_fanout', 'socket_broadcast_fanout', {
      eventType: 'socket_broadcast_fanout',
      channel: 'pond_user_joined',
      pondId: payload.pondId,
      fanoutCount: roomFanoutCount(payload.pondId),
    });
    logStructuredEvent('join_pond', 'join_pond_success', {
      playerId: authPlayerId,
      userId: result.user.id,
      socketId: socket.id,
      pondId: payload.pondId,
      spotId: result.user.spotId,
      fishingPhase: result.user.fishingPhase,
      reason: 'join_pond_success',
      joinKind: 'fresh',
    });
    recordStructuredMetric('join_pond_success', {
      playerId: authPlayerId,
      userId: result.user.id,
      socketId: socket.id,
      pondId: payload.pondId,
      spotId: result.user.spotId,
      fishingPhase: result.user.fishingPhase,
      reason: 'join_pond_success',
      joinKind: 'fresh',
    });
    const pondDef = getGamePondDef(payload.pondId);
    recordFishingMetric('return_fee_mode_selected', {
      playerId: authPlayerId,
      pondId: payload.pondId,
      payload: {
        mode: result.returnFeeMode,
        feePer2h: pondDef ? resolvePondFeePer2h(pondDef, result.returnFeeMode) : 0,
      },
    });
    ack?.(buildJoinSuccessAck(authPlayerId, payload.pondId, result.user.id, result.returnFeeMode));
    });
  });

  socket.on('leave_pond', (raw, ack) => {
    const { pondId, reason } = parseLeavePondPayload(raw);
    const session = getSession(socket.id);
    const user = leavePond(socket.id);
    socket.leave(pondId);
    if (user) {
      console.log(
        `[leave_pond] ${JSON.stringify({
          playerId: session?.playerId ?? user.playerId,
          userId: user.id,
          pondId,
          spotId: user.spotId,
          fishingPhase: user.fishingPhase,
          reason,
          ts: Date.now(),
        })}`,
      );
      recordFishingMetric('leave_pond', {
        playerId: session?.playerId ?? user.playerId,
        pondId,
        payload: {
          reason,
          spotId: user.spotId,
          fishingPhase: user.fishingPhase,
          ...biteSessionMetricPayload(user.id),
        },
      });
      clearBiteSessionCounters(user.id);
      cancelByUser(user.id);
      unbindSocket(socket.id);
      io.to(pondId).emit('pond_user_left', user.id);
      logStructuredEvent('socket_broadcast_fanout', 'socket_broadcast_fanout', {
        eventType: 'socket_broadcast_fanout',
        channel: 'pond_user_left',
        pondId,
        fanoutCount: roomFanoutCount(pondId),
      });
    }
    if (session) cancelByUser(session.userId);
    ack?.({ ok: true });
  });

  socket.on('leave_spot', (payload, ack) => {
    if (rejectIfRateLimited('leave_spot', ack)) return;
    const result = leaveSpot(socket.id, payload.pondId);
    if (!result.ok) {
      ack?.({ ok: false, error: result.error });
      return;
    }
    io.to(payload.pondId).emit('pond_user_updated', enrichPondUser(result.user));
    ack?.({ ok: true });
  });

  socket.on('start_fishing', (payload, ack) => {
    if (rejectIfRateLimited('start_fishing', ack)) return;
    const current = getPondUser(socket.id, payload.pondId);
    if (!current) {
      ack?.({ ok: false, error: '请先加入鱼塘' });
      return;
    }
    if (!current.spotId) {
      ack?.({ ok: false, error: '请先选择钓点' });
      return;
    }
    if (payload.spotId && payload.spotId !== current.spotId) {
      ack?.({ ok: false, error: '当前钓点已变化，请重新选择' });
      return;
    }
    if (current.playerId) {
      const ban = checkForbiddenPondBan(current.playerId, payload.pondId);
      if (!ban.ok) {
        ack?.({ ok: false, error: ban.error });
        return;
      }
    }
    const seq = beginFishingSequence(io, payload.pondId, current.id, socket.id);
    if (!seq.ok) {
      ack?.({ ok: false, error: seq.error });
      return;
    }
    ack?.({ ok: true });
  });

  socket.on('groundbait_start', (payload, ack) => {
    if (rejectIfRateLimited('groundbait_start', ack)) return;
    const current = getPondUser(socket.id, payload.pondId);
    if (!current) {
      ack?.({ ok: false, error: '请先加入鱼塘', code: 'NOT_SEATED' });
      return;
    }
    if (!payload.groundbaitId) {
      ack?.({ ok: false, error: '请选择窝料', code: 'LOCKED' });
      return;
    }
    const seq = beginGroundbaitSequence(
      io,
      payload.pondId,
      current.id,
      payload.groundbaitId,
      socket.id,
    );
    if (!seq.ok) {
      ack?.({ ok: false, error: seq.error, code: seq.code });
      return;
    }
    ack?.({ ok: true });
  });

  socket.on('take_spot', (payload, ack) => {
    if (rejectIfRateLimited('take_spot', ack)) return;
    const result = startFishing(socket.id, payload.pondId, payload.spotId);
    if (!result.ok) {
      ack?.({ ok: false, error: result.error });
      socket.emit('error', result.error);
      return;
    }
    emitEvictedBots(io, payload.pondId, result.evictedUserIds);
    io.to(payload.pondId).emit('pond_user_updated', enrichPondUser(result.user));
    ack?.({ ok: true });
  });

  socket.on('stop_fishing', (pondId, ack) => {
    if (rejectIfRateLimited('stop_fishing', ack)) return;
    const session = getSession(socket.id);
    if (!session) {
      ack?.({ ok: false, error: '请先加入鱼塘' });
      return;
    }
    // BUG-19：handleStopFishing 内已先 finalize 再 stopping；此处幂等补结算并对齐 ack
    const result = handleStopFishing(io, pondId, session.userId);
    if (!result.ok) {
      ack?.({ ok: false, error: result.error });
      return;
    }
    const finalUser = result.user;
    settleFishingSession(finalUser, Date.now(), 'stop_fishing_ack', { mode: 'finalize' });
    const todayFishingBaseMs = getTodayFishingMs(session.playerId);
    const quotaDateKey = todayKey();
    const todayRemainingMs = Math.max(0, MAX_DAILY_FISHING_MS - todayFishingBaseMs);
    const enriched = enrichPondUser(finalUser);
    io.to(pondId).emit('pond_user_updated', enriched);
    ack?.({
      ok: true,
      todayFishingMs: todayFishingBaseMs,
      todayFishingBaseMs,
      todayRemainingMs,
      quotaDateKey,
    });
  });

  socket.on('send_chat', (payload, ack) => {
    if (rejectIfRateLimited('send_chat', ack)) return;
    const result = addChatMessage(socket.id, payload.pondId, payload.text);
    if (!result.ok) {
      ack?.({ ok: false, error: result.error });
      return;
    }
    io.to(payload.pondId).emit('chat_message', result.message);
    ack?.({ ok: true });
  });

  socket.on('accept_catch', (catchId, ack) => {
    if (rejectIfRateLimited('accept_catch', ack)) return;
    const session = getSession(socket.id);
    if (!session) {
      ack?.({ ok: false, error: '未加入鱼塘' });
      return;
    }
    const result = acceptCatch(session.userId, session.playerId, catchId, session.pondId);
    if (!result.ok) {
      ack?.({ ok: false, error: result.error });
      return;
    }
    recordFishingMetric('pending_catch_accept', {
      playerId: session.playerId,
      pondId: session.pondId,
      payload: {
        speciesId: result.item.speciesId,
        quality: result.item.quality,
        sizeM: result.item.sizeM,
        eventId: catchId,
      },
    });
    const wasNewCodex = isCodexNewForPlayer(session.playerId, result.item.speciesId);
    const codexUnlock = recordCodexCatch(session.playerId, result.item.speciesId, result.item.sizeM);
    if (codexUnlock) socket.emit('codex_unlocked', codexUnlock);

    addAlbumCandidate({
      playerId: session.playerId,
      speciesId: result.item.speciesId,
      quality: result.item.quality,
      sizeM: result.item.sizeM,
      pondId: session.pondId,
      source: wasNewCodex ? 'first_codex' : 'catch',
      inventoryItemId: result.item.id,
    });

    const newly = tryUnlockAchievements(session.playerId);
    for (const ach of newly) {
      socket.emit('achievement_unlocked', {
        achievementId: ach.achievementId,
        name: ach.name,
        desc: ach.desc,
      });
    }

    socket.emit('inventory_updated', getInventory(session.playerId));

    const autoResult = tryAutoReturnFish(session.playerId, result.item.id);
    if (autoResult.ok) {
      socket.emit('inventory_updated', autoResult.items);
      ack?.({
        ok: true,
        autoReturned: true,
        gold: autoResult.gold,
        playerXp: autoResult.playerXp,
        pondXp: autoResult.pondXp,
        newSizeM: autoResult.newSizeM,
        sizeGainM: autoResult.sizeGainM,
        totalCoins: autoResult.totalCoins,
      });
    } else {
      ack?.({ ok: true, item: result.item });
    }

    if (isAnnounceQuality(result.item.quality)) {
      const species = getSpecies(result.item.speciesId);
      const quality = getQualityInfo(result.item.quality);
      const text = `${session.nickname}钓到了【${quality.name}】的${species.name}！`;
      const msg = postAnnouncement(session.pondId, text);
      io.to(session.pondId).emit('chat_message', msg);
      autoShareEpicCatch(session.playerId, session.nickname, result.item);
    }
  });
}
