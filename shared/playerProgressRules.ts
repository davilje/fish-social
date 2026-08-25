import {
  getPlayerLevelDef,
  getPondLevelDef,
  getPondModifier,
  getGamePondDef,
  getFishXpGrant,
  getFishingFormulaConstant,
  getSellQualityDef,
} from './gameData.client';
import type { FishQuality } from './fish';

export interface PlayerProgressState {
  level: number;
  xp: number;
}

export interface PondProficiencyState {
  level: number;
  xp: number;
}

export interface XpGrantResult {
  player: PlayerProgressState;
  pond: PondProficiencyState;
  playerLeveled: boolean;
  pondLeveled: boolean;
  pondXpGranted: number;
  pondXpCapped: boolean;
  playerXpGranted: number;
}

function clampLevel(level: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, level));
}

/** Apply player XP with level-ups up to table max. */
export function applyPlayerXp(state: PlayerProgressState, grant: number): {
  state: PlayerProgressState;
  leveled: boolean;
} {
  let level = clampLevel(state.level || 1, 1, 20);
  let xp = Math.max(0, state.xp);
  let remaining = Math.max(0, Math.floor(grant));
  let leveled = false;

  while (remaining > 0) {
    const def = getPlayerLevelDef(level);
    if (!def || def.xpToNext <= 0) {
      xp += remaining;
      remaining = 0;
      break;
    }
    const need = def.xpToNext - xp;
    if (remaining < need) {
      xp += remaining;
      remaining = 0;
      break;
    }
    remaining -= need;
    level += 1;
    xp = 0;
    leveled = true;
    if (level >= 20) {
      xp += remaining;
      remaining = 0;
      break;
    }
  }

  return { state: { level, xp }, leveled };
}

/**
 * Pond proficiency: stop granting when at Lv10 or when next level would exceed
 * player maxPondLevel (FEAT-PROG-01 停发塘 XP).
 */
export function applyPondXp(
  pondState: PondProficiencyState,
  grant: number,
  playerLevel: number,
): {
  state: PondProficiencyState;
  leveled: boolean;
  granted: number;
  capped: boolean;
} {
  let level = clampLevel(pondState.level || 1, 1, 10);
  let xp = Math.max(0, pondState.xp);
  const playerDef = getPlayerLevelDef(playerLevel);
  const maxPondLevel = playerDef?.maxPondLevel ?? 1;

  if (level >= 10 || level >= maxPondLevel) {
    return { state: { level, xp }, leveled: false, granted: 0, capped: true };
  }

  let remaining = Math.max(0, Math.floor(grant));
  let leveled = false;
  let granted = 0;

  while (remaining > 0) {
    if (level >= 10 || level >= maxPondLevel) {
      return { state: { level, xp }, leveled, granted, capped: true };
    }
    const def = getPondLevelDef(level);
    if (!def || def.xpToNext <= 0) {
      return { state: { level, xp }, leveled, granted, capped: true };
    }
    // Cannot start filling XP toward a level above maxPondLevel
    if (level + 1 > maxPondLevel && xp >= def.xpToNext) {
      return { state: { level, xp }, leveled, granted, capped: true };
    }
    const need = def.xpToNext - xp;
    if (remaining < need) {
      xp += remaining;
      granted += remaining;
      remaining = 0;
      break;
    }
    if (level + 1 > maxPondLevel) {
      // Stop before leveling past lock
      return { state: { level, xp }, leveled, granted, capped: true };
    }
    remaining -= need;
    granted += need;
    level += 1;
    xp = 0;
    leveled = true;
  }

  return { state: { level, xp }, leveled, granted, capped: false };
}

/** Catch XP scaled by body length: base × (sizeM / SIZE_REF)^XP_SIZE_EXP */
export function calcCatchXpGrant(
  speciesId: string,
  quality: FishQuality,
  sizeM: number,
): { playerXp: number; pondXp: number } {
  const base = getFishXpGrant(speciesId, quality);
  const ref = getSellQualityDef(quality)?.SIZE_REF ?? 0;
  const exp = getFishingFormulaConstant('XP_SIZE_EXP', 0.85);
  if (!(ref > 0) || !(sizeM > 0) || base.playerXp <= 0) {
    return base;
  }
  const mul = Math.pow(sizeM / ref, exp);
  return {
    playerXp: Math.max(1, Math.floor(base.playerXp * mul)),
    pondXp: Math.max(1, Math.floor(base.pondXp * mul)),
  };
}

export function grantCatchXp(
  player: PlayerProgressState,
  pond: PondProficiencyState,
  playerXp: number,
  pondXp: number,
  pondId: string,
): XpGrantResult {
  const playerResult = applyPlayerXp(player, playerXp);
  const mod = getGamePondDef(pondId);
  const mul = mod ? getPondModifier(mod.pondCategory).pondXpMul : 1;
  const scaledPondXp = Math.round(pondXp * mul);
  const pondResult = applyPondXp(pond, scaledPondXp, playerResult.state.level);

  return {
    player: playerResult.state,
    pond: pondResult.state,
    playerLeveled: playerResult.leveled,
    pondLeveled: pondResult.leveled,
    pondXpGranted: pondResult.granted,
    pondXpCapped: pondResult.capped && scaledPondXp > 0,
    playerXpGranted: Math.max(0, Math.floor(playerXp)),
  };
}

/** Duration pond XP from effective fishing ms at current player level rate. */
export function calcDurationPondXp(playerLevel: number, fishingMs: number): number {
  const def = getPlayerLevelDef(playerLevel);
  if (!def || fishingMs <= 0) return 0;
  const hours = fishingMs / (60 * 60 * 1000);
  return Math.max(0, Math.floor(def.pondXpPerHour * hours));
}
