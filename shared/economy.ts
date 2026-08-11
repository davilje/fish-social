import type { FishInventoryItem, FishQuality } from './types';
import { getQualityInfo, getSpecies } from './fish';

/** 根据品质与尺寸计算出售金币 */
const QUALITY_BASE_COINS: Record<FishQuality, number> = {
  gray: 5,
  green: 15,
  blue: 40,
  purple: 120,
  red: 300,
  orange: 800,
  gold: 2000,
};

export function calcFishSellPrice(item: Pick<FishInventoryItem, 'quality' | 'sizeM'>): number {
  const base = QUALITY_BASE_COINS[item.quality];
  const sizeBonus = Math.floor(item.sizeM * 10);
  return base + sizeBonus;
}

export function formatPostFishText(item: Pick<FishInventoryItem, 'speciesId' | 'quality' | 'sizeM'>): string {
  const species = getSpecies(item.speciesId);
  const q = getQualityInfo(item.quality);
  return `钓到了【${q.name}】${species.name}，体长 ${item.sizeM.toFixed(2)}m`;
}

export function formatEpicCatchPostText(item: Pick<FishInventoryItem, 'speciesId' | 'quality' | 'sizeM'>): string {
  return `${formatPostFishText(item)} · 纪念照已生成`;
}
