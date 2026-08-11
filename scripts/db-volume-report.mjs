/**
 * Quick DB volume report (avoids full COUNT on huge tables when possible).
 * Usage: node scripts/db-volume-report.mjs
 */
import Database from 'better-sqlite3';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const dbPath = process.env.DB_PATH ?? path.join(root, 'data', 'fish-social.db');

const fileBytes = fs.statSync(dbPath).size;
const db = new Database(dbPath, { readonly: true, fileMustExist: true, timeout: 5000 });
try {
  db.pragma('query_only = ON');
} catch {}

const pageCount = Number(db.pragma('page_count', { simple: true }));
const pageSize = Number(db.pragma('page_size', { simple: true }));
const freelist = Number(db.pragma('freelist_count', { simple: true }));

const tables = db
  .prepare(`SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name`)
  .all()
  .map((r) => r.name);

console.log('=== FILE ===');
console.log(
  JSON.stringify(
    {
      dbPath,
      fileGB: +(fileBytes / 1024 / 1024 / 1024).toFixed(2),
      fileMB: Math.round(fileBytes / 1024 / 1024),
      pageCount,
      pageSize,
      freelistPages: freelist,
      freelistMB: Math.round((freelist * pageSize) / 1024 / 1024),
      logicalMB: Math.round(((pageCount - freelist) * pageSize) / 1024 / 1024),
    },
    null,
    2,
  ),
);

// Per-table page usage via dbstat (fast-ish)
let byTable = [];
try {
  db.exec('CREATE VIRTUAL TABLE IF NOT EXISTS temp.dbstat USING dbstat');
  byTable = db
    .prepare(
      `SELECT name AS tableName,
              SUM(pgsize) AS bytes,
              COUNT(*) AS pages
       FROM temp.dbstat
       WHERE name NOT LIKE 'sqlite_%' AND name NOT LIKE 'idx_%' AND name != 'dbstat'
       GROUP BY name
       ORDER BY bytes DESC`,
    )
    .all();
  // Also indexes
  const idx = db
    .prepare(
      `SELECT name AS tableName,
              SUM(pgsize) AS bytes,
              COUNT(*) AS pages
       FROM temp.dbstat
       WHERE name LIKE 'idx_%' OR name LIKE 'sqlite_autoindex%'
       GROUP BY name
       ORDER BY bytes DESC
       LIMIT 20`,
    )
    .all();
  console.log('=== SPACE_BY_TABLE (dbstat) ===');
  for (const r of byTable.slice(0, 25)) {
    console.log(
      `${r.tableName.padEnd(36)} ${(r.bytes / 1024 / 1024).toFixed(1).padStart(8)} MB  pages=${r.pages}`,
    );
  }
  console.log('=== TOP_INDEXES ===');
  for (const r of idx) {
    console.log(
      `${r.tableName.padEnd(48)} ${(r.bytes / 1024 / 1024).toFixed(1).padStart(8)} MB`,
    );
  }
} catch (e) {
  console.log('dbstat failed:', e.message);
}

function safeCount(sql, params = []) {
  const t0 = Date.now();
  try {
    const row = db.prepare(sql).get(...params);
    return { ...(row || {}), ms: Date.now() - t0 };
  } catch (e) {
    return { error: e.message, ms: Date.now() - t0 };
  }
}

console.log('=== FISHING_METRICS ===');
const fmCols = db.prepare(`PRAGMA table_info(fishing_metrics)`).all().map((c) => c.name);
console.log('columns:', fmCols.join(', '));

// min/max created_at + rowid range (cheap)
const span = safeCount(
  `SELECT MIN(rowid) AS minRowid, MAX(rowid) AS maxRowid,
          MIN(created_at) AS minAt, MAX(created_at) AS maxAt
   FROM fishing_metrics`,
);
console.log('span:', span);

// Approximate: if rowids dense, max-min+1; else sample
if (span.maxRowid != null) {
  console.log('rowidSpanApprox:', Number(span.maxRowid) - Number(span.minRowid) + 1);
}

// Exact count with progress timeout — may be slow; set SKIP_EXACT=1 to skip
if (process.env.SKIP_EXACT !== '1') {
  console.log('counting fishing_metrics (may take minutes)...');
  const exact = safeCount(`SELECT COUNT(*) AS c FROM fishing_metrics`);
  console.log('exactCount:', exact);
}

console.log('=== METRICS BREAKDOWNS (sampled queries) ===');
const byEvent = safeCount(
  `SELECT event_type, COUNT(*) AS c FROM fishing_metrics GROUP BY event_type ORDER BY c DESC LIMIT 25`,
);
// GROUP BY full table is slow — use approximate via index if needed
console.log('byEvent (full scan if no covering idx):', byEvent);

const botish = safeCount(
  `SELECT COUNT(*) AS c FROM fishing_metrics WHERE player_id LIKE 'bot-%'`,
);
console.log('botPlayerId:', botish);

const nullPlayer = safeCount(
  `SELECT COUNT(*) AS c FROM fishing_metrics WHERE player_id IS NULL OR player_id = ''`,
);
console.log('nullOrEmptyPlayer:', nullPlayer);

const biteMiss = safeCount(
  `SELECT COUNT(*) AS c FROM fishing_metrics WHERE event_type IN ('bite_tick_miss','bite_tick_hit','bite_tick')`,
);
console.log('biteTickFamily:', biteMiss);

const last7 = Date.now() - 7 * 86400_000;
const last1 = Date.now() - 86400_000;
console.log(
  'last24h:',
  safeCount(`SELECT COUNT(*) AS c FROM fishing_metrics WHERE created_at >= ?`, [last1]),
);
console.log(
  'before7d:',
  safeCount(`SELECT COUNT(*) AS c FROM fishing_metrics WHERE created_at < ?`, [last7]),
);
console.log(
  'last7d:',
  safeCount(`SELECT COUNT(*) AS c FROM fishing_metrics WHERE created_at >= ?`, [last7]),
);

console.log('=== OTHER TABLES (exact, usually small) ===');
for (const t of tables.filter((n) => n !== 'fishing_metrics')) {
  const r = safeCount(`SELECT COUNT(*) AS c FROM "${t}"`);
  if (r.c != null && r.c > 0) console.log(t, r);
}

console.log('=== PLAYERS / BOTS ===');
console.log(
  'players:',
  safeCount(
    `SELECT COUNT(*) AS total,
            SUM(CASE WHEN player_id LIKE 'bot-%' THEN 1 ELSE 0 END) AS bots,
            SUM(CASE WHEN player_id NOT LIKE 'bot-%' THEN 1 ELSE 0 END) AS humans
     FROM players`,
  ),
);

console.log('=== ERROR_LOGS ===');
console.log('error_logs:', safeCount(`SELECT COUNT(*) AS c FROM error_logs`));
console.log(
  'epipeish:',
  safeCount(
    `SELECT COUNT(*) AS c FROM error_logs WHERE message LIKE '%EPIPE%' OR message LIKE '%broken pipe%'`,
  ),
);

console.log('=== DAILY AGG TABLES ===');
for (const t of tables.filter((n) => n.includes('daily') || n.includes('summary'))) {
  console.log(t, safeCount(`SELECT COUNT(*) AS c FROM "${t}"`));
}

db.close();
console.log('DONE');
