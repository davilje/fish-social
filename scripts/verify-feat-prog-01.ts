/**
 * FEAT-PROG-01 smoke verify: game-data load, sell formula, pond access, XP rules.
 * Run: npm run build:shared && npx tsx scripts/verify-feat-prog-01.ts
 */
import assert from 'node:assert/strict';
import {
  ADMISSION_FEE_SLICE_MS,
  applyPlayerXp,
  applyPondXp,
  calcDurationPondXp,
  calcFishSellPrice,
  evaluatePondAccess,
  getCatchGroup,
  getGameDataMeta,
  getGamePondDef,
  getFishXpGrant,
  getPondById,
  getPondModifier,
  getSellQualityDef,
  grantCatchXp,
  listGamePonds,
} from '@fish-social/shared';

function section(name: string) {
  console.log(`\n== ${name} ==`);
}

section('game-data');
const meta = getGameDataMeta();
assert.equal(meta.version, '1.0.0');
assert.equal(Number(meta.SIZE_EXP), 1.15);
const ponds = listGamePonds();
assert.equal(ponds.length, 21);
assert.ok(getGamePondDef('pond-novice'));
assert.ok(getGamePondDef('pond-calm')?.feePer2h === 200);
assert.ok(getGamePondDef('pond-crystal')?.feePer2h === 500);
assert.ok(getGamePondDef('pond-frost')?.isOpen === false);
assert.ok(getPondById('pond-novice')?.spots.length === 20);
console.log('ponds + meta OK');

section('pond access');
assert.equal(
  evaluatePondAccess('pond-calm', { onboardingCompleted: false, playerLevel: 1 }).ok,
  false,
);
assert.equal(
  evaluatePondAccess('pond-novice', { onboardingCompleted: false, playerLevel: 1 }).ok,
  true,
);
assert.equal(
  evaluatePondAccess('pond-novice', { onboardingCompleted: true, playerLevel: 1 }).ok,
  false,
);
assert.equal(
  evaluatePondAccess('pond-crystal', { onboardingCompleted: true, playerLevel: 4 }).ok,
  false,
);
assert.equal(
  evaluatePondAccess('pond-crystal', { onboardingCompleted: true, playerLevel: 5 }).ok,
  true,
);
assert.equal(
  evaluatePondAccess('pond-frost', { onboardingCompleted: true, playerLevel: 20 }).ok,
  false,
);
console.log('access gates OK');

section('sell formula');
const gray = calcFishSellPrice({ quality: 'gray', sizeM: 0.2, speciesId: 'crucian' });
assert.ok(gray >= (getSellQualityDef('gray')?.MIN_SELL ?? 40));
const goldish = calcFishSellPrice({ quality: 'gold', sizeM: 5, speciesId: 'marlin' });
assert.ok(goldish > 10000);
assert.equal(getCatchGroup('bass'), 'lure_predator');
console.log('sell gray@0.2=', gray, 'gold marlin@5=', goldish);

section('xp rules');
const grant = getFishXpGrant('crucian', 'gray');
assert.ok(grant.playerXp > 0);
const capped = applyPondXp({ level: 1, xp: 0 }, 9999, 1);
assert.ok(capped.capped || capped.state.level <= 1);
const leveled = applyPlayerXp({ level: 1, xp: 0 }, 100);
assert.equal(leveled.state.level, 2);
const dur = calcDurationPondXp(1, 30 * 60 * 1000);
assert.equal(dur, 6); // 12/h * 0.5h
const catchGrant = grantCatchXp(
  { level: 1, xp: 0 },
  { level: 1, xp: 0 },
  grant.playerXp,
  grant.pondXp,
  'pond-calm',
);
assert.ok(catchGrant.playerXpGranted === grant.playerXp);
console.log('xp OK duration30m=', dur);

section('modifiers');
assert.equal(getPondModifier('wilderness').biteRateMul, 0.75);
assert.equal(getPondModifier('reservoir').pondXpMul, 1.15);
assert.equal(ADMISSION_FEE_SLICE_MS, 2 * 60 * 60 * 1000);
console.log('modifiers OK');

console.log('\nFEAT-PROG-01 verify passed');
