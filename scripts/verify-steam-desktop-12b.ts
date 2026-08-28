/**
 * STEAM-DESKTOP-12B: list pond/spot fish + force_bite guards.
 * Run: npm run verify:steam-desktop-12b
 */
import '../server/src/db.js';
import { db } from '../server/src/db.js';
import { ensurePlayer } from '../server/src/players.js';
import {
  catchFishDebug,
  catchQualityDebugFish,
  completeCatchFromHooked,
  forceGameplayDebugInstantCatch,
  getGameplayDebugSpotStats,
  instantCatchFromHooked,
  listGameplayDebugPondFish,
  serializeDebugFish,
} from '../server/src/gameplayDebug.js';
import { listPondFishEntities } from '../server/src/pondEcology.js';
import { PONDS } from '@fish-social/shared';

const id = 'test-steam-desktop-12b';
db.prepare('DELETE FROM players WHERE player_id = ?').run(id);
ensurePlayer(id, 'DebugFishInspect');

let list = listGameplayDebugPondFish(id, 'pond');
if (list.ok) throw new Error('pond fish without live pond should fail');
if (!list.error.includes('不在鱼塘')) throw new Error(list.error);

list = listGameplayDebugPondFish(id, 'spot');
if (list.ok) throw new Error('spot fish without live pond should fail');

const spotStats = getGameplayDebugSpotStats(id);
if (spotStats.ok) throw new Error('spot stats without live pond should fail');

const instantCatch = instantCatchFromHooked({} as never, id);
if (instantCatch.ok) throw new Error('instant catch without live pond should fail');

const instantBite = catchFishDebug({} as never, id, 'fish-x');
if (instantBite.ok) throw new Error('catch fish without live pond should fail');

const catchRed = catchQualityDebugFish({} as never, id, 'red');
if (catchRed.ok) throw new Error('catch quality without live pond should fail');

const pondId = PONDS[0]?.id;
if (!pondId) throw new Error('no ponds');
const fish = listPondFishEntities(pondId);
if (fish.length === 0) throw new Error('expected seeded pond fish');
const view = serializeDebugFish(fish[0]!);
if (!view.id || !view.speciesId || !view.quality) {
  throw new Error('serializeDebugFish missing fields');
}
if (typeof view.nearMaxSize !== 'boolean') {
  throw new Error('nearMaxSize missing');
}

db.prepare('DELETE FROM players WHERE player_id = ?').run(id);
console.log('STEAM-DESKTOP-12B server smoke ok');
