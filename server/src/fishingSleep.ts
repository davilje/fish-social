import { isFishingActive, type PondUser } from '@fish-social/shared';

const attentionAtByUser = new WeakMap<PondUser, number>();

export function getFishingAttentionAt(user: PondUser): number | null {
  return attentionAtByUser.get(user) ?? null;
}

export function restoreFishingSleep(
  user: PondUser,
  asleep: boolean,
  sleepAt: number | null,
  attentionAt: number | null,
): void {
  user.fishingAsleep = asleep;
  user.fishingSleepAt = sleepAt;
  if (attentionAt != null) attentionAtByUser.set(user, attentionAt);
  else attentionAtByUser.delete(user);
}

export function clearFishingSleep(user: PondUser): void {
  restoreFishingSleep(user, false, null, null);
}

export function resetFishingSleep(user: PondUser, timeoutMs: number, atMs = Date.now()): void {
  if (user.isBot) {
    clearFishingSleep(user);
    return;
  }
  restoreFishingSleep(user, false, timeoutMs > 0 ? atMs + timeoutMs : null, atMs);
}

export function refreshFishingSleep(user: PondUser, timeoutMs: number, atMs = Date.now()): boolean {
  if (user.isBot || user.fishingAsleep || !isFishingActive(user.fishingPhase) ||
      user.fishingPhase === 'stopping') return false;
  const previousDeadline = user.fishingSleepAt;
  let attentionAt = attentionAtByUser.get(user);
  if (attentionAt == null) {
    attentionAt = user.sessionStartedAt ?? user.fishingStartedAt ?? atMs;
    attentionAtByUser.set(user, attentionAt);
  }
  user.fishingSleepAt = timeoutMs > 0 ? attentionAt + timeoutMs : null;
  if (user.fishingPhase === 'waiting' && user.fishingSleepAt != null &&
      atMs >= user.fishingSleepAt) {
    user.fishingAsleep = true;
    return true;
  }
  return previousDeadline !== user.fishingSleepAt;
}
