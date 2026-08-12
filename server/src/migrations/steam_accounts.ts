import type Database from 'better-sqlite3';

export function migrateSteamAccounts(db: Database.Database): { tableCreated: boolean } {
  const existing = db
    .prepare("SELECT name FROM sqlite_master WHERE type='table' AND name='steam_accounts'")
    .get();
  if (existing) return { tableCreated: false };

  db.exec(`
    CREATE TABLE steam_accounts (
      id TEXT PRIMARY KEY,
      steam_id64 TEXT NOT NULL UNIQUE,
      player_id TEXT NOT NULL UNIQUE,
      app_id TEXT NOT NULL,
      created_at INTEGER NOT NULL,
      last_login_at INTEGER NOT NULL,
      revoked_at INTEGER,
      FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE CASCADE
    );
    CREATE INDEX idx_steam_accounts_player ON steam_accounts(player_id);
  `);
  return { tableCreated: true };
}

