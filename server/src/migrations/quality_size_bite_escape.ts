import type Database from 'better-sqlite3';
import {
  calcQualitySizeBiteRate,
  type FishQuality,
} from '@fish-social/shared';

const MIGRATION_KEY = 'QUALITY_SIZE_BITE_ESCAPE_V1';

export interface QualitySizeBiteEscapeMigrationResult {
  columnsAdded: boolean;
  rowsConverted: number;
}

export function migrateQualitySizeBiteEscape(
  database: Database.Database,
): QualitySizeBiteEscapeMigrationResult {
  const result: QualitySizeBiteEscapeMigrationResult = {
    columnsAdded: false,
    rowsConverted: 0,
  };

  const done = database
    .prepare('SELECT config_value FROM game_config WHERE config_key = ?')
    .get(MIGRATION_KEY) as { config_value: string } | undefined;
  if (done?.config_value === 'true') return result;

  const columns = database.prepare('PRAGMA table_info(pond_fish)').all() as Array<{ name: string }>;
  const names = new Set(columns.map((c) => c.name));

  if (!names.has('bite_multiplier')) {
    database.exec('ALTER TABLE pond_fish ADD COLUMN bite_multiplier REAL');
    result.columnsAdded = true;
  }
  if (!names.has('escape_multiplier')) {
    database.exec('ALTER TABLE pond_fish ADD COLUMN escape_multiplier REAL');
    result.columnsAdded = true;
  }

  const rows = database
    .prepare('SELECT id, quality, size_m, bite_weight FROM pond_fish')
    .all() as Array<{
    id: string;
    quality: string;
    size_m: number;
    bite_weight: number | null;
  }>;

  const update = database.prepare(
    'UPDATE pond_fish SET bite_multiplier = ?, escape_multiplier = ? WHERE id = ?',
  );

  for (const row of rows) {
    const quality = row.quality as FishQuality;
    const baseBite = calcQualitySizeBiteRate(quality, row.size_m);
    let biteMultiplier = 1.0;

    if (row.bite_weight != null && baseBite > 0) {
      const ratio = row.bite_weight / baseBite;
      if (ratio >= 0.9 && ratio <= 1.1) {
        biteMultiplier = ratio;
      }
    }

    update.run(biteMultiplier, 1.0, row.id);
    result.rowsConverted += 1;
  }

  database
    .prepare(
      `INSERT INTO game_config (config_key, config_value, updated_at) VALUES (?, 'true', ?)
       ON CONFLICT(config_key) DO UPDATE SET config_value = 'true', updated_at = excluded.updated_at`,
    )
    .run(MIGRATION_KEY, Date.now());

  return result;
}
