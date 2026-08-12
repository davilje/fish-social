import { describe, expect, it } from 'vitest';
import { PONDS, POND_ECOSYSTEM_TICK_MS } from '@fish-social/shared';
import { db } from '../db.js';
import {
  ensurePondEcologyCurrent,
  listPondFishEntities,
  resetAllEcology,
  tickAllPonds,
} from '../pondEcology.js';

describe('offline pond ecology', () => {
  it('does not tick empty ponds and wakes idempotently', async () => {
    const pondId = PONDS[0]!.id;
    resetAllEcology();

    const beforeFish = listPondFishEntities(pondId);
    const beforeState = db
      .prepare('SELECT last_simulated_at FROM pond_state WHERE pond_id = ?')
      .get(pondId) as { last_simulated_at: number };

    tickAllPonds();
    const asleepState = db
      .prepare('SELECT last_simulated_at FROM pond_state WHERE pond_id = ?')
      .get(pondId) as { last_simulated_at: number };
    expect(asleepState.last_simulated_at).toBe(beforeState.last_simulated_at);

    const wakeAt = beforeState.last_simulated_at + POND_ECOSYSTEM_TICK_MS * 4;
    const first = ensurePondEcologyCurrent(pondId, wakeAt);
    expect(first?.offlineMs).toBe(POND_ECOSYSTEM_TICK_MS * 4);
    expect(first?.replaySteps).toBe(4);
    const afterFirstWake = listPondFishEntities(pondId);
    expect(afterFirstWake.length).toBeGreaterThanOrEqual(beforeFish.length);

    const second = ensurePondEcologyCurrent(pondId, wakeAt);
    expect(second?.offlineMs).toBe(0);
    expect(second?.replaySteps).toBe(0);
    expect(listPondFishEntities(pondId).length).toBe(afterFirstWake.length);

    const concurrent = await Promise.all([
      Promise.resolve(ensurePondEcologyCurrent(pondId, wakeAt + POND_ECOSYSTEM_TICK_MS)),
      Promise.resolve(ensurePondEcologyCurrent(pondId, wakeAt + POND_ECOSYSTEM_TICK_MS)),
    ]);
    expect(concurrent.filter((result) => result?.offlineMs > 0)).toHaveLength(1);
  });
});

