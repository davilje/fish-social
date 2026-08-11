/**
 * QUAL-02: Admin ecology / pond inspection routes (split from admin.ts).
 */
import type { Express, RequestHandler } from 'express';
import type { BaitId, TackleId } from '@fish-social/shared';
import { getAdminPondOverview, listPondFishEntities, resetAllEcology } from './pondEcology.js';
import { listBotsInPond, listUsersInPond } from './gameState.js';
import { buildFishingDebugReport } from './fishingDebug.js';
import { logError } from './errorLog.js';
import { getCachedFishingDebug, setCachedFishingDebug } from './adminFishingDebugCache.js';

interface EcologyRouteDeps {
  requireAdmin: RequestHandler;
  logAdminAudit: (action: string, req: import('express').Request, details?: Record<string, unknown>) => void;
}

export function registerAdminEcologyRoutes(app: Express, deps: EcologyRouteDeps): void {
  const { requireAdmin, logAdminAudit } = deps;

  app.get('/api/admin/ponds', requireAdmin, (_req, res) => {
    try {
      const ponds = getAdminPondOverview().map((item) => {
        const users = listUsersInPond(item.pondId);
        const botCount = listBotsInPond(item.pondId).length;
        const botRatio = users.length > 0 ? botCount / users.length : 0;
        return {
          ...item,
          botCount,
          humanCount: Math.max(0, users.length - botCount),
          botRatio,
        };
      });
      res.json({ ponds });
    } catch (e) {
      logError('admin ponds', e, 'admin');
      res.status(500).json({ error: '读取鱼塘失败' });
    }
  });

  app.get('/api/admin/ponds/:pondId/fish', requireAdmin, (req, res) => {
    try {
      const pondId = String(req.params.pondId);
      const fish = listPondFishEntities(pondId);
      res.json({ pondId, fish, count: fish.length });
    } catch (e) {
      logError('admin pond fish', e, 'admin');
      res.status(500).json({ error: '读取鱼数据失败' });
    }
  });

  app.get('/api/admin/ponds/:pondId/fishing-debug', requireAdmin, (req, res) => {
    try {
      const pondId = String(req.params.pondId);
      const skipCache = req.query.refresh === '1';
      const baitId = typeof req.query.baitId === 'string' ? req.query.baitId : undefined;
      const tackleId = typeof req.query.tackleId === 'string' ? req.query.tackleId : undefined;
      const playerId = typeof req.query.playerId === 'string' ? req.query.playerId : undefined;
      const cacheKey = `${pondId}:${baitId ?? ''}:${tackleId ?? ''}:${playerId ?? ''}`;
      if (!skipCache) {
        const cached = getCachedFishingDebug(cacheKey);
        if (cached) {
          res.json(cached);
          return;
        }
      }
      const data = buildFishingDebugReport(pondId, {
        baitId: baitId as BaitId | undefined,
        tackleId: tackleId as TackleId | undefined,
        playerId,
      });
      setCachedFishingDebug(cacheKey, data);
      res.json(data);
    } catch (e) {
      logError('admin fishing debug', e, 'admin');
      res.status(500).json({ error: '读取钓鱼概率失败' });
    }
  });

  app.post('/api/admin/ecology/reset', requireAdmin, (_req, res) => {
    try {
      logAdminAudit('ecology_reset', _req);
      resetAllEcology();
      res.json({ ok: true, message: '所有鱼塘生态已重置并重新播种' });
    } catch (e) {
      logError('reset ecology', e, 'admin');
      res.status(500).json({ error: '重置失败' });
    }
  });
}
