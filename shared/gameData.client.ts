/**
 * Client (Expo web / native) game-data loader.
 * Uses static JSON imports so Metro never pulls Node `fs` / `import.meta`.
 */
import type { FishQuality } from './fish';
import { FISH_QUALITIES, resolveSpeciesId } from './fish';
import type {
  CatchGroup,
  FishSellQualityDef,
  FishSpeciesGameDef,
  FishXpDef,
  GameBaitDef,
  GameDataMeta,
  GamePondDef,
  PlayerLevelDef,
  PondCategory,
  PondLevelDef,
  PondModifierDef,
  RodDef,
  VesselDef,
  ReturnRulesDef,
  GroundbaitDef,
  AchievementDef,
  PondFishPoolDef,
  PondEcologyDef,
  PondCategoryQualityWeightDef,
  FishQualityStatsDef,
} from './gameDataTypes';

import metaJson from './generated/game-data/_meta.json';
import pondsJson from './generated/game-data/ponds.json';
import playerLevelsJson from './generated/game-data/player_levels.json';
import pondLevelsJson from './generated/game-data/pond_levels.json';
import modifiersJson from './generated/game-data/pond_modifiers.json';
import fishXpJson from './generated/game-data/fish_xp.json';
import speciesJson from './generated/game-data/fish_species.json';
import rodsJson from './generated/game-data/rods.json';
import baitsJson from './generated/game-data/baits.json';
import vesselsJson from './generated/game-data/vessels.json';
import returnRulesJson from './generated/game-data/return_rules.json';
import groundbaitsJson from './generated/game-data/groundbaits.json';
import achievementsJson from './generated/game-data/achievements.json';
import pondFishPoolJson from './generated/game-data/pond_fish_pool.json';
import qualityStatsJson from './generated/game-data/fish_quality_stats.json';
import formulaConstantsJson from './generated/game-data/fishing_formula_constants.json';
import categoryQualityWeightsJson from './generated/game-data/pond_category_quality_weights.json';

export type {
  CatchGroup,
  FishSellQualityDef,
  FishSpeciesGameDef,
  FishXpDef,
  GameBaitDef,
  GameDataMeta,
  GamePondDef,
  PlayerLevelDef,
  PondCategory,
  PondLevelDef,
  PondModifierDef,
  RodDef,
  VesselDef,
  ReturnRulesDef,
  GroundbaitDef,
  AchievementDef,
  PondFishPoolDef,
  PondEcologyDef,
  PondCategoryQualityWeightDef,
  FishQualityStatsDef,
} from './gameDataTypes';

export { ADMISSION_FEE_SLICE_MS } from './gameDataTypes';

function truthy(v: unknown): boolean {
  return v === true || v === 1 || v === 'TRUE' || v === 'true';
}

const meta = metaJson as GameDataMeta;
const pondsList = (pondsJson as GamePondDef[]).map((p) => ({
  ...p,
  maxPopulation: Number(p.maxPopulation) || undefined,
  minPopulation: Number(p.minPopulation) || undefined,
  initialPopulation: Number(p.initialPopulation) || undefined,
}));
const playerLevelsList = playerLevelsJson as PlayerLevelDef[];
const pondLevelsList = pondLevelsJson as PondLevelDef[];
const modifiersList = modifiersJson as PondModifierDef[];
const fishXpList = fishXpJson as FishXpDef[];
const speciesList = (speciesJson as FishSpeciesGameDef[]).map((s) => {
  const qualityMin = Math.max(1, Math.min(7, Number(s.qualityMin) || 1));
  const qualityMax = Math.max(qualityMin, Math.min(7, Number(s.qualityMax) || 7));
  return {
    ...s,
    nationwide: truthy(s.nationwide),
    qualityMin,
    qualityMax,
  };
});
const rodsList = rodsJson as RodDef[];
const baitsList = baitsJson as GameBaitDef[];
const vesselsList = vesselsJson as VesselDef[];
const returnRulesList = returnRulesJson as ReturnRulesDef[];
const groundbaitsList = groundbaitsJson as GroundbaitDef[];
const achievementsList = (achievementsJson as AchievementDef[]).map((a) => ({
  ...a,
  isHidden: Boolean((a as AchievementDef).isHidden),
  conditionValue: Number(a.conditionValue),
  sortOrder: Number(a.sortOrder),
}));

const ponds = new Map(pondsList.map((p) => [p.pondId, p]));
const playerLevels = new Map(playerLevelsList.map((r) => [r.level, r]));
const pondLevels = new Map(pondLevelsList.map((r) => [r.level, r]));
const qualityStatsList = (qualityStatsJson as FishQualityStatsDef[]).map((row) => ({
  ...row,
  sizeCapM: Number(row.sizeCapM),
  biteBaseAtMaxSize: Number(row.biteBaseAtMaxSize),
  QUALITY_BASE: Number(row.QUALITY_BASE) || 0,
  SIZE_REF: Number(row.SIZE_REF) || 0.2,
  MIN_SELL: Number(row.MIN_SELL) || 0,
}));
const sellByQuality = new Map<FishQuality, FishSellQualityDef>();
for (const row of qualityStatsList) {
  if (row.QUALITY_BASE > 0) {
    sellByQuality.set(row.quality, {
      quality: row.quality,
      QUALITY_BASE: row.QUALITY_BASE,
      SIZE_REF: row.SIZE_REF,
      MIN_SELL: row.MIN_SELL,
    });
  }
}
const modifiers = new Map(modifiersList.map((m) => [m.category, m]));
const fishXp = new Map(fishXpList.map((r) => [`${r.speciesId}:${r.quality}`, r]));
const species = new Map(speciesList.map((s) => [s.speciesId, s]));
const rods = new Map(rodsList.map((r) => [r.rodId, r]));
const baits = new Map(baitsList.map((b) => [b.baitId, b]));
const vessels = new Map(vesselsList.map((v) => [v.vesselId, v]));
const groundbaits = new Map(groundbaitsList.map((g) => [g.groundbaitId, g]));
const achievements = new Map(achievementsList.map((a) => [a.achievementId, a]));

export function getGameDataMeta(): GameDataMeta {
  return meta;
}

export function getGamePondDef(pondId: string): GamePondDef | undefined {
  return ponds.get(pondId);
}

export function listGamePonds(): GamePondDef[] {
  return [...ponds.values()];
}

export function getPlayerLevelDef(level: number): PlayerLevelDef | undefined {
  return playerLevels.get(level);
}

export function getMaxPlayerLevel(): number {
  let max = 1;
  for (const row of playerLevelsList) {
    if (row.level > max) max = row.level;
  }
  return max;
}

export function getPondLevelDef(level: number): PondLevelDef | undefined {
  return pondLevels.get(level);
}

export function getPondModifier(category: PondCategory): PondModifierDef {
  return (
    modifiers.get(category) ?? {
      category,
      biteRateMul: 1,
      escapeRateMul: 1,
      infoRevealMul: 1,
      qualityWeightSkew: 1,
      sizeCapMul: 1,
      pondXpMul: 1,
    }
  );
}

export function getCatchGroup(speciesId: string): CatchGroup {
  return species.get(resolveSpeciesId(speciesId))?.catchGroup ?? 'still_bait';
}

export function getGameSpecies(speciesId: string): FishSpeciesGameDef | undefined {
  return species.get(resolveSpeciesId(speciesId));
}

export function listGameSpecies(): FishSpeciesGameDef[] {
  return [...speciesList];
}

export function getGameSpeciesDiet(speciesId: string): string {
  return species.get(resolveSpeciesId(speciesId))?.diet ?? 'omnivore';
}

export function getRodDef(rodId: string): RodDef | undefined {
  return rods.get(rodId);
}

export function listRods(): RodDef[] {
  return [...rodsList];
}

export function getGameBaitDef(baitId: string): GameBaitDef | undefined {
  return baits.get(baitId);
}

export function listGameBaits(): GameBaitDef[] {
  return [...baitsList];
}

export function getGroundbaitDef(groundbaitId: string): GroundbaitDef | undefined {
  return groundbaits.get(groundbaitId);
}

export function listGroundbaits(): GroundbaitDef[] {
  return [...groundbaitsList];
}

export function getGroundbaitMaxStack(): number {
  const v = Number(meta.maxStackCount);
  return Number.isFinite(v) && v > 0 ? Math.floor(v) : 50;
}

export function getBiteMulGlobalCap(): number {
  const v = Number(meta.biteMulGlobalCap);
  return Number.isFinite(v) && v > 0 ? v : 1.5;
}

export function getAlbumPinCap(): number {
  const v = Number(meta.albumPinCap);
  return Number.isFinite(v) && v > 0 ? Math.floor(v) : 12;
}

export function getAchievementDef(achievementId: string): AchievementDef | undefined {
  return achievements.get(achievementId);
}

export function listAchievements(): AchievementDef[] {
  return [...achievementsList].sort((a, b) => a.sortOrder - b.sortOrder);
}

export function getVesselDef(vesselId: string): VesselDef | undefined {
  return vessels.get(vesselId);
}

export function listVessels(): VesselDef[] {
  return [...vesselsList];
}

export function getFishXpGrant(
  speciesId: string,
  quality: FishQuality,
): { playerXp: number; pondXp: number } {
  const row = fishXp.get(`${resolveSpeciesId(speciesId)}:${quality}`);
  if (row) return { playerXp: row.playerXp, pondXp: row.pondXp };
  return { playerXp: 0, pondXp: 0 };
}

export function getSellSizeExp(): number {
  const v = Number(meta.SIZE_EXP);
  return Number.isFinite(v) && v > 0 ? v : 1.15;
}

export function getSellQualityDef(quality: FishQuality): FishSellQualityDef | undefined {
  return sellByQuality.get(quality);
}

/** @deprecated 卖价不再使用钓组系数；恒为 1 */
export function getSpeciesSellMult(_catchGroup: string): number {
  return 1;
}

const DEFAULT_RETURN_RULES: ReturnRulesDef = {
  maxSizeRatio: 1.0,
  goldMulVsSell: 1.5,
  goldMulHeavy: 3,
  minWeightJin: 10,
  heavyWeightJin: 100,
  minQuality: 'purple',
  autoMinQuality: 'purple',
  playerXp: 8,
  pondXp: 4,
  sizeGainMinM: 0.02,
  sizeGainMaxM: 0.05,
  sizeGainMode: 'uniform_random',
};

/** FEAT-RETURN-01/05：回鱼规则（表首行；缺表时用默认） */
export function getReturnRules(): ReturnRulesDef {
  const row = returnRulesList[0];
  if (!row) return { ...DEFAULT_RETURN_RULES };
  return {
    maxSizeRatio: Number(row.maxSizeRatio) || DEFAULT_RETURN_RULES.maxSizeRatio,
    goldMulVsSell: Number(row.goldMulVsSell) || DEFAULT_RETURN_RULES.goldMulVsSell,
    goldMulHeavy: Number(row.goldMulHeavy) || DEFAULT_RETURN_RULES.goldMulHeavy,
    minWeightJin: Number(row.minWeightJin) || DEFAULT_RETURN_RULES.minWeightJin,
    heavyWeightJin: Number(row.heavyWeightJin) || DEFAULT_RETURN_RULES.heavyWeightJin,
    playerXp: Math.max(0, Math.floor(Number(row.playerXp) || 0)),
    pondXp: Math.max(0, Math.floor(Number(row.pondXp) || 0)),
    sizeGainMinM: Number(row.sizeGainMinM) || DEFAULT_RETURN_RULES.sizeGainMinM,
    sizeGainMaxM: Number(row.sizeGainMaxM) || DEFAULT_RETURN_RULES.sizeGainMaxM,
    sizeGainMode: row.sizeGainMode || DEFAULT_RETURN_RULES.sizeGainMode,
    minQuality: (row.minQuality as ReturnRulesDef['minQuality']) || DEFAULT_RETURN_RULES.minQuality,
    minSizeRatio: row.minSizeRatio != null ? Number(row.minSizeRatio) : undefined,
    autoMinQuality:
      (row.autoMinQuality as ReturnRulesDef['autoMinQuality']) || DEFAULT_RETURN_RULES.autoMinQuality,
    autoMinSizeRatio: row.autoMinSizeRatio != null ? Number(row.autoMinSizeRatio) : undefined,
  };
}

export function reloadGameDataForTests(): void {
  // Static JSON — no-op on client
}

const pondFishPoolList = (pondFishPoolJson as PondFishPoolDef[]).map((row) => ({
  ...row,
  spawnWeight: Number(row.spawnWeight) || 0,
  enabled: truthy(row.enabled),
}));
const formulaConstants = new Map(
  (formulaConstantsJson as Array<{ key: string; value: number | string }>).map((r) => [
    r.key,
    Number(r.value),
  ]),
);
const categoryQualityWeightList = (categoryQualityWeightsJson as PondCategoryQualityWeightDef[]).map(
  (row) => ({
    ...row,
    spawnWeight: Number(row.spawnWeight) || 0,
  }),
);
const categoryQualityByPond = new Map<PondCategory, PondCategoryQualityWeightDef[]>();
for (const row of categoryQualityWeightList) {
  const list = categoryQualityByPond.get(row.pondCategory) ?? [];
  list.push(row);
  categoryQualityByPond.set(row.pondCategory, list);
}
const poolByPond = new Map<string, PondFishPoolDef[]>();
for (const row of pondFishPoolList) {
  const list = poolByPond.get(row.pondId) ?? [];
  list.push(row);
  poolByPond.set(row.pondId, list);
}

export function listPondFishPool(pondId: string): PondFishPoolDef[] {
  return (poolByPond.get(pondId) ?? []).filter((r) => r.enabled && r.spawnWeight > 0);
}

/** 人口三列已并入 ponds；此函数从 GamePondDef 投影，兼容旧调用 */
export function getPondEcologyDef(pondId: string): PondEcologyDef | undefined {
  const pond = getGamePondDef(pondId);
  if (!pond) return undefined;
  const maxPopulation = Number(pond.maxPopulation);
  const minPopulation = Number(pond.minPopulation);
  const initialPopulation = Number(pond.initialPopulation);
  if (!(maxPopulation > 0)) return undefined;
  return {
    pondId,
    maxPopulation,
    minPopulation: minPopulation > 0 ? minPopulation : 10,
    initialPopulation: initialPopulation > 0 ? initialPopulation : 40,
  };
}

export function getFishQualityStats(quality: FishQuality): {
  sizeCapM: number;
  biteBaseAtMaxSize: number;
} | undefined {
  const row = qualityStatsList.find((r) => r.quality === quality);
  if (!row) return undefined;
  return { sizeCapM: Number(row.sizeCapM), biteBaseAtMaxSize: Number(row.biteBaseAtMaxSize) };
}

export function getFishingFormulaConstant(key: string, fallback: number): number {
  const v = formulaConstants.get(key);
  return Number.isFinite(v) ? (v as number) : fallback;
}

export function getPondCategoryQualityWeights(
  pondCategory: PondCategory,
): PondCategoryQualityWeightDef[] {
  return categoryQualityByPond.get(pondCategory) ?? [];
}

/** 种池加权抽一种（不含品质） */
export function pickPondSpecies(pondId: string): string | undefined {
  const rows = listPondFishPool(pondId);
  if (rows.length === 0) return undefined;
  const total = rows.reduce((s, r) => s + r.spawnWeight, 0);
  if (total <= 0) return rows[rows.length - 1]?.speciesId;
  let r = Math.random() * total;
  for (const row of rows) {
    r -= row.spawnWeight;
    if (r <= 0) return row.speciesId;
  }
  return rows[rows.length - 1]?.speciesId;
}

const RARITY_PREF_RANK: Record<string, number> = {
  legendary: 4,
  rare: 3,
  uncommon: 2,
  common: 1,
};

function weightedPickPoolRow(
  rows: PondFishPoolDef[],
): PondFishPoolDef | undefined {
  if (rows.length === 0) return undefined;
  const total = rows.reduce((s, r) => s + Math.max(0, r.spawnWeight ?? 0), 0);
  if (total <= 0) return rows[rows.length - 1];
  let r = Math.random() * total;
  for (const row of rows) {
    r -= Math.max(0, row.spawnWeight ?? 0);
    if (r <= 0) return row;
  }
  return rows[rows.length - 1];
}

/**
 * 给定目标品质：从塘种池中选种。
 * 仅保留 qualityMin…qualityMax 覆盖该品质的种；在可出种中优先最高稀有度档，同档内按 spawnWeight。
 */
export function pickPondSpeciesForQuality(
  pondId: string,
  quality: FishQuality,
): string | undefined {
  const rank = FISH_QUALITIES.findIndex((q) => q.id === quality) + 1;
  if (rank < 1) return undefined;
  const rows = listPondFishPool(pondId).filter((row) => {
    if ((row.spawnWeight ?? 0) <= 0) return false;
    const def = getGameSpecies(row.speciesId);
    const qMin = Number(def?.qualityMin) || 1;
    const qMax = Number(def?.qualityMax) || 7;
    return rank >= qMin && rank <= qMax;
  });
  if (rows.length === 0) return undefined;

  let bestPref = 0;
  for (const row of rows) {
    const tier = getGameSpecies(row.speciesId)?.rarityTier ?? 'common';
    bestPref = Math.max(bestPref, RARITY_PREF_RANK[tier] ?? 0);
  }
  const preferred = rows.filter((row) => {
    const tier = getGameSpecies(row.speciesId)?.rarityTier ?? 'common';
    return (RARITY_PREF_RANK[tier] ?? 0) === bestPref;
  });
  return weightedPickPoolRow(preferred)?.speciesId;
}

/** 按塘分级品质权重表抽品质；可选种品质带（序 1–7）过滤 */
export function rollPondQuality(
  pondId: string,
  qualityMin = 1,
  qualityMax = 7,
): FishQuality {
  const pond = getGamePondDef(pondId);
  const cat = (pond?.pondCategory ?? 'advanced') as PondCategory;
  const minR = Math.max(1, Math.min(7, qualityMin));
  const maxR = Math.max(minR, Math.min(7, qualityMax));
  const rows = getPondCategoryQualityWeights(cat).filter((r) => {
    if (r.spawnWeight <= 0) return false;
    const rank = FISH_QUALITIES.findIndex((q) => q.id === r.quality) + 1;
    return rank >= minR && rank <= maxR;
  });
  if (rows.length === 0) return 'gray';
  const total = rows.reduce((s, r) => s + r.spawnWeight, 0);
  let r = Math.random() * total;
  for (const row of rows) {
    r -= row.spawnWeight;
    if (r <= 0) return row.quality;
  }
  return rows[rows.length - 1]!.quality;
}

/**
 * 播种一条：先按塘品质权重表抽品质（保证塘级品质分布），
 * 再在可出该品质的种中优先高稀有度选种。
 */
export function pickSpawnFish(pondId: string): { speciesId: string; quality: FishQuality } | undefined {
  const quality = rollPondQuality(pondId, 1, 7);
  let speciesId = pickPondSpeciesForQuality(pondId, quality);
  if (!speciesId) {
    // 种池无覆盖该品质的种时：退回按种池抽种，并将品质截到该种 qualityMax
    speciesId = pickPondSpecies(pondId);
    if (!speciesId) return undefined;
    const def = getGameSpecies(speciesId);
    const qMax = Number(def?.qualityMax) || 7;
    const qMin = Number(def?.qualityMin) || 1;
    const clamped = rollPondQuality(pondId, qMin, qMax);
    return { speciesId, quality: clamped };
  }
  return { speciesId, quality };
}

/** @deprecated 用 pickPondSpecies / pickSpawnFish；旧名保留兼容 */
export function pickPondPoolEntry(pondId: string): PondFishPoolDef | undefined {
  const rows = listPondFishPool(pondId);
  if (rows.length === 0) return undefined;
  const total = rows.reduce((s, r) => s + r.spawnWeight, 0);
  let r = Math.random() * total;
  for (const row of rows) {
    r -= row.spawnWeight;
    if (r <= 0) return row;
  }
  return rows[rows.length - 1];
}
