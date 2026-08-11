/**
 * 日聚合：daily_player_stats / daily_pond_stats
 * OPS-CATCH-1.1：catch_count ← inventory（含 bot）；hook/escape/disconnect ← metrics
 */
import Database from 'better-sqlite3';
import path from 'path';
import { fileURLToPath } from 'url';
import { shanghaiDayBounds, parseDateArg } from './analytics/date-utils.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(__dirname, '../data');
const dbPath = process.env.DB_PATH ?? path.join(dataDir, 'fish-social.db');

const dateKey = parseDateArg();
const { dayStart, dayEnd } = (() => {
  const b = shanghaiDayBounds(dateKey);
  return { dayStart: b.dayStartMs, dayEnd: b.dayEndMs };
})();

const db = new Database(dbPath);
db.pragma('journal_mode = WAL');

function hasTable(name) {
  return !!db
    .prepare(`SELECT 1 AS ok FROM sqlite_master WHERE type='table' AND name=?`)
    .get(name);
}

function hasInventoryPondId() {
  if (!hasTable('inventory')) return false;
  return db.prepare(`PRAGMA table_info(inventory)`).all().some((c) => c.name === 'pond_id');
}

/** 当日 metrics 塘时间线（无 inventory.pond_id 时对齐） */
function playerPondTimeline(dayStartMs, dayEndMs) {
  const map = new Map();
  if (!hasTable('fishing_metrics')) return map;
  const rows = db
    .prepare(
      `SELECT player_id AS playerId, pond_id AS pondId, created_at AS at
         FROM fishing_metrics
        WHERE created_at >= ? AND created_at < ?
          AND player_id IS NOT NULL AND player_id != ''
          AND pond_id IS NOT NULL AND pond_id != ''
        ORDER BY created_at ASC`,
    )
    .all(dayStartMs, dayEndMs);
  for (const r of rows) {
    if (!map.has(r.playerId)) map.set(r.playerId, []);
    map.get(r.playerId).push({ at: r.at, pondId: r.pondId });
  }
  return map;
}

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

/** @returns {Map<string, number>} pondId → catch count */
function inventoryCatchByPond(dayStartMs, dayEndMs) {
  const out = new Map();
  if (!hasTable('inventory')) return out;
  const timeline = playerPondTimeline(dayStartMs, dayEndMs);
  const hasPondCol = hasInventoryPondId();
  const rows = hasPondCol
    ? db
        .prepare(
          `SELECT player_id AS playerId, caught_at AS caughtAt, pond_id AS pondId
             FROM inventory WHERE caught_at >= ? AND caught_at < ?`,
        )
        .all(dayStartMs, dayEndMs)
    : db
        .prepare(
          `SELECT player_id AS playerId, caught_at AS caughtAt, NULL AS pondId
             FROM inventory WHERE caught_at >= ? AND caught_at < ?`,
        )
        .all(dayStartMs, dayEndMs);

  for (const r of rows) {
    const stored = r.pondId && String(r.pondId).trim() ? String(r.pondId) : null;
    const pondId = stored || resolvePondFromTimeline(timeline.get(r.playerId), r.caughtAt);
    out.set(pondId, (out.get(pondId) || 0) + 1);
  }
  return out;
}

/** @returns {Map<string, number>} playerId → catch count */
function inventoryCatchByPlayer(dayStartMs, dayEndMs) {
  const out = new Map();
  if (!hasTable('inventory')) return out;
  const rows = db
    .prepare(
      `SELECT player_id AS playerId, COUNT(*) AS c
         FROM inventory
        WHERE caught_at >= ? AND caught_at < ?
          AND player_id IS NOT NULL AND player_id != ''
        GROUP BY player_id`,
    )
    .all(dayStartMs, dayEndMs);
  for (const r of rows) out.set(r.playerId, Number(r.c) || 0);
  return out;
}

/** Ensure D-L2-15 columns exist (idempotent; server migration also adds them). */
const pondCols = db.prepare("PRAGMA table_info('daily_pond_stats')").all();
const pondColNames = new Set(pondCols.map((c) => c.name));
if (!pondColNames.has('hook_count')) {
  db.exec('ALTER TABLE daily_pond_stats ADD COLUMN hook_count INTEGER NOT NULL DEFAULT 0');
}
if (!pondColNames.has('escape_count')) {
  db.exec('ALTER TABLE daily_pond_stats ADD COLUMN escape_count INTEGER NOT NULL DEFAULT 0');
}

const invByPlayer = inventoryCatchByPlayer(dayStart, dayEnd);

// metrics：escape / disconnect / fishing_ms（含有事件的玩家；bot 也可有）
const metricsByPlayer = new Map();
if (hasTable('fishing_metrics')) {
  const rows = db
    .prepare(
      `SELECT player_id AS playerId,
          SUM(CASE WHEN event_type = 'escape' THEN 1 ELSE 0 END) AS escapeCount,
          SUM(CASE WHEN event_type IN ('disconnect', 'socket_disconnect') THEN 1 ELSE 0 END) AS disconnectCount,
          COALESCE(SUM(CAST(JSON_EXTRACT(payload, '$.fishingMs') AS INTEGER)), 0) AS fishingMs
         FROM fishing_metrics
        WHERE created_at >= ? AND created_at < ?
          AND player_id IS NOT NULL AND player_id != ''
        GROUP BY player_id`,
    )
    .all(dayStart, dayEnd);
  for (const r of rows) {
    metricsByPlayer.set(r.playerId, {
      escapeCount: Number(r.escapeCount) || 0,
      disconnectCount: Number(r.disconnectCount) || 0,
      fishingMs: Number(r.fishingMs) || 0,
    });
  }
}

const playerIds = new Set([...invByPlayer.keys(), ...metricsByPlayer.keys()]);
db.prepare('DELETE FROM daily_player_stats WHERE date_key = ?').run(dateKey);
const insertPlayer = db.prepare(`
  INSERT INTO daily_player_stats (date_key, player_id, catch_count, escape_count, disconnect_count, fishing_ms)
  VALUES (?, ?, ?, ?, ?, ?)
`);
const insertPlayerTx = db.transaction(() => {
  for (const playerId of playerIds) {
    const inv = invByPlayer.get(playerId) ?? 0;
    const m = metricsByPlayer.get(playerId) ?? {
      escapeCount: 0,
      disconnectCount: 0,
      fishingMs: 0,
    };
    insertPlayer.run(
      dateKey,
      playerId,
      inv,
      m.escapeCount,
      m.disconnectCount,
      m.fishingMs,
    );
  }
});
insertPlayerTx();

// pond：hook/escape/disconnect from metrics；catch_count from inventory
const invByPond = inventoryCatchByPond(dayStart, dayEnd);
const metricsByPond = new Map();
if (hasTable('fishing_metrics')) {
  const rows = db
    .prepare(
      `SELECT pond_id AS pondId,
          SUM(CASE WHEN event_type = 'bite_hook' THEN 1 ELSE 0 END) AS hookCount,
          COALESCE(SUM(
            CASE WHEN event_type = 'bite_hook'
              THEN CAST(JSON_EXTRACT(payload, '$.missTicksSinceLastHook') AS INTEGER)
              ELSE 0 END
          ), 0) AS biteTickMiss,
          SUM(CASE WHEN event_type IN ('disconnect', 'socket_disconnect') THEN 1 ELSE 0 END) AS disconnectCount,
          AVG(CAST(JSON_EXTRACT(payload, '$.population') AS REAL)) AS avgPopulation,
          SUM(CASE WHEN event_type = 'escape' THEN 1 ELSE 0 END) AS escapeCount
         FROM fishing_metrics
        WHERE created_at >= ? AND created_at < ?
          AND pond_id IS NOT NULL AND pond_id != ''
        GROUP BY pond_id`,
    )
    .all(dayStart, dayEnd);
  for (const r of rows) {
    metricsByPond.set(r.pondId, {
      hookCount: Number(r.hookCount) || 0,
      biteTickMiss: Number(r.biteTickMiss) || 0,
      disconnectCount: Number(r.disconnectCount) || 0,
      avgPopulation: r.avgPopulation != null ? Number(r.avgPopulation) : null,
      escapeCount: Number(r.escapeCount) || 0,
    });
  }
}

const pondIds = new Set([...invByPond.keys(), ...metricsByPond.keys()]);
pondIds.delete('unknown'); // 可选：仍写入 unknown 塘以便对账
if (invByPond.has('unknown') || metricsByPond.has('unknown')) {
  pondIds.add('unknown');
}

db.prepare('DELETE FROM daily_pond_stats WHERE date_key = ?').run(dateKey);
const insertPond = db.prepare(`
  INSERT INTO daily_pond_stats (
    date_key, pond_id, catch_count, bite_tick_hit, bite_tick_miss, disconnect_count, avg_population,
    hook_count, escape_count
  ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
`);
const insertPondTx = db.transaction(() => {
  for (const pondId of pondIds) {
    const catches = invByPond.get(pondId) ?? 0;
    const m = metricsByPond.get(pondId) ?? {
      hookCount: 0,
      biteTickMiss: 0,
      disconnectCount: 0,
      avgPopulation: null,
      escapeCount: 0,
    };
    insertPond.run(
      dateKey,
      pondId,
      catches,
      m.hookCount,
      m.biteTickMiss,
      m.disconnectCount,
      m.avgPopulation,
      m.hookCount,
      m.escapeCount,
    );
  }
});
insertPondTx();

const stats = db.prepare('SELECT COUNT(*) as c FROM daily_player_stats WHERE date_key = ?').get(dateKey);
const invTotal = [...invByPlayer.values()].reduce((s, n) => s + n, 0);
console.log(
  `[aggregate-daily] ${dateKey} (Asia/Shanghai): ${stats.c} player rows, inventory catch=${invTotal}, pond stats (OPS-CATCH-1.1)`,
);

db.close();
