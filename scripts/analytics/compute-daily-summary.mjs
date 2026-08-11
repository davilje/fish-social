/**
 * 从 fishing_metrics + daily_* 表计算单日运营 summary.json
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { shanghaiDayBounds, previousDateKeys } from './date-utils.mjs';
import { maxPopulation, POND_IDS } from './pond-config.mjs';
import { resolveRulesVersion } from './resolve-rules-version.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ANALYTICS_DAILY = path.join(__dirname, '../../docs/analytics/daily');
const TARGET_DAILY_CATCH = 100;
const ECONOMY_NET_ALERT_THRESHOLD = 500;
const HUMAN_FILTER = "player_id IS NOT NULL AND player_id NOT LIKE 'bot-%'";

const KPI_IDS = [
  'kpi_daily_catch',
  'kpi_dau',
  'kpi_fishing_dau',
  'kpi_catch_per_fisher',
  'kpi_disconnect_rate',
  'kpi_abandon_rate',
  'kpi_avg_pop_ratio',
];

const QUALITY_NAMES = {
  gray: '普通',
  green: '优良',
  blue: '稀有',
  purple: '史诗',
  red: '传说',
  orange: '神话',
  gold: '至尊',
};

function round(n, d = 2) {
  if (n == null || Number.isNaN(n)) return null;
  const f = 10 ** d;
  return Math.round(n * f) / f;
}

function pct(n) {
  return n == null ? null : round(n * 100, 1);
}

function nextDateKey(dateKey) {
  const { dayEndMs } = shanghaiDayBounds(dateKey);
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Shanghai' }).format(new Date(dayEndMs));
}

function addDays(dateKey, days) {
  const { dayStartMs } = shanghaiDayBounds(dateKey);
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Shanghai' }).format(
    new Date(dayStartMs + days * 86_400_000),
  );
}

function hasDedupKey(db) {
  const cols = db.prepare("PRAGMA table_info('fishing_metrics')").all();
  return cols.some((c) => c.name === 'dedup_key');
}

function hasTable(db, name) {
  return !!db.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name=?").get(name);
}

function bounds(dateKey) {
  const { dayStartMs, dayEndMs } = shanghaiDayBounds(dateKey);
  return { dayStart: dayStartMs, dayEnd: dayEndMs };
}

/** Legacy: metrics accept events (human only). Kept for对照. */
function countMetricsAcceptHuman(db, dayStart, dayEnd, useDedup) {
  const dedup = useDedup ? 'DISTINCT COALESCE(dedup_key, id)' : '*';
  const row = db
    .prepare(
      `SELECT COUNT(${dedup}) as c FROM fishing_metrics
       WHERE event_type IN ('catch_accept', 'pending_catch_accept')
       AND created_at >= ? AND created_at < ?
       AND ${HUMAN_FILTER}`,
    )
    .get(dayStart, dayEnd);
  return row?.c ?? 0;
}

/** OPS-CATCH-1：产量 = inventory 入库（上海日） */
function countInventoryCatch(db, dayStart, dayEnd) {
  if (!hasTable(db, 'inventory')) {
    return { total: 0, human: 0, bot: 0 };
  }
  const total =
    db
      .prepare(
        `SELECT COUNT(*) as c FROM inventory
         WHERE caught_at >= ? AND caught_at < ?`,
      )
      .get(dayStart, dayEnd)?.c ?? 0;
  const human =
    db
      .prepare(
        `SELECT COUNT(*) as c FROM inventory
         WHERE caught_at >= ? AND caught_at < ?
         AND player_id IS NOT NULL AND player_id NOT LIKE 'bot-%'`,
      )
      .get(dayStart, dayEnd)?.c ?? 0;
  return { total, human, bot: total - human };
}

function countEvent(db, eventType, dayStart, dayEnd, humanOnly = false) {
  const types = Array.isArray(eventType) ? eventType : [eventType];
  const placeholders = types.map(() => '?').join(',');
  const human = humanOnly ? ` AND ${HUMAN_FILTER}` : '';
  const row = db
    .prepare(
      `SELECT COUNT(*) as c FROM fishing_metrics
       WHERE event_type IN (${placeholders})
       AND created_at >= ? AND created_at < ?${human}`,
    )
    .get(...types, dayStart, dayEnd);
  return row?.c ?? 0;
}

function distinctPlayers(db, eventType, dayStart, dayEnd) {
  const types = eventType ? (Array.isArray(eventType) ? eventType : [eventType]) : null;
  let sql = `SELECT COUNT(DISTINCT player_id) as c FROM fishing_metrics
    WHERE created_at >= ? AND created_at < ? AND ${HUMAN_FILTER}`;
  const params = [dayStart, dayEnd];
  if (types) {
    sql += ` AND event_type IN (${types.map(() => '?').join(',')})`;
    params.push(...types);
  }
  return db.prepare(sql).get(...params)?.c ?? 0;
}

function playerActiveOnDay(db, playerId, dateKey) {
  const { dayStart, dayEnd } = bounds(dateKey);
  const row = db
    .prepare(
      `SELECT 1 as ok FROM fishing_metrics
       WHERE player_id = ? AND created_at >= ? AND created_at < ? LIMIT 1`,
    )
    .get(playerId, dayStart, dayEnd);
  return !!row;
}

function completeness(db, dayStart, dayEnd) {
  const row = db
    .prepare(
      `SELECT COUNT(*) as metricCount, MIN(created_at) as firstEventAt, MAX(created_at) as lastEventAt
       FROM fishing_metrics WHERE created_at >= ? AND created_at < ?`,
    )
    .get(dayStart, dayEnd);
  return {
    metricCount: row?.metricCount ?? 0,
    firstEventAt: row?.firstEventAt ?? null,
    lastEventAt: row?.lastEventAt ?? null,
  };
}

function statusDailyCatch(v) {
  if (v == null) return null;
  if (v >= 80 && v <= 120) return 'ok';
  if ((v >= 50 && v < 80) || (v > 120 && v <= 150)) return 'warn';
  return 'bad';
}

function statusDisconnectRate(v) {
  if (v == null) return null;
  if (v < 0.2) return 'ok';
  if (v <= 0.3) return 'warn';
  return 'bad';
}

function statusPopRatio(v) {
  if (v == null) return null;
  if (v >= 85) return 'ok';
  if (v >= 70) return 'warn';
  return 'bad';
}

function makeKpi(value, prev, avg7d, status) {
  const deltaPct =
    prev != null && prev !== 0 && value != null ? round(((value - prev) / prev) * 100, 1) : null;
  return { value, prev, avg7d, deltaPct, status };
}

function readSimRef() {
  const analysisPath = path.join(__dirname, '../../docs/analytics/pond-day-simulation/analysis.json');
  if (!fs.existsSync(analysisPath)) return null;
  try {
    const analysis = JSON.parse(fs.readFileSync(analysisPath, 'utf8'));
    const row = analysis.scenarioRows?.find((r) => r.anglers === 5);
    return row?.perDayCaught ?? null;
  } catch {
    return null;
  }
}

function loadEcology(db, dateKey) {
  if (hasTable(db, 'daily_pond_ecology')) {
    const rows = db.prepare('SELECT * FROM daily_pond_ecology WHERE date_key = ?').all(dateKey);
    if (rows.length) {
      return rows.map((r) => ({
        pondId: r.pond_id,
        population: r.population,
        maxPopulation: r.max_population,
        popRatio: r.pop_ratio,
        avgSizeM: r.avg_size_m,
        byQuality: JSON.parse(r.quality_json || '{}'),
        status: statusPopRatio(r.pop_ratio),
      }));
    }
  }
  const snapPath = path.join(ANALYTICS_DAILY, dateKey, 'ecology-snapshot.json');
  if (fs.existsSync(snapPath)) {
    const snap = JSON.parse(fs.readFileSync(snapPath, 'utf8'));
    return (snap.ponds || []).map((p) => ({
      ...p,
      status: statusPopRatio(p.popRatio),
    }));
  }
  return POND_IDS.map((pondId) => ({
    pondId,
    population: 0,
    maxPopulation: maxPopulation(pondId) ?? 0,
    popRatio: null,
    avgSizeM: null,
    byQuality: {},
    status: null,
  }));
}

function economySink(db, dayStart, dayEnd) {
  const row = db
    .prepare(
      `SELECT COALESCE(SUM(CAST(JSON_EXTRACT(payload, '$.cost') AS INTEGER)), 0) as total
       FROM fishing_metrics
       WHERE event_type IN ('bait_buy', 'tackle_buy', 'tackle_repair')
       AND created_at >= ? AND created_at < ?`,
    )
    .get(dayStart, dayEnd);
  return row?.total ?? 0;
}

function economyFaucet(db, dayStart, dayEnd) {
  const row = db
    .prepare(
      `SELECT COALESCE(SUM(CAST(JSON_EXTRACT(payload, '$.amount') AS INTEGER)), 0) as total
       FROM fishing_metrics
       WHERE event_type = 'gold_earn'
       AND created_at >= ? AND created_at < ?`,
    )
    .get(dayStart, dayEnd);
  return row?.total ?? 0;
}

function economyBreakdown(db, dayStart, dayEnd) {
  const faucetBySource = db
    .prepare(
      `SELECT JSON_EXTRACT(payload, '$.source') as source,
              COALESCE(SUM(CAST(JSON_EXTRACT(payload, '$.amount') AS INTEGER)), 0) as total
       FROM fishing_metrics WHERE event_type = 'gold_earn'
       AND created_at >= ? AND created_at < ?
       GROUP BY source`,
    )
    .all(dayStart, dayEnd)
    .map((r) => ({
      source: r.source?.replace(/"/g, '') || 'unknown',
      total: r.total,
    }));

  const sinkByType = db
    .prepare(
      `SELECT event_type as type,
              COALESCE(SUM(CAST(JSON_EXTRACT(payload, '$.cost') AS INTEGER)), 0) as total
       FROM fishing_metrics
       WHERE event_type IN ('bait_buy', 'tackle_buy', 'tackle_repair')
       AND created_at >= ? AND created_at < ?
       GROUP BY event_type`,
    )
    .all(dayStart, dayEnd);

  return { faucetBySource, sinkByType };
}

function hasFaucetEvents(db) {
  const row = db.prepare("SELECT 1 FROM fishing_metrics WHERE event_type = 'gold_earn' LIMIT 1").get();
  return !!row;
}

function computeStability(db, dayStart, dayEnd) {
  const joinAttempts = countEvent(db, 'join_pond_attempt', dayStart, dayEnd);
  const joinFails = countEvent(db, 'join_pond_fail', dayStart, dayEnd);
  const joinFailRate = joinAttempts > 0 ? round(joinFails / joinAttempts, 4) : null;

  const leaveRows = db
    .prepare(
      `SELECT JSON_EXTRACT(payload, '$.reason') as reason, COUNT(*) as c
       FROM fishing_metrics WHERE event_type = 'leave_pond'
       AND created_at >= ? AND created_at < ?
       GROUP BY reason`,
    )
    .all(dayStart, dayEnd)
    .map((r) => ({
      reason: r.reason?.replace(/"/g, '') || 'unknown',
      count: r.c,
    }));

  return {
    disconnectCount: countEvent(db, ['disconnect', 'socket_disconnect'], dayStart, dayEnd),
    reconnectCount: countEvent(db, 'reconnect', dayStart, dayEnd),
    disconnectTimeoutCount: countEvent(db, 'disconnect_timeout', dayStart, dayEnd),
    joinAttempts,
    joinSuccessCount: countEvent(db, 'join_pond_success', dayStart, dayEnd),
    joinFails,
    joinFailRate,
    leaveByReason: leaveRows,
    phaseInvalidCount: countEvent(db, 'phase_transition_invalid', dayStart, dayEnd),
  };
}

function computeRetention(db, dateKey) {
  if (!hasTable(db, 'players')) {
    return { cohortSize: 0, d1Rate: null, d7Rate: null, note: 'players 表不存在' };
  }
  const { dayStart, dayEnd } = bounds(dateKey);
  const cohort = db
    .prepare(
      `SELECT player_id FROM players
       WHERE created_at >= ? AND created_at < ?
       AND player_id NOT LIKE 'bot-%'`,
    )
    .all(dayStart, dayEnd);

  if (!cohort.length) {
    return { cohortSize: 0, d1Rate: null, d7Rate: null, note: '当日无新增玩家' };
  }

  const d1Key = nextDateKey(dateKey);
  let d1Return = 0;
  for (const { player_id } of cohort) {
    if (playerActiveOnDay(db, player_id, d1Key)) d1Return++;
  }
  const d1Rate = round((d1Return / cohort.length) * 100, 1);

  const d7Key = addDays(dateKey, 7);
  const todayKey = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Shanghai' }).format(new Date());
  let d7Rate = null;
  let d7Note = null;
  if (d7Key > todayKey) {
    d7Note = 'D7 尚未到期';
  } else {
    let d7Return = 0;
    for (const { player_id } of cohort) {
      if (playerActiveOnDay(db, player_id, d7Key)) d7Return++;
    }
    d7Rate = round((d7Return / cohort.length) * 100, 1);
  }

  return { cohortSize: cohort.length, d1Rate, d7Rate, d7Note };
}

function computeSessionStats(db, dateKey, fishingDau, fishingStarts) {
  const rows = db.prepare('SELECT fishing_ms FROM daily_player_stats WHERE date_key = ?').all(dateKey);
  const msList = rows.map((r) => r.fishing_ms).filter((ms) => ms > 0);
  const sorted = [...msList].sort((a, b) => a - b);
  const avgMs = msList.length ? round(msList.reduce((a, b) => a + b, 0) / msList.length, 0) : null;
  const p50 = sorted.length ? sorted[Math.floor(sorted.length * 0.5)] : null;
  const p90 = sorted.length ? sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * 0.9))] : null;
  const sessionsPerFisher = fishingDau > 0 ? round(fishingStarts / fishingDau, 2) : null;
  return { avgFishingMs: avgMs, p50FishingMs: p50, p90FishingMs: p90, sessionsPerFisher };
}

function loadRulesHistory(dateKey, currentRulesVersion, currentCatch) {
  const keys = previousDateKeys(dateKey, 7);
  const rows = [];
  for (const k of keys) {
    const summaryPath = path.join(ANALYTICS_DAILY, k, 'summary.json');
    if (k === dateKey) {
      rows.push({ dateKey: k, rulesVersion: currentRulesVersion, dailyCatch: currentCatch });
      continue;
    }
    if (fs.existsSync(summaryPath)) {
      try {
        const s = JSON.parse(fs.readFileSync(summaryPath, 'utf8'));
        rows.push({
          dateKey: k,
          rulesVersion: s.meta?.rulesVersion ?? '—',
          dailyCatch: s.kpis?.kpi_daily_catch?.value ?? null,
        });
      } catch {
        /* skip */
      }
    }
  }
  return rows;
}

function aggregateDay(db, dateKey, useDedup) {
  const { dayStart, dayEnd } = bounds(dateKey);
  const inv = countInventoryCatch(db, dayStart, dayEnd);
  const metricsAcceptHuman = countMetricsAcceptHuman(db, dayStart, dayEnd, useDedup);
  const dailyCatch = inv.total;
  const dailyCatchHuman = inv.human;
  const dailyCatchBot = inv.bot;
  const dau = distinctPlayers(db, null, dayStart, dayEnd);
  const fishingDau = distinctPlayers(db, 'fishing_start', dayStart, dayEnd);
  const fishingStarts = countEvent(db, 'fishing_start', dayStart, dayEnd, true);
  const catchPerFisher = fishingDau > 0 ? round(dailyCatchHuman / fishingDau, 2) : null;

  const disconnects = countEvent(db, ['disconnect', 'socket_disconnect'], dayStart, dayEnd);
  const joins = countEvent(db, 'join_pond_success', dayStart, dayEnd);
  const disconnectRate = joins > 0 ? round(disconnects / joins, 4) : null;

  const abandons = countEvent(db, 'abandon_fishing', dayStart, dayEnd, true);
  const abandonRate = fishingStarts > 0 ? round(abandons / fishingStarts, 4) : null;

  const ecologyPonds = loadEcology(db, dateKey);
  const popRatios = ecologyPonds.map((p) => p.popRatio).filter((v) => v != null);
  const avgPopRatio = popRatios.length ? round(popRatios.reduce((a, b) => a + b, 0) / popRatios.length, 1) : null;

  const pondRows = db.prepare('SELECT * FROM daily_pond_stats WHERE date_key = ?').all(dateKey);

  return {
    dailyCatch,
    dailyCatchHuman,
    dailyCatchBot,
    metricsAcceptHuman,
    dau,
    fishingDau,
    fishingStarts,
    catchPerFisher,
    disconnects,
    disconnectRate,
    abandonRate,
    avgPopRatio,
    pondRows,
    ecologyPonds,
    faucetTotal: economyFaucet(db, dayStart, dayEnd),
    sinkTotal: economySink(db, dayStart, dayEnd),
  };
}

function hasInventoryPondId(db) {
  if (!hasTable(db, 'inventory')) return false;
  return db.prepare(`PRAGMA table_info(inventory)`).all().some((c) => c.name === 'pond_id');
}

/** OPS-CATCH-1：品质分布 = 背包入库 */
function qualityDistribution(db, dayStart, dayEnd) {
  if (!hasTable(db, 'inventory')) return [];
  const rows = db
    .prepare(
      `SELECT quality,
              COUNT(*) AS c,
              SUM(CASE WHEN player_id NOT LIKE 'bot-%' THEN 1 ELSE 0 END) AS human,
              SUM(CASE WHEN player_id LIKE 'bot-%' THEN 1 ELSE 0 END) AS bot
         FROM inventory
        WHERE caught_at >= ? AND caught_at < ?
        GROUP BY quality
        ORDER BY c DESC`,
    )
    .all(dayStart, dayEnd);
  return rows.map((r) => ({
    quality: r.quality || 'unknown',
    label: QUALITY_NAMES[r.quality] || r.quality || '未知',
    count: r.c,
    human: r.human ?? 0,
    bot: r.bot ?? 0,
  }));
}

/**
 * 当日 metrics 按玩家时间线（用于无 inventory.pond_id 的历史行）。
 * @returns {Map<string, Array<{ at: number, pondId: string }>>}
 */
function playerPondTimeline(db, dayStart, dayEnd) {
  const map = new Map();
  if (!hasTable(db, 'fishing_metrics')) return map;
  const rows = db
    .prepare(
      `SELECT player_id AS playerId, pond_id AS pondId, created_at AS at
         FROM fishing_metrics
        WHERE created_at >= ? AND created_at < ?
          AND player_id IS NOT NULL AND player_id != ''
          AND pond_id IS NOT NULL AND pond_id != ''
        ORDER BY created_at ASC`,
    )
    .all(dayStart, dayEnd);
  for (const r of rows) {
    if (!map.has(r.playerId)) map.set(r.playerId, []);
    map.get(r.playerId).push({ at: r.at, pondId: r.pondId });
  }
  return map;
}

/** 与 caught_at 时间最近的塘；无则 unknown */
function resolvePondFromTimeline(timeline, caughtAt) {
  if (!timeline?.length) return 'unknown';
  let best = timeline[0];
  let bestDelta = Math.abs(timeline[0].at - caughtAt);
  for (let i = 1; i < timeline.length; i++) {
    const d = Math.abs(timeline[i].at - caughtAt);
    if (d < bestDelta) {
      best = timeline[i];
      bestDelta = d;
    }
  }
  return best.pondId || 'unknown';
}

/**
 * 背包分塘：优先 inventory.pond_id；否则按 caught_at 对齐当日 metrics 时间线。
 * @returns {Map<string, { total: number, human: number, bot: number }>}
 */
function inventoryCatchByPond(db, dayStart, dayEnd) {
  const out = new Map();
  const bump = (pondId, isBot) => {
    const key = pondId || 'unknown';
    if (!out.has(key)) out.set(key, { total: 0, human: 0, bot: 0 });
    const row = out.get(key);
    row.total += 1;
    if (isBot) row.bot += 1;
    else row.human += 1;
  };

  if (!hasTable(db, 'inventory')) return out;

  const timeline = playerPondTimeline(db, dayStart, dayEnd);
  const hasPondCol = hasInventoryPondId(db);
  const rows = hasPondCol
    ? db
        .prepare(
          `SELECT player_id AS playerId, caught_at AS caughtAt, pond_id AS pondId
             FROM inventory WHERE caught_at >= ? AND caught_at < ?`,
        )
        .all(dayStart, dayEnd)
    : db
        .prepare(
          `SELECT player_id AS playerId, caught_at AS caughtAt, NULL AS pondId
             FROM inventory WHERE caught_at >= ? AND caught_at < ?`,
        )
        .all(dayStart, dayEnd);

  for (const r of rows) {
    const stored = r.pondId && String(r.pondId).trim() ? String(r.pondId) : null;
    const pondId = stored || resolvePondFromTimeline(timeline.get(r.playerId), r.caughtAt);
    bump(pondId, String(r.playerId || '').startsWith('bot-'));
  }
  return out;
}

function checkEconomyImbalance(db, dateKey) {
  const keys = [addDays(dateKey, -2), addDays(dateKey, -1), dateKey];
  const days = [];
  for (const k of keys) {
    const { dayStart, dayEnd } = bounds(k);
    const faucet = economyFaucet(db, dayStart, dayEnd);
    const sink = economySink(db, dayStart, dayEnd);
    days.push({ dateKey: k, faucet, sink, net: faucet - sink });
  }
  const allHeavyOutflow = days.every(
    (d) => d.net < 0 && Math.abs(d.net) >= ECONOMY_NET_ALERT_THRESHOLD,
  );
  const allFaucetLow = days.every((d) => d.sink > 0 && d.faucet < d.sink * 0.2);
  return { triggered: allHeavyOutflow || allFaucetLow, days };
}

function buildAlerts(metrics, sections, comp) {
  const alerts = [];
  const { dailyCatchHuman, dau, disconnectRate } = metrics;
  // 告警对照真人产量（总量含 bot 后会远超旧阈值）
  const catchForAlert = dailyCatchHuman ?? 0;

  if (comp.metricCount === 0) {
    alerts.push({ id: 'alert_no_data', level: 'bad', message: '当日无任何 metrics 事件' });
    return alerts;
  }

  if (catchForAlert > 120) {
    alerts.push({ id: 'alert_catch_high', level: 'warn', message: `真人日钓 ${catchForAlert} 条，超过目标上限 120` });
  }
  if (catchForAlert < 50 && dau > 5) {
    alerts.push({ id: 'alert_catch_low', level: 'bad', message: `真人日钓仅 ${catchForAlert} 条且 DAU=${dau}` });
  }

  for (const pond of sections.ecology.ponds) {
    if (pond.popRatio != null && pond.popRatio < 70) {
      alerts.push({
        id: 'alert_pop_low',
        level: 'bad',
        message: `${pond.pondId} 人口率 ${pond.popRatio}% < 70%`,
      });
    }
  }

  if (disconnectRate != null && disconnectRate > 0.3) {
    alerts.push({
      id: 'alert_disconnect',
      level: 'bad',
      message: `断线率 ${pct(disconnectRate)}% 超过 30%`,
    });
  }

  const pendingRate = sections.catch.pendingExpiredRate;
  if (pendingRate != null && pendingRate > 0.1) {
    alerts.push({
      id: 'alert_pending_expired',
      level: 'warn',
      message: `pending 超时率 ${pct(pendingRate)}% 超过 10%`,
    });
  }

  if (sections.economy.imbalance?.triggered) {
    alerts.push({
      id: 'alert_economy_imbalance',
      level: 'warn',
      message: '连续 3 日金币净流出超阈值或 faucet 远低于 sink',
    });
  }

  return alerts;
}

/** @param {import('better-sqlite3').Database} db */
export function computeDailySummary(db, dateKey) {
  const useDedup = hasDedupKey(db);
  const { dayStart, dayEnd } = bounds(dateKey);
  const comp = completeness(db, dayStart, dayEnd);
  const rulesVersion = resolveRulesVersion(db);

  const day = aggregateDay(db, dateKey, useDedup);
  const keys7 = previousDateKeys(dateKey, 7);
  const prevKey = keys7[keys7.length - 2];
  const prev = prevKey ? aggregateDay(db, prevKey, useDedup) : null;

  const history = keys7.map((k) => aggregateDay(db, k, useDedup));
  const avg = (fn) => {
    const vals = history.map(fn).filter((v) => v != null && !Number.isNaN(v));
    return vals.length ? round(vals.reduce((a, b) => a + b, 0) / vals.length, vals[0] % 1 ? 2 : 0) : null;
  };

  const hookCount = countEvent(db, 'bite_hook', dayStart, dayEnd);
  const escapes = countEvent(db, 'escape', dayStart, dayEnd, true);
  const landed = day.dailyCatch;
  // D-L2-15: escape / bite_hook (prefer); fallback escape/(escape+landed) when no hooks
  const escapeRate =
    hookCount > 0
      ? round(escapes / hookCount, 4)
      : landed + escapes > 0
        ? round(escapes / (landed + escapes), 4)
        : null;
  const catchRate = hookCount > 0 ? round(landed / hookCount, 4) : null;
  // Deprecated tick hit-rate (D-L2-15); field kept null for backward HTML consumers
  const biteHitRate = null;
  let totalFishingMs = 0;
  try {
    const row = db.prepare(
      `SELECT COALESCE(SUM(fishing_ms), 0) AS ms FROM daily_player_stats WHERE date_key = ?`,
    ).get(dateKey);
    totalFishingMs = row?.ms ?? 0;
  } catch {
    totalFishingMs = 0;
  }
  const hookFrequencyPerHour =
    totalFishingMs > 0 ? round(hookCount / (totalFishingMs / 3_600_000), 2) : null;

  const pendingCreated = countEvent(db, 'pending_catch_created', dayStart, dayEnd);
  const pendingExpired = countEvent(db, 'pending_catch_expired', dayStart, dayEnd);
  const pendingExpiredRate = pendingCreated > 0 ? round(pendingExpired / pendingCreated, 4) : null;

  const invPond = inventoryCatchByPond(db, dayStart, dayEnd);
  const pondIds = [
    ...POND_IDS,
    ...[...invPond.keys()].filter((id) => id !== 'unknown' && !POND_IDS.includes(id)),
    ...(invPond.has('unknown') ? ['unknown'] : []),
  ];
  const byPond = pondIds.map((pondId) => {
    const r = day.pondRows.find((row) => row.pond_id === pondId);
    const inv = invPond.get(pondId) || { total: 0, human: 0, bot: 0 };
    const max = maxPopulation(pondId) ?? null;
    const eco = day.ecologyPonds.find((p) => p.pondId === pondId);
    const popRatio =
      eco?.popRatio ??
      (max && r?.avg_population != null ? round((r.avg_population / max) * 100, 1) : null);
    return {
      pondId,
      catches: inv.total,
      catchesHuman: inv.human,
      catchesBot: inv.bot,
      catchSource: 'inventory',
      hookCount: r?.hook_count ?? r?.bite_tick_hit ?? 0,
      escapeCount: r?.escape_count ?? 0,
      biteTickHit: r?.hook_count ?? r?.bite_tick_hit ?? 0,
      biteTickMiss: r?.bite_tick_miss ?? 0,
      disconnects: r?.disconnect_count ?? 0,
      avgPopulation: r?.avg_population != null ? round(r.avg_population, 1) : null,
      maxPopulation: max,
      popRatio,
    };
  });

  const byQuality = qualityDistribution(db, dayStart, dayEnd);
  const faucetAvailable = hasFaucetEvents(db);
  const faucetTotal = faucetAvailable ? day.faucetTotal : null;
  const sinkTotal = day.sinkTotal;
  const economyNet = faucetTotal != null ? faucetTotal - sinkTotal : null;
  const economyBreakdownData = economyBreakdown(db, dayStart, dayEnd);
  const economyImbalance = faucetAvailable ? checkEconomyImbalance(db, dateKey) : { triggered: false, days: [] };

  const stability = computeStability(db, dayStart, dayEnd);
  const retention = computeRetention(db, dateKey);
  const session = computeSessionStats(db, dateKey, day.fishingDau, day.fishingStarts);

  const simRef = readSimRef();
  // 目标对照主值=背包总量；旁注真人数（设计目标 100 偏真人场景）
  const actualTotal = day.dailyCatch;
  const actualHuman = day.dailyCatchHuman;
  const deviationPct =
    simRef != null && simRef !== 0 ? round(((actualTotal - simRef) / simRef) * 100, 1) : null;

  const sections = {
    players: {
      dau: day.dau,
      fishingDau: day.fishingDau,
      sessionsPerFisher: session.sessionsPerFisher,
      avgFishingMs: session.avgFishingMs,
      p50FishingMs: session.p50FishingMs,
      p90FishingMs: session.p90FishingMs,
    },
    retention,
    catch: {
      source: 'inventory',
      total: day.dailyCatch,
      human: day.dailyCatchHuman,
      bot: day.dailyCatchBot,
      metricsAcceptHuman: day.metricsAcceptHuman,
      byPond,
      byQuality,
      biteHitRate,
      hookCount,
      hookFrequencyPerHour,
      catchRate,
      escapeRate,
      pendingExpiredRate,
      totalCatches: day.dailyCatch,
      totalDisconnects: day.disconnects,
      metricNote:
        '产量口径 OPS-CATCH-1：日钓获/品质/分塘均基于 inventory 入库（含 bot）。无 pond_id 历史行按 caught_at 对齐当日 metrics 塘时间线。上钩=bite_hook。',
    },
    stability,
    economy: {
      faucetTotal,
      sinkTotal,
      net: economyNet,
      faucetAvailable,
      breakdown: economyBreakdownData,
      imbalance: economyImbalance,
      note: faucetAvailable ? null : 'faucet 埋点缺失（gold_earn），净变化不可算',
    },
    ecology: {
      ponds: day.ecologyPonds,
      snapshotNote: '基于日批时 pond_fish 快照',
    },
    targetCompare: {
      target: TARGET_DAILY_CATCH,
      simRef,
      simScenario: '5 人/塘',
      actual: actualTotal,
      actualHuman,
      actualTotal,
      deviationPct,
      targetDeviationPct: round(((actualTotal - TARGET_DAILY_CATCH) / TARGET_DAILY_CATCH) * 100, 1),
      humanTargetDeviationPct: round(
        ((actualHuman - TARGET_DAILY_CATCH) / TARGET_DAILY_CATCH) * 100,
        1,
      ),
      note: '昨日实测=背包总量；真人旁注。设计目标 100 原按真人场景，对照总量会系统性偏大。',
    },
    rulesHistory: loadRulesHistory(dateKey, rulesVersion, day.dailyCatch),
  };

  const catchKpi = makeKpi(
    day.dailyCatch,
    prev?.dailyCatch ?? null,
    avg((d) => d.dailyCatch),
    statusDailyCatch(day.dailyCatchHuman),
  );
  catchKpi.note = `背包入库 · 真人 ${day.dailyCatchHuman} · 机器人 ${day.dailyCatchBot}`;

  const kpis = {
    kpi_daily_catch: catchKpi,
    kpi_daily_catch_human: makeKpi(
      day.dailyCatchHuman,
      prev?.dailyCatchHuman ?? null,
      avg((d) => d.dailyCatchHuman),
      null,
    ),
    kpi_daily_catch_bot: makeKpi(
      day.dailyCatchBot,
      prev?.dailyCatchBot ?? null,
      avg((d) => d.dailyCatchBot),
      null,
    ),
    kpi_dau: makeKpi(day.dau, prev?.dau ?? null, avg((d) => d.dau), null),
    kpi_fishing_dau: makeKpi(day.fishingDau, prev?.fishingDau ?? null, avg((d) => d.fishingDau), null),
    kpi_catch_per_fisher: makeKpi(day.catchPerFisher, prev?.catchPerFisher ?? null, avg((d) => d.catchPerFisher), null),
    kpi_disconnect_rate: makeKpi(
      day.disconnectRate != null ? pct(day.disconnectRate) : null,
      prev?.disconnectRate != null ? pct(prev.disconnectRate) : null,
      avg((d) => (d.disconnectRate != null ? pct(d.disconnectRate) : null)),
      statusDisconnectRate(day.disconnectRate),
    ),
    kpi_abandon_rate: makeKpi(
      day.abandonRate != null ? pct(day.abandonRate) : null,
      prev?.abandonRate != null ? pct(prev.abandonRate) : null,
      avg((d) => (d.abandonRate != null ? pct(d.abandonRate) : null)),
      null,
    ),
    kpi_avg_pop_ratio: makeKpi(day.avgPopRatio, prev?.avgPopRatio ?? null, avg((d) => d.avgPopRatio), statusPopRatio(day.avgPopRatio)),
  };

  if (stability.joinSuccessCount === 0) {
    kpis.kpi_disconnect_rate.value = null;
    kpis.kpi_disconnect_rate.note = '（无 join_pond_success 分母，断线率展示 —）';
  }
  if (day.fishingStarts === 0) {
    kpis.kpi_abandon_rate.value = null;
    kpis.kpi_abandon_rate.note = '（无 fishing_start 分母，弃钓率展示 —）';
  }

  const alerts = buildAlerts(day, sections, comp);

  return {
    meta: {
      dateKey,
      generatedAt: new Date().toISOString(),
      rulesVersion,
      timezone: 'Asia/Shanghai',
      completeness: comp,
    },
    kpis,
    sections,
    alerts,
    kpiIds: KPI_IDS,
  };
}

export { KPI_IDS, TARGET_DAILY_CATCH, ECONOMY_NET_ALERT_THRESHOLD };
