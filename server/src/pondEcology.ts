import { randomUUID } from 'crypto';

import {
  POND_ECOSYSTEM_TICK_MS,
  createFishEntryGrowth,
  calcEntryFishLength,
  isFishEntryGrowthState,
  calcPondSupplementCount,
  getPondEcologyDayKey,
  PONDS,
  SPOT_BITE_WEIGHT_REFRESH_MS,
  FISH_MIGRATION_FRACTION,
  FISH_MIGRATION_CHECK_MS,
  FISH_QUALITIES,
  calcSupplementCheckMs,
  getPondStockConfig,
  pondAllowsEcologySupplement,
  pickSpawnFish,
  pickSupplementFish,
  isFishingActive,
  pickMigrationSpot,
  pickSpotForNewFish,
  rollIndividualMultiplier,
  round2,
  roundFishLength,
  type FishQuality,
  type FishSpeciesId,
  type PondEcologySummary,
  type PondFishEntity,
  type PondStockConfig,
} from '@fish-social/shared';

import { db } from './db.js';
import {
  fishRowToEntity as rowToEntity, readFishEntity, recordFishEscape, archiveCapturedFish,
  readCapturedFish, applyReturnGrowth, getHookLockedFishIds, feedGroundbaitAtSpot,
  type PondFishRow,
} from './fishLifecycle.js';
import { listUsersInPond } from './gameState.js';
import { logStructuredEvent } from './fishingObservability.js';

interface PondStateRow {
  depleted_until: number | null;
  last_weight_refresh: number;
  last_supplement_at: number;
  last_migration_at: number;
  last_simulated_at: number;
  last_daily_settlement_key: string | null;
  initial_stocking_batch_id: string | null;
}

const insertFishStmt = db.prepare(`
  INSERT INTO pond_fish (id, pond_id, spot_id, species_id, quality, size_m, born_at, generation,
    bite_weight, bite_multiplier, escape_multiplier, birth_size_m, entered_at, entry_size_m,
    juvenile_entry_size_m, individual_cap_m, growth_bonus_minutes, growth_rule_version, source, stocking_batch_id, out_of_pond_ms)
  VALUES (@id, @pondId, @spotId, @speciesId, @quality, @sizeM, @bornAt, @generation,
    @biteWeight, @biteMultiplier, @escapeMultiplier, @birthSizeM, @enteredAt, @entrySizeM,
    @juvenileEntrySizeM, @individualCapM, @growthBonusMinutes, @growthRuleVersion, @source, @stockingBatchId, @outOfPondMs)
`);

function persistFish(fish: PondFishEntity): void {
  insertFishStmt.run({ ...fish, biteWeight: fish.biteWeight ?? null,
    biteMultiplier: fish.biteMultiplier ?? 1, escapeMultiplier: fish.escapeMultiplier ?? 1,
    birthSizeM: isFishEntryGrowthState(fish) ? null : fish.birthSizeM ?? fish.sizeM,
    enteredAt: fish.enteredAt ?? null, entrySizeM: fish.entrySizeM ?? null,
    juvenileEntrySizeM: fish.juvenileEntrySizeM ?? null, individualCapM: fish.individualCapM ?? null,
    growthBonusMinutes: fish.growthBonusMinutes ?? 0, growthRuleVersion: fish.growthRuleVersion ?? null,
    source: fish.source ?? null, stockingBatchId: fish.stockingBatchId ?? null,
    outOfPondMs: fish.outOfPondMs ?? 0 });
}

const deleteFishStmt = db.prepare('DELETE FROM pond_fish WHERE id = ?');
const countFishStmt = db.prepare('SELECT COUNT(*) as c FROM pond_fish WHERE pond_id = ?');
const listFishStmt = db.prepare('SELECT * FROM pond_fish WHERE pond_id = ?');
const listFishAtSpotStmt = db.prepare(
  'SELECT * FROM pond_fish WHERE pond_id = ? AND spot_id = ?',
);
const updateFishSizeStmt = db.prepare('UPDATE pond_fish SET size_m = ? WHERE id = ?');
const updateFishSpotStmt = db.prepare('UPDATE pond_fish SET spot_id = ? WHERE id = ?');

const upsertPondStateStmt = db.prepare(`
  INSERT INTO pond_state (pond_id, depleted_until, last_weight_refresh, last_supplement_at, last_migration_at, last_simulated_at)
  VALUES (@pondId, @depletedUntil, @lastWeightRefresh, @lastSupplementAt, @lastMigrationAt, @lastSimulatedAt)
  ON CONFLICT(pond_id) DO UPDATE SET
    depleted_until = excluded.depleted_until,
    last_weight_refresh = excluded.last_weight_refresh,
    last_supplement_at = excluded.last_supplement_at,
    last_migration_at = excluded.last_migration_at,
    last_simulated_at = excluded.last_simulated_at
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

function pickPoolOrFallback(pondId: string, config: PondStockConfig): {
  speciesId: FishSpeciesId;
  quality: FishQuality;
} {
  const picked = pickSpawnFish(pondId);
  if (!picked) throw new Error(`No legal initial fish species/quality for ${pondId}`);
  return picked;
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

/** Local instant-fishing mode: keep a manually selected spot testable after depletion. */
export function ensureInstantTestFishAtSpot(pondId: string, spotId: string): boolean {
  if (listPondFishAtSpot(pondId, spotId).length > 0) return true;
  const config = getPondStockConfig(pondId);
  if (!config) return false;
  const actualByQuality = countFishByQuality(pondId);
  const fish = createSupplementFish(pondId, config, actualByQuality);
  if (!fish) return false;
  if (fish.spotId !== spotId) {
    db.prepare('UPDATE pond_fish SET spot_id = ? WHERE id = ?').run(spotId, fish.id);
  }
  return true;
}

function insertPondFish(
  pondId: string, speciesId: FishSpeciesId, quality: FishQuality,
  logPrefix: 'seed' | 'supplement', at = Date.now(), forcedSpotId?: string, batchId?: string,
): PondFishEntity {
  const id = randomUUID();
  const growth = createFishEntryGrowth(quality, id, at, logPrefix === 'seed' ? 'pre_opening_seed' : 'supplement');
  const fish: PondFishEntity = {
    ...growth, stockingBatchId: batchId, id, pondId, spotId: forcedSpotId ?? pickSpotForPond(pondId),
    speciesId, quality, sizeM: roundFishLength(calcEntryFishLength(quality, growth, at)),
    bornAt: 0, generation: 0, biteMultiplier: rollIndividualMultiplier(), escapeMultiplier: rollIndividualMultiplier(),
  };
  persistFish(fish);
  return fish;
}

/** Debug fish use an explicit entry length, never a fabricated biological birth. */
export function spawnDebugHookFish(pondId: string, spotId: string, quality: FishQuality, sizeRatio = 0.92): PondFishEntity | null {
  const config = getPondStockConfig(pondId);
  if (!config) return null;
  const picked = pickPoolOrFallback(pondId, config);
  const id = randomUUID();
  const growth = createFishEntryGrowth(quality, id, Date.now(), 'debug');
  growth.entrySizeM = roundFishLength(Math.max(growth.juvenileEntrySizeM,
    Math.min(growth.individualCapM, growth.individualCapM * sizeRatio)));
  const fish: PondFishEntity = { ...growth, id, pondId, spotId, speciesId: picked.speciesId, quality,
    sizeM: growth.entrySizeM, bornAt: 0, generation: 0,
    biteMultiplier: rollIndividualMultiplier(), escapeMultiplier: rollIndividualMultiplier() };
  persistFish(fish);
  return fish;
}

function createSupplementFish(pondId: string, config: PondStockConfig,
  actualByQuality: Record<FishQuality, number>, at = Date.now(), batchId?: string): PondFishEntity | null {
  const picked = pickSupplementFish(pondId, actualByQuality, config.maxPopulation);
  if (!picked) return null; // Never bypass the quality inventory cap via a spawn fallback.
  return insertPondFish(pondId, picked.speciesId, picked.quality, 'supplement', at, undefined, batchId);
}

function seedPond(pondId: string, count: number, at = Date.now(), batchId = `opening:${pondId}`): void {
  const config = getPondStockConfig(pondId);
  if (!config) return;
  for (let i = 0; i < count; i++) {
    const picked = pickPoolOrFallback(pondId, config);
    insertPondFish(pondId, picked.speciesId, picked.quality, 'seed', at, undefined, batchId);
  }
}

function getPondStateOrDefault(pondId: string): PondStateRow {
  const state = getPondStateStmt.get(pondId) as PondStateRow | undefined;
  return {
    depleted_until: state?.depleted_until ?? null,
    last_weight_refresh: state?.last_weight_refresh ?? 0,
    last_supplement_at: state?.last_supplement_at ?? 0,
    last_migration_at: state?.last_migration_at ?? 0,
    last_simulated_at: state?.last_simulated_at ?? 0,
    last_daily_settlement_key: state?.last_daily_settlement_key ?? null,
    initial_stocking_batch_id: state?.initial_stocking_batch_id ?? null,
  };
}

function doSupplement(pondId: string, config: PondStockConfig, at = Date.now(), daily = false): number {
  if (!pondAllowsEcologySupplement(pondId)) return 0;
  const currentCount = (countFishStmt.get(pondId) as { c: number }).c;
  const gap = calcPondSupplementCount(currentCount, daily, config.maxPopulation);
  const counts = countFishByQuality(pondId);
  const batchId = `${daily ? 'daily' : 'hourly'}:${pondId}:${at}`;
  let added = 0;
  for (let i = 0; i < gap; i++) {
    const fish = createSupplementFish(pondId, config, counts, at, batchId);
    if (!fish) throw new Error(`No legal supplement candidate for ${pondId}`);
    counts[fish.quality]++;
    added++;
  }
  return added;
}

function migrateFishSpots(pondId: string, random: () => number = Math.random, at = Date.now()): number {
  const spotIds = getSpotIds(pondId);
  if (spotIds.length === 0) return 0;

  const habitatWeights = getHabitatWeights(pondId);
  const rows = [...(listFishStmt.all(pondId) as PondFishRow[])].sort((a, b) =>
    a.id.localeCompare(b.id),
  );

  const locked = getHookLockedFishIds(at);
  const pending = new Set((db.prepare('SELECT pond_fish_id FROM pending_catch_locks').all() as Array<{pond_fish_id:string}>).map(r=>r.pond_fish_id));
  let migrated = 0;
  for (const row of rows) {
    if (locked.has(row.id) || pending.has(row.id)) continue;
    if (random() > FISH_MIGRATION_FRACTION) continue;
    const newSpotId = pickMigrationSpot(spotIds, habitatWeights, random);
    if (newSpotId !== row.spot_id) {
      updateFishSpotStmt.run(newSpotId, row.id);
      migrated += 1;
    }
  }
  return migrated;
}

function refreshSpotWeights(pondId: string, atMs: number = Date.now()): void {
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
    lastWeightRefresh: atMs,
    lastSupplementAt: state.last_supplement_at,
    lastMigrationAt: state.last_migration_at,
    lastSimulatedAt: state.last_simulated_at,
  });
}

/** Shanghai ecological day changes at 04:00, independently of player reward resets. */
export function ecologyDayKey(at: number): string {
  return getPondEcologyDayKey(at);
}

export function initPondEcology(): void {
  const at = Date.now();
  for (const config of PONDS.map(p => getPondStockConfig(p.id)).filter(Boolean)) {
    const pondId = config!.pondId;
    db.transaction(() => {
      const count = (countFishStmt.get(pondId) as {c:number}).c;
      const existing = getPondStateStmt.get(pondId) as PondStateRow | undefined;
      // A depleted, previously initialized pond must never be reseeded on restart.
      if (!existing) {
        const batchId = `opening:${pondId}:${at}`;
        upsertPondStateStmt.run({ pondId, depletedUntil: null, lastWeightRefresh: 0,
          lastSupplementAt: at, lastMigrationAt: at, lastSimulatedAt: at });
        db.prepare(`UPDATE pond_state SET initial_stocking_batch_id=?, last_daily_settlement_key=? WHERE pond_id=?`)
          .run(count === 0 ? batchId : `legacy:${pondId}`, ecologyDayKey(at), pondId);
        refreshSpotWeights(pondId, at);
        if (count === 0) seedPond(pondId, config!.initialPopulation, at, batchId);
      } else if (!existing.initial_stocking_batch_id) {
        db.prepare(`UPDATE pond_state SET initial_stocking_batch_id=?,
          last_daily_settlement_key=COALESCE(last_daily_settlement_key,?) WHERE pond_id=?`)
          .run(`legacy:${pondId}`, ecologyDayKey(at), pondId);
      }
    })();
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

export function getPondFishById(fishId: string, at = Date.now()): PondFishEntity | null {
  return readFishEntity(fishId, at);
}

export function removePondFish(fishId: string, capture?: { catchId: string; caughtAt: number; sizeM: number }): PondFishEntity | null {
  return db.transaction(() => {
    const fish = readFishEntity(fishId, capture?.caughtAt);
    if (!fish) return null;
    if (capture) {
      fish.sizeM = capture.sizeM;
      archiveCapturedFish(fish, capture.catchId, capture.caughtAt);
    }
    deleteFishStmt.run(fishId);
    // Only scheduled low-water/day settlement replenishes; capture never triggers an extra batch.
    return fish;
  })();
}

/** E0: retained public name for older callers; records an escape, never multiplies size. */
export function applyEscapeGrowthBonus(fishId: string, catchId: string = randomUUID(), at = Date.now(), spotId?: string): PondFishEntity | null {
  return recordFishEscape(fishId, catchId, at, spotId);
}

export function growOrSpawnReturnedFish(opts: {
  pondId: string; spotId: string; speciesId: FishSpeciesId; quality: FishQuality;
  baseSizeM: number; sizeGainM: number; fishId?: string; returnEventId?: string; caughtAt?: number;
}): { entity: PondFishEntity; sizeGainApplied: number; spawned: boolean } {
  return db.transaction(() => {
    const at = Date.now();
    const eventId = opts.returnEventId ?? `return:${randomUUID()}`;
    const archived = opts.fishId ? readCapturedFish(opts.fishId) : null;
    if (opts.fishId && readFishEntity(opts.fishId, at)) throw new Error('Fish is already present in a pond');
    let fish: PondFishEntity;
    if (archived) {
      fish = { ...archived, pondId: opts.pondId, spotId: opts.spotId, sizeM: opts.baseSizeM };
      if (isFishEntryGrowthState(fish)) {
        if (archived.pondId === opts.pondId) {
          fish.outOfPondMs = (fish.outOfPondMs ?? 0) + Math.max(0, at-(opts.caughtAt ?? at));
        } else {
          // A real return to another pond starts a new stay; earlier stays remain archived.
          fish.enteredAt = at; fish.entrySizeM = opts.baseSizeM; fish.growthBonusMinutes = 0;
          fish.outOfPondMs = 0; fish.source = 'returned';
        }
      }
    } else {
      // Historical owned fish have no trustworthy identity/entry record. Preserve size and legacy status.
      fish = { id: opts.fishId ?? randomUUID(), pondId: opts.pondId, spotId: opts.spotId,
        speciesId: opts.speciesId, quality: opts.quality, sizeM: opts.baseSizeM, bornAt: at,
        birthSizeM: opts.baseSizeM, generation: 0,
        biteMultiplier: rollIndividualMultiplier(), escapeMultiplier: rollIndividualMultiplier() };
    }
    persistFish(fish);
    const before = readFishEntity(fish.id, at)!.sizeM;
    const grown = applyReturnGrowth(fish.id, eventId, Math.max(0, opts.sizeGainM), at)?.fish ?? fish;
    return { entity: grown, sizeGainApplied: roundFishLength(Math.max(0, grown.sizeM-before)), spawned: true };
  })();
}

function snapshotFishLengths(pondId: string, at: number): void {
  for (const row of listFishStmt.all(pondId) as PondFishRow[]) {
    const size = rowToEntity(row, at).sizeM;
    if (size !== row.size_m) updateFishSizeStmt.run(size, row.id);
  }
}

export function tickPondEcology(pondId: string, atMs = Date.now(),
  opts?: { activeAnglers?: number; random?: () => number; offline?: boolean }): void {
  const config = getPondStockConfig(pondId);
  if (!config) return;
  db.transaction(() => {
    const state = getPondStateOrDefault(pondId);
    if (atMs < state.last_simulated_at) return;
    const random = opts?.random ?? Math.random;
    let lastWeight = state.last_weight_refresh;
    let lastSupplement = state.last_supplement_at;
    let lastMigration = state.last_migration_at;
    const day = ecologyDayKey(atMs);
    const daily = state.last_daily_settlement_key != null && state.last_daily_settlement_key < day;
    const hourDue = atMs-lastSupplement >= calcSupplementCheckMs(opts?.activeAnglers ?? 0);
    const migrationDue = atMs-lastMigration >= FISH_MIGRATION_CHECK_MS;
    if (atMs-lastWeight >= SPOT_BITE_WEIGHT_REFRESH_MS) {
      refreshSpotWeights(pondId, atMs); lastWeight = atMs;
    }
    if (daily || hourDue) {
      doSupplement(pondId, config, atMs, daily);
      lastSupplement = atMs;
    }
    if (daily) snapshotFishLengths(pondId, atMs);
    if (migrationDue && !opts?.offline) migrateFishSpots(pondId, random, atMs);
    if (migrationDue || lastMigration <= 0) lastMigration = atMs;
    if (!opts?.offline) {
      const occupiedSpots = new Set(listUsersInPond(pondId)
        .filter(u=>!u.isBot && u.fishingPhase !== 'disconnected' && u.spotId).map(u=>u.spotId!));
      for (const spotId of occupiedSpots) feedGroundbaitAtSpot(pondId, spotId, atMs, random);
    }
    // No full fish writes every 30 seconds. Persist scheduling anchors only when due or waking.
    if (daily || hourDue || migrationDue || lastWeight !== state.last_weight_refresh || opts?.offline) {
      upsertPondStateStmt.run({ pondId, depletedUntil: state.depleted_until,
        lastWeightRefresh: lastWeight, lastSupplementAt: lastSupplement,
        lastMigrationAt: lastMigration, lastSimulatedAt: atMs });
      db.prepare('UPDATE pond_state SET last_daily_settlement_key=? WHERE pond_id=?').run(day, pondId);
      db.prepare('DELETE FROM fish_hook_locks WHERE expires_at<=?').run(atMs);
    }
  })();
}

export interface PondEcologyCatchupResult {
  pondId: string;
  offlineMs: number;
  replaySteps: number;
  migrated: number;
  supplemented: number;
  durationMs: number;
  catchupCompacted: boolean;
}

export function ensurePondEcologyCurrent(pondId: string, atMs = Date.now()): PondEcologyCatchupResult | null {
  if (!getPondStockConfig(pondId)) return null;
  const started = Date.now();
  const result = db.transaction(() => {
    const state = getPondStateOrDefault(pondId);
    const baseline = state.last_simulated_at || atMs;
    const offlineMs = Math.max(0, atMs-baseline);
    const before = (countFishStmt.get(pondId) as {c:number}).c;
    if (offlineMs > 0) tickPondEcology(pondId, atMs, { offline: true, activeAnglers: 0 });
    const after = (countFishStmt.get(pondId) as {c:number}).c;
    return { pondId, offlineMs, replaySteps: offlineMs>0 ? 1 : 0, migrated: 0,
      supplemented: Math.max(0, after-before), durationMs: Date.now()-started,
      catchupCompacted: offlineMs>POND_ECOSYSTEM_TICK_MS };
  })();
  logStructuredEvent('pond_ecology', 'pond_ecology_catchup', { eventType:'pond_ecology_catchup', ...result });
  return result;
}

export function tickAllPonds(): void {
  // PERF-05: empty ponds remain asleep; their state is caught up on wake.
  for (const pond of PONDS) {
    const hasConnectedHuman = listUsersInPond(pond.id).some(
      (user) => !user.isBot && user.fishingPhase !== 'disconnected',
    );
    if (!hasConnectedHuman) continue;
    db.transaction(() => {
      tickPondEcology(pond.id);
    })();
  }
}

export function listPondFishEntities(pondId: string, at = Date.now()): PondFishEntity[] {
  return (listFishStmt.all(pondId) as PondFishRow[]).map(row => rowToEntity(row, at));
}

export function listPondFishAtSpot(pondId: string, spotId: string, at = Date.now()): PondFishEntity[] {
  return (listFishAtSpotStmt.all(pondId, spotId) as PondFishRow[]).map(row => rowToEntity(row, at));
}

export function getPondLastMigrationAt(pondId: string): number {
  return getPondStateOrDefault(pondId).last_migration_at;
}

export function resetAllEcology(): void {
  if (process.env.DB_PATH !== ':memory:' && process.env.NODE_ENV !== 'test') {
    throw new Error('Use the offline FISH-ECOLOGY-01 backup/rebuild tool for a persistent database');
  }
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
