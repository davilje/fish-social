import { randomUUID } from 'crypto';

import {
  POND_ECOSYSTEM_TICK_MS,
  POND_SUPPLEMENT_BATCH_MAX,
  POND_SUPPLEMENT_BATCH_RATIO,
  POND_SUPPLEMENT_TARGET_RATIO,
  PONDS,
  SPOT_BITE_WEIGHT_REFRESH_MS,
  FISH_MIGRATION_FRACTION,
  FISH_QUALITIES,
  calcEscapeGrowthSize,
  calcQualitySizeBiteRate,
  calcSizeEscapeRate,
  calcSupplementCheckMs,
  formatBiteRatePct,
  getPondStockConfig,
  getSpecies,
  growFishSizeV2,
  isFishingActive,
  pickMigrationSpot,
  pickSpotForNewFish,
  rollFishQuality,
  rollIndividualMultiplier,
  rollJuvenileSize,
  rollSupplementQuality,
  round2,
  type FishQuality,
  type FishSpeciesId,
  type PondEcologySummary,
  type PondFishEntity,
  type PondStockConfig,
} from '@fish-social/shared';

import { db } from './db.js';
import { listUsersInPond } from './gameState.js';
import { isEcologyVerbose } from './fishingObservability.js';

interface PondFishRow {
  id: string;
  pond_id: string;
  spot_id: string;
  species_id: string;
  quality: string;
  size_m: number;
  born_at: number;
  generation: number;
  bite_weight: number | null;
  bite_multiplier: number | null;
  escape_multiplier: number | null;
  birth_size_m: number | null;
}

interface PondStateRow {
  depleted_until: number | null;
  last_weight_refresh: number;
  last_supplement_at: number;
  last_migration_at: number;
}

function rowToEntity(row: PondFishRow): PondFishEntity {
  return {
    id: row.id,
    pondId: row.pond_id,
    spotId: row.spot_id,
    speciesId: row.species_id as FishSpeciesId,
    quality: row.quality as PondFishEntity['quality'],
    sizeM: row.size_m,
    bornAt: row.born_at,
    generation: row.generation,
    biteMultiplier: row.bite_multiplier ?? 1.0,
    escapeMultiplier: row.escape_multiplier ?? 1.0,
    biteWeight: row.bite_weight,
    birthSizeM: row.birth_size_m ?? row.size_m,
  };
}

const insertFishStmt = db.prepare(`
  INSERT INTO pond_fish (id, pond_id, spot_id, species_id, quality, size_m, born_at, generation, bite_weight, bite_multiplier, escape_multiplier, birth_size_m)
  VALUES (@id, @pondId, @spotId, @speciesId, @quality, @sizeM, @bornAt, @generation, @biteWeight, @biteMultiplier, @escapeMultiplier, @birthSizeM)
`);

const deleteFishStmt = db.prepare('DELETE FROM pond_fish WHERE id = ?');
const countFishStmt = db.prepare('SELECT COUNT(*) as c FROM pond_fish WHERE pond_id = ?');
const listFishStmt = db.prepare('SELECT * FROM pond_fish WHERE pond_id = ?');
const listFishAtSpotStmt = db.prepare(
  'SELECT * FROM pond_fish WHERE pond_id = ? AND spot_id = ?',
);
const getFishStmt = db.prepare('SELECT * FROM pond_fish WHERE id = ?');
const updateFishSizeStmt = db.prepare('UPDATE pond_fish SET size_m = ? WHERE id = ?');
const updateFishSpotStmt = db.prepare('UPDATE pond_fish SET spot_id = ? WHERE id = ?');

const upsertPondStateStmt = db.prepare(`
  INSERT INTO pond_state (pond_id, depleted_until, last_weight_refresh, last_supplement_at, last_migration_at)
  VALUES (@pondId, @depletedUntil, @lastWeightRefresh, @lastSupplementAt, @lastMigrationAt)
  ON CONFLICT(pond_id) DO UPDATE SET
    depleted_until = excluded.depleted_until,
    last_weight_refresh = excluded.last_weight_refresh,
    last_supplement_at = excluded.last_supplement_at,
    last_migration_at = excluded.last_migration_at
`);

const getPondStateStmt = db.prepare('SELECT * FROM pond_state WHERE pond_id = ?');

const upsertSpotWeightStmt = db.prepare(`
  INSERT INTO spot_bite_weights (pond_id, spot_id, weight)
  VALUES (@pondId, @spotId, @weight)
  ON CONFLICT(pond_id, spot_id) DO UPDATE SET weight = excluded.weight
`);

const listSpotWeightsStmt = db.prepare(
  'SELECT spot_id, weight FROM spot_bite_weights WHERE pond_id = ?',
);

/** PERF-05: per-pond spot bite weights; filled on refresh, read on bite path */
const spotWeightCache = new Map<string, Map<string, number>>();

function loadSpotWeightsIntoCache(pondId: string): Map<string, number> {
  const cache = new Map<string, number>();
  for (const row of listSpotWeightsStmt.all(pondId) as { spot_id: string; weight: number }[]) {
    cache.set(row.spot_id, row.weight);
  }
  spotWeightCache.set(pondId, cache);
  return cache;
}

function getCachedSpotWeights(pondId: string): Map<string, number> {
  return spotWeightCache.get(pondId) ?? loadSpotWeightsIntoCache(pondId);
}

function getHabitatWeights(pondId: string): Record<string, number> {
  const weights: Record<string, number> = {};
  for (const [spotId, weight] of getCachedSpotWeights(pondId)) {
    weights[spotId] = weight;
  }
  return weights;
}

function getSpotIds(pondId: string): string[] {
  const pond = PONDS.find((p) => p.id === pondId);
  return pond?.spots.map((s) => s.id) ?? [];
}

function pickSpotForPond(pondId: string): string {
  const spotIds = getSpotIds(pondId);
  if (spotIds.length === 0) return 'spot-1';
  return pickSpotForNewFish(spotIds, getHabitatWeights(pondId));
}

function pickSpecies(config: PondStockConfig): FishSpeciesId {
  const pool =
    Math.random() < config.rareSpawnRate ? config.rareSpecies : config.commonSpecies;
  return pool[Math.floor(Math.random() * pool.length)];
}

function emptyQualityCounts(): Record<FishQuality, number> {
  return Object.fromEntries(FISH_QUALITIES.map((q) => [q.id, 0])) as Record<FishQuality, number>;
}

function countFishByQuality(pondId: string): Record<FishQuality, number> {
  const counts = emptyQualityCounts();
  for (const row of listFishStmt.all(pondId) as PondFishRow[]) {
    const quality = row.quality as FishQuality;
    counts[quality] += 1;
  }
  return counts;
}

export function countActiveAnglers(pondId: string): number {
  return listUsersInPond(pondId).filter(
    (u) => !u.isBot && isFishingActive(u.fishingPhase),
  ).length;
}

function insertPondFish(
  pondId: string,
  speciesId: FishSpeciesId,
  quality: FishQuality,
  logPrefix: 'seed' | 'supplement',
): PondFishEntity {
  const species = getSpecies(speciesId);
  const sizeM = rollJuvenileSize(quality, species);
  const biteMultiplier = rollIndividualMultiplier();
  const escapeMultiplier = rollIndividualMultiplier();
  const biteBase = calcQualitySizeBiteRate(quality, sizeM);
  const escapeBase = calcSizeEscapeRate(sizeM);
  const spotId = pickSpotForPond(pondId);
  const bornAt = Date.now();

  const fish: PondFishEntity = {
    id: randomUUID(),
    pondId,
    spotId,
    speciesId,
    quality,
    sizeM,
    bornAt,
    generation: 0,
    biteMultiplier,
    escapeMultiplier,
    birthSizeM: sizeM,
  };

  insertFishStmt.run({
    id: fish.id,
    pondId,
    spotId,
    speciesId,
    quality,
    sizeM,
    bornAt,
    generation: 0,
    biteWeight: null,
    biteMultiplier,
    escapeMultiplier,
    birthSizeM: sizeM,
  });

  const tag = logPrefix === 'supplement' ? 'fish supplement' : 'fish seed';
  if (isEcologyVerbose()) {
    console.log(
      `[${tag}] id=${fish.id.slice(0, 8)} spot=${spotId} quality=${quality} size=${sizeM.toFixed(2)}m ` +
        `biteBase=${formatBiteRatePct(biteBase)} biteMulti=${biteMultiplier.toFixed(2)} ` +
        `escapeBase=${formatBiteRatePct(escapeBase)} escapeMult=${escapeMultiplier.toFixed(2)}`,
    );
  }

  return fish;
}

function createFish(pondId: string, speciesId: FishSpeciesId): PondFishEntity {
  return insertPondFish(pondId, speciesId, rollFishQuality(), 'seed');
}

function createSupplementFish(
  pondId: string,
  config: PondStockConfig,
  actualByQuality: Record<FishQuality, number>,
): PondFishEntity {
  const quality = rollSupplementQuality(actualByQuality, config.maxPopulation);
  return insertPondFish(pondId, pickSpecies(config), quality, 'supplement');
}

function seedPond(pondId: string, count: number): void {
  const config = getPondStockConfig(pondId);
  if (!config) return;
  for (let i = 0; i < count; i++) {
    createFish(pondId, pickSpecies(config));
  }
}

function getPondStateOrDefault(pondId: string): PondStateRow {
  const state = getPondStateStmt.get(pondId) as PondStateRow | undefined;
  return {
    depleted_until: state?.depleted_until ?? null,
    last_weight_refresh: state?.last_weight_refresh ?? 0,
    last_supplement_at: state?.last_supplement_at ?? 0,
    last_migration_at: state?.last_migration_at ?? 0,
  };
}

function doSupplement(pondId: string, config: PondStockConfig): number {
  const currentCount = (countFishStmt.get(pondId) as { c: number }).c;
  const targetCount = Math.floor(config.maxPopulation * POND_SUPPLEMENT_TARGET_RATIO);
  const gap = Math.max(0, targetCount - currentCount);
  if (gap === 0) return 0;

  const supplementN = Math.min(
    POND_SUPPLEMENT_BATCH_MAX,
    Math.max(1, Math.ceil(gap * POND_SUPPLEMENT_BATCH_RATIO)),
  );

  const actualByQuality = countFishByQuality(pondId);
  for (let i = 0; i < supplementN; i++) {
    const added = createSupplementFish(pondId, config, actualByQuality);
    actualByQuality[added.quality] += 1;
  }
  return supplementN;
}

function migrateFishSpots(pondId: string): number {
  const spotIds = getSpotIds(pondId);
  if (spotIds.length === 0) return 0;

  const habitatWeights = getHabitatWeights(pondId);
  const rows = [...(listFishStmt.all(pondId) as PondFishRow[])].sort((a, b) =>
    a.id.localeCompare(b.id),
  );

  let migrated = 0;
  for (const row of rows) {
    if (Math.random() > FISH_MIGRATION_FRACTION) continue;
    const newSpotId = pickMigrationSpot(spotIds, habitatWeights);
    if (newSpotId !== row.spot_id) {
      updateFishSpotStmt.run(newSpotId, row.id);
      migrated += 1;
    }
  }
  return migrated;
}

function trySupplement(pondId: string, config: PondStockConfig, force = false): number {
  const state = getPondStateOrDefault(pondId);
  const activeAnglers = countActiveAnglers(pondId);
  const effectiveCheckMs = calcSupplementCheckMs(activeAnglers);

  if (!force && Date.now() - state.last_supplement_at < effectiveCheckMs) {
    return 0;
  }

  const currentCount = (countFishStmt.get(pondId) as { c: number }).c;
  const supplementN = doSupplement(pondId, config);
  if (supplementN === 0) return 0;

  const gap = Math.max(
    0,
    Math.floor(config.maxPopulation * POND_SUPPLEMENT_TARGET_RATIO) - currentCount,
  );

  upsertPondStateStmt.run({
    pondId,
    depletedUntil: state.depleted_until,
    lastWeightRefresh: state.last_weight_refresh,
    lastSupplementAt: Date.now(),
    lastMigrationAt: state.last_migration_at,
  });

  if (supplementN > 0 && isEcologyVerbose()) {
    console.log(
      `[pond supplement] ${pondId} +${supplementN} active=${activeAnglers} checkMs=${effectiveCheckMs} gap=${gap} now=${currentCount + supplementN}`,
    );
  }
  return supplementN;
}

function refreshSpotWeights(pondId: string): void {
  const pond = PONDS.find((p) => p.id === pondId);
  if (!pond) return;

  const state = getPondStateOrDefault(pondId);
  const cache = new Map<string, number>();
  for (const spot of pond.spots) {
    const weight = round2(Math.random() * 5);
    upsertSpotWeightStmt.run({ pondId, spotId: spot.id, weight });
    cache.set(spot.id, weight);
  }
  spotWeightCache.set(pondId, cache);

  upsertPondStateStmt.run({
    pondId,
    depletedUntil: state.depleted_until,
    lastWeightRefresh: Date.now(),
    lastSupplementAt: state.last_supplement_at,
    lastMigrationAt: state.last_migration_at,
  });
}

export function initPondEcology(): void {
  for (const config of PONDS.map((p) => getPondStockConfig(p.id)).filter(Boolean)) {
    const pondId = config!.pondId;
    const count = (countFishStmt.get(pondId) as { c: number }).c;
    if (count === 0) {
      seedPond(pondId, config!.initialPopulation);
    }

    const state = getPondStateStmt.get(pondId);
    if (!state) {
      upsertPondStateStmt.run({
        pondId,
        depletedUntil: null,
        lastWeightRefresh: 0,
        lastSupplementAt: Date.now(),
        lastMigrationAt: 0,
      });
      refreshSpotWeights(pondId);
    } else if (
      Date.now() - (state as PondStateRow).last_weight_refresh >= SPOT_BITE_WEIGHT_REFRESH_MS
    ) {
      refreshSpotWeights(pondId);
    }
  }
}

export function getSpotBiteWeight(pondId: string, spotId: string): number {
  return getCachedSpotWeights(pondId).get(spotId) ?? 0;
}

export function getPondEcologySummary(pondId: string): PondEcologySummary | null {
  const config = getPondStockConfig(pondId);
  if (!config) return null;

  const fishCount = (countFishStmt.get(pondId) as { c: number }).c;
  const state = getPondStateOrDefault(pondId);
  const depleted = fishCount === 0;

  const spotWeights: Record<string, number> = {};
  for (const [spotId, weight] of getCachedSpotWeights(pondId)) {
    spotWeights[spotId] = weight;
  }

  return {
    pondId,
    fishCount,
    maxPopulation: config.maxPopulation,
    depleted,
    depletedUntil: depleted ? state.depleted_until ?? undefined : undefined,
    commonSpecies: config.commonSpecies,
    rareSpecies: config.rareSpecies,
    spotWeights,
    lastWeightRefresh: state.last_weight_refresh,
  };
}

export function isPondDepleted(pondId: string): boolean {
  return (countFishStmt.get(pondId) as { c: number }).c === 0;
}

export function removePondFish(fishId: string): PondFishEntity | null {
  const row = getFishStmt.get(fishId) as PondFishRow | undefined;
  if (!row) return null;

  deleteFishStmt.run(fishId);
  const config = getPondStockConfig(row.pond_id);
  const remaining = (countFishStmt.get(row.pond_id) as { c: number }).c;

  if (remaining === 0 && config) {
    trySupplement(row.pond_id, config, true);
  }

  return rowToEntity(row);
}

export function applyEscapeGrowthBonus(fishId: string): PondFishEntity | null {
  const row = getFishStmt.get(fishId) as PondFishRow | undefined;
  if (!row) return null;

  const species = getSpecies(row.species_id as FishSpeciesId);
  const quality = row.quality as PondFishEntity['quality'];
  const newSize = calcEscapeGrowthSize(quality, species, row.size_m);
  if (newSize > row.size_m) {
    updateFishSizeStmt.run(newSize, fishId);
    return rowToEntity({ ...row, size_m: newSize });
  }
  return rowToEntity(row);
}

function growAllFish(pondId: string): void {
  const now = Date.now();
  // PERF-02: compute then batch-write (caller holds per-pond txn)
  const updates: Array<{ id: string; sizeM: number }> = [];
  for (const row of listFishStmt.all(pondId) as PondFishRow[]) {
    const species = getSpecies(row.species_id as FishSpeciesId);
    const quality = row.quality as PondFishEntity['quality'];
    const birthSizeM = row.birth_size_m ?? row.size_m;
    const newSize = growFishSizeV2(quality, species, row.size_m, birthSizeM, row.born_at, now);
    if (newSize !== row.size_m) {
      updates.push({ id: row.id, sizeM: newSize });
    }
  }
  for (const u of updates) {
    updateFishSizeStmt.run(u.sizeM, u.id);
  }
}

export function tickPondEcology(pondId: string): void {
  const config = getPondStockConfig(pondId);
  if (!config) return;

  const state = getPondStateOrDefault(pondId);
  if (Date.now() - state.last_weight_refresh >= SPOT_BITE_WEIGHT_REFRESH_MS) {
    refreshSpotWeights(pondId);
  }

  const activeAnglers = countActiveAnglers(pondId);
  const effectiveCheckMs = calcSupplementCheckMs(activeAnglers);
  const now = Date.now();

  if (now - state.last_supplement_at >= effectiveCheckMs) {
    const currentCount = (countFishStmt.get(pondId) as { c: number }).c;
    const gap = Math.max(
      0,
      Math.floor(config.maxPopulation * POND_SUPPLEMENT_TARGET_RATIO) - currentCount,
    );
    const supplementN = doSupplement(pondId, config);
    const migrated = migrateFishSpots(pondId);

    const refreshed = getPondStateOrDefault(pondId);
    upsertPondStateStmt.run({
      pondId,
      depletedUntil: refreshed.depleted_until,
      lastWeightRefresh: refreshed.last_weight_refresh,
      lastSupplementAt: now,
      lastMigrationAt: now,
    });

    if (supplementN > 0 && isEcologyVerbose()) {
      console.log(
        `[pond supplement] ${pondId} +${supplementN} active=${activeAnglers} checkMs=${effectiveCheckMs} gap=${gap} now=${currentCount + supplementN}`,
      );
    }
    if (migrated > 0 && isEcologyVerbose()) {
      console.log(`[pond migration] ${pondId} migrated=${migrated} active=${activeAnglers}`);
    }
  }

  growAllFish(pondId);
}

export function tickAllPonds(): void {
  // PERF-02: per-pond write txn — avoid locking the whole DB for all ponds
  for (const pond of PONDS) {
    db.transaction(() => {
      tickPondEcology(pond.id);
    })();
  }
}

export function listPondFishEntities(pondId: string): PondFishEntity[] {
  return (listFishStmt.all(pondId) as PondFishRow[]).map(rowToEntity);
}

export function listPondFishAtSpot(pondId: string, spotId: string): PondFishEntity[] {
  return (listFishAtSpotStmt.all(pondId, spotId) as PondFishRow[]).map(rowToEntity);
}

export function getPondLastMigrationAt(pondId: string): number {
  return getPondStateOrDefault(pondId).last_migration_at;
}

export function resetAllEcology(): void {
  db.exec('DELETE FROM pond_fish');
  db.exec('DELETE FROM pond_state');
  db.exec('DELETE FROM spot_bite_weights');
  spotWeightCache.clear();
  initPondEcology();
}

export function getAdminPondOverview(): Array<{
  pondId: string;
  summary: PondEcologySummary;
}> {
  return PONDS.map((p) => ({
    pondId: p.id,
    summary: getPondEcologySummary(p.id)!,
  })).filter((x) => x.summary);
}

// Re-export tick interval for callers that import from pondEcology
export { POND_ECOSYSTEM_TICK_MS };
