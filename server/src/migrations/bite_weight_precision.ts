import type Database from 'better-sqlite3';
import { getSpecies, getSpeciesBiteRatePerTick, type FishSpeciesId } from '@fish-social/shared';

const MIGRATION_KEY = 'BITE_WEIGHT_PRECISION_V1';

export interface BiteWeightPrecisionResult {
  rowsUpdated: number;
}

/**
 * 将旧版 round2 精度的 bite_weight（e.g. 0.02）按物种重算为 4 位小数精度
 * 消除因 round2(round2(biteWeight*0.2)*rand) 双重截断导致所有鱼显示同一咬钩率的问题
 */
export function migrateBiteWeightPrecision(
  database: Database.Database,
): BiteWeightPrecisionResult {
  const result: BiteWeightPrecisionResult = { rowsUpdated: 0 };

  const done = database
    .prepare('SELECT config_value FROM game_config WHERE config_key = ?')
    .get(MIGRATION_KEY) as { config_value: string } | undefined;
  if (done?.config_value === 'true') return result;

  const rows = database
    .prepare('SELECT id, species_id, bite_weight FROM pond_fish WHERE bite_weight IS NOT NULL')
    .all() as Array<{ id: string; species_id: string; bite_weight: number }>;

  const update = database.prepare('UPDATE pond_fish SET bite_weight = ? WHERE id = ?');

  const updateMany = database.transaction(() => {
    for (const row of rows) {
      let species;
      try {
        species = getSpecies(row.species_id as FishSpeciesId);
      } catch {
        continue;
      }
      const correctRate = getSpeciesBiteRatePerTick(species);
      // Only update fish whose stored value has <= 2dp precision (old round2 values)
      const storedDp = countDecimalPlaces(row.bite_weight);
      if (storedDp <= 2) {
        update.run(correctRate, row.id);
        result.rowsUpdated += 1;
      }
    }
  });

  updateMany();

  database
    .prepare(
      `INSERT INTO game_config (config_key, config_value, updated_at) VALUES (?, 'true', ?)
       ON CONFLICT(config_key) DO UPDATE SET config_value = 'true', updated_at = excluded.updated_at`,
    )
    .run(MIGRATION_KEY, Date.now());

  return result;
}

function countDecimalPlaces(n: number): number {
  const s = n.toString();
  const dot = s.indexOf('.');
  return dot === -1 ? 0 : s.length - dot - 1;
}
