/**
 * STEAM-DESKTOP-12: gameplay debug action catalog + gates.
 * Run: npm run verify:steam-desktop-12
 */
import assert from 'node:assert/strict';
import {
  GAMEPLAY_DEBUG_ACTIONS,
  GAMEPLAY_DEBUG_FISH,
  GAMEPLAY_DEBUG_GOLD,
  isGameplayDebugEnabled,
} from '../server/src/gameplayDebug.js';
import {
  getMaxPlayerLevel,
  getPlayerLevelDef,
} from '@fish-social/shared';
import { FISHING_METRIC_EVENTS, validateMetricPayload } from '@fish-social/shared/metrics-schema.js';

function section(name: string) {
  console.log(`\n== ${name} ==`);
}

section('catalog');
assert.deepEqual([...GAMEPLAY_DEBUG_ACTIONS], [
  'level_up',
  'level_max',
  'pond_level_up',
  'pond_level_max',
  'add_gold',
  'police_raid',
  'grant_fish',
  'advance_fee_2h',
]);
assert.equal(GAMEPLAY_DEBUG_GOLD, 1_000_000);
assert.equal(GAMEPLAY_DEBUG_FISH.speciesId, 'crucian');
assert.equal(GAMEPLAY_DEBUG_FISH.quality, 'gray');

section('levels');
assert.equal(getMaxPlayerLevel(), 20);
assert.equal(getPlayerLevelDef(20)?.xpToNext, 0);
assert.equal(getPlayerLevelDef(1)?.maxPondLevel, 1);
assert.equal(getPlayerLevelDef(20)?.maxPondLevel, 10);

section('metrics');
const schema = FISHING_METRIC_EVENTS.find((e) => e.eventType === 'gameplay_debug_action');
assert.ok(schema, 'gameplay_debug_action in schema');
assert.deepEqual(schema.requiredFields, ['playerId', 'action']);
assert.deepEqual(
  validateMetricPayload('gameplay_debug_action', { playerId: 'p1', action: 'level_up' }),
  [],
);

section('gate');
const prev = process.env.NODE_ENV;
process.env.NODE_ENV = 'production';
delete process.env.GAMEPLAY_DEBUG;
assert.equal(isGameplayDebugEnabled(), false);
process.env.GAMEPLAY_DEBUG = '1';
assert.equal(isGameplayDebugEnabled(), true);
if (prev === undefined) delete process.env.NODE_ENV;
else process.env.NODE_ENV = prev;
delete process.env.GAMEPLAY_DEBUG;

console.log('\nSTEAM-DESKTOP-12 catalog + gates ok');
