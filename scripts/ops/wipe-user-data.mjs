/**
 * Reset accumulated user/bot progress data, but KEEP account rows.
 *
 * Keeps:
 *   - players（账号行；coins/showcase 会归零）
 *   - steam_accounts（Steam 绑定）
 *   - game_config / pond_fish / pond_state / spot_bite_weights（配置与鱼塘世界）
 *
 * Clears (humans + bots):
 *   inventory, gear, metrics, daily_*, social, sessions, progress, album, etc.
 *
 * Stop the game server first.
 *
 *   node scripts/ops/wipe-user-data.mjs --dry-run
 *   node scripts/ops/wipe-user-data.mjs --apply
 *   node scripts/ops/wipe-user-data.mjs --apply --vacuum
 */
import Database from 'better-sqlite3';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const dataDir = path.join(root, 'data');
const dbPath = process.env.DB_PATH ?? path.join(dataDir, 'fish-social.db');

const dryRun = process.argv.includes('--dry-run');
const apply = process.argv.includes('--apply');
const doVacuum = process.argv.includes('--vacuum');

if (!dryRun && !apply) {
  console.error('Usage: node scripts/ops/wipe-user-data.mjs --dry-run | --apply [--vacuum]');
  process.exit(1);
}

if (!fs.existsSync(dbPath)) {
  console.error('missing', dbPath);
  process.exit(1);
}

/** Progress / history tables — DELETE all rows (humans + bots). */
const WIPE_TABLES = [
  'fishing_metrics',
  'inventory',
  'daily_player_stats',
  'daily_pond_ecology',
  'daily_pond_stats',
  'daily_fishing',
  'daily_admission_fees',
  'social_posts',
  'post_likes',
  'post_comments',
  'friend_links',
  'friend_requests',
  'dm_messages',
  'dm_read_cursor',
  'player_gear',
  'player_pond_session',
  'player_album_candidates',
  'player_album_pins',
  'player_achievements',
  'player_pond_proficiency',
  'player_fishing_progress',
  'player_forbidden_bans',
  'fish_codex',
  'pending_catch_locks',
  'client_logs',
  'error_logs',
  'audit_log',
  'config_audit_log',
  'config_change_requests',
  'leaderboard_snapshots',
];

/** Account / world tables — never DELETE rows. */
const KEEP_ACCOUNT_TABLES = ['players', 'steam_accounts'];
const KEEP_WORLD_TABLES = ['game_config', 'pond_fish', 'pond_state', 'spot_bite_weights'];

function mb(n) {
  return (n / 1024 / 1024).toFixed(1);
}

const db = new Database(dbPath);
db.pragma('busy_timeout = 30000');

const existing = new Set(
  db
    .prepare(`SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'`)
    .all()
    .map((r) => r.name),
);

const wipe = WIPE_TABLES.filter((t) => existing.has(t));
const missing = WIPE_TABLES.filter((t) => !existing.has(t));
const keepAll = [...KEEP_ACCOUNT_TABLES, ...KEEP_WORLD_TABLES];
const unexpected = [...existing].filter((t) => !WIPE_TABLES.includes(t) && !keepAll.includes(t));

const humanAccounts = existing.has('players')
  ? db.prepare(`SELECT COUNT(*) AS c FROM players WHERE player_id NOT LIKE 'bot-%'`).get().c
  : 0;
const botAccounts = existing.has('players')
  ? db.prepare(`SELECT COUNT(*) AS c FROM players WHERE player_id LIKE 'bot-%'`).get().c
  : 0;
const steamAccounts = existing.has('steam_accounts')
  ? db.prepare(`SELECT COUNT(*) AS c FROM steam_accounts`).get().c
  : 0;
const coinsBefore = existing.has('players')
  ? db.prepare(`SELECT COALESCE(SUM(coins),0) AS s FROM players`).get().s
  : 0;

console.log(`[wipe-user-data] db=${dbPath} size=${mb(fs.statSync(dbPath).size)} MB`);
console.log(`[wipe-user-data] mode=${dryRun ? 'DRY-RUN' : 'APPLY'}${doVacuum ? ' +VACUUM' : ''}`);
console.log(
  `[wipe-user-data] KEEP accounts: humans=${humanAccounts} bots=${botAccounts} steam=${steamAccounts}`,
);
console.log(`[wipe-user-data] will RESET players.coins/showcase (coins sum now=${coinsBefore})`);
if (missing.length) console.log('[wipe-user-data] skip missing tables:', missing.join(', '));
if (unexpected.length) {
  console.log('[wipe-user-data] WARNING unexpected tables (not wiped):', unexpected.join(', '));
}

const before = {};
let beforeTotal = 0;
for (const t of wipe) {
  const c = db.prepare(`SELECT COUNT(*) AS c FROM "${t}"`).get().c;
  before[t] = c;
  beforeTotal += c;
}
console.log('[wipe-user-data] detail rows to delete:', beforeTotal);
for (const [t, c] of Object.entries(before).sort((a, b) => b[1] - a[1])) {
  if (c > 0) console.log(`  ${t}\t${c}`);
}

if (dryRun) {
  db.close();
  console.log('[wipe-user-data] DRY-RUN done (no changes)');
  process.exit(0);
}

const stamp = new Date().toISOString().replace(/[:.]/g, '-');
console.log('[wipe-user-data] applying…');
db.pragma('foreign_keys = OFF');

const tx = db.transaction(() => {
  const changes = {};
  for (const t of wipe) {
    const r = db.prepare(`DELETE FROM "${t}"`).run();
    changes[t] = r.changes;
  }
  // Keep account rows; zero progress fields on profile.
  if (existing.has('players')) {
    const r = db
      .prepare(`UPDATE players SET coins = 0, showcase_fish_ids = '[]'`)
      .run();
    changes.players_reset = r.changes;
  }
  return changes;
});

const changes = tx();
db.pragma('foreign_keys = ON');

let afterTotal = 0;
for (const t of wipe) {
  afterTotal += db.prepare(`SELECT COUNT(*) AS c FROM "${t}"`).get().c;
}
const accountsAfter = existing.has('players')
  ? db.prepare(`SELECT COUNT(*) AS c FROM players`).get().c
  : 0;
const coinsAfter = existing.has('players')
  ? db.prepare(`SELECT COALESCE(SUM(coins),0) AS s FROM players`).get().s
  : 0;

console.log(
  '[wipe-user-data] deleted:',
  JSON.stringify(Object.fromEntries(Object.entries(changes).filter(([, v]) => v > 0))),
);
console.log(`[wipe-user-data] remaining wipe-table rows: ${afterTotal}`);
console.log(`[wipe-user-data] players still ${accountsAfter}, coins sum=${coinsAfter}`);

for (const t of keepAll) {
  if (!existing.has(t)) continue;
  const c = db.prepare(`SELECT COUNT(*) AS c FROM "${t}"`).get().c;
  console.log(`[wipe-user-data] kept ${t}=${c}`);
}

if (doVacuum) {
  const compactPath = path.join(dataDir, 'fish-social.wipe-compact.db');
  if (fs.existsSync(compactPath)) fs.unlinkSync(compactPath);
  console.log('[wipe-user-data] VACUUM INTO compact…');
  db.exec(`VACUUM INTO '${compactPath.replace(/\\/g, '/').replace(/'/g, "''")}'`);
  db.close();
  const oldPath = path.join(dataDir, `fish-social.pre-vacuum-after-wipe-${stamp}.db`);
  fs.renameSync(dbPath, oldPath);
  fs.renameSync(compactPath, dbPath);
  for (const suffix of ['-wal', '-shm']) {
    const p = dbPath + suffix;
    if (fs.existsSync(p)) fs.unlinkSync(p);
  }
  console.log(`[wipe-user-data] live DB now ${mb(fs.statSync(dbPath).size)} MB`);
  console.log(`[wipe-user-data] hollow file kept as ${oldPath} (safe to delete to free disk)`);
} else {
  db.close();
  console.log('[wipe-user-data] skipped VACUUM (file may still be ~3GB until --vacuum)');
}

console.log('[wipe-user-data] DONE · restart server to continue');
