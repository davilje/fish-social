import type { Express, Request, Response } from 'express';
import type { Server } from 'socket.io';
import {
  ADMISSION_FEE_SLICE_MS,
  FISH_SPECIES,
  calcHookDurationMs,
  getGamePondDef,
  getMaxPlayerLevel,
  getPlayerLevelDef,
  getQualityMaxSize,
  getSpecies,
  getQualityInfo,
  isFishingActive,
  isNearMaxSize,
  type AchievementDef,
  type ClientToServerEvents,
  type CodexUnlockPayload,
  type FishInventoryItem,
  type FishQuality,
  type FishSpeciesId,
  type PondFishEntity,
  type ServerToClientEvents,
} from '@fish-social/shared';
import { requireAuth, resolveAuthedPlayerId } from './auth.js';
import { addAlbumCandidate } from './album.js';
import { tryUnlockAchievements } from './achievements.js';
import { isCodexNewForPlayer, recordCodexCatch } from './codex.js';
import { recordFishingMetric } from './fishingMetrics.js';
import { buildFishingDebugReport } from './fishingDebug.js';
import {
  findLivePondUser,
  forceTriggerPoliceRaid,
} from './forbiddenPolice.js';
import { scheduleHookFromBite, flushHookedPhaseToPendingCatch, flushResolvingPhase } from './fishingStateMachine.js';
import { settleAcceptedCatch } from './catchSettlement.js';
import {
  addFishToInventory,
  getInventory,
  getPendingCatch,
} from './inventory.js';
import { addCoins, getPlayer } from './players.js';
import {
  applyAdmissionFeeProgress,
  ensurePlayerProgress,
  getPondProficiency,
  getProgressPublicView,
  setPlayerLevelForDebug,
  setPondProficiencyLevelForDebug,
} from './playerProgress.js';
import {
  clearTodayFishingMsRecord,
  debugResetTodayFishingDuration,
  emitPondUserUpdated,
  requestFeeStop,
} from './pondUserManager.js';
import {
  getPondFishById,
  listPondFishAtSpot,
  listPondFishEntities,
  spawnDebugHookFish,
} from './pondEcology.js';
import { getHookDurationScale, isInstantFishingTestMode } from './runtimeConfig.js';
import { resolveSocketByPlayer } from './sessionRegistry.js';

export const GAMEPLAY_DEBUG_ACTIONS = [
  'level_up',
  'level_max',
  'pond_level_up',
  'pond_level_max',
  'add_gold',
  'police_raid',
  'grant_fish',
  'grant_fish_max_size',
  'grant_fish_epic_plus',
  'advance_fee_2h',
  'reset_fishing_duration',
] as const;

export type GameplayDebugAction = (typeof GAMEPLAY_DEBUG_ACTIONS)[number];

export const GAMEPLAY_DEBUG_GOLD = 1_000_000;
export const GAMEPLAY_DEBUG_FISH = {
  speciesId: 'crucian' as const,
  quality: 'gray' as const,
  sizeM: 0.18,
};

const POND_TABLE_MAX = 10;

/** 最近一次发放鱼获的副作用（供路由推 Socket） */
let lastFishSideEffects: {
  playerId: string;
  codex: CodexUnlockPayload | null;
  achievements: AchievementDef[];
} | null = null;

export function isGameplayDebugEnabled(): boolean {
  return process.env.NODE_ENV !== 'production' || process.env.GAMEPLAY_DEBUG === '1';
}

function pondCapForPlayer(playerLevel: number): number {
  const def = getPlayerLevelDef(playerLevel);
  const lock = def?.maxPondLevel ?? 1;
  return Math.max(1, Math.min(POND_TABLE_MAX, lock));
}

function isKnownAction(value: string): value is GameplayDebugAction {
  return (GAMEPLAY_DEBUG_ACTIONS as readonly string[]).includes(value);
}

export type GameplayDebugResult =
  | {
      ok: true;
      message: string;
      action:
        | GameplayDebugAction
        | 'force_bite'
        | 'force_bite_instant'
        | 'instant_catch'
        | 'complete_catch'
        | 'catch_fish'
        | 'catch_quality_purple'
        | 'catch_quality_red'
        | 'catch_quality_orange'
        | 'catch_quality_gold';
      pondId?: string;
      autoReturned?: boolean;
      gold?: number;
    }
  | { ok: false; error: string; action?: string; pondId?: string };

/** 与正式领鱼一致：图鉴 + 相册自动入墙 + 成就 */
function finalizeDebugFishGrant(
  playerId: string,
  item: FishInventoryItem,
  pondId: string | undefined,
  label: string,
): { message: string; pondId?: string } {
  const speciesId = item.speciesId as FishSpeciesId;
  const wasNewCodex = isCodexNewForPlayer(playerId, speciesId);
  const codex = recordCodexCatch(playerId, speciesId, item.sizeM);
  addAlbumCandidate({
    playerId,
    speciesId: item.speciesId,
    quality: item.quality,
    sizeM: item.sizeM,
    pondId: pondId ?? null,
    source: wasNewCodex ? 'first_codex' : 'catch',
    inventoryItemId: item.id,
  });
  const achievements = tryUnlockAchievements(playerId);
  lastFishSideEffects = { playerId, codex, achievements };
  return { pondId, message: label };
}

function pickSpeciesForQuality(quality: FishQuality) {
  const candidates = FISH_SPECIES.filter(
    (s) => (s.qualityAffinity?.[quality] ?? 0) > 0,
  );
  if (candidates.length === 0) return FISH_SPECIES[FISH_SPECIES.length - 1];
  return candidates[Math.floor(Math.random() * candidates.length)];
}

export function executeGameplayDebugAction(
  playerId: string,
  action: string,
): GameplayDebugResult {
  lastFishSideEffects = null;
  if (!isKnownAction(action)) {
    return { ok: false, error: '未知 Debug 动作', action };
  }

  const live = findLivePondUser(playerId);
  const pondId = live?.pondId;
  const maxPlayer = getMaxPlayerLevel();

  if (action === 'level_up') {
    const progress = ensurePlayerProgress(playerId);
    if (progress.level >= maxPlayer) {
      return { ok: false, error: `钓鱼等级已满（Lv${maxPlayer}）`, action };
    }
    const next = setPlayerLevelForDebug(playerId, progress.level + 1);
    return { ok: true, action, message: `钓鱼等级升至 ${next.level}` };
  }

  if (action === 'level_max') {
    const progress = ensurePlayerProgress(playerId);
    if (progress.level >= maxPlayer) {
      return { ok: false, error: `钓鱼等级已满（Lv${maxPlayer}）`, action };
    }
    setPlayerLevelForDebug(playerId, maxPlayer);
    return { ok: true, action, message: `钓鱼等级已设为满级 ${maxPlayer}` };
  }

  if (action === 'pond_level_up' || action === 'pond_level_max') {
    if (!pondId) return { ok: false, error: '当前不在鱼塘', action };
    const progress = ensurePlayerProgress(playerId);
    const cap = pondCapForPlayer(progress.level);
    const row = getPondProficiency(playerId, pondId);
    if (action === 'pond_level_up') {
      if (row.level >= POND_TABLE_MAX) {
        return { ok: false, error: '当前塘熟练度已满级（10）', action, pondId };
      }
      if (row.level >= cap) {
        return {
          ok: false,
          error: `当前玩家等级锁顶塘熟练度为 ${cap}，无法再升`,
          action,
          pondId,
        };
      }
      const next = setPondProficiencyLevelForDebug(playerId, pondId, row.level + 1);
      const locked = next.level >= cap && cap < POND_TABLE_MAX;
      return {
        ok: true,
        action,
        pondId,
        message: locked
          ? `当前塘熟练度升至 ${next.level}（已达玩家锁顶）`
          : `当前塘熟练度升至 ${next.level}`,
      };
    }

    if (row.level >= cap) {
      return {
        ok: false,
        error:
          cap < POND_TABLE_MAX
            ? `当前塘熟练度已达玩家锁顶 ${cap}`
            : '当前塘熟练度已满级（10）',
        action,
        pondId,
      };
    }
    setPondProficiencyLevelForDebug(playerId, pondId, cap);
    return {
      ok: true,
      action,
      pondId,
      message:
        cap < POND_TABLE_MAX
          ? `当前塘熟练度已提到锁顶 ${cap}`
          : `当前塘熟练度已满级（10）`,
    };
  }

  if (action === 'add_gold') {
    const coins = addCoins(playerId, GAMEPLAY_DEBUG_GOLD);
    if (coins <= 0 && !getPlayer(playerId)) {
      return { ok: false, error: '玩家档案不存在', action };
    }
    return {
      ok: true,
      action,
      message: `金币 +${GAMEPLAY_DEBUG_GOLD}，当前 ${coins}`,
    };
  }

  if (action === 'police_raid') {
    const result = forceTriggerPoliceRaid(playerId);
    if (!result.ok) return { ...result, action, pondId };
    return { ok: true, action, pondId, message: result.message };
  }

  if (action === 'grant_fish') {
    const item = addFishToInventory(
      playerId,
      {
        speciesId: GAMEPLAY_DEBUG_FISH.speciesId,
        quality: GAMEPLAY_DEBUG_FISH.quality,
        sizeM: GAMEPLAY_DEBUG_FISH.sizeM,
        caughtAt: Date.now(),
        pondId: pondId ?? 'pond-calm',
      },
      { pondId: pondId ?? 'pond-calm' },
    );
    const grant = finalizeDebugFishGrant(
      playerId,
      item,
      pondId ?? 'pond-calm',
      `已发放调试鱼获：鲫鱼·灰·${GAMEPLAY_DEBUG_FISH.sizeM}m（id=${item.id}）`,
    );
    return { ok: true, action, pondId: grant.pondId, message: grant.message };
  }

  if (action === 'grant_fish_max_size') {
    const quality: FishQuality = 'gold';
    const species = pickSpeciesForQuality(quality);
    const sizeM = getQualityMaxSize(quality, getSpecies(species.id));
    const item = addFishToInventory(
      playerId,
      {
        speciesId: species.id,
        quality,
        sizeM,
        caughtAt: Date.now(),
        pondId: pondId ?? 'pond-calm',
      },
      { pondId: pondId ?? 'pond-calm' },
    );
    const grant = finalizeDebugFishGrant(
      playerId,
      item,
      pondId ?? 'pond-calm',
      `已发放最大尺寸鱼获：${species.name}·至尊·${sizeM}m（已入相册）`,
    );
    return { ok: true, action, pondId: grant.pondId, message: grant.message };
  }

  if (action === 'grant_fish_epic_plus') {
    const epicQualities: FishQuality[] = ['purple', 'red', 'orange', 'gold'];
    const quality = epicQualities[Math.floor(Math.random() * epicQualities.length)];
    const species = pickSpeciesForQuality(quality);
    const sizeM =
      Math.round(getQualityMaxSize(quality, getSpecies(species.id)) * 0.92 * 100) / 100;
    const item = addFishToInventory(
      playerId,
      {
        speciesId: species.id as FishSpeciesId,
        quality,
        sizeM,
        caughtAt: Date.now(),
        pondId: pondId ?? 'pond-calm',
      },
      { pondId: pondId ?? 'pond-calm' },
    );
    const grant = finalizeDebugFishGrant(
      playerId,
      item,
      pondId ?? 'pond-calm',
      `已发放高品质鱼获：${species.name}·${quality}·${sizeM}m（已入相册）`,
    );
    return { ok: true, action, pondId: grant.pondId, message: grant.message };
  }

  if (action === 'advance_fee_2h') {
    if (!pondId || !live) return { ok: false, error: '当前不在鱼塘', action };
    const pond = getGamePondDef(pondId);
    if (!pond || pond.feePer2h <= 0) {
      return { ok: false, error: '当前不是收费塘，无法推进入场费', action, pondId };
    }
    if (!isFishingActive(live.user.fishingPhase)) {
      return { ok: false, error: '需在收费塘钓鱼中才能推进 +2h', action, pondId };
    }
    const fee = applyAdmissionFeeProgress(playerId, pondId, ADMISSION_FEE_SLICE_MS);
    if (fee.kind === 'insufficient') {
      requestFeeStop(
        live.user.id,
        pondId,
        `金币不足，已停止钓鱼（需要 ${fee.feePer2h} 金币支付下一时段）`,
      );
      return {
        ok: true,
        action,
        pondId,
        message: `入场费进度 +2h，金币不足已按正式规则停钓（需要 ${fee.feePer2h}）`,
      };
    }
    const charged = fee.charged;
    return {
      ok: true,
      action,
      pondId,
      message:
        charged > 0
          ? `入场费进度 +2h，已扣 ${charged} 金币（今日第 ${fee.state.charges} 次）`
          : `入场费进度 +2h（今日已扣 ${fee.state.charges} 次）`,
    };
  }

  if (action === 'reset_fishing_duration') {
    if (live) {
      debugResetTodayFishingDuration(live.user);
    } else {
      clearTodayFishingMsRecord(playerId);
    }
    return {
      ok: true,
      action,
      pondId,
      message: '已重置今日钓鱼时长为 0',
    };
  }

  return { ok: false, error: '未知 Debug 动作', action };
}

export type DebugFishView = {
  id: string;
  pondId: string;
  spotId: string;
  speciesId: string;
  speciesName: string;
  speciesIcon: string;
  quality: string;
  sizeM: number;
  bornAt: number;
  generation: number;
  biteMultiplier: number;
  escapeMultiplier: number;
  birthSizeM: number;
  nearMaxSize: boolean;
};

export function serializeDebugFish(fish: PondFishEntity): DebugFishView {
  const species = getSpecies(fish.speciesId);
  return {
    id: fish.id,
    pondId: fish.pondId,
    spotId: fish.spotId,
    speciesId: fish.speciesId,
    speciesName: species?.name ?? fish.speciesId,
    speciesIcon: species?.icon ?? '',
    quality: fish.quality,
    sizeM: fish.sizeM,
    bornAt: fish.bornAt,
    generation: fish.generation,
    biteMultiplier: fish.biteMultiplier ?? 1,
    escapeMultiplier: fish.escapeMultiplier ?? 1,
    birthSizeM: fish.birthSizeM ?? fish.sizeM,
    nearMaxSize: isNearMaxSize(fish),
  };
}

export function listGameplayDebugPondFish(
  playerId: string,
  scope: 'pond' | 'spot',
):
  | { ok: true; scope: 'pond' | 'spot'; pondId: string; spotId: string; fish: DebugFishView[] }
  | { ok: false; error: string } {
  const live = findLivePondUser(playerId);
  if (!live) return { ok: false, error: '当前不在鱼塘' };
  const spotId = live.user.spotId ?? '';
  if (scope === 'spot' && !spotId) {
    return { ok: false, error: '当前未坐席，无法查看钓位鱼' };
  }
  const raw =
    scope === 'spot'
      ? listPondFishAtSpot(live.pondId, spotId)
      : listPondFishEntities(live.pondId);
  const fish = raw
    .map(serializeDebugFish)
    .sort((a, b) => a.speciesName.localeCompare(b.speciesName, 'zh') || a.sizeM - b.sizeM);
  return { ok: true, scope, pondId: live.pondId, spotId, fish };
}

export function getGameplayDebugSpotStats(playerId: string):
  | {
      ok: true;
      pondId: string;
      spotId: string;
      summary: ReturnType<typeof buildFishingDebugReport>['summary'];
      constants: ReturnType<typeof buildFishingDebugReport>['constants'];
      spot: ReturnType<typeof buildFishingDebugReport>['spots'][number] | null;
    }
  | { ok: false; error: string } {
  const live = findLivePondUser(playerId);
  if (!live) return { ok: false, error: '当前不在鱼塘' };
  const spotId = live.user.spotId ?? '';
  if (!spotId) return { ok: false, error: '当前未坐席，无法查看钓位数据' };
  const report = buildFishingDebugReport(live.pondId, { playerId });
  const spot = report.spots.find((s) => s.spotId === spotId) ?? null;
  return {
    ok: true,
    pondId: live.pondId,
    spotId,
    summary: report.summary,
    constants: report.constants,
    spot,
  };
}

export function forceGameplayDebugBite(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  playerId: string,
  fishId: string,
): GameplayDebugResult {
  const live = findLivePondUser(playerId);
  if (!live) return { ok: false, error: '当前不在鱼塘', action: 'force_bite' };
  const { pondId, user } = live;
  if (user.fishingPhase !== 'waiting') {
    return {
      ok: false,
      error: `须在 waiting 阶段上钩（当前：${user.fishingPhase}）`,
      action: 'force_bite',
      pondId,
    };
  }
  if (getPendingCatch(user.id)) {
    return { ok: false, error: '已有待领取鱼获', action: 'force_bite', pondId };
  }
  const fish = getPondFishById(fishId);
  if (!fish || fish.pondId !== pondId) {
    return { ok: false, error: '鱼不存在或不属于当前塘', action: 'force_bite', pondId };
  }
  const hookDurationMs = Math.round(
    (isInstantFishingTestMode()
      ? 1000
      : calcHookDurationMs(fish.quality, fish.sizeM, fish.speciesId)) *
      getHookDurationScale(),
  );
  const socketId = resolveSocketByPlayer(playerId) ?? null;
  scheduleHookFromBite(io, pondId, user.id, socketId, {
    fish,
    escaped: false,
    hookDurationMs,
  });
  const species = getSpecies(fish.speciesId);
  return {
    ok: true,
    action: 'force_bite',
    pondId,
    message: `已强制上钩：${species?.name ?? fish.speciesId} / ${fish.quality} / ${fish.sizeM.toFixed(2)}m`,
  };
}

const CATCH_QUALITY_ACTIONS = {
  catch_quality_purple: 'purple',
  catch_quality_red: 'red',
  catch_quality_orange: 'orange',
  catch_quality_gold: 'gold',
} as const satisfies Record<string, FishQuality>;

type DebugCatchAction =
  | 'complete_catch'
  | 'instant_catch'
  | 'catch_fish'
  | 'force_bite_instant'
  | 'catch_quality_purple'
  | 'catch_quality_red'
  | 'catch_quality_orange'
  | 'catch_quality_gold';

function formatDebugCatchSuccess(
  settled: { item: FishInventoryItem; autoReturned: boolean; gold?: number },
  action: DebugCatchAction,
  pondId: string,
): GameplayDebugResult {
  const species = getSpecies(settled.item.speciesId);
  const q = getQualityInfo(settled.item.quality);
  const message = settled.autoReturned
    ? `已自动回塘：【${q.name}】${species.name} ${settled.item.sizeM.toFixed(2)}m` +
      (settled.gold != null ? `，+${settled.gold} 金币` : '')
    : `已入背包：【${q.name}】${species.name} ${settled.item.sizeM.toFixed(2)}m（未达回鱼标准或本局为出售档）`;
  return {
    ok: true,
    action,
    pondId,
    message,
    autoReturned: settled.autoReturned,
    gold: settled.gold,
  };
}

function settlePendingCatchForDebug(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  live: NonNullable<ReturnType<typeof findLivePondUser>>,
  socketId: string | null,
  action: DebugCatchAction,
): GameplayDebugResult {
  const { pondId, user } = live;
  const pending = getPendingCatch(user.id);
  if (!pending) {
    return { ok: false, error: '没有待结算鱼获（可能已自动结算）', action, pondId };
  }
  const settled = settleAcceptedCatch(
    {
      io,
      socketId,
      userId: user.id,
      playerId: user.playerId!,
      pondId,
      nickname: user.nickname,
    },
    pending.catchId,
    { auto: true },
  );
  if (!settled.ok) {
    return { ok: false, error: settled.error, action, pondId };
  }
  flushResolvingPhase(io, pondId, user.id, socketId);
  return formatDebugCatchSuccess(settled, action, pondId);
}

function completeDebugCatchFromFish(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  playerId: string,
  fishId: string,
  action: DebugCatchAction,
): GameplayDebugResult {
  const live = findLivePondUser(playerId);
  if (!live) return { ok: false, error: '当前不在鱼塘', action };
  const { pondId, user } = live;
  const socketId = resolveSocketByPlayer(playerId) ?? null;

  if (getPendingCatch(user.id)) {
    return settlePendingCatchForDebug(io, live, socketId, action);
  }

  const fish = getPondFishById(fishId);
  if (!fish || fish.pondId !== pondId) {
    return { ok: false, error: '鱼不存在或不属于当前塘', action, pondId };
  }

  if (user.fishingPhase === 'waiting') {
    scheduleHookFromBite(io, pondId, user.id, socketId, {
      fish,
      escaped: false,
      hookDurationMs: 0,
    });
  } else if (user.fishingPhase === 'hooked') {
    // 已在 hooked：继续收杆
  } else if (user.fishingPhase === 'resolving') {
    return settlePendingCatchForDebug(io, live, socketId, action);
  } else {
    return {
      ok: false,
      error: `须在 waiting 或 hooked 阶段（当前：${user.fishingPhase}）`,
      action,
      pondId,
    };
  }

  if (
    !flushHookedPhaseToPendingCatch(io, pondId, user.id, socketId, {
      deferAutoSettle: true,
    })
  ) {
    return { ok: false, error: '收杆结算失败', action, pondId };
  }
  return settlePendingCatchForDebug(io, live, socketId, action);
}

export function completeCatchFromHooked(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  playerId: string,
): GameplayDebugResult {
  const live = findLivePondUser(playerId);
  if (!live) return { ok: false, error: '当前不在鱼塘', action: 'complete_catch' };
  const { pondId, user } = live;
  const socketId = resolveSocketByPlayer(playerId) ?? null;

  if (getPendingCatch(user.id)) {
    return settlePendingCatchForDebug(io, live, socketId, 'complete_catch');
  }

  if (user.fishingPhase === 'hooked') {
    if (
      !flushHookedPhaseToPendingCatch(io, pondId, user.id, socketId, {
        deferAutoSettle: true,
      })
    ) {
      return { ok: false, error: '收杆结算失败', action: 'complete_catch', pondId };
    }
    return settlePendingCatchForDebug(io, live, socketId, 'complete_catch');
  }

  if (user.fishingPhase === 'resolving') {
    return settlePendingCatchForDebug(io, live, socketId, 'complete_catch');
  }

  return {
    ok: false,
    error: `须在 hooked 阶段（当前：${user.fishingPhase}）`,
    action: 'complete_catch',
    pondId,
  };
}

/** @deprecated 别名 completeCatchFromHooked */
export function instantCatchFromHooked(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  playerId: string,
): GameplayDebugResult {
  return completeCatchFromHooked(io, playerId);
}

export function catchFishDebug(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  playerId: string,
  fishId: string,
): GameplayDebugResult {
  return completeDebugCatchFromFish(io, playerId, fishId, 'catch_fish');
}

/** @deprecated 别名 catchFishDebug */
export function forceGameplayDebugInstantCatch(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  playerId: string,
  fishId: string,
): GameplayDebugResult {
  return catchFishDebug(io, playerId, fishId);
}

export function catchQualityDebugFish(
  io: Server<ClientToServerEvents, ServerToClientEvents>,
  playerId: string,
  quality: FishQuality,
): GameplayDebugResult {
  const action = `catch_quality_${quality}` as DebugCatchAction;
  const live = findLivePondUser(playerId);
  if (!live) return { ok: false, error: '当前不在鱼塘', action };
  const spotId = live.user.spotId ?? '';
  if (!spotId) {
    return { ok: false, error: '当前未坐席，无法生成测试鱼', action, pondId: live.pondId };
  }
  if (live.user.fishingPhase !== 'waiting' && live.user.fishingPhase !== 'hooked') {
    return {
      ok: false,
      error: `须在 waiting 阶段（当前：${live.user.fishingPhase}）`,
      action,
      pondId: live.pondId,
    };
  }
  if (getPendingCatch(live.user.id)) {
    return { ok: false, error: '已有待结算鱼获', action, pondId: live.pondId };
  }
  const spawned = spawnDebugHookFish(live.pondId, spotId, quality);
  if (!spawned) {
    return { ok: false, error: '生成测试鱼失败', action, pondId: live.pondId };
  }
  return completeDebugCatchFromFish(io, playerId, spawned.id, action);
}

export function registerGameplayDebugRoutes(
  app: Express,
  io: Server<ClientToServerEvents, ServerToClientEvents>,
): void {
  const guardDebug = (req: Request, res: Response): string | null => {
    if (!isGameplayDebugEnabled()) {
      res.status(403).json({ ok: false, error: '正式环境未开启玩法 Debug' });
      return null;
    }
    const playerId = resolveAuthedPlayerId(req);
    if (!playerId) {
      res.status(401).json({ ok: false, error: '未登录' });
      return null;
    }
    return playerId;
  };

  app.get('/api/debug/gameplay/pond-fish', requireAuth, (req: Request, res: Response) => {
    const playerId = guardDebug(req, res);
    if (!playerId) return;
    const rawScope = typeof req.query.scope === 'string' ? req.query.scope.trim() : 'pond';
    const scope = rawScope === 'spot' ? 'spot' : 'pond';
    const result = listGameplayDebugPondFish(playerId, scope);
    if (!result.ok) {
      res.status(400).json(result);
      return;
    }
    res.json(result);
  });

  app.get('/api/debug/gameplay/spot-stats', requireAuth, (req: Request, res: Response) => {
    const playerId = guardDebug(req, res);
    if (!playerId) return;
    const result = getGameplayDebugSpotStats(playerId);
    if (!result.ok) {
      res.status(400).json(result);
      return;
    }
    res.json(result);
  });

  app.post('/api/debug/gameplay', requireAuth, (req: Request, res: Response) => {
    const playerId = guardDebug(req, res);
    if (!playerId) return;
    const action =
      typeof req.body?.action === 'string' ? req.body.action.trim() : '';
    const fishId =
      typeof req.body?.fishId === 'string' ? req.body.fishId.trim() : '';

    const result =
      action === 'force_bite'
        ? forceGameplayDebugBite(io, playerId, fishId)
        : action === 'force_bite_instant' || action === 'catch_fish'
          ? catchFishDebug(io, playerId, fishId)
          : action === 'instant_catch' || action === 'complete_catch'
            ? completeCatchFromHooked(io, playerId)
            : action in CATCH_QUALITY_ACTIONS
              ? catchQualityDebugFish(
                  io,
                  playerId,
                  CATCH_QUALITY_ACTIONS[action as keyof typeof CATCH_QUALITY_ACTIONS],
                )
              : executeGameplayDebugAction(playerId, action);

    recordFishingMetric('gameplay_debug_action', {
      playerId,
      pondId: result.ok ? result.pondId : findLivePondUser(playerId)?.pondId,
      payload: {
        action,
        fishId: fishId || undefined,
        ok: result.ok,
        message: result.ok ? result.message : result.error,
      },
    });
    if (!result.ok) {
      res.status(400).json(result);
      return;
    }

    const socketId = resolveSocketByPlayer(playerId);
    const live = findLivePondUser(playerId);

    if (
      action === 'grant_fish' ||
      action === 'grant_fish_max_size' ||
      action === 'grant_fish_epic_plus'
    ) {
      if (socketId) {
        io.to(socketId).emit('inventory_updated', getInventory(playerId));
        const side = lastFishSideEffects;
        if (side && side.playerId === playerId) {
          if (side.codex) io.to(socketId).emit('codex_unlocked', side.codex);
          for (const ach of side.achievements) {
            io.to(socketId).emit('achievement_unlocked', {
              achievementId: ach.achievementId,
              name: ach.name,
              desc: ach.desc,
            });
          }
        }
      }
    }

    if (action === 'reset_fishing_duration' && live) {
      emitPondUserUpdated(io, live.pondId, live.user);
    }

    res.json({
      ...result,
      progress: getProgressPublicView(playerId),
      coins: getPlayer(playerId)?.coins ?? 0,
    });
  });
}
