import type Database from 'better-sqlite3';
import {
  QUALITY_SIZE_CAP,
  upgradeQualityForSize,
  type FishQuality,
} from '@fish-social/shared';

interface PondFishRow {
  id: string;
  species_id: string;
  quality: string;
  size_m: number;
}

export interface MigrationResult {
  biteWeightColumnAdded: boolean;
  qualityUpgrades: Array<{
    id: string;
    speciesId: string;
    fromQuality: string;
    toQuality: string;
    sizeM: number;
  }>;
  remainingInvalid: number;
}

export function migrateFishingV2(
  database: Database.Database,
  options: { dryRun?: boolean } = {},
): MigrationResult {
  const { dryRun = false } = options;
  const result: MigrationResult = {
    biteWeightColumnAdded: false,
    qualityUpgrades: [],
    remainingInvalid: 0,
  };

  const columns = database
    .prepare(`PRAGMA table_info(pond_fish)`)
    .all() as Array<{ name: string }>;
  const hasBiteWeight = columns.some((c) => c.name === 'bite_weight');

  if (!hasBiteWeight) {
    if (!dryRun) {
      database.exec(`ALTER TABLE pond_fish ADD COLUMN bite_weight REAL NULL`);
    }
    result.biteWeightColumnAdded = true;
  }

  const rows = database.prepare('SELECT id, species_id, quality, size_m FROM pond_fish').all() as PondFishRow[];
  const updateQualityStmt = database.prepare(
    'UPDATE pond_fish SET quality = @quality WHERE id = @id',
  );

  for (const row of rows) {
    const quality = row.quality as FishQuality;
    const cap = QUALITY_SIZE_CAP[quality];
    if (row.size_m <= cap) continue;

    const upgraded = upgradeQualityForSize(quality, row.size_m);
    if (upgraded === quality) {
      result.remainingInvalid++;
      continue;
    }

    result.qualityUpgrades.push({
      id: row.id,
      speciesId: row.species_id,
      fromQuality: row.quality,
      toQuality: upgraded,
      sizeM: row.size_m,
    });

    if (!dryRun) {
      updateQualityStmt.run({ id: row.id, quality: upgraded });
    }
  }

  if (!dryRun) {
    let bad = 0;
    for (const row of database.prepare('SELECT quality, size_m FROM pond_fish').all() as PondFishRow[]) {
      const cap = QUALITY_SIZE_CAP[row.quality as FishQuality];
      if (row.size_m > cap) bad++;
    }
    result.remainingInvalid = bad;
  }

  return result;
}
