import { randomUUID } from 'crypto';
import {
  MAX_DAILY_FISHING_MS,
  MAX_POND_USERS,
  PONDS,
  DEFAULT_AVATARS,
  defaultAvatarPath,
  getPondById,
  isFishingActive,
  type ClientToServerEvents,
  type PondSnapshot,
  type PondUser,
  type ServerToClientEvents,
} from '@fish-social/shared';
import type { Server } from 'socket.io';
import { getInventory, clearPendingCatch } from './inventory.js';
import { getPlayer } from './players.js';
import { db } from './db.js';
import { getPondEcologySummary } from './pondEcology.js';
import {
  logStructuredEvent,
  shouldLogPerf,
} from './fishingObservability.js';
import { recordFishingMetric } from './fishingMetrics.js';
import {
  applyCheckpointToUser,
  deletePlayerPondSession,
  isCheckpointExpired,
  loadPlayerPondSession,
  upsertPlayerPondSession,
} from './playerPondSession.js';
import { cancelByUser } from './timerRegistry.js';
import { getConfigString } from './gameConfig.js';
import { getPondMessages, ensurePondChat } from './pondChat.js';
import { ensureFishingStartedAt } from './fishingStartedAt.js';

let lastSnapshotBuildPerfLogAt = 0;

const AVATAR_COLORS = ['#E57373', '#64B5F6', '#81C784', '#FFB74D', '#BA68C8', '#4DD0E1', '#F06292', '#A1887F'];

export interface BotMeta {
  playerId: string;
  pondId: string;
  leaveAt: number;
}

const pondUsers = new Map<string, Map<string, PondUser>>();
const botMeta = new Map<string, BotMeta>();
const dirtyUsersByPond = new Map<string, Set<string>>();
const waitingUsersByPond = new Map<string, Set<string>>();
/** BUG-19：内部「已落账到时刻」；不上网。checkpoint 前移此值，不改 sessionStartedAt。 */
const quotaCheckpointAtByUser = new Map<string, number>();

export function getQuotaCheckpointAt(userId: string): number | undefined {
  return quotaCheckpointAtByUser.get(userId);
}

export function clearQuotaCheckpoint(userId: string): void {
  quotaCheckpointAtByUser.delete(userId);
}

export function initQuotaCheckpoint(userId: string, atMs: number): void {
  quotaCheckpointAtByUser.set(userId, atMs);
}

const getDailyFishingStmt = db.prepare(
  'SELECT ms FROM daily_fishing WHERE user_id = ? AND date_key = ?',
);
const upsertDailyFishingStmt = db.prepare(`
  INSERT INTO daily_fishing (user_id, date_key, ms) VALUES (@userId, @dateKey, @ms)
  ON CONFLICT(user_id, date_key) DO UPDATE SET ms = excluded.ms
`);

/** Testable clock (FISH-DAILY-1 verify). */
let clockNow: (() => number) | null = null;

export function setFishingClockForTests(fn: (() => number) | null): void {
  clockNow = fn;
}

function nowMs(): number {
  return clockNow ? clockNow() : Date.now();
}

/** Asia/Shanghai calendar day YYYY-MM-DD — fishing quota day key. */
export function todayKey(ms: number = nowMs()): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Shanghai' }).format(new Date(ms));
}

export function shanghaiDayStartMs(dateKey: string): number {
  return Date.parse(`${dateKey}T00:00:00+08:00`);
}

export function getFishingMsForDate(playerId: string, dateKey: string): number {
  const row = getDailyFishingStmt.get(playerId, dateKey) as { ms: number } | undefined;
  const ms = row?.ms ?? 0;
  return Math.min(MAX_DAILY_FISHING_MS, Math.max(0, ms));
}

/** Max credited segment per flush (also daily cap). */
const MAX_FLUSH_SEGMENT_MS = MAX_DAILY_FISHING_MS;
/**
 * Uncredited span beyond this is treated as corrupt anchor (epoch / stale).
 * Must be > MAX so a full legal day segment can still credit; must not clamp to now−8h.
 */
const ABSURD_UNCREDITED_SESSION_MS = 24 * 60 * 60 * 1000;

export function addFishingMsForDate(playerId: string, dateKey: string, deltaMs: number): number {
  if (!Number.isFinite(deltaMs) || deltaMs <= 0) return getFishingMsForDate(playerId, dateKey);
  // Ignore absurd deltas (e.g. startedAt≈0 → multi-year elapsed)
  const cappedDelta = Math.min(deltaMs, MAX_FLUSH_SEGMENT_MS);
  const current = getFishingMsForDate(playerId, dateKey);
  const next = Math.min(MAX_DAILY_FISHING_MS, current + cappedDelta);
  if (next <= current) return current;
  upsertDailyFishingStmt.run({ userId: playerId, dateKey, ms: next });
  return next;
}

export function getTodayFishingMs(playerId: string, atMs: number = nowMs()): number {
  return getFishingMsForDate(playerId, todayKey(atMs));
}

export function addTodayFishingMs(playerId: string, deltaMs: number, atMs: number = nowMs()): number {
  return addFishingMsForDate(playerId, todayKey(atMs), deltaMs);
}

function isActivelyFishing(user: PondUser): boolean {
  const sessionAt = user.sessionStartedAt ?? user.fishingStartedAt;
  return user.status === 'fishing' && sessionAt != null;
}

function getSessionStartedAt(user: PondUser): number | null {
  return user.sessionStartedAt ?? user.fishingStartedAt ?? null;
}

function setSessionAnchors(user: PondUser, atMs: number | null): void {
  user.sessionStartedAt = atMs;
  user.fishingStartedAt = atMs;
}

/**
 * 仅对齐已有锚点 / checkpoint；finalize 清空后不得在 enrich 路径复活。
 * 开钓必须显式 setSessionAnchors + initQuotaCheckpoint。
 */
function ensureSessionAnchors(user: PondUser, _atMs: number = nowMs()): void {
  if (!isFishingActive(user.fishingPhase) || user.fishingPhase === 'stopping') return;
  if (user.sessionStartedAt == null && user.fishingStartedAt != null) {
    user.sessionStartedAt = user.fishingStartedAt;
  } else if (user.fishingStartedAt == null && user.sessionStartedAt != null) {
    user.fishingStartedAt = user.sessionStartedAt;
  }
  const sessionAt = getSessionStartedAt(user);
  if (sessionAt == null) return;
  if (!quotaCheckpointAtByUser.has(user.id)) {
    initQuotaCheckpoint(user.id, sessionAt);
  }
}

/**
 * Safe elapsed for quota credit.
 * - Illegal / future / non-finite → 0
 * - Span > MAX_DAILY → 0（禁止「夹成 8h 前再记满一天」）
 * - Otherwise min(elapsed, MAX_FLUSH)
 */
export function safeFishingElapsedMs(
  startedAt: number | null | undefined,
  atMs: number = nowMs(),
): number {
  if (startedAt == null || !Number.isFinite(startedAt) || startedAt <= 0 || startedAt > atMs) {
    return 0;
  }
  const elapsed = atMs - startedAt;
  if (elapsed <= 0) return 0;
  // >8h uncredited: reject (corrupt or overlong) — do not credit a full day from a bad anchor
  if (elapsed > MAX_FLUSH_SEGMENT_MS) return 0;
  return elapsed;
}

/**
 * Fix illegal / absurd fishingStartedAt for display & subsequent credit.
 * Overlong span → reset to now (elapsed 0), NEVER clamp to atMs−8h.
 */
export function sanitizeFishingStartedAt(user: PondUser, atMs: number = nowMs()): void {
  if (user.fishingStartedAt == null) return;
  const started = user.fishingStartedAt;
  if (!Number.isFinite(started) || started <= 0 || started > atMs) {
    user.fishingStartedAt = atMs;
    return;
  }
  if (atMs - started > ABSURD_UNCREDITED_SESSION_MS) {
    user.fishingStartedAt = atMs;
    return;
  }
  // Span in (MAX_DAILY, 24h]: leave anchor for session timer UX; flush uses safeFishingElapsedMs → 0
}

function syncTodayFishingMsFromDb(user: PondUser, atMs: number = nowMs()): void {
  if (!user.playerId) {
    user.todayFishingMs = 0;
    return;
  }
  user.todayFishingMs = getTodayFishingMs(user.playerId, atMs);
}

/**
 * FISH-DAILY-1 + BUG-15: Shanghai day rollover; idle always aligns memory to DB.
 */
export function ensureFishingDayRollover(user: PondUser, atMs: number = nowMs()): void {
  const today = todayKey(atMs);
  if (!user.fishingDayKey) {
    user.fishingDayKey = today;
    if (!isActivelyFishing(user)) {
      syncTodayFishingMsFromDb(user, atMs);
    } else if (user.playerId) {
      syncTodayFishingMsFromDb(user, atMs);
      sanitizeFishingStartedAt(user, atMs);
    }
    return;
  }

  // BUG-15：同日未在钓也读库覆盖脏内存；在钓只校正 baseline（= DB），不把 enrich 展示值写回
  if (user.fishingDayKey === today) {
    if (!isActivelyFishing(user)) {
      syncTodayFishingMsFromDb(user, atMs);
    } else {
      syncTodayFishingMsFromDb(user, atMs);
      sanitizeFishingStartedAt(user, atMs);
    }
    return;
  }

  const oldDay = user.fishingDayKey;
  const boundaryMs = shanghaiDayStartMs(today);

  if (user.status === 'fishing' && getSessionStartedAt(user) !== null) {
    if (user.fishingStartedAt != null) sanitizeFishingStartedAt(user, atMs);
    const started = getSessionStartedAt(user)!;
    const checkpointAt = quotaCheckpointAtByUser.get(user.id) ?? started;
    if (user.playerId && checkpointAt < boundaryMs) {
      const oldElapsed = safeFishingElapsedMs(checkpointAt, Math.min(atMs, boundaryMs));
      if (oldElapsed > 0) {
        addFishingMsForDate(user.playerId, oldDay, oldElapsed);
      }
    }
    // 新上海日：展示锚点与 checkpoint 都收到日界（会话不中断）
    const reanchor = Math.max(started, boundaryMs);
    setSessionAnchors(user, reanchor);
    initQuotaCheckpoint(user.id, Math.max(checkpointAt, boundaryMs));
    user.todayFishingMs = user.playerId ? getTodayFishingMs(user.playerId, atMs) : 0;
    user.todayFishingBaseMs = user.todayFishingMs;
  } else if (user.playerId) {
    user.todayFishingMs = getTodayFishingMs(user.playerId, atMs);
  } else {
    user.todayFishingMs = 0;
  }

  user.fishingDayKey = today;
}

function pickColor(index: number): string {
  return AVATAR_COLORS[index % AVATAR_COLORS.length];
}

function pickBotAvatar(): string {
  const avatar = DEFAULT_AVATARS[Math.floor(Math.random() * DEFAULT_AVATARS.length)];
  return defaultAvatarPath(avatar.filename);
}

function resolvePlayerAvatar(playerId: string): string | undefined {
  return getPlayer(playerId)?.avatarUrl;
}

export function ensurePondUsers(pondId: string): Map<string, PondUser> {
  if (!pondUsers.has(pondId)) {
    pondUsers.set(pondId, new Map());
  }
  ensurePondChat(pondId);
  return pondUsers.get(pondId)!;
}

function ensureDirtySet(pondId: string): Set<string> {
  if (!dirtyUsersByPond.has(pondId)) dirtyUsersByPond.set(pondId, new Set());
  return dirtyUsersByPond.get(pondId)!;
}

function ensureWaitingSet(pondId: string): Set<string> {
  if (!waitingUsersByPond.has(pondId)) waitingUsersByPond.set(pondId, new Set());
  return waitingUsersByPond.get(pondId)!;
}

/** Mark user for dirty-index bookkeeping (phase/spot changes still call this). */
export function markUserDirty(pondId: string, userId: string): void {
  ensureDirtySet(pondId).add(userId);
}

/**
 * PERF-04: drain dirty user ids. Live room sync must NOT broadcast from this
 * (BUG-07: dirty merge dropped sessionFishingMs). Prefer emitUserUpdated for
 * real state changes and session_timer_tick for 1s duration. Metrics loop
 * drains the set so it does not grow unbounded.
 */
export function consumeDirtyUsers(pondId: string): string[] {
  const set = ensureDirtySet(pondId);
  const ids = [...set];
  set.clear();
  return ids;
}

export function setUserWaiting(pondId: string, userId: string, waiting: boolean): void {
  const set = ensureWaitingSet(pondId);
  if (waiting) set.add(userId);
  else set.delete(userId);
}

export function getWaitingUserIds(pondId: string): string[] {
  return [...ensureWaitingSet(pondId)];
}

export function removeUserIndexes(pondId: string, userId: string): void {
  ensureDirtySet(pondId).delete(userId);
  ensureWaitingSet(pondId).delete(userId);
}

export function findFreeSpot(pondId: string): string | null {
  const pond = getPondById(pondId);
  if (!pond) return null;
  const users = ensurePondUsers(pondId);
  const taken = new Set(
    [...users.values()].map((u) => u.spotId).filter((s): s is string => s !== null),
  );
  const free = pond.spots.find((s) => !taken.has(s.id));
  return free?.id ?? null;
}

export function allSpotsTaken(pondId: string): boolean {
  return findFreeSpot(pondId) === null;
}

export function hasBotInPond(pondId: string): boolean {
  return [...ensurePondUsers(pondId).values()].some((u) => u.isBot);
}

export function listBotsInPond(pondId: string): PondUser[] {
  return [...ensurePondUsers(pondId).values()].filter((u) => u.isBot);
}

export function getBotPlayerId(userId: string): string | undefined {
  return botMeta.get(userId)?.playerId;
}

export function getBotMeta(userId: string): BotMeta | undefined {
  return botMeta.get(userId);
}

export function removeBotUser(pondId: string, userId: string): PondUser | null {
  const users = pondUsers.get(pondId);
  const user = users?.get(userId);
  if (!user?.isBot) return null;

  users?.delete(userId);
  removeUserIndexes(pondId, userId);
  clearPendingCatch(userId);
  botMeta.delete(userId);
  return user;
}

export function evictOneBot(pondId: string): PondUser | null {
  const bots = listBotsInPond(pondId);
  if (bots.length === 0) return null;
  const victim = bots[Math.floor(Math.random() * bots.length)];
  return removeBotUser(pondId, victim.id);
}

export function addBotUser(
  pondId: string,
  playerId: string,
  nickname: string,
  leaveAt: number,
): PondUser | null {
  const pond = getPondById(pondId);
  if (!pond) return null;

  const users = ensurePondUsers(pondId);
  if (users.size >= MAX_POND_USERS) return null;

  const userId = randomUUID();
  const user: PondUser = {
    id: userId,
    playerId,
    nickname: nickname.trim().slice(0, 12) || '钓友',
    color: pickColor(users.size),
    avatarUrl: pickBotAvatar(),
    spotId: null,
    status: 'idle',
    fishingStartedAt: null,
    todayFishingMs: 0,
    fishingDayKey: todayKey(),
    isBot: true,
    fishingPhase: 'idle',
    phaseEndsAt: null,
  };

  users.set(userId, user);
  markUserDirty(pondId, userId);
  setUserWaiting(pondId, userId, false);
  botMeta.set(userId, { playerId, pondId, leaveAt });
  return user;
}

export function evictBotsForHuman(pondId: string): string[] {
  const evicted: string[] = [];
  const evictPolicy = getConfigString('BOT_EVICT_POLICY', 'random');
  if (evictPolicy === 'none') return evicted;
  while (ensurePondUsers(pondId).size >= MAX_POND_USERS && hasBotInPond(pondId)) {
    const removed = evictOneBot(pondId);
    if (!removed) break;
    evicted.push(removed.id);
    recordFishingMetric('bot_evicted_for_human', {
      playerId: removed.playerId,
      pondId,
      payload: { userId: removed.id, reason: 'pond_full_or_spot_pressure', eventId: `bot_evict:${removed.id}:${Date.now()}` },
    });
  }
  while (allSpotsTaken(pondId) && hasBotInPond(pondId)) {
    const removed = evictOneBot(pondId);
    if (!removed) break;
    evicted.push(removed.id);
    recordFishingMetric('bot_evicted_for_human', {
      playerId: removed.playerId,
      pondId,
      payload: { userId: removed.id, reason: 'all_spots_taken', eventId: `bot_evict:${removed.id}:${Date.now()}` },
    });
  }
  return evicted;
}

/**
 * BUG-19：今日已用展示 = DB base + 自 checkpoint 起未落账段。
 * 注意：不得写成 base + (now - sessionStartedAt)，否则 checkpoint 后会双计。
 */
function computeDisplayUsedMs(user: PondUser, atMs: number = nowMs()): number {
  const base = user.playerId ? getTodayFishingMs(user.playerId, atMs) : Math.max(0, user.todayFishingMs ?? 0);
  const sessionAt = getSessionStartedAt(user);
  const fishing =
    isFishingActive(user.fishingPhase) &&
    user.fishingPhase !== 'stopping' &&
    sessionAt != null;
  if (!fishing) return Math.min(MAX_DAILY_FISHING_MS, base);

  const checkpointAt = quotaCheckpointAtByUser.get(user.id) ?? sessionAt;
  const uncredited = safeFishingElapsedMs(checkpointAt, atMs);
  return Math.min(MAX_DAILY_FISHING_MS, base + uncredited);
}

export function computeSessionFishingMs(user: PondUser, atMs: number = nowMs()): number {
  if (isFishingActive(user.fishingPhase) && user.fishingPhase !== 'stopping') {
    ensureSessionAnchors(user, atMs);
  }
  const sessionAt = getSessionStartedAt(user);
  if (
    sessionAt != null &&
    (user.status === 'fishing' || isFishingActive(user.fishingPhase))
  ) {
    return Math.max(0, atMs - sessionAt);
  }
  return 0;
}

/** BUG-19：finalize | checkpoint；advance 为 checkpoint 兼容别名 */
export type SettleFishingMode = 'finalize' | 'checkpoint' | 'advance';

/**
 * BUG-19 统一结算出口（幂等）。
 * - finalize：入账后清空展示锚点与 checkpoint（stop / leave / disconnect / 相位收尾）
 * - checkpoint：入账后仅前移内部 checkpoint，**禁止**改 sessionStartedAt / fishingStartedAt
 * - advance：兼容别名 → checkpoint
 */
export function settleFishingSession(
  user: PondUser,
  atMs: number = nowMs(),
  reason: string = 'settle',
  opts?: { mode?: SettleFishingMode },
): number {
  const rawMode: SettleFishingMode = opts?.mode ?? 'finalize';
  const mode: 'finalize' | 'checkpoint' = rawMode === 'advance' ? 'checkpoint' : rawMode;
  ensureFishingDayRollover(user, atMs);

  if (!user.playerId || user.isBot) {
    if (mode === 'finalize') {
      setSessionAnchors(user, null);
      clearQuotaCheckpoint(user.id);
    }
    return 0;
  }

  const sessionAt = getSessionStartedAt(user);
  let checkpointAt = quotaCheckpointAtByUser.get(user.id) ?? sessionAt;

  // 已结算（无会话、无 checkpoint）→ 对齐 DB 后返回
  if (sessionAt == null && checkpointAt == null) {
    syncTodayFishingMsFromDb(user, atMs);
    user.todayFishingBaseMs = user.todayFishingMs;
    return 0;
  }

  if (checkpointAt == null && sessionAt != null) {
    checkpointAt = sessionAt;
    initQuotaCheckpoint(user.id, checkpointAt);
  }

  if (user.fishingStartedAt != null) sanitizeFishingStartedAt(user, atMs);
  // sanitize 可能改 fishingStartedAt；展示锚点以 sessionStartedAt 为准，拉回
  if (user.sessionStartedAt != null && user.fishingStartedAt !== user.sessionStartedAt) {
    user.fishingStartedAt = user.sessionStartedAt;
  }

  const elapsed = safeFishingElapsedMs(checkpointAt, atMs);
  const before = getTodayFishingMs(user.playerId, atMs);
  let credited = 0;
  if (elapsed > 0) {
    const after = addTodayFishingMs(user.playerId, elapsed, atMs);
    credited = Math.max(0, after - before);
    user.todayFishingMs = after;
    user.todayFishingBaseMs = after;
  } else {
    syncTodayFishingMsFromDb(user, atMs);
    user.todayFishingBaseMs = user.todayFishingMs;
  }

  if (mode === 'checkpoint') {
    // 即使 elapsed=0（坏锚点被拒）也前移 checkpoint，避免下一段再次 >8h 记 0
    initQuotaCheckpoint(user.id, atMs);
    // BUG-19：展示锚点绝对不动
  } else {
    setSessionAnchors(user, null);
    clearQuotaCheckpoint(user.id);
  }

  logStructuredEvent('fishing_settle', 'fishing_session_settled', {
    playerId: user.playerId,
    userId: user.id,
    fishingPhase: user.fishingPhase ?? null,
    reason,
    creditedMs: credited,
    elapsedMs: elapsed,
    mode,
    dateKey: todayKey(atMs),
    baseAfterMs: user.todayFishingBaseMs ?? user.todayFishingMs,
    todayFishingMs: user.todayFishingMs,
    sessionStartedAt: user.sessionStartedAt ?? null,
    checkpointAt: quotaCheckpointAtByUser.get(user.id) ?? null,
  });

  return credited;
}

/** @deprecated 用 settleFishingSession；保留别名供既有调用 */
export function flushFishingSessionToToday(user: PondUser, atMs: number = nowMs()): void {
  settleFishingSession(user, atMs, 'phase_end', { mode: 'finalize' });
}

export function enrichPondUser(user: PondUser, atMs: number = nowMs()): PondUser {
  ensureFishingDayRollover(user, atMs);
  const todayFishingBaseMs = user.playerId ? getTodayFishingMs(user.playerId, atMs) : 0;
  const active =
    isFishingActive(user.fishingPhase) &&
    user.fishingPhase !== 'stopping' &&
    getSessionStartedAt(user) != null;

  if (active) {
    if (user.status !== 'fishing') user.status = 'fishing';
    ensureSessionAnchors(user, atMs);
    if (user.fishingStartedAt != null) sanitizeFishingStartedAt(user, atMs);
    if (user.sessionStartedAt != null) user.fishingStartedAt = user.sessionStartedAt;
  } else if (user.playerId) {
    // BUG-15：未在钓强制对齐 DB（同日脏内存也会被纠正）
    syncTodayFishingMsFromDb(user, atMs);
  }

  const todayFishingMs = computeDisplayUsedMs(user, atMs);
  const todayRemainingMs = Math.max(0, MAX_DAILY_FISHING_MS - todayFishingMs);
  return {
    ...user,
    sessionStartedAt: user.sessionStartedAt ?? user.fishingStartedAt ?? null,
    todayFishingBaseMs,
    todayFishingMs,
    todayRemainingMs,
    sessionFishingMs: computeSessionFishingMs(user, atMs),
  };
}

/**
 * BUG-15 + BUG-19：扫描在塘人类 —
 * - 闲置：跨日 rollover / 对齐 DB 后推送
 * - 在钓：checkpoint 落账（不前移展示锚点）
 */
export function syncHumanQuotaAndEmit(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  atMs: number = nowMs(),
): number {
  let emitted = 0;
  for (const pond of PONDS) {
    for (const user of listUsersInPond(pond.id)) {
      if (user.isBot || !user.playerId) continue;
      const wasFishing = isActivelyFishing(user);
      const prevKey = user.fishingDayKey;
      const prevBase = user.playerId ? getTodayFishingMs(user.playerId, atMs) : user.todayFishingMs;
      const sessionBefore = getSessionStartedAt(user);

      let segmentCredited = 0;
      if (
        wasFishing ||
        (isFishingActive(user.fishingPhase) &&
          user.fishingPhase !== 'stopping' &&
          getSessionStartedAt(user) != null)
      ) {
        if (user.status !== 'fishing') user.status = 'fishing';
        segmentCredited = settleFishingSession(user, atMs, 'segment_tick', { mode: 'checkpoint' });
        // 契约：checkpoint 不得改展示锚点
        if (getSessionStartedAt(user) !== sessionBefore && sessionBefore != null) {
          setSessionAnchors(user, sessionBefore);
        }
      } else {
        ensureFishingDayRollover(user, atMs);
      }

      const dayChanged = prevKey !== user.fishingDayKey;
      const baseNow = getTodayFishingMs(user.playerId, atMs);
      const idleResynced = !wasFishing && prevBase !== baseNow;
      if (dayChanged || idleResynced || segmentCredited > 0) {
        emitPondUserUpdated(io, pond.id, user);
        emitted += 1;
      }
    }
  }
  return emitted;
}

/** BUG-13：统一出口，禁止裸 emit 未 enrich 的 PondUser */
export function emitPondUserUpdated(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  pondId: string,
  user: PondUser,
): void {
  io.to(pondId).emit('pond_user_updated', enrichPondUser(user));
}

export function buildSnapshot(pondId: string, playerId?: string): PondSnapshot | null {
  const startedAt = performance.now();
  const pond = getPondById(pondId);
  if (!pond) return null;
  const users = [...ensurePondUsers(pondId).values()].map(enrichPondUser);
  const snapshot: PondSnapshot = {
    pond,
    users,
    messages: getPondMessages(pondId),
    inventory: playerId ? getInventory(playerId) : undefined,
    ecology: getPondEcologySummary(pondId) ?? undefined,
  };
  const durationMs = performance.now() - startedAt;
  if (shouldLogPerf(lastSnapshotBuildPerfLogAt, durationMs)) {
    lastSnapshotBuildPerfLogAt = Date.now();
    logStructuredEvent('perf', 'snapshot_build_duration_ms', {
      eventType: 'snapshot_build_duration_ms',
      durationMs,
      pondId,
      userCount: users.length,
    });
  }
  return snapshot;
}

export function createHumanPondUser(
  pondId: string,
  playerId: string,
  nickname: string,
): PondUser {
  const users = ensurePondUsers(pondId);
  const userId = randomUUID();
  const user: PondUser = {
    id: userId,
    playerId,
    nickname: nickname.trim().slice(0, 12) || '钓友',
    color: pickColor(users.size),
    avatarUrl: resolvePlayerAvatar(playerId),
    spotId: null,
    status: 'idle',
    fishingStartedAt: null,
    sessionStartedAt: null,
    todayFishingMs: getTodayFishingMs(playerId),
    todayFishingBaseMs: getTodayFishingMs(playerId),
    fishingDayKey: todayKey(),
    fishingPhase: 'idle',
    phaseEndsAt: null,
  };
  users.set(userId, user);
  markUserDirty(pondId, userId);
  setUserWaiting(pondId, userId, false);
  return user;
}

export function stopBotFishing(
  pondId: string,
  userId: string,
): { ok: true; user: PondUser } | { ok: false; error: string } {
  const users = ensurePondUsers(pondId);
  const user = users.get(userId);
  if (!user?.isBot) return { ok: false, error: '非机器人' };

  if (user.status === 'fishing' && user.fishingStartedAt !== null) {
    ensureFishingDayRollover(user);
    user.todayFishingMs += nowMs() - user.fishingStartedAt;
  }

  user.status = 'idle';
  user.fishingStartedAt = null;
  user.fishingPhase = 'idle';
  user.phaseEndsAt = null;
  setUserWaiting(pondId, userId, false);
  return { ok: true, user };
}

export function resolveBotFishingSpot(pondId: string, userId: string): string | null {
  const users = ensurePondUsers(pondId);
  const user = users.get(userId);
  if (!user) return null;

  if (user.spotId) {
    const taken = [...users.values()].some(
      (u) => u.id !== userId && u.spotId === user.spotId,
    );
    if (!taken) return user.spotId;
  }
  return findFreeSpot(pondId);
}

export function startBotFishing(
  pondId: string,
  userId: string,
  spotId?: string,
  opts?: { elapsedMs?: number },
): { ok: true; user: PondUser } | { ok: false; error: string } {
  const pond = getPondById(pondId);
  if (!pond) return { ok: false, error: '鱼塘不存在' };

  const users = ensurePondUsers(pondId);
  const user = users.get(userId);
  if (!user?.isBot) return { ok: false, error: '非机器人' };

  const targetSpot = spotId ?? resolveBotFishingSpot(pondId, userId);
  if (!targetSpot) return { ok: false, error: '无空闲钓点' };

  const spotTaken = [...users.values()].some((u) => u.id !== userId && u.spotId === targetSpot);
  if (spotTaken) return { ok: false, error: '钓点已被占用' };

  ensureFishingDayRollover(user);
  const now = nowMs();
  const elapsed =
    opts?.elapsedMs != null && Number.isFinite(opts.elapsedMs)
      ? Math.max(0, Math.floor(opts.elapsedMs))
      : 0;
  user.spotId = targetSpot;
  user.status = 'fishing';
  // FISH-BOT-2：仅回拨内存锚点，不预写 daily_fishing
  user.fishingStartedAt = now - elapsed;
  user.fishingDayKey = todayKey();
  user.fishingPhase = 'waiting';
  user.phaseEndsAt = null;
  markUserDirty(pondId, userId);
  setUserWaiting(pondId, userId, true);
  return { ok: true, user };
}

export function autoAssignSpot(pondId: string, userId: string): PondUser | null {
  const users = ensurePondUsers(pondId);
  const user = users.get(userId);
  if (!user || user.spotId) return user ?? null;
  const spotId = findFreeSpot(pondId);
  if (!spotId) return user;
  user.spotId = spotId;
  markUserDirty(pondId, user.id);
  return user;
}

export function listPondOccupancy(): Record<string, number> {
  const result: Record<string, number> = {};
  for (const pond of PONDS) {
    result[pond.id] = pondUsers.get(pond.id)?.size ?? 0;
  }
  return result;
}

export function getUserById(pondId: string, userId: string): PondUser | undefined {
  return ensurePondUsers(pondId).get(userId);
}

export function updatePondUser(pondId: string, user: PondUser): void {
  ensurePondUsers(pondId).set(user.id, user);
  markUserDirty(pondId, user.id);
  setUserWaiting(pondId, user.id, user.fishingPhase === 'waiting');
}

export function listUsersInPond(pondId: string): PondUser[] {
  return [...ensurePondUsers(pondId).values()];
}

export function getEnrichedUsersByIds(pondId: string, userIds: readonly string[]): PondUser[] {
  const users = ensurePondUsers(pondId);
  const out: PondUser[] = [];
  for (const userId of userIds) {
    const user = users.get(userId);
    if (!user) continue;
    out.push(enrichPondUser(user));
  }
  return out;
}

export function findDisconnectedUserByPlayerId(pondId: string, playerId: string): PondUser | undefined {
  return [...ensurePondUsers(pondId).values()].find(
    (u) => u.playerId === playerId && u.fishingPhase === 'disconnected',
  );
}

export function markUserDisconnected(pondId: string, userId: string): PondUser | null {
  const users = ensurePondUsers(pondId);
  const user = users.get(userId);
  if (!user) return null;
  user.fishingPhase = 'disconnected';
  user.disconnectedAt = Date.now();
  user.phaseEndsAt = Date.now() + 60_000;
  markUserDirty(pondId, user.id);
  setUserWaiting(pondId, user.id, false);
  return user;
}

export function removeDisconnectedUser(pondId: string, userId: string): PondUser | null {
  const users = pondUsers.get(pondId);
  const user = users?.get(userId);
  if (!user) return null;
  // BUG-16：防御式结算（断线前通常已 finalize；锚点为空则幂等 0）
  settleFishingSession(user, nowMs(), 'disconnect_timeout', { mode: 'finalize' });
  if (user.spotId) {
    recordFishingMetric('spot_release', {
      playerId: user.playerId,
      pondId,
      payload: {
        userId: user.id,
        spotId: user.spotId,
        fishingPhase: user.fishingPhase,
        reason: 'disconnect_timeout',
        eventId: `spot_release:${user.id}:disconnect_timeout`,
      },
    });
  }
  users?.delete(userId);
  removeUserIndexes(pondId, userId);
  cancelByUser(userId);
  clearPendingCatch(userId, { playerId: user.playerId, pondId });
  if (user.playerId) deletePlayerPondSession(user.playerId, pondId);
  return user;
}

export function listHumansInPond(pondId: string): PondUser[] {
  return [...ensurePondUsers(pondId).values()].filter((u) => !u.isBot && u.playerId);
}

export function listAllHumanPlayerIdsInPonds(): string[] {
  const ids = new Set<string>();
  for (const pond of PONDS) {
    for (const u of listHumansInPond(pond.id)) {
      if (u.playerId) ids.add(u.playerId);
    }
  }
  return [...ids];
}

export function detachPondUser(pondId: string, userId: string): void {
  pondUsers.get(pondId)?.delete(userId);
  removeUserIndexes(pondId, userId);
}

export function restoreCheckpointUser(
  pondId: string,
  playerId: string,
  nickname: string,
): PondUser | null {
  const row = loadPlayerPondSession(playerId, pondId);
  if (!row || isCheckpointExpired(row)) {
    if (row) deletePlayerPondSession(playerId, pondId);
    return null;
  }

  const pond = getPondById(pondId);
  if (!pond) return null;

  const users = ensurePondUsers(pondId);
  if (users.size >= MAX_POND_USERS && !users.has(row.user_id)) {
    return null;
  }

  let user = users.get(row.user_id);
  if (!user) {
    user = {
      id: row.user_id,
      playerId,
      nickname: nickname.trim().slice(0, 12) || '钓友',
      color: pickColor(users.size),
      avatarUrl: resolvePlayerAvatar(playerId),
      spotId: null,
      status: 'idle',
      fishingStartedAt: null,
      sessionStartedAt: null,
      todayFishingMs: getTodayFishingMs(playerId),
      todayFishingBaseMs: getTodayFishingMs(playerId),
      fishingDayKey: todayKey(),
      fishingPhase: 'idle',
      phaseEndsAt: null,
    };
    users.set(row.user_id, user);
    markUserDirty(pondId, row.user_id);
    setUserWaiting(pondId, row.user_id, false);
  }

  ensureFishingDayRollover(user);
  applyCheckpointToUser(user, row);
  markUserDirty(pondId, user.id);
  setUserWaiting(pondId, user.id, user.fishingPhase === 'waiting');
  return user;
}

export { MAX_POND_USERS };
