import type { FishInventoryItem, FishQuality } from './types';
import { getQualityInfo, getSpecies } from './fish';
import { getSellQualityDef, getSellSizeExp } from './gameData.client';

/** Legacy fallback if game-data missing a quality row */
const LEGACY_BASE: Record<FishQuality, number> = {
  gray: 5,
  green: 15,
  blue: 40,
  purple: 120,
  red: 300,
  orange: 800,
  gold: 2000,
};

/**
 * 卖价（钓组不参与）：
 * floor( QUALITY_BASE[q] × (sizeM / SIZE_REF[q])^SIZE_EXP )
 * then max(..., MIN_SELL[q])
 */
export function calcFishSellPrice(
  item: Pick<FishInventoryItem, 'quality' | 'sizeM'> & { speciesId?: string },
): number {
  const row = getSellQualityDef(item.quality);
  if (!row) {
    return (LEGACY_BASE[item.quality] ?? 5) + Math.floor(item.sizeM * 10);
  }

  const sizeRef = row.SIZE_REF > 0 ? row.SIZE_REF : 0.2;
  const ratio = Math.max(0.01, item.sizeM / sizeRef);
  const raw = row.QUALITY_BASE * Math.pow(ratio, getSellSizeExp());
  const sold = Math.floor(raw);
  return Math.max(sold, row.MIN_SELL);
}

/** FEAT-RETURN-01：回鱼金 = floor(卖价 × goldMulVsSell)，默认 1.5× 卖价 */
export function calcFishReturnGold(
  item: Pick<FishInventoryItem, 'quality' | 'sizeM'> & { speciesId?: string },
  goldMulVsSell?: number,
): number {
  const sell = calcFishSellPrice(item);
  const mul =
    goldMulVsSell != null && Number.isFinite(goldMulVsSell)
      ? goldMulVsSell
      : 1.5;
  return Math.max(0, Math.floor(sell * mul));
}

export function formatPostFishText(item: Pick<FishInventoryItem, 'speciesId' | 'quality' | 'sizeM'>): string {
  const species = getSpecies(item.speciesId);
  const q = getQualityInfo(item.quality);
  return `钓到了【${q.name}】${species.name}，体长 ${item.sizeM.toFixed(2)}m`;
}

export function formatEpicCatchPostText(item: Pick<FishInventoryItem, 'speciesId' | 'quality' | 'sizeM'>): string {
  return `${formatPostFishText(item)} · 纪念照已生成`;
}
