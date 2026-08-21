/**
 * FEAT-GEAR-01: rod/bait/vessel table rules.
 * Run: npm run verify:feat-gear-01
 */
import assert from 'node:assert/strict';
import {
  BASIC_BAIT_ID,
  STARTER_ROD_ID,
  getRodDef,
  hasUsableRod,
  isOversizeForRod,
  listGameBaits,
  listRods,
  listVessels,
  pickBaitForDiet,
  rodBiteMultiplier,
  rodEscapeReductionValue,
  shouldDestroyRod,
  unlockedBaitsForPlayerLevel,
} from '@fish-social/shared';

function section(name: string) {
  console.log(`\n== ${name} ==`);
}

section('catalog');
assert.ok(listRods().length >= 8);
assert.ok(getRodDef(STARTER_ROD_ID));
assert.equal(getRodDef(STARTER_ROD_ID)?.priceGold, 0);
assert.equal(listGameBaits().length, 4);
assert.equal(listVessels().every((v) => v.enabledUse === false), true);
console.log('rods', listRods().length, 'baits 4, vessels unusable');

section('unlock baits');
assert.deepEqual(unlockedBaitsForPlayerLevel(1), [BASIC_BAIT_ID]);
assert.ok(unlockedBaitsForPlayerLevel(2).includes('bait-veg'));
assert.ok(unlockedBaitsForPlayerLevel(4).includes('bait-meat'));

section('auto bait');
const veg = pickBaitForDiet(unlockedBaitsForPlayerLevel(4), 'crucian', 100);
assert.equal(veg.baitId, 'bait-veg');
assert.equal(veg.cost, 15);
const poor = pickBaitForDiet(unlockedBaitsForPlayerLevel(4), 'crucian', 0);
assert.equal(poor.baitId, BASIC_BAIT_ID);
assert.equal(poor.cost, 0);
const carn = pickBaitForDiet(unlockedBaitsForPlayerLevel(4), 'bass', 100);
assert.equal(carn.baitId, 'bait-meat');

section('rod weak bonus + break');
const bambooMul = rodBiteMultiplier(STARTER_ROD_ID, 'gray', 'crucian');
assert.ok(bambooMul > 1 && bambooMul < 1.3, 'bamboo gray still_bait should be a small boost');
const giantOnGray = rodBiteMultiplier('rod-giant', 'gray', 'crucian');
assert.ok(giantOnGray < bambooMul, 'giant rod should not crush gray still-bait');
assert.equal(rodEscapeReductionValue(STARTER_ROD_ID), 0);
assert.ok(isOversizeForRod(STARTER_ROD_ID, 0.5));
assert.equal(isOversizeForRod(STARTER_ROD_ID, 0.2), false);
assert.equal(shouldDestroyRod(STARTER_ROD_ID, 3), true);
assert.equal(shouldDestroyRod(STARTER_ROD_ID, 2), false);
assert.equal(hasUsableRod([], ''), false);
assert.equal(hasUsableRod(['rod-bamboo'], 'rod-bamboo'), true);

console.log('\nFEAT-GEAR-01 verify passed');
