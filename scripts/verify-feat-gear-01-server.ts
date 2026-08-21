import '../server/src/db.js';
import { ensurePlayer } from '../server/src/players.js';
import { addCoins } from '../server/src/players.js';
import { ensurePlayerProgress } from '../server/src/playerProgress.js';
import {
  canStartFishingWithRod,
  ensurePlayerGear,
  noteRodOversizeLanding,
} from '../server/src/gear.js';
import { db } from '../server/src/db.js';

const id = 'test-gear-01-smoke';
db.prepare('DELETE FROM player_gear WHERE player_id = ?').run(id);
db.prepare('DELETE FROM player_fishing_progress WHERE player_id = ?').run(id);
db.prepare('DELETE FROM players WHERE player_id = ?').run(id);

ensurePlayer(id, 'GearTester');
ensurePlayerProgress(id);
const granted = ensurePlayerGear(id);
if (!granted.ownedRods.includes('rod-bamboo')) throw new Error('starter rod missing');
const after = canStartFishingWithRod(id);
if (!after.ok) throw new Error(after.error);

addCoins(id, 50);
const gear = ensurePlayerGear(id);
if (!gear.ownedRods.includes('rod-bamboo')) throw new Error('starter rod missing');

for (let i = 0; i < 3; i++) {
  noteRodOversizeLanding(id, 0.5);
}
const broken = canStartFishingWithRod(id);
if (broken.ok) throw new Error('expected rod destroyed after 3 oversized landings');

db.prepare('DELETE FROM player_gear WHERE player_id = ?').run(id);
db.prepare('DELETE FROM player_fishing_progress WHERE player_id = ?').run(id);
db.prepare('DELETE FROM players WHERE player_id = ?').run(id);
console.log('FEAT-GEAR-01 server smoke ok');
