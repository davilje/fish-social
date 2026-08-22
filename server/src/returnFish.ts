/**
 * FEAT-RETURN-01：回鱼 — 准入校验、删包、塘内增重、发金+XP。
 */
import {
  calcFishReturnGold,
  calcFishSellPrice,
  getQualityMaxSize,
  getReturnRules,
  getSpecies,
  qualityIndex,
  type FishQuality,
  type FishSpeciesId,
} from '@fish-social/shared';
import { getFishById, getInventory, removeFishFromInventory } from './inventory.js';
import { growOrSpawnReturnedFish } from './pondEcology.js';
import { findLivePondUser } from './forbiddenPolice.js';
import { addCoins } from './players.js';
import { grantReturnProgress } from './playerProgress.js';
import { recordFishingMetric } from './fishingMetrics.js';

export type ReturnFishErrorCode =
  | 'NOT_IN_POND'
  | 'QUALITY_TOO_LOW'
  | 'SIZE_OUT_OF_RANGE'
  | 'AT_MAX_SIZE'
  | 'ITEM_NOT_FOUND';

export type ReturnFishOk = {
  ok: true;
  gold: number;
  playerXp: number;
  pondXp: number;
  newSizeM: number;
  sizeGainM: number;
  totalCoins: number;
  items: ReturnType<typeof getInventory>;
};

export type ReturnFishFail = {
  ok: false;
  error: string;
  code: ReturnFishErrorCode;
};

function rollSizeGain(minM: number, maxM: number): number {
  const lo = Math.min(minM, maxM);
  const hi = Math.max(minM, maxM);
  if (hi <= lo) return lo;
  return lo + Math.random() * (hi - lo);
}

export function returnFishToPond(
  playerId: string,
  inventoryItemId: string,
): ReturnFishOk | ReturnFishFail {
  const live = findLivePondUser(playerId);
  if (!live || !live.user.spotId) {
    return {
      ok: false,
      error: '需在当前鱼塘钓位才能回鱼',
      code: 'NOT_IN_POND',
    };
  }

  const fish = getFishById(playerId, inventoryItemId);
  if (!fish) {
    return { ok: false, error: '背包无此鱼', code: 'ITEM_NOT_FOUND' };
  }

  const rules = getReturnRules();
  const minQ = rules.minQuality as FishQuality;
  if (qualityIndex(fish.quality) < qualityIndex(minQ)) {
    return {
      ok: false,
      error: `品质不足（需 ${minQ} 及以上）`,
      code: 'QUALITY_TOO_LOW',
    };
  }

  const species = getSpecies(fish.speciesId);
  const speciesMax = getQualityMaxSize(fish.quality, species);
  if (fish.sizeM >= speciesMax - 1e-9) {
    return {
      ok: false,
      error: '已达最大尺寸，不可回鱼',
      code: 'AT_MAX_SIZE',
    };
  }

  const ratio = speciesMax > 0 ? fish.sizeM / speciesMax : 0;
  if (ratio < rules.minSizeRatio) {
    return {
      ok: false,
      error: '体长过小，暂不可回鱼',
      code: 'SIZE_OUT_OF_RANGE',
    };
  }
  if (ratio >= rules.maxSizeRatio) {
    return {
      ok: false,
      error: '已达最大尺寸，不可回鱼',
      code: 'AT_MAX_SIZE',
    };
  }

  const removed = removeFishFromInventory(playerId, inventoryItemId);
  if (!removed) {
    return { ok: false, error: '背包无此鱼', code: 'ITEM_NOT_FOUND' };
  }

  const sizeGain = rollSizeGain(rules.sizeGainMinM, rules.sizeGainMaxM);
  const grown = growOrSpawnReturnedFish({
    pondId: live.pondId,
    spotId: live.user.spotId,
    speciesId: removed.speciesId as FishSpeciesId,
    quality: removed.quality,
    baseSizeM: removed.sizeM,
    sizeGainM: sizeGain,
  });

  const gold = calcFishReturnGold(removed, rules.goldMulVsSell);
  const totalCoins = addCoins(playerId, gold);
  const xp = grantReturnProgress(
    playerId,
    live.pondId,
    rules.playerXp,
    rules.pondXp,
  );

  recordFishingMetric('fish_returned_to_pond', {
    playerId,
    pondId: live.pondId,
    payload: {
      speciesId: removed.speciesId,
      sizeM: removed.sizeM,
      gold,
      sizeGainM: grown.sizeGainApplied,
      quality: removed.quality,
      newSizeM: grown.entity.sizeM,
      sellGold: calcFishSellPrice(removed),
      spawned: grown.spawned,
    },
  });
  recordFishingMetric('gold_earn', {
    playerId,
    payload: {
      amount: gold,
      source: 'fish_return',
      fishId: inventoryItemId,
      quality: removed.quality,
      sizeM: removed.sizeM,
    },
  });

  return {
    ok: true,
    gold,
    playerXp: xp.playerXpGranted,
    pondXp: xp.pondXpGranted,
    newSizeM: grown.entity.sizeM,
    sizeGainM: grown.sizeGainApplied,
    totalCoins,
    items: getInventory(playerId),
  };
}
