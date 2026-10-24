import { randomUUID } from 'crypto';
import type { Server, Socket } from 'socket.io';
import { afterAll, afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  PONDS, getGamePondDef, pondAllowsReturnFish,
  type ClientToServerEvents, type PondUser, type ServerToClientEvents,
} from '@fish-social/shared';

const originalDbPath = vi.hoisted(() => {
  const original = process.env.DB_PATH;
  process.env.DB_PATH = ':memory:';
  return original;
});

import { db } from '../db.js';
import {
  buildSnapshot, clearSession, createHumanPondUser, reconnectSession, removeDisconnectedUser,
} from '../gameState.js';
import {
  flushHookedPhaseToPendingCatch, flushResolvingPhase, getHookContext, handleDisconnect,
  handleStopFishing, resumeAfterReconnect, scheduleHookFromBite, tickFishingPhases,
} from '../fishingStateMachine.js';
import { getInventory, getPendingCatch } from '../inventory.js';
import { spawnDebugHookFish } from '../pondEcology.js';
import { ensurePlayer } from '../players.js';
import { completeOnboarding } from '../playerProgress.js';
import { ensurePlayerGear } from '../gear.js';
import { clearPondSessionLedger, ensurePondSessionLedger } from '../pondSessionLedger.js';
import { registerSocketPondHandlers } from '../socketPondHandlers.js';
import { resetSocketEventRateLimitForTests } from '../socketEventRateLimit.js';
import { cancelAll } from '../timerRegistry.js';

const emissions: Array<{ event: string; payload: any }> = [];
const users: Array<{ user: PondUser; pondId: string }> = [];
const io = {
  to: () => ({ emit: (event: string, payload: unknown) => {
    emissions.push({ event, payload: JSON.parse(JSON.stringify(payload)) });
  } }),
} as unknown as Server<ClientToServerEvents, ServerToClientEvents>;

function createFisher(autoReturn = false): { user: PondUser; pondId: string } {
  const pond = autoReturn
    ? PONDS.find((entry) => pondAllowsReturnFish(getGamePondDef(entry.id)))!
    : PONDS.find((entry) => entry.id === 'pond-calm')!;
  expect(pond).toBeDefined();
  const playerId = randomUUID();
  ensurePlayer(playerId, 'Audio identity');
  completeOnboarding(playerId);
  ensurePlayerGear(playerId);
  db.prepare('UPDATE players SET coins = 100000 WHERE player_id = ?').run(playerId);
  db.prepare('UPDATE player_fishing_progress SET level = 100 WHERE player_id = ?').run(playerId);
  const user = createHumanPondUser(pond.id, playerId, 'Audio identity', autoReturn ? 'auto_return' : 'sell_only');
  user.spotId = pond.spots[0].id;
  user.fishingPhase = 'waiting';
  reconnectSession(user.id, pond.id, user.id, playerId, user.nickname);
  ensurePondSessionLedger(playerId, pond.id, autoReturn ? 'auto_return' : 'sell_only');
  const entry = { user, pondId: pond.id };
  users.push(entry);
  return entry;
}

function bite(entry: { user: PondUser; pondId: string }, escaped = false): string {
  const { user, pondId } = entry;
  const fish = spawnDebugHookFish(pondId, user.spotId!, 'purple', 0.9)!;
  // Deterministic eligible size for the real automatic-return path.
  fish.sizeM = 2;
  db.prepare('UPDATE pond_fish SET size_m = ? WHERE id = ?').run(fish.sizeM, fish.id);
  scheduleHookFromBite(io, pondId, user.id, user.id, { fish, escaped, hookDurationMs: 1000 });
  expect(user.fishingPhase).toBe('hooked');
  const catchId = user.phaseContext!.catchId!;
  expect(catchId).toMatch(/^[0-9a-f-]{36}$/);
  expect(getHookContext(user.id)?.catchId).toBe(catchId);
  expect(user.phaseContext?.sizeM).toBe(2);
  return catchId;
}

function acceptHandler(user: PondUser) {
  const handlers = new Map<string, (...args: any[]) => void>();
  const socket = {
    id: user.id, data: {},
    on: (event: string, handler: (...args: any[]) => void) => handlers.set(event, handler),
  } as unknown as Socket<ClientToServerEvents, ServerToClientEvents>;
  registerSocketPondHandlers(socket, {
    io, resolveSocketPlayerId: () => user.playerId!, roomFanoutCount: () => 1,
  });
  return handlers.get('accept_catch')!;
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(Date.parse('2026-10-24T10:00:00Z'));
  resetSocketEventRateLimitForTests();
  emissions.length = 0;
});

afterEach(() => {
  for (const { user, pondId } of users.splice(0)) {
    removeDisconnectedUser(pondId, user.id);
    clearSession(user.id);
    clearPondSessionLedger(user.playerId!);
    db.prepare('DELETE FROM pond_fish WHERE pond_id = ?').run(pondId);
  }
  cancelAll();
  vi.useRealTimers();
});

afterAll(() => {
  if (originalDbPath === undefined) delete process.env.DB_PATH;
  else process.env.DB_PATH = originalDbPath;
});

describe('authoritative audio presentation identities', () => {
  it.each([false, true])('uses one catchId in bite, pending, settled and ACK (autoReturn=%s)', (autoReturn) => {
    const entry = createFisher(autoReturn);
    const { user, pondId } = entry;
    const catchId = bite(entry);
    expect(flushHookedPhaseToPendingCatch(io, pondId, user.id, user.id, { deferAutoSettle: true })).toBe(true);
    expect(getPendingCatch(user.id)?.catchId).toBe(catchId);
    expect(user.phaseContext?.catchId).toBe(catchId);
    const ack = vi.fn();
    const accept = acceptHandler(user);
    accept(catchId, ack);
    expect(ack).toHaveBeenCalledWith(expect.objectContaining({ ok: true, catchId }));
    const results = emissions.filter(({ event }) => event === 'fish_catch_settled');
    expect(results).toHaveLength(1);
    expect(results[0].payload).toMatchObject({ catchId, autoReturned: autoReturn });
    expect(getInventory(user.playerId!)).toHaveLength(autoReturn ? 0 : 1);
    // Replayed commands must not create a second reward or a different result ID.
    accept(catchId, ack);
    expect(ack).toHaveBeenLastCalledWith(expect.objectContaining({ ok: false }));
    expect(emissions.filter(({ event }) => event === 'fish_catch_settled')).toHaveLength(1);
    expect(getInventory(user.playerId!)).toHaveLength(autoReturn ? 0 : 1);
  });

  it.each([false, true])('preserves catchId in automatic settlement (autoReturn=%s)', (autoReturn) => {
    const entry = createFisher(autoReturn);
    const catchId = bite(entry);
    vi.setSystemTime(entry.user.phaseEndsAt!);
    tickFishingPhases(io, new Map([[entry.user.id, entry.user.id]]));
    expect(emissions.find(({ event }) => event === 'fish_catch_settled')?.payload)
      .toMatchObject({ catchId, autoReturned: autoReturn });
  });

  it('keeps replayed phase snapshots stable and gives two adjacent catches distinct IDs', () => {
    const entry = createFisher();
    const { user, pondId } = entry;
    const first = bite(entry);
    for (let repeat = 0; repeat < 3; repeat++) {
      expect(buildSnapshot(pondId, user.playerId)!.users.find((item) => item.id === user.id)?.phaseContext?.catchId)
        .toBe(first);
    }
    flushHookedPhaseToPendingCatch(io, pondId, user.id, user.id);
    flushResolvingPhase(io, pondId, user.id, user.id);
    // Skip only test animation wait; both are real distinct authoritative results at the same clock time.
    user.fishingPhase = 'waiting';
    const second = bite(entry);
    expect(second).not.toBe(first);
    flushHookedPhaseToPendingCatch(io, pondId, user.id, user.id);
    expect(emissions.filter(({ event }) => event === 'fish_catch_settled').map(({ payload }) => payload.catchId))
      .toEqual([first, second]);
  });

  it('retains the bite identity across disconnect/reconnect and its escaped result', () => {
    const entry = createFisher();
    const { user, pondId } = entry;
    const catchId = bite(entry, true);
    handleDisconnect(pondId, user.id, vi.fn());
    vi.setSystemTime(Date.now() + 150);
    resumeAfterReconnect(io, pondId, user, user.id);
    expect(user.phaseContext?.catchId).toBe(catchId);
    flushHookedPhaseToPendingCatch(io, pondId, user.id, user.id);
    expect(user.phaseContext).toMatchObject({ catchId, resultId: catchId, outcome: 'escape' });
    expect(emissions.find(({ event }) => event === 'fish_miss')?.payload)
      .toMatchObject({ resultId: catchId, actorId: user.id, pondId, reason: 'escaped' });
    for (let repeat = 0; repeat < 3; repeat++) {
      expect(buildSnapshot(pondId, user.playerId)!.users.find((item) => item.id === user.id)?.phaseContext?.resultId)
        .toBe(catchId);
    }
    expect(flushHookedPhaseToPendingCatch(io, pondId, user.id, user.id)).toBe(false);
    expect(emissions.filter(({ event }) => event === 'fish_miss')).toHaveLength(1);
  });

  it('preserves the authoritative escape identity on stop, without inventing failure on ordinary stop', () => {
    const entry = createFisher();
    const catchId = bite(entry);
    handleStopFishing(io, entry.pondId, entry.user.id);
    expect(entry.user.phaseContext).toMatchObject({ outcome: 'escape', catchId, resultId: catchId });
    handleStopFishing(io, entry.pondId, entry.user.id);
    expect(entry.user.phaseContext?.resultId).toBe(catchId);
    const ordinary = createFisher();
    handleStopFishing(io, ordinary.pondId, ordinary.user.id);
    expect(ordinary.user.phaseContext?.resultId).toBeUndefined();
    expect(emissions.filter(({ event }) => event === 'fish_miss')).toHaveLength(0);
  });
});
