import type { Express, Request, Response, NextFunction } from 'express';
import { db } from './db.js';
import { clearAllDmData } from './dm.js';
import { clearErrorLogs, getErrorLogs, getErrorLogsSince, getErrorLogsByContext, logError, type ErrorLogEntry } from './errorLog.js';
import { clearAllFriendData } from './friends.js';
import { clearAllPosts } from './posts.js';
import {
  approveConfigChange,
  getConfigViews,
  listConfigurableKeys,
  listConfigAuditLog,
  listPendingConfigChanges,
  rejectConfigChange,
  submitConfigChange,
} from './gameConfig.js';
import { getFishingMetricsSummary, getPlayerFishingTimeline } from './fishingMetrics.js';
import { applyRuntimeConfigFromDb } from './runtimeConfig.js';
import {
  startDebugSampling, stopDebugSampling, listActiveTargets,
  listHistory, recordAudit
} from './debugSampler.js';
import { sendAlert } from './alertWebhook.js';
import { registerLiveSession, getActiveConnectionCount } from './liveSessionInspector.js';
import { extractAdminKey, requireRole } from './adminRbac.js';
import { getLokiEnabled } from './logTransportLoki.js';
import { getBusinessHealthTrend } from './businessHealth.js';
import { correlationToTraceId, isOtelEnabled, listSpansByCorrelationId } from './otelTracing.js';
import { buildPlayerExport, erasePlayerData, playerExists } from './playerPrivacy.js';
import { getGrayMetricsDashboard, rollbackConfigToPrevious } from './grayMetrics.js';
import { getPlayerLiveState } from './playerLiveState.js';
import { getCapacitySnapshot } from './humanCapacity.js';
import { registerAdminEcologyRoutes } from './adminEcologyRoutes.js';
import { getPlayersOverview } from './adminPlayersOverview.js';

const ADMIN_SECRET = process.env.ADMIN_SECRET ?? '';
const ADMIN_ROUTE_SLOW_MS = Number(process.env.ADMIN_ROUTE_SLOW_MS ?? 200);

export function assertAdminSecurityConfigured(): void {
  if (process.env.NODE_ENV === 'production' && !ADMIN_SECRET) {
    console.error('[startup] ADMIN_SECRET is required in production');
    process.exit(1);
  }
}

function logAdminAudit(action: string, req: Request, details?: Record<string, unknown>): void {
  console.log(
    `[admin_audit] ${JSON.stringify({
      ts: Date.now(),
      eventType: 'admin_audit',
      action,
      method: req.method,
      path: req.path,
      ip: req.ip,
      ...details,
    })}`,
  );
}

/** STAB-05: production REST uses Header only; query.key blocked unless ADMIN_ALLOW_QUERY_KEY */
function requireAdmin(req: Request, res: Response, next: NextFunction): void {
  const { key } = extractAdminKey(req);
  if (!key || key !== ADMIN_SECRET) {
    res.status(401).json({ error: '无效的管理员密钥' });
    return;
  }
  next();
}

export function registerAdminRoutes(app: Express): void {
  app.use('/api/admin', (req, res, next) => {
    const startedAt = Date.now();
    res.on('finish', () => {
      const durationMs = Date.now() - startedAt;
      const payload = {
        eventType: 'admin_route_duration_ms',
        method: req.method,
        path: req.path,
        status: res.statusCode,
        durationMs,
        ts: Date.now(),
      };
      console.log(`[admin_route_duration_ms] ${JSON.stringify(payload)}`);
      if (durationMs >= ADMIN_ROUTE_SLOW_MS) {
        console.log(
          `[admin_route_slow] ${JSON.stringify({
            ...payload,
            eventType: 'admin_route_slow',
            thresholdMs: ADMIN_ROUTE_SLOW_MS,
          })}`,
        );
      }
    });
    next();
  });

  app.get('/api/admin/status', requireAdmin, (_req, res) => {
    const capacity = getCapacitySnapshot();
    res.json({
      ok: true,
      adminEnabled: true,
      hint: '鱼塘生态数据已持久化到 SQLite，重启后自动恢复',
      ...capacity,
    });
  });

  registerAdminEcologyRoutes(app, { requireAdmin, logAdminAudit });

  app.get('/api/admin/players/overview', requireAdmin, (req, res) => {
    try {
      const hours = Number(req.query.hours) || 24;
      const humansOnly =
        req.query.humansOnly === undefined ||
        req.query.humansOnly === '1' ||
        req.query.humansOnly === 'true';
      const pondId = typeof req.query.pondId === 'string' ? req.query.pondId : undefined;
      const phase = typeof req.query.phase === 'string' ? req.query.phase : undefined;
      const q = typeof req.query.q === 'string' ? req.query.q : undefined;
      res.json(getPlayersOverview({ hours, humansOnly, pondId, phase, q }));
    } catch (e) {
      logError('admin players overview', e, 'admin');
      res.status(500).json({ error: '读取玩家一览失败' });
    }
  });

  app.get('/api/admin/players/:playerId/export', requireRole('admin'), (req, res) => {
    try {
      const playerId = String(req.params.playerId);
      if (!playerExists(playerId)) {
        res.status(404).json({ error: '玩家不存在' });
        return;
      }
      const bundle = buildPlayerExport(playerId);
      logAdminAudit('player_export', req, { playerId });
      recordAudit('admin', 'player_export', playerId, 'admin export', { exportedAt: bundle?.exportedAt });
      res.json(bundle);
    } catch (e) {
      logError('player export', e, 'admin');
      res.status(500).json({ error: '导出失败' });
    }
  });

  app.post('/api/admin/players/:playerId/erase', requireRole('admin'), (req, res) => {
    try {
      const playerId = String(req.params.playerId);
      const dryRun =
        req.query.dryRun === '1' ||
        req.query.dryRun === 'true' ||
        (req.body as { dryRun?: boolean })?.dryRun === true;

      if (!playerExists(playerId)) {
        res.status(404).json({ error: '玩家不存在' });
        return;
      }

      const result = erasePlayerData(playerId, { dryRun });
      logAdminAudit('player_erase', req, {
        playerId,
        dryRun,
        deletedTables: result.deletedTables,
        anonymizedRows: result.anonymizedRows,
      });
      recordAudit('admin', dryRun ? 'player_erase_dry_run' : 'player_erase', playerId, 'admin erase', {
        dryRun,
        deletedTables: result.deletedTables,
        anonymizedRows: result.anonymizedRows,
        anonymizedId: result.anonymizedId,
      });
      res.json(result);
    } catch (e) {
      logError('player erase', e, 'admin');
      res.status(500).json({ error: '删号失败' });
    }
  });

  app.post('/api/admin/users/clear', requireAdmin, (_req, res) => {
    try {
      logAdminAudit('users_clear', _req);
      const tx = db.transaction(() => {
        db.exec('DELETE FROM inventory');
        db.exec('DELETE FROM player_gear');
        db.exec('DELETE FROM daily_fishing');
        db.exec('DELETE FROM players');
        clearAllPosts();
        clearAllFriendData();
        clearAllDmData();
      });
      tx();
      res.json({
        ok: true,
        message: '已清理所有玩家数据（鱼塘生态数据保留）',
      });
    } catch (e) {
      logError('clear users', e, 'admin');
      res.status(500).json({ error: '清理失败' });
    }
  });

  app.get('/api/admin/logs', requireAdmin, (req, res) => {
    try {
      const limit = Math.min(200, Number(req.query.limit) || 100);
      const since = req.query.since ? Number(req.query.since) : undefined;
      const context = typeof req.query.context === 'string' ? req.query.context : undefined;
      const correlationId = typeof req.query.correlationId === 'string' ? req.query.correlationId : undefined;
      let logs;
      if (correlationId) {
        logs = db.prepare(`
          SELECT id, message, stack, context, correlation_id as correlationId, created_at as createdAt
          FROM error_logs
          WHERE correlation_id = ?
          ORDER BY created_at DESC
          LIMIT ?
        `).all(correlationId, limit) as ErrorLogEntry[];
      } else if (since) {
        logs = getErrorLogsSince(since, limit);
      } else if (context) {
        logs = getErrorLogsByContext(context, limit);
      } else {
        logs = getErrorLogs(limit);
      }
      res.json({ logs });
    } catch (e) {
      logError('admin logs query', e, 'admin');
      res.status(500).json({ error: '查询日志失败' });
    }
  });

  app.post('/api/admin/logs/clear', requireAdmin, (_req, res) => {
    try {
      clearErrorLogs();
      res.json({ ok: true, message: '已清空错误日志' });
    } catch (e) {
      logError('admin logs clear', e, 'admin');
      res.status(500).json({ error: '清空失败' });
    }
  });

  app.get('/api/admin/metrics/daily', requireAdmin, (req, res) => {
    try {
      const date = typeof req.query.date === 'string' ? req.query.date : undefined;
      const pondId = typeof req.query.pondId === 'string' ? req.query.pondId : undefined;
      let rows;
      if (date && pondId) {
        rows = db.prepare('SELECT * FROM daily_pond_stats WHERE date_key = ? AND pond_id = ?').all(date, pondId);
      } else if (date) {
        rows = db.prepare('SELECT * FROM daily_pond_stats WHERE date_key = ?').all(date);
      } else if (pondId) {
        rows = db.prepare('SELECT * FROM daily_pond_stats WHERE pond_id = ? ORDER BY date_key DESC').all(pondId);
      } else {
        rows = db.prepare('SELECT * FROM daily_pond_stats ORDER BY date_key DESC LIMIT 30').all();
      }
      res.json({ rows });
    } catch (e) {
      logError('admin daily metrics', e, 'admin');
      res.status(500).json({ error: '查询日聚合数据失败' });
    }
  });

  app.get('/api/admin/metrics/business-health', requireAdmin, (req, res) => {
    try {
      const days = Math.min(30, Number(req.query.days) || 7);
      res.json(getBusinessHealthTrend(days));
    } catch (e) {
      logError('admin business health', e, 'admin');
      res.status(500).json({ error: '查询业务健康看板失败' });
    }
  });

  app.get('/api/admin/metrics/gray-dashboard', requireAdmin, (req, res) => {
    try {
      const hours = Math.min(720, Number(req.query.hours) || 168);
      res.json(getGrayMetricsDashboard(hours));
    } catch (e) {
      logError('admin gray dashboard', e, 'admin');
      res.status(500).json({ error: '查询灰度指标看板失败' });
    }
  });

  app.post('/api/admin/config/rollback', requireAdmin, (req, res) => {
    try {
      const { key, operator } = req.body as { key?: string; operator?: string };
      if (!key || !operator) {
        res.status(400).json({ error: '缺少 key / operator' });
        return;
      }
      const result = rollbackConfigToPrevious(key, operator);
      if (!result.ok) {
        res.status(400).json({ error: result.error });
        return;
      }
      logAdminAudit('config_rollback', req, { key, operator });
      res.json(result);
    } catch (e) {
      logError('config rollback', e, 'admin');
      res.status(500).json({ error: '回滚失败' });
    }
  });

  app.get('/api/admin/traces', requireAdmin, (req, res) => {
    try {
      const correlationId = typeof req.query.correlationId === 'string' ? req.query.correlationId : undefined;
      if (!correlationId) {
        res.status(400).json({ error: '缺少 correlationId' });
        return;
      }
      const spans = listSpansByCorrelationId(correlationId);
      res.json({
        correlationId,
        traceId: correlationToTraceId(correlationId),
        otelEnabled: isOtelEnabled(),
        spans,
        count: spans.length,
      });
    } catch (e) {
      logError('admin traces', e, 'admin');
      res.status(500).json({ error: '查询 trace 失败' });
    }
  });

  app.get('/api/admin/config', requireAdmin, (_req, res) => {
    res.json({ keys: listConfigurableKeys(), entries: getConfigViews() });
  });

  app.get('/api/admin/config/pending', requireAdmin, (_req, res) => {
    res.json({ requests: listPendingConfigChanges() });
  });

  app.get('/api/admin/config/audit', requireAdmin, (req, res) => {
    const limit = Math.min(200, Number(req.query.limit) || 100);
    res.json({ audit: listConfigAuditLog(limit) });
  });

  app.post('/api/admin/config/submit', requireAdmin, (req, res) => {
    const { key, value, submittedBy } = req.body as {
      key?: string;
      value?: string;
      submittedBy?: string;
    };
    if (!key || value === undefined || !submittedBy) {
      res.status(400).json({ error: '缺少 key / value / submittedBy' });
      return;
    }
    const result = submitConfigChange(key, String(value), submittedBy);
    if (!result.ok) {
      res.status(400).json({ error: result.error });
      return;
    }
    res.json(result);
  });

  app.post('/api/admin/config/approve', requireAdmin, (req, res) => {
    const { requestId, approver } = req.body as { requestId?: string; approver?: string };
    if (!requestId || !approver) {
      res.status(400).json({ error: '缺少 requestId / approver' });
      return;
    }
    const result = approveConfigChange(requestId, approver);
    if (!result.ok) {
      res.status(400).json({ error: result.error });
      return;
    }
    res.json({ ok: true });
  });

  app.post('/api/admin/config/reject', requireAdmin, (req, res) => {
    const { requestId, approver } = req.body as { requestId?: string; approver?: string };
    if (!requestId || !approver) {
      res.status(400).json({ error: '缺少 requestId / approver' });
      return;
    }
    const result = rejectConfigChange(requestId, approver);
    if (!result.ok) {
      res.status(400).json({ error: result.error });
      return;
    }
    res.json({ ok: true });
  });

  app.post('/api/admin/config/refresh', requireAdmin, (_req, res) => {
    applyRuntimeConfigFromDb();
    res.json({ ok: true, entries: getConfigViews() });
  });

  app.get('/api/admin/metrics/fishing', requireAdmin, (req, res) => {
    const hours = Math.min(720, Number(req.query.hours) || 168);
    const correlationId = typeof req.query.correlationId === 'string' ? req.query.correlationId : undefined;
    if (correlationId) {
      const rows = db.prepare(`
        SELECT id, event_type, player_id, pond_id, payload, correlation_id, created_at
        FROM fishing_metrics
        WHERE correlation_id = ?
        ORDER BY created_at DESC
        LIMIT 200
      `).all(correlationId);
      // Return timeline context: 5 before and 5 after each match
      res.json({ correlationId, rows, count: rows.length });
    } else {
      res.json(getFishingMetricsSummary(hours));
    }
  });

  /** 单用户钓鱼事件时间线（挂机断线排查 SOP 步骤 1） */
  app.get('/api/admin/metrics/fishing/player/:playerId', requireAdmin, (req, res) => {
    try {
      const playerId = String(req.params.playerId);
      const hours = Math.min(720, Number(req.query.hours) || 24);
      const limit = Math.min(1000, Number(req.query.limit) || 500);
      res.json(getPlayerFishingTimeline(playerId, hours, limit));
    } catch (e) {
      logError('admin player fishing timeline', e, 'admin');
      res.status(500).json({ error: '读取玩家时间线失败' });
    }
  });

  // ======== P1-A2: Debug Sampling ========

  app.post('/api/admin/debug-sample/start', requireRole('operator'), (req, res) => {
    try {
      const { playerId, reason, ttlMs } = req.body as { playerId?: string; reason?: string; ttlMs?: number };
      if (!playerId) {
        res.status(400).json({ error: '缺少 playerId' });
        return;
      }
      const result = startDebugSampling(playerId, { reason, requestedBy: 'admin', ttlMs });
      logAdminAudit('debug_sample_start', req, { playerId, reason });
      res.json(result);
    } catch (e) {
      logError('debug sample start', e, 'admin');
      res.status(500).json({ error: '启动采样失败' });
    }
  });

  app.post('/api/admin/debug-sample/stop/:playerId', requireRole('operator'), (req, res) => {
    try {
      const playerId = String(req.params.playerId);
      const stopped = stopDebugSampling(playerId);
      if (stopped) {
        logAdminAudit('debug_sample_stop', req, { playerId });
        res.json({ ok: true, playerId });
      } else {
        res.status(404).json({ error: '未找到活跃采样目标' });
      }
    } catch (e) {
      logError('debug sample stop', e, 'admin');
      res.status(500).json({ error: '停止采样失败' });
    }
  });

  app.get('/api/admin/debug-sample/list', requireRole('viewer'), (_req, res) => {
    res.json({ targets: listActiveTargets() });
  });

  app.get('/api/admin/debug-sample/history', requireRole('viewer'), (_req, res) => {
    res.json({ history: listHistory() });
  });

  // ======== P1-D2: Client Logs ========

  app.get('/api/admin/client-logs', requireRole('viewer'), (req, res) => {
    try {
      const playerId = typeof req.query.playerId === 'string' ? req.query.playerId : undefined;
      const since = typeof req.query.since === 'string' ? Number(req.query.since) : undefined;
      const limit = Math.min(200, Number(req.query.limit) || 100);
      let logs;
      if (playerId && since) {
        logs = db.prepare('SELECT * FROM client_logs WHERE player_id = ? AND created_at >= ? ORDER BY created_at DESC LIMIT ?').all(playerId, since, limit);
      } else if (playerId) {
        logs = db.prepare('SELECT * FROM client_logs WHERE player_id = ? ORDER BY created_at DESC LIMIT ?').all(playerId, limit);
      } else if (since) {
        logs = db.prepare('SELECT * FROM client_logs WHERE created_at >= ? ORDER BY created_at DESC LIMIT ?').all(since, limit);
      } else {
        logs = db.prepare('SELECT * FROM client_logs ORDER BY created_at DESC LIMIT ?').all(limit);
      }
      res.json({ logs, count: logs.length });
    } catch (e) {
      logError('admin client logs', e, 'admin');
      res.status(500).json({ error: '查询客户端日志失败' });
    }
  });

  // ======== P1-D1: Live Session Inspector (SSE) ========

  app.get('/api/admin/players/:playerId/live-state', requireRole('viewer'), (req, res) => {
    try {
      const playerId = String(req.params.playerId);
      res.json(getPlayerLiveState(playerId));
    } catch (e) {
      logError('player live-state', e, 'admin');
      res.status(500).json({ error: '读取玩家实时状态失败' });
    }
  });

  app.get('/api/admin/live-session', requireRole('viewer'), (req, res) => {
    try {
      const playerId = typeof req.query.playerId === 'string' ? req.query.playerId : undefined;
      if (!playerId) {
        res.status(400).json({ error: '缺少 playerId 查询参数' });
        return;
      }
      const ok = registerLiveSession(playerId, res);
      if (!ok) {
        res.status(503).json({ error: '同时最多 5 个 Inspector 连接' });
        return;
      }
    } catch (e) {
      logError('live session', e, 'admin');
      res.status(500).json({ error: '启动实时会话失败' });
    }
  });

  // ======== P1-C3: Alert Test ========

  app.post('/api/admin/alert/test', requireRole('admin'), async (_req, res) => {
    try {
      const ok = await sendAlert({
        title: 'Fish Social 告警测试',
        message: '这是一条来自 Admin 的手动测试告警',
        severity: 'info',
        ruleName: 'manual_test',
        timestamp: Date.now(),
      });
      res.json({ ok, webhookConfigured: !!process.env.ALERT_WEBHOOK_URL });
    } catch (e) {
      logError('alert test', e, 'admin');
      res.status(500).json({ error: '发送测试告警失败' });
    }
  });

  // ======== P1-E1: RBAC Status ========

  app.get('/api/admin/rbac/status', requireRole('admin'), (_req, res) => {
    res.json({
      enabled: !!process.env.ADMIN_RBAC_RULES,
      backwardCompatible: !process.env.ADMIN_RBAC_RULES,
    });
  });

  // ======== Loki Status ========

  app.get('/api/admin/loki/status', requireRole('viewer'), (_req, res) => {
    res.json({
      enabled: getLokiEnabled(),
      host: process.env.LOKI_HOST ?? 'http://localhost:3100',
    });
  });
}
