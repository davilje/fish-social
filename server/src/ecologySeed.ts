import { randomUUID } from 'node:crypto';
import type Database from 'better-sqlite3';
import {
  PONDS, FISH_ENTRY_GROWTH_VERSION, createFishEntryGrowth, calcEntryFishLength,
  getPondStockConfig, pickSpawnFish, pickSpotForNewFish, pondAllowsEcologySupplement,
  rollIndividualMultiplier, roundFishLength, getPondEcologyDayKey,
} from '@fish-social/shared';

/** Pure injected-database seed callback for the explicit offline L1 tool. */
export function seedEcologyRebuild(database: Database.Database, batchId: string, now: number): number {
  const formalPonds = PONDS.filter(pond => pondAllowsEcologySupplement(pond.id));
  if (formalPonds.length !== 40) throw new Error(`Expected 40 configured formal ponds; found ${formalPonds.length}. Build/export current game data first.`);
  const insertFish = database.prepare(`INSERT INTO pond_fish (
    id,pond_id,spot_id,species_id,quality,size_m,born_at,generation,bite_weight,bite_multiplier,escape_multiplier,birth_size_m,
    entered_at,entry_size_m,juvenile_entry_size_m,individual_cap_m,growth_bonus_minutes,growth_rule_version,source,stocking_batch_id
  ) VALUES (?,?,?,?,?,?,0,0,NULL,?,?,NULL,?,?,?,?,0,?,?,?)`);
  const insertState = database.prepare(`INSERT INTO pond_state (
    pond_id,depleted_until,last_weight_refresh,last_supplement_at,last_migration_at,last_simulated_at,last_daily_settlement_key,initial_stocking_batch_id
  ) VALUES (?,NULL,?,?,?,?,?,?)`);
  const insertSpot = database.prepare('INSERT INTO spot_bite_weights(pond_id,spot_id,weight) VALUES (?,?,?)');
  const dailyKey = getPondEcologyDayKey(now);
  let count = 0;
  for (const pond of PONDS) {
    const config = getPondStockConfig(pond.id);
    if (!config) throw new Error(`Missing pond stock configuration: ${pond.id}`);
    if (pondAllowsEcologySupplement(pond.id) && (config.initialPopulation !== 100 || config.maxPopulation !== 100 || pond.spots.length !== 20)) {
      throw new Error(`Formal pond must configure 100 fish and 20 spots: ${pond.id}`);
    }
    const habitat: Record<string, number> = {};
    for (const spot of pond.spots) {
      habitat[spot.id] = Math.round(Math.random() * 500) / 100;
      insertSpot.run(pond.id, spot.id, habitat[spot.id]);
    }
    for (let i = 0; i < config.initialPopulation; i++) {
      const selected = pickSpawnFish(pond.id);
      if (!selected) throw new Error(`No legal initial fish species/quality for ${pond.id}`);
      const id = randomUUID();
      const state = createFishEntryGrowth(selected.quality, id, now, 'pre_opening_seed');
      insertFish.run(id, pond.id, pickSpotForNewFish(pond.spots.map(spot => spot.id), habitat),
        selected.speciesId, selected.quality, roundFishLength(calcEntryFishLength(selected.quality, state, now)),
        rollIndividualMultiplier(), rollIndividualMultiplier(), state.enteredAt, state.entrySizeM,
        state.juvenileEntrySizeM, state.individualCapM, FISH_ENTRY_GROWTH_VERSION, state.source, batchId);
      count++;
    }
    insertState.run(pond.id, now, now, now, now, dailyKey, batchId);
  }
  return count;
}
