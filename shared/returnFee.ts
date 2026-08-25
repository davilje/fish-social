import type { FishQuality, FishSpeciesId } from './fish';
import { getSpecies } from './fish';
import { getQualityMaxSize, qualityIndex } from './fishing';
import type { GamePondDef, ReturnRulesDef } from './gameDataTypes';

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

/** 回鱼准入（手动 / 自动共用）：高品质 + 高体长 + 未满尺寸 */
export function isReturnEligible(
  fish: { quality: string; sizeM: number; speciesId: string },
  rules: ReturnRulesDef,
): boolean {
  const minQuality = (rules.autoMinQuality ?? rules.minQuality ?? 'purple') as FishQuality;
  const minSizeRatio = rules.autoMinSizeRatio ?? rules.minSizeRatio ?? 0.75;
  if (qualityIndex(fish.quality as FishQuality) < qualityIndex(minQuality)) {
    return false;
  }
  const species = getSpecies(fish.speciesId as FishSpeciesId);
  const speciesMax = getQualityMaxSize(fish.quality as FishQuality, species);
  if (fish.sizeM >= speciesMax - 1e-9) return false;
  const ratio = speciesMax > 0 ? fish.sizeM / speciesMax : 0;
  if (ratio < minSizeRatio) return false;
  if (ratio >= rules.maxSizeRatio) return false;
  return true;
}

/** @deprecated 别名；请用 isReturnEligible */
export function isAutoReturnEligible(
  fish: { quality: string; sizeM: number; speciesId: string },
  rules: ReturnRulesDef,
): boolean {
  return isReturnEligible(fish, rules);
}
