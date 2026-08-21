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
  feePer2h: number;
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

export interface PondModifierDef {
  category: PondCategory;
  biteRateMul: number;
  escapeRateMul: number;
  infoRevealMul: number;
  qualityWeightSkew: number;
  sizeCapMul: number;
  pondXpMul: number;
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

export interface GameDataMeta {
  version: string;
  SIZE_EXP: number;
  maxFeeChargesPerDayDefault: number;
  schemaNote?: string;
}

/** Admission fee accounting slice (2 hours). */
export const ADMISSION_FEE_SLICE_MS = 2 * 60 * 60 * 1000;
