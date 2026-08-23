import type Database from 'better-sqlite3';

export function migratePlayerAlbumAchievements(database: Database.Database): {
  tablesCreated: string[];
} {
  const tablesCreated: string[] = [];

  database.exec(`
    CREATE TABLE IF NOT EXISTS player_album_candidates (
      candidate_id TEXT PRIMARY KEY,
      player_id TEXT NOT NULL,
      species_id TEXT NOT NULL,
      quality TEXT NOT NULL,
      size_m REAL NOT NULL,
      pond_id TEXT,
      pond_name TEXT,
      source TEXT NOT NULL,
      event_at INTEGER NOT NULL,
      inventory_item_id TEXT,
      FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE CASCADE
    );

    CREATE INDEX IF NOT EXISTS idx_album_candidates_player_time
      ON player_album_candidates(player_id, event_at DESC);

    CREATE TABLE IF NOT EXISTS player_album_pins (
      player_id TEXT NOT NULL,
      pin_id TEXT NOT NULL,
      sort_order INTEGER NOT NULL,
      candidate_id TEXT,
      species_id TEXT NOT NULL,
      quality TEXT NOT NULL,
      size_m REAL NOT NULL,
      pond_id TEXT,
      pond_name TEXT,
      source TEXT NOT NULL,
      event_at INTEGER NOT NULL,
      pinned_at INTEGER NOT NULL,
      PRIMARY KEY (player_id, pin_id),
      FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE CASCADE
    );

    CREATE INDEX IF NOT EXISTS idx_album_pins_player_order
      ON player_album_pins(player_id, sort_order);

    CREATE TABLE IF NOT EXISTS player_achievements (
      player_id TEXT NOT NULL,
      achievement_id TEXT NOT NULL,
      unlocked_at INTEGER NOT NULL,
      PRIMARY KEY (player_id, achievement_id),
      FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE CASCADE
    );
  `);

  for (const name of [
    'player_album_candidates',
    'player_album_pins',
    'player_achievements',
  ]) {
    const existing = database
      .prepare(`SELECT name FROM sqlite_master WHERE type='table' AND name=?`)
      .get(name) as { name: string } | undefined;
    if (existing) tablesCreated.push(name);
  }

  return { tablesCreated };
}
