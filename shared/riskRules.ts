import type { PondCategory } from './gameDataTypes';
import { getPondModifier } from './gameData.client';

export const POLICE_WARNING_TEXT = '巡警来了！快跑！';
export const POLICE_ESCAPE_BAN_MS = 2 * 60 * 60 * 1000;
export const DEFAULT_POLICE_WARNING_MS = 10_000;
export const DEFAULT_FINE_CHANCE_PER_HOUR = 0.15;
export const DEFAULT_FINE_GOLD = 800;

export interface PoliceRules {
  enabled: boolean;
  chancePerHour: number;
  fineGold: number;
  warningMs: number;
}

export function getPoliceRules(category: PondCategory | string | null | undefined): PoliceRules {
  if (category !== 'forbidden') {
    return {
      enabled: false,
      chancePerHour: 0,
      fineGold: 0,
      warningMs: DEFAULT_POLICE_WARNING_MS,
    };
  }
  const mod = getPondModifier('forbidden');
  const chancePerHour = mod.fineChancePerHour ?? DEFAULT_FINE_CHANCE_PER_HOUR;
  const fineGold = mod.fineGold ?? DEFAULT_FINE_GOLD;
  const warningMs = mod.policeWarningMs ?? DEFAULT_POLICE_WARNING_MS;
  return {
    enabled: chancePerHour > 0,
    chancePerHour,
    fineGold,
    warningMs,
  };
}

/** Probability of triggering during a dtMs fishing interval given hourly chance. */
export function policeTriggerProbability(chancePerHour: number, dtMs: number): number {
  if (chancePerHour <= 0 || dtMs <= 0) return 0;
  const hours = dtMs / 3_600_000;
  return 1 - Math.pow(1 - Math.min(1, chancePerHour), hours);
}
