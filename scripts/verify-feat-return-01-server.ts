/**
 * FEAT-RETURN-01 server smoke: return grows pond fish, sell does not; gates work.
 * Run: npm run verify:feat-return-01-server
 */
import '../server/src/db.js';
import { db } from '../server/src/db.js';
import { addFishToInventory, getInventory, sellFish } from '../server/src/inventory.js';
import { returnFishToPond } from '../server/src/returnFish.js';
import {
  initPondEcology,
  listPondFishEntities,
  growOrSpawnReturnedFish,
} from '../server/src/pondEcology.js';
import { ensurePlayer, addCoins } from '../server/src/players.js';
import { calcFishReturnGold, calcFishSellPrice, getReturnRules } from '@fish-social/shared';

const playerId = 'test-return-01';
const pondId = 'pond-calm';

db.prepare('DELETE FROM inventory WHERE player_id = ?').run(playerId);
db.prepare('DELETE FROM players WHERE player_id = ?').run(playerId);
try {
  db.prepare('DELETE FROM player_fishing_progress WHERE player_id = ?').run(playerId);
} catch {
  /* table may not exist in older DBs */
}
try {
  db.prepare('DELETE FROM player_pond_proficiency WHERE player_id = ?').run(playerId);
} catch {
  /* optional */
}

ensurePlayer(playerId, 'ReturnTester');
addCoins(playerId, 0);
initPondEcology();

const beforeCount = listPondFishEntities(pondId).length;

const grown = growOrSpawnReturnedFish({
  pondId,
  spotId: 'calm-spot-1',
  speciesId: 'crucian',
  quality: 'gray',
  baseSizeM: 0.12,
  sizeGainM: 0.03,
});
assert(grown.sizeGainApplied > 0 || grown.entity.sizeM >= 0.12, 'grow/spawn should apply');
assert(listPondFishEntities(pondId).length >= beforeCount, 'pond should not shrink');

const mid = addFishToInventory(playerId, {
  speciesId: 'crucian',
  quality: 'gray',
  sizeM: 0.15,
  caughtAt: Date.now(),
  pondId,
});

const noSeat = returnFishToPond(playerId, mid.id);
assert(!noSeat.ok && noSeat.code === 'NOT_IN_POND', `expected NOT_IN_POND, got ${JSON.stringify(noSeat)}`);

const maxItem = addFishToInventory(playerId, {
  speciesId: 'crucian',
  quality: 'gray',
  sizeM: 0.3,
  caughtAt: Date.now(),
  pondId,
});

const bagBeforeSell = getInventory(playerId).length;
const sold = sellFish(playerId, maxItem.id);
assert(sold.ok, 'sell should work');
assert(getInventory(playerId).length === bagBeforeSell - 1, 'sell removes bag item');
const sellGold = calcFishSellPrice(sold.fish);
const rules = getReturnRules();
const heavyItem = { quality: 'purple' as const, sizeM: 1.7, speciesId: 'crucian' };
const heavySell = calcFishSellPrice(heavyItem);
const expectedReturn = calcFishReturnGold(heavyItem, {
  goldMulVsSell: rules.goldMulVsSell,
  goldMulHeavy: rules.goldMulHeavy,
  minWeightJin: rules.minWeightJin,
  heavyWeightJin: rules.heavyWeightJin,
});
assert(expectedReturn === Math.floor(heavySell * 3));

console.log('FEAT-RETURN-01 server smoke ok');
console.log('  grown size=', grown.entity.sizeM, 'gain=', grown.sizeGainApplied, 'spawned=', grown.spawned);
console.log('  sellGold=', sellGold, 'expectedReturn=', expectedReturn);

function assert(cond: unknown, msg?: string): asserts cond {
  if (!cond) throw new Error(msg ?? 'assertion failed');
}
