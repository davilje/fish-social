/**
 * FEAT-RETURN-01 unit checks: return_rules, gold mul, eligibility ratios.
 * Run: npm run verify:feat-return-01
 */
import assert from 'node:assert/strict';
import {
  calcFishReturnGold,
  calcFishSellPrice,
  getQualityMaxSize,
  getReturnRules,
  getSpecies,
  qualityIndex,
} from '@fish-social/shared';

const rules = getReturnRules();
assert.equal(rules.goldMulVsSell, 0.7);
assert.equal(rules.minSizeRatio, 0.2);
assert.equal(rules.maxSizeRatio, 1.0);
assert.ok(rules.playerXp >= 0);
assert.ok(rules.pondXp >= 0);
assert.ok(rules.sizeGainMinM > 0);
assert.ok(rules.sizeGainMaxM >= rules.sizeGainMinM);
assert.equal(qualityIndex(rules.minQuality), qualityIndex('gray'));

const item = { quality: 'blue' as const, sizeM: 0.6, speciesId: 'carp' };
const sell = calcFishSellPrice(item);
const ret = calcFishReturnGold(item, rules.goldMulVsSell);
assert.equal(ret, Math.floor(sell * 0.7));
assert.ok(ret < sell || sell === 0, 'return gold should be <= sell at 0.7');

const species = getSpecies('carp');
const max = getQualityMaxSize('blue', species);
assert.ok(item.sizeM < max, 'test fish should be under quality max');
const ratio = item.sizeM / max;
assert.ok(ratio >= rules.minSizeRatio);
assert.ok(ratio < rules.maxSizeRatio);

const full = { quality: 'gray' as const, sizeM: getQualityMaxSize('gray'), speciesId: 'crucian' };
assert.ok(full.sizeM / getQualityMaxSize('gray') >= rules.maxSizeRatio);

console.log('FEAT-RETURN-01 table + gold formula ok');
console.log('  sell=', sell, 'return=', ret, 'ratio=', ratio.toFixed(3));
