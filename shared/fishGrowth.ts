import { MAX_FISH_SIZE_M, MIN_FISH_SIZE_M, type FishQuality, type FishSpecies } from './fish';
import { getFishQualityStats, getFishingFormulaConstant } from './gameData.client';

/** @deprecated Historical species model; retained only for legacy rows and migrations. New fish use pond-entry-v1. */
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

/** @deprecated Historical species/quality potential (75%-100%). New fish use getFishIndividualCap. */
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

/** @deprecated Historical growth rate, not the T80 rule used for new fish. */
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

/** New fish use explicit pond-entry anchors. The older functions above are retained for historical migrations. */
export const FISH_ENTRY_GROWTH_VERSION = 'pond-entry-v1';
export const FISH_DAY_MS = 86_400_000;
export type FishEntrySource = 'pre_opening_seed' | 'supplement' | 'debug' | 'returned';
export interface FishEntryGrowthState {
  enteredAt: number;
  entrySizeM: number;
  juvenileEntrySizeM: number;
  individualCapM: number;
  growthBonusMinutes: number;
  /** Time spent outside the pond after a catch; excluded from residence and natural growth on return. */
  outOfPondMs?: number;
  growthRuleVersion: string;
  source: FishEntrySource;
  stockingBatchId?: string;
}

const DEFAULT_CAPS: Record<FishQuality, number> = { gray: .2, green: .35, blue: .5, purple: .8, red: 3, orange: 15, gold: 40 };
const DEFAULT_T80: Record<FishQuality, number> = { gray: 1, green: 2, blue: 3, purple: 5, red: 7, orange: 10, gold: 14 };

/** Symmetric triangular distribution with its peak at one. */
export function triangularFishFactor(unit: number): number {
  const spread = getFishingFormulaConstant('FISH_INDIVIDUAL_VARIATION', .15);
  const u = Math.max(0, Math.min(1, unit));
  return u < .5 ? 1-spread+spread*Math.sqrt(2*u) : 1+spread-spread*Math.sqrt(2*(1-u));
}

export function getFishQualityBaseCap(quality: FishQuality): number {
  return getFishQualityStats(quality)?.sizeCapM ?? DEFAULT_CAPS[quality];
}

export function getFishIndividualCap(quality: FishQuality, individualId: string): number {
  return roundFishLength(Math.min(MAX_FISH_SIZE_M,
    getFishQualityBaseCap(quality) * triangularFishFactor(individualUnit(individualId, 'entry-cap-v1'))));
}

export function getFishJuvenileEntrySize(individualId: string): number {
  return roundFishLength(getFishingFormulaConstant('FISH_JUVENILE_ENTRY_BASE_M', .04)
    * triangularFishFactor(individualUnit(individualId, 'entry-juvenile-v1')));
}

export function getFishGrowthT80Days(quality: FishQuality): number {
  return getFishQualityStats(quality)?.growthT80Days ?? DEFAULT_T80[quality];
}

export function getFishEntryGrowthRate(quality: FishQuality, juvenileEntrySizeM: number, individualCapM: number): number {
  const t = getFishGrowthT80Days(quality);
  if (!Number.isFinite(juvenileEntrySizeM) || !Number.isFinite(individualCapM)
      || juvenileEntrySizeM <= 0 || juvenileEntrySizeM >= .8*individualCapM || !Number.isFinite(t) || t <= 0) {
    throw new RangeError('Invalid fish entry growth parameters');
  }
  return Math.log((individualCapM-juvenileEntrySizeM)/(.2*individualCapM))/t;
}

export function isFishEntryGrowthState(value: Partial<FishEntryGrowthState> | null | undefined): value is FishEntryGrowthState {
  return value?.growthRuleVersion === FISH_ENTRY_GROWTH_VERSION
    && Number.isFinite(value.enteredAt) && Number.isFinite(value.entrySizeM)
    && Number.isFinite(value.juvenileEntrySizeM) && Number.isFinite(value.individualCapM)
    && Number.isFinite(value.growthBonusMinutes)
    && (value.outOfPondMs == null || (Number.isFinite(value.outOfPondMs) && value.outOfPondMs >= 0))
    && value.entrySizeM! > 0 && value.entrySizeM! <= value.individualCapM!
    && value.juvenileEntrySizeM! > 0 && value.juvenileEntrySizeM! < .8*value.individualCapM!
    && value.growthBonusMinutes! >= 0;
}

/** Exact-time growth; callers round snapshots only, never feed rounded display values back into the curve. */
export function calcEntryFishLength(quality: FishQuality, state: FishEntryGrowthState, now = Date.now()): number {
  if (!isFishEntryGrowthState(state) || !Number.isFinite(now)) throw new RangeError('Invalid fish entry state');
  const k = getFishEntryGrowthRate(quality,state.juvenileEntrySizeM,state.individualCapM);
  const days = Math.max(0,now-state.enteredAt-Math.max(0,state.outOfPondMs ?? 0))/FISH_DAY_MS + state.growthBonusMinutes/1440;
  return Math.min(state.individualCapM,state.individualCapM-(state.individualCapM-state.entrySizeM)*Math.exp(-k*days));
}

/** How many nutrition minutes would move this individual from its current length to a requested length. */
export function calcFishGrowthBonusMinutes(quality: FishQuality, state: FishEntryGrowthState, targetSizeM: number, now = Date.now()): number {
  const current = calcEntryFishLength(quality,state,now);
  const target = Math.min(state.individualCapM,Math.max(current,targetSizeM));
  if (current >= state.individualCapM || target <= current) return 0;
  if (target >= state.individualCapM) return Number.POSITIVE_INFINITY;
  return Math.max(0,Math.log((state.individualCapM-current)/(state.individualCapM-target))
    /getFishEntryGrowthRate(quality,state.juvenileEntrySizeM,state.individualCapM)*1440);
}

/** Opening length is sampled first, then its historical entry date is recovered. Supplement stages never backdate entry. */
export function createFishEntryGrowth(
  quality: FishQuality, individualId: string, now: number, source: FishEntrySource,
  random: () => number = Math.random,
): FishEntryGrowthState {
  if (!Number.isFinite(now)) throw new RangeError('Invalid fish entry time');
  const s = getFishJuvenileEntrySize(individualId);
  const c = getFishIndividualCap(quality,individualId);
  const k = getFishEntryGrowthRate(quality,s,c);
  const draw = () => Math.max(0,Math.min(1,random()));
  const state: FishEntryGrowthState = {
    enteredAt: now,entrySizeM:s,juvenileEntrySizeM:s,individualCapM:c,
    growthBonusMinutes:0,outOfPondMs:0,growthRuleVersion:FISH_ENTRY_GROWTH_VERSION,source,
  };
  if (source === 'pre_opening_seed') {
    const maxDays = getFishingFormulaConstant('FISH_OPENING_HISTORY_DAYS',14);
    const upper = c-(c-s)*Math.exp(-k*maxDays);
    const initialLength = s+draw()*(upper-s);
    const ageDays = Math.max(0,Math.min(maxDays,-Math.log((c-initialLength)/(c-s))/k));
    state.enteredAt = now-Math.round(ageDays*FISH_DAY_MS);
  } else if (source === 'supplement') {
    const stage = draw();
    const youngWeight = getFishingFormulaConstant('FISH_RESTOCK_JUVENILE_SHARE',.5);
    const growingWeight = getFishingFormulaConstant('FISH_RESTOCK_GROWING_SHARE',.4);
    const youngEnd = getFishingFormulaConstant('FISH_RESTOCK_JUVENILE_END_T80',.2);
    const growingEnd = getFishingFormulaConstant('FISH_RESTOCK_GROWING_END_T80',.8);
    const matureEnd = getFishingFormulaConstant('FISH_RESTOCK_MATURE_END_T80',1.5);
    const [lo,hi] = stage < youngWeight ? [0,youngEnd]
      : stage < youngWeight+growingWeight ? [youngEnd,growingEnd] : [growingEnd,matureEnd];
    const days = (lo+draw()*(hi-lo))*getFishGrowthT80Days(quality);
    state.entrySizeM = roundFishLength(Math.min(c,c-(c-s)*Math.exp(-k*days)));
  }
  return state;
}
