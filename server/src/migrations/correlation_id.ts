import type Database from 'better-sqlite3';

export function migrateCorrelationId(db: Database): { columnAdded: boolean } {
  const tableInfo = db.prepare("PRAGMA table_info('fishing_metrics')").all() as Array<{ name: string }>;
  const hasCol = tableInfo.some((c) => c.name === 'correlation_id');
  if (hasCol) return { columnAdded: false };

  db.exec("ALTER TABLE fishing_metrics ADD COLUMN correlation_id TEXT");
  db.exec("CREATE INDEX IF NOT EXISTS idx_fishing_metrics_correlation ON fishing_metrics(correlation_id)");
  console.log('Migration: fishing_metrics.correlation_id column added');
  return { columnAdded: true };
}
