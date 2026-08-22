import { randomUUID } from 'crypto';
import type { Express, Request, Response } from 'express';
import type { Server } from 'socket.io';
import {
  PONDS,
  POLICE_ESCAPE_BAN_MS,
  POLICE_WARNING_TEXT,
  getGamePondDef,
  getPoliceRules,
  isFishingActive,
  policeTriggerProbability,
  type ClientToServerEvents,
  type PoliceRaidPayload,
  type PondUser,
  type ServerToClientEvents,
} from '@fish-social/shared';
import { requireAuth, resolveAuthedPlayerId } from './auth.js';
import { db } from './db.js';
import { recordFishingMetric } from './fishingMetrics.js';
import { deductCoinsUpTo } from './players.js';
import { listUsersInPond, shanghaiDayStartMs, todayKey } from './pondUserManager.js';
import {
  resolveSocketByPlayer,
  resolveSocketByUser,
  unbindSocket,
} from './sessionRegistry.js';

const POLICE_TICK_MS = 1000;

export interface ActivePoliceRaid {
  raidId: string;
  playerId: string;
  pondId: string;
  userId: string;
  socketId: string;
  deadlineMs: number;
  settling: boolean;
}

type LeavePondFn = (socketId: string) => PondUser | null;

const raidsByPlayer = new Map<string, ActivePoliceRaid>();

let ioRef: Server<ClientToServerEvents, ServerToClientEvents> | null = null;
let leavePondFn: LeavePondFn | null = null;

export function bindPoliceRuntime(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  leavePond: LeavePondFn,
): void {
  ioRef = io;
  leavePondFn = leavePond;
}

export function isPoliceDebugEnabled(): boolean {
  return (
    process.env.NODE_ENV !== 'production' ||
    process.env.POLICE_RAID_DEBUG === '1' ||
    process.env.GAMEPLAY_DEBUG === '1'
  );
}

function pondDisplayName(pondId: string): string {
  return getGamePondDef(pondId)?.name ?? '该禁止塘';
}

function emitRaid(socketId: string, payload: PoliceRaidPayload): void {
  ioRef?.to(socketId).emit('police_raid', payload);
}

export function checkForbiddenPondBan(
  playerId: string,
  pondId: string,
  atMs: number = Date.now(),
): { ok: true } | { ok: false; error: string } {
  const row = db
    .prepare(
      `SELECT until_ms, kind FROM player_forbidden_bans WHERE player_id = ? AND pond_id = ?`,
    )
    .get(playerId, pondId) as { until_ms: number; kind: string } | undefined;
  if (!row || row.until_ms <= atMs) return { ok: true };
  const pondName = pondDisplayName(pondId);
  if (row.kind === 'fine') {
    return { ok: false, error: `今日禁止在${pondName}钓鱼（巡警罚款）` };
  }
  return { ok: false, error: `${pondName} 2 小时内不可再进入（刚才跑掉了）` };
}

export function upsertForbiddenBan(
  playerId: string,
  pondId: string,
  untilMs: number,
  kind: 'escape' | 'fine',
): void {
  const existing = db
    .prepare(
      `SELECT until_ms FROM player_forbidden_bans WHERE player_id = ? AND pond_id = ?`,
    )
    .get(playerId, pondId) as { until_ms: number } | undefined;
  const nextUntil = existing ? Math.max(existing.until_ms, untilMs) : untilMs;
  db.prepare(
    `INSERT INTO player_forbidden_bans (player_id, pond_id, until_ms, kind, updated_at)
     VALUES (?, ?, ?, ?, ?)
     ON CONFLICT(player_id, pond_id) DO UPDATE SET
       until_ms = excluded.until_ms,
       kind = excluded.kind,
       updated_at = excluded.updated_at`,
  ).run(playerId, pondId, nextUntil, kind, Date.now());
}

export function notePoliceLeaveIfNeeded(playerId: string, pondId: string): void {
  const raid = raidsByPlayer.get(playerId);
  if (!raid || raid.pondId !== pondId) return;
  if (raid.settling) return;
  resolveEscape(raid);
}

function resolveEscape(raid: ActivePoliceRaid): void {
  const untilMs = Date.now() + POLICE_ESCAPE_BAN_MS;
  upsertForbiddenBan(raid.playerId, raid.pondId, untilMs, 'escape');
  raidsByPlayer.delete(raid.playerId);
  recordFishingMetric('forbidden_pond_escaped', {
    playerId: raid.playerId,
    pondId: raid.pondId,
    payload: { untilMs, raidId: raid.raidId },
  });
  emitRaid(raid.socketId, {
    status: 'escaped',
    raidId: raid.raidId,
    pondId: raid.pondId,
    text: POLICE_WARNING_TEXT,
    deadlineMs: raid.deadlineMs,
    message: `${pondDisplayName(raid.pondId)}：已跑掉，免罚款。2 小时内不可再进入。`,
  });
}

function resolveFine(raid: ActivePoliceRaid): void {
  raid.settling = true;
  const rules = getPoliceRules('forbidden');
  const fine = deductCoinsUpTo(raid.playerId, rules.fineGold);
  const untilMs = shanghaiDayStartMs(todayKey()) + 24 * 60 * 60 * 1000;
  upsertForbiddenBan(raid.playerId, raid.pondId, untilMs, 'fine');
  const message =
    fine.charged > 0
      ? `巡警罚款 ${fine.charged} 金币（余额 ${fine.coinsAfter}）。今日禁止再进入${pondDisplayName(raid.pondId)}。`
      : `金币不足已归零。今日禁止再进入${pondDisplayName(raid.pondId)}。`;
  recordFishingMetric('forbidden_pond_fine', {
    playerId: raid.playerId,
    pondId: raid.pondId,
    payload: {
      charged: fine.charged,
      coinsAfter: fine.coinsAfter,
      fineGold: rules.fineGold,
      raidId: raid.raidId,
    },
  });
  emitRaid(raid.socketId, {
    status: 'fined',
    raidId: raid.raidId,
    pondId: raid.pondId,
    text: POLICE_WARNING_TEXT,
    deadlineMs: raid.deadlineMs,
    coinsAfter: fine.coinsAfter,
    charged: fine.charged,
    message,
  });
  const left = leavePondFn?.(raid.socketId) ?? null;
  ioRef?.sockets.sockets.get(raid.socketId)?.leave(raid.pondId);
  unbindSocket(raid.socketId);
  if (left?.id) ioRef?.to(raid.pondId).emit('pond_user_left', left.id);
  recordFishingMetric('leave_pond', {
    playerId: raid.playerId,
    pondId: raid.pondId,
    payload: { reason: 'police_fine', raidId: raid.raidId },
  });
  raidsByPlayer.delete(raid.playerId);
}

export function startPoliceRaidForUser(opts: {
  playerId: string;
  pondId: string;
  userId: string;
  socketId: string;
  now?: number;
}): { ok: true; raid: ActivePoliceRaid } | { ok: false; error: string } {
  const now = opts.now ?? Date.now();
  const def = getGamePondDef(opts.pondId);
  const rules = getPoliceRules(def?.pondCategory);
  if (!rules.enabled) return { ok: false, error: '当前不是禁止钓鱼塘' };
  if (raidsByPlayer.has(opts.playerId)) return { ok: false, error: '巡警已在追捕中' };
  const raid: ActivePoliceRaid = {
    raidId: randomUUID(),
    playerId: opts.playerId,
    pondId: opts.pondId,
    userId: opts.userId,
    socketId: opts.socketId,
    deadlineMs: now + rules.warningMs,
    settling: false,
  };
  raidsByPlayer.set(opts.playerId, raid);
  emitRaid(opts.socketId, {
    status: 'warning',
    raidId: raid.raidId,
    pondId: raid.pondId,
    text: POLICE_WARNING_TEXT,
    deadlineMs: raid.deadlineMs,
    message: POLICE_WARNING_TEXT,
  });
  return { ok: true, raid };
}

export function tryTriggerPoliceRaids(now: number = Date.now(), dtMs: number = POLICE_TICK_MS): void {
  for (const pond of PONDS) {
    const def = getGamePondDef(pond.id);
    const rules = getPoliceRules(def?.pondCategory);
    if (!rules.enabled) continue;
    const chance = policeTriggerProbability(rules.chancePerHour, dtMs);
    if (chance <= 0) continue;
    for (const user of listUsersInPond(pond.id)) {
      if (user.isBot || !user.playerId) continue;
      if (!isFishingActive(user.fishingPhase)) continue;
      if (raidsByPlayer.has(user.playerId)) continue;
      if (!checkForbiddenPondBan(user.playerId, pond.id, now).ok) continue;
      if (Math.random() >= chance) continue;
      const socketId = resolveSocketByUser(user.id) ?? resolveSocketByPlayer(user.playerId);
      if (!socketId) continue;
      startPoliceRaidForUser({
        playerId: user.playerId,
        pondId: pond.id,
        userId: user.id,
        socketId,
        now,
      });
    }
  }
}

export function resolveExpiredRaids(now: number = Date.now()): void {
  for (const raid of [...raidsByPlayer.values()]) {
    if (raid.settling || now < raid.deadlineMs) continue;
    resolveFine(raid);
  }
}

export function tickPoliceRaids(now: number = Date.now()): void {
  tryTriggerPoliceRaids(now);
  resolveExpiredRaids(now);
}

export function findLivePondUser(
  playerId: string,
): { pondId: string; user: PondUser } | null {
  for (const pond of PONDS) {
    for (const user of listUsersInPond(pond.id)) {
      if (user.playerId === playerId && !user.isBot) {
        return { pondId: pond.id, user };
      }
    }
  }
  return null;
}

export function forceTriggerPoliceRaid(
  playerId: string,
): { ok: true; message: string } | { ok: false; error: string } {
  const found = findLivePondUser(playerId);
  if (!found) return { ok: false, error: '当前不在鱼塘' };
  const def = getGamePondDef(found.pondId);
  if (def?.pondCategory !== 'forbidden') return { ok: false, error: '当前不是禁止钓鱼塘' };
  if (!isFishingActive(found.user.fishingPhase)) return { ok: false, error: '需在禁止塘钓鱼中' };
  const socketId =
    resolveSocketByUser(found.user.id) ?? resolveSocketByPlayer(playerId);
  if (!socketId) return { ok: false, error: '当前不在鱼塘' };
  const started = startPoliceRaidForUser({
    playerId,
    pondId: found.pondId,
    userId: found.user.id,
    socketId,
  });
  if (!started.ok) return started;
  return { ok: true, message: POLICE_WARNING_TEXT };
}

export function getActivePoliceRaid(playerId: string): ActivePoliceRaid | undefined {
  return raidsByPlayer.get(playerId);
}

export function resetPoliceStateForTests(): void {
  raidsByPlayer.clear();
}

export function registerForbiddenPoliceRoutes(app: Express): void {
  app.post('/api/debug/police-raid', requireAuth, (req: Request, res: Response) => {
    if (!isPoliceDebugEnabled()) {
      res.status(403).json({ ok: false, error: '正式环境不提供一键出警' });
      return;
    }
    const playerId = resolveAuthedPlayerId(req);
    if (!playerId) {
      res.status(401).json({ ok: false, error: '未登录' });
      return;
    }
    const result = forceTriggerPoliceRaid(playerId);
    if (!result.ok) {
      res.status(400).json(result);
      return;
    }
    res.json(result);
  });
}
