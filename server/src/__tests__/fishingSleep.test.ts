import { randomUUID } from 'crypto';
import Database from 'better-sqlite3';
import type { Server, Socket } from 'socket.io';
import { afterAll, afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ClientToServerEvents, PondUser, ServerToClientEvents } from '@fish-social/shared';

const originalEnv = vi.hoisted(() => {
  const saved = { db: process.env.DB_PATH, instant: process.env.FISHING_TEST_MODE };
  process.env.DB_PATH = ':memory:';
  process.env.FISHING_TEST_MODE = 'instant';
  return saved;
});

import { db } from '../db.js';
import {
  buildSnapshot, clearSession, createHumanPondUser, detachPondUserForCheckpointTest,
  enrichPondUser, getSession, getUserById, leavePond, leaveSpot, reconnectSession,
  removeDisconnectedUser, restoreUserFromCheckpoint, syncPlayerAvatarToPonds,
} from '../gameState.js';
import {
  beginFishingSequence, beginGroundbaitSequence, flushHookedPhaseToPendingCatch, flushResolvingPhase,
  handleDisconnect, handleFishingAttention, handleStopFishing, processWaitingBiteTick,
  resumeAfterReconnect, scheduleHookFromBite, tickFishingPhases,
} from '../fishingStateMachine.js';
import { getFishingAttentionAt } from '../fishingSleep.js';
import {
  approveConfigChange, getConfigDefault, getFishingSleepAfterMs, submitConfigChange,
} from '../gameConfig.js';
import { loadPlayerPondSession, upsertPlayerPondSession } from '../playerPondSession.js';
import { migrateFishingSleep } from '../migrations/fishing_sleep.js';
import { ensurePlayer, getPlayer } from '../players.js';
import { completeOnboarding, ensurePlayerProgress } from '../playerProgress.js';
import { ensurePlayerGear, resolveBaitForBite } from '../gear.js';
import * as gearModule from '../gear.js';
import { getInventory, getPendingCatch } from '../inventory.js';
import { getPondFishById, spawnDebugHookFish } from '../pondEcology.js';
import * as ecologyModule from '../pondEcology.js';
import { buildPondSessionSummary, clearPondSessionLedger, ensurePondSessionLedger } from '../pondSessionLedger.js';
import { cancelAll } from '../timerRegistry.js';
import { registerSocketPondHandlers } from '../socketPondHandlers.js';
import { resetSocketEventRateLimitForTests } from '../socketEventRateLimit.js';

const pondId = 'pond-calm';
const spotId = 'calm-spot-1';
const timeoutMs = 1_800_000;
const startAt = Date.parse('2026-10-24T10:00:00Z');
const users: PondUser[] = [];
const emissions: Array<{ event: string; payload: any }> = [];
const io = {
  to: () => ({ emit: (event: string, payload: unknown) => {
    emissions.push({ event, payload: JSON.parse(JSON.stringify(payload)) });
  } }),
} as unknown as Server<ClientToServerEvents, ServerToClientEvents>;

function configure(value: number | string): void {
  db.prepare(`INSERT INTO game_config (config_key, config_value, updated_at)
    VALUES ('FISHING_SLEEP_AFTER_MS', ?, ?)
    ON CONFLICT(config_key) DO UPDATE SET config_value = excluded.config_value`)
    .run(String(value), Date.now());
}

function createFisher(): PondUser {
  const playerId = randomUUID();
  ensurePlayer(playerId, 'Sleep test');
  completeOnboarding(playerId);
  ensurePlayerGear(playerId);
  db.prepare('UPDATE players SET coins = 1000 WHERE player_id = ?').run(playerId);
  const user = createHumanPondUser(pondId, playerId, 'Sleep test', 'sell_only');
  users.push(user);
  user.spotId = spotId;
  user.fishingPhase = 'seated';
  reconnectSession(user.id, pondId, user.id, playerId, user.nickname);
  ensurePondSessionLedger(playerId, pondId, 'sell_only');
  expect(beginFishingSequence(io, pondId, user.id, user.id).ok).toBe(true);
  return user;
}

function tickAt(atMs: number): void {
  vi.setSystemTime(atMs);
  tickFishingPhases(io, new Map(users.map((user) => [user.id, user.id])));
}

function waitForBite(user: PondUser): void {
  while (user.fishingPhase === 'baiting' || user.fishingPhase === 'casting') {
    tickAt(user.phaseEndsAt!);
  }
  expect(user.fishingPhase).toBe('waiting');
}

function bite(user: PondUser): boolean {
  return processWaitingBiteTick(io, pondId, user.id, user.playerId!, spotId, user.id);
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(startAt);
  configure(timeoutMs);
  resetSocketEventRateLimitForTests();
  emissions.length = 0;
});

afterEach(() => {
  for (const user of users.splice(0)) {
    removeDisconnectedUser(pondId, user.id);
    clearSession(user.id);
    clearPondSessionLedger(user.playerId!);
  }
  cancelAll();
  db.prepare('DELETE FROM pond_fish WHERE pond_id = ?').run(pondId);
  vi.useRealTimers();
  vi.restoreAllMocks();
});

afterAll(() => {
  if (originalEnv.db === undefined) delete process.env.DB_PATH;
  else process.env.DB_PATH = originalEnv.db;
  if (originalEnv.instant === undefined) delete process.env.FISHING_TEST_MODE;
  else process.env.FISHING_TEST_MODE = originalEnv.instant;
});

describe('authoritative fishing sleep', () => {
  it('sleeps exactly at the deadline, without snapshots, avatar updates or repeated start resetting it', () => {
    const user = createFisher();
    waitForBite(user);
    const deadline = startAt + timeoutMs;
    expect(user.fishingSleepAt).toBe(deadline);
    expect(beginFishingSequence(io, pondId, user.id).ok).toBe(false);
    tickAt(deadline - 1);
    expect(user.fishingAsleep).toBe(false);
    for (let repeat = 0; repeat < 5; repeat++) {
      expect(buildSnapshot(pondId, user.playerId)!.users.find((entry) => entry.id === user.id))
        .toMatchObject({ fishingAsleep: false, fishingSleepAt: deadline });
      enrichPondUser(user);
    }
    syncPlayerAvatarToPonds(user.playerId!, 'avatar-test');
    tickAt(deadline);
    expect(user.fishingAsleep).toBe(true);
    expect(user.fishingSleepAt).toBe(deadline);
    expect(loadPlayerPondSession(user.playerId!, pondId)?.fishing_asleep).toBe(1);
    tickAt(deadline + timeoutMs);
    expect(user.fishingAsleep).toBe(true);
    expect(getFishingAttentionAt(user)).toBe(startAt);
  });

  it('does not interrupt a hooked or resolving animation and sleeps on the next waiting phase', () => {
    const user = createFisher();
    waitForBite(user);
    tickAt(startAt + timeoutMs - 1);
    const fish = spawnDebugHookFish(pondId, spotId, 'gray', 0.5)!;
    scheduleHookFromBite(io, pondId, user.id, user.id, { fish, escaped: false, hookDurationMs: 1000 });
    tickAt(startAt + timeoutMs);
    expect(user).toMatchObject({ fishingPhase: 'hooked', fishingAsleep: false });
    tickAt(user.phaseEndsAt!);
    expect(user).toMatchObject({ fishingPhase: 'resolving', fishingAsleep: false });
    expect(getInventory(user.playerId!)).toHaveLength(1);
    tickAt(user.phaseEndsAt!);
    expect(user.fishingAsleep).toBe(false);
    waitForBite(user);
    expect(user.fishingAsleep).toBe(true);
    expect(user.fishingSleepAt).toBe(startAt + timeoutMs);
  });

  it('settles sleeping bites as escapes with pool, bait and groundbait effects but no catch rewards', () => {
    const escapeGrowth = vi.spyOn(ecologyModule, 'applyEscapeGrowthBonus');
    const escapeDurability = vi.spyOn(gearModule, 'applyTackleDurabilityOnEscape');
    const user = createFisher();
    waitForBite(user);
    const fish = spawnDebugHookFish(pondId, spotId, 'gray', 0.3)!;
    db.prepare('UPDATE player_fishing_progress SET level = 10 WHERE player_id = ?').run(user.playerId);
    user.groundbait = { groundbaitId: 'gb-basic', stackCount: 1, expiresAt: 0,
      bitesLeft: 3, biteBonus: 0.1, sizeBonus: 0 };
    const progressBefore = ensurePlayerProgress(user.playerId!);
    const coinsBefore = getPlayer(user.playerId!)!.coins;
    const baitCost = resolveBaitForBite(user.playerId!, fish.speciesId).cost;
    expect(baitCost).toBeGreaterThan(0);
    vi.setSystemTime(startAt + timeoutMs);
    expect(bite(user)).toBe(true);
    expect(user).toMatchObject({ fishingAsleep: true, fishingPhase: 'resolving', phaseContext: { outcome: 'escape' } });
    expect(user.groundbait?.bitesLeft).toBe(2);
    expect(getInventory(user.playerId!)).toEqual([]);
    expect(getPendingCatch(user.id)).toBeUndefined();
    expect(getPlayer(user.playerId!)!.coins).toBe(coinsBefore - baitCost);
    expect(ensurePlayerProgress(user.playerId!)).toEqual(progressBefore);
    expect(escapeGrowth).toHaveBeenCalledExactlyOnceWith(fish.id);
    expect(escapeDurability).toHaveBeenCalledExactlyOnceWith(user.playerId, io);
    expect(getPondFishById(fish.id)!.sizeM).toBeGreaterThanOrEqual(fish.sizeM);
    expect(buildPondSessionSummary(user.playerId!)).toMatchObject({ biteCount: 1, escapeCount: 1, catches: [] });
    expect(emissions.some(({ event, payload }) => event === 'fish_miss' && payload.reason === 'escaped')).toBe(true);
    expect(emissions.some(({ event }) => event === 'fish_catch_settled')).toBe(false);
    expect(emissions.some(({ event, payload }) => event === 'pond_user_updated' && payload.fishingPhase === 'hooked')).toBe(false);
    expect(handleFishingAttention(io, pondId, user.id).ok).toBe(true);
    expect(user.phaseContext?.outcome).toBe('escape');
    flushResolvingPhase(io, pondId, user.id, user.id);
    waitForBite(user);
    expect(bite(user)).toBe(true);
    expect(user.fishingPhase).toBe('hooked');
    flushHookedPhaseToPendingCatch(io, pondId, user.id, user.id);
    expect(getInventory(user.playerId!)).toHaveLength(1);
    expect(getPondFishById(fish.id)).toBeNull();
  });

  it('keeps sleeping across automatic rebaiting and clicks rearm the server deadline', () => {
    const user = createFisher();
    spawnDebugHookFish(pondId, spotId, 'gray', 0.3);
    waitForBite(user);
    tickAt(startAt + timeoutMs);
    expect(bite(user)).toBe(true);
    flushResolvingPhase(io, pondId, user.id, user.id);
    waitForBite(user);
    expect(user.fishingAsleep).toBe(true);
    expect(user.fishingSleepAt).toBe(startAt + timeoutMs);
    const clickedAt = Date.now();
    handleFishingAttention(io, pondId, user.id);
    expect(user).toMatchObject({ fishingAsleep: false, fishingSleepAt: clickedAt + timeoutMs });
    tickAt(clickedAt + timeoutMs - 1);
    expect(user.fishingAsleep).toBe(false);
    tickAt(clickedAt + timeoutMs);
    expect(user.fishingAsleep).toBe(true);
  });

  it('rejects start while asleep both actively fishing and automatically paused in seated', () => {
    const user = createFisher();
    waitForBite(user);
    tickAt(user.fishingSleepAt!);
    const deadline = user.fishingSleepAt;
    expect(beginFishingSequence(io, pondId, user.id).ok).toBe(false);
    expect(user).toMatchObject({ fishingAsleep: true, fishingSleepAt: deadline });
    spawnDebugHookFish(pondId, spotId, 'gray', 0.3);
    expect(bite(user)).toBe(true);
    db.prepare("UPDATE player_gear SET equipped_rod = '', owned_rods = '[]' WHERE player_id = ?")
      .run(user.playerId);
    flushResolvingPhase(io, pondId, user.id, user.id);
    expect(user).toMatchObject({ fishingPhase: 'seated', fishingAsleep: true });
    db.prepare('UPDATE player_gear SET equipped_rod = ?, owned_rods = ? WHERE player_id = ?')
      .run('rod-bamboo', JSON.stringify(['rod-bamboo']), user.playerId);
    expect(beginFishingSequence(io, pondId, user.id).ok).toBe(false);
    expect(user).toMatchObject({ fishingAsleep: true, fishingSleepAt: deadline });
    expect(getFishingAttentionAt(user)).toBe(startAt);
    expect(handleFishingAttention(io, pondId, user.id).ok).toBe(true);
    expect(beginFishingSequence(io, pondId, user.id).ok).toBe(true);
    expect(user.fishingAsleep).toBe(false);
  });

  it('rejects groundbait before consumption while asleep and permits it after touch', () => {
    const user = createFisher();
    db.prepare('UPDATE player_fishing_progress SET level = 10 WHERE player_id = ?').run(user.playerId);
    waitForBite(user);
    tickAt(user.fishingSleepAt!);
    expect(beginGroundbaitSequence(io, pondId, user.id, 'gb-basic').ok).toBe(false);
    spawnDebugHookFish(pondId, spotId, 'gray', 0.3);
    expect(bite(user)).toBe(true);
    db.prepare("UPDATE player_gear SET equipped_rod = '', owned_rods = '[]' WHERE player_id = ?")
      .run(user.playerId);
    flushResolvingPhase(io, pondId, user.id, user.id);
    expect(user).toMatchObject({ fishingPhase: 'seated', fishingAsleep: true });
    const coinsBefore = getPlayer(user.playerId!)!.coins;
    const stateBefore = JSON.stringify(user);
    const checkpointBefore = loadPlayerPondSession(user.playerId!, pondId);
    expect(beginGroundbaitSequence(io, pondId, user.id, 'gb-basic').ok).toBe(false);
    expect(getPlayer(user.playerId!)!.coins).toBe(coinsBefore);
    expect(JSON.stringify(user)).toBe(stateBefore);
    expect(loadPlayerPondSession(user.playerId!, pondId)).toEqual(checkpointBefore);
    expect(getFishingAttentionAt(user)).toBe(startAt);
    expect(handleFishingAttention(io, pondId, user.id).ok).toBe(true);
    expect(beginGroundbaitSequence(io, pondId, user.id, 'gb-basic').ok).toBe(true);
    expect(user).toMatchObject({ fishingPhase: 'groundbaiting', fishingAsleep: false });
    expect(getPlayer(user.playerId!)!.coins).toBe(coinsBefore - 30);
  });

  it.each([
    { alreadyAsleep: false, fromDatabase: false },
    { alreadyAsleep: true, fromDatabase: false },
    { alreadyAsleep: false, fromDatabase: true },
    { alreadyAsleep: true, fromDatabase: true },
  ])('retains deadline on reconnect: %j', ({ alreadyAsleep, fromDatabase }) => {
    let user = createFisher();
    spawnDebugHookFish(pondId, spotId, 'gray', 0.3);
    waitForBite(user);
    tickAt(startAt + timeoutMs - (alreadyAsleep ? 0 : 1000));
    handleDisconnect(pondId, user.id, vi.fn());
    clearSession(user.id);
    vi.setSystemTime(startAt + timeoutMs + 1000);
    if (!fromDatabase) {
      reconnectSession(user.id, pondId, user.id, user.playerId!, user.nickname);
    } else {
      detachPondUserForCheckpointTest(pondId, user.id);
      user = restoreUserFromCheckpoint(user.id, pondId, user.playerId!, user.nickname)!;
    }
    resumeAfterReconnect(io, pondId, user, user.id);
    expect(user).toMatchObject({ fishingPhase: 'waiting', fishingAsleep: true, fishingSleepAt: startAt + timeoutMs });
    expect(user.fishingStartedAt).toBe(Date.now());
    expect(getFishingAttentionAt(user)).toBe(startAt);
    const row = loadPlayerPondSession(user.playerId!, pondId)!;
    expect(row.fishing_attention_at).toBe(startAt);
    expect(bite(user)).toBe(true);
    expect(getInventory(user.playerId!)).toEqual([]);
    handleFishingAttention(io, pondId, user.id);
    upsertPlayerPondSession(user, pondId);
    expect(loadPlayerPondSession(user.playerId!, pondId)?.fishing_asleep).toBe(0);
  });

  it('keeps the original deadline on an early reconnect and extends it only on an awake click', () => {
    const user = createFisher();
    waitForBite(user);
    tickAt(startAt + 60_000);
    handleDisconnect(pondId, user.id, vi.fn());
    vi.setSystemTime(startAt + 90_000);
    resumeAfterReconnect(io, pondId, user, user.id);
    expect(user).toMatchObject({ fishingAsleep: false, fishingSleepAt: startAt + timeoutMs });
    handleFishingAttention(io, pondId, user.id);
    expect(user.fishingSleepAt).toBe(startAt + 90_000 + timeoutMs);
    tickAt(startAt + timeoutMs);
    expect(user.fishingAsleep).toBe(false);
  });

  it.each([false, true])('clears sleep through an explicit stop during resolving, asleep=%s', (asleep) => {
    const user = createFisher();
    waitForBite(user);
    spawnDebugHookFish(pondId, spotId, 'gray', 0.3);
    if (asleep) tickAt(user.fishingSleepAt!);
    expect(bite(user)).toBe(true);
    handleStopFishing(io, pondId, user.id);
    expect(user.fishingPhase).toBe('resolving');
    tickAt(Date.now() + 1);
    expect(user).toMatchObject({ fishingAsleep: false, fishingSleepAt: null });
    expect(handleFishingAttention(io, pondId, user.id).ok).toBe(false);
    tickAt(user.phaseEndsAt!);
    expect(user).toMatchObject({ fishingPhase: 'seated', fishingAsleep: false, fishingSleepAt: null });
    expect(getFishingAttentionAt(user)).toBeNull();
    expect(beginFishingSequence(io, pondId, user.id).ok).toBe(true);
    expect(user.fishingSleepAt).toBe(Date.now() + timeoutMs);
  });

  it('applies dynamic settings to the original attention time and disabling never wakes sleeping players', () => {
    const user = createFisher();
    waitForBite(user);
    configure(3_600_000);
    tickAt(startAt + timeoutMs);
    expect(user).toMatchObject({ fishingAsleep: false, fishingSleepAt: startAt + 3_600_000 });
    configure(0);
    tickAt(startAt + 3_600_000);
    expect(user).toMatchObject({ fishingAsleep: false, fishingSleepAt: null });
    configure(60_000);
    tickAt(Date.now());
    expect(user.fishingAsleep).toBe(true);
    configure(0);
    tickAt(Date.now());
    expect(user.fishingAsleep).toBe(true);
    handleFishingAttention(io, pondId, user.id);
    expect(user).toMatchObject({ fishingAsleep: false, fishingSleepAt: null });
  });

  it.each([
    { phase: 'stopping', disconnect: false },
    { phase: 'resolving', disconnect: false },
    { phase: 'stopping', disconnect: true },
    { phase: 'resolving', disconnect: true },
  ])('retains explicit stop intent across checkpoint recovery: %j', ({ phase, disconnect }) => {
    let user = createFisher();
    waitForBite(user);
    tickAt(user.fishingSleepAt!);
    if (phase === 'resolving') {
      spawnDebugHookFish(pondId, spotId, 'gray', 0.3);
      expect(bite(user)).toBe(true);
    }
    handleStopFishing(io, pondId, user.id);
    expect(user.fishingPhase).toBe(phase);
    if (disconnect) handleDisconnect(pondId, user.id, vi.fn());
    detachPondUserForCheckpointTest(pondId, user.id);
    user = restoreUserFromCheckpoint(user.id, pondId, user.playerId!, user.nickname)!;
    resumeAfterReconnect(io, pondId, user, user.id);
    expect(user).toMatchObject({ fishingPhase: 'seated', fishingAsleep: false, fishingSleepAt: null });
    expect(getFishingAttentionAt(user)).toBeNull();
    expect(beginFishingSequence(io, pondId, user.id).ok).toBe(true);
    expect(user.fishingSleepAt).toBe(Date.now() + timeoutMs);
  });

  it.each([false, true])('revokes the previous socket session when reconnecting, checkpoint=%s', (checkpoint) => {
    const user = createFisher();
    waitForBite(user);
    tickAt(user.fishingSleepAt!);
    const newSocketId = `${user.id}-reconnected`;
    if (checkpoint) restoreUserFromCheckpoint(newSocketId, pondId, user.playerId!, user.nickname);
    else reconnectSession(newSocketId, pondId, user.id, user.playerId!, user.nickname);
    expect(getSession(user.id)).toBeUndefined();
    expect(getSession(newSocketId)?.userId).toBe(user.id);
    expect(user.fishingAsleep).toBe(true);
    clearSession(newSocketId);
  });

  it.each([0, 60_000, 86_400_000])('never wakes an already sleeping checkpoint when config changes to %s', (setting) => {
    let user = createFisher();
    waitForBite(user);
    tickAt(user.fishingSleepAt!);
    handleDisconnect(pondId, user.id, vi.fn());
    detachPondUserForCheckpointTest(pondId, user.id);
    configure(setting);
    user = restoreUserFromCheckpoint(user.id, pondId, user.playerId!, user.nickname)!;
    resumeAfterReconnect(io, pondId, user, user.id);
    tickAt(Date.now() + 1);
    expect(user).toMatchObject({ fishingAsleep: true, fishingSleepAt: startAt + timeoutMs });
  });

  it('exempts bots and clears sleep on explicit stop, leave and expired disconnect cleanup', () => {
    const bot = createFisher();
    bot.isBot = true;
    waitForBite(bot);
    tickAt(startAt + timeoutMs);
    expect(bot.fishingAsleep).toBe(false);
    expect(handleFishingAttention(io, pondId, bot.id).ok).toBe(false);
    const user = createFisher();
    waitForBite(user);
    tickAt(user.fishingSleepAt!);
    expect(user.fishingAsleep).toBe(true);
    handleStopFishing(io, pondId, user.id);
    expect(user).toMatchObject({ fishingAsleep: false, fishingSleepAt: null });
    tickAt(user.phaseEndsAt!);
    expect(leaveSpot(user.id, pondId).ok).toBe(true);
    expect(loadPlayerPondSession(user.playerId!, pondId)?.fishing_attention_at).toBeNull();
    leavePond(user.id);
    expect(loadPlayerPondSession(user.playerId!, pondId)).toBeUndefined();
    const expired = createFisher();
    waitForBite(expired);
    tickAt(expired.fishingSleepAt!);
    handleDisconnect(pondId, expired.id, () => removeDisconnectedUser(pondId, expired.id));
    vi.advanceTimersByTime(60_000);
    expect(getUserById(pondId, expired.id)).toBeUndefined();
    expect(loadPlayerPondSession(expired.playerId!, pondId)).toBeUndefined();
    expect(getFishingAttentionAt(expired)).toBeNull();
  });

  it('only accepts payload-free attention for the authenticated socket session and rate limits it', () => {
    const user = createFisher();
    const other = createFisher();
    waitForBite(user);
    waitForBite(other);
    tickAt(startAt + timeoutMs);
    const handlers = new Map<string, (...args: any[]) => void>();
    const socket = {
      id: user.id, data: {},
      on: (name: string, handler: (...args: any[]) => void) => handlers.set(name, handler),
    } as unknown as Socket<ClientToServerEvents, ServerToClientEvents>;
    const resolvePlayer = vi.fn(() => user.playerId!);
    registerSocketPondHandlers(socket, { io, resolveSocketPlayerId: resolvePlayer, roomFanoutCount: () => 2 });
    const attention = handlers.get('fishing_attention')!;
    attention({ userId: other.id, fishingAsleep: false, fishingSleepAt: Date.now() + 999999 });
    expect(user.fishingAsleep).toBe(true);
    expect(other.fishingAsleep).toBe(true);
    resolvePlayer.mockReturnValue(other.playerId!);
    const rejected = vi.fn();
    attention(rejected);
    expect(rejected).toHaveBeenCalledWith({ ok: false, error: 'unauthorized' });
    resolvePlayer.mockReturnValue(user.playerId!);
    attention();
    expect(user.fishingAsleep).toBe(false);
    expect(other.fishingAsleep).toBe(true);
    const ack = vi.fn();
    for (let attempt = 0; attempt < 25; attempt++) attention(ack);
    expect(ack).toHaveBeenLastCalledWith({ ok: false, error: 'rate_limited' });
    clearSession(user.id);
    vi.setSystemTime(Date.now() + 2000);
    attention(rejected);
    expect(rejected).toHaveBeenLastCalledWith({ ok: false, error: 'unauthorized' });
  });
});

describe('sleep configuration and migration', () => {
  it('installs sleep columns through normal database initialization', () => {
    const columns = db.prepare('PRAGMA table_info(player_pond_session)').all() as Array<{ name: string }>;
    expect(columns.map((column) => column.name)).toEqual(expect.arrayContaining([
      'fishing_asleep', 'fishing_sleep_at', 'fishing_attention_at', 'disconnected_from_phase', 'stop_requested',
    ]));
  });
  it('validates the admin config and applies approved changes', () => {
    expect(getConfigDefault('FISHING_SLEEP_AFTER_MS')).toBe('1800000');
    for (const value of ['', '-1', '1', '59999', '86400001', '60000.5', 'NaN', 'Infinity']) {
      expect(submitConfigChange('FISHING_SLEEP_AFTER_MS', value, 'operator').ok).toBe(false);
    }
    for (const value of [0, 60_000, 86_400_000]) {
      const result = submitConfigChange('FISHING_SLEEP_AFTER_MS', String(value), 'operator');
      expect(result.ok).toBe(true);
      if (!result.ok) throw new Error(result.error);
      expect(approveConfigChange(result.request.id, 'reviewer').ok).toBe(true);
      expect(getFishingSleepAfterMs()).toBe(value);
    }
    configure('Infinity');
    expect(getFishingSleepAfterMs()).toBe(timeoutMs);
    configure(999999999);
    expect(getFishingSleepAfterMs()).toBe(86_400_000);
  });

  it('migrates existing checkpoints idempotently without deleting rows', () => {
    const legacy = new Database(':memory:');
    legacy.exec("CREATE TABLE player_pond_session (player_id TEXT); INSERT INTO player_pond_session VALUES ('legacy')");
    migrateFishingSleep(legacy);
    migrateFishingSleep(legacy);
    expect(legacy.prepare('SELECT * FROM player_pond_session').get()).toEqual({ player_id: 'legacy',
      fishing_asleep: 0, fishing_sleep_at: null, fishing_attention_at: null, disconnected_from_phase: null,
      stop_requested: 0 });
    legacy.close();
  });
});
