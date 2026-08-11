import type { Express, Request, Response } from 'express';
import type { Server } from 'socket.io';
import {
  BAITS,
  TACKLES,
  getBait,
  getTackle,
  type BaitId,
  type ClientToServerEvents,
  type PlayerGearState,
  type ServerToClientEvents,
  type ShopErrorCode,
  type TackleId,
} from '@fish-social/shared';
import {
  addBaitToInventory,
  addOwnedTackle,
  ensurePlayerGear,
  equipBait,
  equipTackle,
  repairTackle,
} from './gear.js';
import { getPlayerCodex } from './codex.js';
import { recordFishingMetric } from './fishingMetrics.js';
import { deductCoins, getPlayer } from './players.js';
import { requireAuth, resolveAuthedPlayerId } from './auth.js';
import { resolveSocketByPlayer } from './sessionRegistry.js';

const MAX_BUY_QUANTITY = 100;
const IDEMPOTENCY_TTL_MS = 10 * 60 * 1000;

interface IdempotencyEntry {
  at: number;
  status: number;
  body: unknown;
}

const idempotencyCache = new Map<string, IdempotencyEntry>();

function shopError(res: Response, status: number, code: ShopErrorCode, message: string): void {
  res.status(status).json({ error: message, code });
}

function requirePlayerId(req: Request, res: Response): string | null {
  const fromBody = (req.body as { playerId?: string })?.playerId;
  const fromQuery = typeof req.query.playerId === 'string' ? req.query.playerId : undefined;
  const playerId = resolveAuthedPlayerId(req, fromBody ?? fromQuery);
  if (!playerId) {
    shopError(res, 401, 'UNAUTHORIZED', '未登录');
    return null;
  }
  if (!getPlayer(playerId)) {
    shopError(res, 401, 'UNAUTHORIZED', '玩家不存在');
    return null;
  }
  return playerId;
}

function idempotencyKey(req: Request, playerId: string, action: string): string | null {
  const key = req.header('X-Idempotency-Key');
  if (!key) return null;
  return `${playerId}:${action}:${key}`;
}

function readIdempotent(
  req: Request,
  res: Response,
  playerId: string,
  action: string,
): boolean {
  const cacheKey = idempotencyKey(req, playerId, action);
  if (!cacheKey) return false;
  const cached = idempotencyCache.get(cacheKey);
  const now = Date.now();
  if (cached && now - cached.at < IDEMPOTENCY_TTL_MS) {
    res.status(cached.status).json(cached.body);
    return true;
  }
  return false;
}

function writeIdempotent(
  req: Request,
  playerId: string,
  action: string,
  status: number,
  body: unknown,
): void {
  const cacheKey = idempotencyKey(req, playerId, action);
  if (!cacheKey) return;
  idempotencyCache.set(cacheKey, { at: Date.now(), status, body });
}

function emitGearUpdated(
  io: Server<ClientToServerEvents, ServerToClientEvents> | undefined,
  playerId: string,
  gear: PlayerGearState,
): void {
  if (!io) return;
  const socketId = resolveSocketByPlayer(playerId);
  if (socketId) io.to(socketId).emit('gear_updated', gear);
}

export function registerShopRoutes(
  app: Express,
  io?: Server<ClientToServerEvents, ServerToClientEvents>,
): void {
  app.get('/api/shop/baits', (_req, res) => {
    res.json({ baits: BAITS });
  });

  app.get('/api/shop/tackle', (_req, res) => {
    res.json({ tackles: TACKLES });
  });

  app.get('/api/player/gear', requireAuth, (req, res) => {
    const playerId = requirePlayerId(req, res);
    if (!playerId) return;
    const gear = ensurePlayerGear(playerId);
    const profile = getPlayer(playerId)!;
    res.json({ gear, coins: profile.coins });
  });

  app.post('/api/shop/baits/buy', requireAuth, (req, res) => {
    const playerId = requirePlayerId(req, res);
    if (!playerId) return;
    if (readIdempotent(req, res, playerId, 'buy-bait')) return;

    const { baitId, quantity } = req.body as { baitId?: string; quantity?: number };
    const bait = baitId ? getBait(baitId) : undefined;
    if (!bait || bait.id === 'basic') {
      shopError(res, 400, 'INVALID_ITEM', '无效的鱼饵');
      return;
    }
    const qty = Number(quantity);
    if (!Number.isFinite(qty) || qty <= 0 || qty > MAX_BUY_QUANTITY) {
      shopError(res, 400, 'INVALID_QUANTITY', '购买数量无效');
      return;
    }

    const totalCost = bait.price * qty;
    const spend = deductCoins(playerId, totalCost);
    if (!spend.ok) {
      shopError(res, 400, spend.code, '金币不足');
      return;
    }

    const gear = addBaitToInventory(playerId, bait.id as BaitId, qty);
    const body = { ok: true, gear, coins: spend.coins, baitId: bait.id, quantity: qty };
    writeIdempotent(req, playerId, 'buy-bait', 200, body);
    recordFishingMetric('bait_buy', {
      playerId,
      payload: { baitId: bait.id, quantity: qty, cost: totalCost },
    });
    emitGearUpdated(io, playerId, gear);
    res.json(body);
  });

  app.post('/api/shop/tackle/buy', requireAuth, (req, res) => {
    const playerId = requirePlayerId(req, res);
    if (!playerId) return;
    if (readIdempotent(req, res, playerId, 'buy-tackle')) return;

    const { tackleId } = req.body as { tackleId?: string };
    const tackle = tackleId ? getTackle(tackleId) : undefined;
    if (!tackle || tackle.id === 'basic') {
      shopError(res, 400, 'INVALID_ITEM', '无效的渔具');
      return;
    }

    const gearBefore = ensurePlayerGear(playerId);
    if (gearBefore.ownedTackles.includes(tackle.id as TackleId)) {
      shopError(res, 400, 'ALREADY_OWNED', '已拥有该渔具');
      return;
    }

    const spend = deductCoins(playerId, tackle.price);
    if (!spend.ok) {
      shopError(res, 400, spend.code, '金币不足');
      return;
    }

    const gear = addOwnedTackle(playerId, tackle.id as TackleId);
    const body = { ok: true, gear, coins: spend.coins, tackleId: tackle.id };
    writeIdempotent(req, playerId, 'buy-tackle', 200, body);
    recordFishingMetric('tackle_buy', {
      playerId,
      payload: { tackleId: tackle.id, cost: tackle.price },
    });
    emitGearUpdated(io, playerId, gear);
    res.json(body);
  });

  app.post('/api/player/equip/bait', requireAuth, (req, res) => {
    const playerId = requirePlayerId(req, res);
    if (!playerId) return;

    const { baitId } = req.body as { baitId?: string };
    const bait = baitId ? getBait(baitId) : undefined;
    if (!bait) {
      shopError(res, 400, 'INVALID_ITEM', '无效的鱼饵');
      return;
    }

    const gearBefore = ensurePlayerGear(playerId);
    if (bait.consumed) {
      const count = gearBefore.baitInventory[bait.id] ?? 0;
      if (count <= 0 && bait.id !== 'basic') {
        shopError(res, 400, 'NOT_IN_INVENTORY', '背包中没有该鱼饵');
        return;
      }
    }

    const gear = equipBait(playerId, bait.id as BaitId);
    emitGearUpdated(io, playerId, gear);
    res.json({ ok: true, gear });
  });

  app.post('/api/player/equip/tackle', requireAuth, (req, res) => {
    const playerId = requirePlayerId(req, res);
    if (!playerId) return;

    const { tackleId } = req.body as { tackleId?: string };
    const tackle = tackleId ? getTackle(tackleId) : undefined;
    if (!tackle) {
      shopError(res, 400, 'INVALID_ITEM', '无效的渔具');
      return;
    }

    const gearBefore = ensurePlayerGear(playerId);
    if (!gearBefore.ownedTackles.includes(tackle.id as TackleId)) {
      shopError(res, 400, 'NOT_IN_INVENTORY', '未拥有该渔具');
      return;
    }

    const gear = equipTackle(playerId, tackle.id as TackleId);
    emitGearUpdated(io, playerId, gear);
    res.json({ ok: true, gear });
  });

  app.post('/api/shop/tackle/repair', requireAuth, (req, res) => {
    const playerId = requirePlayerId(req, res);
    if (!playerId) return;

    const { tackleId } = req.body as { tackleId?: string };
    const tackle = tackleId ? getTackle(tackleId) : undefined;
    if (!tackle) {
      shopError(res, 400, 'INVALID_ITEM', '无效的渔具');
      return;
    }

    const result = repairTackle(playerId, tackle.id as TackleId);
    if (!result.ok) {
      shopError(res, 400, 'INVALID_ITEM', result.error);
      return;
    }

    emitGearUpdated(io, playerId, result.gear);
    res.json({ ok: true, gear: result.gear, coins: result.coins, cost: result.cost });
  });

  app.get('/api/player/codex', requireAuth, (req, res) => {
    const playerId = requirePlayerId(req, res);
    if (!playerId) return;
    res.json({ entries: getPlayerCodex(playerId) });
  });
}
