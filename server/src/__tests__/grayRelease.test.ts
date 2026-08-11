import { describe, it, expect } from 'vitest';
import { hashPlayerCohort, isPlayerInGrayCohort } from '../grayRelease.js';

describe('gray release cohort (C7)', () => {
  it('100% includes all players', () => {
    expect(isPlayerInGrayCohort('player-a', 100)).toBe(true);
  });

  it('0% excludes all players', () => {
    expect(isPlayerInGrayCohort('player-a', 0)).toBe(false);
  });

  it('cohort assignment is stable', () => {
    const id = 'cohort-test-player-xyz';
    expect(hashPlayerCohort(id)).toBe(hashPlayerCohort(id));
    expect(isPlayerInGrayCohort(id, 50)).toBe(isPlayerInGrayCohort(id, 50));
  });
});
