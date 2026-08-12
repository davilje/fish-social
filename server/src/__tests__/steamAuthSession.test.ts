import { describe, expect, it } from 'vitest';
import type { Socket } from 'socket.io';
import { requireSelf, signPlayerToken } from '../auth.js';
import { socketAuthMiddleware } from '../socketAuth.js';

process.env.JWT_SECRET = process.env.JWT_SECRET ?? 'vitest-steam-session-secret';

function fakeSocket(token?: string): Pick<Socket, 'handshake' | 'id' | 'data'> {
  return {
    id: 'socket-test',
    handshake: { auth: token ? { token } : {} } as Socket['handshake'],
    data: {},
  };
}

describe('Steam JWT REST/Socket session boundary', () => {
  it('accepts REST Bearer access and rejects missing or mismatched identities', () => {
    const token = signPlayerToken('steam-rest-player');
    const makeRequest = (playerId: string, bearer?: string) => ({
      params: { playerId },
      header: (name: string) => name === 'Authorization' ? bearer : undefined,
      body: {},
      query: {},
      path: `/api/inventory/${playerId}`,
      method: 'GET',
    });
    const makeResponse = () => {
      let status: number | undefined;
      const res = {
        status(code: number) {
          status = code;
          return res;
        },
        json() {
          return res;
        },
        get statusCode() {
          return status;
        },
      };
      return res;
    };

    const okReq = makeRequest('steam-rest-player', `Bearer ${token}`);
    const okRes = makeResponse();
    let nextCalled = false;
    requireSelf('playerId')(okReq as never, okRes as never, () => { nextCalled = true; });
    expect(nextCalled).toBe(true);
    expect(okRes.statusCode).toBeUndefined();

    const mismatchReq = makeRequest('other-player', `Bearer ${token}`);
    const mismatchRes = makeResponse();
    requireSelf('playerId')(mismatchReq as never, mismatchRes as never, () => {});
    expect(mismatchRes.statusCode).toBe(403);

    const missingReq = makeRequest('steam-rest-player');
    const missingRes = makeResponse();
    requireSelf('playerId')(missingReq as never, missingRes as never, () => {});
    expect(missingRes.statusCode).toBe(401);
  });

  it('accepts a valid JWT and attaches the authoritative playerId', () => {
    const socket = fakeSocket(signPlayerToken('steam-session-player'));
    let error: Error | undefined;
    socketAuthMiddleware(socket, (nextError) => {
      error = nextError;
    });
    expect(error).toBeUndefined();
    expect(socket.data.authPlayerId).toBe('steam-session-player');
  });

  it('rejects missing and tampered JWTs', () => {
    for (const token of [undefined, 'not.a.jwt']) {
      const socket = fakeSocket(token);
      let error: Error | undefined;
      socketAuthMiddleware(socket, (nextError) => {
        error = nextError;
      });
      expect(error?.message).toBe('unauthorized');
      expect(socket.data.authPlayerId).toBeUndefined();
    }
  });
});

