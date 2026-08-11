import type Database from 'better-sqlite3';

const MIGRATION_KEY = 'POND_SUPPLEMENT_STATE_V1';

export interface PondSupplementStateMigrationResult {
  columnAdded: boolean;
}

export function migratePondSupplementState(
  database: Database.Database,
): PondSupplementStateMigrationResult {
  const result: PondSupplementStateMigrationResult = { columnAdded: false };

  const done = database
    .prepare('SELECT config_value FROM game_config WHERE config_key = ?')
    .get(MIGRATION_KEY) as { config_value: string } | undefined;
  if (done?.config_value === 'true') return result;

  const columns = database.prepare('PRAGMA table_info(pond_state)').all() as Array<{ name: string }>;
  if (!columns.some((c) => c.name === 'last_supplement_at')) {
    database.exec(
      'ALTER TABLE pond_state ADD COLUMN last_supplement_at INTEGER NOT NULL DEFAULT 0',
    );
    result.columnAdded = true;
  }

  database
    .prepare(
      `INSERT INTO game_config (config_key, config_value, updated_at) VALUES (?, 'true', ?)
       ON CONFLICT(config_key) DO UPDATE SET config_value = 'true', updated_at = excluded.updated_at`,
    )
    .run(MIGRATION_KEY, Date.now());

  return result;
}
