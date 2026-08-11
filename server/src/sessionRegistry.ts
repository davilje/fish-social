import { socketConnectionsGauge } from './metricsPrometheus.js';

interface PlayerBinding {
  socketId: string;
  correlationId?: string;
}

interface PondUserBinding {
  socketId: string;
  pondId: string;
}

const playerBySocket = new Map<string, string>();
const socketByPlayer = new Map<string, PlayerBinding>();
const userBySocket = new Map<string, string>();
const socketByUser = new Map<string, PondUserBinding>();

function logSessionRebound(fields: Record<string, unknown>): void {
  void import('./fishingObservability.js').then(({ logStructuredEvent }) => {
    logStructuredEvent('session_rebound', 'session_rebound', fields);
  });
}

export function bindPlayer(playerId: string, socketId: string, correlationId?: string): void {
  const prev = socketByPlayer.get(playerId);
  if (prev && prev.socketId !== socketId) {
    logSessionRebound({
      playerId,
      socketId,
      previousSocketId: prev.socketId,
      reason: 'session_registry_bind_player',
      correlationId,
    });
    playerBySocket.delete(prev.socketId);
  }
  playerBySocket.set(socketId, playerId);
  socketByPlayer.set(playerId, { socketId, correlationId });
  socketConnectionsGauge.set(playerBySocket.size);
}

export function bindPondUser(userId: string, socketId: string, pondId: string): void {
  const prev = socketByUser.get(userId);
  if (prev && prev.socketId !== socketId) {
    userBySocket.delete(prev.socketId);
  }
  userBySocket.set(socketId, userId);
  socketByUser.set(userId, { socketId, pondId });
}

export function setCorrelationId(playerId: string, correlationId: string): void {
  const current = socketByPlayer.get(playerId);
  if (!current) return;
  socketByPlayer.set(playerId, { ...current, correlationId });
}

export function unbindSocket(socketId: string): void {
  const playerId = playerBySocket.get(socketId);
  if (playerId) {
    playerBySocket.delete(socketId);
    const current = socketByPlayer.get(playerId);
    if (current?.socketId === socketId) {
      socketByPlayer.delete(playerId);
    }
    socketConnectionsGauge.set(playerBySocket.size);
  }
  const userId = userBySocket.get(socketId);
  if (userId) {
    userBySocket.delete(socketId);
    const current = socketByUser.get(userId);
    if (current?.socketId === socketId) {
      socketByUser.delete(userId);
    }
  }
}

export function resolveBySocket(socketId: string): { playerId?: string; userId?: string } {
  return {
    playerId: playerBySocket.get(socketId),
    userId: userBySocket.get(socketId),
  };
}

export function resolveByUser(userId: string): PondUserBinding | undefined {
  return socketByUser.get(userId);
}

export function resolveSocketByPlayer(playerId: string): string | undefined {
  return socketByPlayer.get(playerId)?.socketId;
}

export function resolveSocketByUser(userId: string): string | undefined {
  return socketByUser.get(userId)?.socketId;
}

export function resolveCorrelationIdBySocket(socketId: string): string | undefined {
  const playerId = playerBySocket.get(socketId);
  if (!playerId) return undefined;
  return socketByPlayer.get(playerId)?.correlationId;
}

export function resolveCorrelationIdByPlayer(playerId: string): string | undefined {
  return socketByPlayer.get(playerId)?.correlationId;
}

/** Bound authenticated humans (bots do not use this registry). */
export function getBoundHumanSocketCount(): number {
  return socketByPlayer.size;
}

export function isPlayerSocketBound(playerId: string): boolean {
  return socketByPlayer.has(playerId);
}

/** Test helper — clear all bindings */
export function resetSessionRegistryForTests(): void {
  playerBySocket.clear();
  socketByPlayer.clear();
  userBySocket.clear();
  socketByUser.clear();
  socketConnectionsGauge.set(0);
}
