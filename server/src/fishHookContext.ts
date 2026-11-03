import type { PondFishEntity } from '@fish-social/shared';
import { db } from './db.js';

export interface PersistedFishHookContext {
  catchId: string;
  fish: PondFishEntity;
  escaped: boolean;
  hookDurationMs: number;
  hookEndsAt: number;
}

/** Called inside the hook/food/payment transaction; never allocates a new catch identity. */
export function persistFishHookContext(userId: string | null, context: PersistedFishHookContext): void {
  db.prepare(`UPDATE fish_hook_locks SET user_id=COALESCE(?,user_id), context_json=?, expires_at=?
    WHERE fish_id=? AND catch_id=?`)
    .run(userId, JSON.stringify(context), context.hookEndsAt + 120_000, context.fish.id, context.catchId);
}

/** A lock must still belong to this user and this fish; stale JSON is never trusted after reassignment. */
export function readPersistedFishHookContext(userId: string, at = Date.now()): PersistedFishHookContext | undefined {
  const row = db.prepare(`SELECT h.fish_id,h.catch_id,h.context_json FROM fish_hook_locks h
    WHERE h.user_id=? AND h.expires_at>? AND h.context_json IS NOT NULL
      AND EXISTS(SELECT 1 FROM pond_fish f WHERE f.id=h.fish_id)
    ORDER BY h.locked_at DESC LIMIT 1`).get(userId, at) as {
      fish_id: string; catch_id: string; context_json: string;
    } | undefined;
  if (!row) return undefined;
  try {
    const context = JSON.parse(row.context_json) as PersistedFishHookContext;
    return context.catchId === row.catch_id && context.fish?.id === row.fish_id
      && typeof context.escaped === 'boolean' && Number.isFinite(context.hookDurationMs)
      && context.hookDurationMs >= 0 && Number.isFinite(context.hookEndsAt) ? context : undefined;
  } catch { return undefined; }
}
