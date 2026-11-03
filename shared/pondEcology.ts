import type { FishSpeciesId } from './fish';
import { FISH_QUALITIES, type FishQuality } from './fish';
import type { FishEntryGrowthState } from './fishGrowth';
import { POND_CATALOG } from './pondCatalog';
import { getPondEcologyDef, listPondFishPool, getPondCategoryQualityWeights, getGamePondDef, getGameSpecies, getFishingFormulaConstant, pickPondSpeciesForQuality } from './gameData.client';
import type { PondCategory } from './gameDataTypes';
/** 鱼塘生态常量 */
/** @deprecated v0.3.1 仅保留兼容；阻断式恢复已废弃 */
export const POND_DEPLETED_RECOVERY_MS = 15 * 60 * 1000;
/** @deprecated v0.3.1 繁殖机制已移除 */
export const POND_BREED_CHANCE_PER_TICK = 0.08;
export const POND_GROWTH_RATE_PER_HOUR = 0.012;

/** 鱼群补充检查间隔（60 分钟） */
export const POND_SUPPLEMENT_CHECK_MS = 60 * 60 * 1000;
export const POND_SUPPLEMENT_LOW_WATERMARK = getFishingFormulaConstant('POND_SUPPLEMENT_LOW_WATERMARK', 40);
export const POND_SUPPLEMENT_LOW_TARGET = getFishingFormulaConstant('POND_SUPPLEMENT_LOW_TARGET', 80);
export const POND_DAILY_TARGET = getFishingFormulaConstant('POND_DAILY_TARGET', 100);
export const POND_DAILY_SETTLEMENT_HOUR = getFishingFormulaConstant('POND_DAILY_SETTLEMENT_HOUR', 4);

/** Daily settlement takes priority over the hourly low-watermark check. */
export function calcPondSupplementCount(currentCount: number, daily = false, maxPopulation = POND_DAILY_TARGET): number {
  const target = daily ? Math.min(maxPopulation, POND_DAILY_TARGET)
    : currentCount <= POND_SUPPLEMENT_LOW_WATERMARK ? Math.min(maxPopulation, POND_SUPPLEMENT_LOW_TARGET) : currentCount;
  return Math.max(0, Math.floor(target-currentCount));
}

/** Ecology day is anchored at 04:00 Asia/Shanghai, independent of the host timezone. */
export function getPondEcologyDayKey(now: number): string {
  return new Date(now+(8-POND_DAILY_SETTLEMENT_HOUR)*3_600_000).toISOString().slice(0,10);
}

/** 补充目标：补满至 maxPopulation */
export const POND_SUPPLEMENT_TARGET_RATIO = 1.0;

/** 每次补充量 = min(缺口 × 本比例, POND_SUPPLEMENT_BATCH_MAX) */
export const POND_SUPPLEMENT_BATCH_RATIO = 0.50;

/** 单次补充条数上限 */
export const POND_SUPPLEMENT_BATCH_MAX = 12;

/** 活跃人数达到此值及以上 → 间隔倍率 = 1（正常 60min） */
export const POND_SUPPLEMENT_FULL_ACTIVITY_ANGLERS = 3;

/** 每少 1 名活跃钓鱼者，间隔增加的倍率步长（线性） */
export const POND_SUPPLEMENT_IDLE_INTERVAL_STEP = 0.5;

/** 无人钓鱼时间隔倍率上限 */
export const POND_SUPPLEMENT_MAX_INTERVAL_MULT = 3.0;

/** 史诗及以上：塘内仍有该品质鱼时，补充权重乘数 */
export const SUPPLEMENT_HIGH_TIER_PRESENT_MULT = 0.02;

/** 稀有（蓝）：塘内仍有且占比 ≥ 理想×此比例时，压低补充权重 */
export const SUPPLEMENT_MID_TIER_SATURATION_RATIO = 0.6;

export const SUPPLEMENT_MID_TIER_PRESENT_MULT = 0.15;

/** 低品质（灰/绿）缺口加成 */
export const SUPPLEMENT_LOW_TIER_DEFICIT_BOOST = 1.25;

/** 高品质索引阈值（purple=3, red=4, orange=5, gold=6） */
export const SUPPLEMENT_HIGH_TIER_MIN_INDEX = 3;

/** 稀有蓝 tier index */
export const SUPPLEMENT_MID_TIER_INDEX = 2;

/** 鱼群迁徙检查间隔 — 与 POND_SUPPLEMENT_CHECK_MS 相同，同 tick 触发 */
export const FISH_MIGRATION_CHECK_MS = 60 * 60 * 1000;

/** 每次迁徙事件中，参与换点的鱼占全塘比例 */
export const FISH_MIGRATION_FRACTION = 0.4;

/** 迁徙/播种目的地：栖息地权重与均匀项的混合比例 */
export const FISH_MIGRATION_HABITAT_BLEND = 0.7;

/** 计算各钓点迁徙/播种目的地权重（与 FISH_MIGRATION_HABITAT_BLEND 公式一致） */
export function calcSpotDestinationWeights(
  spotIds: string[],
  habitatWeights: Record<string, number>,
): number[] {
  const uniform = 1 - FISH_MIGRATION_HABITAT_BLEND;
  return spotIds.map((id) => {
    const habitat = habitatWeights[id] ?? 0;
    return habitat * FISH_MIGRATION_HABITAT_BLEND + uniform;
  });
}

function weightedPickSpot(
  spotIds: string[],
  destWeights: number[],
  random: () => number = Math.random,
): string {
  if (spotIds.length === 0) throw new Error('pickSpot: no spotIds');
  const total = destWeights.reduce((a, b) => a + b, 0);
  if (total <= 0) return spotIds[spotIds.length - 1]!;
  let r = random() * total;
  for (let i = 0; i < spotIds.length; i++) {
    r -= destWeights[i]!;
    if (r <= 0) return spotIds[i]!;
  }
  return spotIds[spotIds.length - 1]!;
}

/** 播种/补充时分配钓点 */
export function pickSpotForNewFish(
  spotIds: string[],
  habitatWeights: Record<string, number>,
  random: () => number = Math.random,
): string {
  const weights = calcSpotDestinationWeights(spotIds, habitatWeights);
  return weightedPickSpot(spotIds, weights, random);
}

/** 迁徙目的地钓点 */
export function pickMigrationSpot(
  spotIds: string[],
  habitatWeights: Record<string, number>,
  random: () => number = Math.random,
): string {
  const weights = calcSpotDestinationWeights(spotIds, habitatWeights);
  return weightedPickSpot(spotIds, weights, random);
}

/** v0.3.2 D7：有效补充检查间隔（毫秒） */
export function calcSupplementCheckMs(_activeAnglers: number): number {
  return POND_SUPPLEMENT_CHECK_MS;
}

function qualityWeightMap(pondCategory?: PondCategory): Record<FishQuality, number> {
  const map = {} as Record<FishQuality, number>;
  const rows = pondCategory ? getPondCategoryQualityWeights(pondCategory) : [];
  for (const q of FISH_QUALITIES) {
    const row = rows.find((r) => r.quality === q.id);
    map[q.id] = row ? row.spawnWeight : pondCategory ? 0 : q.weight;
  }
  return map;
}

/** 各品质理想条数（baselineFrac × maxPopulation） */
export function calcSupplementIdealCounts(
  maxPopulation: number,
  pondCategory?: PondCategory,
): Record<FishQuality, number> {
  const weights = qualityWeightMap(pondCategory);
  const sum = FISH_QUALITIES.reduce((s, q) => s + (weights[q.id] ?? 0), 0) || 1;
  const ideal = {} as Record<FishQuality, number>;
  for (const q of FISH_QUALITIES) {
    ideal[q.id] = ((weights[q.id] ?? 0) / sum) * maxPopulation;
  }
  return ideal;
}

/** v0.3.2 D8：与 FISH_QUALITIES 同序的品质补充权重 */
export function calcSupplementQualityWeights(
  actualByQuality: Record<FishQuality, number>,
  maxPopulation: number,
  pondCategory?: PondCategory,
): number[] {
  const total = FISH_QUALITIES.reduce((s, q) => s + (actualByQuality[q.id] ?? 0), 0);
  const catWeights = qualityWeightMap(pondCategory);
  const weightSum = FISH_QUALITIES.reduce((s, q) => s + (catWeights[q.id] ?? 0), 0) || 1;
  const weights: number[] = [];

  for (let index = 0; index < FISH_QUALITIES.length; index++) {
    const q = FISH_QUALITIES[index];
    const actual = actualByQuality[q.id] ?? 0;
    const baselineFrac = (catWeights[q.id] ?? 0) / weightSum;
    const idealCount = baselineFrac * maxPopulation;
    const deficit = Math.max(0, idealCount - actual);
    const share = total > 0 ? actual / total : 0;

    let raw = deficit + baselineFrac * maxPopulation * 0.1;

    if (index <= 1 && deficit > 0) {
      raw *= 1 + SUPPLEMENT_LOW_TIER_DEFICIT_BOOST;
    }
    if (
      index === SUPPLEMENT_MID_TIER_INDEX &&
      actual > 0 &&
      share >= baselineFrac * SUPPLEMENT_MID_TIER_SATURATION_RATIO
    ) {
      raw *= SUPPLEMENT_MID_TIER_PRESENT_MULT;
    }
    if (index >= SUPPLEMENT_HIGH_TIER_MIN_INDEX && actual >= idealCount) {
      raw = 0;
    }

    weights.push(raw);
  }

  return weights;
}

/** No unrestricted fallback: a depleted candidate set means no supplement can be generated. */
export function rollSupplementQuality(
  actualByQuality: Record<FishQuality, number>,
  maxPopulation: number,
  pondCategory?: PondCategory,
): FishQuality | undefined {
  const weights = calcSupplementQualityWeights(actualByQuality, maxPopulation, pondCategory);
  const sum = weights.reduce((a, b) => a + b, 0);
  if (sum <= 0) return undefined;

  let r = Math.random() * sum;
  for (let i = 0; i < FISH_QUALITIES.length; i++) {
    if (weights[i] <= 0) continue;
    r -= weights[i];
    if (r <= 0) return FISH_QUALITIES[i].id;
  }
  return FISH_QUALITIES.filter((_, i) => weights[i] > 0).at(-1)?.id;
}

/**
 * Supplement weights are masked by the legal species pool before sampling.
 * A missing species must never bypass the high-quality stock cap via a spawn fallback.
 */
export function pickSupplementFish(
  pondId: string,
  actualByQuality: Record<FishQuality, number>,
  maxPopulation: number,
): { speciesId: string; quality: FishQuality } | undefined {
  const pond = getGamePondDef(pondId);
  const category = (pond?.pondCategory ?? 'advanced') as PondCategory;
  const weights = calcSupplementQualityWeights(actualByQuality,maxPopulation,category);
  const pool = listPondFishPool(pondId);
  for (let i=0;i<FISH_QUALITIES.length;i++) {
    const rank=i+1;
    if (!pool.some(row => {
      const species=getGameSpecies(row.speciesId);
      return row.spawnWeight>0 && species && rank >= (species.qualityMin ?? 1) && rank <= (species.qualityMax ?? 7);
    })) weights[i]=0;
  }
  const total=weights.reduce((sum,w)=>sum+w,0);
  if (!(total>0)) return undefined;
  let roll=Math.random()*total;
  let selected=-1;
  for (let i=0;i<weights.length;i++) {
    if (weights[i]<=0) continue;
    selected=i;
    roll-=weights[i];
    if (roll<=0) break;
  }
  const quality=FISH_QUALITIES[selected]!.id;
  const speciesId = pickPondSpeciesForQuality(pondId, quality);
  if (speciesId) return { speciesId, quality };
  return undefined;
}

export interface PondStockConfig {
  pondId: string;
  /** 常见鱼种 */
  commonSpecies: FishSpeciesId[];
  /** 稀有鱼种 */
  rareSpecies: FishSpeciesId[];
  maxPopulation: number;
  minPopulation: number;
  initialPopulation: number;
  rareSpawnRate: number;
}

export interface PondFishEntity extends Partial<FishEntryGrowthState> {
  id: string;
  pondId: string;
  /** 鱼当前归属钓点（v0.4.0 分区咬钩） */
  spotId: string;
  speciesId: FishSpeciesId;
  quality: import('./fish').FishQuality;
  sizeM: number;
  bornAt: number;
  generation: number;
  /** 个体咬钩偏置系数 0.90~1.10（v0.3.0） */
  biteMultiplier?: number | null;
  /** 个体脱钩偏置系数 0.90~1.10（v0.3.0） */
  escapeMultiplier?: number | null;
  /** @deprecated 历史列，不再参与计算 */
  biteWeight?: number | null;
  /** 出生时体长（成长曲线起点） */
  birthSizeM?: number;
}

export interface PondEcologySummary {
  pondId: string;
  fishCount: number;
  maxPopulation: number;
  depleted: boolean;
  depletedUntil?: number;
  commonSpecies: FishSpeciesId[];
  rareSpecies: FishSpeciesId[];
  spotWeights: Record<string, number>;
  lastWeightRefresh: number;
}

export const POND_STOCK_CONFIGS: PondStockConfig[] = POND_CATALOG.map((p) =>
  buildStockConfigFromTables(p.id),
);

function buildStockConfigFromTables(pondId: string): PondStockConfig {
  const ecology = getPondEcologyDef(pondId);
  const pool = listPondFishPool(pondId);
  const common = new Set<FishSpeciesId>();
  for (const row of pool) {
    common.add(row.speciesId);
  }
  return {
    pondId,
    commonSpecies: [...common],
    rareSpecies: [],
    maxPopulation: ecology?.maxPopulation ?? 70,
    minPopulation: ecology?.minPopulation ?? 10,
    initialPopulation: ecology?.initialPopulation ?? 40,
    rareSpawnRate: 0.12,
  };
}

export function getPondStockConfig(pondId: string): PondStockConfig | undefined {
  const found = POND_STOCK_CONFIGS.find((c) => c.pondId === pondId);
  if (found && (found.commonSpecies.length > 0 || found.rareSpecies.length > 0)) return found;
  const built = buildStockConfigFromTables(pondId);
  if (built.commonSpecies.length > 0 || built.rareSpecies.length > 0) return built;
  return undefined;
}

/** 新手塘钓完不补、不重种；正式塘走 60min 补充节奏 */
export function pondAllowsEcologySupplement(pondId: string): boolean {
  const pond = getGamePondDef(pondId);
  return (pond?.pondCategory ?? 'advanced') !== 'novice';
}
