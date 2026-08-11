import { isFishingActive, type PondUser } from '@fish-social/shared';

/** 钓鱼活跃且锚点缺失时补齐，避免 sessionFishingMs 卡死 */
export function ensureFishingStartedAt(user: PondUser): void {
  if (isFishingActive(user.fishingPhase) && user.fishingStartedAt == null) {
    user.fishingStartedAt = Date.now();
  }
}
