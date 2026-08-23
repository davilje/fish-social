/**
 * FEAT-ALBUM-01：成就表、相册候选规则、钉选上限、profile-hub 聚合。
 * Run: npm run verify:feat-album-01
 */
import assert from 'node:assert/strict';
import '../server/src/db.js';
import { db } from '../server/src/db.js';
import { ensurePlayer, addCoins } from '../server/src/players.js';
import {
  addAlbumCandidate,
  getAlbumPinCap,
  listAlbumCandidates,
  listAlbumPins,
  setAlbumPins,
  shouldAutoCandidate,
} from '../server/src/album.js';
import { getAchievementCatalog, tryUnlockAchievements, listPlayerUnlocks } from '../server/src/achievements.js';
import { getProfileHub } from '../server/src/profileHub.js';
import { listAchievements } from '@fish-social/shared';

const playerId = 'test-album-01';
db.prepare('DELETE FROM player_album_pins WHERE player_id = ?').run(playerId);
db.prepare('DELETE FROM player_album_candidates WHERE player_id = ?').run(playerId);
db.prepare('DELETE FROM player_achievements WHERE player_id = ?').run(playerId);
db.prepare('DELETE FROM fish_codex WHERE player_id = ?').run(playerId);
db.prepare('DELETE FROM inventory WHERE player_id = ?').run(playerId);
db.prepare('DELETE FROM players WHERE player_id = ?').run(playerId);

ensurePlayer(playerId, 'AlbumTester');
addCoins(playerId, 100);

assert.equal(getAlbumPinCap(), 12);
const defs = listAchievements();
assert.ok(defs.length >= 6, `expected >=6 achievements, got ${defs.length}`);
assert.ok(defs.some((d) => d.achievementId === 'ach-first-catch'));
assert.ok(getAchievementCatalog().length >= 6);

assert.equal(
  shouldAutoCandidate({
    quality: 'gray',
    sizeM: 0.1,
    speciesId: 'crucian',
    source: 'catch',
  }),
  false,
);
assert.equal(
  shouldAutoCandidate({
    quality: 'blue',
    sizeM: 0.1,
    speciesId: 'crucian',
    source: 'catch',
  }),
  true,
);
assert.equal(
  shouldAutoCandidate({
    quality: 'gray',
    sizeM: 0.1,
    speciesId: 'crucian',
    source: 'return',
  }),
  true,
);

const c1 = addAlbumCandidate({
  playerId,
  speciesId: 'crucian',
  quality: 'blue',
  sizeM: 0.25,
  pondId: 'pond-calm',
  source: 'catch',
});
assert.ok(c1);
const c2 = addAlbumCandidate({
  playerId,
  speciesId: 'carp',
  quality: 'purple',
  sizeM: 0.4,
  pondId: 'pond-calm',
  source: 'first_codex',
});
assert.ok(c2);
const c3 = addAlbumCandidate({
  playerId,
  speciesId: 'bass',
  quality: 'green',
  sizeM: 0.3,
  pondId: 'pond-calm',
  source: 'return',
});
assert.ok(c3);

assert.equal(listAlbumCandidates(playerId).length, 3);

const pinned = setAlbumPins(playerId, [c1!.id, c2!.id, c3!.id]);
assert.ok(pinned.ok);
assert.equal(pinned.ok && pinned.pinCount, 3);
assert.equal(listAlbumPins(playerId).length, 3);

const manyIds: string[] = [];
for (let i = 0; i < 13; i++) {
  const c = addAlbumCandidate({
    playerId,
    speciesId: 'crucian',
    quality: 'blue',
    sizeM: 0.2 + i * 0.01,
    pondId: 'pond-calm',
    source: 'catch',
  });
  assert.ok(c);
  manyIds.push(c!.id);
}
const over = setAlbumPins(playerId, manyIds);
assert.ok(!over.ok);
assert.equal(!over.ok && over.code, 'PIN_CAP');

// restore 3 pins for hub checks
assert.ok(setAlbumPins(playerId, [c1!.id, c2!.id, c3!.id]).ok);
// First catch achievement via inventory insert
db.prepare(
  `INSERT INTO inventory (id, player_id, species_id, quality, size_m, caught_at)
   VALUES ('inv-album-1', ?, 'crucian', 'blue', 0.25, ?)`,
).run(playerId, Date.now());
const newly = tryUnlockAchievements(playerId);
assert.ok(newly.some((a) => a.achievementId === 'ach-first-catch'));
assert.ok(newly.some((a) => a.achievementId === 'ach-album-3'));
assert.ok(listPlayerUnlocks(playerId).length >= 2);

const hubSelf = getProfileHub(playerId, playerId);
assert.ok(hubSelf);
assert.equal(hubSelf!.isSelf, true);
assert.equal(hubSelf!.canEdit, true);
assert.equal(hubSelf!.albumPins.length, 3);
assert.ok(hubSelf!.albumCandidates.length >= 3);
assert.ok(hubSelf!.achievements.some((a) => a.unlocked && a.achievementId === 'ach-first-catch'));

const otherId = 'test-album-viewer';
db.prepare('DELETE FROM players WHERE player_id = ?').run(otherId);
ensurePlayer(otherId, 'Viewer');
const hubOther = getProfileHub(playerId, otherId);
assert.ok(hubOther);
assert.equal(hubOther!.isSelf, false);
assert.equal(hubOther!.canEdit, false);
assert.equal(hubOther!.albumCandidates.length, 0, 'others must not see candidates');
assert.ok(hubOther!.albumPins.length === 3);
assert.ok(hubOther!.achievements.every((a) => a.unlocked));

console.log('FEAT-ALBUM-01 verify ok');
console.log('  achievements=', defs.length, 'pinCap=', getAlbumPinCap());
