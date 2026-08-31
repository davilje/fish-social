import { randomUUID } from 'crypto';
import {
  getGamePondDef,
  getPondById,
  isFishingActive,
  validateJoinReturnFeeMode,
  type ChatMessage,
  type PondUser,
  type ReturnFeeMode,
} from '@fish-social/shared';
import { clearPendingCatch } from './inventory.js';
import { clearGroundbait } from './groundbait.js';
import { recordFishingMetric } from './fishingMetrics.js';
import { recordPhaseTransition } from './fishingObservability.js';
import {
  deletePlayerPondSession,
  upsertPlayerPondSession,
} from './playerPondSession.js';
import { cancelBySocket, cancelByUser } from './timerRegistry.js';
import { appendChatMessage } from './pondChat.js';
import {
  MAX_POND_USERS,
  createHumanPondUser,
  ensureFishingDayRollover,
  ensurePondUsers,
  evictBotsForHuman,
  detachPondUser,
  getTodayFishingMs,
  removeBotUser,
  removeUserIndexes,
  restoreCheckpointUser,
  settleFishingSession,
  todayKey,
} from './pondUserManager.js';
import { checkJoinPondAccess } from './playerProgress.js';
import { checkForbiddenPondBan, notePoliceLeaveIfNeeded } from './forbiddenPolice.js';

export interface SessionMeta {
  userId: string;
  playerId: string;
  pondId: string;
  nickname: string;
}

const sessions = new Map<string, SessionMeta>();

export function getSession(socketId: string): SessionMeta | undefined {
  return sessions.get(socketId);
}

export function clearSession(socketId: string): void {
  cancelBySocket(socketId);
  sessions.delete(socketId);
}

export function getSessionByUserId(userId: string): SessionMeta | undefined {
  for (const session of sessions.values()) {
    if (session.userId === userId) return session;
  }
  return undefined;
}

export function getPondUser(socketId: string, pondId: string): PondUser | null {
  const session = sessions.get(socketId);
  if (!session || session.pondId !== pondId) return null;
  return ensurePondUsers(pondId).get(session.userId) ?? null;
}

export function clearSessionsForUser(userId: string): void {
  for (const [socketId, session] of sessions.entries()) {
    if (session.userId === userId) sessions.delete(socketId);
  }
}

export function joinPond(
  socketId: string,
  pondId: string,
  nickname: string,
  playerId: string,
  returnFeeMode?: ReturnFeeMode,
): { ok: true; user: PondUser; evictedUserIds: string[]; returnFeeMode: ReturnFeeMode } | { ok: false; error: string } {
  const pond = getPondById(pondId);
  if (!pond) return { ok: false, error: '鱼塘不存在' };

  const pondDef = getGamePondDef(pondId);
  const modeResult = validateJoinReturnFeeMode(pondDef, returnFeeMode);
  if (!modeResult.ok) {
    recordFishingMetric('join_pond_fail', {
      playerId,
      pondId,
      payload: { socketId, reason: 'return_fee_mode', ackError: modeResult.error },
    });
    return { ok: false, error: modeResult.error };
  }
  const resolvedMode = modeResult.mode;

  const access = checkJoinPondAccess(playerId, pondId);
  if (!access.ok) {
    recordFishingMetric('join_pond_fail', {
      playerId,
      pondId,
      payload: { socketId, reason: 'pond_access', ackError: access.error },
    });
    return { ok: false, error: access.error };
  }

  const ban = checkForbiddenPondBan(playerId, pondId);
  if (!ban.ok) {
    recordFishingMetric('join_pond_fail', {
      playerId,
      pondId,
      payload: { socketId, reason: 'police_ban', ackError: ban.error },
    });
    return { ok: false, error: ban.error };
  }

  const users = ensurePondUsers(pondId);
  const evictedUserIds = evictBotsForHuman(pondId);
  if (users.size >= MAX_POND_USERS) {
    recordFishingMetric('pond_full_reject', {
      playerId,
      pondId,
      payload: { socketId, reason: 'max_pond_users', maxPondUsers: MAX_POND_USERS, eventId: `pond_full:${playerId}:${Date.now()}` },
    });
    recordFishingMetric('join_pond_fail', {
      playerId,
      pondId,
      payload: { socketId, reason: 'pond_full', ackError: `鱼塘已满（最多 ${MAX_POND_USERS} 人）`, eventId: `join_fail:${playerId}:${Date.now()}` },
    });
    return { ok: false, error: `鱼塘已满（最多 ${MAX_POND_USERS} 人）` };
  }

  if (sessions.has(socketId)) {
    leavePond(socketId);
  }

  const user = createHumanPondUser(pondId, playerId, nickname, resolvedMode);
  sessions.set(socketId, { userId: user.id, playerId, pondId, nickname: user.nickname });
  upsertPlayerPondSession(user, pondId);

  return { ok: true, user, evictedUserIds, returnFeeMode: resolvedMode };
}

export function leavePond(socketId: string): PondUser | null {
  const session = sessions.get(socketId);
  if (!session) return null;

  notePoliceLeaveIfNeeded(session.playerId, session.pondId);

  const users = ensurePondUsers(session.pondId);
  const user = users.get(session.userId);
  if (user) {
    settleFishingSession(user, Date.now(), 'leave_pond', {
      mode: 'finalize',
      pondId: session.pondId,
    });
    clearGroundbait(user);
  }
  if (user?.spotId) {
    recordFishingMetric('spot_release', {
      playerId: session.playerId,
      pondId: session.pondId,
      payload: {
        userId: session.userId,
        spotId: user.spotId,
        fishingPhase: user.fishingPhase,
        reason: 'leave_pond',
        eventId: `spot_release:${session.userId}:leave_pond`,
      },
    });
  }

  users.delete(session.userId);
  removeUserIndexes(session.pondId, session.userId);
  cancelByUser(session.userId);
  cancelBySocket(socketId);
  clearPendingCatch(session.userId, { playerId: session.playerId, pondId: session.pondId });
  deletePlayerPondSession(session.playerId, session.pondId);
  sessions.delete(socketId);
  return user ?? null;
}

export function leaveSpot(
  socketId: string,
  pondId: string,
): { ok: true; user: PondUser } | { ok: false; error: string } {
  const session = sessions.get(socketId);
  if (!session || session.pondId !== pondId) {
    return { ok: false, error: '请先加入鱼塘' };
  }

  const user = ensurePondUsers(pondId).get(session.userId);
  if (!user) return { ok: false, error: '用户不存在' };
  if (!user.spotId) return { ok: true, user };

  const phase = user.fishingPhase ?? 'idle';
  if ((isFishingActive(phase) &&
       phase !== 'stopping' &&
       phase !== 'resolving') ||
      user.status === 'fishing') {
    return { ok: false, error: '请先收杆再离席' };
  }
  if (phase !== 'seated' &&
      phase !== 'stopping' &&
      phase !== 'resolving' &&
      phase !== 'groundbaiting') {
    return { ok: false, error: '当前不在可离席状态' };
  }

  const spotId = user.spotId;
  cancelByUser(session.userId);
  if (phase === 'stopping' || phase === 'resolving')
    settleFishingSession(user, Date.now(), 'leave_spot', {
      mode: 'finalize',
      pondId,
    });
  user.spotId = null;
  user.status = 'idle';
  user.fishingPhase = 'idle';
  user.fishingStartedAt = null;
  user.sessionStartedAt = null;
  user.phaseEndsAt = null;
  user.phaseContext = { isRebait: false };
  clearGroundbait(user);
  upsertPlayerPondSession(user, pondId);
  recordFishingMetric('spot_release', {
    playerId: session.playerId,
    pondId,
    payload: {
      userId: user.id,
      spotId,
      fishingPhase: user.fishingPhase,
      reason: 'leave_spot',
      eventId: `spot_release:${user.id}:${spotId}:leave_spot`,
    },
  });
  return { ok: true, user };
}

export function startFishing(
  socketId: string,
  pondId: string,
  spotId: string,
): { ok: true; user: PondUser; evictedUserIds: string[] } | { ok: false; error: string } {
  const session = sessions.get(socketId);
  if (!session || session.pondId !== pondId) {
    return { ok: false, error: '请先加入鱼塘' };
  }

  const pond = getPondById(pondId);
  if (!pond) return { ok: false, error: '鱼塘不存在' };

  const spot = pond.spots.find((s) => s.id === spotId);
  if (!spot) return { ok: false, error: '钓点不存在' };

  const users = ensurePondUsers(pondId);
  const user = users.get(session.userId);
  if (!user) return { ok: false, error: '用户不存在' };
  if (isFishingActive(user.fishingPhase) || user.status === 'fishing') {
    return { ok: false, error: '请先收起鱼竿' };
  }

  const evictedUserIds: string[] = evictBotsForHuman(pondId);

  const occupant = [...users.values()].find((u) => u.id !== user.id && u.spotId === spotId);
  if (occupant) {
    if (occupant.isBot) {
      const removed = removeBotUser(pondId, occupant.id);
      if (removed) evictedUserIds.push(removed.id);
      if (removed) {
        recordFishingMetric('bot_evicted_for_human', {
          playerId: removed.playerId,
          pondId,
          payload: { userId: removed.id, spotId, reason: 'spot_take_bot_eviction', eventId: `bot_evict:${removed.id}:spot` },
        });
      }
    } else {
      recordFishingMetric('spot_take_fail', {
        playerId: session.playerId,
        pondId,
        payload: { userId: user.id, spotId, reason: 'occupied_by_human', occupantUserId: occupant.id, eventId: `spot_fail:${user.id}:${spotId}` },
      });
      return { ok: false, error: '该钓点已被占用' };
    }
  }

  ensureFishingDayRollover(user);
  const now = Date.now();
  // 今日时长已满仍可落座（seated）；仅开始钓鱼时由 beginFishingSequence 拦截
  // （对齐 Web：可坐席、不可开钓）

  // 换钓点前先结算上一局未入账段
  settleFishingSession(user, now, 'take_spot', { mode: 'finalize' });

  const fromPhase = user.fishingPhase ?? 'idle';
  const prevSpot = user.spotId;
  user.spotId = spotId;
  user.status = 'idle';
  user.fishingStartedAt = null;
  user.sessionStartedAt = null;
  user.todayFishingMs = getTodayFishingMs(session.playerId);
  user.todayFishingBaseMs = user.todayFishingMs;
  user.fishingDayKey = todayKey();
  user.fishingPhase = 'seated';
  user.phaseEndsAt = null;
  user.phaseContext = { isRebait: false };
  if (prevSpot !== spotId) clearGroundbait(user);

  recordPhaseTransition({
    playerId: user.playerId,
    userId: user.id,
    socketId,
    pondId,
    spotId: user.spotId,
    fishingPhase: user.fishingPhase,
    isBot: user.isBot,
    fromPhase,
    toPhase: 'seated',
    cause: 'take_spot',
    phaseDeadlineTs: user.phaseEndsAt,
  });
  recordFishingMetric('spot_take_success', {
    playerId: user.playerId,
    pondId,
    payload: { userId: user.id, socketId, spotId: user.spotId, fishingPhase: user.fishingPhase, eventId: `spot_take:${user.id}:${spotId}` },
  });
  upsertPlayerPondSession(user, pondId);

  return { ok: true, user, evictedUserIds };
}

export function stopFishing(socketId: string, pondId: string): { ok: true; user: PondUser } | { ok: false; error: string } {
  const session = sessions.get(socketId);
  if (!session || session.pondId !== pondId) {
    return { ok: false, error: '请先加入鱼塘' };
  }

  const users = ensurePondUsers(pondId);
  const user = users.get(session.userId);
  if (!user) return { ok: false, error: '用户不存在' };

  if (user.status === 'fishing' && user.fishingStartedAt !== null) {
    settleFishingSession(user, Date.now(), 'stop_fishing', {
      mode: 'finalize',
      pondId,
    });
  }

  user.status = 'idle';
  user.fishingStartedAt = null;
  user.sessionStartedAt = null;

  return { ok: true, user };
}

export function addChatMessage(
  socketId: string,
  pondId: string,
  text: string,
): { ok: true; message: ChatMessage } | { ok: false; error: string } {
  const session = sessions.get(socketId);
  if (!session || session.pondId !== pondId) {
    return { ok: false, error: '请先加入鱼塘' };
  }

  const trimmed = text.trim();
  if (!trimmed) return { ok: false, error: '消息不能为空' };
  if (trimmed.length > 200) return { ok: false, error: '消息过长' };

  const message = appendChatMessage(pondId, {
    id: randomUUID(),
    pondId,
    userId: session.userId,
    nickname: session.nickname,
    text: trimmed,
    createdAt: Date.now(),
  });

  return { ok: true, message };
}

export function restoreUserFromCheckpoint(
  socketId: string,
  pondId: string,
  playerId: string,
  nickname: string,
): PondUser | null {
  const user = restoreCheckpointUser(pondId, playerId, nickname);
  if (!user) return null;
  sessions.set(socketId, { userId: user.id, playerId, pondId, nickname: user.nickname });
  return user;
}

export function reconnectSession(
  socketId: string,
  pondId: string,
  userId: string,
  playerId: string,
  nickname: string,
): PondUser | null {
  const users = ensurePondUsers(pondId);
  const user = users.get(userId);
  if (!user) return null;

  sessions.set(socketId, { userId, playerId, pondId, nickname });
  return user;
}

export function detachPondUserForCheckpointTest(pondId: string, userId: string): void {
  detachPondUser(pondId, userId);
  for (const [socketId, session] of sessions.entries()) {
    if (session.userId === userId) sessions.delete(socketId);
  }
}