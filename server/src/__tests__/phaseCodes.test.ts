import { describe, it, expect } from 'vitest';
import {
  PHASE_CODES,
  PHASE_CODE_NULL,
  phaseToCode,
  codeToPhase,
  compactPhaseTransitionPayload,
  type FishingPhase,
} from '@fish-social/shared';

describe('phaseCodes (D-L2-16)', () => {
  it('maps all 9 phases 0–8 bijectively', () => {
    const phases = Object.keys(PHASE_CODES) as FishingPhase[];
    expect(phases).toHaveLength(9);
    for (const phase of phases) {
      const code = phaseToCode(phase);
      expect(code).toBeGreaterThanOrEqual(0);
      expect(code).toBeLessThanOrEqual(8);
      expect(codeToPhase(code)).toBe(phase);
    }
  });

  it('null from → -1 and back', () => {
    expect(phaseToCode(null)).toBe(PHASE_CODE_NULL);
    expect(phaseToCode(undefined)).toBe(PHASE_CODE_NULL);
    expect(codeToPhase(PHASE_CODE_NULL)).toBeNull();
  });

  it('compactPhaseTransitionPayload shape', () => {
    expect(compactPhaseTransitionPayload('waiting', 'hooked', 'bite_hook')).toEqual({
      f: 4,
      t: 5,
      c: 'bite_hook',
    });
    expect(compactPhaseTransitionPayload(null, 'seated', 'take_spot')).toEqual({
      f: -1,
      t: 1,
      c: 'take_spot',
    });
  });
});
