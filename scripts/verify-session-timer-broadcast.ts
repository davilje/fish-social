/**
 * BE-OPT-B / BUG-07：waiting 阶段 sessionFishingMs 递增 + session_timer_tick 不走 dirty
 * 运行: npm run verify:session-timer-broadcast
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import '../server/src/db.js';
import {
  enrichPondUser,
  getUserById,
  joinPond,
  startFishing,
  updatePondUser,
} from '../server/src/gameState.js';

const POND_ID = 'pond-calm';
const SPOT_ID = 'calm-spot-1';
const rootDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function testEnrichSessionFishingMsIncreases(): Promise<void> {
  console.log('\n=== TC: enrichPondUser sessionFishingMs increases over time ===');
  const playerId = `verify-timer-${Date.now()}`;
  const socketId = `sock-timer-${Date.now()}`;

  const joined = joinPond(socketId, POND_ID, '钓友计时', playerId);
  assert(joined.ok, 'join pond');
  const start = startFishing(socketId, POND_ID, SPOT_ID);
  assert(start.ok, 'take spot');

  const user = getUserById(POND_ID, joined.user.id);
  assert(Boolean(user), 'user in pond');
  user!.status = 'fishing';
  user!.fishingPhase = 'waiting';
  user!.fishingStartedAt = Date.now() - 2_000;
  updatePondUser(POND_ID, user!);

  const first = enrichPondUser(user!);
  assert((first.sessionFishingMs ?? 0) >= 2_000, 'initial sessionFishingMs at least 2s');
  await sleep(1_100);
  const refreshed = getUserById(POND_ID, joined.user.id)!;
  const second = enrichPondUser(refreshed);
  assert(
    (second.sessionFishingMs ?? 0) > (first.sessionFishingMs ?? 0),
    'sessionFishingMs increases after 1.1s in waiting',
  );
}

function testSessionTimerLoopSource(): void {
  console.log('\n=== TC: sessionTimer emits session_timer_tick (not dirty merge) ===');
  const src = fs.readFileSync(path.join(rootDir, 'server/src/serverLoops.ts'), 'utf8');
  const sessionBlock = src.match(
    /\/\/ PERF-03[\s\S]*?stopFns\.push\(\(\) => clearInterval\(sessionTimer\)\);/,
  );
  assert(Boolean(sessionBlock), 'PERF-03 sessionTimer block present');
  assert(sessionBlock![0].includes("emit('session_timer_tick'"), 'emits session_timer_tick');
  assert(sessionBlock![0].includes('listUsersInPond(pond.id)'), 'iterates all pond users');
  assert(sessionBlock![0].includes('.map(enrichPondUser)'), 'enriches for live sessionFishingMs');
  assert(!sessionBlock![0].includes('consumeDirtyUsers('), 'sessionTimer does not call consumeDirtyUsers');
  assert(!sessionBlock![0].includes("emit('pond_user_updated'"), 'sessionTimer does not emit full pond_user_updated');
  assert(!sessionBlock![0].includes('fishingPhase:'), 'PERF-03b: tick omit fishingPhase');
  assert(!sessionBlock![0].includes('fishingStartedAt:'), 'PERF-03b: tick omit fishingStartedAt');
  assert(src.includes('getWaitingUserIds(pond.id)'), 'bite loop still uses waiting index');
}

function testClientMergesSessionTimerTick(): void {
  console.log('\n=== TC: mobile merges session_timer_tick ===');
  const src = fs.readFileSync(path.join(rootDir, 'mobile/lib/usePondSocket.ts'), 'utf8');
  assert(src.includes("socket.on('session_timer_tick'"), 'client subscribes to session_timer_tick');
  assert(src.includes('sessionFishingMs: payload.sessionFishingMs'), 'client merges sessionFishingMs');
  assert(!src.includes('payload.fishingPhase'), 'client does not merge phase from tick');
}

function testBug13ZeroFlashGuards(): void {
  console.log('\n=== TC: BUG-13 bots enrich + client merge guards ===');
  const bots = fs.readFileSync(path.join(rootDir, 'server/src/bots.ts'), 'utf8');
  assert(bots.includes('emitPondUserUpdated'), 'bots uses emitPondUserUpdated');
  assert(!/emit\('pond_user_updated'/.test(bots), 'bots has no bare pond_user_updated emit');
  assert(bots.includes('Heal stuck bots') || bots.includes('fishingStartedAt == null'), 'bots heal missing anchors');

  const manager = fs.readFileSync(path.join(rootDir, 'server/src/pondUserManager.ts'), 'utf8');
  assert(manager.includes('export function emitPondUserUpdated'), 'emitPondUserUpdated exported');
  assert(manager.includes('ensureFishingStartedAt'), 'enrich path ensures fishingStartedAt');
  assert(manager.includes("fishingPhase = 'idle'"), 'stopBotFishing clears fishingPhase');
  assert(manager.includes('setUserWaiting(pondId, userId, true)'), 'startBotFishing sets waiting index');

  const client = fs.readFileSync(path.join(rootDir, 'mobile/lib/usePondSocket.ts'), 'utf8');
  assert(client.includes('mergePondUserUpdated'), 'client has mergePondUserUpdated');
  assert(client.includes('isFishingActive(user.fishingPhase)'), 'client interpolates with isFishingActive');
  assert(
    !client.includes('u.id === user.id ? user : u'),
    'client does not whole-replace pond_user_updated',
  );
  assert(client.includes('incoming.fishingStartedAt == null'), 'merge preserves null fishingStartedAt');
  assert(client.includes('Math.max(payload.sessionFishingMs, localMs)'), 'tick refuses regress to below local');

  const badge = fs.readFileSync(path.join(rootDir, 'mobile/components/PondCharacter.tsx'), 'utf8');
  assert(badge.includes('fishingStartedAt'), 'PondCharacter prefers fishingStartedAt for sessionMs');
}

async function testEnrichRecoversMissingStartedAt(): Promise<void> {
  console.log('\n=== TC: enrich recovers missing fishingStartedAt on waiting ===');
  const playerId = `verify-timer-anchor-${Date.now()}`;
  const socketId = `sock-timer-anchor-${Date.now()}`;
  const spotId = 'calm-spot-2';

  const joined = joinPond(socketId, POND_ID, '锚点恢复', playerId);
  assert(joined.ok, 'join pond');
  const start = startFishing(socketId, POND_ID, spotId);
  assert(start.ok, 'take spot');

  const user = getUserById(POND_ID, joined.user.id)!;
  user.status = 'idle';
  user.fishingPhase = 'waiting';
  user.fishingStartedAt = null;
  updatePondUser(POND_ID, user);

  const first = enrichPondUser(user);
  assert(first.fishingStartedAt != null, 'enrich sets fishingStartedAt');
  assert((first.sessionFishingMs ?? 0) >= 0, 'sessionFishingMs non-null');
  await sleep(1_100);
  const refreshed = getUserById(POND_ID, joined.user.id)!;
  const second = enrichPondUser(refreshed);
  assert(
    (second.sessionFishingMs ?? 0) > (first.sessionFishingMs ?? 0),
    'sessionFishingMs increases after recover',
  );
}

function testSharedEventType(): void {
  console.log('\n=== TC: shared SessionTimerTickPayload ===');
  const src = fs.readFileSync(path.join(rootDir, 'shared/types.ts'), 'utf8');
  assert(src.includes('SessionTimerTickPayload'), 'shared payload type exists');
  assert(src.includes('session_timer_tick:'), 'ServerToClientEvents includes session_timer_tick');
  const payloadBlock = src.match(/export interface SessionTimerTickPayload \{[\s\S]*?\}/);
  assert(Boolean(payloadBlock), 'payload interface block');
  assert(payloadBlock![0].includes('userId'), 'payload has userId');
  assert(payloadBlock![0].includes('sessionFishingMs'), 'payload has sessionFishingMs');
  assert(!payloadBlock![0].includes('fishingPhase'), 'PERF-03b: payload has no fishingPhase');
  assert(!payloadBlock![0].includes('fishingStartedAt'), 'PERF-03b: payload has no fishingStartedAt');
}

async function main(): Promise<void> {
  console.log('verify-session-timer-broadcast');
  await testEnrichSessionFishingMsIncreases();
  await testEnrichRecoversMissingStartedAt();
  testSessionTimerLoopSource();
  testClientMergesSessionTimerTick();
  testBug13ZeroFlashGuards();
  testSharedEventType();
  console.log('\nAll session timer broadcast checks passed.');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
