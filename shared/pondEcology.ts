import type { FishSpeciesId } from './fish';
import { FISH_QUALITIES, rollFishQuality, type FishQuality } from './fish';
import { POND_CATALOG } from './pondCatalog';
/** 鱼塘生态常量 */
/** @deprecated v0.3.1 仅保留兼容；阻断式恢复已废弃 */
export const POND_DEPLETED_RECOVERY_MS = 15 * 60 * 1000;
/** @deprecated v0.3.1 繁殖机制已移除 */
export const POND_BREED_CHANCE_PER_TICK = 0.08;
export const POND_GROWTH_RATE_PER_HOUR = 0.012;

/** 鱼群补充检查间隔（15 分钟） */
export const POND_SUPPLEMENT_CHECK_MS = 15 * 60 * 1000;

/** 补充目标：补满至 maxPopulation */
export const POND_SUPPLEMENT_TARGET_RATIO = 1.0;

/** 每次补充量 = min(缺口 × 本比例, POND_SUPPLEMENT_BATCH_MAX) */
export const POND_SUPPLEMENT_BATCH_RATIO = 0.50;

/** 单次补充条数上限 */
export const POND_SUPPLEMENT_BATCH_MAX = 12;

/** 活跃人数达到此值及以上 → 间隔倍率 = 1（正常 15min） */
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
export const FISH_MIGRATION_CHECK_MS = 15 * 60 * 1000;

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
export function calcSupplementCheckMs(activeAnglers: number): number {
  const shortfall = Math.max(0, POND_SUPPLEMENT_FULL_ACTIVITY_ANGLERS - activeAnglers);
  const mult = Math.min(
    POND_SUPPLEMENT_MAX_INTERVAL_MULT,
    1 + shortfall * POND_SUPPLEMENT_IDLE_INTERVAL_STEP,
  );
  return Math.floor(POND_SUPPLEMENT_CHECK_MS * mult);
}

/** 各品质理想条数（baselineFrac × maxPopulation） */
export function calcSupplementIdealCounts(maxPopulation: number): Record<FishQuality, number> {
  const ideal = {} as Record<FishQuality, number>;
  for (const q of FISH_QUALITIES) {
    ideal[q.id] = (q.weight / 100) * maxPopulation;
  }
  return ideal;
}

/** v0.3.2 D8：与 FISH_QUALITIES 同序的品质补充权重 */
export function calcSupplementQualityWeights(
  actualByQuality: Record<FishQuality, number>,
  maxPopulation: number,
): number[] {
  const total = FISH_QUALITIES.reduce((s, q) => s + (actualByQuality[q.id] ?? 0), 0);
  const weights: number[] = [];

  for (let index = 0; index < FISH_QUALITIES.length; index++) {
    const q = FISH_QUALITIES[index];
    const actual = actualByQuality[q.id] ?? 0;
    const baselineFrac = q.weight / 100;
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
    if (index >= SUPPLEMENT_HIGH_TIER_MIN_INDEX && actual > 0) {
      raw *= SUPPLEMENT_HIGH_TIER_PRESENT_MULT;
    }

    weights.push(raw);
  }

  return weights;
}

/** v0.3.2 D8：按缺口加权抽补充品质；sum=0 时 fallback rollFishQuality() */
export function rollSupplementQuality(
  actualByQuality: Record<FishQuality, number>,
  maxPopulation: number,
): FishQuality {
  const weights = calcSupplementQualityWeights(actualByQuality, maxPopulation);
  const sum = weights.reduce((a, b) => a + b, 0);
  if (sum <= 0) return rollFishQuality();

  let r = Math.random() * sum;
  for (let i = 0; i < FISH_QUALITIES.length; i++) {
    r -= weights[i];
    if (r <= 0) return FISH_QUALITIES[i].id;
  }
  return FISH_QUALITIES[FISH_QUALITIES.length - 1]!.id;
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

export interface PondFishEntity {
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

/** 模板轮转：20 塘各有一份库存配置（FEAT-SCENE-TILE-3） */
const STOCK_TEMPLATES: Array<Omit<PondStockConfig, 'pondId'>> = [
  {
    commonSpecies: ['crucian', 'carp', 'tilapia', 'perch'],
    rareSpecies: ['mandarin', 'trout'],
    maxPopulation: 80,
    minPopulation: 12,
    initialPopulation: 48,
    rareSpawnRate: 0.1,
  },
  {
    commonSpecies: ['trout', 'herring', 'cod', 'perch'],
    rareSpecies: ['salmon', 'pike'],
    maxPopulation: 70,
    minPopulation: 10,
    initialPopulation: 42,
    rareSpawnRate: 0.12,
  },
  {
    commonSpecies: ['tuna', 'mackerel', 'snapper', 'salmon'],
    rareSpecies: ['marlin', 'sturgeon'],
    maxPopulation: 60,
    minPopulation: 8,
    initialPopulation: 36,
    rareSpawnRate: 0.14,
  },
  {
    commonSpecies: ['koi', 'carp', 'crucian', 'catfish'],
    rareSpecies: ['bass', 'eel'],
    maxPopulation: 75,
    minPopulation: 10,
    initialPopulation: 45,
    rareSpawnRate: 0.11,
  },
];

export const POND_STOCK_CONFIGS: PondStockConfig[] = POND_CATALOG.map((p, i) => ({
  pondId: p.id,
  ...STOCK_TEMPLATES[i % STOCK_TEMPLATES.length]!,
}));

export function getPondStockConfig(pondId: string): PondStockConfig | undefined {
  const found = POND_STOCK_CONFIGS.find((c) => c.pondId === pondId);
  if (found) return found;
  // FEAT-PROG-01：新手塘复用静心湖生态模板
  if (pondId === 'pond-novice') {
    const calm = POND_STOCK_CONFIGS.find((c) => c.pondId === 'pond-calm');
    if (calm) return { ...calm, pondId: 'pond-novice' };
  }
  return undefined;
}
