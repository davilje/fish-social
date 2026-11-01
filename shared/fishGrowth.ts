import { MAX_FISH_SIZE_M, MIN_FISH_SIZE_M, type FishQuality, type FishSpecies } from './fish';

/** 体型为游戏设计值：品质影响鱼种的潜力，不再强制所有鱼长到同一体长。 */
export const FISH_GROWTH_PROFILES: Record<FishQuality, { sizeMultiplier: number; daysTo90: number }> = {
  gray: { sizeMultiplier: 0.65, daysTo90: 2 },
  green: { sizeMultiplier: 0.8, daysTo90: 3 },
  blue: { sizeMultiplier: 1, daysTo90: 4 },
  purple: { sizeMultiplier: 1.15, daysTo90: 5 },
  red: { sizeMultiplier: 1.3, daysTo90: 7 },
  orange: { sizeMultiplier: 1.5, daysTo90: 10 },
  gold: { sizeMultiplier: 1.8, daysTo90: 14 },
};

/** 0.001m = 0.1cm。仅用于体长；金币、体重等仍用各自精度。 */
export function roundFishLength(sizeM: number): number {
  return Math.round(sizeM * 1000) / 1000;
}

/** 从永久实体 ID 派生稳定差异，重启或重复生态 tick 不会重新抽取。 */
function individualUnit(key: string, trait: string): number {
  let hash = 2166136261;
  for (const char of `${key}:${trait}`) {
    hash = Math.imul(hash ^ char.charCodeAt(0), 16777619);
  }
  // Final avalanche avoids adjacent IDs producing correlated traits.
  hash ^= hash >>> 16;
  hash = Math.imul(hash, 0x7feb352d);
  hash ^= hash >>> 15;
  hash = Math.imul(hash, 0x846ca68b);
  hash ^= hash >>> 16;
  return (hash >>> 0) / 0x100000000;
}

/** 鱼种/品质的潜力上界；传入 ID 时再限定为该个体上界（75%-100%）。 */
export function getSpeciesSizePotential(
  quality: FishQuality,
  species: FishSpecies,
  individualId?: string,
): number {
  const speciesMax = Number.isFinite(species.typicalMaxM) && species.typicalMaxM > 0
    ? species.typicalMaxM : 0.5;
  const envelope = Math.min(MAX_FISH_SIZE_M, speciesMax * FISH_GROWTH_PROFILES[quality].sizeMultiplier);
  const individual = individualId ? 0.75 + 0.25 * individualUnit(individualId, 'size') : 1;
  return roundFishLength(Math.max(MIN_FISH_SIZE_M, envelope * individual));
}

export function getFishGrowthDaysTo90(quality: FishQuality, individualId: string): number {
  return FISH_GROWTH_PROFILES[quality].daysTo90 * (0.85 + 0.3 * individualUnit(individualId, 'speed'));
}

/** 从出生尺寸渐近个体上界；达到剩余生长空间的 90% 后继续缓慢长大。 */
export function calcIndividualFishLength(
  quality: FishQuality,
  species: FishSpecies,
  birthSizeM: number,
  ageDays: number,
  individualId: string,
): number {
  const cap = getSpeciesSizePotential(quality, species, individualId);
  const birth = Math.max(MIN_FISH_SIZE_M, Math.min(cap, birthSizeM));
  const progress = -Math.expm1(-Math.LN10 * Math.max(0, ageDays) / getFishGrowthDaysTo90(quality, individualId));
  return roundFishLength(Math.min(cap, birth + (cap - birth) * progress));
}
