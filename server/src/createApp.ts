import cors from 'cors';
import express, { type Express } from 'express';
import path from 'path';
import { randomUUID } from 'crypto';
import type { Server } from 'socket.io';
import { PONDS, WORLD_POND_REGIONS, type ClientToServerEvents, type ServerToClientEvents } from '@fish-social/shared';
import { getInventory } from './inventory.js';
import { ensurePlayer } from './players.js';
import { requireAuth, requireSelf, signPlayerToken } from './auth.js';
import { listPondOccupancy } from './gameState.js';
import { registerSocialRoutes } from './socialRoutes.js';
import { registerShopRoutes } from './shop.js';
import { registerAdminRoutes } from './admin.js';
import { registerForbiddenPoliceRoutes } from './forbiddenPolice.js';
import { runWithCorrelationId } from './fishingObservability.js';
import { getPendingMetricsCount } from './fishingMetrics.js';
import { getMetricsContent } from './metricsPrometheus.js';
import { registerHttpMetricsMiddleware } from './httpMetricsMiddleware.js';
import { registerSecurityMiddleware, requireLocalhostDevToken } from './securityMiddleware.js';
import { db } from './db.js';
import { getCapacitySnapshot } from './humanCapacity.js';
import { loginWithSteamTicket, SteamAuthError } from './steamAuth.js';

let startedAt = Date.now();
let shuttingDown = false;

export function isShuttingDown(): boolean {
  return shuttingDown;
}
export function setShuttingDown(v: boolean): void {
  shuttingDown = v;
}

function resolveAllowedOrigins(): string[] {
  return (process.env.ALLOWED_ORIGINS ?? '')
    .split(',')
    .map((origin) => origin.trim())
    .filter(Boolean);
}

export function getCorsOriginPolicy(): string[] | '*' {
  const env = process.env.NODE_ENV;
  const origins = resolveAllowedOrigins();
  if (origins.length === 0) {
    if (env === 'development') return '*';
    throw new Error('ALLOWED_ORIGINS is required in production');
  }
  if (env === 'production' && origins.includes('*')) {
    throw new Error('ALLOWED_ORIGINS cannot include "*" in production');
  }
  return origins.length === 1 && origins[0] === '*' ? '*' : origins;
}

export function createApp(
  projectRoot: string,
  io: Server<ClientToServerEvents, ServerToClientEvents>,
): Express {
  const app = express();
  const corsOrigins = getCorsOriginPolicy();
  app.use(
    cors({
      origin: corsOrigins === '*' ? true : corsOrigins,
      credentials: true,
    }),
  );
  app.use(express.json());
  registerSecurityMiddleware(app);
  registerHttpMetricsMiddleware(app);
  app.use((req, _res, next) => {
    const requestId = req.header('X-Request-Id') ?? randomUUID();
    req.correlationId = requestId;
    runWithCorrelationId(requestId, next);
  });
  app.use('/image/profile', express.static(path.join(projectRoot, 'image/profile')));
  app.use('/image/fishing_photos', express.static(path.join(projectRoot, 'image/fishing_photos')));

  // 运维平台静态页 + Admin 运维台（与 API 同端口 :3001）
  // QUAL-07: OPS_STATIC_ENABLED=false 时不挂载 /planning /ops /analytics 等（公网生产建议关闭）
  const opsStaticEnabled = (() => {
    const v = process.env.OPS_STATIC_ENABLED;
    if (v === '0' || v === 'false') return false;
    if (v === '1' || v === 'true') return true;
    return true; // default on for local/dev; production may set false
  })();

  if (opsStaticEnabled) {
  // 根目录中文文件名用 ASCII 别名 + decode 匹配，避免 Express 对百分号编码路径匹配失败
  const opsDocsDir = path.join(projectRoot, 'docs', 'ops');
  const analyticsDir = path.join(projectRoot, 'docs', 'analytics');
  const planningDir = path.join(projectRoot, 'docs', 'planning');
  const rootPortalHtml = path.join(projectRoot, '运营平台.html');
  const rootBoardHtml = path.join(projectRoot, '策划进度看板.html');
  const rootPlanXlsx = path.join(projectRoot, '项目开发需求计划表.xlsx');
  const rootFlowHtml = path.join(projectRoot, '开发流程说明.html');
  const rootPreviewHtml = path.join(projectRoot, 'preview.html');

  const rootDeliverables: Record<string, string> = {
    '/运营平台.html': rootPortalHtml,
    '/策划进度看板.html': rootBoardHtml,
    '/项目开发需求计划表.xlsx': rootPlanXlsx,
    '/开发流程说明.html': rootFlowHtml,
    '/preview.html': rootPreviewHtml,
    '/ops/portal.html': rootPortalHtml,
    '/ops/board.html': rootBoardHtml,
    '/ops/plan.xlsx': rootPlanXlsx,
  };

  app.get(['/ops', '/ops/'], (_req, res) => {
    res.sendFile(rootPortalHtml);
  });
  app.use((req, res, next) => {
    let decoded = req.path;
    try {
      decoded = decodeURIComponent(req.path);
    } catch {
      /* keep raw */
    }
    const file = rootDeliverables[decoded] ?? rootDeliverables[req.path];
    if (!file) {
      next();
      return;
    }
    res.sendFile(file, (err) => {
      if (err) next();
    });
  });
  app.use('/ops', express.static(opsDocsDir));
  app.use('/docs/ops', express.static(opsDocsDir));
  app.use('/analytics', express.static(analyticsDir));
  app.use('/planning', express.static(planningDir));
  }

  const adminWebDist = path.join(projectRoot, 'admin-web', 'dist');
  app.use('/admin-web', express.static(adminWebDist));
  app.get(['/admin-web', '/admin-web/'], (_req, res, next) => {
    res.sendFile(path.join(adminWebDist, 'index.html'), (err) => {
      if (err) next();
    });
  });

  // STAB-06: draining → 503 (align with /ready — not schedulable during shutdown)
  app.get('/health', (_req, res) => {
    if (shuttingDown) {
      res.status(503).json({ ok: false, draining: true });
      return;
    }
    res.json({ ok: true, uptimeSec: Math.floor((Date.now() - startedAt) / 1000), version: '0.1.0' });
  });

  app.get('/ready', (_req, res) => {
    if (shuttingDown) {
      res.status(503).json({ ok: false, draining: true });
      return;
    }
    let dbOk = 'ok';
    try {
      db.prepare('SELECT 1').get();
    } catch {
      dbOk = 'error';
    }
    const mem = process.memoryUsage();
    const capacity = getCapacitySnapshot();
    res.json({
      ok: true,
      db: dbOk,
      metricsQueueDepth: getPendingMetricsCount(),
      memoryMb: { rss: Math.round(mem.rss / 1024 / 1024), heapUsed: Math.round(mem.heapUsed / 1024 / 1024) },
      humanSocketCount: capacity.humanSocketCount,
      botSocketCount: capacity.botSocketCount,
      botInPondCount: capacity.botInPondCount,
      humanInPond: capacity.humanInPond,
      capacityLimit: capacity.capacityLimit,
    });
  });

  // Public world map (SEC §2.1)
  app.get('/api/world', (_req, res) => {
    res.json({
      regions: WORLD_POND_REGIONS,
      ponds: PONDS.map((p) => ({ id: p.id, name: p.name, regionId: p.regionId })),
      occupancy: listPondOccupancy(),
    });
  });

  // SEC-03: inventory is private
  app.get('/api/inventory/:playerId', requireSelf('playerId'), (req, res) => {
    res.json({ items: getInventory(req.params.playerId) });
  });

  if (process.env.NODE_ENV === 'development') {
    app.post('/api/auth/dev-token', requireLocalhostDevToken, (req, res) => {
      const { playerId } = req.body as { playerId?: string };
      if (!playerId) return res.status(400).json({ error: 'missing playerId' });
      ensurePlayer(playerId, '钓友');
      res.json({ token: signPlayerToken(playerId) });
    });
  }

  app.post('/api/auth/steam', async (req, res) => {
    const body = req.body as { ticket?: unknown; appId?: unknown; identity?: unknown };
    try {
      const result = await loginWithSteamTicket(
        body.ticket,
        body.appId,
        undefined,
        req.ip ?? req.socket.remoteAddress ?? 'unknown',
        body.identity,
      );
      res.json({ ok: true, ...result });
    } catch (error) {
      if (error instanceof SteamAuthError) {
        res.status(error.status).json({ ok: false, error: error.message, code: error.code });
        return;
      }
      res.status(500).json({ ok: false, error: 'Steam 登录失败', code: 'STEAM_INTERNAL_ERROR' });
    }
  });

  // Prometheus metrics endpoint (default disabled, port 3002)
  if (process.env.METRICS_PROMETHEUS_ENABLED === 'true') {
    app.get('/metrics', async (_req, res) => {
      try {
        const metrics = await getMetricsContent();
        res.set('Content-Type', 'text/plain; charset=utf-8');
        res.send(metrics);
      } catch (e) {
        res.status(500).send('Error collecting metrics');
      }
    });
  }

  // SEC-05: client logs require auth; batch hard cap
  const CLIENT_LOGS_MAX = 50;
  app.post('/api/client-logs', requireAuth, (req, res) => {
    try {
      const authPlayerId = req.authPlayerId;
      if (!authPlayerId) {
        res.status(401).json({ error: 'unauthorized' });
        return;
      }
      const { logs } = req.body as {
        logs?: Array<{
          playerId?: string;
          ts: number;
          level: string;
          eventType: string;
          fields?: Record<string, unknown>;
        }>;
      };
      if (!logs || !Array.isArray(logs) || logs.length === 0) {
        res.status(400).json({ error: '缺少 logs 数组' });
        return;
      }
      if (logs.length > CLIENT_LOGS_MAX) {
        res.status(400).json({ error: 'logs 批次过大', code: 'logs_batch_limit', max: CLIENT_LOGS_MAX });
        return;
      }
      const insertStmt = db.prepare(`
        INSERT INTO client_logs (id, player_id, ts, level, event_type, fields, created_at)
        VALUES (@id, @playerId, @ts, @level, @eventType, @fields, @createdAt)
      `);
      const tx = db.transaction((rows: Array<Record<string, unknown>>) => {
        for (const row of rows) insertStmt.run(row);
      });
      const rows = logs.map((log) => ({
        id: randomUUID(),
        playerId: authPlayerId,
        ts: log.ts ?? Date.now(),
        level: log.level ?? 'info',
        eventType: log.eventType ?? 'client_log',
        fields: JSON.stringify(log.fields ?? {}),
        createdAt: Date.now(),
      }));
      tx(rows);
      res.json({ ok: true, count: rows.length });
    } catch (e) {
      res.status(500).json({ error: '写入客户端日志失败' });
    }
  });

  registerSocialRoutes(app, io);
  registerShopRoutes(app, io);
  registerForbiddenPoliceRoutes(app);
  registerAdminRoutes(app);

  return app;
}

declare global {
  namespace Express {
    interface Request {
      correlationId?: string;
    }
  }
}
