import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

/** @param {import('better-sqlite3').Database} db */
export function resolveRulesVersion(db) {
  try {
    const row = db.prepare("SELECT config_value FROM game_config WHERE config_key = 'RULES_VERSION'").get();
    if (row?.config_value) return String(row.config_value);
  } catch {
    /* game_config may not exist in fixture DB */
  }
  if (process.env.RULES_VERSION) return process.env.RULES_VERSION;
  try {
    const pkg = JSON.parse(fs.readFileSync(path.join(__dirname, '../../package.json'), 'utf8'));
    if (pkg.version) return `pkg-${pkg.version}`;
  } catch {
    /* ignore */
  }
  return 'unknown';
}
