import {
  FISH_QUALITIES,
  PONDS,
  calcEffectiveEscapeRate,
  calcSupplementCheckMs,
  calcSupplementIdealCounts,
  getPondStockConfig,
  getSpeciesDiet,
  isNearMaxSize,
  round2,
  type BaitId,
  type FishQuality,
  type PondFishingDebugResponse,
  type TackleId,
} from '@fish-social/shared';
import { buildSnapshot } from './gameState.js';
import { getPlayerGear } from './gear.js';
import { getPhaseEndsAt } from './fishingStateMachine.js';
import { getSpotBiteTickModel } from './fishingSession.js';
import { getLockedPondFishIds } from './inventory.js';
import { getBiteCheckMs } from './runtimeConfig.js';
import { countActiveAnglers, getPondLastMigrationAt, listPondFishEntities } from './pondEcology.js';

export interface FishingDebugQuery {
  baitId?: BaitId;
  tackleId?: TackleId;
  playerId?: string;
}

function emptyQualityCounts(): Record<FishQuality, number> {
  return Object.fromEntries(FISH_QUALITIES.map((q) => [q.id, 0])) as Record<FishQuality, number>;
}

function resolveDebugGear(query: FishingDebugQuery): {
  baitId: BaitId;
  tackleId: TackleId;
  playerId?: string;
} {
  let baitId: BaitId = query.baitId ?? 'basic';
  let tackleId: TackleId = query.tackleId ?? 'basic';
  const playerId = query.playerId;

  if (playerId) {
    const gear = getPlayerGear(playerId);
    if (gear) {
      if (!query.baitId) baitId = gear.equippedBait;
      if (!query.tackleId) tackleId = gear.equippedTackle;
    }
  }

  return { baitId, tackleId, playerId };
}

export function buildFishingDebugReport(
  pondId: string,
  query: FishingDebugQuery = {},
): PondFishingDebugResponse {
  const pond = PONDS.find((p) => p.id === pondId);
  const fish = listPondFishEntities(pondId);
  const lockedFishIds = [...getLockedPondFishIds()];
  const lockedSet = new Set(lockedFishIds);
  const checkMs = getBiteCheckMs();
  const queryContext = resolveDebugGear(query);
  const gearCtx = {
    equippedBait: queryContext.baitId,
    equippedTackle: queryContext.tackleId,
  };

  const byQuality = emptyQualityCounts();
  for (const f of fish) {
    byQuality[f.quality] += 1;
  }

  const stockConfig = getPondStockConfig(pondId);
  const maxPopulation = stockConfig?.maxPopulation ?? 0;
  const idealByQuality = calcSupplementIdealCounts(maxPopulation);
  const qualitySupplement = Object.fromEntries(
    FISH_QUALITIES.map((q) => [
      q.id,
      {
        actual: byQuality[q.id],
        ideal: round2(idealByQuality[q.id]),
      },
    ]),
  ) as PondFishingDebugResponse['summary']['qualitySupplement'];
  const activeAnglers = countActiveAnglers(pondId);
  const effectiveSupplementCheckMs = calcSupplementCheckMs(activeAnglers);
  const lastMigrationAt = getPondLastMigrationAt(pondId);

  const spots = pond?.spots ?? [];
  const spotReports = spots.map((spot) => {
    const fishAtSpot = fish.filter((f) => f.spotId === spot.id);
    const model = getSpotBiteTickModel(pondId, spot.id, lockedSet, gearCtx);
    if (!model) {
      return {
        spotId: spot.id,
        spotMultiplier: 0,
        tickBiteChance: 0,
        pBite: 0,
        pickedFishId: null,
        fishAtSpotCount: fishAtSpot.length,
        fishContributions: [],
        lockedFishIds,
        spotBite: 0,
      };
    }

    const fishContributions = model.entries
      .map(({ fish: f, effectiveBite, baitBonus, baseBite, qualityPickShare }) => {
        const escapeRate = round2(
          calcEffectiveEscapeRate(
            f.sizeM,
            queryContext.tackleId,
            f.escapeMultiplier ?? 1.0,
          ),
        );
        return {
          fishId: f.id,
          speciesId: f.speciesId,
          quality: f.quality,
          diet: getSpeciesDiet(f.speciesId),
          sizeM: f.sizeM,
          fishBiteRate: baseBite,
          baitBonus,
          spotBiteRate: effectiveBite,
          escapeRate,
          pickShare: qualityPickShare,
          isNearMaxSize: isNearMaxSize(f),
          biteWeight: baseBite,
          effectiveWeight: effectiveBite,
          shareOfTotal: qualityPickShare,
        };
      })
      .filter((row) => row.spotBiteRate > 0 || row.pickShare > 0);

    fishContributions.sort((a, b) => b.pickShare - a.pickShare);

    return {
      spotId: spot.id,
      spotMultiplier: round2(model.spotMultiplier),
      tickBiteChance: model.pBite,
      pBite: model.pBite,
      pickedFishId: model.pickedFishId,
      fishAtSpotCount: fishAtSpot.length,
      fishContributions,
      lockedFishIds,
      spotBite: round2(model.spotMultiplier),
    };
  });

  const avgTickBiteChance =
    spotReports.length > 0
      ? spotReports.reduce((s, sp) => s + sp.tickBiteChance, 0) / spotReports.length
      : 0;

  const snapshot = buildSnapshot(pondId);
  const activeFishers = (snapshot?.users ?? [])
    .filter((u) => u.status === 'fishing' && u.spotId)
    .map((u) => {
      const hooked = u.fishingPhase === 'hooked';
      const gear = u.playerId ? getPlayerGear(u.playerId) : undefined;
      return {
        userId: u.id,
        playerId: u.playerId,
        nickname: u.nickname,
        isBot: !!u.isBot,
        spotId: u.spotId!,
        fishingPhase: u.fishingPhase ?? (hooked ? 'hooked' : u.status),
        phaseEndsAt: u.phaseEndsAt ?? getPhaseEndsAt(pondId, u.id),
        fishingStartedAt: u.fishingStartedAt,
        sessionFishingMs: u.sessionFishingMs ?? 0,
        disconnectedAt: u.disconnectedAt ?? null,
        equippedBaitId: u.equippedBaitId ?? gear?.equippedBait ?? 'basic',
        equippedTackleId: u.equippedTackleId ?? gear?.equippedTackle ?? 'basic',
      };
    });

  return {
    pondId,
    updatedAt: Date.now(),
    queryContext,
    constants: { checkMs, activeAnglers, effectiveSupplementCheckMs, lastMigrationAt },
    spots: spotReports,
    activeFishers,
    summary: {
      totalFish: fish.length,
      byQuality,
      avgTickBiteChance,
      qualitySupplement,
    },
  };
}
