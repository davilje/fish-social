/**
 * v0.5 R0-2 验收：在线态 checkpoint 与 pending 落库
 * 运行: npm run verify:session-checkpoint
 */
import '../server/src/db.js';
import {
  detachPondUserForCheckpointTest,
  getUserById,
  joinPond,
  restoreUserFromCheckpoint,
  startFishing,
  updatePondUser,
} from '../server/src/gameState.js';
import {
  getPendingCatch,
  lockPendingCatch,
  PENDING_CATCH_TIMEOUT_MS,
  restorePendingCatchFromDb,
} from '../server/src/inventory.js';
import {
  loadPlayerPondSession,
  upsertPlayerPondSession,
} from '../server/src/playerPondSession.js';
import { db } from '../server/src/db.js';

const POND_ID = 'pond-calm';
const SPOT_ID = 'calm-spot-1';

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function testCheckpointRestoreWaitingSpot(): void {
  console.log('\n=== TC: checkpoint restore waiting spot ===');
  const playerId = `verify-cp-${Date.now()}`;
  const socketA = `sock-cp-a-${Date.now()}`;
  const socketB = `sock-cp-b-${Date.now()}`;

  const joined = joinPond(socketA, POND_ID, '钓友CP', playerId);
  assert(joined.ok, 'join pond');

  const spot = startFishing(socketA, POND_ID, SPOT_ID);
  assert(spot.ok, 'take spot');

  const user = getUserById(POND_ID, spot.user.id)!;
  user.fishingPhase = 'waiting';
  user.status = 'fishing';
  user.fishingStartedAt = Date.now();
  updatePondUser(POND_ID, user);
  upsertPlayerPondSession(user, POND_ID);

  const row = loadPlayerPondSession(playerId, POND_ID);
  assert(row?.spot_id === SPOT_ID, 'checkpoint row has spot');
  assert(row?.fishing_phase === 'waiting', 'checkpoint row has waiting phase');

  detachPondUserForCheckpointTest(POND_ID, user.id);
  assert(getUserById(POND_ID, user.id) === undefined, 'memory user cleared');

  const restored = restoreUserFromCheckpoint(socketB, POND_ID, playerId, '钓友CP');
  assert(restored != null, 'restore from checkpoint');
  assert(restored!.spotId === SPOT_ID, 'spotId restored');
  assert(restored!.fishingPhase === 'waiting', 'phase restored');
}

function testPendingLockPersisted(): void {
  console.log('\n=== TC: pending lock persisted to DB ===');
  const userId = `user-pending-${Date.now()}`;
  const playerId = `player-pending-${Date.now()}`;
  const catchId = `catch-${Date.now()}`;
  const pondFishId = `fish-${Date.now()}`;

  const locked = lockPendingCatch(
    userId,
    {
      catchId,
      pondFishId,
      speciesId: 'carp',
      quality: 'gray',
      sizeM: 0.25,
      hookDurationMs: 1000,
    },
    { playerId, pondId: POND_ID },
  );
  assert(locked != null, 'lock pending in memory');

  const row = db
    .prepare('SELECT * FROM pending_catch_locks WHERE user_id = ?')
    .get(userId) as { catch_id: string; pond_fish_id: string } | undefined;
  assert(row?.catch_id === catchId, 'pending row in sqlite');

  restorePendingCatchFromDb(
    userId,
    locked!,
    { playerId, pondId: POND_ID },
    PENDING_CATCH_TIMEOUT_MS - 1000,
  );
  assert(getPendingCatch(userId)?.catchId === catchId, 'pending restored to memory after simulated restart');
}

function main(): void {
  console.log('verify-session-checkpoint');
  testCheckpointRestoreWaitingSpot();
  testPendingLockPersisted();
  console.log('\nAll session checkpoint checks passed.');
}

main();
