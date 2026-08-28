import type { FishQuality } from './fish';
import { FISH_QUALITIES } from './fish';
import { calcFishWeightJin } from './fishing';
import type { GamePondDef, ReturnRulesDef } from './gameDataTypes';

function qualityRank(quality: string): number {
  return FISH_QUALITIES.findIndex((q) => q.id === quality);
}

export type ReturnFeeMode = 'sell_only' | 'auto_return';

const DUAL_FEE_CATEGORIES = new Set(['advanced', 'veteran', 'forbidden']);

export function pondAllowsDualFee(pond: GamePondDef | null | undefined): boolean {
  return pondAllowsReturnFish(pond);
}

/** 收费且表内开启回鱼的塘才允许回鱼（免票塘 / 仅出售塘不可回） */
export function pondAllowsReturnFish(pond: GamePondDef | null | undefined): boolean {
  if (!pond?.allowsAutoReturn) return false;
  return resolvePondFeePer2h(pond, 'sell_only') > 0;
}

export function resolvePondFeePer2h(pond: GamePondDef, mode: ReturnFeeMode): number {
  const sell = pond.feePer2hSellOnly ?? pond.feePer2h ?? 0;
  if (mode === 'auto_return') {
    if (pond.feePer2hAutoReturn != null && pond.feePer2hAutoReturn > 0) {
      return pond.feePer2hAutoReturn;
    }
    return sell > 0 ? Math.round(sell * 1.75) : 0;
  }
  return sell;
}

export function defaultAllowsAutoReturn(pond: GamePondDef): boolean {
  const sell = pond.feePer2hSellOnly ?? pond.feePer2h ?? 0;
  return sell > 0 && DUAL_FEE_CATEGORIES.has(pond.pondCategory);
}

export function validateJoinReturnFeeMode(
  pond: GamePondDef | null | undefined,
  mode?: ReturnFeeMode | null,
): { ok: true; mode: ReturnFeeMode } | { ok: false; error: string } {
  if (!pond) return { ok: false, error: '鱼塘不存在' };
  if (!pondAllowsDualFee(pond)) {
    return { ok: true, mode: 'sell_only' };
  }
  if (mode !== 'sell_only' && mode !== 'auto_return') {
    return { ok: false, error: '请选择进塘收费模式' };
  }
  return { ok: true, mode };
}

/** 回鱼金体重分档倍率（与 calcFishReturnGold 一致） */
export function resolveReturnGoldMul(
  sizeM: number,
  rules: Pick<ReturnRulesDef, 'goldMulVsSell' | 'goldMulHeavy' | 'minWeightJin' | 'heavyWeightJin'>,
): number {
  const minJin = rules.minWeightJin ?? 10;
  const heavyJin = rules.heavyWeightJin ?? 100;
  const jin = calcFishWeightJin(sizeM);
  if (jin > heavyJin) return rules.goldMulHeavy ?? 3;
  if (jin >= minJin) return rules.goldMulVsSell ?? 1.5;
  return 0;
}

/**
 * 回鱼准入（手动 / 自动共用；流程上须在回鱼档 auto_return）
 * - 品质 ≥ minQuality（默认 purple）
 * - 市斤体重 ≥ minWeightJin（默认 10）
 */
export function isReturnEligible(
  fish: { quality: string; sizeM: number; speciesId: string },
  rules: ReturnRulesDef,
): boolean {
  const minQ = (rules.minQuality ?? rules.autoMinQuality ?? 'purple') as FishQuality;
  const fishRank = qualityRank(fish.quality);
  const minRank = qualityRank(minQ);
  if (fishRank < 0 || minRank < 0 || fishRank < minRank) return false;

  const minJin = rules.minWeightJin ?? 10;
  return calcFishWeightJin(fish.sizeM) >= minJin;
}

/** @deprecated 别名；请用 isReturnEligible */
export function isAutoReturnEligible(
  fish: { quality: string; sizeM: number; speciesId: string },
  rules: ReturnRulesDef,
): boolean {
  return isReturnEligible(fish, rules);
}
