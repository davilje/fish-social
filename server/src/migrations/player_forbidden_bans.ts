import type Database from 'better-sqlite3';

export function migrateForbiddenBans(database: Database.Database): {
  tableCreated: boolean;
} {
  database.exec(`
    CREATE TABLE IF NOT EXISTS player_forbidden_bans (
      player_id TEXT NOT NULL,
      pond_id TEXT NOT NULL,
      until_ms INTEGER NOT NULL,
      kind TEXT NOT NULL,
      updated_at INTEGER NOT NULL,
      PRIMARY KEY (player_id, pond_id),
      FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE CASCADE
    );
  `);

  const existing = database
    .prepare(`SELECT name FROM sqlite_master WHERE type='table' AND name=?`)
    .get('player_forbidden_bans') as { name: string } | undefined;

  return { tableCreated: Boolean(existing) };
}
