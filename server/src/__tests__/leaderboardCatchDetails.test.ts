import { afterAll, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

// Exercise the real queries in an isolated database, never the user's data.
const originalDbPath = vi.hoisted(() => {
  const previous = process.env.DB_PATH;
  process.env.DB_PATH = ':memory:';
  return previous;
});

import { db } from '../db.js';
import { ensurePlayer } from '../players.js';
import {
  clearLeaderboardCacheForTests,
  getMyLeaderboardRank,
  getPondLeaderboard,
  getRareLeaderboard,
  shanghaiWeekBounds,
} from '../leaderboard.js';

describe('count leaderboard representative catches', () => {
  const week = '2026-W44';
  const base = shanghaiWeekBounds(week).startMs;
  let metricId = 0;

  function catchFish(playerId: string, speciesId: string, sizeM: number, offset: number,
    pondId = 'pond-a', quality = 'purple', eventType = 'catch_accept') {
    db.prepare(`INSERT INTO fishing_metrics (id, event_type, player_id, pond_id, payload, created_at)
      VALUES (?, ?, ?, ?, ?, ?)`).run(`catch-${++metricId}`, eventType, playerId, pondId,
      JSON.stringify({ speciesId, quality, sizeM }), base + offset);
  }

  beforeAll(() => {
    for (const playerId of ['alice', 'bob', 'carol', 'dave', 'no-catches']) ensurePlayer(playerId, playerId);
  });

  beforeEach(() => {
    db.prepare('DELETE FROM fishing_metrics').run();
    clearLeaderboardCacheForTests();
    // Insert the later maximum first so a tie must actively select the earlier record.
    catchFish('alice', 'carp', 2, 500);
    catchFish('alice', 'silver_carp', 2, 200, 'pond-b', 'orange');
    catchFish('alice', 'crucian', 1, 100);
    catchFish('alice', 'black_bass', 2, 300, 'pond-a', 'purple', 'pending_catch_accept');
    catchFish('alice', 'blue_only', 99, 50, 'pond-b', 'blue');
    catchFish('bob', 'carp', 7, 600);
    catchFish('bob', 'crucian', 7, 400);
    catchFish('alice', 'not_accepted', 100, 1, 'pond-a', 'gold', 'catch_pending');
    catchFish('alice', 'previous_week', 100, -1);
  });

  afterAll(() => {
    db.close();
    if (originalDbPath === undefined) delete process.env.DB_PATH;
    else process.env.DB_PATH = originalDbPath;
  });

  it('keeps pond ranking by count and reports the earliest largest fish in that pond', () => {
    const entries = getPondLeaderboard('pond-a', { week });
    expect(entries.map(entry => [entry.playerId, entry.value])).toEqual([['alice', 3], ['bob', 2]]);
    expect(entries[0].extra).toEqual({
      catchCount: 3, sizeM: 2, speciesId: 'black_bass', pondId: 'pond-a', caughtAt: base + 300,
    });
  });

  it('selects rare representative fish across ponds while excluding lower quality and out-of-period catches', () => {
    const entries = getRareLeaderboard({ week });
    expect(entries.map(entry => [entry.playerId, entry.value])).toEqual([['alice', 4], ['bob', 2]]);
    expect(entries[0].extra).toEqual({
      catchCount: 4, sizeM: 2, speciesId: 'silver_carp', pondId: 'pond-b', caughtAt: base + 200,
    });
  });

  it.each(['pond', 'rare'] as const)('returns the same fish details for a player outside the %s board limit', board => {
    const options = { week, pondId: 'pond-a' };
    const full = getMyLeaderboardRank('bob', board, { ...options, limit: 20 });
    const outside = getMyLeaderboardRank('bob', board, { ...options, limit: 1 });
    expect(full.rank).toBe(2);
    expect(outside).toEqual({ rank: null, value: 2, entry: { ...full.entry, rank: 0 } });
    expect(outside.entry?.extra).toEqual({
      catchCount: 2, sizeM: 7, speciesId: 'crucian', pondId: 'pond-a', caughtAt: base + 400,
    });
    expect(getMyLeaderboardRank('no-catches', board, { ...options, limit: 1 })).toEqual({ rank: null, value: 0 });
  });

  it('preserves count ties by earliest catch and then player ID rather than representative size', () => {
    for (const [playerId, firstAt, sizeM] of [['carol', 90, 0.1], ['dave', 100, 10]] as const) {
      catchFish(playerId, 'carp', sizeM, firstAt);
      catchFish(playerId, 'carp', sizeM, 700);
      catchFish(playerId, 'carp', sizeM, 800);
    }
    expect(getPondLeaderboard('pond-a', { week }).map(entry => entry.playerId))
      .toEqual(['carol', 'alice', 'dave', 'bob']);
  });
});
