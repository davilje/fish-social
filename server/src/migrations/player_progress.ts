import type Database from 'better-sqlite3';

export function migratePlayerProgress(database: Database.Database): {
  tablesCreated: string[];
} {
  const tablesCreated: string[] = [];

  database.exec(`
    CREATE TABLE IF NOT EXISTS player_fishing_progress (
      player_id TEXT PRIMARY KEY,
      level INTEGER NOT NULL DEFAULT 1,
      xp INTEGER NOT NULL DEFAULT 0,
      onboarding_completed INTEGER NOT NULL DEFAULT 0,
      onboarding_completed_at INTEGER,
      updated_at INTEGER NOT NULL,
      FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE CASCADE
    );

    CREATE TABLE IF NOT EXISTS player_pond_proficiency (
      player_id TEXT NOT NULL,
      pond_id TEXT NOT NULL,
      level INTEGER NOT NULL DEFAULT 1,
      xp INTEGER NOT NULL DEFAULT 0,
      updated_at INTEGER NOT NULL,
      PRIMARY KEY (player_id, pond_id),
      FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE CASCADE
    );

    CREATE TABLE IF NOT EXISTS daily_admission_fees (
      player_id TEXT NOT NULL,
      date_key TEXT NOT NULL,
      charges INTEGER NOT NULL DEFAULT 0,
      progress_ms INTEGER NOT NULL DEFAULT 0,
      needs_fee_to_continue INTEGER NOT NULL DEFAULT 0,
      last_pond_id TEXT,
      updated_at INTEGER NOT NULL,
      PRIMARY KEY (player_id, date_key),
      FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE CASCADE
    );
  `);

  const existing = database
    .prepare(`SELECT name FROM sqlite_master WHERE type='table' AND name=?`)
    .get('player_fishing_progress') as { name: string } | undefined;
  if (existing) tablesCreated.push('player_fishing_progress');

  return { tablesCreated };
}
