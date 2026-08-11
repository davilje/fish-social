import type Database from 'better-sqlite3';
import { PONDS } from '@fish-social/shared';

const MIGRATION_KEY = 'POND_FISH_SPOT_ID_V1';

export interface PondFishSpotIdMigrationResult {
  columnAdded: boolean;
  rowsBackfilled: number;
  indexCreated: boolean;
  migrationAtColumnAdded: boolean;
}

function pickRandomSpot(pondId: string): string {
  const pond = PONDS.find((p) => p.id === pondId);
  if (!pond || pond.spots.length === 0) return 'spot-1';
  const spot = pond.spots[Math.floor(Math.random() * pond.spots.length)]!;
  return spot.id;
}

export function migratePondFishSpotId(
  database: Database.Database,
): PondFishSpotIdMigrationResult {
  const result: PondFishSpotIdMigrationResult = {
    columnAdded: false,
    rowsBackfilled: 0,
    indexCreated: false,
    migrationAtColumnAdded: false,
  };

  const done = database
    .prepare('SELECT config_value FROM game_config WHERE config_key = ?')
    .get(MIGRATION_KEY) as { config_value: string } | undefined;
  if (done?.config_value === 'true') return result;

  const fishColumns = database.prepare('PRAGMA table_info(pond_fish)').all() as Array<{ name: string }>;
  if (!fishColumns.some((c) => c.name === 'spot_id')) {
    database.exec("ALTER TABLE pond_fish ADD COLUMN spot_id TEXT NOT NULL DEFAULT 'spot-1'");
    result.columnAdded = true;
  }

  const stateColumns = database.prepare('PRAGMA table_info(pond_state)').all() as Array<{ name: string }>;
  if (!stateColumns.some((c) => c.name === 'last_migration_at')) {
    database.exec(
      'ALTER TABLE pond_state ADD COLUMN last_migration_at INTEGER NOT NULL DEFAULT 0',
    );
    result.migrationAtColumnAdded = true;
  }

  const ponds = database
    .prepare('SELECT DISTINCT pond_id FROM pond_fish')
    .all() as Array<{ pond_id: string }>;

  const updateStmt = database.prepare('UPDATE pond_fish SET spot_id = ? WHERE id = ?');
  for (const { pond_id } of ponds) {
    const rows = database
      .prepare('SELECT id, spot_id FROM pond_fish WHERE pond_id = ?')
      .all(pond_id) as Array<{ id: string; spot_id: string }>;
    const pond = PONDS.find((p) => p.id === pond_id);
    const validIds = new Set(pond?.spots.map((s) => s.id) ?? []);
    for (const row of rows) {
      if (!validIds.has(row.spot_id)) {
        updateStmt.run(pickRandomSpot(pond_id), row.id);
        result.rowsBackfilled += 1;
      }
    }
  }

  // 首次迁移：将默认 spot-1 的存量鱼打散到各塘合法随机钓点
  if (result.columnAdded) {
    for (const { pond_id } of ponds) {
      const rows = database
        .prepare('SELECT id FROM pond_fish WHERE pond_id = ?')
        .all(pond_id) as Array<{ id: string }>;
      for (const row of rows) {
        updateStmt.run(pickRandomSpot(pond_id), row.id);
        result.rowsBackfilled += 1;
      }
    }
  }

  database.exec(
    'CREATE INDEX IF NOT EXISTS idx_pond_fish_pond_spot ON pond_fish(pond_id, spot_id)',
  );
  result.indexCreated = true;

  database
    .prepare(
      `INSERT INTO game_config (config_key, config_value, updated_at) VALUES (?, 'true', ?)
       ON CONFLICT(config_key) DO UPDATE SET config_value = 'true', updated_at = excluded.updated_at`,
    )
    .run(MIGRATION_KEY, Date.now());

  return result;
}
