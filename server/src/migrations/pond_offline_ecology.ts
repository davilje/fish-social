import type Database from 'better-sqlite3';

export interface PondOfflineEcologyMigrationResult {
  columnAdded: boolean;
  rowsBackfilled: number;
}

export function migratePondOfflineEcology(
  database: Database.Database,
): PondOfflineEcologyMigrationResult {
  const columns = database.prepare('PRAGMA table_info(pond_state)').all() as Array<{ name: string }>;
  let columnAdded = false;
  if (!columns.some((column) => column.name === 'last_simulated_at')) {
    database.exec('ALTER TABLE pond_state ADD COLUMN last_simulated_at INTEGER NOT NULL DEFAULT 0');
    columnAdded = true;
  }

  const result = database
    .prepare(`
      UPDATE pond_state
      SET last_simulated_at = CASE
        WHEN last_simulated_at > 0 THEN last_simulated_at
        ELSE MAX(last_weight_refresh, last_supplement_at, last_migration_at)
      END
      WHERE last_simulated_at = 0
    `)
    .run();

  return { columnAdded, rowsBackfilled: result.changes };
}

