import { randomUUID } from 'node:crypto';
import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import {
  PONDS, calcSupplementIdealCounts, getGamePondDef, getPondStockConfig,
  isFishEntryGrowthState, type FishQuality,
} from '@fish-social/shared';

vi.hoisted(() => { process.env.DB_PATH = ':memory:'; });
import { db } from '../db.js';
import {
  initPondEcology, resetAllEcology, listPondFishEntities, getPondFishById,
  spawnDebugHookFish, tickPondEcology, ensurePondEcologyCurrent, growOrSpawnReturnedFish,
} from '../pondEcology.js';
import {
  claimFishHook, recordFishEscape, buildFishHistorySnapshot, createGroundbaitBudget,
  feedGroundbaitAtSpot, getHookLockedFishIds,
} from '../fishLifecycle.js';
import { lockPendingCatch, acceptCatch, getInventory, getPendingCatch, clearPendingCatch } from '../inventory.js';
import { ensurePlayer } from '../players.js';
import { ensurePlayerGear } from '../gear.js';
import { cancelAll } from '../timerRegistry.js';
import { listAlbumCandidates, listAlbumPins, addAlbumCandidate } from '../album.js';

const pondId = 'pond-calm';
const at = Date.parse('2026-11-04T06:00:00Z');
const spot = PONDS.find(p => p.id === pondId)!.spots[0]!.id;
const otherSpot = PONDS.find(p => p.id === pondId)!.spots[1]!.id;
const hour = 3_600_000;
const count = (id = pondId) => (db.prepare('SELECT COUNT(*) n FROM pond_fish WHERE pond_id=?').get(id) as {n:number}).n;
const pendingUsers: string[] = [];

function youngFish(quality: FishQuality = 'gray', spotId = spot) {
  const fish = spawnDebugHookFish(pondId, spotId, quality, 0)!;
  expect(fish).toBeTruthy();
  return fish;
}

function pendingFish() {
  const playerId = randomUUID();
  ensurePlayer(playerId, 'Ecology test');
  ensurePlayerGear(playerId);
  const fish = youngFish('red');
  const catchId = randomUUID();
  const fed = claimFishHook(fish.id, catchId, playerId, at)!;
  const history = buildFishHistorySnapshot(fed, at)!;
  const pending = lockPendingCatch(playerId, {
    catchId, pondFishId: fish.id, speciesId: fish.speciesId, quality: fish.quality,
    sizeM: fed.sizeM, hookDurationMs: 2000, history,
  }, { playerId, pondId });
  expect(pending).toBeTruthy();
  pendingUsers.push(playerId);
  return { playerId, fish: fed, catchId, history };
}

beforeEach(() => {
  vi.useFakeTimers(); vi.setSystemTime(at);
  db.exec('DELETE FROM fish_lifecycle_events; DELETE FROM groundbait_casts; DELETE FROM fish_hook_locks; DELETE FROM fish_entity_archive; DELETE FROM pending_catch_locks;');
  resetAllEcology();
});
afterEach(() => {
  for (const userId of pendingUsers.splice(0)) clearPendingCatch(userId);
  cancelAll(); vi.useRealTimers();
});

describe('FISH-ECOLOGY-01 online integration', () => {
  it('seeds historical entry batches once, including after a pond is completely caught out', () => {
    const formal = PONDS.filter(p => getGamePondDef(p.id)?.pondCategory !== 'novice');
    expect(formal).toHaveLength(40);
    expect(formal.reduce((n,p)=>n+count(p.id),0)).toBe(4000);
    const fish = listPondFishEntities(pondId, at);
    expect(new Set(fish.map(f=>f.enteredAt)).size).toBeGreaterThan(90);
    for (const f of fish) {
      expect(isFishEntryGrowthState(f)).toBe(true);
      expect(f.enteredAt).toBeGreaterThanOrEqual(at-14*86_400_000);
      expect(f.enteredAt).toBeLessThanOrEqual(at);
      expect(f.sizeM).toBeLessThanOrEqual(f.individualCapM!);
      expect(f.bornAt).toBe(0);
    }
    const ids = fish.map(f=>f.id);
    initPondEcology(); expect(listPondFishEntities(pondId).map(f=>f.id)).toEqual(ids);
    db.prepare('DELETE FROM pond_fish WHERE pond_id=?').run(pondId);
    initPondEcology(); expect(count()).toBe(0);
  });

  it('keeps the novice tutorial independent of formal-pond replenishment', () => {
    expect(count('pond-novice')).toBe(1);
    db.prepare('DELETE FROM pond_fish WHERE pond_id=?').run('pond-novice');
    tickPondEcology('pond-novice', at+hour);
    tickPondEcology('pond-novice', at+86_400_000);
    expect(count('pond-novice')).toBe(0); // Its per-player tutorial award is handled separately.
    initPondEcology();
    expect(count('pond-novice')).toBe(0);
  });

  it('calculates growth at read time without writing lengths on each 30-second tick', () => {
    const fish = youngFish();
    const before = db.prepare('SELECT size_m FROM pond_fish WHERE id=?').get(fish.id) as {size_m:number};
    expect(getPondFishById(fish.id,at+6*hour)!.sizeM).toBeGreaterThan(fish.sizeM);
    tickPondEcology(pondId,at+30_000);
    expect((db.prepare('SELECT size_m FROM pond_fish WHERE id=?').get(fish.id) as {size_m:number}).size_m).toBe(before.size_m);
    expect(getPondFishById(fish.id)!.enteredAt).toBe(fish.enteredAt);
  });

  it('leaves 41 fish alone, fills 40 to 80, and gives the 04:00 daily target priority', () => {
    const ids = listPondFishEntities(pondId).map(f=>f.id);
    for (const id of ids.slice(41)) db.prepare('DELETE FROM pond_fish WHERE id=?').run(id);
    tickPondEcology(pondId,at+hour); expect(count()).toBe(41);
    db.prepare('DELETE FROM pond_fish WHERE id=?').run(ids[0]);
    tickPondEcology(pondId,at+2*hour); expect(count()).toBe(80);
    const fresh = listPondFishEntities(pondId,at+2*hour).filter(f=>f.source==='supplement');
    expect(fresh).toHaveLength(40);
    expect(fresh.every(f=>f.enteredAt===at+2*hour)).toBe(true);
    const nextFour = Date.parse('2026-11-04T20:00:00Z');
    tickPondEcology(pondId,nextFour); expect(count()).toBe(100);
    const after = listPondFishEntities(pondId,nextFour).map(f=>f.id);
    tickPondEcology(pondId,nextFour);
    expect(listPondFishEntities(pondId,nextFour).map(f=>f.id)).toEqual(after);
    // Force exactly the low-water boundary at the next coincident daily/hourly tick.
    for (const id of after.slice(40)) db.prepare('DELETE FROM pond_fish WHERE id=?').run(id);
    tickPondEcology(pondId,nextFour+86_400_000); expect(count()).toBe(100);
  });

  it('enforces high-quality ceilings throughout a real supplement batch and never migrates dormant fish', () => {
    const pond = PONDS.find(p=>getGamePondDef(p.id)?.pondCategory==='advanced')!;
    db.prepare('DELETE FROM pond_fish WHERE pond_id=?').run(pond.id);
    tickPondEcology(pond.id,at+hour); expect(count(pond.id)).toBe(80);
    const limits = calcSupplementIdealCounts(getPondStockConfig(pond.id)!.maxPopulation,'advanced');
    for (const q of ['purple','red','orange','gold'] as FishQuality[]) {
      expect(listPondFishEntities(pond.id).filter(f=>f.quality===q).length).toBeLessThanOrEqual(Math.ceil(limits[q]));
    }
    const positions = new Map(listPondFishEntities(pond.id).map(f=>[f.id,f.spotId]));
    const wake = ensurePondEcologyCurrent(pond.id,at+7*86_400_000)!;
    expect(wake.replaySteps).toBe(1); expect(wake.migrated).toBe(0); expect(count(pond.id)).toBe(100);
    for (const fish of listPondFishEntities(pond.id)) if(positions.has(fish.id)) expect(fish.spotId).toBe(positions.get(fish.id));
    expect(ensurePondEcologyCurrent(pond.id,at+7*86_400_000)!.replaySteps).toBe(0);
  });

  it('grants a hook meal once, rejects another fisher, records E0 escapes once, and retains identity', () => {
    const fish = youngFish(); const attempt='first-hook';
    const fed = claimFishHook(fish.id,attempt,'alice',at)!;
    expect(fed.growthBonusMinutes).toBe(15);
    expect(claimFishHook(fish.id,attempt,'alice',at)!.growthBonusMinutes).toBe(15);
    expect(claimFishHook(fish.id,'second-hook','bob',at)).toBeNull();
    const escaped=recordFishEscape(fish.id,attempt,at,spot)!;
    recordFishEscape(fish.id,attempt,at,spot);
    expect(escaped.sizeM).toBe(fed.sizeM);
    expect(escaped.enteredAt).toBe(fish.enteredAt);
    expect(getHookLockedFishIds(at).has(fish.id)).toBe(false);
    const caught = claimFishHook(fish.id,'third-hook','bob',at)!;
    const history=buildFishHistorySnapshot(caught,at)!;
    expect(history.fishId).toBe(fish.id);
    expect(history.hookFeedCount).toBe(2); expect(history.escapeCount).toBe(1);
    expect(history.escapes[0]).toMatchObject({at,pondId,spotId:spot,sizeM:fed.sizeM});
  });

  it('shares a rolling 360-minute nutrition limit and does not reset it at 04:00', () => {
    const fish=youngFish();
    const beforeFour=Date.parse('2026-11-04T19:59:00Z');
    for(let i=0;i<30;i++) {
      claimFishHook(fish.id,`meal-${i}`,'a',beforeFour);
      recordFishEscape(fish.id,`meal-${i}`,beforeFour);
    }
    expect(getPondFishById(fish.id,beforeFour)!.growthBonusMinutes).toBe(360);
    const afterFour=beforeFour+120_000;
    expect(claimFishHook(fish.id,'after-four','a',afterFour)!.growthBonusMinutes).toBe(360);
    recordFishEscape(fish.id,'after-four',afterFour);
    expect(claimFishHook(fish.id,'next-window','a',beforeFour+86_400_000)!.growthBonusMinutes).toBe(375);
    expect(getPondFishById(fish.id)!.enteredAt).toBe(fish.enteredAt);
  });

  it('spends a local 12-portion budget only on actual unlocked feeders, at most two per fish per cast', () => {
    db.prepare('DELETE FROM pond_fish WHERE pond_id=?').run(pondId);
    const local=Array.from({length:7},()=>youngFish());
    const away=youngFish('gray',otherSpot);
    claimFishHook(local[0]!.id,'locked','angler',at,at+hour);
    createGroundbaitBudget({castId:'ground-1',playerId:'feeder',pondId,spotId:spot,groundbaitId:'gb-basic',createdAt:at,expiresAt:at+hour});
    for(let i=0;i<20;i++) {
      feedGroundbaitAtSpot(pondId,spot,at+i*30_000,()=>0);
      feedGroundbaitAtSpot(pondId,spot,at+i*30_000,()=>0); // retry cannot consume twice
    }
    const rows=db.prepare("SELECT fish_id,SUM(feed_portions) n,SUM(granted_bonus_minutes) m FROM fish_lifecycle_events WHERE kind='groundbait_feed' GROUP BY fish_id").all() as {fish_id:string;n:number;m:number}[];
    expect(rows.reduce((n,r)=>n+r.n,0)).toBe(12);
    expect(rows.reduce((n,r)=>n+r.m,0)).toBe(120);
    expect(rows.every(r=>r.n<=2&&r.fish_id!==away.id&&r.fish_id!==local[0]!.id)).toBe(true);
    const left=youngFish('gray',otherSpot);
    createGroundbaitBudget({castId:'expires',playerId:'feeder',pondId,spotId:otherSpot,groundbaitId:'gb-basic',createdAt:at,expiresAt:at+30_000});
    feedGroundbaitAtSpot(pondId,otherSpot,at,()=>0);
    feedGroundbaitAtSpot(pondId,otherSpot,at+30_000,()=>0);
    const cast=db.prepare('SELECT portions_consumed,status FROM groundbait_casts WHERE cast_id=?').get('expires') as {portions_consumed:number;status:string};
    expect(cast).toEqual({portions_consumed:1,status:'expired'});
    expect(buildFishHistorySnapshot(getPondFishById(left.id)!,at)!.groundbaitFeedCount).toBeLessThanOrEqual(1);
  });

  it('commits capture and ownership atomically, freezes history, preserves it in album and restores the same fish on return', () => {
    const {playerId,fish,catchId,history}=pendingFish();
    db.exec("CREATE TEMP TRIGGER refuse_inventory BEFORE INSERT ON inventory BEGIN SELECT RAISE(ABORT,'injected inventory failure'); END");
    expect(()=>acceptCatch(playerId,playerId,catchId,pondId)).toThrow('injected inventory failure');
    expect(getPondFishById(fish.id)).not.toBeNull();
    expect(getPendingCatch(playerId)?.catchId).toBe(catchId);
    expect(getInventory(playerId)).toHaveLength(0);
    db.exec('DROP TRIGGER refuse_inventory');
    const result=acceptCatch(playerId,playerId,catchId,pondId);
    expect(result.ok).toBe(true); if(!result.ok)throw Error(result.error);
    expect(getPondFishById(fish.id)).toBeNull();
    expect(result.item.history).toEqual(history);
    vi.setSystemTime(at+2*86_400_000);
    expect(getInventory(playerId)[0]!.history).toEqual(history);
    addAlbumCandidate({playerId,speciesId:fish.speciesId,quality:fish.quality,sizeM:fish.sizeM,
      pondId,source:'catch',inventoryItemId:result.item.id,history});
    expect(listAlbumCandidates(playerId)[0]!.history).toEqual(history);
    expect(listAlbumPins(playerId)[0]!.history).toEqual(history);
    const returned=growOrSpawnReturnedFish({pondId,spotId:spot,speciesId:fish.speciesId,quality:fish.quality,
      baseSizeM:fish.sizeM,sizeGainM:0,fishId:fish.id,caughtAt:history.caughtAt,returnEventId:`return:${result.item.id}`});
    expect(returned.entity.id).toBe(fish.id);
    expect(returned.entity.enteredAt).toBe(fish.enteredAt);
    expect(returned.entity.sizeM).toBe(fish.sizeM);
    expect(returned.entity.outOfPondMs).toBe(2*86_400_000);
    expect(buildFishHistorySnapshot(returned.entity)!.residenceMs).toBe(history.residenceMs);
    expect(()=>growOrSpawnReturnedFish({pondId,spotId:spot,speciesId:fish.speciesId,quality:fish.quality,
      baseSizeM:fish.sizeM,sizeGainM:0,fishId:fish.id})).toThrow('already present');
  });
});
