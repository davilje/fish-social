import type Database from 'better-sqlite3';

export interface PlayerPondSessionMigrationResult {
  tablesCreated: string[];
}

export function migratePlayerPondSession(database: Database.Database): PlayerPondSessionMigrationResult {
  const tablesCreated: string[] = [];

  database.exec(`
    CREATE TABLE IF NOT EXISTS player_pond_session (
      player_id TEXT NOT NULL,
      pond_id TEXT NOT NULL,
      user_id TEXT NOT NULL,
      spot_id TEXT,
      fishing_phase TEXT,
      phase_ends_at INTEGER,
      hook_ends_at INTEGER,
      disconnected_at INTEGER,
      updated_at INTEGER NOT NULL,
      PRIMARY KEY (player_id, pond_id)
    );

    CREATE TABLE IF NOT EXISTS pending_catch_locks (
      user_id TEXT PRIMARY KEY,
      catch_id TEXT NOT NULL,
      pond_fish_id TEXT NOT NULL,
      species_id TEXT NOT NULL,
      quality TEXT NOT NULL,
      size_m REAL NOT NULL,
      hook_duration_ms INTEGER NOT NULL,
      is_codex_new INTEGER NOT NULL DEFAULT 0,
      player_id TEXT,
      pond_id TEXT,
      locked_at INTEGER NOT NULL
    );
  `);

  tablesCreated.push('player_pond_session', 'pending_catch_locks');
  return { tablesCreated };
}
