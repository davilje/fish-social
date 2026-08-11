import type Database from 'better-sqlite3';

export interface PlayerGearMigrationResult {
  tableCreated: boolean;
  rowsBackfilled: number;
}

export function migratePlayerGear(database: Database.Database): PlayerGearMigrationResult {
  const result: PlayerGearMigrationResult = { tableCreated: false, rowsBackfilled: 0 };

  database.exec(`
    CREATE TABLE IF NOT EXISTS player_gear (
      player_id TEXT PRIMARY KEY,
      equipped_bait TEXT NOT NULL DEFAULT 'basic',
      equipped_tackle TEXT NOT NULL DEFAULT 'basic',
      bait_inventory TEXT NOT NULL DEFAULT '{}',
      owned_tackles TEXT NOT NULL DEFAULT '["basic"]',
      updated_at INTEGER NOT NULL,
      FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE CASCADE
    );
  `);
  result.tableCreated = true;

  const players = database.prepare('SELECT player_id FROM players').all() as {
    player_id: string;
  }[];
  const insert = database.prepare(`
    INSERT OR IGNORE INTO player_gear (
      player_id, equipped_bait, equipped_tackle, bait_inventory, owned_tackles, updated_at
    ) VALUES (?, 'basic', 'basic', ?, '["basic"]', ?)
  `);
  const now = Date.now();
  const trialCorn = JSON.stringify({ corn: 5 });

  const tx = database.transaction(() => {
    for (const { player_id } of players) {
      const info = insert.run(player_id, trialCorn, now);
      if (info.changes > 0) result.rowsBackfilled += 1;
    }
  });
  tx();

  return result;
}
