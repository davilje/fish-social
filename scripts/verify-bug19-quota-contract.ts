/**
 * BUG-19：额度单一口径契约。
 * 运行: npx tsx scripts/verify-bug19-quota-contract.ts
 */
import { MAX_DAILY_FISHING_MS, type PondUser } from '@fish-social/shared';
import {
  enrichPondUser,
  getQuotaCheckpointAt,
  getTodayFishingMs,
  settleFishingSession,
  setFishingClockForTests,
  todayKey,
} from '../server/src/pondUserManager.js';
import {
  deriveTodayFishingBaseline,
  effectiveTodayUsedMs,
  remainingDailyFishingMs,
} from '../mobile/lib/fishingDuration.ts';
import { readFileSync } from 'fs';
import { join } from 'path';

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function main(): void {
  console.log('verify-bug19-quota-contract');

  const t0 = Date.parse('2026-08-10T04:00:00.000Z');
  const playerId = `verify-bug19-${Date.now()}`;
  setFishingClockForTests(() => t0);

  const user: PondUser = {
    id: 'u-bug19',
    playerId,
    nickname: '单一口径',
    color: '#123',
    spotId: 'spot-1',
    status: 'fishing',
    fishingStartedAt: t0,
    sessionStartedAt: t0,
    todayFishingMs: 0,
    todayFishingBaseMs: 0,
    fishingDayKey: todayKey(t0),
    fishingPhase: 'waiting',
    phaseEndsAt: null,
  };

  const t30 = t0 + 30_000;
  setFishingClockForTests(() => t30);
  const c1 = settleFishingSession(user, t30, 'segment_tick', { mode: 'checkpoint' });
  assert(c1 === 30_000, 'checkpoint credits 30s');
  assert(user.sessionStartedAt === t0, 'sessionStartedAt unchanged');
  assert(user.fishingStartedAt === t0, 'fishingStartedAt unchanged');
  assert(getQuotaCheckpointAt(user.id) === t30, 'internal checkpoint advanced');
  assert(getTodayFishingMs(playerId, t30) === 30_000, 'DB base=30s');

  const enriched = enrichPondUser(user, t30);
  assert(enriched.todayFishingBaseMs === 30_000, 'enrich base=30s');
  assert(enriched.todayFishingMs === 30_000, 'enrich used at boundary=30s (no double count)');
  assert(enriched.todayRemainingMs === MAX_DAILY_FISHING_MS - 30_000, 'enrich remaining');

  const t45 = t0 + 45_000;
  setFishingClockForTests(() => t45);
  const mid = enrichPondUser(user, t45);
  assert(mid.todayFishingBaseMs === 30_000, 'mid base still 30s');
  assert(mid.todayFishingMs === 45_000, 'mid used = base + 15s uncredited');
  assert(mid.sessionStartedAt === t0, 'mid session anchor stable');

  // Client：开钓冻结 base=0，墙钟插值；即便 mid.base 已因 checkpoint 变成 30s 也不双计
  const frozenBase = 0;
  const clientRemaining = remainingDailyFishingMs(mid, t45, frozenBase);
  assert(
    Math.abs(clientRemaining - (MAX_DAILY_FISHING_MS - 45_000)) < 2,
    `client remaining ≈ MAX-45s (got ${clientRemaining})`,
  );
  const clientUsed = effectiveTodayUsedMs(mid, t45, frozenBase);
  assert(Math.abs(clientUsed - 45_000) < 2, `client used ≈ 45s (got ${clientUsed})`);
  // sessionFishingMs 与墙钟对齐时仍应继续走秒（不被 tick 对消）
  const tickSynced = { ...mid, sessionFishingMs: 45_000 };
  const rTick = remainingDailyFishingMs(tickSynced, t45 + 1_000, frozenBase);
  assert(
    Math.abs(rTick - (MAX_DAILY_FISHING_MS - 46_000)) < 2,
    'client still ticks after sessionFishingMs sync',
  );
  assert(deriveTodayFishingBaseline(mid) === 30_000, 'derive reads current base for new session only');

  const finalized = settleFishingSession(user, t45, 'stop_fishing', { mode: 'finalize' });
  assert(finalized === 15_000, 'finalize credits remaining 15s');
  assert(user.sessionStartedAt == null, 'finalize clears sessionStartedAt');
  assert(user.fishingStartedAt == null, 'finalize clears fishingStartedAt');
  assert(getTodayFishingMs(playerId, t45) === 45_000, 'DB after stop = 45s');
  const idle = enrichPondUser(user, t45);
  assert(idle.todayFishingBaseMs === 45_000, 'idle base=45s');
  assert(idle.todayFishingMs === 45_000, 'idle used=base');
  assert(idle.todayRemainingMs === MAX_DAILY_FISHING_MS - 45_000, 'idle remaining');

  const again = settleFishingSession(user, t45 + 1000, 'stop_repeat', { mode: 'finalize' });
  assert(again === 0, 'repeat finalize is idempotent');
  assert(getTodayFishingMs(playerId, t45) === 45_000, 'DB unchanged after repeat');

  // Dirty full-day row detection (ops note: do not auto-wipe)
  const src = readFileSync(
    join(process.cwd(), 'server/src/pondUserManager.ts'),
    'utf8',
  );
  assert(src.includes('quotaCheckpointAtByUser'), 'internal checkpoint map present');
  assert(src.includes("mode: 'checkpoint'") || src.includes("'checkpoint'"), 'checkpoint mode present');
  assert(src.includes('NEVER clamp to atMs−8h') || src.includes('never clamp'), 'keeps no now-8h credit policy');

  const clientSrc = readFileSync(join(process.cwd(), 'mobile/lib/fishingDuration.ts'), 'utf8');
  assert(!clientSrc.includes('stored - elapsed'), 'client no reverse baseline');
  assert(clientSrc.includes('todayFishingBaseMs') || clientSrc.includes('todayRemainingMs'), 'client reads base/remaining');

  const socketSrc = readFileSync(join(process.cwd(), 'mobile/lib/usePondSocket.ts'), 'utf8');
  assert(socketSrc.includes('applyQuotaSeedToUsers'), 'client seeds quota on join ack');
  const handlerSrc = readFileSync(join(process.cwd(), 'server/src/socketPondHandlers.ts'), 'utf8');
  assert(
    handlerSrc.includes('todayFishingBaseMs') && handlerSrc.includes('todayRemainingMs'),
    'join_pond ack returns base/remaining',
  );

  setFishingClockForTests(null);
  console.log('\nPASS verify-bug19-quota-contract');
}

main();
