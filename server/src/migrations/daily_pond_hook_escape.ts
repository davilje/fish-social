import type Database from 'better-sqlite3';

/** D-L2-15: daily_pond_stats.hook_count / escape_count */
export function migrateDailyPondHookEscape(db: Database): { columnsAdded: string[] } {
  const cols = db.prepare("PRAGMA table_info('daily_pond_stats')").all() as Array<{ name: string }>;
  const names = new Set(cols.map((c) => c.name));
  const added: string[] = [];

  if (!names.has('hook_count')) {
    db.exec('ALTER TABLE daily_pond_stats ADD COLUMN hook_count INTEGER NOT NULL DEFAULT 0');
    added.push('hook_count');
  }
  if (!names.has('escape_count')) {
    db.exec('ALTER TABLE daily_pond_stats ADD COLUMN escape_count INTEGER NOT NULL DEFAULT 0');
    added.push('escape_count');
  }

  if (added.length) {
    console.log(`Migration: daily_pond_stats added ${added.join(', ')}`);
  }
  return { columnsAdded: added };
}
