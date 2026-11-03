import { randomUUID } from 'node:crypto';
import { afterAll, afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Server } from 'socket.io';
import type { ClientToServerEvents, PondUser, ServerToClientEvents } from '@fish-social/shared';

const previousEnv = vi.hoisted(() => {
  const old = { db: process.env.DB_PATH, instant: process.env.FISHING_TEST_MODE };
  process.env.DB_PATH = ':memory:';
  process.env.FISHING_TEST_MODE = 'instant';
  return old;
});

import { db } from '../db.js';
import { createHumanPondUser, removeDisconnectedUser, clearSession } from '../gameState.js';
import { ensurePlayer, getPlayer } from '../players.js';
import { completeOnboarding, ensurePlayerProgress } from '../playerProgress.js';
import { ensurePlayerGear } from '../gear.js';
import * as gear from '../gear.js';
import { getPondFishById, spawnDebugHookFish } from '../pondEcology.js';
import { rollBiteHook } from '../fishingSession.js';
import { getHookContext, flushHookedPhaseToPendingCatch, processWaitingBiteTick } from '../fishingStateMachine.js';
import { applyCheckpointToUser, loadPlayerPondSession, upsertPlayerPondSession } from '../playerPondSession.js';
import { getPendingCatch, clearPendingCatch } from '../inventory.js';
import { readPersistedFishHookContext } from '../fishHookContext.js';
import { tryStartGroundbaitCast, applyGroundbaitCastComplete } from '../groundbait.js';
import { cancelAll } from '../timerRegistry.js';

const pondId = 'pond-calm';
const spotId = 'calm-spot-1';
const users: PondUser[] = [];
const io = { to: () => ({ emit: () => undefined }) } as unknown as Server<ClientToServerEvents,ServerToClientEvents>;
const now = Date.UTC(2026,10,4,1);

function fisher(): PondUser {
  const playerId = randomUUID();
  ensurePlayer(playerId,'Recovery test');
  completeOnboarding(playerId);
  ensurePlayerGear(playerId);
  db.prepare('UPDATE players SET coins=1000 WHERE player_id=?').run(playerId);
  const user = createHumanPondUser(pondId,playerId,'Recovery test','sell_only');
  user.spotId=spotId;user.fishingPhase='waiting';
  users.push(user);
  upsertPlayerPondSession(user,pondId);
  return user;
}

beforeEach(() => { vi.useFakeTimers();vi.setSystemTime(now); });
afterEach(() => {
  for(const user of users.splice(0)) { clearPendingCatch(user.id);removeDisconnectedUser(pondId,user.id);clearSession(user.id); }
  cancelAll();
  db.exec('DELETE FROM fish_lifecycle_events; DELETE FROM fish_hook_locks; DELETE FROM groundbait_casts; DELETE FROM pond_fish;');
  vi.restoreAllMocks();vi.useRealTimers();
});
afterAll(() => {
  if(previousEnv.db===undefined)delete process.env.DB_PATH;else process.env.DB_PATH=previousEnv.db;
  if(previousEnv.instant===undefined)delete process.env.FISHING_TEST_MODE;else process.env.FISHING_TEST_MODE=previousEnv.instant;
});

describe('durable hook context and atomic bait nutrition', () => {
  it.each([false,true])('recovers persisted outcome=%s without an in-memory hook or a second food award', escaped => {
    const user=fisher();
    const fish=spawnDebugHookFish(pondId,spotId,'gray',.4)!;
    const rolled=rollBiteHook(pondId,spotId,new Set(),{equippedBait:'bait-basic',equippedTackle:'basic',hookUserId:user.id,forceEscape:escaped});
    expect(rolled.outcome).toBe('hooked');if(rolled.outcome!=='hooked')return;
    const saved=readPersistedFishHookContext(user.id)!;
    expect(saved).toMatchObject({catchId:rolled.event.catchId,escaped,fish:{id:fish.id}});
    // No scheduleHookFromBite occurred: this models a process stopping between committed bite and phase update.
    expect(loadPlayerPondSession(user.playerId!,pondId)!.fishing_phase).toBe('waiting');
    applyCheckpointToUser(user,loadPlayerPondSession(user.playerId!,pondId)!);
    expect(user.fishingPhase).toBe('hooked');expect(user.phaseEndsAt).toBe(saved.hookEndsAt);
    expect(getHookContext(user.id)?.catchId).toBe(rolled.event.catchId);
    expect(flushHookedPhaseToPendingCatch(io,pondId,user.id,null,{deferAutoSettle:true})).toBe(true);
    expect(db.prepare("SELECT COUNT(*) n FROM fish_lifecycle_events WHERE kind='hook_feed'").get()).toEqual({n:1});
    if(escaped) {
      expect(getPendingCatch(user.id)).toBeUndefined();
      expect(db.prepare("SELECT COUNT(*) n,SUM(granted_bonus_minutes) minutes FROM fish_lifecycle_events WHERE kind='escape'").get()).toEqual({n:1,minutes:0});
      expect(getPondFishById(fish.id)!.growthBonusMinutes).toBe(saved.fish.growthBonusMinutes);
      expect(getPondFishById(fish.id)!.sizeM).toBe(saved.fish.sizeM);
    } else {
      expect(getPendingCatch(user.id)?.catchId).toBe(rolled.event.catchId);
      expect(getPendingCatch(user.id)?.history?.fishId).toBe(fish.id);
    }
  });

  it('rolls back the hook lock, context and food when the bait payment fails', () => {
    const user=fisher();const fish=spawnDebugHookFish(pondId,spotId,'gray',.4)!;
    vi.spyOn(gear,'resolveBaitForBite').mockReturnValue({baitId:'bait-basic',cost:10});
    vi.spyOn(gear,'chargeBaitUse').mockReturnValue({charged:false,coins:1000});
    const before=getPondFishById(fish.id)!;
    expect(processWaitingBiteTick(io,pondId,user.id,user.playerId!,spotId,null)).toBe(false);
    expect(db.prepare('SELECT COUNT(*) n FROM fish_hook_locks').get()).toEqual({n:0});
    expect(db.prepare('SELECT COUNT(*) n FROM fish_lifecycle_events').get()).toEqual({n:0});
    expect(getPondFishById(fish.id)!.growthBonusMinutes).toBe(before.growthBonusMinutes);
    expect(getPlayer(user.playerId!)!.coins).toBe(1000);
    expect(user.fishingPhase).toBe('waiting');
  });

  it('commits one payment and one food award across bite and scheduling', () => {
    const user=fisher();spawnDebugHookFish(pondId,spotId,'gray',.4);
    vi.spyOn(gear,'resolveBaitForBite').mockReturnValue({baitId:'bait-basic',cost:10});
    expect(processWaitingBiteTick(io,pondId,user.id,user.playerId!,spotId,null)).toBe(true);
    expect(getPlayer(user.playerId!)!.coins).toBe(990);
    expect(db.prepare("SELECT COUNT(*) n,SUM(granted_bonus_minutes) minutes FROM fish_lifecycle_events WHERE kind='hook_feed'").get()).toEqual({n:1,minutes:15});
    expect(readPersistedFishHookContext(user.id)?.catchId).toBe(getHookContext(user.id)?.catchId);
  });
});

describe('expired casting budget', () => {
  it('cannot revive an expired paid cast from stale in-memory pending IDs', () => {
    const user=fisher();user.fishingPhase='seated';ensurePlayerProgress(user.playerId!);
    db.prepare('UPDATE player_fishing_progress SET level=10 WHERE player_id=?').run(user.playerId!);
    expect(tryStartGroundbaitCast(user.playerId!,user,'gb-basic',pondId).ok).toBe(true);
    const cast=db.prepare('SELECT cast_id,expires_at FROM groundbait_casts').get() as {cast_id:string;expires_at:number};
    const coins=getPlayer(user.playerId!)!.coins;
    vi.setSystemTime(cast.expires_at+1);
    expect(applyGroundbaitCastComplete(user.playerId!,user,pondId)).toBeNull();
    expect(db.prepare('SELECT status,portions_consumed,expires_at FROM groundbait_casts WHERE cast_id=?').get(cast.cast_id))
      .toEqual({status:'expired',portions_consumed:0,expires_at:cast.expires_at});
    expect(getPlayer(user.playerId!)!.coins).toBe(coins);
    expect(db.prepare('SELECT COUNT(*) n FROM fish_lifecycle_events').get()).toEqual({n:0});
  });
});
