import type Database from 'better-sqlite3';

export interface FishingMetricsPlayerIdxMigrationResult {
  indexCreated: boolean;
}

/** v0.4.3：按 player_id + created_at 查询钓鱼 metrics 时间线 */
export function migrateFishingMetricsPlayerIdx(
  database: Database.Database,
): FishingMetricsPlayerIdxMigrationResult {
  const exists = database
    .prepare(`SELECT name FROM sqlite_master WHERE type = 'index' AND name = 'idx_fishing_metrics_player_time'`)
    .get();
  if (exists) return { indexCreated: false };

  database.exec(`
    CREATE INDEX idx_fishing_metrics_player_time
      ON fishing_metrics(player_id, created_at DESC)
  `);
  return { indexCreated: true };
}
