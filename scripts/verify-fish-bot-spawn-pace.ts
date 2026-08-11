/**
 * FISH-BOT-2：启动 3～6、时长回拨、Steady 单次最多 +1、可慢补满塘。
 * 运行: npm run verify:fish-bot-spawn-pace
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { MAX_POND_USERS, PONDS } from '@fish-social/shared';
import '../server/src/db.js';
import {
  BOT_POOL_SIZE,
  bootstrapBots,
  ensureBotPool,
  tickSpawn,
} from '../server/src/bots.js';
import {
  computeSessionFishingMs,
  enrichPondUser,
  listBotsInPond,
  removeBotUser,
  startBotFishing,
} from '../server/src/pondUserManager.js';
import { initBotFishingPhase } from '../server/src/fishingStateMachine.js';
import { refreshRuntimeFromDb, setRuntimeNumber } from '../server/src/runtimeConfig.js';

const rootDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function mockIo() {
  return {
    to() {
      return { emit() {} };
    },
  } as unknown as import('socket.io').Server;
}

function clearAllBots(): void {
  for (const pond of PONDS) {
    for (const b of [...listBotsInPond(pond.id)]) {
      removeBotUser(pond.id, b.id);
    }
  }
}

function main(): void {
  console.log('verify-fish-bot-spawn-pace');
  ensureBotPool(BOT_POOL_SIZE);
  refreshRuntimeFromDb();

  // --- source guards ---
  console.log('\n=== TC: source guards ===');
  const botsSrc = fs.readFileSync(path.join(rootDir, 'server/src/bots.ts'), 'utf8');
  assert(botsSrc.includes('export function bootstrapBots'), 'bootstrapBots exported');
  assert(botsSrc.includes('export function tickSpawn'), 'tickSpawn exported');
  assert(!/function tickSpawn[\s\S]*?while\s*\(true\)/.test(botsSrc), 'tickSpawn has no while(true) fill');
  assert(botsSrc.includes('bootstrapBots(io)'), 'startBotLoop calls bootstrapBots');
  assert(!botsSrc.includes('BOT_SOFT_CAP'), 'no BOT_SOFT_CAP');

  const mgrSrc = fs.readFileSync(path.join(rootDir, 'server/src/pondUserManager.ts'), 'utf8');
  assert(mgrSrc.includes('elapsedMs'), 'startBotFishing supports elapsedMs');
  assert(mgrSrc.includes('now - elapsed'), 'backdates fishingStartedAt');

  // --- elapsedMs backdate ---
  console.log('\n=== TC: startBotFishing elapsedMs ===');
  clearAllBots();
  const io = mockIo();
  setRuntimeNumber('BOT_SPAWN_CHANCE', 0);
  // put one bot via bootstrap with forced config
  setRuntimeNumber('BOT_BOOT_MIN', 1);
  setRuntimeNumber('BOT_BOOT_MAX', 1);
  setRuntimeNumber('BOT_BOOT_FISHING_RATIO', 0);
  bootstrapBots(io);
  const pondId = 'pond-calm';
  const idleBot = listBotsInPond(pondId)[0];
  assert(!!idleBot, 'boot placed one idle bot');
  const elapsed = 30 * 60 * 1000;
  const started = startBotFishing(pondId, idleBot!.id, undefined, { elapsedMs: elapsed });
  assert(started.ok, 'startBotFishing with elapsed');
  initBotFishingPhase(started.user!);
  const ms = computeSessionFishingMs(started.user!);
  assert(ms >= elapsed - 2_000 && ms <= elapsed + 2_000, `session ~${elapsed}ms (got ${ms})`);
  const enriched = enrichPondUser(started.user!);
  assert(
    (enriched.sessionFishingMs ?? 0) >= elapsed - 2_000,
    'enrich preserves backdated session',
  );

  // --- boot count range ---
  console.log('\n=== TC: bootstrap count in [3,6] ===');
  clearAllBots();
  setRuntimeNumber('BOT_BOOT_MIN', 3);
  setRuntimeNumber('BOT_BOOT_MAX', 6);
  setRuntimeNumber('BOT_BOOT_FISHING_RATIO', 1);
  setRuntimeNumber('BOT_BOOT_ELAPSED_MIN_MS', 5 * 60 * 1000);
  setRuntimeNumber('BOT_BOOT_ELAPSED_MAX_MS', 75 * 60 * 1000);
  bootstrapBots(io);

  for (const pond of PONDS) {
    const n = listBotsInPond(pond.id).length;
    assert(n >= 3 && n <= 6, `${pond.id} boot bots in [3,6] (got ${n})`);
    assert(n < 15, `${pond.id} not near-full at boot (got ${n})`);
  }

  const fishing = listBotsInPond(pondId).filter((b) => b.status === 'fishing');
  assert(fishing.length >= 1, 'boot has fishing bots when ratio=1');
  const sessions = fishing.map((b) => computeSessionFishingMs(b));
  const uniqueBuckets = new Set(sessions.map((ms) => Math.floor(ms / 60_000)));
  assert(uniqueBuckets.size >= 1, 'fishing bots have session duration');
  if (fishing.length >= 2) {
    const minS = Math.min(...sessions);
    const maxS = Math.max(...sessions);
    assert(maxS - minS > 1_000 || fishing.length === 1, 'elapsed values not all identical (or single fisher)');
  }
  for (const s of sessions) {
    assert(s >= 4 * 60 * 1000 && s <= 76 * 60 * 1000, `elapsed in ~5–75min (got ${s})`);
  }

  // --- steady: at most +1 per tick ---
  console.log('\n=== TC: tickSpawn at most +1 ===');
  const before = listBotsInPond(pondId).length;
  setRuntimeNumber('BOT_SPAWN_CHANCE', 1);
  setRuntimeNumber('BOT_JOIN_FISHING_CHANCE', 0);
  tickSpawn(io);
  const afterOne = listBotsInPond(pondId).length;
  assert(afterOne === before + 1, `one tick +1 (before=${before}, after=${afterOne})`);

  setRuntimeNumber('BOT_SPAWN_CHANCE', 0);
  tickSpawn(io);
  assert(listBotsInPond(pondId).length === afterOne, 'chance=0 adds nobody');

  // --- can eventually fill ---
  console.log('\n=== TC: can fill to MAX_BOTS_PER_POND ===');
  setRuntimeNumber('BOT_SPAWN_CHANCE', 1);
  setRuntimeNumber('MAX_BOTS_PER_POND', MAX_POND_USERS);
  let guard = 0;
  while (listBotsInPond(pondId).length < MAX_POND_USERS && guard < 40) {
    tickSpawn(io);
    guard += 1;
  }
  assert(
    listBotsInPond(pondId).length === MAX_POND_USERS,
    `slow fill to ${MAX_POND_USERS} (got ${listBotsInPond(pondId).length}, ticks=${guard})`,
  );

  clearAllBots();
  console.log('\nPASS verify-fish-bot-spawn-pace');
}

main();
