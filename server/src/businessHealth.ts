import { db } from './db.js';

export interface PondHealthDay {
  pondId: string;
  catchCount: number;
  hookCount: number;
  escapeCount: number;
  /** @deprecated D-L2-15: alias of hookCount for old admin UI */
  biteTickHit: number;
  /** @deprecated D-L2-15: missTicks sum or 0 */
  biteTickMiss: number;
  disconnectCount: number;
  avgPopulation: number | null;
  /** D-L2-15: catch / hook (获鱼率); 0 when no hooks */
  biteHitRate: number;
  escapeRate: number;
  disconnectRate: number;
}

export interface BusinessHealthDay {
  dateKey: string;
  totalCatch: number;
  totalDisconnect: number;
  hookCount: number;
  escapeCount: number;
  biteTickHit: number;
  biteTickMiss: number;
  biteHitRate: number;
  escapeRate: number;
  disconnectRate: number;
  activePlayers: number;
  ponds: PondHealthDay[];
}

export interface BusinessHealthTrend {
  days: number;
  fromDate: string;
  toDate: string;
  /** OPS-CATCH-1.1：产量=inventory（含机器人） */
  catchSource: 'inventory';
  catchNote: string;
  daily: BusinessHealthDay[];
  totals: {
    catchCount: number;
    disconnectCount: number;
    hookCount: number;
    escapeCount: number;
    biteTickHit: number;
    biteTickMiss: number;
    activePlayers: number;
  };
}

/** Asia/Shanghai 自然日 dateKey，与运营日报一致 */
function shanghaiDateKeyOffset(daysAgo: number, nowMs: number = Date.now()): string {
  const todayKey = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Shanghai' }).format(
    new Date(nowMs),
  );
  const dayStart = Date.parse(`${todayKey}T00:00:00+08:00`);
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Shanghai' }).format(
    new Date(dayStart - daysAgo * 86_400_000),
  );
}

function safeRate(numerator: number, denominator: number): number {
  if (denominator <= 0) return 0;
  return Math.round((numerator / denominator) * 1000) / 1000;
}

export function getBusinessHealthTrend(days = 7): BusinessHealthTrend {
  const windowDays = Math.min(30, Math.max(1, days));
  const fromDate = shanghaiDateKeyOffset(windowDays - 1);
  const toDate = shanghaiDateKeyOffset(0);

  const pondRows = db
    .prepare(
      `
    SELECT date_key, pond_id, catch_count,
      COALESCE(hook_count, bite_tick_hit, 0) AS hook_count,
      COALESCE(escape_count, 0) AS escape_count,
      bite_tick_hit, bite_tick_miss, disconnect_count, avg_population
    FROM daily_pond_stats
    WHERE date_key >= ? AND date_key <= ?
    ORDER BY date_key ASC, pond_id ASC
  `,
    )
    .all(fromDate, toDate) as Array<{
    date_key: string;
    pond_id: string;
    catch_count: number;
    hook_count: number;
    escape_count: number;
    bite_tick_hit: number;
    bite_tick_miss: number;
    disconnect_count: number;
    avg_population: number | null;
  }>;

  // 活跃人数排除 bot；总钓获含 bot（与 inventory 口径一致）
  const playerRows = db
    .prepare(
      `
    SELECT date_key,
      COUNT(DISTINCT CASE WHEN player_id NOT LIKE 'bot-%' THEN player_id END) AS active_players,
      COALESCE(SUM(catch_count), 0) AS total_catch,
      COALESCE(SUM(CASE WHEN player_id NOT LIKE 'bot-%' THEN disconnect_count ELSE 0 END), 0) AS total_disconnect
    FROM daily_player_stats
    WHERE date_key >= ? AND date_key <= ?
    GROUP BY date_key
    ORDER BY date_key ASC
  `,
    )
    .all(fromDate, toDate) as Array<{
    date_key: string;
    active_players: number;
    total_catch: number;
    total_disconnect: number;
  }>;

  const playerByDate = new Map(playerRows.map((r) => [r.date_key, r]));
  const pondsByDate = new Map<string, PondHealthDay[]>();

  for (const row of pondRows) {
    const hookCount = Number(row.hook_count) || 0;
    const escapeCount = Number(row.escape_count) || 0;
    const catchCount = Number(row.catch_count) || 0;
    const activityTotal = catchCount + row.disconnect_count + hookCount + escapeCount;
    const pond: PondHealthDay = {
      pondId: row.pond_id,
      catchCount,
      hookCount,
      escapeCount,
      biteTickHit: hookCount,
      biteTickMiss: row.bite_tick_miss,
      disconnectCount: row.disconnect_count,
      avgPopulation: row.avg_population,
      biteHitRate: safeRate(catchCount, hookCount),
      escapeRate: safeRate(escapeCount, hookCount),
      disconnectRate: safeRate(row.disconnect_count, activityTotal),
    };
    const list = pondsByDate.get(row.date_key) ?? [];
    list.push(pond);
    pondsByDate.set(row.date_key, list);
  }

  const dateKeys: string[] = [];
  for (let i = windowDays - 1; i >= 0; i -= 1) {
    dateKeys.push(shanghaiDateKeyOffset(i));
  }

  const daily: BusinessHealthDay[] = dateKeys.map((dateKey) => {
    const ponds = pondsByDate.get(dateKey) ?? [];
    const hookCount = ponds.reduce((sum, p) => sum + p.hookCount, 0);
    const escapeCount = ponds.reduce((sum, p) => sum + p.escapeCount, 0);
    const biteTickMiss = ponds.reduce((sum, p) => sum + p.biteTickMiss, 0);
    const pondCatch = ponds.reduce((sum, p) => sum + p.catchCount, 0);
    const pondDisconnect = ponds.reduce((sum, p) => sum + p.disconnectCount, 0);
    const player = playerByDate.get(dateKey);
    // 优先塘合计（与分塘一致）；无塘行时回退玩家合计
    const totalCatch = pondCatch > 0 || ponds.length > 0 ? pondCatch : (player?.total_catch ?? 0);
    const totalDisconnect =
      pondDisconnect > 0 || ponds.length > 0
        ? pondDisconnect
        : (player?.total_disconnect ?? 0);
    const activityTotal = totalCatch + totalDisconnect + hookCount + escapeCount;
    return {
      dateKey,
      totalCatch,
      totalDisconnect,
      hookCount,
      escapeCount,
      biteTickHit: hookCount,
      biteTickMiss,
      biteHitRate: safeRate(totalCatch, hookCount),
      escapeRate: safeRate(escapeCount, hookCount),
      disconnectRate: safeRate(totalDisconnect, activityTotal),
      activePlayers: player?.active_players ?? 0,
      ponds,
    };
  });

  const totals = daily.reduce(
    (acc, day) => ({
      catchCount: acc.catchCount + day.totalCatch,
      disconnectCount: acc.disconnectCount + day.totalDisconnect,
      hookCount: acc.hookCount + day.hookCount,
      escapeCount: acc.escapeCount + day.escapeCount,
      biteTickHit: acc.biteTickHit + day.biteTickHit,
      biteTickMiss: acc.biteTickMiss + day.biteTickMiss,
      activePlayers: acc.activePlayers + day.activePlayers,
    }),
    {
      catchCount: 0,
      disconnectCount: 0,
      hookCount: 0,
      escapeCount: 0,
      biteTickHit: 0,
      biteTickMiss: 0,
      activePlayers: 0,
    },
  );

  return {
    days: windowDays,
    fromDate,
    toDate,
    catchSource: 'inventory',
    catchNote: '产量=背包入库（含机器人）；活跃人数不含 bot',
    daily,
    totals,
  };
}
