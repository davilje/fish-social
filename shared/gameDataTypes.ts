import type { FishQuality } from './fish';

export type PondCategory =
  | 'novice'
  | 'advanced'
  | 'veteran'
  | 'wilderness'
  | 'reservoir'
  | 'forbidden'
  | 'giant';

export type CatchGroup =
  | 'still_bait'
  | 'stream_light'
  | 'lure_predator'
  | 'cast_heavy'
  | 'giant_game';

export interface GamePondDef {
  pondId: string;
  name: string;
  pondCategory: PondCategory;
  mapZoneId: string;
  /** @deprecated 兼容旧表；新逻辑用 feePer2hSellOnly */
  feePer2h: number;
  /** FEAT-RETURN-02：不可回鱼档每 2h 扣费 */
  feePer2hSellOnly?: number;
  /** FEAT-RETURN-02：可自动回鱼档每 2h 扣费 */
  feePer2hAutoReturn?: number;
  /** FEAT-RETURN-02：是否提供进塘双价选择 */
  allowsAutoReturn?: boolean;
  maxFeeChargesPerDay: number;
  unlock: string;
  isOpen: boolean;
  showOnWorldMap: boolean;
  minPlayerLevel: number;
  mapX?: number;
  mapY?: number;
  /** FEAT-FISH-CN-01 */
  bioRegion?: string;
  waterType?: string;
  realWorldRef?: string;
  /** 塘内鱼实体上限（原 pond_ecology 已并入） */
  maxPopulation?: number;
  minPopulation?: number;
  initialPopulation?: number;
}

export interface PlayerLevelDef {
  level: number;
  xpToNext: number;
  pondXpPerHour: number;
  maxPondLevel: number;
}

export interface PondLevelDef {
  level: number;
  xpToNext: number;
}

export interface FishSellQualityDef {
  quality: FishQuality;
  QUALITY_BASE: number;
  SIZE_REF: number;
  MIN_SELL: number;
}

/** 品质玩法+卖价同表（原 fish_sell 已并入） */
export interface FishQualityStatsDef {
  quality: FishQuality;
  sizeCapM: number;
  biteBaseAtMaxSize: number;
  displayName?: string;
  QUALITY_BASE?: number;
  SIZE_REF?: number;
  MIN_SELL?: number;
}

/** FEAT-RETURN-01：回鱼全局规则（表通常仅一行） */
export interface ReturnRulesDef {
  minQuality: FishQuality;
  minSizeRatio: number;
  maxSizeRatio: number;
  goldMulVsSell: number;
  playerXp: number;
  pondXp: number;
  sizeGainMinM: number;
  sizeGainMaxM: number;
  sizeGainMode: 'uniform_random' | string;
  /** FEAT-RETURN-02：自动回鱼最低品质（默认 purple） */
  autoMinQuality?: FishQuality;
  /** FEAT-RETURN-02：自动回鱼最低体长比（相对种 max，默认 0.75） */
  autoMinSizeRatio?: number;
}

export interface PondModifierDef {
  category: PondCategory;
  biteRateMul: number;
  escapeRateMul: number;
  infoRevealMul: number;
  qualityWeightSkew: number;
  sizeCapMul: number;
  pondXpMul: number;
  /** FEAT-RISK-01：禁止塘每小时出警概率；其它分级为 0 */
  fineChancePerHour?: number;
  /** FEAT-RISK-01：超时罚款金币；不足归零 */
  fineGold?: number;
  /** FEAT-RISK-01：出警后离塘时限（毫秒） */
  policeWarningMs?: number;
}

export interface FishXpDef {
  speciesId: string;
  speciesName?: string;
  quality: FishQuality;
  playerXp: number;
  pondXp: number;
}

export interface FishSpeciesGameDef {
  speciesId: string;
  name: string;
  diet: string;
  catchGroup: CatchGroup;
  typicalMinM?: number;
  typicalMaxM?: number;
  rarityTier?: string;
  nationwide?: boolean;
  /** 品质序 1=gray … 7=gold；播种带下限，默认 1 */
  qualityMin?: number;
  /** 品质序上限；稀有度抬高，与塘权重求交 */
  qualityMax?: number;
}

export interface PondFishPoolDef {
  pondId: string;
  speciesId: string;
  /** 展示用中文名（与 fish_species 对齐） */
  speciesName?: string;
  /** @deprecated 种池不再绑品质；保留兼容旧 JSON */
  quality?: FishQuality;
  spawnWeight: number;
  enabled: boolean;
}

/** @deprecated 人口字段已并入 GamePondDef；保留类型兼容旧调用 */
export interface PondEcologyDef {
  pondId: string;
  maxPopulation: number;
  minPopulation: number;
  initialPopulation: number;
  notes?: string;
}

export interface PondCategoryQualityWeightDef {
  pondCategory: PondCategory;
  quality: FishQuality;
  spawnWeight: number;
}

export interface RodDef {
  rodId: string;
  name: string;
  subType: string;
  priceGold: number;
  biteBonus: number;
  escapeReduction: number;
  breakSizeM: number;
  breakMaxLandings: number;
  fitGray: number;
  fitGreen: number;
  fitBlue: number;
  fitPurple: number;
  fitRed: number;
  fitOrange: number;
  fitGold: number;
  fitStillBait: number;
  fitStreamLight: number;
  fitLurePredator: number;
  fitCastHeavy: number;
  fitGiantGame: number;
}

export interface GameBaitDef {
  baitId: string;
  name: string;
  diet: string;
  unlockPlayerLevel: number;
  costGoldPerUse: number;
  biteBonusHerbivore: number;
  biteBonusOmnivore: number;
  biteBonusCarnivore: number;
  isDefaultInfinite: boolean;
}

/** FEAT-GROUND-01：窝料定义 */
export interface GroundbaitDef {
  groundbaitId: string;
  name: string;
  unlockPlayerLevel: number;
  costGoldPerUse: number;
  castDurationMs: number;
  durationMin: number;
  maxBites: number;
  perStackBiteBonus: number;
  maxBonus: number;
  stackK: number;
  sizeBonusPerStack: number;
  maxSizeBonus: number;
}

/** FEAT-ALBUM-01：成就定义（数值表 achievements） */
export interface AchievementDef {
  achievementId: string;
  name: string;
  desc: string;
  iconKey: string;
  category: string;
  conditionType: string;
  conditionValue: number;
  sortOrder: number;
  isHidden: boolean;
}

export interface VesselDef {
  vesselId: string;
  name: string;
  unlockPlayerLevel: number;
  priceGold: number;
  placeholderCatchCount: number;
  enabledUse: boolean;
}

export interface GameDataMeta {
  version: string;
  SIZE_EXP: number;
  maxFeeChargesPerDayDefault: number;
  /** FEAT-GROUND-01 */
  maxStackCount?: number;
  biteMulGlobalCap?: number;
  /** FEAT-ALBUM-01：相册精选上限 */
  albumPinCap?: number;
  schemaNote?: string;
}

/** Admission fee accounting slice (2 hours). */
export const ADMISSION_FEE_SLICE_MS = 2 * 60 * 60 * 1000;

/** FEAT-SPOT-02：activity 线索对应的钓位实时鱼情档位（v1 随机；v2 按生态匹配） */
export type SpotActivitySignal =
  | 'habitat'
  | 'active_high'
  | 'active_mid'
  | 'active_low'
  | 'inactive'
  | 'disturbed';

export interface SpotTagDef {
  tagId: string;
  tagCategory: string;
  nameZh: string;
  descriptionZh: string;
}

export interface PondSpotTagDef {
  pondId: string;
  spotId: string;
  /** Comma-separated tag ids */
  tags: string;
}

export interface SpotClueTextDef {
  clueId: string;
  clueType: 'habitat' | 'activity';
  clueText: string;
  weight?: number;
  minPlayerLevel?: number;
  minPondLevel?: number;
  pondCategory?: string;
  spotTag?: string;
  activitySignal?: SpotActivitySignal;
  enabled?: boolean;
}
