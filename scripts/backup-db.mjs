import Database from 'better-sqlite3';
import path from 'path';
import { fileURLToPath } from 'url';
import fs from 'fs';
import zlib from 'zlib';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(__dirname, '../data');
const dbPath = process.env.DB_PATH ?? path.join(dataDir, 'fish-social.db');
const backupDir = path.join(dataDir, 'backups');
const RETAIN = Number(process.env.DB_BACKUP_RETAIN ?? 7);

if (!fs.existsSync(backupDir)) {
  fs.mkdirSync(backupDir, { recursive: true });
}

const now = new Date();
const ts = `${now.getFullYear()}${String(now.getMonth()+1).padStart(2,'0')}${String(now.getDate()).padStart(2,'0')}-${String(now.getHours()).padStart(2,'0')}${String(now.getMinutes()).padStart(2,'0')}`;
const backupFile = path.join(backupDir, `fish-social-${ts}.db.gz`);

const db = new Database(dbPath);
const backupDb = new Database(':memory:');
const backup = db.backup(backupDb);
backup.step(-1);
backup.complete();

const buf = backupDb.serialize();
const gzipped = zlib.gzipSync(buf);
fs.writeFileSync(backupFile, gzipped);
backupDb.close();
db.close();

console.log(`[backup-db] created: ${backupFile} (${(gzipped.length / 1024).toFixed(1)} KB)`);

// Prune old backups
const files = fs.readdirSync(backupDir)
  .filter(f => f.startsWith('fish-social-') && f.endsWith('.db.gz'))
  .sort()
  .reverse();

if (files.length > RETAIN) {
  for (const f of files.slice(RETAIN)) {
    fs.unlinkSync(path.join(backupDir, f));
    console.log(`[backup-db] pruned: ${f}`);
  }
}
