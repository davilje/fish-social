import type { Socket } from 'socket.io';
import { logStructuredEvent } from './fishingObservability.js';
import { isAuthDisabled, verifyPlayerToken } from './auth.js';

export interface SocketAuthData {
  authPlayerId?: string;
}

export function socketAuthMiddleware(
  socket: Pick<Socket, 'handshake' | 'id' | 'data'>,
  next: (error?: Error) => void,
): void {
  if (isAuthDisabled()) {
    next();
    return;
  }
  const token = socket.handshake.auth?.token as string | undefined;
  const payload = verifyPlayerToken(token);
  if (!payload) {
    logStructuredEvent('auth', 'auth_failed', {
      reason: 'invalid_socket_token',
      socketId: socket.id,
    });
    next(new Error('unauthorized'));
    return;
  }
  socket.data.authPlayerId = payload.playerId;
  next();
}

