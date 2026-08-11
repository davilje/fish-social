import { randomUUID } from 'crypto';
import type { Server } from 'socket.io';
import type { ClientToServerEvents, ServerToClientEvents } from '@fish-social/shared';
import { getSession, getUserById, enrichPondUser, clearSession, removeDisconnectedUser } from './gameState.js';
import { ensurePlayer } from './players.js';
import { isAuthDisabled } from './auth.js';
import { handleDisconnect } from './fishingStateMachine.js';
import { logStructuredEvent, recordStructuredMetric, runWithCorrelationId } from './fishingObservability.js';
import { registerSocketPondHandlers } from './socketPondHandlers.js';
import { bindPlayer, setCorrelationId, unbindSocket } from './sessionRegistry.js';
import { cancelBySocket, cancelByUser } from './timerRegistry.js';
import { registerSocketEventTap } from './socketEventTap.js';
import { withTraceSpan } from './otelTracing.js';
import { MAX_SOCKET_CONNECTIONS } from './securityMiddleware.js';
import { clearSocketEventRate } from './socketEventRateLimit.js';

interface LifecycleDeps {
  io: Server<ClientToServerEvents, ServerToClientEvents>;
  roomFanoutCount: (pondId: string) => number;
}

export function resolveSocketPlayerId(
  socket: import('socket.io').Socket<ClientToServerEvents, ServerToClientEvents>,
  payloadPlayerId?: string,
): string | null {
  const authPlayerId = socket.data.authPlayerId as string | undefined;
  if (isAuthDisabled()) return authPlayerId ?? payloadPlayerId ?? null;
  if (!authPlayerId) return null;
  if (payloadPlayerId && payloadPlayerId !== authPlayerId) {
    logStructuredEvent('auth', 'identity_mismatch', {
      authPlayerId,
      bodyPlayerId: payloadPlayerId,
      socketId: socket.id,
      reason: 'socket_payload_mismatch',
    });
  }
  return authPlayerId;
}

export function registerSocketLifecycle({ io, roomFanoutCount }: LifecycleDeps): void {
  io.on('connection', (socket) => {
    if (io.engine.clientsCount > MAX_SOCKET_CONNECTIONS) {
      logStructuredEvent('security', 'socket_connection_rejected', {
        reason: 'max_connections',
        maxConnections: MAX_SOCKET_CONNECTIONS,
        currentCount: io.engine.clientsCount,
        socketId: socket.id,
      });
      socket.disconnect(true);
      return;
    }

    socket.data.correlationId = randomUUID();
    const correlationId = socket.data.correlationId as string;
    const wrap = <T extends unknown[]>(fn: (...args: T) => void) => (...args: T) =>
      runWithCorrelationId(correlationId, () => fn(...args));

    logStructuredEvent('socket_connect', 'socket_connect', {
      socketId: socket.id,
      reason: 'socket_connected',
      correlationId,
    });
    recordStructuredMetric('socket_connect', {
      socketId: socket.id,
      reason: 'socket_connected',
      correlationId,
    });

    registerSocketEventTap(socket, correlationId);

    socket.on('register_player', wrap((playerId) => {
      const authPlayerId = resolveSocketPlayerId(socket, playerId);
      if (!authPlayerId) return;
      ensurePlayer(authPlayerId, '钓友');
      bindPlayer(authPlayerId, socket.id, correlationId);
      setCorrelationId(authPlayerId, correlationId);
    }));

    registerSocketPondHandlers(socket, {
      io,
      resolveSocketPlayerId,
      roomFanoutCount,
    });

    socket.on('disconnect', wrap(() => {
      clearSocketEventRate(socket.id);
      withTraceSpan('socket.disconnect', correlationId, { socketId: socket.id }, () => {
      const session = getSession(socket.id);
      unbindSocket(socket.id);
      if (!session) return;
      cancelByUser(session.userId);
      cancelBySocket(socket.id);
      handleDisconnect(session.pondId, session.userId, () => {
        const removed = removeDisconnectedUser(session.pondId, session.userId);
        if (removed) {
          io.to(session.pondId).emit('pond_user_left', removed.id);
          logStructuredEvent('socket_broadcast_fanout', 'socket_broadcast_fanout', {
            eventType: 'socket_broadcast_fanout',
            channel: 'pond_user_left',
            pondId: session.pondId,
            fanoutCount: roomFanoutCount(session.pondId),
          });
        }
      });
      clearSession(socket.id);

      const disconnectedUser = getUserById(session.pondId, session.userId);
      if (disconnectedUser) io.to(session.pondId).emit('pond_user_updated', enrichPondUser(disconnectedUser));
      });
    }));
  });

  io.engine.on('connection_error', (err) => {
    logStructuredEvent('socket_connect', 'socket_connect_error', {
      socketId: err.req?._query?.sid,
      reason: err.message,
      code: err.code,
    });
    recordStructuredMetric('socket_connect_error', {
      socketId: err.req?._query?.sid,
      reason: err.message,
      code: err.code,
    });
  });
}
