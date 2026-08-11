import type { Express, Request, Response, NextFunction } from 'express';
import { httpRequestCounter, httpRequestDurationHistogram } from './metricsPrometheus.js';

function normalizePath(req: Request): string {
  const p = req.path || req.url.split('?')[0] || '/';
  if (p.startsWith('/api/admin/')) return '/api/admin/*';
  if (p.startsWith('/api/inventory/')) return '/api/inventory/:playerId';
  return p;
}

export function registerHttpMetricsMiddleware(app: Express): void {
  app.use((req: Request, res: Response, next: NextFunction) => {
    const startedAt = Date.now();
    const pathLabel = normalizePath(req);
    res.on('finish', () => {
      const durationMs = Date.now() - startedAt;
      const labels = { method: req.method, path: pathLabel, status: String(res.statusCode) };
      httpRequestCounter.inc(labels);
      httpRequestDurationHistogram.observe({ method: req.method, path: pathLabel }, durationMs);
    });
    next();
  });
}
