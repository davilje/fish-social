import { randomUUID } from 'crypto';
import {
  BITE_LAMBDA,
  FISH_BITE_CHECK_MS,
  FISH_SPECIES,
  type FishQuality,
} from '@fish-social/shared';
import { db } from './db.js';
import { applyRuntimeConfigFromDb, refreshRuntimeFromDb } from './runtimeConfig.js';

export interface ConfigEntryView {
  key: string;
  effectiveValue: string;
  defaultValue: string;
  source: 'runtime' | 'default';
  updatedAt?: number;
}

export interface ConfigChangeRequest {
  id: string;
  configKey: string;
  proposedValue: string;
  status: 'pending' | 'approved' | 'rejected';
  submittedBy: string;
  approvedBy?: string;
  createdAt: number;
  resolvedAt?: number;
}

export interface ConfigAuditEntry {
  id: string;
  configKey: string;
  oldValue: string | null;
  newValue: string;
  changedBy: string;
  approvedBy: string | null;
  createdAt: number;
}

const CONFIG_DEFAULTS: Record<string, string> = {
  BITE_LAMBDA: String(BITE_LAMBDA),
  FISH_BITE_CHECK_MS: String(FISH_BITE_CHECK_MS),
  HOOK_DURATION_SCALE: '1',
  GRAY_RELEASE_PERCENT: '100',
  RULES_VERSION: 'v0.4.1',
  C3_SINK_ENABLED: 'true',
  C4_GENETICS_ENABLED: 'true',
  C6_SKIP_CASTING_ON_REBATE: 'true',
  BOT_CATCH_SHARE_CAP: '0.4',
  MAX_BOTS_PER_POND: '20',
  BOT_EVICT_POLICY: 'random',
  /** FISH-BOT-2 / FEAT-SCENE-TILE-3：20 塘时启动更稀疏，避免 bot 总量压垮单机 */
  BOT_BOOT_MIN: '1',
  BOT_BOOT_MAX: '2',
  BOT_SPAWN_CHECK_MS: '60000',
  BOT_SPAWN_CHANCE: '0.2',
  BOT_BOOT_FISHING_RATIO: '0.75',
  BOT_BOOT_ELAPSED_MIN_MS: String(5 * 60 * 1000),
  BOT_BOOT_ELAPSED_MAX_MS: String(75 * 60 * 1000),
  BOT_JOIN_FISHING_CHANCE: '0.4',
  BOT_JOIN_ELAPSED_MAX_MS: String(10 * 60 * 1000),
  ...Object.fromEntries(
    FISH_SPECIES.map((s) => [`SPECIES_ESCAPE_RATE_${s.id}`, String(s.baseEscapeRate)]),
  ),
};

export function getConfigDefault(key: string): string | undefined {
  return CONFIG_DEFAULTS[key];
}

export function listConfigurableKeys(): string[] {
  return Object.keys(CONFIG_DEFAULTS).sort();
}

export function loadGameConfigFromDb(): Map<string, string> {
  const rows = db.prepare('SELECT config_key, config_value FROM game_config').all() as Array<{
    config_key: string;
    config_value: string;
  }>;
  const map = new Map<string, string>();
  for (const row of rows) map.set(row.config_key, row.config_value);
  return map;
}

export function initGameConfig(): void {
  const now = Date.now();
  const insert = db.prepare(`
    INSERT OR IGNORE INTO game_config (config_key, config_value, updated_at) VALUES (?, ?, ?)
  `);
  for (const [key, value] of Object.entries(CONFIG_DEFAULTS)) {
    insert.run(key, value, now);
  }
  db.prepare(
    `INSERT INTO game_config (config_key, config_value, updated_at) VALUES (?, ?, ?)
     ON CONFLICT(config_key) DO UPDATE SET config_value = excluded.config_value, updated_at = excluded.updated_at`,
  ).run('FISH_BITE_CHECK_MS', String(FISH_BITE_CHECK_MS), now);
  refreshRuntimeFromDb();
}

export function getConfigViews(): ConfigEntryView[] {
  const dbMap = loadGameConfigFromDb();
  const rows = db
    .prepare('SELECT config_key, config_value, updated_at FROM game_config')
    .all() as Array<{ config_key: string; config_value: string; updated_at: number }>;
  const updatedAtByKey = new Map(rows.map((r) => [r.config_key, r.updated_at]));

  return listConfigurableKeys().map((key) => {
    const defaultValue = CONFIG_DEFAULTS[key] ?? '';
    const effective = dbMap.get(key) ?? defaultValue;
    return {
      key,
      effectiveValue: effective,
      defaultValue,
      source: dbMap.has(key) ? 'runtime' : 'default',
      updatedAt: updatedAtByKey.get(key),
    };
  });
}

export function getConfigBool(key: string, fallback = false): boolean {
  const raw = loadGameConfigFromDb().get(key) ?? CONFIG_DEFAULTS[key];
  if (raw === undefined) return fallback;
  return raw === 'true' || raw === '1';
}

export function getConfigNumber(key: string, fallback: number): number {
  const raw = loadGameConfigFromDb().get(key) ?? CONFIG_DEFAULTS[key];
  if (raw === undefined) return fallback;
  const n = Number(raw);
  return Number.isFinite(n) ? n : fallback;
}

export function getConfigString(key: string, fallback = ''): string {
  const raw = loadGameConfigFromDb().get(key) ?? CONFIG_DEFAULTS[key];
  if (raw === undefined) return fallback;
  return String(raw);
}

export function submitConfigChange(
  key: string,
  proposedValue: string,
  submittedBy: string,
): { ok: true; request: ConfigChangeRequest } | { ok: false; error: string } {
  if (!CONFIG_DEFAULTS[key] && !key.startsWith('SPECIES_') && !key.startsWith('QUALITY_')) {
    return { ok: false, error: '无效的配置项' };
  }
  const id = randomUUID();
  const now = Date.now();
  db.prepare(`
    INSERT INTO config_change_requests (id, config_key, proposed_value, status, submitted_by, created_at)
    VALUES (?, ?, ?, 'pending', ?, ?)
  `).run(id, key, proposedValue, submittedBy, now);
  return {
    ok: true,
    request: {
      id,
      configKey: key,
      proposedValue,
      status: 'pending',
      submittedBy,
      createdAt: now,
    },
  };
}

export function approveConfigChange(
  requestId: string,
  approver: string,
): { ok: true } | { ok: false; error: string } {
  const row = db.prepare('SELECT * FROM config_change_requests WHERE id = ?').get(requestId) as
    | {
        id: string;
        config_key: string;
        proposed_value: string;
        status: string;
        submitted_by: string;
      }
    | undefined;
  if (!row) return { ok: false, error: '申请不存在' };
  if (row.status !== 'pending') return { ok: false, error: '申请已处理' };
  if (row.submitted_by === approver) {
    return { ok: false, error: '双人确认：审批人不能与提交人相同' };
  }

  const oldRow = db
    .prepare('SELECT config_value FROM game_config WHERE config_key = ?')
    .get(row.config_key) as { config_value: string } | undefined;
  const oldValue = oldRow?.config_value ?? CONFIG_DEFAULTS[row.config_key] ?? null;
  const now = Date.now();

  const tx = db.transaction(() => {
    db.prepare(`
      INSERT INTO game_config (config_key, config_value, updated_at) VALUES (?, ?, ?)
      ON CONFLICT(config_key) DO UPDATE SET config_value = excluded.config_value, updated_at = excluded.updated_at
    `).run(row.config_key, row.proposed_value, now);
    db.prepare(`
      UPDATE config_change_requests SET status = 'approved', approved_by = ?, resolved_at = ? WHERE id = ?
    `).run(approver, now, requestId);
    db.prepare(`
      INSERT INTO config_audit_log (id, config_key, old_value, new_value, changed_by, approved_by, created_at)
      VALUES (?, ?, ?, ?, ?, ?, ?)
    `).run(randomUUID(), row.config_key, oldValue, row.proposed_value, row.submitted_by, approver, now);
  });
  tx();
  applyRuntimeConfigFromDb();
  return { ok: true };
}

export function rejectConfigChange(
  requestId: string,
  approver: string,
): { ok: true } | { ok: false; error: string } {
  const row = db.prepare('SELECT status FROM config_change_requests WHERE id = ?').get(requestId) as
    | { status: string }
    | undefined;
  if (!row) return { ok: false, error: '申请不存在' };
  if (row.status !== 'pending') return { ok: false, error: '申请已处理' };
  db.prepare(`
    UPDATE config_change_requests SET status = 'rejected', approved_by = ?, resolved_at = ? WHERE id = ?
  `).run(approver, Date.now(), requestId);
  return { ok: true };
}

export function listPendingConfigChanges(): ConfigChangeRequest[] {
  const rows = db
    .prepare(`SELECT * FROM config_change_requests WHERE status = 'pending' ORDER BY created_at DESC`)
    .all() as Array<{
      id: string;
      config_key: string;
      proposed_value: string;
      status: string;
      submitted_by: string;
      approved_by: string | null;
      created_at: number;
      resolved_at: number | null;
    }>;
  return rows.map((r) => ({
    id: r.id,
    configKey: r.config_key,
    proposedValue: r.proposed_value,
    status: r.status as ConfigChangeRequest['status'],
    submittedBy: r.submitted_by,
    approvedBy: r.approved_by ?? undefined,
    createdAt: r.created_at,
    resolvedAt: r.resolved_at ?? undefined,
  }));
}

export function listConfigAuditLog(limit = 100): ConfigAuditEntry[] {
  const since = Date.now() - 30 * 24 * 60 * 60 * 1000;
  const rows = db
    .prepare(
      `SELECT * FROM config_audit_log WHERE created_at >= ? ORDER BY created_at DESC LIMIT ?`,
    )
    .all(since, limit) as Array<{
      id: string;
      config_key: string;
      old_value: string | null;
      new_value: string;
      changed_by: string;
      approved_by: string | null;
      created_at: number;
    }>;
  return rows.map((r) => ({
    id: r.id,
    configKey: r.config_key,
    oldValue: r.old_value,
    newValue: r.new_value,
    changedBy: r.changed_by,
    approvedBy: r.approved_by,
    createdAt: r.created_at,
  }));
}

/** @deprecated v0.3.0 脱钩改由 calcQualitySizeEscapeRate；保留供旧配置读取 */
export function getSpeciesRuntimeStats(speciesId: string): { baseEscapeRate: number } | null {
  const species = FISH_SPECIES.find((s) => s.id === speciesId);
  if (!species) return null;
  const escapeKey = `SPECIES_ESCAPE_RATE_${speciesId}`;
  return {
    baseEscapeRate: getConfigNumber(escapeKey, species.baseEscapeRate),
  };
}

/** @deprecated v0.3.0 已合并进 QUALITY_ESCAPE_BASE */
export function getQualityEscapeBonusRuntime(quality: FishQuality): number {
  const key = `QUALITY_ESCAPE_BONUS_${quality.toUpperCase()}`;
  return getConfigNumber(key, 0);
}
