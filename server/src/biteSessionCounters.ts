/**
 * D-L2-15: in-memory bite attempt counters for the current fishing session.
 * Miss ticks stay memory-only unless METRICS_BITE_TICK_PERSIST=1.
 */

export interface BiteSessionCounters {
  sessionHooks: number;
  sessionEscapes: number;
  sessionMissTicks: number;
  missTicksSinceLastHook: number;
  /** Anchor for waitingMsSinceLastHook (session start or last hook) */
  waitAnchorAt: number;
}

const byUserId = new Map<string, BiteSessionCounters>();

/** Default off: do not write bite_tick_miss / bite_tick_hit rows. */
export function isBiteTickPersistEnabled(): boolean {
  const v = process.env.METRICS_BITE_TICK_PERSIST;
  return v === '1' || v === 'true' || v === 'TRUE';
}

export function resetBiteSessionCounters(userId: string, startedAt = Date.now()): BiteSessionCounters {
  const c: BiteSessionCounters = {
    sessionHooks: 0,
    sessionEscapes: 0,
    sessionMissTicks: 0,
    missTicksSinceLastHook: 0,
    waitAnchorAt: startedAt,
  };
  byUserId.set(userId, c);
  return c;
}

export function getBiteSessionCounters(userId: string): BiteSessionCounters | undefined {
  return byUserId.get(userId);
}

export function ensureBiteSessionCounters(userId: string, startedAt = Date.now()): BiteSessionCounters {
  return byUserId.get(userId) ?? resetBiteSessionCounters(userId, startedAt);
}

export function clearBiteSessionCounters(userId: string): void {
  byUserId.delete(userId);
}

export function noteBiteMiss(userId: string): BiteSessionCounters {
  const c = ensureBiteSessionCounters(userId);
  c.sessionMissTicks += 1;
  c.missTicksSinceLastHook += 1;
  return c;
}

export function noteBiteHook(userId: string, now = Date.now()): {
  counters: BiteSessionCounters;
  waitingMsSinceLastHook: number;
} {
  const c = ensureBiteSessionCounters(userId);
  c.sessionHooks += 1;
  const waitingMsSinceLastHook = Math.max(0, now - c.waitAnchorAt);
  const snapshotMiss = c.missTicksSinceLastHook;
  c.missTicksSinceLastHook = 0;
  c.waitAnchorAt = now;
  // missTicksSinceLastHook in payload is pre-clear value
  return {
    counters: { ...c, missTicksSinceLastHook: snapshotMiss },
    waitingMsSinceLastHook,
  };
}

export function noteBiteEscape(userId: string): BiteSessionCounters {
  const c = ensureBiteSessionCounters(userId);
  c.sessionEscapes += 1;
  return c;
}

/** Payload fields for bite_hook / escape / fishing_stop summary. */
export function biteSessionMetricPayload(
  userId: string,
  opts: { includeWaitingMs?: boolean; waitingMsSinceLastHook?: number } = {},
): Record<string, number> {
  const c = getBiteSessionCounters(userId);
  if (!c) {
    return {
      sessionHooks: 0,
      sessionEscapes: 0,
      sessionMissTicks: 0,
      missTicksSinceLastHook: 0,
    };
  }
  const payload: Record<string, number> = {
    sessionHooks: c.sessionHooks,
    sessionEscapes: c.sessionEscapes,
    sessionMissTicks: c.sessionMissTicks,
    missTicksSinceLastHook: c.missTicksSinceLastHook,
  };
  if (opts.includeWaitingMs && opts.waitingMsSinceLastHook != null) {
    payload.waitingMsSinceLastHook = opts.waitingMsSinceLastHook;
  }
  return payload;
}
