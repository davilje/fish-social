import rateLimit from 'express-rate-limit';
import type { Express, Request, Response, NextFunction } from 'express';
import { logStructuredEvent } from './fishingObservability.js';

export const MAX_SOCKET_CONNECTIONS = Number(process.env.MAX_SOCKET_CONNECTIONS ?? 200);

export function isLocalhostRequest(req: Request): boolean {
  const ip = (req.ip ?? req.socket.remoteAddress ?? '').replace('::ffff:', '');
  return ip === '127.0.0.1' || ip === '::1' || ip === 'localhost';
}

export function registerSecurityMiddleware(app: Express): void {
  const globalLimiter = rateLimit({
    windowMs: 1000,
    max: Number(process.env.RATE_LIMIT_GLOBAL_PER_SEC ?? 100),
    standardHeaders: true,
    legacyHeaders: false,
    handler: (req, res) => {
      logStructuredEvent('security', 'rate_limit_exceeded', {
        scope: 'global',
        path: req.path,
        ip: req.ip,
        correlationId: req.correlationId,
      });
      res.status(429).json({ error: '请求过于频繁，请稍后重试' });
    },
  });

  const authLimiter = rateLimit({
    windowMs: 60_000,
    max: Number(process.env.RATE_LIMIT_AUTH_PER_MIN ?? 10),
    standardHeaders: true,
    legacyHeaders: false,
    handler: (req, res) => {
      logStructuredEvent('security', 'rate_limit_exceeded', {
        scope: 'auth',
        path: req.path,
        ip: req.ip,
        correlationId: req.correlationId,
      });
      res.status(429).json({ error: '鉴权请求过于频繁' });
    },
  });

  app.use(globalLimiter);
  app.use('/api/auth', authLimiter);
}

export function requireLocalhostDevToken(req: Request, res: Response, next: NextFunction): void {
  if (!isLocalhostRequest(req)) {
    logStructuredEvent('security', 'dev_token_rejected', {
      ip: req.ip,
      path: req.path,
      reason: 'non_localhost',
    });
    res.status(403).json({ error: 'dev-token 仅允许 localhost 访问' });
    return;
  }
  next();
}
