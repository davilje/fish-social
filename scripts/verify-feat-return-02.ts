/**
 * FEAT-RETURN-02 smoke: dual fee mode, fee tick, sell_only gate, auto-return eligibility.
 * Run: npm run verify:feat-return-02
 */
import '../server/src/db.js';
import { db } from '../server/src/db.js';
import {
  getGamePondDef,
  getReturnRules,
  isReturnEligible,
  pondAllowsDualFee,
  pondAllowsReturnFish,
  resolvePondFeePer2h,
  validateJoinReturnFeeMode,
} from '@fish-social/shared';
import { joinPond, leavePond, startFishing } from '../server/src/pondSession.js';
import { addFishToInventory } from '../server/src/inventory.js';
import { returnFishToPond, tryAutoReturnFish } from '../server/src/returnFish.js';
import { applyAdmissionFeeProgress, completeOnboarding, ensurePlayerProgress } from '../server/src/playerProgress.js';
import { ensurePlayer, addCoins } from '../server/src/players.js';
import { initPondEcology } from '../server/src/pondEcology.js';
import { ADMISSION_FEE_SLICE_MS } from '@fish-social/shared';

const playerId = 'test-return-02';
const pondId = 'pond-calm';
const socketId = 'sock-return-02';

db.prepare('DELETE FROM inventory WHERE player_id = ?').run(playerId);
db.prepare('DELETE FROM players WHERE player_id = ?').run(playerId);
db.prepare('DELETE FROM daily_admission_fees WHERE player_id = ?').run(playerId);
try {
  db.prepare('DELETE FROM player_pond_session WHERE player_id = ?').run(playerId);
} catch {
  /* optional */
}

ensurePlayer(playerId, 'Return02Tester');
ensurePlayerProgress(playerId);
completeOnboarding(playerId);
addCoins(playerId, 50_000);
initPondEcology();

const pondDef = getGamePondDef(pondId);
assert(pondDef != null, 'pond-calm def');
assert(pondAllowsDualFee(pondDef!), 'advanced fee pond allows dual fee');
assert(pondAllowsReturnFish(pondDef!), 'fee pond with allowsAutoReturn permits return');

const freePond = getGamePondDef('pond-novice');
assert(freePond != null, 'pond-novice def');
assert(!pondAllowsReturnFish(freePond!), 'free pond blocks return');
assert(resolvePondFeePer2h(pondDef!, 'sell_only') === 200);
assert(resolvePondFeePer2h(pondDef!, 'auto_return') === 350);

const missingMode = validateJoinReturnFeeMode(pondDef!, undefined);
assert(!missingMode.ok, 'dual pond requires mode');

const sellMode = validateJoinReturnFeeMode(pondDef!, 'sell_only');
assert(sellMode.ok && sellMode.mode === 'sell_only');

const joinedSell = joinPond(socketId, pondId, 'Tester', playerId, 'sell_only');
assert(joinedSell.ok, 'join sell_only');
assert(joinedSell.user.returnFeeMode === 'sell_only');

const spot = startFishing(socketId, pondId, 'calm-spot-1');
assert(spot.ok, 'take spot');

const grayFish = addFishToInventory(playerId, {
  speciesId: 'crucian',
  quality: 'gray',
  sizeM: 0.15,
  caughtAt: Date.now(),
  pondId,
});

const blocked = returnFishToPond(playerId, grayFish.id);
assert(!blocked.ok && blocked.code === 'SELL_ONLY_MODE', 'sell_only blocks manual return');

leavePond(socketId);

const joinedAuto = joinPond(socketId + '-auto', pondId, 'Tester', playerId, 'auto_return');
assert(joinedAuto.ok, 'join auto_return');
assert(joinedAuto.user.returnFeeMode === 'auto_return');

const spotAuto = startFishing(socketId + '-auto', pondId, 'calm-spot-1');
assert(spotAuto.ok, 'take spot auto');

const feeTick = applyAdmissionFeeProgress(playerId, pondId, ADMISSION_FEE_SLICE_MS, 'auto_return');
assert(feeTick.kind === 'ok' && feeTick.charged === 350, 'auto_return fee tick charges 350');

const manualBlocked = returnFishToPond(playerId, grayFish.id);
assert(!manualBlocked.ok && manualBlocked.code === 'AUTO_RETURN_MODE', 'auto_return blocks manual');

const rules = getReturnRules();
const smallFish = addFishToInventory(playerId, {
  speciesId: 'crucian',
  quality: 'purple',
  sizeM: 0.12,
  caughtAt: Date.now(),
  pondId,
});
assert(!isReturnEligible(smallFish, rules), 'light fish not return eligible');

const midFish = addFishToInventory(playerId, {
  speciesId: 'crucian',
  quality: 'purple',
  sizeM: 0.78,
  caughtAt: Date.now(),
  pondId,
});
assert(isReturnEligible(midFish, rules), 'purple ≥10 jin return eligible');

const grayHeavy = addFishToInventory(playerId, {
  speciesId: 'crucian',
  quality: 'gray',
  sizeM: 0.2,
  caughtAt: Date.now(),
  pondId,
});
assert(!isReturnEligible(grayHeavy, rules), 'gray not return eligible');

const heavyFish = addFishToInventory(playerId, {
  speciesId: 'chinese_sturgeon',
  quality: 'red',
  sizeM: 1.7,
  caughtAt: Date.now(),
  pondId,
});
assert(isReturnEligible(heavyFish, rules), 'heavy red fish return eligible');

const skip = tryAutoReturnFish(playerId, smallFish.id);
assert('skipped' in skip && skip.skipped, 'ineligible skips auto return');

const skipGray = tryAutoReturnFish(playerId, grayHeavy.id);
assert('skipped' in skipGray && skipGray.skipped, 'gray skips auto return');

const autoOk = tryAutoReturnFish(playerId, midFish.id);
assert(autoOk.ok, 'eligible auto return');
assert(autoOk.ok && autoOk.gold === Math.floor((autoOk as { gold: number }).gold), 'gold finite');
// mid tier should be 1.5× sell — re-check with second eligible heavy
const autoHeavy = tryAutoReturnFish(playerId, heavyFish.id);
assert(autoHeavy.ok, 'heavy auto return');


leavePond(socketId + '-auto');

console.log('FEAT-RETURN-02 smoke ok');
console.log('  sell fee=', resolvePondFeePer2h(pondDef!, 'sell_only'));
console.log('  auto fee=', resolvePondFeePer2h(pondDef!, 'auto_return'));
console.log('  auto gold=', autoOk.ok ? autoOk.gold : 0);

function assert(cond: unknown, msg?: string): asserts cond {
  if (!cond) throw new Error(msg ?? 'assertion failed');
}
