import { formatPondName, formatSpotName, PONDS } from '@fish-social/shared';
import { db } from './db.js';
import { flushFishingMetricsQueue } from './fishingMetrics.js';
import { listUsersInPond, enrichPondUser } from './pondUserManager.js';
import { getPlayer } from './players.js';

export interface PlayerOverviewRow {
  playerId: string;
  nickname: string;
  pondId: string | null;
  spotId: string | null;
  pondName: string | null;
  spotName: string | null;
  fishingPhase: string | null;
  sessionFishingMs: number | null;
  catchCount: number;
  disconnectCount: number;
  biteHookCount: number;
  isBot: boolean;
  online: boolean;
  lastEventAt: number | null;
}

export interface PlayersOverviewResult {
  hours: number;
  humansOnly: boolean;
  pondId: string | null;
  phase: string | null;
  q: string | null;
  rows: PlayerOverviewRow[];
}

function isBotId(playerId: string): boolean {
  return playerId.startsWith('bot-');
}

function withPlaceNames(
  pondId: string | null,
  spotId: string | null,
): Pick<PlayerOverviewRow, 'pondName' | 'spotName'> {
  return {
    pondName: pondId ? formatPondName(pondId) : null,
    spotName: spotId ? formatSpotName(spotId, pondId) : null,
  };
}

function hasInventoryTable(): boolean {
  const row = db
    .prepare(`SELECT 1 AS ok FROM sqlite_master WHERE type='table' AND name='inventory'`)
    .get() as { ok: number } | undefined;
  return !!row;
}

/** OPS-CATCH-1.1：窗口内背包入库数（含 bot） */
function inventoryCatchByPlayer(sinceMs: number): Map<string, number> {
  const map = new Map<string, number>();
  if (!hasInventoryTable()) return map;
  const rows = db
    .prepare(
      `SELECT player_id AS playerId, COUNT(*) AS c
         FROM inventory
        WHERE caught_at >= ?
          AND player_id IS NOT NULL AND player_id != ''
        GROUP BY player_id`,
    )
    .all(sinceMs) as Array<{ playerId: string; c: number }>;
  for (const r of rows) map.set(r.playerId, Number(r.c) || 0);
  return map;
}

export function getPlayersOverview(opts: {
  hours?: number;
  humansOnly?: boolean;
  pondId?: string;
  phase?: string;
  q?: string;
}): PlayersOverviewResult {
  flushFishingMetricsQueue();
  const hours = Math.min(720, Math.max(1, opts.hours ?? 24));
  const humansOnly = opts.humansOnly !== false;
  const pondFilter = opts.pondId?.trim() || null;
  const phaseFilter = opts.phase?.trim() || null;
  const q = opts.q?.trim() || null;
  const since = Date.now() - hours * 60 * 60 * 1000;
  const invCatch = inventoryCatchByPlayer(since);

  const aggRows = db
    .prepare(
      `SELECT player_id AS playerId,
        SUM(CASE WHEN event_type IN ('disconnect','socket_disconnect','disconnect_timeout') THEN 1 ELSE 0 END) AS disconnectCount,
        SUM(CASE WHEN event_type = 'bite_hook' THEN 1 ELSE 0 END) AS biteHookCount,
        MAX(created_at) AS lastEventAt
       FROM fishing_metrics
       WHERE created_at >= ?
         AND player_id IS NOT NULL
         AND player_id != ''
       GROUP BY player_id`,
    )
    .all(since) as Array<{
    playerId: string;
    disconnectCount: number;
    biteHookCount: number;
    lastEventAt: number;
  }>;

  const lastPondRows = db
    .prepare(
      `SELECT player_id AS playerId, pond_id AS pondId
       FROM fishing_metrics
       WHERE created_at >= ?
         AND player_id IS NOT NULL
         AND player_id != ''
         AND pond_id IS NOT NULL
       ORDER BY created_at DESC`,
    )
    .all(since) as Array<{ playerId: string; pondId: string }>;
  const lastPond = new Map<string, string>();
  for (const row of lastPondRows) {
    if (!lastPond.has(row.playerId)) lastPond.set(row.playerId, row.pondId);
  }

  const map = new Map<string, PlayerOverviewRow>();
  for (const row of aggRows) {
    const bot = isBotId(row.playerId);
    if (humansOnly && bot) continue;
    const profile = getPlayer(row.playerId);
    const pondId = lastPond.get(row.playerId) ?? null;
    map.set(row.playerId, {
      playerId: row.playerId,
      nickname: profile?.nickname ?? '—',
      pondId,
      spotId: null,
      ...withPlaceNames(pondId, null),
      fishingPhase: null,
      sessionFishingMs: null,
      catchCount: invCatch.get(row.playerId) ?? 0,
      disconnectCount: Number(row.disconnectCount) || 0,
      biteHookCount: Number(row.biteHookCount) || 0,
      isBot: bot,
      online: false,
      lastEventAt: row.lastEventAt ?? null,
    });
  }

  // 仅有背包入库、无 metrics 的玩家（常见于 bot）
  for (const [playerId, catchCount] of invCatch) {
    if (map.has(playerId)) continue;
    const bot = isBotId(playerId);
    if (humansOnly && bot) continue;
    const profile = getPlayer(playerId);
    const pondId = lastPond.get(playerId) ?? null;
    map.set(playerId, {
      playerId,
      nickname: profile?.nickname ?? '—',
      pondId,
      spotId: null,
      ...withPlaceNames(pondId, null),
      fishingPhase: null,
      sessionFishingMs: null,
      catchCount,
      disconnectCount: 0,
      biteHookCount: 0,
      isBot: bot,
      online: false,
      lastEventAt: null,
    });
  }

  for (const pond of PONDS) {
    for (const raw of listUsersInPond(pond.id)) {
      const user = enrichPondUser(raw);
      if (!user.playerId) continue;
      const bot = !!user.isBot || isBotId(user.playerId);
      if (humansOnly && bot) continue;
      const spotId = user.spotId ?? null;
      const place = withPlaceNames(pond.id, spotId);
      const existing = map.get(user.playerId);
      if (existing) {
        existing.online = true;
        existing.pondId = pond.id;
        existing.spotId = spotId;
        existing.pondName = place.pondName;
        existing.spotName = place.spotName;
        existing.fishingPhase = user.fishingPhase ?? null;
        existing.sessionFishingMs = user.sessionFishingMs ?? null;
        if (user.nickname) existing.nickname = user.nickname;
        existing.isBot = bot;
        existing.catchCount = invCatch.get(user.playerId) ?? existing.catchCount;
      } else {
        map.set(user.playerId, {
          playerId: user.playerId,
          nickname: user.nickname || getPlayer(user.playerId)?.nickname || '—',
          pondId: pond.id,
          spotId,
          ...place,
          fishingPhase: user.fishingPhase ?? null,
          sessionFishingMs: user.sessionFishingMs ?? null,
          catchCount: invCatch.get(user.playerId) ?? 0,
          disconnectCount: 0,
          biteHookCount: 0,
          isBot: bot,
          online: true,
          lastEventAt: null,
        });
      }
    }
  }

  let rows = [...map.values()];
  if (pondFilter) {
    rows = rows.filter((r) => r.pondId === pondFilter);
  }
  if (phaseFilter) {
    rows = rows.filter((r) => r.fishingPhase === phaseFilter);
  }
  if (q) {
    const lower = q.toLowerCase();
    rows = rows.filter(
      (r) =>
        r.playerId === q ||
        r.playerId.toLowerCase().includes(lower) ||
        r.nickname.toLowerCase().includes(lower),
    );
  }

  rows.sort((a, b) => {
    if (a.online !== b.online) return a.online ? -1 : 1;
    if ((b.lastEventAt ?? 0) !== (a.lastEventAt ?? 0)) {
      return (b.lastEventAt ?? 0) - (a.lastEventAt ?? 0);
    }
    return a.playerId.localeCompare(b.playerId);
  });

  return {
    hours,
    humansOnly,
    pondId: pondFilter,
    phase: phaseFilter,
    q,
    rows,
  };
}
