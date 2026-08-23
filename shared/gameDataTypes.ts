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
