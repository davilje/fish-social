/**
 * Client (Expo web / native) game-data loader.
 * Uses static JSON imports so Metro never pulls Node `fs` / `import.meta`.
 */
import type { FishQuality } from './fish';
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
} from './gameDataTypes';

import metaJson from './generated/game-data/_meta.json';
import pondsJson from './generated/game-data/ponds.json';
import playerLevelsJson from './generated/game-data/player_levels.json';
import pondLevelsJson from './generated/game-data/pond_levels.json';
import fishSellJson from './generated/game-data/fish_sell.json';
import modifiersJson from './generated/game-data/pond_modifiers.json';
import fishXpJson from './generated/game-data/fish_xp.json';
import speciesJson from './generated/game-data/fish_species.json';
import rodsJson from './generated/game-data/rods.json';
import baitsJson from './generated/game-data/baits.json';
import vesselsJson from './generated/game-data/vessels.json';

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
} from './gameDataTypes';

export { ADMISSION_FEE_SLICE_MS } from './gameDataTypes';

const meta = metaJson as GameDataMeta;
const pondsList = pondsJson as GamePondDef[];
const playerLevelsList = playerLevelsJson as PlayerLevelDef[];
const pondLevelsList = pondLevelsJson as PondLevelDef[];
const fishSell = fishSellJson as Array<Record<string, unknown>>;
const modifiersList = modifiersJson as PondModifierDef[];
const fishXpList = fishXpJson as FishXpDef[];
const speciesList = speciesJson as FishSpeciesGameDef[];
const rodsList = rodsJson as RodDef[];
const baitsList = baitsJson as GameBaitDef[];
const vesselsList = vesselsJson as VesselDef[];

const ponds = new Map(pondsList.map((p) => [p.pondId, p]));
const playerLevels = new Map(playerLevelsList.map((r) => [r.level, r]));
const pondLevels = new Map(pondLevelsList.map((r) => [r.level, r]));
const sellByQuality = new Map<FishQuality, FishSellQualityDef>();
const speciesMult = new Map<string, number>();
for (const row of fishSell) {
  if (typeof row.quality === 'string' && row.QUALITY_BASE != null) {
    sellByQuality.set(row.quality as FishQuality, row as unknown as FishSellQualityDef);
  } else if (typeof row.catchGroup === 'string' && row.SPECIES_MULT != null) {
    speciesMult.set(String(row.catchGroup), Number(row.SPECIES_MULT));
  }
}
const modifiers = new Map(modifiersList.map((m) => [m.category, m]));
const fishXp = new Map(fishXpList.map((r) => [`${r.speciesId}:${r.quality}`, r]));
const species = new Map(speciesList.map((s) => [s.speciesId, s]));
const rods = new Map(rodsList.map((r) => [r.rodId, r]));
const baits = new Map(baitsList.map((b) => [b.baitId, b]));
const vessels = new Map(vesselsList.map((v) => [v.vesselId, v]));

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
  return species.get(speciesId)?.catchGroup ?? 'still_bait';
}

export function getGameSpeciesDiet(speciesId: string): string {
  return species.get(speciesId)?.diet ?? 'omnivore';
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
  const row = fishXp.get(`${speciesId}:${quality}`);
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

export function getSpeciesSellMult(catchGroup: string): number {
  return speciesMult.get(catchGroup) ?? 1;
}

export function reloadGameDataForTests(): void {
  // Static JSON — no-op on client
}
