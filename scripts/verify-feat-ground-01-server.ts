/**
 * FEAT-GROUND-01 server smoke: cast start deducts gold; complete stacks; cap / gold reject.
 * Run: npm run verify:feat-ground-01-server
 */
import '../server/src/db.js';
import { db } from '../server/src/db.js';
import { ensurePlayer, addCoins, getPlayer } from '../server/src/players.js';
import { ensurePlayerProgress } from '../server/src/playerProgress.js';
import {
  tryStartGroundbaitCast,
  applyGroundbaitCastComplete,
  clearGroundbait,
} from '../server/src/groundbait.js';
import type { PondUser } from '@fish-social/shared';
import { calcGroundbaitBiteBonus, getGroundbaitDef } from '@fish-social/shared';

const playerId = 'test-ground-01';
db.prepare('DELETE FROM players WHERE player_id = ?').run(playerId);
db.prepare('DELETE FROM player_fishing_progress WHERE player_id = ?').run(playerId);

ensurePlayer(playerId, 'GroundTester');
ensurePlayerProgress(playerId);
db.prepare('UPDATE player_fishing_progress SET level = 10 WHERE player_id = ?').run(playerId);
assert(ensurePlayerProgress(playerId).level === 10, 'level bump failed');

addCoins(playerId, 500);

const user = {
  id: 'u-gb-1',
  playerId,
  nickname: 'GroundTester',
  color: '#fff',
  spotId: 'calm-spot-1',
  status: 'idle',
  fishingStartedAt: null,
  todayFishingMs: 0,
  fishingPhase: 'seated',
  groundbait: null,
} as PondUser;

db.prepare('UPDATE players SET coins = 0 WHERE player_id = ?').run(playerId);
const rejectGold = tryStartGroundbaitCast(playerId, { ...user }, 'gb-basic');
assert(
  !rejectGold.ok && rejectGold.code === 'INSUFFICIENT_GOLD',
  `expected INSUFFICIENT_GOLD, got ${JSON.stringify(rejectGold)}`,
);

addCoins(playerId, 500);
const started = tryStartGroundbaitCast(playerId, user, 'gb-basic');
assert(started.ok, started.ok ? '' : started.error);
assert(started.ok && started.costGold === 30);
const coinsAfter = getPlayer(playerId)?.coins ?? -1;
assert(coinsAfter === 470, `expected 470 coins, got ${coinsAfter}`);

applyGroundbaitCastComplete(playerId, user, 'pond-calm');
assert(user.groundbait != null, 'buff missing');
assert(user.groundbait!.stackCount === 1);
assert(user.groundbait!.groundbaitId === 'gb-basic');

const def = getGroundbaitDef('gb-basic')!;
const expected = calcGroundbaitBiteBonus(1, def.maxBonus, def.stackK);
assert(Math.abs(user.groundbait!.biteBonus - expected) < 1e-9);

const started2 = tryStartGroundbaitCast(playerId, user, 'gb-basic');
assert(started2.ok);
applyGroundbaitCastComplete(playerId, user, 'pond-calm');
assert(user.groundbait!.stackCount === 2);

const started3 = tryStartGroundbaitCast(playerId, user, 'gb-mix');
assert(started3.ok);
applyGroundbaitCastComplete(playerId, user, 'pond-calm');
assert(user.groundbait!.stackCount === 1);
assert(user.groundbait!.groundbaitId === 'gb-mix');

user.groundbait = {
  groundbaitId: 'gb-mix',
  stackCount: 50,
  expiresAt: Date.now() + 60_000,
  bitesLeft: 5,
  biteBonus: 0.1,
  sizeBonus: 0.05,
};
const capped = tryStartGroundbaitCast(playerId, user, 'gb-mix');
assert(!capped.ok && capped.code === 'STACK_CAP', 'expected STACK_CAP');

clearGroundbait(user);
assert(user.groundbait == null);

console.log('FEAT-GROUND-01 server smoke ok');

function assert(cond: unknown, msg?: string): asserts cond {
  if (!cond) throw new Error(msg ?? 'assertion failed');
}
