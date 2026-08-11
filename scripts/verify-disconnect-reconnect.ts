/**
 * P0/P1 验收：断线重连后取消 60s 清场定时器；hooked 阶段续接
 * 运行: npm run verify:disconnect-reconnect
 *
 * 可选慢测（需 ~65s）: npx tsx scripts/verify-disconnect-reconnect.ts --slow
 */
import type { Server } from 'socket.io';
import type { ClientToServerEvents, PondFishEntity, ServerToClientEvents } from '@fish-social/shared';
import '../server/src/db.js';
import {
  getUserById,
  joinPond,
  reconnectSession,
  removeDisconnectedUser,
  updatePondUser,
} from '../server/src/gameState.js';
import {
  getHookContext,
  handleDisconnect,
  hasPendingDisconnectTimer,
  restoreDisconnectedUser,
  resumeAfterReconnect,
  scheduleHookFromBite,
} from '../server/src/fishingStateMachine.js';

const POND_ID = 'pond-calm';
const SPOT_ID = 'calm-spot-1';

const mockIo = {
  to: () => ({ emit: () => {} }),
} as unknown as Server<ClientToServerEvents, ServerToClientEvents>;

function mockFish(): PondFishEntity {
  return {
    id: `verify-fish-entity-${Date.now()}`,
    pondId: POND_ID,
    spotId: SPOT_ID,
    speciesId: 'carp',
    quality: 'gray',
    sizeM: 0.3,
    bornAt: Date.now(),
    generation: 1,
  };
}

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function testFishingUserReconnectKeepsSpot(): void {
  console.log('\n=== TC: fishing 断线重连保位 ===');
  const playerId = `verify-fish-${Date.now()}`;
  const socketA = `sock-a-${Date.now()}`;
  const socketB = `sock-b-${Date.now()}`;

  const joined = joinPond(socketA, POND_ID, '钓友A', playerId);
  assert(joined.ok, 'join pond');
  const userId = joined.user.id;

  const user = getUserById(POND_ID, userId)!;
  user.spotId = SPOT_ID;
  user.status = 'fishing';
  user.fishingPhase = 'waiting';
  user.fishingStartedAt = Date.now();
  updatePondUser(POND_ID, user);

  let timedOut = false;
  handleDisconnect(POND_ID, userId, () => {
    timedOut = true;
    removeDisconnectedUser(POND_ID, userId);
  });
  assert(hasPendingDisconnectTimer(userId), 'disconnect timer started');

  const afterDisconnect = getUserById(POND_ID, userId)!;
  assert(afterDisconnect.fishingPhase === 'disconnected', 'phase is disconnected');
  assert(afterDisconnect.spotId === SPOT_ID, 'spot retained while disconnected');

  reconnectSession(socketB, POND_ID, userId, playerId, '钓友A');
  restoreDisconnectedUser(getUserById(POND_ID, userId)!, POND_ID);

  assert(!hasPendingDisconnectTimer(userId), 'disconnect timer cancelled after reconnect');
  const restored = getUserById(POND_ID, userId)!;
  assert(restored.fishingPhase === 'waiting', 'phase restored to waiting');
  assert(restored.disconnectedAt === null || restored.disconnectedAt === undefined, 'disconnectedAt cleared');
  assert(restored.spotId === SPOT_ID, 'spot unchanged after reconnect');
  assert(!timedOut, 'timeout callback not fired yet');
}

function testSeatedUserReconnectKeepsSpot(): void {
  console.log('\n=== TC: seated 断线重连保位 ===');
  const playerId = `verify-seat-${Date.now()}`;
  const socketA = `sock-seat-a-${Date.now()}`;
  const socketB = `sock-seat-b-${Date.now()}`;

  const joined = joinPond(socketA, POND_ID, '钓友B', playerId);
  assert(joined.ok, 'join pond');
  const userId = joined.user.id;

  const user = getUserById(POND_ID, userId)!;
  user.spotId = SPOT_ID;
  user.status = 'idle';
  user.fishingPhase = 'seated';
  updatePondUser(POND_ID, user);

  handleDisconnect(POND_ID, userId, () => {
    removeDisconnectedUser(POND_ID, userId);
  });
  reconnectSession(socketB, POND_ID, userId, playerId, '钓友B');
  restoreDisconnectedUser(getUserById(POND_ID, userId)!, POND_ID);

  const restored = getUserById(POND_ID, userId)!;
  assert(restored.fishingPhase === 'seated', 'phase restored to seated');
  assert(restored.spotId === SPOT_ID, 'seated spot retained');
  assert(!hasPendingDisconnectTimer(userId), 'timer cancelled');
}

function testHookedReconnectRestoresPhase(): void {
  console.log('\n=== TC: hooked 断线重连续接 ===');
  const playerId = `verify-hooked-${Date.now()}`;
  const socketA = `sock-hooked-a-${Date.now()}`;
  const socketB = `sock-hooked-b-${Date.now()}`;

  const joined = joinPond(socketA, POND_ID, '钓友D', playerId);
  assert(joined.ok, 'join pond');
  const userId = joined.user.id;

  const user = getUserById(POND_ID, userId)!;
  user.spotId = SPOT_ID;
  user.status = 'fishing';
  user.fishingStartedAt = Date.now();
  updatePondUser(POND_ID, user);

  const hookDurationMs = 8000;
  scheduleHookFromBite(mockIo, POND_ID, userId, socketA, {
    fish: mockFish(),
    escaped: false,
    hookDurationMs,
  });
  const hooked = getUserById(POND_ID, userId)!;
  assert(hooked.fishingPhase === 'hooked', 'user is hooked');
  const hookEndsAt = hooked.phaseEndsAt;
  assert(hookEndsAt != null && hookEndsAt > Date.now(), 'hook timer active');

  handleDisconnect(POND_ID, userId, () => {
    removeDisconnectedUser(POND_ID, userId);
  });
  assert(getHookContext(userId)?.hookEndsAt === hookEndsAt, 'hookEndsAt preserved on disconnect');

  reconnectSession(socketB, POND_ID, userId, playerId, '钓友D');
  resumeAfterReconnect(mockIo, POND_ID, getUserById(POND_ID, userId)!, socketB);

  const restored = getUserById(POND_ID, userId)!;
  assert(restored.fishingPhase === 'hooked', 'phase restored to hooked');
  assert(restored.phaseEndsAt === hookEndsAt, 'hook deadline unchanged');
  assert(getHookContext(userId) !== undefined, 'hook context retained');
}

function testHookedReconnectExpiredEntersResolving(): void {
  console.log('\n=== TC: hooked 断线超时重连进 resolving ===');
  const playerId = `verify-hooked-exp-${Date.now()}`;
  const socketA = `sock-hooked-exp-a-${Date.now()}`;
  const socketB = `sock-hooked-exp-b-${Date.now()}`;

  const joined = joinPond(socketA, POND_ID, '钓友E', playerId);
  assert(joined.ok, 'join pond');
  const userId = joined.user.id;

  const user = getUserById(POND_ID, userId)!;
  user.spotId = SPOT_ID;
  user.status = 'fishing';
  user.fishingStartedAt = Date.now();
  updatePondUser(POND_ID, user);

  scheduleHookFromBite(mockIo, POND_ID, userId, socketA, {
    fish: mockFish(),
    escaped: false,
    hookDurationMs: 3000,
  });

  handleDisconnect(POND_ID, userId, () => {
    removeDisconnectedUser(POND_ID, userId);
  });

  const ctx = getHookContext(userId);
  assert(ctx?.hookEndsAt != null, 'hookEndsAt saved');
  ctx!.hookEndsAt = Date.now() - 500;

  reconnectSession(socketB, POND_ID, userId, playerId, '钓友E');
  resumeAfterReconnect(mockIo, POND_ID, getUserById(POND_ID, userId)!, socketB);

  const restored = getUserById(POND_ID, userId)!;
  assert(restored.fishingPhase === 'resolving', 'expired hook advances to resolving');
  assert(restored.phaseContext?.outcome === 'catch', 'catch outcome preserved');
  assert(getHookContext(userId) === undefined, 'hook context cleared after resolve');
}

async function testTimeoutRemovesUserWithoutReconnect(): Promise<void> {
  console.log('\n=== TC: >60s 未重连清场（慢测） ===');
  const playerId = `verify-timeout-${Date.now()}`;
  const socketA = `sock-timeout-${Date.now()}`;

  const joined = joinPond(socketA, POND_ID, '钓友C', playerId);
  assert(joined.ok, 'join pond');
  const userId = joined.user.id;

  const user = getUserById(POND_ID, userId)!;
  user.spotId = SPOT_ID;
  user.status = 'fishing';
  user.fishingPhase = 'waiting';
  updatePondUser(POND_ID, user);

  let removed = false;
  handleDisconnect(POND_ID, userId, () => {
    removed = true;
    removeDisconnectedUser(POND_ID, userId);
  });

  await sleep(65_000);
  assert(removed, 'timeout callback fired after 60s');
  assert(getUserById(POND_ID, userId) === undefined, 'user removed from pond');
}

async function main(): Promise<void> {
  console.log('verify-disconnect-reconnect');
  testFishingUserReconnectKeepsSpot();
  testSeatedUserReconnectKeepsSpot();
  testHookedReconnectRestoresPhase();
  testHookedReconnectExpiredEntersResolving();

  if (process.argv.includes('--slow')) {
    await testTimeoutRemovesUserWithoutReconnect();
  } else {
    console.log('\n  SKIP: slow timeout test (run with --slow to include ~65s wait)');
  }

  console.log('\nAll disconnect/reconnect checks passed.');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
