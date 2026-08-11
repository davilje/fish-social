import { randomUUID } from 'crypto';

import type { Server } from 'socket.io';

import {
  FISH_QUALITIES,
  baitBiteBonus,
  calcEffectiveEscapeRate,
  calcHookDurationMs,
  calcQualitySizeBiteRate,
  calcSingleFishBiteProbability,
  getSpeciesDiet,
  pickSpotFishCandidate,
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
  isPondDepleted,
  listPondFishAtSpot,
} from './pondEcology.js';
import { getHookDurationScale } from './runtimeConfig.js';

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
  equippedBait: BaitId;
  equippedTackle: TackleId;
}

function listBiteCandidates(
  pondId: string,
  spotId: string,
  excludeIds: ReadonlySet<string>,
): PondFishEntity[] {
  if (isPondDepleted(pondId)) return [];
  return listPondFishAtSpot(pondId, spotId).filter((f) => !excludeIds.has(f.id));
}

function qualityPickWeight(quality: PondFishEntity['quality']): number {
  return FISH_QUALITIES.find((q) => q.id === quality)?.weight ?? 1;
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
  const pickWeights = candidates.map((fish) => qualityPickWeight(fish.quality));
  const totalPickWeight = pickWeights.reduce((a, b) => a + b, 0);

  return candidates.map((fish, index) => {
    const bonus = baitBiteBonus(equippedBait, fish.speciesId);
    const baseBite =
      calcQualitySizeBiteRate(fish.quality, fish.sizeM) * (fish.biteMultiplier ?? 1.0);
    const effectiveBite = calcSingleFishBiteProbability(fish, spotMultiplier, bonus);
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
  const equippedBait = gear?.equippedBait ?? 'basic';
  const target = pickSpotFishCandidate(candidates);
  const baitBonus = baitBiteBonus(equippedBait, target.speciesId);
  const pBite = calcSingleFishBiteProbability(target, spotMultiplier, baitBonus);

  if (Math.random() >= pBite) {
    return { outcome: 'miss', reason: 'failed', sampledFish: target };
  }

  const tackleId = gear?.equippedTackle ?? 'basic';
  const escaped =
    Math.random() <
    calcEffectiveEscapeRate(target.sizeM, tackleId, target.escapeMultiplier ?? 1.0);
  const hookDurationMs = Math.round(
    calcHookDurationMs(target.quality, target.sizeM, target.speciesId) * getHookDurationScale(),
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
