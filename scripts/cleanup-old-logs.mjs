/**
 * P1-B2: Clean up old log files beyond retention period
 * Run: node scripts/cleanup-old-logs.mjs
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const rootDir = path.resolve(__dirname, '..');

const LOG_DIR = process.env.LOG_DIR ? path.resolve(process.env.LOG_DIR) : path.join(rootDir, 'server/logs');
const RETENTION_DAYS = parseInt(process.env.LOG_RETENTION_DAYS || '30', 10);
const NOW = Date.now();
const CUTOFF_MS = NOW - RETENTION_DAYS * 24 * 60 * 60 * 1000;

let deletedCount = 0;
let totalSize = 0;

function walk(dir) {
  if (!fs.existsSync(dir)) {
    console.log('Log directory does not exist:', dir);
    return;
  }
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      walk(fullPath);
    } else if (entry.isFile()) {
      const stat = fs.statSync(fullPath);
      if (stat.mtimeMs < CUTOFF_MS) {
        try {
          totalSize += stat.size;
          fs.unlinkSync(fullPath);
          deletedCount++;
          console.log('Deleted:', fullPath);
        } catch (e) {
          console.error('Failed to delete:', fullPath, e.message);
        }
      }
    }
  }
}

walk(LOG_DIR);
console.log('Cleanup complete: deleted ' + deletedCount + ' files (' + (totalSize / 1024).toFixed(1) + ' KB freed)');
if (deletedCount === 0) {
  console.log('No expired log files found (retention: ' + RETENTION_DAYS + ' days)');
}
