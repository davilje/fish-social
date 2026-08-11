import type { FishDiet, FishQuality, FishSpecies, FishSpeciesId, RolledFish } from './fish';
import { FISH_QUALITIES, MIN_FISH_SIZE_M, MAX_FISH_SIZE_M, getSpecies, rollFishQuality, rollFishSpecies } from './fish';
import type { PondFishEntity } from './pondEcology';

/** @deprecated A0-v2 已废弃指数模型，保留仅供旧脚本兼容 */
export const BITE_LAMBDA = 0.02;

/** 品质—尺寸上限（终生不变，成长不超过此值） */
export const QUALITY_SIZE_CAP: Record<FishQuality, number> = {
  gray: 0.3,
  green: 0.8,
  blue: 2.0,
  purple: 4.5,
  red: 9.0,
  orange: 18,
  gold: 40,
};

/** 满尺寸判定：体长 ≥ 上限 × 此比例 */
export const NEAR_MAX_SIZE_RATIO = 0.975;

/** 补种/出生时强制满尺寸概率 */
export const NEAR_MAX_SPAWN_CHANCE = 0.006;

/** @deprecated D6 鱼塘运行时改用 calcSizeEscapeRate；保留兼容 */
export const NEAR_MAX_ESCAPE_RATE = 0.984;

/** 咬钩基础表全局缩放（v0.4.1 D11，默认 ÷20）；QUALITY_BITE_BASE 表数值不改 */
export const BITE_BASE_SCALE = 1 / 20;

/** true = 按 FISH_QUALITIES.weight 抽本点候选鱼 */
export const SPOT_FISH_PICK_BY_QUALITY = true;

/** false = 不按 effectiveBite 加权抽候选（v0.4.1） */
export const SPOT_FISH_PICK_BY_BITE = false;

/** 幼鱼脱钩曲线上限体长（米） */
export const JUVENILE_ESCAPE_SIZE_M = 0.35;

/** 0.08m 幼鱼脱钩率锚点（减免前） */
export const ESCAPE_AT_JUVENILE_MIN = 0.22;

/** 0.35m 幼鱼脱钩率锚点，与 40m 曲线衔接 */
export const ESCAPE_AT_JUVENILE_MAX = 0.08;

// ─── 品质基础咬钩率表（满尺寸 n=1 时的最高值，文档对照用原表 × BITE_BASE_SCALE） ──
//   品质     gray    green   blue    purple  red     orange  gold
//   出率权重   38      28      18      9       4       2       1
export const QUALITY_BITE_BASE: Record<FishQuality, number> = {
  gray: 0.05, //  5.00 %
  green: 0.032, //  3.20 %
  blue: 0.018, //  1.80 %
  purple: 0.008, //  0.80 %
  red: 0.0035, //  0.35 %
  orange: 0.0015, //  0.15 %
  gold: 0.0006, //  0.06 %
};

// ─── 品质基础脱钩率表（最小尺寸 n=0 时的起始值） ────────────────────────
// @deprecated D6 鱼塘运行时改用 calcSizeEscapeRate(sizeM)
//   品质     gray    green   blue    purple  red     orange  gold
/** @deprecated D6 鱼塘运行时改用 calcSizeEscapeRate */
export const QUALITY_ESCAPE_BASE: Record<FishQuality, number> = {
  gray: 0.055, //  5.5 %
  green: 0.095, //  9.5 %
  blue: 0.16, // 16.0 %
  purple: 0.27, // 27.0 %
  red: 0.43, // 43.0 %
  orange: 0.6, // 60.0 %
  gold: 0.65, // 65.0 %
};

/** 尺寸对咬钩率的影响系数（满尺寸=最高；最小尺寸=满尺寸×35%） */
export const SIZE_BITE_K = 0.65;

/** 尺寸对脱钩率的影响系数（满尺寸=起始值×150%） */
export const SIZE_ESCAPE_K = 0.5;

/** 公式计算脱钩率上限（97%；满尺寸特殊规则 NEAR_MAX_ESCAPE_RATE 另行覆盖） */
export const ESCAPE_RATE_MAX = 0.97;

// ─── 咬钩率完整档次速查表（每 60 秒 / 1 分钟，× BITE_BASE_SCALE，个体浮动前） ─────
// 公式：QUALITY_BITE_BASE[q] × BITE_BASE_SCALE × (1 - SIZE_BITE_K × (1 - n))
// 档次    n     gray    green   blue    purple  red     orange  gold
// 第01档 0.10  2.08%   1.33%   0.75%   0.33%   0.15%   0.06%   0.025%
// 第02档 0.20  2.40%   1.54%   0.86%   0.38%   0.17%   0.07%   0.029%
// 第03档 0.30  2.73%   1.74%   0.98%   0.44%   0.19%   0.08%   0.033%
// 第04档 0.40  3.05%   1.95%   1.10%   0.49%   0.21%   0.09%   0.037%
// 第05档 0.50  3.38%   2.16%   1.22%   0.54%   0.24%   0.10%   0.041%
// 第06档 0.60  3.70%   2.37%   1.33%   0.59%   0.26%   0.11%   0.044%
// 第07档 0.70  4.03%   2.58%   1.45%   0.64%   0.28%   0.12%   0.048%
// 第08档 0.80  4.35%   2.78%   1.57%   0.70%   0.30%   0.13%   0.052%
// 第09档 0.90  4.68%   2.99%   1.68%   0.75%   0.33%   0.14%   0.056%
// 第10档 1.00  5.00%   3.20%   1.80%   0.80%   0.35%   0.15%   0.060%

// ─── 体长脱钩/收杆速查（curve = 1-(1-n)^1.8, n=sizeM/40） ─────────────────
// sizeM   脱钩      收杆
// 0.10m   0.44%    ~34s
// 1.00m   4.4%     ~5.4min
// 10.0m   ~39%     ~55min
// 40.0m   98.5%    2h

/** 脱钩/收杆曲线锚点体长（= 至尊上限） */
export const REFERENCE_SIZE_M = 40;

/** 40m 时脱钩率锚点（渔具减免前） */
export const ESCAPE_AT_40M = 0.985;

/** 体长脱钩曲线指数 */
export const SIZE_ESCAPE_CURVE_EXPONENT = 1.8;

/** @deprecated D6 使用 HOOK_AT_40M_MS */
export const FULL_SIZE_HOOK_MS = 2 * 60 * 60 * 1000;

/** 40m 时收杆窗口锚点 */
export const HOOK_AT_40M_MS = FULL_SIZE_HOOK_MS;

/** 极小幼鱼最小收杆窗口 */
export const HOOK_MIN_MS = 2_000;

/** 体长收杆曲线指数 */
export const SIZE_HOOK_CURVE_EXPONENT = 1.8;

// ─── 脱钩率完整档次速查表（@deprecated 品质×尺寸，D6 前） ───────────────
// 公式：min(0.97, QUALITY_ESCAPE_BASE[q] × (1 + SIZE_ESCAPE_K × n))
// 满尺寸(n≥0.975)覆盖为 NEAR_MAX_ESCAPE_RATE = 0.984
// 档次    n     gray    green   blue    purple  red     orange  gold
// 第01档 0.10  5.8%    10.0%   16.8%   28.4%   45.2%   63.0%   68.3%
// 第02档 0.20  6.1%    10.5%   17.6%   29.7%   47.3%   66.0%   71.5%
// 第03档 0.30  6.3%    10.9%   18.4%   31.1%   49.5%   69.0%   74.8%
// 第04档 0.40  6.6%    11.4%   19.2%   32.4%   51.6%   72.0%   78.0%
// 第05档 0.50  6.9%    11.9%   20.0%   33.8%   53.8%   75.0%   81.3%
// 第06档 0.60  7.2%    12.4%   20.8%   35.1%   55.9%   78.0%   84.5%
// 第07档 0.70  7.4%    12.8%   21.6%   36.5%   58.1%   81.0%   87.8%
// 第08档 0.80  7.7%    13.3%   22.4%   37.8%   60.2%   84.0%   91.0%
// 第09档 0.90  8.0%    13.8%   23.2%   39.2%   62.4%   87.0%   94.3%
// 第10档 1.00  8.3%    14.3%   24.0%   40.5%   64.5%   90.0%   97.0%*

/** @deprecated 别名 = GROWTH_CURVE_DAYS */
export const GROWTH_DAYS = 7;

/** 生长曲线走完所需天数（至尊 40m 触顶时间） */
export const GROWTH_CURVE_DAYS = 7;

/** 曲线在 t=GROWTH_CURVE_DAYS 时的体长（= 至尊 QUALITY_SIZE_CAP） */
export const GROWTH_CURVE_CEILING_M = 40;

/** 减速曲线指数：越大则初期越快、后期越慢 */
export const GROWTH_CURVE_EXPONENT = 1.8;

/** 幼年鱼绝对体长区间（米），全品质通用 */
export const JUVENILE_SIZE_M_MIN = 0.08;

export const JUVENILE_SIZE_M_MAX = 0.20;

/** 脱钩后尺寸乘数：newSize = sizeM × (1 + 本值) */
export const ESCAPE_GROWTH_BONUS_RATIO = 0.02;

/** 渔具脱钩减免上限 */
export const MAX_TACKLE_ESCAPE_REDUCTION = 0.3;

export function qualityIndex(quality: FishQuality): number {
  return FISH_QUALITIES.findIndex((q) => q.id === quality);
}

export function clamp01(n: number): number {
  return Math.max(0, Math.min(1, n));
}

export function round2(n: number): number {
  return Math.round(n * 100) / 100;
}

/** 咬钩率展示：百分比 2 位小数（商店 / 图鉴 / Debug 统一） */
export function formatBiteRatePct(rate: number): string {
  return `${(rate * 100).toFixed(2)}%`;
}

export function getQualityMaxSize(quality: FishQuality, _species?: FishSpecies): number {
  return Math.min(QUALITY_SIZE_CAP[quality], MAX_FISH_SIZE_M);
}

/** @deprecated v0.3.0 物种 biteWeight 不再参与；使用 calcQualitySizeBiteRate */
export function getSpeciesBiteRatePerTick(species: FishSpecies): number {
  if (species.biteRatePerTick !== undefined) return species.biteRatePerTick;
  return species.biteWeight * 0.2;
}

/**
 * 品质×尺寸咬钩率（个体偏置前）
 * - QUALITY_BITE_BASE 为满尺寸（n=1）时的最高值
 * - 尺寸越大咬钩率越高，最小尺寸时降至满尺寸值的 35%
 */
export function calcQualitySizeBiteRate(quality: FishQuality, sizeM: number): number {
  const n = clamp01(sizeM / QUALITY_SIZE_CAP[quality]);
  return QUALITY_BITE_BASE[quality] * BITE_BASE_SCALE * (1 - SIZE_BITE_K * (1 - n));
}

function calcSizeEscapeRateMainCurve(sizeM: number): number {
  const n = clamp01(sizeM / REFERENCE_SIZE_M);
  const curve = 1 - Math.pow(1 - n, SIZE_ESCAPE_CURVE_EXPONENT);
  return clamp01(ESCAPE_AT_40M * curve);
}

/** 体长脱钩率（渔具减免前，与品质/物种无关；≤0.35m 幼鱼抬高曲线） */
export function calcSizeEscapeRate(sizeM: number): number {
  if (sizeM <= JUVENILE_ESCAPE_SIZE_M) {
    const span = JUVENILE_ESCAPE_SIZE_M - JUVENILE_SIZE_M_MIN;
    const t = span > 0 ? clamp01((sizeM - JUVENILE_SIZE_M_MIN) / span) : 0;
    const juvenile =
      ESCAPE_AT_JUVENILE_MIN + (ESCAPE_AT_JUVENILE_MAX - ESCAPE_AT_JUVENILE_MIN) * t;
    return clamp01(juvenile);
  }
  return calcSizeEscapeRateMainCurve(sizeM);
}

/** 按品质权重从本点候选中抽一条鱼（v0.4.1 D10） */
export function pickSpotFishCandidate(candidates: PondFishEntity[]): PondFishEntity {
  if (candidates.length === 0) {
    throw new Error('pickSpotFishCandidate: no candidates');
  }
  if (SPOT_FISH_PICK_BY_BITE) {
    throw new Error('SPOT_FISH_PICK_BY_BITE is not implemented');
  }
  const weights = candidates.map((fish) => {
    const q = FISH_QUALITIES.find((entry) => entry.id === fish.quality);
    return q?.weight ?? 1;
  });
  const total = weights.reduce((a, b) => a + b, 0);
  if (total <= 0) return candidates[candidates.length - 1]!;
  let r = Math.random() * total;
  for (let i = 0; i < candidates.length; i++) {
    r -= weights[i]!;
    if (r <= 0) return candidates[i]!;
  }
  return candidates[candidates.length - 1]!;
}

/** 单鱼咬钩概率：spotMult × (baseBite + baitBonus) */
export function calcSingleFishBiteProbability(
  fish: PondFishEntity,
  spotMultiplier: number,
  baitBonus = 0,
): number {
  const baseBite =
    calcQualitySizeBiteRate(fish.quality, fish.sizeM) * (fish.biteMultiplier ?? 1.0);
  return calcFishBiteContribution(spotMultiplier, baseBite, baitBonus);
}

/** 体长收杆窗口（与品质/物种无关） */
export function calcSizeHookDurationMs(sizeM: number): number {
  const n = clamp01(sizeM / REFERENCE_SIZE_M);
  const curve = 1 - Math.pow(1 - n, SIZE_HOOK_CURVE_EXPONENT);
  const span = HOOK_AT_40M_MS - HOOK_MIN_MS;
  return Math.round(HOOK_MIN_MS + span * curve);
}

/**
 * @deprecated D6 鱼塘改用 calcSizeEscapeRate(sizeM)
 * - 满尺寸（n≥0.975）覆盖为 NEAR_MAX_ESCAPE_RATE
 */
export function calcQualitySizeEscapeRate(quality: FishQuality, sizeM: number): number {
  const n = clamp01(sizeM / QUALITY_SIZE_CAP[quality]);
  if (n >= NEAR_MAX_SIZE_RATIO) return NEAR_MAX_ESCAPE_RATE;
  return Math.min(ESCAPE_RATE_MAX, QUALITY_ESCAPE_BASE[quality] * (1 + SIZE_ESCAPE_K * n));
}

/** 个体偏置系数 ±10%，入库用 */
export function rollIndividualMultiplier(): number {
  return parseFloat((0.9 + Math.random() * 0.2).toFixed(4));
}

/** v0.3.1 运行时脱钩率（仅体长 + 个体偏置 + 渔具绝对减免） */
export function calcEffectiveEscapeRate(
  sizeM: number,
  tackleId: string = 'basic',
  escapeMultiplier = 1.0,
): number {
  const base = calcSizeEscapeRate(sizeM) * escapeMultiplier;
  const reduction = tackleEscapeReduction(tackleId);
  return Math.max(0, Math.min(1, base - reduction));
}

export function isNearMaxSize(fish: {
  quality: FishQuality;
  sizeM: number;
  speciesId: FishSpeciesId;
}): boolean {
  const species = getSpecies(fish.speciesId);
  const maxSize = getQualityMaxSize(fish.quality, species);
  return fish.sizeM >= maxSize * NEAR_MAX_SIZE_RATIO;
}

/** @deprecated v0.3.0 使用 calcQualitySizeBiteRate × biteMultiplier */
export function getFishBaseBiteRate(
  fish: {
    quality: FishQuality;
    sizeM: number;
    speciesId: FishSpeciesId;
    biteWeight?: number | null;
    biteMultiplier?: number | null;
  },
  species?: FishSpecies,
): number {
  const mult = fish.biteMultiplier ?? 1.0;
  return calcQualitySizeBiteRate(fish.quality, fish.sizeM) * mult;
}

/** A0-v2：每 30s 至少一条鱼咬钩概率 = min(1, Σ effectiveBite) */
export function calcTickBiteProbability(totalEffectiveBite: number): number {
  if (totalEffectiveBite <= 0) return 0;
  return Math.min(1, totalEffectiveBite);
}

/** @deprecated 使用 calcTickBiteProbability */
export function calcBiteTickProbability(
  totalWeight: number,
  _biteLambda: number = BITE_LAMBDA,
): number {
  return calcTickBiteProbability(totalWeight);
}

/** A0-v2：钓点系数 × (基础咬钩率 + 饵加成) */
export function calcFishBiteContribution(
  spotMultiplier: number,
  baseBiteRate: number,
  baitBonus = 0,
): number {
  return clamp01(spotMultiplier * (baseBiteRate + baitBonus));
}

/** 出生尺寸：全区间均匀 + 0.6% 满尺寸（非鱼塘内鱼，如图鉴展示用） */
export function rollInitialSize(quality: FishQuality, species: FishSpecies): number {
  const maxSize = getQualityMaxSize(quality, species);
  const floor = Math.max(MIN_FISH_SIZE_M, species.typicalMinM * 0.5);
  const threshold = maxSize * NEAR_MAX_SIZE_RATIO;

  if (Math.random() < NEAR_MAX_SPAWN_CHANCE) {
    const sizeM = threshold + Math.random() * (maxSize - threshold);
    const rounded = round2(sizeM);
    if (rounded < threshold) {
      return round2(Math.min(maxSize, Math.ceil(threshold * 100) / 100));
    }
    return Math.min(maxSize, rounded);
  }

  const cap = Math.max(floor, threshold - 0.01);
  let sizeM = round2(floor + (cap - floor) * Math.random());
  if (sizeM >= threshold) {
    sizeM = round2(Math.max(floor, threshold - 0.02));
  }
  return Math.max(MIN_FISH_SIZE_M, sizeM);
}

/**
 * 播种 / 系统补充专用：绝对幼年体长，无 NEAR_MAX_SPAWN_CHANCE
 * 若随机值超过该鱼品质上限（极少数），截断至 maxSize × 0.95
 */
export function rollJuvenileSize(quality: FishQuality, species: FishSpecies): number {
  const maxSize = getQualityMaxSize(quality, species);
  const raw = JUVENILE_SIZE_M_MIN + Math.random() * (JUVENILE_SIZE_M_MAX - JUVENILE_SIZE_M_MIN);
  const capped = Math.min(raw, maxSize * 0.95);
  return round2(Math.max(MIN_FISH_SIZE_M, capped));
}

/** 脱钩后即时成长：当前体长 × (1 + ESCAPE_GROWTH_BONUS_RATIO)，触顶截断 */
export function calcEscapeGrowthSize(
  quality: FishQuality,
  species: FishSpecies,
  currentSizeM: number,
): number {
  const maxSize = getQualityMaxSize(quality, species);
  return round2(Math.min(maxSize, currentSizeM * (1 + ESCAPE_GROWTH_BONUS_RATIO)));
}

/** 全鱼共用标准体长曲线 L(t)，与品质无关 */
export function calcStandardLengthAtAge(ageDays: number): number {
  const progress = clamp01(ageDays / GROWTH_CURVE_DAYS);
  return round2(
    GROWTH_CURVE_CEILING_M * (1 - Math.pow(1 - progress, GROWTH_CURVE_EXPONENT)),
  );
}

/** 个体咬钩率 ±10% 浮动，保留 4 位小数避免精度抹平 */
export function rollIndividualBiteRate(speciesBiteRate: number): number {
  const raw = speciesBiteRate * (0.9 + Math.random() * 0.2);
  return Math.round(raw * 10000) / 10000;
}

/** @deprecated 使用 rollIndividualBiteRate */
export function rollIndividualBiteWeight(speciesBiteWeight: number): number {
  return rollIndividualBiteRate(speciesBiteWeight * 0.2);
}

/**
 * v0.3.1 成长：全鱼共用 L(t) 曲线，品质仅作上限截断
 */
export function growFishSizeV2(
  quality: FishQuality,
  species: FishSpecies,
  currentSizeM: number,
  birthSizeM: number,
  bornAt: number,
  now = Date.now(),
): number {
  const maxSize = getQualityMaxSize(quality, species);
  const ageDays = Math.max(0, (now - bornAt) / 86_400_000);
  const standardL = calcStandardLengthAtAge(ageDays);
  const targetSize = Math.min(maxSize, Math.max(birthSizeM, standardL));
  return round2(Math.max(currentSizeM, targetSize));
}

/** @deprecated D6 使用 calcEffectiveEscapeRate(sizeM, ...) */
export function calcEscapeRate(
  _species: FishSpecies,
  _quality: FishQuality,
  tackleId: TackleId = 'basic',
  _qualityEscapeBonus?: number,
  fish?: {
    quality: FishQuality;
    sizeM: number;
    speciesId: FishSpeciesId;
    escapeMultiplier?: number | null;
  },
): number {
  if (fish) {
    return calcEffectiveEscapeRate(fish.sizeM, tackleId, fish.escapeMultiplier ?? 1.0);
  }
  return calcEffectiveEscapeRate(0, tackleId, 1.0);
}

/** @deprecated 参数 quality/speciesId 已忽略；内部仅调 calcSizeHookDurationMs */
export function calcHookDurationMs(
  _quality: FishQuality,
  sizeM: number,
  _speciesId?: FishSpeciesId,
): number {
  return calcSizeHookDurationMs(sizeM);
}

/** 迁移：尺寸超出品质上限时提升到能覆盖的最低档位（不砍尺寸） */
export function upgradeQualityForSize(quality: FishQuality, sizeM: number): FishQuality {
  let needed: FishQuality = 'gold';
  for (const q of FISH_QUALITIES) {
    if (sizeM <= QUALITY_SIZE_CAP[q.id]) {
      needed = q.id;
      break;
    }
  }
  if (qualityIndex(needed) > qualityIndex(quality)) return needed;
  return quality;
}

export function weightedPickByWeight<T extends { weight: number }>(
  items: T[],
  pick: (item: T) => unknown,
): T | null {
  if (items.length === 0) return null;
  const total = items.reduce((s, i) => s + i.weight, 0);
  if (total <= 0) return items[items.length - 1];
  let r = Math.random() * total;
  for (const item of items) {
    r -= item.weight;
    if (r <= 0) return item;
  }
  return items[items.length - 1];
}

/** 验收用：W 档位校准表 */
export const BITE_CALIBRATION_TABLE: Array<{ w: number; expectedPct: number }> = [
  { w: 0.22, expectedPct: 0.4 },
  { w: 2.2, expectedPct: 4.3 },
  { w: 6.6, expectedPct: 12.4 },
  { w: 17.6, expectedPct: 29.6 },
  { w: 24.0, expectedPct: 38.1 },
];

/** 野生随机（演示模式 / 兜底） */
export function rollFishCatch(): RolledFish {
  const quality = rollFishQuality();
  const speciesId = rollFishSpecies(quality);
  const species = getSpecies(speciesId);
  const sizeM = rollInitialSize(quality, species);
  return { speciesId, quality, sizeM };
}

/** 咬钩/脱钩/空杆头顶飘字（A1 + v0.4.1 D12） */
export type FishingFloatTextKind = 'hook' | 'escape' | 'miss' | 'cast';

/** B0 鱼饵 / 渔具 */
export type BaitId = 'basic' | 'corn' | 'pellet' | 'live';
export type TackleId = 'basic' | 'carbon' | 'pro' | 'master';

export type ShopErrorCode =
  | 'INSUFFICIENT_GOLD'
  | 'ALREADY_OWNED'
  | 'INVALID_ITEM'
  | 'NOT_IN_INVENTORY'
  | 'INVALID_QUANTITY'
  | 'UNAUTHORIZED';

export interface BaitConfig {
  id: BaitId;
  name: string;
  icon: string;
  price: number;
  globalBonus: number;
  affinityByDiet?: Partial<Record<FishDiet, number>>;
  consumed: boolean;
}

export interface TackleConfig {
  id: TackleId;
  name: string;
  icon: string;
  price: number;
  escapeReduction: number;
}

export interface PlayerGearState {
  equippedBait: BaitId;
  equippedTackle: TackleId;
  baitInventory: Record<string, number>;
  ownedTackles: TackleId[];
  /** C3：渔具耐久 0~100 */
  tackleDurability: Record<string, number>;
}

export interface BaitDepletedPayload {
  previousBaitId: BaitId;
}

export const FISH_DIET_LABELS: Record<FishDiet, string> = {
  herbivore: '草食',
  omnivore: '杂食',
  carnivore: '肉食',
};

export const BAITS: BaitConfig[] = [
  { id: 'basic', name: '蚯蚓', icon: '🪱', price: 0, globalBonus: 0, consumed: false },
  {
    id: 'corn',
    name: '玉米粒',
    icon: '🌽',
    price: 50,
    globalBonus: 0.02,
    affinityByDiet: { herbivore: 0.06, omnivore: 0.02 },
    consumed: true,
  },
  {
    id: 'pellet',
    name: '商品颗粒',
    icon: '🫘',
    price: 200,
    globalBonus: 0.05,
    affinityByDiet: { herbivore: 0.03, omnivore: 0.03, carnivore: 0.03 },
    consumed: true,
  },
  {
    id: 'live',
    name: '活虾',
    icon: '🦐',
    price: 800,
    globalBonus: 0.04,
    affinityByDiet: { carnivore: 0.14, omnivore: 0.04 },
    consumed: true,
  },
];

export const TACKLES: TackleConfig[] = [
  { id: 'basic', name: '竹竿', icon: '🎋', price: 0, escapeReduction: 0 },
  { id: 'carbon', name: '碳素竿', icon: '🎣', price: 3000, escapeReduction: 0.1 },
  { id: 'pro', name: '专业路亚', icon: '🏹', price: 12000, escapeReduction: 0.2 },
  { id: 'master', name: '大师套装', icon: '👑', price: 50000, escapeReduction: 0.3 },
];

export function getBait(baitId: string): BaitConfig | undefined {
  return BAITS.find((b) => b.id === baitId);
}

export function getTackle(tackleId: string): TackleConfig | undefined {
  return TACKLES.find((t) => t.id === tackleId);
}

/** B1：饵加成 = globalBonus + affinityByDiet[species.diet] */
export function baitBiteBonus(baitId: BaitId | string, speciesId: FishSpeciesId): number {
  const bait = getBait(baitId);
  if (!bait) return 0;
  const species = getSpecies(speciesId);
  const diet: FishDiet = species.diet ?? 'omnivore';
  const affinity = bait.affinityByDiet?.[diet] ?? 0;
  return bait.globalBonus + affinity;
}

/** B0：仅全局加成（兼容旧调用） */
export function baitGlobalBonus(baitId: BaitId | string): number {
  return getBait(baitId)?.globalBonus ?? 0;
}

export function tackleEscapeReduction(tackleId: TackleId | string): number {
  return getTackle(tackleId)?.escapeReduction ?? 0;
}

export function getSpeciesDiet(speciesId: FishSpeciesId): FishDiet {
  return getSpecies(speciesId).diet ?? 'omnivore';
}

export interface FishingFloatTextPayload {
  userId: string;
  pondId: string;
  kind: FishingFloatTextKind;
  speciesId?: FishSpeciesId;
  quality?: FishQuality;
  timestamp: number;
}

/** A2 admin 钓鱼概率 debug 响应 */
export interface PondFishingDebugFishContribution {
  fishId: string;
  speciesId: FishSpeciesId;
  quality: FishQuality;
  diet: FishDiet;
  sizeM: number;
  /** 基础咬钩率（每 30s） */
  fishBiteRate: number;
  baitBonus: number;
  /** 本钓点咬钩率（每 30s） */
  spotBiteRate: number;
  escapeRate: number;
  /** 被选中占比 */
  pickShare: number;
  isNearMaxSize?: boolean;
  /** @deprecated 兼容旧面板 */
  biteWeight?: number;
  effectiveWeight?: number;
  shareOfTotal?: number;
}

export interface PondFishingDebugSpot {
  spotId: string;
  /** 钓点运气系数 0~5 */
  spotMultiplier: number;
  /** 单次抽样鱼的咬钩概率 pBite（v0.4.1） */
  tickBiteChance: number;
  /** 与 tickBiteChance 相同，显式字段 */
  pBite: number;
  /** 品质权重抽样展示用鱼 id */
  pickedFishId: string | null;
  /** 本钓点当前鱼数（仅 Debug） */
  fishAtSpotCount: number;
  fishContributions: PondFishingDebugFishContribution[];
  lockedFishIds: string[];
  /** @deprecated */
  spotBite?: number;
}

export interface PondFishingDebugActiveFisher {
  userId: string;
  playerId?: string;
  nickname?: string;
  isBot: boolean;
  spotId: string;
  fishingPhase: string;
  phaseEndsAt?: number | null;
  fishingStartedAt: number | null;
  sessionFishingMs: number;
  disconnectedAt?: number | null;
  equippedBaitId: string;
  equippedTackleId: string;
}

export interface PondQualitySupplementDebug {
  actual: number;
  ideal: number;
}

export interface PondFishingDebugResponse {
  pondId: string;
  updatedAt: number;
  queryContext: {
    baitId: BaitId;
    tackleId: TackleId;
    playerId?: string;
  };
  constants: {
    checkMs: number;
    activeAnglers: number;
    effectiveSupplementCheckMs: number;
    /** 整塘上次鱼群迁徙时间（Debug） */
    lastMigrationAt?: number;
    /** @deprecated A0-v2 已废弃 */
    biteLambda?: number;
  };
  spots: PondFishingDebugSpot[];
  activeFishers: PondFishingDebugActiveFisher[];
  summary: {
    totalFish: number;
    byQuality: Record<FishQuality, number>;
    avgTickBiteChance: number;
    qualitySupplement: Record<FishQuality, PondQualitySupplementDebug>;
  };
}
