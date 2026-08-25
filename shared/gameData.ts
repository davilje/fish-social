/**
 * Node/server fs game-data loader (optional hot path).
 * Package barrel exports `gameData.client` so Expo web never bundles this file
 * (import.meta in a classic script whitescreens localhost:8082).
 */
import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';
import type { FishQuality } from './fish';
import { resolveSpeciesId } from './fish';
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
  modifiers: Map<PondCategory, PondModifierDef>;
  fishXp: Map<string, FishXpDef>;
  species: Map<string, FishSpeciesGameDef>;
  rods: Map<string, RodDef>;
  baits: Map<string, GameBaitDef>;
  vessels: Map<string, VesselDef>;
  rodsList: RodDef[];
  baitsList: GameBaitDef[];
  vesselsList: VesselDef[];
} | null = null;

function ensureLoaded() {
  if (cached) return cached;
  const dir = resolveGameDataDir();
  const meta = readJson<GameDataMeta>(dir, '_meta.json');
  const pondsList = readJson<GamePondDef[]>(dir, 'ponds.json');
  const playerLevelsList = readJson<PlayerLevelDef[]>(dir, 'player_levels.json');
  const pondLevelsList = readJson<PondLevelDef[]>(dir, 'pond_levels.json');
  const qualityStats = readJson<
    Array<{
      quality: FishQuality;
      QUALITY_BASE?: number;
      SIZE_REF?: number;
      MIN_SELL?: number;
    }>
  >(dir, 'fish_quality_stats.json');
  const modifiersList = readJson<PondModifierDef[]>(dir, 'pond_modifiers.json');
  const fishXpList = readJson<FishXpDef[]>(dir, 'fish_xp.json');
  const speciesList = readJson<FishSpeciesGameDef[]>(dir, 'fish_species.json');
  const rodsList = readJson<RodDef[]>(dir, 'rods.json');
  const baitsList = readJson<GameBaitDef[]>(dir, 'baits.json');
  const vesselsList = readJson<VesselDef[]>(dir, 'vessels.json');

  const ponds = new Map(pondsList.map((p) => [p.pondId, p]));
  const playerLevels = new Map(playerLevelsList.map((r) => [r.level, r]));
  const pondLevels = new Map(pondLevelsList.map((r) => [r.level, r]));
  const sellByQuality = new Map<FishQuality, FishSellQualityDef>();
  for (const row of qualityStats) {
    const base = Number(row.QUALITY_BASE);
    if (typeof row.quality === 'string' && base > 0) {
      sellByQuality.set(row.quality, {
        quality: row.quality,
        QUALITY_BASE: base,
        SIZE_REF: Number(row.SIZE_REF) || 0.2,
        MIN_SELL: Number(row.MIN_SELL) || 0,
      });
    }
  }
  const modifiers = new Map(modifiersList.map((m) => [m.category, m]));
  const fishXp = new Map(fishXpList.map((r) => [`${r.speciesId}:${r.quality}`, r]));
  const species = new Map(speciesList.map((s) => [s.speciesId, s]));
  const rods = new Map(rodsList.map((r) => [r.rodId, r]));
  const baits = new Map(baitsList.map((b) => [b.baitId, b]));
  const vessels = new Map(vesselsList.map((v) => [v.vesselId, v]));

  cached = {
    meta,
    ponds,
    playerLevels,
    pondLevels,
    sellByQuality,
    modifiers,
    fishXp,
    species,
    rods,
    baits,
    vessels,
    rodsList,
    baitsList,
    vesselsList,
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

export function getMaxPlayerLevel(): number {
  let max = 1;
  for (const level of ensureLoaded().playerLevels.keys()) {
    if (level > max) max = level;
  }
  return max;
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
  return ensureLoaded().species.get(resolveSpeciesId(speciesId))?.catchGroup ?? 'still_bait';
}

export function getGameSpeciesDiet(speciesId: string): string {
  return ensureLoaded().species.get(resolveSpeciesId(speciesId))?.diet ?? 'omnivore';
}

export function getRodDef(rodId: string): RodDef | undefined {
  return ensureLoaded().rods.get(rodId);
}

export function listRods(): RodDef[] {
  return [...ensureLoaded().rodsList];
}

export function getGameBaitDef(baitId: string): GameBaitDef | undefined {
  return ensureLoaded().baits.get(baitId);
}

export function listGameBaits(): GameBaitDef[] {
  return [...ensureLoaded().baitsList];
}

export function getVesselDef(vesselId: string): VesselDef | undefined {
  return ensureLoaded().vessels.get(vesselId);
}

export function listVessels(): VesselDef[] {
  return [...ensureLoaded().vesselsList];
}

export function getFishXpGrant(
  speciesId: string,
  quality: FishQuality,
): { playerXp: number; pondXp: number } {
  const row = ensureLoaded().fishXp.get(`${resolveSpeciesId(speciesId)}:${quality}`);
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

/** @deprecated 卖价不再使用钓组系数；恒为 1 */
export function getSpeciesSellMult(_catchGroup: string): number {
  return 1;
}

export function reloadGameDataForTests(): void {
  cached = null;
}
