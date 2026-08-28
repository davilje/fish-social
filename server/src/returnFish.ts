/**
 * FEAT-RETURN-01 + FEAT-RETURN-02：回鱼 — 准入校验、删包、塘内增重、发金+XP；自动回鱼触发。
 */
import {
  calcFishReturnGold,
  calcFishSellPrice,
  FISH_QUALITIES,
  getGamePondDef,
  getReturnRules,
  isReturnEligible,
  pondAllowsReturnFish,
  type FishQuality,
  type FishSpeciesId,
} from '@fish-social/shared';
import { getFishById, getInventory, removeFishFromInventory } from './inventory.js';
import { growOrSpawnReturnedFish } from './pondEcology.js';
import { findLivePondUser } from './forbiddenPolice.js';
import { addCoins } from './players.js';
import { grantReturnProgress } from './playerProgress.js';
import { recordFishingMetric } from './fishingMetrics.js';
import { addAlbumCandidate } from './album.js';
import { tryUnlockAchievements } from './achievements.js';

export type ReturnFishErrorCode =
  | 'NOT_IN_POND'
  | 'POND_NO_RETURN'
  | 'QUALITY_TOO_LOW'
  | 'SIZE_OUT_OF_RANGE'
  | 'WEIGHT_TOO_LOW'
  | 'AT_MAX_SIZE'
  | 'ITEM_NOT_FOUND'
  | 'SELL_ONLY_MODE'
  | 'AUTO_RETURN_MODE';

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

export type AutoReturnSkip = {
  ok: false;
  skipped: true;
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
  opts?: { auto?: boolean },
): ReturnFishOk | ReturnFishFail {
  const live = findLivePondUser(playerId);
  if (!live || !live.user.spotId) {
    return {
      ok: false,
      error: '需在当前鱼塘钓位才能回鱼',
      code: 'NOT_IN_POND',
    };
  }

  const pondDef = getGamePondDef(live.pondId);
  if (!pondAllowsReturnFish(pondDef)) {
    return {
      ok: false,
      error: '本塘不支持回鱼（仅收费塘可回鱼）',
      code: 'POND_NO_RETURN',
    };
  }

  if (!opts?.auto) {
    if (live.user.returnFeeMode === 'sell_only') {
      return {
        ok: false,
        error: '本局为出售档，不可回鱼',
        code: 'SELL_ONLY_MODE',
      };
    }
    if (live.user.returnFeeMode === 'auto_return') {
      return {
        ok: false,
        error: '本局为自动回鱼档，达标鱼将自动回塘',
        code: 'AUTO_RETURN_MODE',
      };
    }
  }

  const fish = getFishById(playerId, inventoryItemId);
  if (!fish) {
    return { ok: false, error: '背包无此鱼', code: 'ITEM_NOT_FOUND' };
  }

  const rules = getReturnRules();
  const minJin = rules.minWeightJin ?? 10;
  const minQ = (rules.minQuality ?? rules.autoMinQuality ?? 'purple') as FishQuality;
  const minQName = FISH_QUALITIES.find((q) => q.id === minQ)?.name ?? '史诗';
  const fishRank = FISH_QUALITIES.findIndex((q) => q.id === fish.quality);
  const minRank = FISH_QUALITIES.findIndex((q) => q.id === minQ);

  if (!isReturnEligible(fish, rules)) {
    if (fishRank < 0 || minRank < 0 || fishRank < minRank) {
      return {
        ok: false,
        error: `品质不足（需${minQName}及以上）`,
        code: 'QUALITY_TOO_LOW',
      };
    }
    return {
      ok: false,
      error: `体重过轻（需达到 ${minJin} 斤）`,
      code: 'WEIGHT_TOO_LOW',
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

  const gold = calcFishReturnGold(removed, {
    goldMulVsSell: rules.goldMulVsSell,
    goldMulHeavy: rules.goldMulHeavy,
    minWeightJin: rules.minWeightJin,
    heavyWeightJin: rules.heavyWeightJin,
  });
  const totalCoins = addCoins(playerId, gold);
  const xp = grantReturnProgress(
    playerId,
    live.pondId,
    rules.playerXp,
    rules.pondXp,
  );

  const metricPayload = {
    speciesId: removed.speciesId,
    sizeM: removed.sizeM,
    gold,
    sizeGainM: grown.sizeGainApplied,
    quality: removed.quality,
    newSizeM: grown.entity.sizeM,
    sellGold: calcFishSellPrice(removed),
    spawned: grown.spawned,
    auto: opts?.auto === true,
  };

  recordFishingMetric('fish_returned_to_pond', {
    playerId,
    pondId: live.pondId,
    payload: metricPayload,
  });
  if (opts?.auto) {
    recordFishingMetric('fish_auto_returned', {
      playerId,
      pondId: live.pondId,
      payload: metricPayload,
    });
  }
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

  addAlbumCandidate({
    playerId,
    speciesId: removed.speciesId,
    quality: removed.quality,
    sizeM: removed.sizeM,
    pondId: live.pondId,
    source: 'return',
    inventoryItemId,
  });
  tryUnlockAchievements(playerId);

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

export function tryAutoReturnFish(
  playerId: string,
  inventoryItemId: string,
): ReturnFishOk | ReturnFishFail | AutoReturnSkip {
  const live = findLivePondUser(playerId);
  if (!live || live.user.returnFeeMode !== 'auto_return' || !live.user.spotId) {
    return { ok: false, skipped: true };
  }
  const pondDef = getGamePondDef(live.pondId);
  if (!pondAllowsReturnFish(pondDef)) {
    return { ok: false, skipped: true };
  }
  const fish = getFishById(playerId, inventoryItemId);
  if (!fish || !isReturnEligible(fish, getReturnRules())) {
    return { ok: false, skipped: true };
  }
  const result = returnFishToPond(playerId, inventoryItemId, { auto: true });
  if (result.ok) return result;
  return result;
}
