import type { FishingPhase, PendingFishCatch, PondUser } from '@fish-social/shared';
import { FISHING_PROMPT_AUTO_CLOSE_MS } from '@fish-social/shared';
import { db } from './db.js';
import { register, cancelByKind } from './timerRegistry.js';
import { ensureFishingStartedAt } from './fishingStartedAt.js';

const DISCONNECT_GRACE_MS = 60_000;
const PENDING_CATCH_TIMEOUT_MS = FISHING_PROMPT_AUTO_CLOSE_MS + 8_000;

export interface SessionRow {
  player_id: string;
  pond_id: string;
  user_id: string;
  spot_id: string | null;
  fishing_phase: string | null;
  phase_ends_at: number | null;
  hook_ends_at: number | null;
  disconnected_at: number | null;
  return_fee_mode: string | null;
  updated_at: number;
}

interface PendingLockRow {
  user_id: string;
  catch_id: string;
  pond_fish_id: string;
  species_id: string;
  quality: string;
  size_m: number;
  hook_duration_ms: number;
  is_codex_new: number;
  player_id: string | null;
  pond_id: string | null;
  locked_at: number;
}

const upsertSessionStmt = db.prepare(`
  INSERT INTO player_pond_session (
    player_id, pond_id, user_id, spot_id, fishing_phase,
    phase_ends_at, hook_ends_at, disconnected_at, return_fee_mode, updated_at
  ) VALUES (
    @playerId, @pondId, @userId, @spotId, @fishingPhase,
    @phaseEndsAt, @hookEndsAt, @disconnectedAt, @returnFeeMode, @updatedAt
  )
  ON CONFLICT(player_id, pond_id) DO UPDATE SET
    user_id = excluded.user_id,
    spot_id = excluded.spot_id,
    fishing_phase = excluded.fishing_phase,
    phase_ends_at = excluded.phase_ends_at,
    hook_ends_at = excluded.hook_ends_at,
    disconnected_at = excluded.disconnected_at,
    return_fee_mode = excluded.return_fee_mode,
    updated_at = excluded.updated_at
`);

const deleteSessionStmt = db.prepare(
  'DELETE FROM player_pond_session WHERE player_id = ? AND pond_id = ?',
);

const loadSessionStmt = db.prepare(
  'SELECT * FROM player_pond_session WHERE player_id = ? AND pond_id = ?',
);

const upsertPendingStmt = db.prepare(`
  INSERT INTO pending_catch_locks (
    user_id, catch_id, pond_fish_id, species_id, quality, size_m,
    hook_duration_ms, is_codex_new, player_id, pond_id, locked_at
  ) VALUES (
    @userId, @catchId, @pondFishId, @speciesId, @quality, @sizeM,
    @hookDurationMs, @isCodexNew, @playerId, @pondId, @lockedAt
  )
  ON CONFLICT(user_id) DO UPDATE SET
    catch_id = excluded.catch_id,
    pond_fish_id = excluded.pond_fish_id,
    species_id = excluded.species_id,
    quality = excluded.quality,
    size_m = excluded.size_m,
    hook_duration_ms = excluded.hook_duration_ms,
    is_codex_new = excluded.is_codex_new,
    player_id = excluded.player_id,
    pond_id = excluded.pond_id,
    locked_at = excluded.locked_at
`);

const deletePendingStmt = db.prepare('DELETE FROM pending_catch_locks WHERE user_id = ?');
const listPendingStmt = db.prepare('SELECT * FROM pending_catch_locks');

export function upsertPlayerPondSession(user: PondUser, pondId: string, hookEndsAt?: number | null): void {
  if (!user.playerId || user.isBot) return;
  upsertSessionStmt.run({
    playerId: user.playerId,
    pondId,
    userId: user.id,
    spotId: user.spotId,
    fishingPhase: user.fishingPhase ?? null,
    phaseEndsAt: user.phaseEndsAt,
    hookEndsAt: hookEndsAt ?? null,
    disconnectedAt: user.disconnectedAt ?? null,
    returnFeeMode: user.returnFeeMode ?? 'sell_only',
    updatedAt: Date.now(),
  });
}

export function deletePlayerPondSession(playerId: string, pondId: string): void {
  deleteSessionStmt.run(playerId, pondId);
}

export function loadPlayerPondSession(playerId: string, pondId: string): SessionRow | undefined {
  return loadSessionStmt.get(playerId, pondId) as SessionRow | undefined;
}

export function isCheckpointExpired(row: SessionRow): boolean {
  const now = Date.now();
  if (row.fishing_phase === 'disconnected') {
    const deadline = row.phase_ends_at ?? (row.disconnected_at != null ? row.disconnected_at + DISCONNECT_GRACE_MS : null);
    if (deadline != null && now > deadline) return true;
  }
  return false;
}

export function persistPendingCatchLock(
  userId: string,
  catchData: PendingFishCatch,
  meta?: { playerId?: string; pondId?: string },
): void {
  upsertPendingStmt.run({
    userId,
    catchId: catchData.catchId,
    pondFishId: catchData.pondFishId,
    speciesId: catchData.speciesId,
    quality: catchData.quality,
    sizeM: catchData.sizeM,
    hookDurationMs: catchData.hookDurationMs,
    isCodexNew: catchData.isCodexNew ? 1 : 0,
    playerId: meta?.playerId ?? null,
    pondId: meta?.pondId ?? null,
    lockedAt: Date.now(),
  });
}

export function deletePendingCatchLock(userId: string): void {
  deletePendingStmt.run(userId);
}

function rowToPending(row: PendingLockRow): PendingFishCatch {
  return {
    catchId: row.catch_id,
    pondFishId: row.pond_fish_id,
    speciesId: row.species_id as PendingFishCatch['speciesId'],
    quality: row.quality as PendingFishCatch['quality'],
    sizeM: row.size_m,
    hookDurationMs: row.hook_duration_ms,
    ...(row.is_codex_new ? { isCodexNew: true } : {}),
  };
}

export function applyCheckpointToUser(user: PondUser, row: SessionRow): PondUser {
  user.id = row.user_id;
  user.spotId = row.spot_id;
  user.fishingPhase = (row.fishing_phase as FishingPhase | null) ?? 'idle';
  user.phaseEndsAt = row.phase_ends_at;
  user.disconnectedAt = row.disconnected_at;
  user.returnFeeMode =
    row.return_fee_mode === 'auto_return' || row.return_fee_mode === 'sell_only'
      ? row.return_fee_mode
      : 'sell_only';
  user.status = user.fishingPhase && user.fishingPhase !== 'idle' && user.fishingPhase !== 'seated'
    ? 'fishing'
    : user.spotId ? 'idle' : 'idle';
  ensureFishingStartedAt(user);
  return user;
}

export type RestorePendingFn = (
  userId: string,
  catchData: PendingFishCatch,
  meta: { playerId?: string; pondId?: string },
  remainingMs: number,
) => void;

export type ExpirePendingFn = (
  userId: string,
  meta: { playerId?: string; pondId?: string },
) => void;

export function recoverPendingCatchLocksOnStartup(
  restorePending: RestorePendingFn,
  expirePending: ExpirePendingFn,
): void {
  const rows = listPendingStmt.all() as PendingLockRow[];
  const now = Date.now();
  for (const row of rows) {
    const age = now - row.locked_at;
    const meta = { playerId: row.player_id ?? undefined, pondId: row.pond_id ?? undefined };
    if (age >= PENDING_CATCH_TIMEOUT_MS) {
      deletePendingCatchLock(row.user_id);
      expirePending(row.user_id, meta);
    } else {
      restorePending(row.user_id, rowToPending(row), meta, PENDING_CATCH_TIMEOUT_MS - age);
    }
  }
}

export function schedulePendingExpireTimer(
  userId: string,
  ms: number,
  onFire: () => void,
): string {
  cancelByKind(userId, 'pending_expire');
  return register({ userId, kind: 'pending_expire', ms, onFire });
}
