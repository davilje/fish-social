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
assert.equal(rules.goldMulVsSell, 1.5);
assert.equal(rules.minSizeRatio, 0.75);
assert.equal(rules.maxSizeRatio, 1.0);
assert.ok(rules.playerXp >= 0);
assert.ok(rules.pondXp >= 0);
assert.ok(rules.sizeGainMinM > 0);
assert.ok(rules.sizeGainMaxM >= rules.sizeGainMinM);
assert.equal(qualityIndex(rules.minQuality), qualityIndex('purple'));

const item = { quality: 'purple' as const, sizeM: 3.4, speciesId: 'crucian' };
const sell = calcFishSellPrice(item);
const ret = calcFishReturnGold(item, rules.goldMulVsSell);
assert.equal(ret, Math.floor(sell * 1.5));
assert.ok(ret > sell || sell === 0, 'return gold should be > sell at 1.5×');

const species = getSpecies('crucian');
const max = getQualityMaxSize('purple', species);
assert.ok(item.sizeM < max, 'test fish should be under quality max');
const ratio = item.sizeM / max;
assert.ok(ratio >= rules.minSizeRatio);
assert.ok(ratio < rules.maxSizeRatio);

const full = { quality: 'gray' as const, sizeM: getQualityMaxSize('gray'), speciesId: 'crucian' };
assert.ok(full.sizeM / getQualityMaxSize('gray') >= rules.maxSizeRatio);

console.log('FEAT-RETURN-01 table + gold formula ok');
console.log('  sell=', sell, 'return=', ret, 'ratio=', ratio.toFixed(3));
