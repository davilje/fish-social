import {
  ADMISSION_FEE_SLICE_MS,
  calcDurationPondXp,
  evaluatePondAccess,
  getFishXpGrant,
  getGamePondDef,
  grantCatchXp,
  type FishQuality,
} from '@fish-social/shared';
import { db } from './db.js';
import { deductCoins, getPlayer } from './players.js';
import { recordFishingMetric } from './fishingMetrics.js';
import { grantStarterRod } from './gear.js';

export interface PlayerFishingProgress {
  playerId: string;
  level: number;
  xp: number;
  onboardingCompleted: boolean;
  onboardingCompletedAt: number | null;
}

export interface PondProficiencyRow {
  pondId: string;
  level: number;
  xp: number;
}

export interface AdmissionFeeState {
  playerId: string;
  dateKey: string;
  charges: number;
  progressMs: number;
  needsFeeToContinue: boolean;
  lastPondId: string | null;
}

function nowMs(): number {
  return Date.now();
}

function todayKey(ms: number = nowMs()): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Shanghai' }).format(new Date(ms));
}

export function ensurePlayerProgress(playerId: string): PlayerFishingProgress {
  const row = db
    .prepare(
      `SELECT player_id, level, xp, onboarding_completed, onboarding_completed_at
       FROM player_fishing_progress WHERE player_id = ?`,
    )
    .get(playerId) as
    | {
        player_id: string;
        level: number;
        xp: number;
        onboarding_completed: number;
        onboarding_completed_at: number | null;
      }
    | undefined;

  if (row) {
    return {
      playerId: row.player_id,
      level: row.level,
      xp: row.xp,
      onboardingCompleted: row.onboarding_completed === 1,
      onboardingCompletedAt: row.onboarding_completed_at,
    };
  }

  const createdAt = nowMs();
  db.prepare(
    `INSERT INTO player_fishing_progress
      (player_id, level, xp, onboarding_completed, onboarding_completed_at, updated_at)
     VALUES (?, 1, 0, 0, NULL, ?)`,
  ).run(playerId, createdAt);

  return {
    playerId,
    level: 1,
    xp: 0,
    onboardingCompleted: false,
    onboardingCompletedAt: null,
  };
}

function savePlayerProgress(progress: PlayerFishingProgress): void {
  db.prepare(
    `INSERT INTO player_fishing_progress
      (player_id, level, xp, onboarding_completed, onboarding_completed_at, updated_at)
     VALUES (?, ?, ?, ?, ?, ?)
     ON CONFLICT(player_id) DO UPDATE SET
       level = excluded.level,
       xp = excluded.xp,
       onboarding_completed = excluded.onboarding_completed,
       onboarding_completed_at = excluded.onboarding_completed_at,
       updated_at = excluded.updated_at`,
  ).run(
    progress.playerId,
    progress.level,
    progress.xp,
    progress.onboardingCompleted ? 1 : 0,
    progress.onboardingCompletedAt,
    nowMs(),
  );
}

export function getPondProficiency(playerId: string, pondId: string): PondProficiencyRow {
  const row = db
    .prepare(
      `SELECT pond_id, level, xp FROM player_pond_proficiency
       WHERE player_id = ? AND pond_id = ?`,
    )
    .get(playerId, pondId) as { pond_id: string; level: number; xp: number } | undefined;

  if (row) return { pondId: row.pond_id, level: row.level, xp: row.xp };

  db.prepare(
    `INSERT INTO player_pond_proficiency (player_id, pond_id, level, xp, updated_at)
     VALUES (?, ?, 1, 0, ?)`,
  ).run(playerId, pondId, nowMs());

  return { pondId, level: 1, xp: 0 };
}

function savePondProficiency(playerId: string, row: PondProficiencyRow): void {
  db.prepare(
    `INSERT INTO player_pond_proficiency (player_id, pond_id, level, xp, updated_at)
     VALUES (?, ?, ?, ?, ?)
     ON CONFLICT(player_id, pond_id) DO UPDATE SET
       level = excluded.level,
       xp = excluded.xp,
       updated_at = excluded.updated_at`,
  ).run(playerId, row.pondId, row.level, row.xp, nowMs());
}

export function listPondProficiencies(playerId: string): PondProficiencyRow[] {
  ensurePlayerProgress(playerId);
  const rows = db
    .prepare(
      `SELECT pond_id, level, xp FROM player_pond_proficiency WHERE player_id = ?`,
    )
    .all(playerId) as Array<{ pond_id: string; level: number; xp: number }>;
  return rows.map((r) => ({ pondId: r.pond_id, level: r.level, xp: r.xp }));
}

export function completeOnboarding(playerId: string): PlayerFishingProgress {
  const progress = ensurePlayerProgress(playerId);
  if (!progress.onboardingCompleted) {
    progress.onboardingCompleted = true;
    progress.onboardingCompletedAt = nowMs();
    savePlayerProgress(progress);
    recordFishingMetric('onboarding_completed', {
      playerId,
      pondId: 'pond-novice',
      payload: { completedAt: progress.onboardingCompletedAt },
    });
    grantStarterRod(playerId);
  }
  return progress;
}

/** 调试：清掉引导完成标记，允许再次进入 pond-novice。不改等级 / XP。 */
export function resetOnboarding(playerId: string): PlayerFishingProgress {
  const progress = ensurePlayerProgress(playerId);
  progress.onboardingCompleted = false;
  progress.onboardingCompletedAt = null;
  savePlayerProgress(progress);
  db.prepare(
    `DELETE FROM player_pond_proficiency WHERE player_id = ? AND pond_id = 'pond-novice'`,
  ).run(playerId);
  try {
    db.prepare(
      `DELETE FROM player_pond_session WHERE player_id = ? AND pond_id = 'pond-novice'`,
    ).run(playerId);
  } catch {
    // table may be missing in older local DBs
  }
  return progress;
}

export function checkJoinPondAccess(
  playerId: string,
  pondId: string,
): { ok: true; progress: PlayerFishingProgress } | { ok: false; error: string } {
  const progress = ensurePlayerProgress(playerId);
  const access = evaluatePondAccess(pondId, {
    onboardingCompleted: progress.onboardingCompleted,
    playerLevel: progress.level,
  });
  if (!access.ok) return { ok: false, error: access.error ?? '无法进入该鱼塘' };
  return { ok: true, progress };
}

export function getAdmissionFeeState(
  playerId: string,
  atMs: number = nowMs(),
): AdmissionFeeState {
  const dateKey = todayKey(atMs);
  const row = db
    .prepare(
      `SELECT player_id, date_key, charges, progress_ms, needs_fee_to_continue, last_pond_id
       FROM daily_admission_fees WHERE player_id = ? AND date_key = ?`,
    )
    .get(playerId, dateKey) as
    | {
        player_id: string;
        date_key: string;
        charges: number;
        progress_ms: number;
        needs_fee_to_continue: number;
        last_pond_id: string | null;
      }
    | undefined;

  if (row) {
    return {
      playerId: row.player_id,
      dateKey: row.date_key,
      charges: row.charges,
      progressMs: row.progress_ms,
      needsFeeToContinue: row.needs_fee_to_continue === 1,
      lastPondId: row.last_pond_id,
    };
  }

  return {
    playerId,
    dateKey,
    charges: 0,
    progressMs: 0,
    needsFeeToContinue: false,
    lastPondId: null,
  };
}

function saveAdmissionFeeState(state: AdmissionFeeState): void {
  db.prepare(
    `INSERT INTO daily_admission_fees
      (player_id, date_key, charges, progress_ms, needs_fee_to_continue, last_pond_id, updated_at)
     VALUES (?, ?, ?, ?, ?, ?, ?)
     ON CONFLICT(player_id, date_key) DO UPDATE SET
       charges = excluded.charges,
       progress_ms = excluded.progress_ms,
       needs_fee_to_continue = excluded.needs_fee_to_continue,
       last_pond_id = excluded.last_pond_id,
       updated_at = excluded.updated_at`,
  ).run(
    state.playerId,
    state.dateKey,
    state.charges,
    state.progressMs,
    state.needsFeeToContinue ? 1 : 0,
    state.lastPondId,
    nowMs(),
  );
}

export function buildJoinFeeHint(playerId: string, pondId: string) {
  const pond = getGamePondDef(pondId);
  const fee = getAdmissionFeeState(playerId);
  const profile = getPlayer(playerId);
  return {
    feePer2h: pond?.feePer2h ?? 0,
    maxFeeChargesPerDay: pond?.maxFeeChargesPerDay ?? 0,
    todayFeeCharges: fee.charges,
    feeProgressMs: fee.progressMs,
    needsFeeToContinue: fee.needsFeeToContinue,
    coins: profile?.coins ?? 0,
    pondCategory: pond?.pondCategory ?? null,
  };
}

export function canStartFishingWithFee(
  playerId: string,
  pondId: string,
): { ok: true } | { ok: false; error: string } {
  const pond = getGamePondDef(pondId);
  if (!pond || pond.feePer2h <= 0) return { ok: true };

  const fee = getAdmissionFeeState(playerId);
  if (!fee.needsFeeToContinue) return { ok: true };

  const profile = getPlayer(playerId);
  if (!profile || profile.coins < pond.feePer2h) {
    return {
      ok: false,
      error: `金币不足，无法支付下一时段入场费（需要 ${pond.feePer2h}）`,
    };
  }
  return { ok: true };
}

export type FeeTickResult =
  | { kind: 'ok'; charged: number; state: AdmissionFeeState }
  | { kind: 'insufficient'; state: AdmissionFeeState; feePer2h: number };

/**
 * Accumulate effective fishing ms on paid ponds and charge each full 2h slice.
 */
export function applyAdmissionFeeProgress(
  playerId: string,
  pondId: string,
  deltaMs: number,
): FeeTickResult {
  const pond = getGamePondDef(pondId);
  if (!pond || pond.feePer2h <= 0 || deltaMs <= 0) {
    return { kind: 'ok', charged: 0, state: getAdmissionFeeState(playerId) };
  }

  const state = getAdmissionFeeState(playerId);
  state.progressMs += deltaMs;
  state.lastPondId = pondId;
  let charged = 0;
  const maxCharges = pond.maxFeeChargesPerDay > 0 ? pond.maxFeeChargesPerDay : 4;

  while (state.charges < maxCharges) {
    const nextThreshold = (state.charges + 1) * ADMISSION_FEE_SLICE_MS;
    if (state.progressMs < nextThreshold) break;

    const result = deductCoins(playerId, pond.feePer2h);
    if (!result.ok) {
      state.needsFeeToContinue = true;
      saveAdmissionFeeState(state);
      recordFishingMetric('fishing_stopped_insufficient_gold', {
        playerId,
        pondId,
        payload: {
          feePer2h: pond.feePer2h,
          charges: state.charges,
          progressMs: state.progressMs,
          coins: getPlayer(playerId)?.coins ?? 0,
        },
      });
      return { kind: 'insufficient', state, feePer2h: pond.feePer2h };
    }

    state.charges += 1;
    state.needsFeeToContinue = false;
    charged += pond.feePer2h;
    recordFishingMetric('admission_fee_charged', {
      playerId,
      pondId,
      payload: {
        feePer2h: pond.feePer2h,
        chargeIndex: state.charges,
        progressMs: state.progressMs,
        coinsAfter: result.coins,
      },
    });
  }

  saveAdmissionFeeState(state);
  return { kind: 'ok', charged, state };
}

export function clearNeedsFeeFlag(playerId: string): void {
  const state = getAdmissionFeeState(playerId);
  if (!state.needsFeeToContinue) return;
  state.needsFeeToContinue = false;
  saveAdmissionFeeState(state);
}

export function grantCatchProgress(
  playerId: string,
  pondId: string,
  speciesId: string,
  quality: FishQuality,
): {
  progress: PlayerFishingProgress;
  proficiency: PondProficiencyRow;
  pondXpCapped: boolean;
} {
  const progress = ensurePlayerProgress(playerId);
  const proficiency = getPondProficiency(playerId, pondId);
  const grant = getFishXpGrant(speciesId, quality);
  const result = grantCatchXp(
    { level: progress.level, xp: progress.xp },
    { level: proficiency.level, xp: proficiency.xp },
    grant.playerXp,
    grant.pondXp,
    pondId,
  );

  progress.level = result.player.level;
  progress.xp = result.player.xp;
  savePlayerProgress(progress);

  proficiency.level = result.pond.level;
  proficiency.xp = result.pond.xp;
  savePondProficiency(playerId, proficiency);

  if (result.pondXpCapped) {
    recordFishingMetric('pond_proficiency_capped', {
      playerId,
      pondId,
      payload: {
        pondLevel: proficiency.level,
        playerLevel: progress.level,
        source: 'catch',
      },
    });
  }

  return { progress, proficiency, pondXpCapped: result.pondXpCapped };
}

export function grantDurationPondXp(
  playerId: string,
  pondId: string,
  fishingMs: number,
): void {
  if (fishingMs <= 0) return;
  const progress = ensurePlayerProgress(playerId);
  const proficiency = getPondProficiency(playerId, pondId);
  const raw = calcDurationPondXp(progress.level, fishingMs);
  if (raw <= 0) return;

  const result = grantCatchXp(
    { level: progress.level, xp: progress.xp },
    { level: proficiency.level, xp: proficiency.xp },
    0,
    raw,
    pondId,
  );

  // Duration only grants pond XP (playerXp grant=0)
  proficiency.level = result.pond.level;
  proficiency.xp = result.pond.xp;
  savePondProficiency(playerId, proficiency);

  if (result.pondXpCapped) {
    recordFishingMetric('pond_proficiency_capped', {
      playerId,
      pondId,
      payload: {
        pondLevel: proficiency.level,
        playerLevel: progress.level,
        source: 'duration',
        fishingMs,
      },
    });
  }
}

export function getProgressPublicView(playerId: string) {
  const progress = ensurePlayerProgress(playerId);
  const fee = getAdmissionFeeState(playerId);
  return {
    level: progress.level,
    xp: progress.xp,
    onboardingCompleted: progress.onboardingCompleted,
    onboardingCompletedAt: progress.onboardingCompletedAt,
    pondProficiencies: listPondProficiencies(playerId),
    todayFeeCharges: fee.charges,
    feeProgressMs: fee.progressMs,
    needsFeeToContinue: fee.needsFeeToContinue,
  };
}
