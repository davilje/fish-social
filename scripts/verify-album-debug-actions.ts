/**
 * Smoke: FEAT album UI debug actions + auto album pin.
 * Run: npx tsx scripts/verify-album-debug-actions.ts
 */
import '../server/src/db.js';
import { ensurePlayer } from '../server/src/players.js';
import { executeGameplayDebugAction } from '../server/src/gameplayDebug.js';
import { getInventory } from '../server/src/inventory.js';
import { listAlbumPins } from '../server/src/album.js';
import { getTodayFishingMs } from '../server/src/pondUserManager.js';
import { qualityIndex } from '@fish-social/shared';

const id = 'test-album-debug-ui';
ensurePlayer(id, 'AlbumDbg');

const max = executeGameplayDebugAction(id, 'grant_fish_max_size');
if (!max.ok) throw new Error(max.error);
const epic = executeGameplayDebugAction(id, 'grant_fish_epic_plus');
if (!epic.ok) throw new Error(epic.error);
const items = getInventory(id);
const last = items[items.length - 1];
if (!last) throw new Error('no fish');
if (qualityIndex(last.quality as never) < qualityIndex('purple')) {
  throw new Error('expected epic+ quality, got ' + last.quality);
}

const pins = listAlbumPins(id);
if (pins.length < 2) {
  throw new Error('expected album pins after debug grants, got ' + pins.length);
}

const reset = executeGameplayDebugAction(id, 'reset_fishing_duration');
if (!reset.ok) throw new Error(reset.error);
if (getTodayFishingMs(id) !== 0) throw new Error('duration not reset');

console.log('album debug actions ok');
console.log('  max:', max.message);
console.log('  epic:', epic.message);
console.log('  pins:', pins.length);
