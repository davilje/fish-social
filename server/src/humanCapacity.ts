/**
 * R2-3: human socket capacity (bots are in-memory and exempt).
 */
import { PONDS } from '@fish-social/shared';
import { listBotsInPond, listUsersInPond } from './gameState.js';
import {
  getBoundHumanSocketCount,
  isPlayerSocketBound,
} from './sessionRegistry.js';

export function getMaxHumanSockets(): number {
  const n = Number(process.env.MAX_HUMAN_SOCKETS ?? 200);
  return Number.isFinite(n) && n > 0 ? n : 200;
}

/** @deprecated Prefer getMaxHumanSockets() — kept for import compatibility */
export const MAX_HUMAN_SOCKETS = getMaxHumanSockets();

/** Optional per-pond human cap; empty / 0 = unlimited beyond spot logic */
export function getMaxHumansPerPond(): number {
  const raw = process.env.MAX_HUMANS_PER_POND;
  if (raw == null || raw === '') return 0;
  const n = Number(raw);
  return Number.isFinite(n) && n > 0 ? n : 0;
}

/** @deprecated Prefer getMaxHumansPerPond() */
export const MAX_HUMANS_PER_POND = getMaxHumansPerPond();

export function getHumanSocketCount(): number {
  return getBoundHumanSocketCount();
}

export function getBotInPondCount(): number {
  let n = 0;
  for (const pond of PONDS) {
    n += listBotsInPond(pond.id).length;
  }
  return n;
}

export function getHumanInPondCount(): number {
  let n = 0;
  for (const pond of PONDS) {
    const users = listUsersInPond(pond.id);
    n += users.filter((u) => !u.isBot).length;
  }
  return n;
}

export function getCapacitySnapshot() {
  const maxHumans = getMaxHumansPerPond();
  return {
    humanSocketCount: getHumanSocketCount(),
    botSocketCount: 0, // bots are not Socket.IO clients
    botInPondCount: getBotInPondCount(),
    humanInPond: getHumanInPondCount(),
    capacityLimit: getMaxHumanSockets(),
    maxHumansPerPond: maxHumans || null,
  };
}

/**
 * Soft-reject new humans when at limit.
 * Already-bound players (reconnect / re-join) are always allowed.
 */
export function shouldRejectHumanJoin(playerId: string): {
  reject: boolean;
  current: number;
  limit: number;
} {
  const current = getHumanSocketCount();
  const limit = getMaxHumanSockets();
  if (isPlayerSocketBound(playerId)) {
    return { reject: false, current, limit };
  }
  return { reject: current >= limit, current, limit };
}

export function shouldRejectHumanJoinPond(
  playerId: string,
  pondId: string,
): { reject: boolean; reason: 'human_socket_limit' | 'humans_per_pond' | null; current: number; limit: number } {
  const global = shouldRejectHumanJoin(playerId);
  if (global.reject) {
    return {
      reject: true,
      reason: 'human_socket_limit',
      current: global.current,
      limit: global.limit,
    };
  }
  const perPond = getMaxHumansPerPond();
  if (perPond > 0) {
    const humans = listUsersInPond(pondId).filter((u) => !u.isBot && u.playerId !== playerId).length;
    // If already in this pond (reconnect), allow
    const alreadyHere = listUsersInPond(pondId).some((u) => !u.isBot && u.playerId === playerId);
    if (!alreadyHere && humans >= perPond) {
      return {
        reject: true,
        reason: 'humans_per_pond',
        current: humans,
        limit: perPond,
      };
    }
  }
  return { reject: false, reason: null, current: global.current, limit: global.limit };
}
