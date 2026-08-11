/**
 * FISH-DAILY-1 + BUG-15：上海日换日、脏内存纠正、写入封顶、坏锚点不写满日。
 * 运行: npm run verify:fish-daily-shanghai
 */
import { MAX_DAILY_FISHING_MS, type PondUser } from '@fish-social/shared';
import {
  addTodayFishingMs,
  ensureFishingDayRollover,
  enrichPondUser,
  flushFishingSessionToToday,
  getFishingMsForDate,
  getTodayFishingMs,
  safeFishingElapsedMs,
  sanitizeFishingStartedAt,
  settleFishingSession,
  setFishingClockForTests,
  shanghaiDayStartMs,
  todayKey,
} from '../server/src/pondUserManager.js';

function assert(cond: boolean, msg: string): void {
  if (!cond) throw new Error(`FAIL: ${msg}`);
  console.log(`  OK: ${msg}`);
}

function main(): void {
  console.log('verify-fish-daily-shanghai-rollover');

  // 2026-07-14 23:30 CST = 2026-07-14T15:30:00.000Z
  const beforeBoundary = Date.parse('2026-07-14T15:30:00.000Z');
  // 2026-07-15 00:30 CST
  const afterBoundary = Date.parse('2026-07-14T16:30:00.000Z');
  const boundary = shanghaiDayStartMs('2026-07-15');

  assert(todayKey(beforeBoundary) === '2026-07-14', 'todayKey before Shanghai midnight');
  assert(todayKey(afterBoundary) === '2026-07-15', 'todayKey after Shanghai midnight');
  assert(boundary === Date.parse('2026-07-14T16:00:00.000Z'), 'Shanghai day boundary ms');

  // UTC date differs from Shanghai around 00:00–08:00 CST
  const utcVsSha = Date.parse('2026-07-15T02:00:00.000Z'); // 10:00 CST Jul 15
  assert(new Date(utcVsSha).toISOString().slice(0, 10) === '2026-07-15', 'UTC date sample');
  const earlyUtcDay = Date.parse('2026-07-14T20:00:00.000Z'); // 04:00 CST Jul 15
  assert(new Date(earlyUtcDay).toISOString().slice(0, 10) === '2026-07-14', 'UTC still Jul14');
  assert(todayKey(earlyUtcDay) === '2026-07-15', 'Shanghai already Jul15 while UTC Jul14');

  const playerId = `verify-fish-daily-${Date.now()}`;
  setFishingClockForTests(() => beforeBoundary);

  // Seed nearly full day on Jul 14
  addTodayFishingMs(playerId, MAX_DAILY_FISHING_MS - 60_000, beforeBoundary);

  const user: PondUser = {
    id: 'u-fish-daily',
    playerId,
    nickname: '跨日钓友',
    color: '#000',
    spotId: 'calm-spot-3',
    status: 'fishing',
    fishingStartedAt: beforeBoundary - 30 * 60_000, // started 30m before snapshot
    todayFishingMs: MAX_DAILY_FISHING_MS - 60_000,
    fishingDayKey: '2026-07-14',
    fishingPhase: 'waiting',
    phaseEndsAt: beforeBoundary + 10_000,
  };

  const phaseBefore = user.fishingPhase;
  const spotBefore = user.spotId;

  setFishingClockForTests(() => afterBoundary);
  ensureFishingDayRollover(user, afterBoundary);

  assert(user.fishingPhase === phaseBefore, 'phase unchanged across rollover');
  assert(user.spotId === spotBefore, 'spot unchanged across rollover');
  assert(user.status === 'fishing', 'still fishing');
  assert(user.fishingDayKey === '2026-07-15', 'fishingDayKey jumped to new Shanghai day');
  assert(user.fishingStartedAt === boundary, 'fishingStartedAt re-anchored to day boundary');

  const oldMs = getFishingMsForDate(playerId, '2026-07-14');
  // 旧日：已接近满 + 跨日补记一段，封顶后 = MAX
  assert(oldMs === MAX_DAILY_FISHING_MS, `old day capped at MAX_DAILY (${oldMs})`);

  const enriched = enrichPondUser(user, afterBoundary);
  assert(
    enriched.todayFishingMs < 60 * 60_000,
    `new-day todayFishingMs near post-boundary only (${enriched.todayFishingMs})`,
  );
  const remain = MAX_DAILY_FISHING_MS - enriched.todayFishingMs;
  assert(remain > MAX_DAILY_FISHING_MS - 60 * 60_000, `remaining ~8h (${remain})`);

  // --- BUG-15：同日脏内存被 DB 纠正（未在钓）---
  console.log('\n=== BUG-15: idle dirty memory corrected from DB ===');
  const idleId = `verify-bug15-idle-${Date.now()}`;
  const idleAt = afterBoundary;
  setFishingClockForTests(() => idleAt);
  // DB 无今日行 → 已用 0
  assert(getTodayFishingMs(idleId, idleAt) === 0, 'DB today ms = 0');

  const idleUser: PondUser = {
    id: 'u-bug15-idle',
    playerId: idleId,
    nickname: '脏内存',
    color: '#111',
    spotId: null,
    status: 'idle',
    fishingStartedAt: null,
    todayFishingMs: MAX_DAILY_FISHING_MS, // 脏：内存已满
    fishingDayKey: todayKey(idleAt),
    fishingPhase: 'idle',
    phaseEndsAt: null,
  };

  const fixed = enrichPondUser(idleUser, idleAt);
  assert(idleUser.todayFishingMs === 0, 'memory todayFishingMs reset to DB 0');
  assert(fixed.todayFishingMs === 0, 'enrich todayFishingMs = 0');
  assert(
    MAX_DAILY_FISHING_MS - fixed.todayFishingMs === MAX_DAILY_FISHING_MS,
    'remaining ≈ 8h when DB empty',
  );

  // --- BUG-15：闲置跨日恢复额度 ---
  console.log('\n=== BUG-15: idle cross-day quota restore ===');
  const crossId = `verify-bug15-cross-${Date.now()}`;
  setFishingClockForTests(() => beforeBoundary);
  addTodayFishingMs(crossId, MAX_DAILY_FISHING_MS, beforeBoundary);

  const crossUser: PondUser = {
    id: 'u-bug15-cross',
    playerId: crossId,
    nickname: '跨日闲置',
    color: '#222',
    spotId: 'calm-spot-1',
    status: 'idle',
    fishingStartedAt: null,
    todayFishingMs: MAX_DAILY_FISHING_MS,
    fishingDayKey: '2026-07-14',
    fishingPhase: 'seated',
    phaseEndsAt: null,
  };

  setFishingClockForTests(() => afterBoundary);
  ensureFishingDayRollover(crossUser, afterBoundary);
  assert(crossUser.fishingDayKey === '2026-07-15', 'idle fishingDayKey rolled');
  assert(crossUser.todayFishingMs === 0, 'idle todayFishingMs reset for new day');
  assert(crossUser.fishingPhase === 'seated', 'idle phase unchanged');

  // --- BUG-15：写入封顶 ---
  console.log('\n=== BUG-15: addTodayFishingMs caps at MAX ===');
  const capId = `verify-bug15-cap-${Date.now()}`;
  setFishingClockForTests(() => afterBoundary);
  const once = addTodayFishingMs(capId, MAX_DAILY_FISHING_MS + 3_600_000, afterBoundary);
  assert(once === MAX_DAILY_FISHING_MS, `single add capped (${once})`);
  const twice = addTodayFishingMs(capId, 60_000, afterBoundary);
  assert(twice === MAX_DAILY_FISHING_MS, `further add stays at MAX (${twice})`);

  // absurd delta from epoch-like anchor
  const absurd = addTodayFishingMs(`verify-bug15-absurd-${Date.now()}`, afterBoundary, afterBoundary);
  assert(absurd <= MAX_DAILY_FISHING_MS, `absurd delta clamped (${absurd})`);

  // --- BUG-15 回归：坏锚点 / >8h 未入账段不得写满日 ---
  console.log('\n=== BUG-15 regression: bad anchor must not fill day ===');
  const badId = `verify-bug15-bad-anchor-${Date.now()}`;
  const flushAt = afterBoundary;
  setFishingClockForTests(() => flushAt);
  assert(getTodayFishingMs(badId, flushAt) === 0, 'bad-anchor DB starts empty');

  assert(safeFishingElapsedMs(flushAt - 9 * 60 * 60_000, flushAt) === 0, '9h span → safe elapsed 0');
  assert(safeFishingElapsedMs(1, flushAt) === 0, 'epoch-like startedAt → 0');
  assert(
    safeFishingElapsedMs(flushAt - 30 * 60_000, flushAt) === 30 * 60_000,
    '30m span credited as-is',
  );

  const badUser: PondUser = {
    id: 'u-bug15-bad',
    playerId: badId,
    nickname: '坏锚点',
    color: '#333',
    spotId: 'calm-spot-2',
    status: 'fishing',
    fishingStartedAt: flushAt - 9 * 60 * 60_000,
    todayFishingMs: 0,
    fishingDayKey: todayKey(flushAt),
    fishingPhase: 'waiting',
    phaseEndsAt: flushAt + 10_000,
  };
  flushFishingSessionToToday(badUser, flushAt);
  assert(getTodayFishingMs(badId, flushAt) === 0, 'flush with 9h anchor does not write DB');
  assert(badUser.fishingStartedAt === null, 'flush clears fishingStartedAt');

  const staleUser: PondUser = {
    id: 'u-bug15-stale',
    playerId: `verify-bug15-stale-${Date.now()}`,
    nickname: '过旧锚点',
    color: '#444',
    spotId: null,
    status: 'fishing',
    fishingStartedAt: flushAt - 25 * 60 * 60_000,
    todayFishingMs: 0,
    fishingDayKey: todayKey(flushAt),
    fishingPhase: 'waiting',
    phaseEndsAt: null,
  };
  sanitizeFishingStartedAt(staleUser, flushAt);
  assert(staleUser.fishingStartedAt === flushAt, 'absurd >24h anchor resets to now (not now-8h)');

  // --- BUG-16：断线结算 + 幂等 + 分段落账 ---
  console.log('\n=== BUG-16: disconnect settle + segment advance ===');
  const discId = `verify-bug16-disc-${Date.now()}`;
  const t0 = afterBoundary;
  setFishingClockForTests(() => t0);
  const discUser: PondUser = {
    id: 'u-bug16-disc',
    playerId: discId,
    nickname: '断线结算',
    color: '#555',
    spotId: 'calm-spot-1',
    status: 'fishing',
    fishingStartedAt: t0 - 5 * 60_000,
    todayFishingMs: 0,
    fishingDayKey: todayKey(t0),
    fishingPhase: 'waiting',
    phaseEndsAt: null,
  };
  const credited1 = settleFishingSession(discUser, t0, 'disconnect', { mode: 'finalize' });
  assert(credited1 === 5 * 60_000, `disconnect credits 5m (${credited1})`);
  assert(getTodayFishingMs(discId, t0) === 5 * 60_000, 'DB has 5m after disconnect settle');
  assert(discUser.fishingStartedAt === null, 'finalize clears anchor');

  const credited2 = settleFishingSession(discUser, t0, 'disconnect_timeout', { mode: 'finalize' });
  assert(credited2 === 0, 'second settle is idempotent (0)');
  assert(getTodayFishingMs(discId, t0) === 5 * 60_000, 'DB unchanged after double settle');

  const segId = `verify-bug16-seg-${Date.now()}`;
  const segStart = t0;
  setFishingClockForTests(() => segStart);
  const segUser: PondUser = {
    id: 'u-bug16-seg',
    playerId: segId,
    nickname: '分段落账',
    color: '#666',
    spotId: 'calm-spot-2',
    status: 'fishing',
    fishingStartedAt: segStart,
    todayFishingMs: 0,
    fishingDayKey: todayKey(segStart),
    fishingPhase: 'waiting',
    phaseEndsAt: null,
  };
  segUser.sessionStartedAt = segStart;
  const t1 = segStart + 30_000;
  setFishingClockForTests(() => t1);
  const seg1 = settleFishingSession(segUser, t1, 'segment_tick', { mode: 'checkpoint' });
  assert(seg1 === 30_000, `segment1 credits 30s (${seg1})`);
  assert(segUser.fishingStartedAt === segStart, 'BUG-19: checkpoint does not move fishingStartedAt');
  assert(segUser.sessionStartedAt === segStart, 'BUG-19: checkpoint does not move sessionStartedAt');
  assert(getTodayFishingMs(segId, t1) === 30_000, 'DB after segment1 = 30s');

  const t2 = t1 + 30_000;
  setFishingClockForTests(() => t2);
  const seg2 = settleFishingSession(segUser, t2, 'segment_tick', { mode: 'checkpoint' });
  assert(seg2 === 30_000, `segment2 credits 30s (${seg2})`);
  assert(segUser.sessionStartedAt === segStart, 'sessionStartedAt still original after 2 checkpoints');
  assert(getTodayFishingMs(segId, t2) === 60_000, 'DB after segment2 = 60s');

  // 模拟 >8h 未入账：checkpoint 前移内部点，展示锚点不变；下一段可继续记
  const longId = `verify-bug16-long-${Date.now()}`;
  const longAt = t2;
  const longSessionStart = longAt - 9 * 60 * 60_000;
  setFishingClockForTests(() => longAt);
  const longUser: PondUser = {
    id: 'u-bug16-long',
    playerId: longId,
    nickname: '长会话',
    color: '#777',
    spotId: 'calm-spot-3',
    status: 'fishing',
    fishingStartedAt: longSessionStart,
    sessionStartedAt: longSessionStart,
    todayFishingMs: 0,
    fishingDayKey: todayKey(longAt),
    fishingPhase: 'waiting',
    phaseEndsAt: null,
  };
  const long0 = settleFishingSession(longUser, longAt, 'segment_tick', { mode: 'checkpoint' });
  assert(long0 === 0, 'stale >8h segment credits 0');
  assert(longUser.sessionStartedAt === longSessionStart, 'BUG-19: display anchor unchanged after stale checkpoint');
  const longAt2 = longAt + 60_000;
  setFishingClockForTests(() => longAt2);
  const long1 = settleFishingSession(longUser, longAt2, 'segment_tick', { mode: 'checkpoint' });
  assert(long1 === 60_000, `after checkpoint, next minute credits (${long1})`);
  assert(getTodayFishingMs(longId, longAt2) === 60_000, 'long session recovers via segments');
  assert(longUser.sessionStartedAt === longSessionStart, 'display anchor still original');

  // BUG-19：enrich 展示 = base + uncredited，跨 checkpoint 不双计、不回满
  setFishingClockForTests(() => t2);
  const segEnriched = enrichPondUser(segUser, t2);
  assert(segEnriched.todayFishingBaseMs === 60_000, 'enrich base = DB 60s');
  assert(segEnriched.sessionStartedAt === segStart, 'enrich keeps sessionStartedAt');
  assert(
    segEnriched.todayFishingMs === 60_000,
    `enrich used = base+uncredited (=60s at checkpoint boundary), got ${segEnriched.todayFishingMs}`,
  );
  assert(
    (segEnriched.todayRemainingMs ?? 0) === 8 * 60 * 60_000 - 60_000,
    'enrich remaining = MAX - 60s',
  );

  setFishingClockForTests(null);
  console.log('\nPASS verify-fish-daily-shanghai-rollover (FISH-DAILY-1 + BUG-15 + BUG-16 + BUG-19)');
}

main();
