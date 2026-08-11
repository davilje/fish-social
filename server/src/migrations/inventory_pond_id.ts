/**
 * OPS-CATCH-1 follow-up：inventory 带 pond_id，看板可分塘聚合背包产量。
 */
import type Database from 'better-sqlite3';

const MIGRATION_KEY = 'INVENTORY_POND_ID_V1';

export function migrateInventoryPondId(database: Database.Database): { columnAdded: boolean } {
  const done = database
    .prepare('SELECT config_value FROM game_config WHERE config_key = ?')
    .get(MIGRATION_KEY) as { config_value: string } | undefined;
  if (done?.config_value === 'true') return { columnAdded: false };

  const cols = database.prepare(`PRAGMA table_info(inventory)`).all() as Array<{ name: string }>;
  let columnAdded = false;
  if (!cols.some((c) => c.name === 'pond_id')) {
    database.exec(`ALTER TABLE inventory ADD COLUMN pond_id TEXT`);
    database.exec(
      `CREATE INDEX IF NOT EXISTS idx_inventory_caught_pond ON inventory(caught_at, pond_id)`,
    );
    columnAdded = true;
  }

  database
    .prepare(
      `INSERT INTO game_config (config_key, config_value, updated_at) VALUES (?, 'true', ?)
       ON CONFLICT(config_key) DO UPDATE SET config_value = 'true', updated_at = excluded.updated_at`,
    )
    .run(MIGRATION_KEY, Date.now());

  return { columnAdded };
}
