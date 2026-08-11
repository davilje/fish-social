import type Database from 'better-sqlite3';
import {
  getSpecies,
  growFishSizeV2,
  type FishSpeciesId,
  type FishQuality,
} from '@fish-social/shared';

export interface FishingV2MultMigrationResult {
  birthSizeColumnAdded: boolean;
  sizesRecalculated: number;
  spotWeightsReseeded: number;
}

export function migrateFishingV2Mult(database: Database.Database): FishingV2MultMigrationResult {
  const result: FishingV2MultMigrationResult = {
    birthSizeColumnAdded: false,
    sizesRecalculated: 0,
    spotWeightsReseeded: 0,
  };

  const done = database
    .prepare('SELECT config_value FROM game_config WHERE config_key = ?')
    .get('FISHING_V2_MULT') as { config_value: string } | undefined;
  if (done?.config_value === 'true') return result;

  const cols = database.prepare(`PRAGMA table_info(pond_fish)`).all() as { name: string }[];
  if (!cols.some((c) => c.name === 'birth_size_m')) {
    database.exec(`ALTER TABLE pond_fish ADD COLUMN birth_size_m REAL`);
    result.birthSizeColumnAdded = true;
  }

  database.exec(`UPDATE pond_fish SET birth_size_m = size_m WHERE birth_size_m IS NULL`);

  const rows = database.prepare('SELECT * FROM pond_fish').all() as Array<{
    id: string;
    species_id: string;
    quality: string;
    size_m: number;
    born_at: number;
    birth_size_m: number | null;
  }>;

  const updateSize = database.prepare(
    'UPDATE pond_fish SET size_m = @sizeM, birth_size_m = @birthSizeM WHERE id = @id',
  );

  const now = Date.now();
  for (const row of rows) {
    const species = getSpecies(row.species_id as FishSpeciesId);
    const quality = row.quality as FishQuality;
    const birthSizeM = row.birth_size_m ?? row.size_m;
    const newSize = growFishSizeV2(quality, species, row.size_m, birthSizeM, row.born_at, now);
    updateSize.run({ id: row.id, sizeM: newSize, birthSizeM });
    result.sizesRecalculated += 1;
  }

  const spotCount = (
    database.prepare('SELECT COUNT(*) as c FROM spot_bite_weights').get() as { c: number }
  ).c;
  if (spotCount > 0) {
    database.exec('DELETE FROM spot_bite_weights');
    result.spotWeightsReseeded = spotCount;
  }

  const upsertFlag = database.prepare(`
    INSERT INTO game_config (config_key, config_value, updated_at)
    VALUES ('FISHING_V2_MULT', 'true', @now)
    ON CONFLICT(config_key) DO UPDATE SET config_value = 'true', updated_at = @now
  `);
  upsertFlag.run({ now: Date.now() });

  return result;
}
