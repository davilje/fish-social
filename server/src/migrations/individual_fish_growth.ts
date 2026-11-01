import type Database from 'better-sqlite3';
import { calcIndividualFishLength, getSpecies, type FishQuality } from '@fish-social/shared';

export const INDIVIDUAL_FISH_GROWTH_KEY = 'INDIVIDUAL_FISH_GROWTH_V1';

/** 一次性重算未钓获塘鱼，解除旧品质固定体长。背包、图鉴、相册等历史记录不变。 */
export function migrateIndividualFishGrowth(database: Database.Database, now = Date.now()): number {
  const done = database.prepare('SELECT config_value FROM game_config WHERE config_key = ?')
    .get(INDIVIDUAL_FISH_GROWTH_KEY) as { config_value: string } | undefined;
  if (done?.config_value === 'true') return 0;

  return database.transaction(() => {
    const rows = database.prepare('SELECT id, species_id, quality, size_m, birth_size_m, born_at FROM pond_fish')
      .all() as Array<{ id: string; species_id: string; quality: FishQuality; size_m: number; birth_size_m: number | null; born_at: number }>;
    const update = database.prepare('UPDATE pond_fish SET size_m = ? WHERE id = ?');
    let changed = 0;
    for (const row of rows) {
      const size = calcIndividualFishLength(row.quality, getSpecies(row.species_id), row.birth_size_m ?? row.size_m,
        Math.max(0, now - row.born_at) / 86_400_000, row.id);
      if (size !== row.size_m) {
        update.run(size, row.id);
        changed++;
      }
    }
    database.prepare(`INSERT INTO game_config (config_key, config_value, updated_at) VALUES (?, 'true', ?)
      ON CONFLICT(config_key) DO UPDATE SET config_value = 'true', updated_at = excluded.updated_at`)
      .run(INDIVIDUAL_FISH_GROWTH_KEY, now);
    return changed;
  })();
}
