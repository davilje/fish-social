/**
 * DB cleanup: drop bot/noise metrics, compact via VACUUM INTO (avoids copying 5GB freelist).
 * Stop the game server first, then: node scripts/cleanup-db-volume.mjs
 */
import Database from 'better-sqlite3';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const dataDir = path.join(root, 'data');
const dbPath = process.env.DB_PATH ?? path.join(dataDir, 'fish-social.db');
const compactPath = path.join(dataDir, 'fish-social.compact.db');
const oldPath = path.join(dataDir, `fish-social.pre-vacuum-${Date.now()}.db`);

function mb(n) {
  return (n / 1024 / 1024).toFixed(1);
}

if (!fs.existsSync(dbPath)) {
  console.error('missing', dbPath);
  process.exit(1);
}

const before = fs.statSync(dbPath).size;
console.log(`[cleanup] before: ${mb(before)} MB → ${dbPath}`);

for (const p of [compactPath]) {
  if (fs.existsSync(p)) fs.unlinkSync(p);
}

const db = new Database(dbPath);
db.pragma('busy_timeout = 15000');

const beforeMetrics = db.prepare('SELECT COUNT(*) AS c FROM fishing_metrics').get().c;
const botMetrics = db.prepare(`SELECT COUNT(*) AS c FROM fishing_metrics WHERE player_id LIKE 'bot-%'`).get().c;

const tx = db.transaction(() => {
  const r1 = db.prepare(`DELETE FROM fishing_metrics WHERE player_id LIKE 'bot-%'`).run();
  const r2 = db
    .prepare(
      `DELETE FROM fishing_metrics
       WHERE event_type IN ('bite_tick_miss', 'bite_tick_hit')
         AND (player_id IS NULL OR player_id = '')`,
    )
    .run();
  const cutoff = Date.now() - 3 * 86400_000;
  const r3 = db
    .prepare(
      `DELETE FROM fishing_metrics
       WHERE event_type IN ('bite_tick_miss', 'bite_tick_hit')
         AND created_at < ?`,
    )
    .run(cutoff);
  return { r1, r2, r3 };
});

const deleted = tx();
const afterDelete = db.prepare('SELECT COUNT(*) AS c FROM fishing_metrics').get().c;
console.log(
  `[cleanup] metrics ${beforeMetrics} → ${afterDelete} (deleted bot=${deleted.r1.changes}, nullTicks=${deleted.r2.changes}, oldTicks=${deleted.r3.changes})`,
);

console.log(`[cleanup] VACUUM INTO ${compactPath} ...`);
db.exec(`VACUUM INTO '${compactPath.replace(/\\/g, '/').replace(/'/g, "''")}'`);
db.close();

const compactSize = fs.statSync(compactPath).size;
console.log(`[cleanup] compact file: ${mb(compactSize)} MB`);

fs.renameSync(dbPath, oldPath);
fs.renameSync(compactPath, dbPath);

// Remove WAL leftovers from old open
for (const suffix of ['-wal', '-shm']) {
  const p = dbPath + suffix;
  if (fs.existsSync(p)) fs.unlinkSync(p);
}

const after = fs.statSync(dbPath).size;
console.log(`[cleanup] replaced live DB: ${mb(after)} MB (was ${mb(before)} MB)`);
console.log(`[cleanup] old hollow file kept as: ${oldPath}`);
console.log('[cleanup] verify app, then delete the .pre-vacuum-*.db to free ~5GB');
console.log('[cleanup] DONE');
