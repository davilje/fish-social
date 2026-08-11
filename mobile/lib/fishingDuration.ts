import {
  MAX_DAILY_FISHING_MS,
  isFishingActive,
  type PondUser,
} from '@fish-social/shared';

function clampUsed(ms: number): number {
  return Math.min(MAX_DAILY_FISHING_MS, Math.max(0, ms));
}

export function sessionAnchor(user: PondUser): number | null {
  return user.sessionStartedAt ?? user.fishingStartedAt ?? null;
}

function isQuotaFishing(user: PondUser): boolean {
  return (
    isFishingActive(user.fishingPhase) &&
    user.fishingPhase !== 'stopping' &&
    sessionAnchor(user) != null
  );
}

/**
 * 开钓时冻结的今日已落账基线（不含本局墙钟）。
 * BUG-19：必须用 todayFishingBaseMs；禁止用 todayFishingMs - elapsed 反推。
 * checkpoint 后 DB base 会上涨，调用方应保持冻结值，勿随 enrich 上调。
 */
export function deriveTodayFishingBaseline(user: PondUser, _now: number = Date.now()): number {
  if (typeof user.todayFishingBaseMs === 'number') {
    return clampUsed(user.todayFishingBaseMs);
  }
  // 兼容旧快照：未带 base 时，仅在非钓鱼用 todayFishingMs
  if (!isQuotaFishing(user)) {
    return clampUsed(user.todayFishingMs ?? 0);
  }
  return clampUsed(user.todayFishingMs ?? 0);
}

/**
 * 展示用今日已用：
 * - 钓鱼中：开钓冻结基线 + (now - sessionStartedAt)
 * - 否则：todayFishingBaseMs / todayFishingMs
 *
 * 不用 sessionFishingMs 做差（会被 session_timer_tick 对消，导致剩余不走）。
 */
export function effectiveTodayUsedMs(
  user: PondUser | undefined | null,
  now: number,
  todayBaseline: number | null = null,
): number {
  if (!user) return 0;

  if (isQuotaFishing(user)) {
    const startedAt = sessionAnchor(user)!;
    const baseline =
      todayBaseline != null ? todayBaseline : deriveTodayFishingBaseline(user, now);
    const elapsed = Math.max(0, now - startedAt);
    return clampUsed(baseline + elapsed);
  }

  // 闲置 / 未选钓点：取各字段中最大的「已用」，避免某一字段为 0 时误显满额 8h
  const candidates: number[] = [];
  if (typeof user.todayFishingBaseMs === 'number') candidates.push(user.todayFishingBaseMs);
  if (typeof user.todayFishingMs === 'number') candidates.push(user.todayFishingMs);
  if (typeof user.todayRemainingMs === 'number') {
    candidates.push(MAX_DAILY_FISHING_MS - user.todayRemainingMs);
  }
  if (candidates.length === 0) return 0;
  return clampUsed(Math.max(...candidates));
}

export function remainingDailyFishingMs(
  user: PondUser | undefined | null,
  now: number,
  todayBaseline: number | null = null,
): number {
  return Math.max(0, MAX_DAILY_FISHING_MS - effectiveTodayUsedMs(user, now, todayBaseline));
}
