import type Database from 'better-sqlite3';
import { getSpecies, growFishSizeV2, type FishQuality, type FishSpeciesId } from '@fish-social/shared';

const MIGRATION_KEY = 'ABSOLUTE_GROWTH_CURVE_V1';

export interface AbsoluteGrowthCurveMigrationResult {
  sizesRecalculated: number;
}

/** v0.3.1：按绝对长度–时间曲线重算所有鱼塘鱼 size_m */
export function migrateAbsoluteGrowthCurve(
  database: Database.Database,
): AbsoluteGrowthCurveMigrationResult {
  const result: AbsoluteGrowthCurveMigrationResult = { sizesRecalculated: 0 };

  const done = database
    .prepare('SELECT config_value FROM game_config WHERE config_key = ?')
    .get(MIGRATION_KEY) as { config_value: string } | undefined;
  if (done?.config_value === 'true') return result;

  const rows = database
    .prepare('SELECT id, species_id, quality, size_m, birth_size_m, born_at FROM pond_fish')
    .all() as Array<{
    id: string;
    species_id: string;
    quality: string;
    size_m: number;
    birth_size_m: number | null;
    born_at: number;
  }>;

  const update = database.prepare('UPDATE pond_fish SET size_m = ? WHERE id = ?');
  const now = Date.now();

  for (const row of rows) {
    const species = getSpecies(row.species_id as FishSpeciesId);
    const quality = row.quality as FishQuality;
    const birthSizeM = row.birth_size_m ?? row.size_m;
    const newSize = growFishSizeV2(quality, species, row.size_m, birthSizeM, row.born_at, now);
    if (newSize !== row.size_m) {
      update.run(newSize, row.id);
      result.sizesRecalculated += 1;
    }
  }

  database
    .prepare(
      `INSERT INTO game_config (config_key, config_value, updated_at) VALUES (?, 'true', ?)
       ON CONFLICT(config_key) DO UPDATE SET config_value = 'true', updated_at = excluded.updated_at`,
    )
    .run(MIGRATION_KEY, now);

  return result;
}
