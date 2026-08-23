/**
 * FEAT-GROUND-01：打窝会话逻辑（扣金、叠层、phase）。
 */
import {
  applyBiteMulGlobalCap,
  computeGroundbaitBuffs,
  getBiteMulGlobalCap,
  getGroundbaitDef,
  getGroundbaitMaxStack,
  type PondUser,
  type PondUserGroundbaitState,
} from '@fish-social/shared';
import { deductCoins, getPlayer } from './players.js';
import { ensurePlayerProgress } from './playerProgress.js';
import { recordFishingMetric } from './fishingMetrics.js';

export type GroundbaitRejectReason = 'gold' | 'cap' | 'state' | 'locked' | 'unknown';

export function clearGroundbait(user: PondUser): void {
  user.groundbait = null;
}

export function refreshGroundbaitExpiry(user: PondUser, nowMs: number = Date.now()): void {
  const gb = user.groundbait;
  if (!gb) return;
  if (gb.expiresAt > 0 && nowMs >= gb.expiresAt) {
    clearGroundbait(user);
    return;
  }
  if (gb.bitesLeft <= 0) {
    clearGroundbait(user);
  }
}

export function noteGroundbaitBite(user: PondUser): void {
  refreshGroundbaitExpiry(user);
  const gb = user.groundbait;
  if (!gb) return;
  gb.bitesLeft = Math.max(0, gb.bitesLeft - 1);
  if (gb.bitesLeft <= 0) clearGroundbait(user);
}

export function getActiveGroundbaitBiteBonus(user: PondUser): number {
  refreshGroundbaitExpiry(user);
  return user.groundbait?.biteBonus ?? 0;
}

export function getActiveGroundbaitSizeBonus(user: PondUser): number {
  refreshGroundbaitExpiry(user);
  return user.groundbait?.sizeBonus ?? 0;
}

/** 乘在 (1+bait) * rod 之后的窝咬钩乘区，再套全局 soft cap。 */
export function applyGroundbaitToBiteMul(baseMul: number, biteBonus: number): number {
  const withGb = baseMul * (1 + Math.max(0, biteBonus));
  return applyBiteMulGlobalCap(withGb, getBiteMulGlobalCap());
}

function buildBuffState(
  groundbaitId: string,
  stackCount: number,
  nowMs: number,
): PondUserGroundbaitState | null {
  const def = getGroundbaitDef(groundbaitId);
  if (!def) return null;
  const buffs = computeGroundbaitBuffs(def, stackCount);
  return {
    groundbaitId,
    stackCount,
    expiresAt: nowMs + Math.max(1, def.durationMin) * 60_000,
    bitesLeft: Math.max(1, Math.floor(def.maxBites)),
    biteBonus: buffs.biteBonus,
    sizeBonus: buffs.sizeBonus,
  };
}

/**
 * 打窝开始：校验 → 扣金 → 返回待进入 groundbaiting 的时长。
 * 叠层在 cast 完成后由 applyGroundbaitCastComplete 写入。
 */
export function tryStartGroundbaitCast(
  playerId: string,
  user: PondUser,
  groundbaitId: string,
):
  | { ok: true; castDurationMs: number; costGold: number; stackBefore: number }
  | { ok: false; error: string; code: string; reason: GroundbaitRejectReason } {
  if (!user.spotId || user.fishingPhase !== 'seated') {
    return {
      ok: false,
      error: '请先坐席后再打窝',
      code: 'NOT_SEATED',
      reason: 'state',
    };
  }

  refreshGroundbaitExpiry(user);
  const maxStack = getGroundbaitMaxStack();
  const current = user.groundbait;
  if (current && current.stackCount >= maxStack) {
    return {
      ok: false,
      error: '已达打窝上限',
      code: 'STACK_CAP',
      reason: 'cap',
    };
  }

  const def = getGroundbaitDef(groundbaitId);
  if (!def) {
    return {
      ok: false,
      error: '窝料不存在',
      code: 'LOCKED',
      reason: 'locked',
    };
  }

  const progress = ensurePlayerProgress(playerId);
  if (progress.level < def.unlockPlayerLevel) {
    return {
      ok: false,
      error: `需钓鱼等级 ${def.unlockPlayerLevel} 解锁该窝料`,
      code: 'LOCKED',
      reason: 'locked',
    };
  }

  const coins = getPlayer(playerId)?.coins ?? 0;
  if (coins < def.costGoldPerUse) {
    return {
      ok: false,
      error: '金币不足，无法继续打窝',
      code: 'INSUFFICIENT_GOLD',
      reason: 'gold',
    };
  }

  const paid = deductCoins(playerId, def.costGoldPerUse);
  if (!paid.ok) {
    return {
      ok: false,
      error: '金币不足，无法继续打窝',
      code: 'INSUFFICIENT_GOLD',
      reason: 'gold',
    };
  }

  const stackBefore = current?.stackCount ?? 0;
  // Stash pending cast id on phaseContext via temporary field on user
  (user as PondUser & { _pendingGroundbaitId?: string })._pendingGroundbaitId =
    groundbaitId;

  recordFishingMetric('groundbait_cast_started', {
    playerId,
    payload: {
      groundbaitId,
      costGold: def.costGoldPerUse,
      stackBefore,
    },
  });

  return {
    ok: true,
    castDurationMs: Math.max(500, Math.floor(def.castDurationMs)),
    costGold: def.costGoldPerUse,
    stackBefore,
  };
}

export function applyGroundbaitCastComplete(
  playerId: string,
  user: PondUser,
  pondId: string,
  nowMs: number = Date.now(),
): PondUserGroundbaitState | null {
  const pendingId =
    (user as PondUser & { _pendingGroundbaitId?: string })._pendingGroundbaitId ??
    user.groundbait?.groundbaitId;
  delete (user as PondUser & { _pendingGroundbaitId?: string })._pendingGroundbaitId;
  if (!pendingId) return user.groundbait ?? null;

  const def = getGroundbaitDef(pendingId);
  if (!def) return user.groundbait ?? null;

  const maxStack = getGroundbaitMaxStack();
  let stack = 1;
  const prev = user.groundbait;
  if (prev && prev.groundbaitId === pendingId) {
    stack = Math.min(maxStack, prev.stackCount + 1);
  } else {
    stack = 1; // 换种重置为 1
  }

  const next = buildBuffState(pendingId, stack, nowMs);
  user.groundbait = next;
  if (next) {
    recordFishingMetric('groundbait_applied', {
      playerId,
      pondId,
      payload: {
        groundbaitId: pendingId,
        stackAfter: next.stackCount,
        biteBonus: next.biteBonus,
        sizeBonus: next.sizeBonus,
      },
    });
  }
  return next;
}

export function recordGroundbaitRejected(
  playerId: string,
  pondId: string | undefined,
  reason: GroundbaitRejectReason,
  code?: string,
): void {
  recordFishingMetric('groundbait_rejected', {
    playerId,
    pondId,
    payload: { reason, code },
  });
}
