/**
 * BUG-14 / BUG-19：钓鱼中今日剩余随墙钟实时下降；开钓冻结 base，不被 session tick 对消。
 */
import assert from 'node:assert/strict';
import { MAX_DAILY_FISHING_MS, type PondUser } from '@fish-social/shared';
import {
  deriveTodayFishingBaseline,
  effectiveTodayUsedMs,
  remainingDailyFishingMs,
} from '../mobile/lib/fishingDuration.ts';

function user(partial: Partial<PondUser>): PondUser {
  return {
    id: 'u1',
    nickname: 't',
    color: '#000',
    status: 'fishing',
    spotId: 'calm-spot-1',
    fishingStartedAt: null,
    sessionStartedAt: null,
    todayFishingMs: 0,
    ...partial,
  };
}

const t0 = 1_000_000;

// 开钓冻结 base=1h；sessionFishingMs 即使被 tick 推高，剩余仍应按墙钟掉
{
  const u = user({
    todayFishingBaseMs: 3_600_000,
    todayFishingMs: 3_600_000,
    todayRemainingMs: MAX_DAILY_FISHING_MS - 3_600_000,
    sessionFishingMs: 5_000, // tick 已同步
    fishingPhase: 'waiting',
    fishingStartedAt: t0,
    sessionStartedAt: t0,
  });
  const baseline = deriveTodayFishingBaseline(u, t0 + 5_000);
  assert.equal(baseline, 3_600_000);
  const r0 = remainingDailyFishingMs(u, t0 + 5_000, baseline);
  const r1 = remainingDailyFishingMs(u, t0 + 6_000, baseline);
  assert.equal(r0 - r1, 1_000, '钓鱼中剩余应每秒下降 1s（不被 sessionFishingMs 对消）');
  assert.equal(effectiveTodayUsedMs(u, t0 + 5_000, baseline), 3_605_000);
}

// checkpoint 后 DB base 上涨，本地仍用冻结基线 + 全会话墙钟
{
  const baseline = 3_600_000; // 开钓时冻结
  const u = user({
    todayFishingBaseMs: 3_630_000, // checkpoint +30s
    todayFishingMs: 3_630_000,
    todayRemainingMs: MAX_DAILY_FISHING_MS - 3_630_000,
    sessionFishingMs: 30_000,
    fishingPhase: 'waiting',
    fishingStartedAt: t0,
    sessionStartedAt: t0,
  });
  const used = effectiveTodayUsedMs(u, t0 + 45_000, baseline);
  assert.equal(used, 3_645_000, '冻结基线+45s，不因 checkpoint 双计');
}

// 未钓鱼
{
  const u = user({
    status: 'idle',
    todayFishingMs: 1_000_000,
    todayFishingBaseMs: 1_000_000,
    todayRemainingMs: MAX_DAILY_FISHING_MS - 1_000_000,
    fishingPhase: 'seated',
    fishingStartedAt: null,
    sessionStartedAt: null,
  });
  assert.equal(effectiveTodayUsedMs(u, t0 + 99_000, null), 1_000_000);
  assert.equal(remainingDailyFishingMs(u, t0 + 99_000, null), MAX_DAILY_FISHING_MS - 1_000_000);
}

// 未选钓点：仅 join ack 的 base/remaining，不得误显满额 8h
{
  const used = 2_340_000;
  const u = user({
    status: 'idle',
    spotId: null,
    todayFishingMs: 0, // 旧快照可能缺已用
    todayFishingBaseMs: used,
    todayRemainingMs: MAX_DAILY_FISHING_MS - used,
    fishingPhase: 'idle',
    fishingStartedAt: null,
    sessionStartedAt: null,
  });
  assert.equal(effectiveTodayUsedMs(u, t0, null), used, '未选钓点应按 base 显示已用');
  assert.equal(
    remainingDailyFishingMs(u, t0, null),
    MAX_DAILY_FISHING_MS - used,
    '未选钓点剩余不得回满 8h',
  );
}

console.log('verify-bug14-daily-remaining: OK');
