import type Database from 'better-sqlite3';

export interface MetricsDedupMigrationResult {
  columnAdded: boolean;
  indexCreated: boolean;
}

export function migrateMetricsDedup(database: Database.Database): MetricsDedupMigrationResult {
  const tableInfo = database
    .prepare(`PRAGMA table_info('fishing_metrics')`)
    .all() as Array<{ name: string }>;
  let columnAdded = false;
  if (!tableInfo.some((c) => c.name === 'dedup_key')) {
    database.exec('ALTER TABLE fishing_metrics ADD COLUMN dedup_key TEXT');
    columnAdded = true;
  }

  const index = database
    .prepare(`SELECT name FROM sqlite_master WHERE type = 'index' AND name = 'idx_fishing_metrics_dedup_key'`)
    .get() as { name: string } | undefined;
  let indexCreated = false;
  if (!index) {
    database.exec(
      'CREATE UNIQUE INDEX idx_fishing_metrics_dedup_key ON fishing_metrics(dedup_key) WHERE dedup_key IS NOT NULL',
    );
    indexCreated = true;
  }

  return { columnAdded, indexCreated };
}
