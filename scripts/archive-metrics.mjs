import Database from 'better-sqlite3';
import path from 'path';
import { fileURLToPath } from 'url';
import fs from 'fs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(__dirname, '../data');
const dbPath = process.env.DB_PATH ?? path.join(dataDir, 'fish-social.db');
const RETENTION_DAYS = Number(process.env.METRICS_RETENTION_DAYS ?? 90);

const db = new Database(dbPath);
db.pragma('journal_mode = WAL');

const cutoff = Date.now() - RETENTION_DAYS * 24 * 60 * 60 * 1000;
const archiveDir = path.join(dataDir, 'archives');
if (!fs.existsSync(archiveDir)) {
  fs.mkdirSync(archiveDir, { recursive: true });
}

const rows = db.prepare('SELECT * FROM fishing_metrics WHERE created_at < ?').all(cutoff);
if (rows.length === 0) {
  console.log('[archive-metrics] no rows to archive');
  db.close();
  process.exit(0);
}

const dateStr = new Date(cutoff).toISOString().split('T')[0];
const archiveFile = path.join(archiveDir, `metrics-${dateStr}.jsonl`);
const lines = rows.map(r => JSON.stringify(r));
fs.writeFileSync(archiveFile, lines.join('\n') + '\n', 'utf8');

db.prepare('DELETE FROM fishing_metrics WHERE created_at < ?').run(cutoff);
console.log(`[archive-metrics] archived ${rows.length} rows to ${archiveFile}`);

db.close();
