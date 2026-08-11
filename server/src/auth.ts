import { createHmac, timingSafeEqual } from 'crypto';
import type { NextFunction, Request, Response } from 'express';
import { logStructuredEvent } from './fishingObservability.js';

const TOKEN_TTL_SEC = 7 * 24 * 60 * 60;

function base64UrlEncode(data: string): string {
  return Buffer.from(data).toString('base64url');
}

function base64UrlDecode(data: string): string {
  return Buffer.from(data, 'base64url').toString('utf8');
}

export function isAuthDisabled(): boolean {
  return process.env.NODE_ENV === 'development' && process.env.AUTH_DISABLED === '1';
}

export function getJwtSecret(): string {
  const secret = process.env.JWT_SECRET;
  if (!secret && !isAuthDisabled()) {
    throw new Error('JWT_SECRET is required (set AUTH_DISABLED=1 in development to bypass)');
  }
  return secret ?? 'dev-auth-disabled-placeholder';
}

export function assertAuthConfigured(): void {
  if (!isAuthDisabled()) {
    getJwtSecret();
  }
}

export function signPlayerToken(playerId: string): string {
  const header = base64UrlEncode(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const exp = Math.floor(Date.now() / 1000) + TOKEN_TTL_SEC;
  const payload = base64UrlEncode(JSON.stringify({ playerId, exp }));
  const secret = getJwtSecret();
  const sig = createHmac('sha256', secret).update(`${header}.${payload}`).digest('base64url');
  return `${header}.${payload}.${sig}`;
}

export function verifyPlayerToken(token: string | undefined | null): { playerId: string } | null {
  if (!token || typeof token !== 'string') return null;
  const parts = token.split('.');
  if (parts.length !== 3) return null;
  const [header, payloadB64, sig] = parts;
  const secret = getJwtSecret();
  const expected = createHmac('sha256', secret).update(`${header}.${payloadB64}`).digest('base64url');
  const sigBuf = Buffer.from(sig);
  const expBuf = Buffer.from(expected);
  if (sigBuf.length !== expBuf.length || !timingSafeEqual(sigBuf, expBuf)) return null;
  try {
    const payload = JSON.parse(base64UrlDecode(payloadB64)) as { playerId?: string; exp?: number };
    if (!payload.playerId || typeof payload.exp !== 'number') return null;
    if (payload.exp < Math.floor(Date.now() / 1000)) return null;
    return { playerId: payload.playerId };
  } catch {
    return null;
  }
}

function extractBearerToken(req: Request): string | undefined {
  const auth = req.header('Authorization');
  if (auth?.startsWith('Bearer ')) return auth.slice(7);
  const bodyToken = (req.body as { token?: string } | undefined)?.token;
  if (typeof bodyToken === 'string') return bodyToken;
  const queryToken =
    req.query && typeof req.query.token === 'string' ? req.query.token : undefined;
  return queryToken;
}

export function requireAuth(req: Request, res: Response, next: NextFunction): void {
  // Prefer Bearer JWT even when AUTH_DISABLED — v0.6 like/comment clients
  // send Authorization only (empty body). Legacy body/query playerId remains a fallback.
  const token = extractBearerToken(req);
  const payload = verifyPlayerToken(token);
  if (payload) {
    req.authPlayerId = payload.playerId;
    next();
    return;
  }

  if (isAuthDisabled()) {
    const fromBody = (req.body as { playerId?: string } | undefined)?.playerId;
    const fromQuery = typeof req.query.playerId === 'string' ? req.query.playerId : undefined;
    const playerId = fromBody ?? fromQuery;
    if (playerId) {
      req.authPlayerId = playerId;
      next();
      return;
    }
    logStructuredEvent('auth', 'auth_failed', {
      reason: 'auth_disabled_missing_identity',
      path: req.path,
      method: req.method,
    });
    res.status(401).json({ error: 'unauthorized' });
    return;
  }

  logStructuredEvent('auth', 'auth_failed', {
    reason: 'invalid_http_token',
    path: req.path,
    method: req.method,
  });
  res.status(401).json({ error: 'unauthorized' });
}

export function resolveAuthedPlayerId(
  req: Request,
  bodyPlayerId?: string,
): string | null {
  const authPlayerId = req.authPlayerId;
  if (!authPlayerId) return null;
  if (bodyPlayerId && bodyPlayerId !== authPlayerId && !isAuthDisabled()) {
    logStructuredEvent('auth', 'identity_mismatch', {
      authPlayerId,
      bodyPlayerId,
      path: req.path,
      method: req.method,
    });
  }
  return authPlayerId;
}

/** Optional viewer id for public reads (wall / likes list); never 401. */
export function tryResolvePlayerId(req: Request): string | undefined {
  const token = extractBearerToken(req);
  const fromJwt = verifyPlayerToken(token)?.playerId;
  if (fromJwt) return fromJwt;
  if (isAuthDisabled()) {
    const fromBody = (req.body as { playerId?: string } | undefined)?.playerId;
    const fromQuery = typeof req.query.playerId === 'string' ? req.query.playerId : undefined;
    return fromBody ?? fromQuery ?? req.authPlayerId;
  }
  return undefined;
}

/**
 * BE-OPT-A: requireAuth + path/query subject must match token.playerId.
 * AUTH_DISABLED: falls back to path playerId when no query/body id.
 */
export function requireSelf(
  paramName: string = 'playerId',
): (req: Request, res: Response, next: NextFunction) => void {
  return (req, res, next) => {
    requireAuth(req, res, () => {
      const pathId = req.params[paramName];
      if (isAuthDisabled()) {
        if (!req.authPlayerId && pathId) req.authPlayerId = pathId;
        next();
        return;
      }
      if (!req.authPlayerId) {
        res.status(401).json({ error: 'unauthorized' });
        return;
      }
      if (pathId && pathId !== req.authPlayerId) {
        logStructuredEvent('auth', 'identity_mismatch', {
          authPlayerId: req.authPlayerId,
          pathPlayerId: pathId,
          path: req.path,
          method: req.method,
          reason: 'require_self',
        });
        res.status(403).json({ error: 'forbidden', code: 'identity_mismatch' });
        return;
      }
      next();
    });
  };
}

declare global {
  namespace Express {
    interface Request {
      authPlayerId?: string;
    }
  }
}
