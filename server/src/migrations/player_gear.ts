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

  const cols = database.prepare(`PRAGMA table_info(player_gear)`).all() as Array<{ name: string }>;
  const names = new Set(cols.map((c) => c.name));
  const addCol = (name: string, ddl: string) => {
    if (names.has(name)) return;
    database.exec(`ALTER TABLE player_gear ADD COLUMN ${ddl}`);
    names.add(name);
  };
  addCol('equipped_rod', `equipped_rod TEXT NOT NULL DEFAULT ''`);
  addCol('owned_rods', `owned_rods TEXT NOT NULL DEFAULT '[]'`);
  addCol('rod_oversize', `rod_oversize TEXT NOT NULL DEFAULT '{}'`);
  addCol('unlocked_baits', `unlocked_baits TEXT NOT NULL DEFAULT '["bait-basic"]'`);
  addCol('owned_vessels', `owned_vessels TEXT NOT NULL DEFAULT '[]'`);
  addCol('starter_rod_granted', `starter_rod_granted INTEGER NOT NULL DEFAULT 0`);

  try {
    database.exec(`
      UPDATE player_gear
      SET starter_rod_granted = 1,
          owned_rods = '["rod-bamboo"]',
          equipped_rod = CASE WHEN equipped_rod IS NULL OR equipped_rod = '' THEN 'rod-bamboo' ELSE equipped_rod END
      WHERE starter_rod_granted = 0
        AND player_id IN (
          SELECT player_id FROM player_fishing_progress WHERE onboarding_completed = 1
        )
    `);
  } catch {
    // player_fishing_progress may not exist yet on a fresh migrate order
  }

  return result;
}
