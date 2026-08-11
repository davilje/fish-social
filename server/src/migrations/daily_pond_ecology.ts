import type Database from 'better-sqlite3';

export function migrateDailyPondEcology(db: Database): void {
  db.exec(`
    CREATE TABLE IF NOT EXISTS daily_pond_ecology (
      date_key TEXT NOT NULL,
      pond_id TEXT NOT NULL,
      population INTEGER NOT NULL DEFAULT 0,
      max_population INTEGER NOT NULL DEFAULT 0,
      pop_ratio REAL,
      quality_json TEXT NOT NULL DEFAULT '{}',
      avg_size_m REAL,
      PRIMARY KEY (date_key, pond_id)
    );
  `);
}
