import type { FishQuality } from './fish';
import type { BaitId } from './fishing';
import type { CatchGroup, GameBaitDef, RodDef } from './gameDataTypes';
import {
  getCatchGroup,
  getGameBaitDef,
  getGameSpeciesDiet,
  getRodDef,
  listGameBaits,
} from './gameData.client';

export const STARTER_ROD_ID = 'rod-bamboo';
export const BASIC_BAIT_ID = 'bait-basic' as const;

const NO_ROD_MESSAGE = '没有可用钓竿，请先到商店购买或装备钓竿';

export function noUsableRodError(): string {
  return NO_ROD_MESSAGE;
}

export function normalizeBaitId(baitId: string | undefined | null): BaitId {
  if (!baitId || baitId === 'basic') return BASIC_BAIT_ID;
  return baitId as BaitId;
}

export function unlockedBaitsForPlayerLevel(level: number): string[] {
  const lv = Math.max(1, Math.floor(level || 1));
  return listGameBaits()
    .filter((b) => b.unlockPlayerLevel <= lv)
    .map((b) => b.baitId);
}

export function pickBaitForDiet(
  unlocked: readonly string[],
  speciesId: string,
  coins: number,
): { baitId: string; cost: number; def: GameBaitDef | undefined } {
  const diet = getGameSpeciesDiet(speciesId);
  const match = listGameBaits().find(
    (b) =>
      !b.isDefaultInfinite &&
      b.diet === diet &&
      unlocked.includes(b.baitId) &&
      coins >= b.costGoldPerUse,
  );
  if (match) return { baitId: match.baitId, cost: match.costGoldPerUse, def: match };
  const basic = getGameBaitDef(BASIC_BAIT_ID);
  return { baitId: BASIC_BAIT_ID, cost: 0, def: basic };
}

function qualityFit(rod: RodDef, quality: FishQuality): number {
  switch (quality) {
    case 'gray':
      return rod.fitGray;
    case 'green':
      return rod.fitGreen;
    case 'blue':
      return rod.fitBlue;
    case 'purple':
      return rod.fitPurple;
    case 'red':
      return rod.fitRed;
    case 'orange':
      return rod.fitOrange;
    case 'gold':
      return rod.fitGold;
    default:
      return 1;
  }
}

function groupFit(rod: RodDef, group: CatchGroup): number {
  switch (group) {
    case 'still_bait':
      return rod.fitStillBait;
    case 'stream_light':
      return rod.fitStreamLight;
    case 'lure_predator':
      return rod.fitLurePredator;
    case 'cast_heavy':
      return rod.fitCastHeavy;
    case 'giant_game':
      return rod.fitGiantGame;
    default:
      return 1;
  }
}

/** Weak rod multiplier: (1 + biteBonus) × quality fit × catchGroup fit. */
export function rodBiteMultiplier(
  rodId: string | undefined,
  quality: FishQuality,
  speciesId: string,
): number {
  const rod = rodId ? getRodDef(rodId) : undefined;
  if (!rod) return 1;
  return (1 + rod.biteBonus) * qualityFit(rod, quality) * groupFit(rod, getCatchGroup(speciesId));
}

export function rodEscapeReductionValue(rodId: string | undefined): number {
  return (rodId ? getRodDef(rodId)?.escapeReduction : undefined) ?? 0;
}

export function isOversizeForRod(rodId: string | undefined, sizeM: number): boolean {
  const rod = rodId ? getRodDef(rodId) : undefined;
  if (!rod || rod.breakSizeM <= 0) return false;
  return sizeM > rod.breakSizeM;
}

export function shouldDestroyRod(
  rodId: string | undefined,
  oversizeLandings: number,
): boolean {
  const rod = rodId ? getRodDef(rodId) : undefined;
  if (!rod) return false;
  return oversizeLandings >= rod.breakMaxLandings;
}

export function hasUsableRod(
  ownedRods: readonly string[] | undefined,
  equippedRod: string | undefined,
): boolean {
  if (!equippedRod || !getRodDef(equippedRod)) return false;
  return (ownedRods ?? []).includes(equippedRod);
}

export function gameBaitBiteBonus(baitId: string, speciesId: string): number | null {
  const def = getGameBaitDef(normalizeBaitId(baitId));
  if (!def) return null;
  const diet = getGameSpeciesDiet(speciesId);
  if (diet === 'herbivore') return def.biteBonusHerbivore;
  if (diet === 'carnivore') return def.biteBonusCarnivore;
  return def.biteBonusOmnivore;
}
