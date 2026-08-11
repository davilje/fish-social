import { calcFishSellPrice, qualityIndex, type FishQuality, type LeaderboardBoardType, type LeaderboardEntry, type LeaderboardMyRank } from '@fish-social/shared';
import { db } from './db.js';
import { getPlayer } from './players.js';

const CACHE_TTL_MS = 5 * 60 * 1000;
const CATCH_EVENTS = "('pending_catch_accept', 'catch_accept')";

interface CacheEntry {
  at: number;
  entries: LeaderboardEntry[];
  computeCount: number;
}

const cache = new Map<string, CacheEntry>();
let totalComputeCount = 0;

interface CatchMetricRow {
  player_id: string;
  pond_id: string | null;
  created_at: number;
  payload: string;
}

interface InventoryCatchRow {
  player_id: string;
  species_id: string;
  quality: string;
  size_m: number;
  caught_at: number;
  pond_id: string | null;
}

interface ParsedCatch {
  playerId: string;
  pondId: string | null;
  createdAt: number;
  speciesId?: string;
  quality: FishQuality;
  sizeM: number;
  value: number;
}

export function getShanghaiDateKey(ms: number = Date.now()): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Shanghai' }).format(new Date(ms));
}

function shanghaiWeekdayMon0(ms: number): number {
  const wd = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Asia/Shanghai',
    weekday: 'short',
  }).format(new Date(ms));
  const map: Record<string, number> = {
    Mon: 0,
    Tue: 1,
    Wed: 2,
    Thu: 3,
    Fri: 4,
    Sat: 5,
    Sun: 6,
  };
  return map[wd] ?? 0;
}

export function getShanghaiWeekKey(ms: number = Date.now()): string {
  const dateKey = getShanghaiDateKey(ms);
  const dayStart = Date.parse(`${dateKey}T00:00:00+08:00`);
  const mondayStart = dayStart - shanghaiWeekdayMon0(ms) * 86_400_000;
  const thursday = mondayStart + 3 * 86_400_000;
  const year = Number(getShanghaiDateKey(thursday).slice(0, 4));
  const jan4 = Date.parse(`${year}-01-04T00:00:00+08:00`);
  const week1Monday = jan4 - shanghaiWeekdayMon0(jan4) * 86_400_000;
  const week = Math.floor((mondayStart - week1Monday) / (7 * 86_400_000)) + 1;
  return `${year}-W${String(week).padStart(2, '0')}`;
}

export function shanghaiDayBounds(dateKey: string): { startMs: number; endMs: number } {
  const startMs = Date.parse(`${dateKey}T00:00:00+08:00`);
  return { startMs, endMs: startMs + 86_400_000 };
}

export function shanghaiWeekBounds(weekKey: string): { startMs: number; endMs: number } {
  const m = /^(\d{4})-W(\d{2})$/.exec(weekKey);
  if (!m) throw new Error(`Invalid weekKey: ${weekKey}`);
  const year = Number(m[1]);
  const week = Number(m[2]);
  const jan4 = Date.parse(`${year}-01-04T00:00:00+08:00`);
  const week1Monday = jan4 - shanghaiWeekdayMon0(jan4) * 86_400_000;
  const startMs = week1Monday + (week - 1) * 7 * 86_400_000;
  return { startMs, endMs: startMs + 7 * 86_400_000 };
}

function parseCatchPayload(row: CatchMetricRow): ParsedCatch | null {
  if (!row.player_id) return null;
  let payload: Record<string, unknown>;
  try {
    payload = JSON.parse(row.payload) as Record<string, unknown>;
  } catch {
    return null;
  }
  const quality = payload.quality as FishQuality | undefined;
  const sizeM = Number(payload.sizeM);
  if (!quality || !Number.isFinite(sizeM)) return null;
  return {
    playerId: row.player_id,
    pondId: row.pond_id,
    createdAt: row.created_at,
    speciesId: typeof payload.speciesId === 'string' ? payload.speciesId : undefined,
    quality,
    sizeM,
    value: calcFishSellPrice({ quality, sizeM }),
  };
}

/** metrics 源（pond/rare 遗留路径；日/周榜已改 inventory） */
function loadCatches(startMs: number, endMs: number, pondId?: string): ParsedCatch[] {
  const rows = (
    pondId
      ? (db
          .prepare(
            `SELECT player_id, pond_id, created_at, payload FROM fishing_metrics
             WHERE event_type IN ${CATCH_EVENTS}
               AND created_at >= ? AND created_at < ?
               AND pond_id = ?`,
          )
          .all(startMs, endMs, pondId) as CatchMetricRow[])
      : (db
          .prepare(
            `SELECT player_id, pond_id, created_at, payload FROM fishing_metrics
             WHERE event_type IN ${CATCH_EVENTS}
               AND created_at >= ? AND created_at < ?`,
          )
          .all(startMs, endMs) as CatchMetricRow[])
  );
  const out: ParsedCatch[] = [];
  for (const row of rows) {
    const parsed = parseCatchPayload(row);
    if (parsed) out.push(parsed);
  }
  return out;
}

/** FEAT-UI-2: 日/周榜按 inventory 聚合（含 bot） */
function loadInventoryCatches(startMs: number, endMs: number): ParsedCatch[] {
  const rows = db
    .prepare(
      `SELECT player_id, species_id, quality, size_m, caught_at, pond_id
       FROM inventory
       WHERE caught_at >= ? AND caught_at < ?
         AND player_id IS NOT NULL AND player_id != ''`,
    )
    .all(startMs, endMs) as InventoryCatchRow[];

  const out: ParsedCatch[] = [];
  for (const row of rows) {
    const quality = row.quality as FishQuality;
    const sizeM = Number(row.size_m);
    if (!quality || !Number.isFinite(sizeM)) continue;
    out.push({
      playerId: row.player_id,
      pondId: row.pond_id,
      createdAt: row.caught_at,
      speciesId: row.species_id,
      quality,
      sizeM,
      value: calcFishSellPrice({ quality, sizeM }),
    });
  }
  return out;
}

function pickBestPerPlayer(catches: ParsedCatch[]): ParsedCatch[] {
  const best = new Map<string, ParsedCatch>();
  for (const c of catches) {
    const prev = best.get(c.playerId);
    if (
      !prev ||
      c.sizeM > prev.sizeM ||
      (c.sizeM === prev.sizeM && c.createdAt < prev.createdAt)
    ) {
      best.set(c.playerId, c);
    }
  }
  return [...best.values()].sort((a, b) => {
    if (b.sizeM !== a.sizeM) return b.sizeM - a.sizeM;
    if (a.createdAt !== b.createdAt) return a.createdAt - b.createdAt;
    return a.playerId.localeCompare(b.playerId);
  });
}

function toEntry(
  rank: number,
  playerId: string,
  value: number,
  extra?: LeaderboardEntry['extra'],
): LeaderboardEntry {
  const player = getPlayer(playerId);
  return {
    rank,
    playerId,
    nickname: player?.nickname ?? '钓友',
    ...(player?.avatarUrl ? { avatarUrl: player.avatarUrl } : {}),
    value,
    ...(extra ? { extra } : {}),
  };
}

function computeDailyBiggest(startMs: number, endMs: number, limit: number): LeaderboardEntry[] {
  const sorted = pickBestPerPlayer(loadInventoryCatches(startMs, endMs));
  return sorted.slice(0, limit).map((c, i) =>
    toEntry(i + 1, c.playerId, c.sizeM, {
      speciesId: c.speciesId,
      sizeM: c.sizeM,
      pondId: c.pondId ?? undefined,
      caughtAt: c.createdAt,
    }),
  );
}

function computeWeeklyKing(startMs: number, endMs: number, limit: number): LeaderboardEntry[] {
  const sorted = pickBestPerPlayer(loadInventoryCatches(startMs, endMs));
  return sorted.slice(0, limit).map((c, i) =>
    toEntry(i + 1, c.playerId, c.sizeM, {
      speciesId: c.speciesId,
      sizeM: c.sizeM,
      pondId: c.pondId ?? undefined,
      caughtAt: c.createdAt,
    }),
  );
}

function computePondBoard(
  startMs: number,
  endMs: number,
  pondId: string,
  limit: number,
): LeaderboardEntry[] {
  const catches = loadCatches(startMs, endMs, pondId);
  const agg = new Map<
    string,
    { catchCount: number; maxSize: number; firstAt: number; maxSizeAt: number }
  >();
  for (const c of catches) {
    const prev = agg.get(c.playerId);
    if (!prev) {
      agg.set(c.playerId, {
        catchCount: 1,
        maxSize: c.sizeM,
        firstAt: c.createdAt,
        maxSizeAt: c.createdAt,
      });
    } else {
      prev.catchCount += 1;
      if (c.createdAt < prev.firstAt) prev.firstAt = c.createdAt;
      if (c.sizeM > prev.maxSize || (c.sizeM === prev.maxSize && c.createdAt < prev.maxSizeAt)) {
        prev.maxSize = c.sizeM;
        prev.maxSizeAt = c.createdAt;
      }
    }
  }
  const sorted = [...agg.entries()].sort((a, b) => {
    if (b[1].catchCount !== a[1].catchCount) return b[1].catchCount - a[1].catchCount;
    if (a[1].firstAt !== b[1].firstAt) return a[1].firstAt - b[1].firstAt;
    return a[0].localeCompare(b[0]);
  });
  return sorted.slice(0, limit).map(([playerId, v], i) =>
    toEntry(i + 1, playerId, v.catchCount, {
      pondId,
      catchCount: v.catchCount,
      sizeM: v.maxSize,
    }),
  );
}

function computeRareBoard(startMs: number, endMs: number, limit: number): LeaderboardEntry[] {
  const purpleIdx = qualityIndex('purple');
  const catches = loadCatches(startMs, endMs).filter((c) => qualityIndex(c.quality) >= purpleIdx);
  const agg = new Map<
    string,
    { catchCount: number; maxSize: number; firstAt: number; maxSizeAt: number }
  >();
  for (const c of catches) {
    const prev = agg.get(c.playerId);
    if (!prev) {
      agg.set(c.playerId, {
        catchCount: 1,
        maxSize: c.sizeM,
        firstAt: c.createdAt,
        maxSizeAt: c.createdAt,
      });
    } else {
      prev.catchCount += 1;
      if (c.createdAt < prev.firstAt) prev.firstAt = c.createdAt;
      if (c.sizeM > prev.maxSize || (c.sizeM === prev.maxSize && c.createdAt < prev.maxSizeAt)) {
        prev.maxSize = c.sizeM;
        prev.maxSizeAt = c.createdAt;
      }
    }
  }
  const sorted = [...agg.entries()].sort((a, b) => {
    if (b[1].catchCount !== a[1].catchCount) return b[1].catchCount - a[1].catchCount;
    if (a[1].firstAt !== b[1].firstAt) return a[1].firstAt - b[1].firstAt;
    return a[0].localeCompare(b[0]);
  });
  return sorted.slice(0, limit).map(([playerId, v], i) =>
    toEntry(i + 1, playerId, v.catchCount, {
      catchCount: v.catchCount,
      sizeM: v.maxSize,
    }),
  );
}

function cacheKey(board: string, periodKey: string, limit: number, pondId?: string): string {
  return `${board}|${periodKey}|${limit}|${pondId ?? ''}`;
}

function getCachedOrCompute(
  key: string,
  compute: () => LeaderboardEntry[],
): { entries: LeaderboardEntry[]; fromCache: boolean } {
  const now = Date.now();
  const hit = cache.get(key);
  if (hit && now - hit.at < CACHE_TTL_MS) {
    return { entries: hit.entries, fromCache: true };
  }
  totalComputeCount += 1;
  const entries = compute();
  cache.set(key, { at: now, entries, computeCount: totalComputeCount });
  return { entries, fromCache: false };
}

export function getDailyBiggestLeaderboard(opts?: {
  date?: string;
  limit?: number;
}): LeaderboardEntry[] {
  const dateKey = opts?.date ?? getShanghaiDateKey(Date.now());
  const limit = Math.min(Math.max(1, opts?.limit ?? 20), 100);
  const { startMs, endMs } = shanghaiDayBounds(dateKey);
  const key = cacheKey('daily_biggest_inv', dateKey, limit);
  return getCachedOrCompute(key, () => computeDailyBiggest(startMs, endMs, limit)).entries;
}

export function getWeeklyKingLeaderboard(opts?: {
  week?: string;
  limit?: number;
}): LeaderboardEntry[] {
  const weekKey = opts?.week ?? getShanghaiWeekKey();
  const limit = Math.min(Math.max(1, opts?.limit ?? 20), 100);
  const { startMs, endMs } = shanghaiWeekBounds(weekKey);
  const key = cacheKey('weekly_king_inv', weekKey, limit);
  return getCachedOrCompute(key, () => computeWeeklyKing(startMs, endMs, limit)).entries;
}

export function getPondLeaderboard(
  pondId: string,
  opts?: { week?: string; limit?: number },
): LeaderboardEntry[] {
  const weekKey = opts?.week ?? getShanghaiWeekKey();
  const limit = Math.min(Math.max(1, opts?.limit ?? 10), 100);
  const { startMs, endMs } = shanghaiWeekBounds(weekKey);
  const periodKey = `${weekKey}:${pondId}`;
  const key = cacheKey('pond', periodKey, limit, pondId);
  return getCachedOrCompute(key, () => computePondBoard(startMs, endMs, pondId, limit)).entries;
}

export function getRareLeaderboard(opts?: {
  week?: string;
  limit?: number;
}): LeaderboardEntry[] {
  const weekKey = opts?.week ?? getShanghaiWeekKey();
  const limit = Math.min(Math.max(1, opts?.limit ?? 20), 100);
  const { startMs, endMs } = shanghaiWeekBounds(weekKey);
  const key = cacheKey('rare', weekKey, limit);
  return getCachedOrCompute(key, () => computeRareBoard(startMs, endMs, limit)).entries;
}

function playerValueOnBoard(
  boardType: LeaderboardBoardType,
  playerId: string,
  period: { date?: string; week?: string; pondId?: string },
): { value: number; extra?: LeaderboardEntry['extra'] } {
  if (boardType === 'daily_biggest') {
    const dateKey = period.date ?? getShanghaiDateKey(Date.now());
    const { startMs, endMs } = shanghaiDayBounds(dateKey);
    const mine = loadInventoryCatches(startMs, endMs).filter((c) => c.playerId === playerId);
    if (mine.length === 0) return { value: 0 };
    const best = mine.reduce((a, b) =>
      b.sizeM > a.sizeM || (b.sizeM === a.sizeM && b.createdAt < a.createdAt) ? b : a,
    );
    return {
      value: best.sizeM,
      extra: {
        speciesId: best.speciesId,
        sizeM: best.sizeM,
        pondId: best.pondId ?? undefined,
        caughtAt: best.createdAt,
      },
    };
  }

  const weekKey = period.week ?? getShanghaiWeekKey();
  const { startMs, endMs } = shanghaiWeekBounds(weekKey);

  if (boardType === 'weekly_king') {
    const mine = loadInventoryCatches(startMs, endMs).filter((c) => c.playerId === playerId);
    if (mine.length === 0) return { value: 0 };
    const best = mine.reduce((a, b) =>
      b.sizeM > a.sizeM || (b.sizeM === a.sizeM && b.createdAt < a.createdAt) ? b : a,
    );
    return {
      value: best.sizeM,
      extra: {
        speciesId: best.speciesId,
        sizeM: best.sizeM,
        pondId: best.pondId ?? undefined,
        caughtAt: best.createdAt,
      },
    };
  }

  if (boardType === 'pond') {
    const pondId = period.pondId;
    if (!pondId) return { value: 0 };
    const mine = loadCatches(startMs, endMs, pondId).filter((c) => c.playerId === playerId);
    const maxSize = mine.reduce((m, c) => Math.max(m, c.sizeM), 0);
    return {
      value: mine.length,
      extra: { pondId, catchCount: mine.length, sizeM: maxSize },
    };
  }

  // rare
  const purpleIdx = qualityIndex('purple');
  const mine = loadCatches(startMs, endMs).filter(
    (c) => c.playerId === playerId && qualityIndex(c.quality) >= purpleIdx,
  );
  const maxSize = mine.reduce((m, c) => Math.max(m, c.sizeM), 0);
  return { value: mine.length, extra: { catchCount: mine.length, sizeM: maxSize } };
}

export function getMyLeaderboardRank(
  playerId: string,
  boardType: LeaderboardBoardType,
  opts?: { date?: string; week?: string; pondId?: string; limit?: number },
): LeaderboardMyRank {
  let entries: LeaderboardEntry[];
  if (boardType === 'daily_biggest') {
    entries = getDailyBiggestLeaderboard({ date: opts?.date, limit: opts?.limit ?? 20 });
  } else if (boardType === 'weekly_king') {
    entries = getWeeklyKingLeaderboard({ week: opts?.week, limit: opts?.limit ?? 20 });
  } else if (boardType === 'pond') {
    if (!opts?.pondId) {
      return { rank: null, value: 0 };
    }
    entries = getPondLeaderboard(opts.pondId, { week: opts?.week, limit: opts?.limit ?? 10 });
  } else {
    entries = getRareLeaderboard({ week: opts?.week, limit: opts?.limit ?? 20 });
  }

  const onBoard = entries.find((e) => e.playerId === playerId);
  if (onBoard) {
    return { rank: onBoard.rank, value: onBoard.value, entry: onBoard };
  }

  const { value, extra } = playerValueOnBoard(boardType, playerId, opts ?? {});
  const entry = value > 0 ? toEntry(0, playerId, value, extra) : undefined;
  return { rank: null, value, ...(entry ? { entry } : {}) };
}

/** Test helpers */
export function clearLeaderboardCacheForTests(): void {
  cache.clear();
  totalComputeCount = 0;
}

export function getLeaderboardComputeCountForTests(): number {
  return totalComputeCount;
}
