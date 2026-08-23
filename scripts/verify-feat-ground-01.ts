/**
 * FEAT-GROUND-01 unit checks: nonlinear formula, stack cap, table load.
 * Run: npm run verify:feat-ground-01
 */
import assert from 'node:assert/strict';
import {
  calcGroundbaitBiteBonus,
  calcGroundbaitSizeBonus,
  getGroundbaitDef,
  getGroundbaitMaxStack,
  linearGroundbaitBiteCap,
  listGroundbaits,
} from '@fish-social/shared';

const maxStack = getGroundbaitMaxStack();
assert.equal(maxStack, 50);

const list = listGroundbaits();
assert.ok(list.length >= 3, 'expected >=3 groundbaits');
const basic = getGroundbaitDef('gb-basic');
assert.ok(basic, 'gb-basic missing');
assert.equal(basic.costGoldPerUse, 30);
assert.equal(basic.castDurationMs, 3000);

const at50 = calcGroundbaitBiteBonus(50, basic.maxBonus, basic.stackK);
const linear50 = linearGroundbaitBiteCap(basic.perStackBiteBonus, 50);
assert.ok(at50 < linear50, `nonlinear ${at50} must be < linear ${linear50}`);
assert.ok(at50 < basic.maxBonus + 1e-9, 'bonus must not exceed maxBonus');
assert.ok(at50 > 0);

const size = calcGroundbaitSizeBonus(10, basic.sizeBonusPerStack, basic.maxSizeBonus);
assert.ok(size <= basic.maxSizeBonus);
assert.ok(size > 0);

const mix = getGroundbaitDef('gb-mix');
const prem = getGroundbaitDef('gb-premium');
assert.ok(mix && prem);

console.log('FEAT-GROUND-01 table + nonlinear ok');
console.log('  gb-basic @50 bite=', at50.toFixed(4), 'linear=', linear50.toFixed(4));
console.log('  size@10=', size.toFixed(4));
