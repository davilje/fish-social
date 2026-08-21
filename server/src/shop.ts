import type { Express, Request, Response } from 'express';
import type { Server } from 'socket.io';
import {
  BASIC_BAIT_ID,
  getRodDef,
  getVesselDef,
  listGameBaits,
  listRods,
  listVessels,
  type ClientToServerEvents,
  type PlayerGearState,
  type ServerToClientEvents,
  type ShopErrorCode,
} from '@fish-social/shared';
import {
  addOwnedRod,
  addOwnedVessel,
  ensurePlayerGear,
  equipBait,
  equipRod,
  repairTackle,
} from './gear.js';
import { getPlayerCodex } from './codex.js';
import { recordFishingMetric } from './fishingMetrics.js';
import { deductCoins, getPlayer } from './players.js';
import { ensurePlayerProgress } from './playerProgress.js';
import { requireAuth, resolveAuthedPlayerId } from './auth.js';
import { resolveSocketByPlayer } from './sessionRegistry.js';

const IDEMPOTENCY_TTL_MS = 10 * 60 * 1000;

interface IdempotencyEntry {
  at: number;
  status: number;
  body: unknown;
}

const idempotencyCache = new Map<string, IdempotencyEntry>();

function gearLevel(playerId: string): number {
  return ensurePlayerProgress(playerId).level;
}

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
    res.json({
      baits: listGameBaits().map((b) => ({
        id: b.baitId,
        name: b.name,
        icon: b.isDefaultInfinite ? '🪱' : '🎣',
        price: b.costGoldPerUse,
        globalBonus: Math.max(b.biteBonusHerbivore, b.biteBonusOmnivore, b.biteBonusCarnivore),
        consumed: !b.isDefaultInfinite,
        diet: b.diet,
        unlockPlayerLevel: b.unlockPlayerLevel,
        costGoldPerUse: b.costGoldPerUse,
        isDefaultInfinite: b.isDefaultInfinite,
        biteBonusHerbivore: b.biteBonusHerbivore,
        biteBonusOmnivore: b.biteBonusOmnivore,
        biteBonusCarnivore: b.biteBonusCarnivore,
      })),
    });
  });

  app.get('/api/shop/tackle', (_req, res) => {
    res.json({
      tackles: listRods().map((r) => ({
        id: r.rodId,
        name: r.name,
        icon: '🎣',
        price: r.priceGold,
        escapeReduction: r.escapeReduction,
        biteBonus: r.biteBonus,
        subType: r.subType,
        breakSizeM: r.breakSizeM,
        breakMaxLandings: r.breakMaxLandings,
        fitGray: r.fitGray,
        fitGreen: r.fitGreen,
        fitBlue: r.fitBlue,
        fitPurple: r.fitPurple,
        fitRed: r.fitRed,
        fitOrange: r.fitOrange,
        fitGold: r.fitGold,
        fitStillBait: r.fitStillBait,
        fitStreamLight: r.fitStreamLight,
        fitLurePredator: r.fitLurePredator,
        fitCastHeavy: r.fitCastHeavy,
        fitGiantGame: r.fitGiantGame,
      })),
    });
  });

  app.get('/api/shop/vessels', (_req, res) => {
    res.json({ vessels: listVessels() });
  });

  app.get('/api/player/gear', requireAuth, (req, res) => {
    const playerId = requirePlayerId(req, res);
    if (!playerId) return;
    const gear = ensurePlayerGear(playerId);
    const profile = getPlayer(playerId)!;
    res.json({ gear, coins: profile.coins, playerLevel: gearLevel(playerId) });
  });

  app.post('/api/shop/baits/buy', requireAuth, (req, res) => {
    shopError(res, 400, 'INVALID_ITEM', '鱼饵无需进货，按钓鱼等级解锁，咬钩时按次扣金');
  });

  app.post('/api/shop/tackle/buy', requireAuth, (req, res) => {
    const playerId = requirePlayerId(req, res);
    if (!playerId) return;
    if (readIdempotent(req, res, playerId, 'buy-tackle')) return;

    const { tackleId, rodId } = req.body as { tackleId?: string; rodId?: string };
    const id = rodId || tackleId;
    const rod = id ? getRodDef(id) : undefined;
    if (!rod || rod.priceGold <= 0) {
      shopError(res, 400, 'INVALID_ITEM', '无效的钓竿');
      return;
    }

    const gearBefore = ensurePlayerGear(playerId);
    if (gearBefore.ownedRods.includes(rod.rodId)) {
      shopError(res, 400, 'ALREADY_OWNED', '已拥有该钓竿');
      return;
    }

    const spend = deductCoins(playerId, rod.priceGold);
    if (!spend.ok) {
      shopError(res, 400, spend.code, '金币不足');
      return;
    }

    const gear = addOwnedRod(playerId, rod.rodId);
    const body = { ok: true, gear, coins: spend.coins, tackleId: rod.rodId, rodId: rod.rodId };
    writeIdempotent(req, playerId, 'buy-tackle', 200, body);
    recordFishingMetric('rod_buy', {
      playerId,
      payload: { rodId: rod.rodId, cost: rod.priceGold },
    });
    emitGearUpdated(io, playerId, gear);
    res.json(body);
  });

  app.post('/api/shop/vessels/buy', requireAuth, (req, res) => {
    const playerId = requirePlayerId(req, res);
    if (!playerId) return;
    if (readIdempotent(req, res, playerId, 'buy-vessel')) return;

    const { vesselId } = req.body as { vesselId?: string };
    const vessel = vesselId ? getVesselDef(vesselId) : undefined;
    if (!vessel) {
      shopError(res, 400, 'INVALID_ITEM', '无效的船具');
      return;
    }
    const level = gearLevel(playerId);
    if (level < vessel.unlockPlayerLevel) {
      shopError(res, 400, 'INVALID_ITEM', `需要钓鱼等级 ${vessel.unlockPlayerLevel}`);
      return;
    }
    const gearBefore = ensurePlayerGear(playerId);
    if (gearBefore.ownedVessels.includes(vessel.vesselId)) {
      shopError(res, 400, 'ALREADY_OWNED', '已拥有该船具');
      return;
    }
    const spend = deductCoins(playerId, vessel.priceGold);
    if (!spend.ok) {
      shopError(res, 400, spend.code, '金币不足');
      return;
    }
    const gear = addOwnedVessel(playerId, vessel.vesselId);
    const body = { ok: true, gear, coins: spend.coins, vesselId: vessel.vesselId };
    writeIdempotent(req, playerId, 'buy-vessel', 200, body);
    recordFishingMetric('vessel_buy', {
      playerId,
      payload: { vesselId: vessel.vesselId, cost: vessel.priceGold },
    });
    emitGearUpdated(io, playerId, gear);
    res.json(body);
  });

  app.post('/api/player/equip/bait', requireAuth, (req, res) => {
    const playerId = requirePlayerId(req, res);
    if (!playerId) return;

    const { baitId } = req.body as { baitId?: string };
    if (!baitId || baitId !== BASIC_BAIT_ID) {
      shopError(res, 400, 'INVALID_ITEM', '进阶饵在咬钩时自动选用，无需装备');
      return;
    }

    const gear = equipBait(playerId, baitId);
    emitGearUpdated(io, playerId, gear);
    res.json({ ok: true, gear });
  });

  app.post('/api/player/equip/tackle', requireAuth, (req, res) => {
    const playerId = requirePlayerId(req, res);
    if (!playerId) return;

    const { tackleId, rodId } = req.body as { tackleId?: string; rodId?: string };
    const id = rodId || tackleId;
    const rod = id ? getRodDef(id) : undefined;
    if (!rod) {
      shopError(res, 400, 'INVALID_ITEM', '无效的钓竿');
      return;
    }

    const gearBefore = ensurePlayerGear(playerId);
    if (!gearBefore.ownedRods.includes(rod.rodId)) {
      shopError(res, 400, 'NOT_IN_INVENTORY', '未拥有该钓竿');
      return;
    }

    const gear = equipRod(playerId, rod.rodId);
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
