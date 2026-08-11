import { randomUUID } from 'crypto';
import { getFishingMetricsSummary } from './fishingMetrics.js';
import { getBusinessHealthTrend } from './businessHealth.js';
import { getConfigNumber, getConfigString } from './gameConfig.js';
import { db } from './db.js';

export interface GrayMetricsDashboard {
  periodHours: number;
  grayReleasePercent: number;
  rulesVersion: string;
  abandonRate: number;
  escapeStreakPlayers: number;
  catchCount: number;
  faucetCoinsEstimate: number;
  sinkCoinsEstimate: number;
  faucetSinkRatio: number;
  baitPurchases: Record<string, number>;
  tacklePurchases: Record<string, number>;
  alerts: string[];
  businessHealth7d: ReturnType<typeof getBusinessHealthTrend>;
}

export function getGrayMetricsDashboard(hours = 168): GrayMetricsDashboard {
  const summary = getFishingMetricsSummary(hours);
  const grayReleasePercent = getConfigNumber('GRAY_RELEASE_PERCENT', 100);
  const rulesVersion = getConfigString('RULES_VERSION', 'v0.4.1');
  const faucet = summary.faucetCoinsEstimate;
  const sink = summary.sinkCoinsEstimate;
  const faucetSinkRatio = sink > 0 ? faucet / sink : faucet > 0 ? Infinity : 0;

  const alerts = [...summary.alerts];
  if (faucetSinkRatio > 1.2) {
    alerts.push(`金币 faucet/sink 比 ${faucetSinkRatio.toFixed(2)} > 1.2（通胀风险）`);
  }
  if (faucetSinkRatio < 0.8 && sink > 0) {
    alerts.push(`金币 faucet/sink 比 ${faucetSinkRatio.toFixed(2)} < 0.8（通缩风险）`);
  }

  return {
    periodHours: hours,
    grayReleasePercent,
    rulesVersion,
    abandonRate: summary.abandonRate,
    escapeStreakPlayers: summary.escapeStreakPlayers,
    catchCount: summary.catchCount,
    faucetCoinsEstimate: faucet,
    sinkCoinsEstimate: sink,
    faucetSinkRatio,
    baitPurchases: summary.baitPurchases,
    tacklePurchases: summary.tacklePurchases,
    alerts,
    businessHealth7d: getBusinessHealthTrend(7),
  };
}

export function rollbackConfigToPrevious(
  key: string,
  operator: string,
): { ok: true; restoredValue: string } | { ok: false; error: string } {
  const row = db
    .prepare(
      `SELECT old_value, new_value, changed_by, approved_by, created_at
       FROM config_audit_log WHERE config_key = ?
       ORDER BY created_at DESC LIMIT 1`,
    )
    .get(key) as
    | { old_value: string | null; new_value: string; changed_by: string; approved_by: string | null; created_at: number }
    | undefined;
  if (!row || row.old_value === null) {
    return { ok: false, error: '没有可回滚的历史版本' };
  }

  const now = Date.now();
  const tx = db.transaction(() => {
    db.prepare(
      `INSERT INTO game_config (config_key, config_value, updated_at) VALUES (?, ?, ?)
       ON CONFLICT(config_key) DO UPDATE SET config_value = excluded.config_value, updated_at = excluded.updated_at`,
    ).run(key, row.old_value, now);
    db.prepare(
      `INSERT INTO config_audit_log (id, config_key, old_value, new_value, changed_by, approved_by, created_at)
       VALUES (?, ?, ?, ?, ?, ?, ?)`,
    ).run(
      randomUUID(),
      key,
      row.new_value,
      row.old_value,
      operator,
      operator,
      now,
    );
  });
  tx();

  void import('./runtimeConfig.js').then(({ applyRuntimeConfigFromDb }) => applyRuntimeConfigFromDb());
  return { ok: true, restoredValue: row.old_value };
}
