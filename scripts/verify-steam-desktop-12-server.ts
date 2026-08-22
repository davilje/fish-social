/**
 * STEAM-DESKTOP-12 server smoke: level/gold/fish/pond fail paths.
 * Run: npm run verify:steam-desktop-12-server
 */
import '../server/src/db.js';
import { db } from '../server/src/db.js';
import { addCoins, ensurePlayer, getPlayer } from '../server/src/players.js';
import { getInventory } from '../server/src/inventory.js';
import {
  executeGameplayDebugAction,
  GAMEPLAY_DEBUG_GOLD,
} from '../server/src/gameplayDebug.js';
import { getProgressPublicView } from '../server/src/playerProgress.js';
import { getMaxPlayerLevel } from '@fish-social/shared';

const id = 'test-steam-desktop-12';
db.prepare('DELETE FROM inventory WHERE player_id = ?').run(id);
db.prepare('DELETE FROM player_pond_proficiency WHERE player_id = ?').run(id);
db.prepare('DELETE FROM player_fishing_progress WHERE player_id = ?').run(id);
db.prepare('DELETE FROM players WHERE player_id = ?').run(id);

ensurePlayer(id, 'DebugTester');
addCoins(id, 10);

let r = executeGameplayDebugAction(id, 'level_up');
if (!r.ok) throw new Error(r.error);
if (getProgressPublicView(id).level !== 2) throw new Error('level_up should reach 2');

r = executeGameplayDebugAction(id, 'level_max');
if (!r.ok) throw new Error(r.error);
if (getProgressPublicView(id).level !== getMaxPlayerLevel()) {
  throw new Error('level_max should hit table max');
}
r = executeGameplayDebugAction(id, 'level_up');
if (r.ok) throw new Error('full level should fail');
if (!r.error.includes('已满')) throw new Error(r.error);

const beforeCoins = getPlayer(id)?.coins ?? 0;
r = executeGameplayDebugAction(id, 'add_gold');
if (!r.ok) throw new Error(r.error);
if ((getPlayer(id)?.coins ?? 0) !== beforeCoins + GAMEPLAY_DEBUG_GOLD) {
  throw new Error('gold grant mismatch');
}

r = executeGameplayDebugAction(id, 'grant_fish');
if (!r.ok) throw new Error(r.error);
const bag = getInventory(id);
if (!bag.some((item) => item.speciesId === 'crucian' && item.quality === 'gray')) {
  throw new Error('expected crucian debug fish');
}

r = executeGameplayDebugAction(id, 'pond_level_up');
if (r.ok) throw new Error('pond up without live pond should fail');
if (!r.error.includes('不在鱼塘')) throw new Error(r.error);

r = executeGameplayDebugAction(id, 'police_raid');
if (r.ok) throw new Error('police without pond should fail');

r = executeGameplayDebugAction(id, 'advance_fee_2h');
if (r.ok) throw new Error('fee +2h without pond should fail');

r = executeGameplayDebugAction(id, 'not_a_real_action');
if (r.ok) throw new Error('unknown action should fail');

db.prepare('DELETE FROM inventory WHERE player_id = ?').run(id);
db.prepare('DELETE FROM player_pond_proficiency WHERE player_id = ?').run(id);
db.prepare('DELETE FROM player_fishing_progress WHERE player_id = ?').run(id);
db.prepare('DELETE FROM players WHERE player_id = ?').run(id);

console.log('STEAM-DESKTOP-12 server smoke ok');
