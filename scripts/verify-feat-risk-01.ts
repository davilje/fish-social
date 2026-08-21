/**
 * FEAT-RISK-01: forbidden pond police table rules.
 * Run: npm run verify:feat-risk-01
 */
import assert from 'node:assert/strict';
import {
  DEFAULT_FINE_CHANCE_PER_HOUR,
  DEFAULT_FINE_GOLD,
  DEFAULT_POLICE_WARNING_MS,
  POLICE_ESCAPE_BAN_MS,
  POLICE_WARNING_TEXT,
  getGamePondDef,
  getPoliceRules,
  policeTriggerProbability,
} from '@fish-social/shared';

function section(name: string) {
  console.log(`\n== ${name} ==`);
}

section('copy');
assert.equal(POLICE_WARNING_TEXT, '巡警来了！快跑！');
assert.equal(POLICE_ESCAPE_BAN_MS, 2 * 60 * 60 * 1000);
assert.equal(DEFAULT_POLICE_WARNING_MS, 10_000);

section('rules by category');
const forbidden = getPoliceRules('forbidden');
assert.equal(forbidden.enabled, true);
assert.equal(forbidden.chancePerHour, DEFAULT_FINE_CHANCE_PER_HOUR);
assert.equal(forbidden.fineGold, DEFAULT_FINE_GOLD);
assert.equal(forbidden.warningMs, DEFAULT_POLICE_WARNING_MS);

assert.equal(getPoliceRules('advanced').enabled, false);
assert.equal(getPoliceRules('novice').chancePerHour, 0);
assert.equal(getPoliceRules(null).enabled, false);

section('pond catalog');
assert.equal(getGamePondDef('pond-ridge')?.pondCategory, 'forbidden');
assert.equal(getGamePondDef('pond-harbor')?.pondCategory, 'forbidden');
assert.equal(getGamePondDef('pond-orchid')?.pondCategory, 'forbidden');

section('hourly probability');
assert.ok(Math.abs(policeTriggerProbability(0.15, 3_600_000) - 0.15) < 1e-12);
assert.equal(policeTriggerProbability(0, 3_600_000), 0);
assert.ok(policeTriggerProbability(0.15, 1000) > 0);
assert.ok(policeTriggerProbability(0.15, 1000) < 0.001);

console.log('\nFEAT-RISK-01 shared rules ok');
