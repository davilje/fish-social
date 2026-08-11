import { randomUUID } from 'crypto';
import type { FishInventoryItem, PendingFishCatch } from '@fish-social/shared';
import { FISHING_PROMPT_AUTO_CLOSE_MS } from '@fish-social/shared';
import { db } from './db.js';
import { removePondFish } from './pondEcology.js';
import { recordFishingMetric } from './fishingMetrics.js';
import {
  deletePendingCatchLock,
  deletePlayerPondSession,
  persistPendingCatchLock,
  schedulePendingExpireTimer,
} from './playerPondSession.js';
import { cancelByKind } from './timerRegistry.js';

const pendingByUser = new Map<string, PendingFishCatch>();
/** pondFishId → session userId，防止同一条鱼同时 pending 给多人 */
const pendingByPondFishId = new Map<string, string>();

export const PENDING_CATCH_TIMEOUT_MS = FISHING_PROMPT_AUTO_CLOSE_MS + 8_000;

export function getLockedPondFishIds(): Set<string> {
  return new Set(pendingByPondFishId.keys());
}

export function isPondFishLocked(pondFishId: string): boolean {
  return pendingByPondFishId.has(pondFishId);
}

function clearPendingTimeout(userId: string): void {
  cancelByKind(userId, 'pending_expire');
}

function releaseUserPending(
  userId: string,
  opts?: { playerId?: string; pondId?: string; reason?: 'expired' | 'cleared' | 'accepted' },
): void {
  const pending = pendingByUser.get(userId);
  if (pending) {
    pendingByPondFishId.delete(pending.pondFishId);
    if (opts?.reason === 'expired') {
      recordFishingMetric('pending_catch_expired', {
        playerId: opts.playerId,
        pondId: opts.pondId,
        payload: {
          userId,
          pondFishId: pending.pondFishId,
          speciesId: pending.speciesId,
          quality: pending.quality,
          sizeM: pending.sizeM,
        },
      });
    }
  }
  pendingByUser.delete(userId);
  clearPendingTimeout(userId);
  deletePendingCatchLock(userId);
  if (opts?.playerId && opts?.pondId) {
    deletePlayerPondSession(opts.playerId, opts.pondId);
  }
}

function schedulePendingTimeout(
  userId: string,
  meta?: { playerId?: string; pondId?: string },
  ms: number = PENDING_CATCH_TIMEOUT_MS,
): void {
  clearPendingTimeout(userId);
  schedulePendingExpireTimer(userId, ms, () => {
    releaseUserPending(userId, { ...meta, reason: 'expired' });
  });
}

export function restorePendingCatchFromDb(
  userId: string,
  catchData: PendingFishCatch,
  meta: { playerId?: string; pondId?: string },
  remainingMs: number,
): void {
  if (pendingByUser.has(userId)) return;
  if (pendingByPondFishId.has(catchData.pondFishId)) return;
  pendingByUser.set(userId, catchData);
  pendingByPondFishId.set(catchData.pondFishId, userId);
  schedulePendingTimeout(userId, meta, remainingMs);
}

export function expirePendingCatchFromDb(
  userId: string,
  meta: { playerId?: string; pondId?: string },
): void {
  releaseUserPending(userId, { ...meta, reason: 'expired' });
}

interface InvRow {
  id: string;
  player_id: string;
  species_id: string;
  quality: string;
  size_m: number;
  caught_at: number;
  pond_id?: string | null;
}

function rowToItem(row: InvRow): FishInventoryItem {
  return {
    id: row.id,
    speciesId: row.species_id as FishInventoryItem['speciesId'],
    quality: row.quality as FishInventoryItem['quality'],
    sizeM: row.size_m,
    caughtAt: row.caught_at,
    pondId: row.pond_id ?? null,
  };
}

const listInvStmt = db.prepare(
  'SELECT * FROM inventory WHERE player_id = ? ORDER BY caught_at DESC',
);
const insertInvStmt = db.prepare(`
  INSERT INTO inventory (id, player_id, species_id, quality, size_m, caught_at, pond_id)
  VALUES (@id, @playerId, @speciesId, @quality, @sizeM, @caughtAt, @pondId)
`);
const deleteInvStmt = db.prepare('DELETE FROM inventory WHERE id = ? AND player_id = ?');

export function getInventory(playerId: string): FishInventoryItem[] {
  return (listInvStmt.all(playerId) as InvRow[]).map(rowToItem);
}

export function setInventory(playerId: string, items: FishInventoryItem[]): void {
  db.prepare('DELETE FROM inventory WHERE player_id = ?').run(playerId);
  for (const item of items) {
    insertInvStmt.run({
      id: item.id,
      playerId,
      speciesId: item.speciesId,
      quality: item.quality,
      sizeM: item.sizeM,
      caughtAt: item.caughtAt,
      pondId: item.pondId ?? null,
    });
  }
}

export function addFishToInventory(
  playerId: string,
  fish: Omit<FishInventoryItem, 'id'>,
  opts?: { pondId?: string | null },
): FishInventoryItem {
  const pondId = opts?.pondId ?? fish.pondId ?? null;
  const item: FishInventoryItem = { ...fish, id: randomUUID(), pondId };
  insertInvStmt.run({
    id: item.id,
    playerId,
    speciesId: item.speciesId,
    quality: item.quality,
    sizeM: item.sizeM,
    caughtAt: item.caughtAt,
    pondId,
  });
  return item;
}

export function getPendingCatch(userId: string): PendingFishCatch | undefined {
  return pendingByUser.get(userId);
}

export function setPendingCatch(userId: string, catchData: PendingFishCatch): void {
  pendingByUser.set(userId, catchData);
}

export function clearPendingCatch(userId: string, meta?: { playerId?: string; pondId?: string }): void {
  releaseUserPending(userId, { ...meta, reason: 'cleared' });
}

/** 锁定已选中的咬钩鱼（由 fishingStateMachine 选定） */
export function lockPendingCatch(
  userId: string,
  catchData: PendingFishCatch,
  meta?: { playerId?: string; pondId?: string },
): PendingFishCatch | null {
  if (pendingByUser.has(userId)) return null;
  if (pendingByPondFishId.has(catchData.pondFishId)) return null;

  pendingByUser.set(userId, catchData);
  pendingByPondFishId.set(catchData.pondFishId, userId);
  persistPendingCatchLock(userId, catchData, meta);
  recordFishingMetric('pending_catch_created', {
    playerId: meta?.playerId,
    pondId: meta?.pondId,
    payload: {
      userId,
      pondFishId: catchData.pondFishId,
      speciesId: catchData.speciesId,
      quality: catchData.quality,
      sizeM: catchData.sizeM,
      hookDurationMs: catchData.hookDurationMs,
    },
  });
  schedulePendingTimeout(userId, meta);
  return catchData;
}

/** @deprecated A0 使用 fishingStateMachine + lockPendingCatch */
export function rollPendingCatch(
  userId: string,
  _pondId: string,
  _spotId: string,
): PendingFishCatch | null {
  if (pendingByUser.has(userId)) return null;
  return null;
}

export function acceptCatch(
  userId: string,
  playerId: string,
  catchId: string,
  pondId?: string,
): { ok: true; item: FishInventoryItem } | { ok: false; error: string } {
  const pending = pendingByUser.get(userId);
  if (!pending) return { ok: false, error: '没有待领取的鱼' };
  if (pending.catchId !== catchId) return { ok: false, error: '鱼已过期' };

  const removed = removePondFish(pending.pondFishId);
  releaseUserPending(userId, { playerId, pondId, reason: 'accepted' });
  if (!removed) {
    return { ok: false, error: '这条鱼已被他人钓走' };
  }

  const item = addFishToInventory(
    playerId,
    {
      speciesId: pending.speciesId,
      quality: pending.quality,
      sizeM: pending.sizeM,
      caughtAt: Date.now(),
      pondId: pondId ?? null,
    },
    { pondId: pondId ?? null },
  );
  return { ok: true, item };
}

export function getFishById(playerId: string, fishId: string): FishInventoryItem | null {
  const row = db
    .prepare('SELECT * FROM inventory WHERE player_id = ? AND id = ?')
    .get(playerId, fishId) as InvRow | undefined;
  return row ? rowToItem(row) : null;
}

export function removeFishFromInventory(playerId: string, fishId: string): FishInventoryItem | null {
  const fish = getFishById(playerId, fishId);
  if (!fish) return null;
  deleteInvStmt.run(fishId, playerId);
  return fish;
}

export function sellFish(
  playerId: string,
  fishId: string,
): { ok: true; fish: FishInventoryItem } | { ok: false; error: string } {
  const fish = removeFishFromInventory(playerId, fishId);
  if (!fish) return { ok: false, error: '鱼不存在' };
  return { ok: true, fish };
}
