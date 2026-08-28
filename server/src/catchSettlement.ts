/**
 * 捕获领取 + 自动回鱼 + session 台账 + 客户端推送（accept_catch 与回鱼档即时结算共用）。
 */
import type { Server } from 'socket.io';
import {
  calcCatchXpGrant,
  getQualityInfo,
  getSpecies,
  isAnnounceQuality,
  type ClientToServerEvents,
  type FishInventoryItem,
  type ServerToClientEvents,
} from '@fish-social/shared';
import { acceptCatch, getInventory } from './inventory.js';
import { tryAutoReturnFish } from './returnFish.js';
import { recordFishingMetric } from './fishingMetrics.js';
import { isCodexNewForPlayer, recordCodexCatch } from './codex.js';
import { addAlbumCandidate } from './album.js';
import { tryUnlockAchievements } from './achievements.js';
import { postAnnouncement } from './pondChat.js';
import { autoShareEpicCatch } from './posts.js';
import { recordSessionCatch } from './pondSessionLedger.js';

export type CatchSettleContext = {
  io: Server<ClientToServerEvents, ServerToClientEvents>;
  socketId: string | null;
  userId: string;
  playerId: string;
  pondId: string;
  nickname: string;
};

export type CatchSettleOk = {
  ok: true;
  item: FishInventoryItem;
  autoReturned: boolean;
  gold?: number;
  playerXp?: number;
  pondXp?: number;
  newSizeM?: number;
  sizeGainM?: number;
  totalCoins?: number;
};

export type CatchSettleResult = CatchSettleOk | { ok: false; error: string };

export type FishCatchSettledPayload = {
  speciesId: string;
  quality: string;
  sizeM: number;
  autoReturned: boolean;
  gold?: number;
  playerXp?: number;
  pondXp?: number;
  newSizeM?: number;
  sizeGainM?: number;
  totalCoins?: number;
  message: string;
};

function buildSettledMessage(
  item: FishInventoryItem,
  autoReturned: boolean,
  gold?: number,
): string {
  const species = getSpecies(item.speciesId);
  const q = getQualityInfo(item.quality);
  if (autoReturned && gold != null) {
    return `【${q.name}】${species.name} ${item.sizeM.toFixed(2)}m 已自动回塘，+${gold} 金币`;
  }
  return `已入包：【${q.name}】${species.name} ${item.sizeM.toFixed(2)}m（未达回鱼标准）`;
}

export function settleAcceptedCatch(
  ctx: CatchSettleContext,
  catchId: string,
  opts?: { auto?: boolean },
): CatchSettleResult {
  const result = acceptCatch(ctx.userId, ctx.playerId, catchId, ctx.pondId);
  if (!result.ok) return result;

  recordFishingMetric('pending_catch_accept', {
    playerId: ctx.playerId,
    pondId: ctx.pondId,
    payload: {
      speciesId: result.item.speciesId,
      quality: result.item.quality,
      sizeM: result.item.sizeM,
      eventId: catchId,
      auto: opts?.auto === true,
    },
  });

  const catchXp = calcCatchXpGrant(
    result.item.speciesId,
    result.item.quality,
    result.item.sizeM,
  );

  const wasNewCodex = isCodexNewForPlayer(ctx.playerId, result.item.speciesId);
  const codexUnlock = recordCodexCatch(
    ctx.playerId,
    result.item.speciesId,
    result.item.sizeM,
  );
  if (codexUnlock && ctx.socketId) {
    ctx.io.to(ctx.socketId).emit('codex_unlocked', codexUnlock);
  }

  const newly = tryUnlockAchievements(ctx.playerId);
  if (ctx.socketId) {
    for (const ach of newly) {
      ctx.io.to(ctx.socketId).emit('achievement_unlocked', {
        achievementId: ach.achievementId,
        name: ach.name,
        desc: ach.desc,
      });
    }
  }

  const autoResult = tryAutoReturnFish(ctx.playerId, result.item.id);
  // 自动回鱼时 returnFishToPond 已写入 source=return 相册卡，勿再记「捕获」造成一张鱼两张照
  if (!autoResult.ok) {
    addAlbumCandidate({
      playerId: ctx.playerId,
      speciesId: result.item.speciesId,
      quality: result.item.quality,
      sizeM: result.item.sizeM,
      pondId: ctx.pondId,
      source: wasNewCodex ? 'first_codex' : 'catch',
      inventoryItemId: result.item.id,
    });
  }

  const settled: CatchSettleOk = autoResult.ok
    ? {
        ok: true,
        item: result.item,
        autoReturned: true,
        gold: autoResult.gold,
        playerXp: autoResult.playerXp + catchXp.playerXp,
        pondXp: autoResult.pondXp + catchXp.pondXp,
        newSizeM: autoResult.newSizeM,
        sizeGainM: autoResult.sizeGainM,
        totalCoins: autoResult.totalCoins,
      }
    : {
        ok: true,
        item: result.item,
        autoReturned: false,
        playerXp: catchXp.playerXp,
        pondXp: catchXp.pondXp,
      };

  recordSessionCatch(ctx.playerId, {
    speciesId: result.item.speciesId,
    quality: result.item.quality,
    sizeM: result.item.sizeM,
    outcome: settled.autoReturned ? 'returned' : 'kept',
    returnGold: settled.gold,
    catchPlayerXp: catchXp.playerXp,
    catchPondXp: catchXp.pondXp,
    returnPlayerXp: settled.autoReturned ? autoResult.ok ? autoResult.playerXp : 0 : 0,
    returnPondXp: settled.autoReturned ? autoResult.ok ? autoResult.pondXp : 0 : 0,
    caughtAt: Date.now(),
  });

  if (ctx.socketId) {
    const payload: FishCatchSettledPayload = {
      speciesId: result.item.speciesId,
      quality: result.item.quality,
      sizeM: result.item.sizeM,
      autoReturned: settled.autoReturned,
      gold: settled.gold,
      playerXp: settled.playerXp,
      pondXp: settled.pondXp,
      newSizeM: settled.newSizeM,
      sizeGainM: settled.sizeGainM,
      totalCoins: settled.totalCoins,
      message: buildSettledMessage(
        result.item,
        settled.autoReturned,
        settled.gold,
      ),
    };
    ctx.io.to(ctx.socketId).emit('fish_catch_settled', payload);
    ctx.io.to(ctx.socketId).emit(
      'inventory_updated',
      autoResult.ok ? autoResult.items : getInventory(ctx.playerId),
    );
  }

  if (isAnnounceQuality(result.item.quality)) {
    const species = getSpecies(result.item.speciesId);
    const quality = getQualityInfo(result.item.quality);
    const text = `${ctx.nickname}钓到了【${quality.name}】的${species.name}！`;
    const msg = postAnnouncement(ctx.pondId, text);
    ctx.io.to(ctx.pondId).emit('chat_message', msg);
    autoShareEpicCatch(ctx.playerId, ctx.nickname, result.item);
  }

  return settled;
}
