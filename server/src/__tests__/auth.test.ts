import { describe, it, expect } from 'vitest';
import type { Request, Response } from 'express';

process.env.JWT_SECRET = process.env.JWT_SECRET ?? 'vitest-jwt-secret';
delete process.env.AUTH_DISABLED;

import {
  signPlayerToken,
  verifyPlayerToken,
  isAuthDisabled,
  requireAuth,
  tryResolvePlayerId,
} from '../auth.js';

describe('auth', () => {
  it('signPlayerToken → verifyPlayerToken round-trip', () => {
    const token = signPlayerToken('player-a');
    const payload = verifyPlayerToken(token);
    expect(payload).toEqual({ playerId: 'player-a' });
  });

  it('rejects missing / malformed token', () => {
    expect(verifyPlayerToken(undefined)).toBeNull();
    expect(verifyPlayerToken('')).toBeNull();
    expect(verifyPlayerToken('not.a.jwt')).toBeNull();
  });

  it('rejects tampered signature', () => {
    const token = signPlayerToken('player-b');
    const parts = token.split('.');
    parts[2] = parts[2]!.replace(/./, (c) => (c === 'a' ? 'b' : 'a'));
    expect(verifyPlayerToken(parts.join('.'))).toBeNull();
  });

  it('isAuthDisabled only in development with AUTH_DISABLED=1', () => {
    const prevNode = process.env.NODE_ENV;
    const prevAuth = process.env.AUTH_DISABLED;
    try {
      process.env.NODE_ENV = 'development';
      process.env.AUTH_DISABLED = '1';
      expect(isAuthDisabled()).toBe(true);
      process.env.NODE_ENV = 'production';
      expect(isAuthDisabled()).toBe(false);
    } finally {
      process.env.NODE_ENV = prevNode;
      if (prevAuth === undefined) delete process.env.AUTH_DISABLED;
      else process.env.AUTH_DISABLED = prevAuth;
    }
  });

  it('AUTH_DISABLED: requireAuth accepts Bearer JWT (empty body)', () => {
    const prevNode = process.env.NODE_ENV;
    const prevAuth = process.env.AUTH_DISABLED;
    try {
      process.env.NODE_ENV = 'development';
      process.env.AUTH_DISABLED = '1';
      const token = signPlayerToken('p_jwt_user');
      const req = {
        header: (name: string) =>
          name === 'Authorization' ? `Bearer ${token}` : undefined,
        body: {},
        query: {},
        path: '/api/posts/x/like',
        method: 'POST',
      } as unknown as Request;
      let status: number | undefined;
      const res = {
        status(code: number) {
          status = code;
          return { json: () => res };
        },
        json: () => res,
      } as unknown as Response;
      let nextCalled = false;
      requireAuth(req, res, () => {
        nextCalled = true;
      });
      expect(nextCalled).toBe(true);
      expect(status).toBeUndefined();
      expect(req.authPlayerId).toBe('p_jwt_user');
      expect(tryResolvePlayerId(req)).toBe('p_jwt_user');
    } finally {
      process.env.NODE_ENV = prevNode;
      if (prevAuth === undefined) delete process.env.AUTH_DISABLED;
      else process.env.AUTH_DISABLED = prevAuth;
    }
  });

  it('AUTH_DISABLED: requireAuth without JWT or playerId → 401', () => {
    const prevNode = process.env.NODE_ENV;
    const prevAuth = process.env.AUTH_DISABLED;
    try {
      process.env.NODE_ENV = 'development';
      process.env.AUTH_DISABLED = '1';
      const req = {
        header: () => undefined,
        body: {},
        query: {},
        path: '/api/posts/x/like',
        method: 'POST',
      } as unknown as Request;
      let status: number | undefined;
      const res = {
        status(code: number) {
          status = code;
          return { json: () => res };
        },
        json: () => res,
      } as unknown as Response;
      let nextCalled = false;
      requireAuth(req, res, () => {
        nextCalled = true;
      });
      expect(nextCalled).toBe(false);
      expect(status).toBe(401);
    } finally {
      process.env.NODE_ENV = prevNode;
      if (prevAuth === undefined) delete process.env.AUTH_DISABLED;
      else process.env.AUTH_DISABLED = prevAuth;
    }
  });
});
