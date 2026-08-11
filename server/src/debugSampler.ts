import { randomUUID } from 'crypto';
import { db } from './db.js';

export interface DebugSampleTarget {
  playerId: string;
  reason: string;
  requestedBy: string;
  createdAt: number;
  ttlMs: number;
}

const targets = new Map<string, DebugSampleTarget>();
const history: (DebugSampleTarget & { status: string; endedAt: number })[] = [];
const MAX_HISTORY = 1000;
const DEFAULT_TTL_MS = 30 * 60 * 1000; // 30 minutes
const CLEANUP_INTERVAL_MS = 60_000;

let cleanupTimer: NodeJS.Timeout | null = null;

function startCleanupLoop(): void {
  if (cleanupTimer) return;
  cleanupTimer = setInterval(() => {
    const now = Date.now();
    for (const [id, target] of targets) {
      if (now - target.createdAt >= target.ttlMs) {
        targets.delete(id);
        recordAudit('system', 'expired', target.playerId, 'TTL expired (' + target.ttlMs + 'ms)');
        history.push({ ...target, status: 'expired', endedAt: now });
        if (history.length > MAX_HISTORY) history.splice(0, history.length - MAX_HISTORY);
      }
    }
  }, CLEANUP_INTERVAL_MS);
  cleanupTimer.unref();
}

export function recordAudit(who: string, what: string, targetPlayerId?: string, reason?: string, details?: Record<string, unknown>): void {
  try {
    db.prepare(
      `INSERT INTO audit_log (id, who, what, target_player_id, reason, details, created_at)
       VALUES (@id, @who, @what, @targetPlayerId, @reason, @details, @createdAt)`
    ).run({
      id: randomUUID(),
      who,
      what,
      targetPlayerId: targetPlayerId ?? null,
      reason: reason ?? null,
      details: details ? JSON.stringify(details) : null,
      createdAt: Date.now(),
    });
  } catch (e) {
    // audit failures should not crash the server
    console.warn('[audit] failed to write audit_log:', e);
  }
}

export function startDebugSampling(
  playerId: string,
  opts: { reason?: string; requestedBy?: string; ttlMs?: number } = {},
): { targetId: string } {
  const targetId = randomUUID();
  const target: DebugSampleTarget = {
    playerId,
    reason: opts.reason ?? 'manual',
    requestedBy: opts.requestedBy ?? 'admin',
    createdAt: Date.now(),
    ttlMs: opts.ttlMs ?? DEFAULT_TTL_MS,
  };
  targets.set(targetId, target);
  recordAudit(target.requestedBy, 'start', playerId, opts.reason);
  startCleanupLoop();
  return { targetId };
}

export function stopDebugSampling(playerId: string): boolean {
  for (const [id, target] of targets) {
    if (target.playerId === playerId) {
      targets.delete(id);
      recordAudit('admin', 'stop', playerId, 'manual stop');
      history.push({ ...target, status: 'stopped', endedAt: Date.now() });
      if (history.length > MAX_HISTORY) history.splice(0, history.length - MAX_HISTORY);
      return true;
    }
  }
  return false;
}

export function isDebugSampled(playerId: string): boolean {
  for (const target of targets.values()) {
    if (target.playerId === playerId) return true;
  }
  return false;
}

export function listActiveTargets(): DebugSampleTarget[] {
  return Array.from(targets.values());
}

export function listHistory(): (DebugSampleTarget & { status: string; endedAt: number })[] {
  return [...history];
}

export function stopCleanupLoop(): void {
  if (cleanupTimer) {
    clearInterval(cleanupTimer);
    cleanupTimer = null;
  }
}
