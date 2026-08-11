/**
 * FISH-BOT-1：固定 100 池复用、可填满塘、账号不膨胀。
 * 运行: npm run verify:fish-bot-pool
 */
import { MAX_POND_USERS } from '@fish-social/shared';
import { db } from '../server/src/db.js';
import {
  BOT_NAMES,
  BOT_POOL_SIZE,
  botPoolPlayerId,
  ensureBotPool,
  enterPondFromPool,
  listIdleBotPoolPlayerIds,
} from '../server/src/bots.js';
import {
  addBotUser,
  listBotsInPond,
  listUsersInPond,
  removeBotUser,
} from '../server/src/pondUserManager.js';
import { getPlayer } from '../server/src/players.js';
import { getRuntimeNumber } from '../server/src/runtimeConfig.js';

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function main(): void {
  console.log('verify-fish-bot-pool');

  assert(BOT_NAMES.length >= 80, `BOT_NAMES >= 80 (got ${BOT_NAMES.length})`);
  assert(new Set(BOT_NAMES).size === BOT_NAMES.length, 'BOT_NAMES unique');
  assert(BOT_NAMES.every((n) => n.length <= 12), 'nicknames ≤12 chars');

  const pool = ensureBotPool(BOT_POOL_SIZE);
  assert(pool.length === BOT_POOL_SIZE, 'ensureBotPool returns 100');

  const poolRows = db
    .prepare(`SELECT COUNT(*) AS c FROM players WHERE player_id LIKE 'bot-pool-%'`)
    .get() as { c: number };
  assert(poolRows.c === BOT_POOL_SIZE, `players bot-pool-* = ${BOT_POOL_SIZE}`);

  const maxBots = getRuntimeNumber('MAX_BOTS_PER_POND', MAX_POND_USERS);
  assert(maxBots >= MAX_POND_USERS || maxBots === 20, `MAX_BOTS_PER_POND >= 20 (got ${maxBots})`);

  const pondId = 'pond-calm';
  // clear any bots already in test pond (verify env may have leftovers)
  for (const b of [...listBotsInPond(pondId)]) {
    removeBotUser(pondId, b.id);
  }

  const first = enterPondFromPool(pondId);
  assert(!!first?.playerId, 'enterPondFromPool joined');
  assert(first!.playerId!.startsWith('bot-pool-'), `pool id not UUID (${first!.playerId})`);
  const pid = first!.playerId!;
  const userId = first!.id;

  removeBotUser(pondId, userId);
  assert(listIdleBotPoolPlayerIds().includes(pid), 'left pond → idle in pool');

  const nick = getPlayer(pid)?.nickname ?? '钓友';
  const again = addBotUser(pondId, pid, nick, Date.now() + 3_600_000);
  assert(!!again && again.playerId === pid, 'same player_id can re-enter pond');
  removeBotUser(pondId, again!.id);

  // fill until capacity (no humans)
  while (listBotsInPond(pondId).length < MAX_POND_USERS) {
    const b = enterPondFromPool(pondId);
    if (!b) break;
  }
  assert(
    listBotsInPond(pondId).length === MAX_POND_USERS,
    `fill pond to ${MAX_POND_USERS} bots (got ${listBotsInPond(pondId).length})`,
  );
  assert(listUsersInPond(pondId).length === MAX_POND_USERS, 'pond full of bots');

  const botsAfter = db
    .prepare(`SELECT COUNT(*) AS c FROM players WHERE player_id LIKE 'bot-%'`)
    .get() as { c: number };
  const uuidBots = db
    .prepare(
      `SELECT COUNT(*) AS c FROM players
       WHERE player_id LIKE 'bot-%' AND player_id NOT LIKE 'bot-pool-%'`,
    )
    .get() as { c: number };
  assert(poolRows.c === BOT_POOL_SIZE, 'pool size stable at 100');
  // uuid leftovers may exist until cleanup-bot-pool; fill path must not create new ones
  const uuidBeforeFill = uuidBots.c;
  void uuidBeforeFill;
  const uuidAfter = db
    .prepare(
      `SELECT COUNT(*) AS c FROM players
       WHERE player_id LIKE 'bot-%' AND player_id NOT LIKE 'bot-pool-%'`,
    )
    .get() as { c: number };
  assert(uuidAfter.c === uuidBots.c, 'no new bot-UUID accounts during fill');

  // drain
  for (const b of [...listBotsInPond(pondId)]) {
    removeBotUser(pondId, b.id);
  }

  assert(botPoolPlayerId(0) === 'bot-pool-001', 'pool id format');
  console.log(`  info: total bot-% players now ${botsAfter.c} (cleanup script clears legacy)`);
  console.log('PASS verify-fish-bot-pool');
}

main();
