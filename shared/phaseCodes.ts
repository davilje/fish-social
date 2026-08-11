/**
 * D-L2-16: fishing phase codes for compact metrics payloads (0–8).
 * Logs may still use full FishingPhase names.
 */
import type { FishingPhase } from './types.js';

/** Null / missing from-phase in metrics */
export const PHASE_CODE_NULL = -1;

export const PHASE_CODES: Record<FishingPhase, number> = {
  idle: 0,
  seated: 1,
  baiting: 2,
  casting: 3,
  waiting: 4,
  hooked: 5,
  resolving: 6,
  stopping: 7,
  disconnected: 8,
};

export const PHASE_FROM_CODE: Readonly<Record<number, FishingPhase>> = {
  0: 'idle',
  1: 'seated',
  2: 'baiting',
  3: 'casting',
  4: 'waiting',
  5: 'hooked',
  6: 'resolving',
  7: 'stopping',
  8: 'disconnected',
};

export function phaseToCode(phase: FishingPhase | null | undefined): number {
  if (phase == null) return PHASE_CODE_NULL;
  return PHASE_CODES[phase];
}

export function codeToPhase(code: number): FishingPhase | null {
  if (code === PHASE_CODE_NULL || code < 0) return null;
  return PHASE_FROM_CODE[code] ?? null;
}

/** Compact metrics payload for fishing_phase_transition / phase_transition_invalid */
export function compactPhaseTransitionPayload(
  fromPhase: FishingPhase | null | undefined,
  toPhase: FishingPhase,
  cause: string,
): { f: number; t: number; c: string } {
  return {
    f: phaseToCode(fromPhase),
    t: phaseToCode(toPhase),
    c: cause,
  };
}
