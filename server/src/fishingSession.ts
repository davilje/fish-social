import { randomUUID } from 'crypto';

import type { Server } from 'socket.io';

import {
  FISH_QUALITIES,
  baitBiteBonus,
  calcEffectiveEscapeRate,
  calcHookDurationMs,
  calcQualitySizeBiteRate,
  calcSingleFishBiteProbability,
  getGamePondDef,
  getPondModifier,
  getSpeciesDiet,
  pickSpotFishCandidate,
  rodBiteMultiplier,
  type BaitId,
  type ClientToServerEvents,
  type PendingFishCatch,
  type PondFishEntity,
  type ServerToClientEvents,
  type TackleId,
} from '@fish-social/shared';

import { getLockedPondFishIds } from './inventory.js';
import { isCodexNewForPlayer } from './codex.js';
import {
  getSpotBiteWeight,
  ensureInstantTestFishAtSpot,
  isPondDepleted,
  listPondFishAtSpot,
} from './pondEcology.js';
import { getHookDurationScale, isInstantFishingTestMode } from './runtimeConfig.js';

export type BiteTickResult = { kind: 'none' } | { kind: 'scheduled' };

export interface BiteHookEvent {
  fish: PondFishEntity;
  escaped: boolean;
  hookDurationMs: number;
}

export type RollBiteHookResult =
  | { outcome: 'hooked'; event: BiteHookEvent }
  | { outcome: 'miss'; reason: 'empty' | 'failed'; sampledFish?: PondFishEntity };

export interface FisherGearContext {
  equippedBait: BaitId | string;
  equippedTackle: TackleId | string;
  equippedRod?: string;
  resolveBait?: (speciesId: string) => string;
}

function listBiteCandidates(
  pondId: string,
  spotId: string,
  excludeIds: ReadonlySet<string>,
): PondFishEntity[] {
  if (isPondDepleted(pondId)) return [];
  if (isInstantFishingTestMode()) {
    ensureInstantTestFishAtSpot(pondId, spotId);
  }
  return listPondFishAtSpot(pondId, spotId).filter((f) => !excludeIds.has(f.id));
}

function pondRateMuls(pondId: string): { bite: number; escape: number; qualitySkew: number } {
  const def = getGamePondDef(pondId);
  if (!def) return { bite: 1, escape: 1, qualitySkew: 1 };
  const mod = getPondModifier(def.pondCategory);
  return {
    bite: mod.biteRateMul,
    escape: mod.escapeRateMul,
    qualitySkew: mod.qualityWeightSkew,
  };
}

function qualityPickWeight(quality: PondFishEntity['quality'], skew = 1): number {
  const base = FISH_QUALITIES.find((q) => q.id === quality)?.weight ?? 1;
  // skew < 1 pushes toward lower qualities by raising gray/green relative weight
  if (skew >= 1) return base;
  const idx = FISH_QUALITIES.findIndex((q) => q.id === quality);
  const lowBoost = 1 + (1 - skew) * Math.max(0, 3 - idx);
  return base * lowBoost;
}

function buildSpotFishDebugEntries(
  pondId: string,
  spotId: string,
  excludeIds: ReadonlySet<string>,
  gear?: FisherGearContext,
): Array<{
  fish: PondFishEntity;
  effectiveBite: number;
  baitBonus: number;
  baseBite: number;
  qualityPickShare: number;
}> | null {
  const candidates = listBiteCandidates(pondId, spotId, excludeIds);
  if (candidates.length === 0) return null;

  const spotMultiplier = getSpotBiteWeight(pondId, spotId);
  const equippedBait = gear?.equippedBait ?? 'basic';
  const muls = pondRateMuls(pondId);
  const pickWeights = candidates.map((fish) => qualityPickWeight(fish.quality, muls.qualitySkew));
  const totalPickWeight = pickWeights.reduce((a, b) => a + b, 0);

  return candidates.map((fish, index) => {
    const bonus = baitBiteBonus(equippedBait, fish.speciesId);
    const baseBite =
      calcQualitySizeBiteRate(fish.quality, fish.sizeM) * (fish.biteMultiplier ?? 1.0);
    const rodMul = rodBiteMultiplier(
      gear?.equippedRod ?? String(gear?.equippedTackle ?? ''),
      fish.quality,
      fish.speciesId,
    );
    const effectiveBite =
      calcSingleFishBiteProbability(fish, spotMultiplier, bonus) * muls.bite * rodMul;
    const qualityPickShare = totalPickWeight > 0 ? pickWeights[index]! / totalPickWeight : 0;
    return { fish, effectiveBite, baitBonus: bonus, baseBite, qualityPickShare };
  });
}

/** 供 debug 面板复用的钓点咬钩模型（v0.4.1 单鱼抽样） */
export function getSpotBiteTickModel(
  pondId: string,
  spotId: string,
  excludeIds: ReadonlySet<string> = getLockedPondFishIds(),
  gear?: FisherGearContext,
): {
  spotMultiplier: number;
  entries: Array<{
    fish: PondFishEntity;
    effectiveBite: number;
    baitBonus: number;
    baseBite: number;
    qualityPickShare: number;
  }>;
  pickedFishId: string | null;
  pBite: number;
  tickBiteChance: number;
} | null {
  const entries = buildSpotFishDebugEntries(pondId, spotId, excludeIds, gear);
  if (!entries) return null;

  const spotMultiplier = getSpotBiteWeight(pondId, spotId);
  const picked = pickSpotFishCandidate(entries.map((e) => e.fish));
  const pickedEntry = entries.find((e) => e.fish.id === picked.id) ?? entries[0]!;
  const pBite = pickedEntry.effectiveBite;

  return {
    spotMultiplier,
    entries,
    pickedFishId: picked.id,
    pBite,
    tickBiteChance: pBite,
  };
}

/** v0.4.1：按品质抽 1 条鱼，再判单次咬钩概率 */
export function rollBiteHook(
  pondId: string,
  spotId: string,
  excludeIds: ReadonlySet<string> = getLockedPondFishIds(),
  gear?: FisherGearContext,
): RollBiteHookResult {
  const candidates = listBiteCandidates(pondId, spotId, excludeIds);
  if (candidates.length === 0) {
    return { outcome: 'miss', reason: 'empty' };
  }

  const spotMultiplier = getSpotBiteWeight(pondId, spotId);
  const muls = pondRateMuls(pondId);
  const target = pickSpotFishCandidate(candidates);
  const equippedBait = gear?.resolveBait
    ? gear.resolveBait(target.speciesId)
    : (gear?.equippedBait ?? 'bait-basic');
  const baitBonus = baitBiteBonus(equippedBait, target.speciesId);
  const rodMul = rodBiteMultiplier(
    gear?.equippedRod ?? String(gear?.equippedTackle ?? ''),
    target.quality,
    target.speciesId,
  );
  const pBite =
    calcSingleFishBiteProbability(target, spotMultiplier, baitBonus) * muls.bite * rodMul;

  if (!isInstantFishingTestMode() && Math.random() >= pBite) {
    return { outcome: 'miss', reason: 'failed', sampledFish: target };
  }

  const tackleId = gear?.equippedRod || gear?.equippedTackle || 'basic';
  const escaped =
    !isInstantFishingTestMode() &&
    Math.random() <
      calcEffectiveEscapeRate(target.sizeM, tackleId, target.escapeMultiplier ?? 1.0) *
        muls.escape;
  const hookDurationMs = Math.round(
    (isInstantFishingTestMode() ? 1000 : calcHookDurationMs(target.quality, target.sizeM, target.speciesId)) *
      getHookDurationScale(),
  );

  return { outcome: 'hooked', event: { fish: target, escaped, hookDurationMs } };
}

export function buildCatchData(
  fish: PondFishEntity,
  hookDurationMs: number,
  playerId?: string,
): PendingFishCatch {
  const isCodexNew =
    playerId !== undefined && isCodexNewForPlayer(playerId, fish.speciesId);

  return {
    catchId: randomUUID(),
    pondFishId: fish.id,
    speciesId: fish.speciesId,
    quality: fish.quality,
    sizeM: fish.sizeM,
    hookDurationMs,
    ...(isCodexNew ? { isCodexNew: true } : {}),
  };
}

export function isFishingV2Enabled(): boolean {
  return process.env.FISHING_V2 !== 'false';
}

export { getSpeciesDiet };
