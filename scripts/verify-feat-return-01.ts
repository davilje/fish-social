/**

 * FEAT-RETURN-01/05 unit checks: return_rules, weight tiers, purple+ gate, gold mul.

 * Run: npm run verify:feat-return-01

 */

import assert from 'node:assert/strict';

import {

  calcFishReturnGold,

  calcFishSellPrice,

  calcFishWeightJin,

  getQualityMaxSize,

  getReturnRules,

  getSpecies,

  isReturnEligible,

  resolveReturnGoldMul,

} from '@fish-social/shared';



const rules = getReturnRules();

assert.equal(rules.goldMulVsSell, 1.5);

assert.equal(rules.goldMulHeavy, 3);

assert.equal(rules.minWeightJin, 10);

assert.equal(rules.heavyWeightJin, 100);

assert.equal(rules.minQuality, 'purple');

assert.equal(rules.maxSizeRatio, 1.0);

assert.ok(rules.playerXp >= 0);

assert.ok(rules.pondXp >= 0);

assert.ok(rules.sizeGainMinM > 0);

assert.ok(rules.sizeGainMaxM >= rules.sizeGainMinM);



// purple max=0.8m → ~12 斤；0.5m → ~3 斤；red 1.7m → >100 斤

const light = { quality: 'purple' as const, sizeM: 0.5, speciesId: 'crucian' };

const mid = { quality: 'purple' as const, sizeM: 0.78, speciesId: 'crucian' };

const grayHeavy = { quality: 'gray' as const, sizeM: 0.2, speciesId: 'crucian' };

const heavy = { quality: 'red' as const, sizeM: 1.7, speciesId: 'chinese_sturgeon' };



assert.ok(calcFishWeightJin(light.sizeM) < 10, 'light fish <10 jin');

assert.ok(calcFishWeightJin(mid.sizeM) >= 10 && calcFishWeightJin(mid.sizeM) <= 100, 'mid fish 10~100 jin');

assert.ok(calcFishWeightJin(heavy.sizeM) > 100, 'heavy fish >100 jin');



assert.equal(resolveReturnGoldMul(light.sizeM, rules), 0);

assert.equal(resolveReturnGoldMul(mid.sizeM, rules), 1.5);

assert.equal(resolveReturnGoldMul(heavy.sizeM, rules), 3);



assert(!isReturnEligible(light, rules), '<10 jin not eligible');

assert(isReturnEligible(mid, rules), 'purple ≥10 jin eligible');

assert(!isReturnEligible(grayHeavy, rules), 'gray not eligible even at size cap');

assert(isReturnEligible(heavy, rules), 'red >100 jin eligible');



const midSell = calcFishSellPrice(mid);

const midRet = calcFishReturnGold(mid, {

  goldMulVsSell: rules.goldMulVsSell,

  goldMulHeavy: rules.goldMulHeavy,

  minWeightJin: rules.minWeightJin,

  heavyWeightJin: rules.heavyWeightJin,

});

assert.equal(midRet, Math.floor(midSell * 1.5));



const heavySell = calcFishSellPrice(heavy);

const heavyRet = calcFishReturnGold(heavy, {

  goldMulVsSell: rules.goldMulVsSell,

  goldMulHeavy: rules.goldMulHeavy,

  minWeightJin: rules.minWeightJin,

  heavyWeightJin: rules.heavyWeightJin,

});

assert.equal(heavyRet, Math.floor(heavySell * 3));



const forced = calcFishReturnGold(heavy, 1.5);

assert.equal(forced, Math.floor(heavySell * 1.5));



assert.equal(getQualityMaxSize('gray'), 0.2);

assert.equal(getQualityMaxSize('purple'), 0.8);

assert.equal(getQualityMaxSize('red'), 3);

assert.ok(mid.sizeM <= getQualityMaxSize('purple', getSpecies('crucian')));

assert.ok(heavy.sizeM <= getQualityMaxSize('red', getSpecies('chinese_sturgeon')));



console.log('FEAT-RETURN-01/05 weight+quality gates + gold formula ok');

console.log(

  '  mid jin=',

  calcFishWeightJin(mid.sizeM),

  'ret=',

  midRet,

  ' heavy jin=',

  calcFishWeightJin(heavy.sizeM),

  'ret=',

  heavyRet,

);


