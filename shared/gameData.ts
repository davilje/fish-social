/**
 * Node/server fs game-data loader (optional hot path).
 * Package barrel exports `gameData.client` so Expo web never bundles this file
 * (import.meta in a classic script whitescreens localhost:8082).
 */
import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';
import type { FishQuality } from './fish';
import type {
  CatchGroup,
  FishSellQualityDef,
  FishSpeciesGameDef,
  FishXpDef,
  GameDataMeta,
  GamePondDef,
  PlayerLevelDef,
  PondCategory,
  PondLevelDef,
  PondModifierDef,
} from './gameDataTypes';

export type {
  CatchGroup,
  FishSellQualityDef,
  FishSpeciesGameDef,
  FishXpDef,
  GameDataMeta,
  GamePondDef,
  PlayerLevelDef,
  PondCategory,
  PondLevelDef,
  PondModifierDef,
} from './gameDataTypes';

export { ADMISSION_FEE_SLICE_MS } from './gameDataTypes';

function resolveGameDataDir(): string {
  // This file is Node/server only. Expo web/native must resolve gameData.web.ts /
  // gameData.native.ts (see shared/package.json "browser" + mobile/metro.config.js).
  const cwd = process.cwd();
  const candidates: string[] = [
    join(cwd, 'shared', 'generated', 'game-data'),
    join(cwd, 'generated', 'game-data'),
    join(cwd, '..', 'shared', 'generated', 'game-data'),
    join(cwd, '..', 'generated', 'game-data'),
  ];
  try {
    const here = dirname(fileURLToPath(import.meta.url));
    candidates.unshift(
      join(here, 'generated', 'game-data'),
      join(here, '..', 'generated', 'game-data'),
    );
  } catch {
    // ignore
  }
  for (const dir of candidates) {
    if (existsSync(join(dir, '_meta.json'))) return dir;
  }
  throw new Error('FEAT-PROG-01 game-data not found; run npm run game-data:export');
}

function readJson<T>(dir: string, name: string): T {
  return JSON.parse(readFileSync(join(dir, name), 'utf8')) as T;
}

let cached: {
  meta: GameDataMeta;
  ponds: Map<string, GamePondDef>;
  playerLevels: Map<number, PlayerLevelDef>;
  pondLevels: Map<number, PondLevelDef>;
  sellByQuality: Map<FishQuality, FishSellQualityDef>;
  speciesMult: Map<string, number>;
  modifiers: Map<PondCategory, PondModifierDef>;
  fishXp: Map<string, FishXpDef>;
  species: Map<string, FishSpeciesGameDef>;
} | null = null;

function ensureLoaded() {
  if (cached) return cached;
  const dir = resolveGameDataDir();
  const meta = readJson<GameDataMeta>(dir, '_meta.json');
  const pondsList = readJson<GamePondDef[]>(dir, 'ponds.json');
  const playerLevelsList = readJson<PlayerLevelDef[]>(dir, 'player_levels.json');
  const pondLevelsList = readJson<PondLevelDef[]>(dir, 'pond_levels.json');
  const fishSell = readJson<Array<Record<string, unknown>>>(dir, 'fish_sell.json');
  const modifiersList = readJson<PondModifierDef[]>(dir, 'pond_modifiers.json');
  const fishXpList = readJson<FishXpDef[]>(dir, 'fish_xp.json');
  const speciesList = readJson<FishSpeciesGameDef[]>(dir, 'fish_species.json');

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

  cached = {
    meta,
    ponds,
    playerLevels,
    pondLevels,
    sellByQuality,
    speciesMult,
    modifiers,
    fishXp,
    species,
  };
  return cached;
}

export function getGameDataMeta(): GameDataMeta {
  return ensureLoaded().meta;
}

export function getGamePondDef(pondId: string): GamePondDef | undefined {
  return ensureLoaded().ponds.get(pondId);
}

export function listGamePonds(): GamePondDef[] {
  return [...ensureLoaded().ponds.values()];
}

export function getPlayerLevelDef(level: number): PlayerLevelDef | undefined {
  return ensureLoaded().playerLevels.get(level);
}

export function getPondLevelDef(level: number): PondLevelDef | undefined {
  return ensureLoaded().pondLevels.get(level);
}

export function getPondModifier(category: PondCategory): PondModifierDef {
  return (
    ensureLoaded().modifiers.get(category) ?? {
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
  return ensureLoaded().species.get(speciesId)?.catchGroup ?? 'still_bait';
}

export function getFishXpGrant(
  speciesId: string,
  quality: FishQuality,
): { playerXp: number; pondXp: number } {
  const row = ensureLoaded().fishXp.get(`${speciesId}:${quality}`);
  if (row) return { playerXp: row.playerXp, pondXp: row.pondXp };
  return { playerXp: 0, pondXp: 0 };
}

export function getSellSizeExp(): number {
  const v = Number(ensureLoaded().meta.SIZE_EXP);
  return Number.isFinite(v) && v > 0 ? v : 1.15;
}

export function getSellQualityDef(quality: FishQuality): FishSellQualityDef | undefined {
  return ensureLoaded().sellByQuality.get(quality);
}

export function getSpeciesSellMult(catchGroup: string): number {
  return ensureLoaded().speciesMult.get(catchGroup) ?? 1;
}

export function reloadGameDataForTests(): void {
  cached = null;
}
