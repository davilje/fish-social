import type Database from 'better-sqlite3';

export function migrateDailyStats(db: Database): void {
  db.exec(`
    CREATE TABLE IF NOT EXISTS daily_player_stats (
      date_key TEXT NOT NULL,
      player_id TEXT NOT NULL,
      catch_count INTEGER NOT NULL DEFAULT 0,
      escape_count INTEGER NOT NULL DEFAULT 0,
      disconnect_count INTEGER NOT NULL DEFAULT 0,
      fishing_ms INTEGER NOT NULL DEFAULT 0,
      PRIMARY KEY (date_key, player_id)
    );

    CREATE TABLE IF NOT EXISTS daily_pond_stats (
      date_key TEXT NOT NULL,
      pond_id TEXT NOT NULL,
      catch_count INTEGER NOT NULL DEFAULT 0,
      bite_tick_hit INTEGER NOT NULL DEFAULT 0,
      bite_tick_miss INTEGER NOT NULL DEFAULT 0,
      disconnect_count INTEGER NOT NULL DEFAULT 0,
      avg_population REAL,
      hook_count INTEGER NOT NULL DEFAULT 0,
      escape_count INTEGER NOT NULL DEFAULT 0,
      PRIMARY KEY (date_key, pond_id)
    );
  `);
}
