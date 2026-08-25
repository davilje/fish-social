import Database from 'better-sqlite3';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.resolve(__dirname, '../../data');
const dbPath = process.env.DB_PATH ?? path.join(dataDir, 'fish-social.db');

if (!fs.existsSync(dataDir)) {
  fs.mkdirSync(dataDir, { recursive: true });
}

export const db = new Database(dbPath);
db.pragma('journal_mode = WAL');
db.pragma('foreign_keys = ON');

const DEFAULT_SLOW_QUERY_MS = Number(process.env.SLOW_QUERY_MS ?? 30);

export function timedDbQuery<T>(
  queryName: string,
  fn: () => T,
  opts?: { thresholdMs?: number; meta?: Record<string, unknown> },
): T {
  const startedAt = Date.now();
  const result = fn();
  const durationMs = Date.now() - startedAt;
  const thresholdMs = opts?.thresholdMs ?? DEFAULT_SLOW_QUERY_MS;
  if (durationMs >= thresholdMs) {
    const rows = Array.isArray(result) ? result.length : undefined;
    const payload = {
      eventType: 'sqlite_query_slow',
      queryName,
      durationMs,
      thresholdMs,
      ...(rows !== undefined ? { rows } : {}),
      ts: Date.now(),
      ...(opts?.meta ?? {}),
    };
    void import('./fishingObservability.js').then(({ logStructuredEvent }) => {
      logStructuredEvent('perf', 'sqlite_query_slow', payload);
    });
  }
  return result;
}

db.exec(`
  CREATE TABLE IF NOT EXISTS players (
    player_id TEXT PRIMARY KEY,
    nickname TEXT NOT NULL,
    coins INTEGER NOT NULL DEFAULT 0,
    share_visibility TEXT NOT NULL DEFAULT 'public',
    avatar_url TEXT,
    bio TEXT NOT NULL DEFAULT '',
    showcase_fish_ids TEXT NOT NULL DEFAULT '[]',
    created_at INTEGER NOT NULL
  );

  CREATE TABLE IF NOT EXISTS inventory (
    id TEXT PRIMARY KEY,
    player_id TEXT NOT NULL,
    species_id TEXT NOT NULL,
    quality TEXT NOT NULL,
    size_m REAL NOT NULL,
    caught_at INTEGER NOT NULL,
    FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE CASCADE
  );

  CREATE TABLE IF NOT EXISTS daily_fishing (
    user_id TEXT NOT NULL,
    date_key TEXT NOT NULL,
    ms INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (user_id, date_key)
  );

  CREATE TABLE IF NOT EXISTS pond_fish (
    id TEXT PRIMARY KEY,
    pond_id TEXT NOT NULL,
    species_id TEXT NOT NULL,
    quality TEXT NOT NULL,
    size_m REAL NOT NULL,
    born_at INTEGER NOT NULL,
    generation INTEGER NOT NULL DEFAULT 0,
    bite_weight REAL
  );

  CREATE TABLE IF NOT EXISTS pond_state (
    pond_id TEXT PRIMARY KEY,
    depleted_until INTEGER,
    last_weight_refresh INTEGER NOT NULL DEFAULT 0,
    last_supplement_at INTEGER NOT NULL DEFAULT 0
  );

  CREATE TABLE IF NOT EXISTS spot_bite_weights (
    pond_id TEXT NOT NULL,
    spot_id TEXT NOT NULL,
    weight REAL NOT NULL,
  PRIMARY KEY (pond_id, spot_id)
  );

  CREATE TABLE IF NOT EXISTS social_posts (
    id TEXT PRIMARY KEY,
    player_id TEXT NOT NULL,
    nickname TEXT NOT NULL,
    author_avatar_url TEXT,
    fish_json TEXT NOT NULL,
    text TEXT NOT NULL,
    photo_url TEXT,
    visibility TEXT NOT NULL,
    created_at INTEGER NOT NULL
  );

  CREATE TABLE IF NOT EXISTS friend_links (
    player_id TEXT NOT NULL,
    friend_id TEXT NOT NULL,
    since INTEGER NOT NULL,
    PRIMARY KEY (player_id, friend_id)
  );

  CREATE TABLE IF NOT EXISTS friend_requests (
    id TEXT PRIMARY KEY,
    from_player_id TEXT NOT NULL,
    from_nickname TEXT NOT NULL,
    to_player_id TEXT NOT NULL,
    to_nickname TEXT NOT NULL,
    status TEXT NOT NULL,
    created_at INTEGER NOT NULL
  );

  CREATE TABLE IF NOT EXISTS dm_messages (
    id TEXT PRIMARY KEY,
    from_player_id TEXT NOT NULL,
    from_nickname TEXT NOT NULL,
    to_player_id TEXT NOT NULL,
    text TEXT NOT NULL,
    created_at INTEGER NOT NULL
  );

  CREATE TABLE IF NOT EXISTS dm_read_cursor (
    player_id TEXT NOT NULL,
    friend_player_id TEXT NOT NULL,
    last_read_at INTEGER NOT NULL,
    PRIMARY KEY (player_id, friend_player_id)
  );

  CREATE INDEX IF NOT EXISTS idx_inventory_player ON inventory(player_id);
  CREATE INDEX IF NOT EXISTS idx_pond_fish_pond ON pond_fish(pond_id);
  CREATE INDEX IF NOT EXISTS idx_posts_created ON social_posts(created_at DESC);
`);

import { migrateFishingV2 } from './migrations/fishing_v2.js';
import { migratePlayerGear } from './migrations/player_gear.js';
import { migrateCPhase } from './migrations/c_phase.js';
import { migrateFishingV2Mult } from './migrations/fishing_v2_mult.js';
import { migrateBiteCheckInterval } from './migrations/bite_check_interval.js';
import { migrateBiteWeightPrecision } from './migrations/bite_weight_precision.js';
import { migrateQualitySizeBiteEscape } from './migrations/quality_size_bite_escape.js';
import { migrateBiteCheckInterval300s } from './migrations/bite_check_interval_300s.js';
import { migratePondSupplementState } from './migrations/pond_supplement_state.js';
import { migrateAbsoluteGrowthCurve } from './migrations/absolute_growth_curve.js';
import { migratePondFishSpotId } from './migrations/pond_fish_spot_id.js';
import { migratePondOfflineEcology } from './migrations/pond_offline_ecology.js';
import { migrateBiteCheckInterval60s } from './migrations/bite_check_interval_60s.js';
import { migrateFishingMetricsPlayerIdx } from './migrations/fishing_metrics_player_idx.js';
import { migrateMetricsDedup } from './migrations/metrics_dedup.js';
import { migratePlayerPondSession } from './migrations/player_pond_session.js';
import { FISH_BITE_CHECK_MS } from '@fish-social/shared';
import { initGameConfig } from './gameConfig.js';

const migration = migrateFishingV2(db);
if (migration.biteWeightColumnAdded) {
  console.log('Migration: pond_fish.bite_weight column added');
}
if (migration.qualityUpgrades.length > 0) {
  console.log(`Migration: upgraded quality for ${migration.qualityUpgrades.length} pond_fish rows`);
}
if (migration.remainingInvalid > 0) {
  console.warn(`Migration: ${migration.remainingInvalid} pond_fish rows still exceed quality cap`);
}

const gearMigration = migratePlayerGear(db);
if (gearMigration.rowsBackfilled > 0) {
  console.log(`Migration: player_gear backfilled for ${gearMigration.rowsBackfilled} players`);
}

const cPhase = migrateCPhase(db);
if (cPhase.tablesCreated.length > 0) {
  console.log(`Migration C-phase: ${cPhase.tablesCreated.join(', ')}`);
}

const v2Mult = migrateFishingV2Mult(db);
if (v2Mult.birthSizeColumnAdded) {
  console.log('Migration: pond_fish.birth_size_m column added');
}
if (v2Mult.sizesRecalculated > 0) {
  console.log(`Migration A0-v2: recalculated size for ${v2Mult.sizesRecalculated} pond_fish rows`);
}
if (v2Mult.spotWeightsReseeded > 0) {
  console.log(`Migration A0-v2: cleared ${v2Mult.spotWeightsReseeded} spot weights for reseed`);
}

const biteInterval = migrateBiteCheckInterval(db);
if (biteInterval.updated) {
  console.log(`Migration: FISH_BITE_CHECK_MS updated to ${FISH_BITE_CHECK_MS}ms`);
}

const biteWeightPrecision = migrateBiteWeightPrecision(db);
if (biteWeightPrecision.rowsUpdated > 0) {
  console.log(`Migration: recomputed bite_weight precision for ${biteWeightPrecision.rowsUpdated} pond_fish rows`);
}

const qualitySizeBite = migrateQualitySizeBiteEscape(db);
if (qualitySizeBite.columnsAdded || qualitySizeBite.rowsConverted > 0) {
  console.log(
    `Migration v0.3.0: bite_multiplier/escape_multiplier columns added, ${qualitySizeBite.rowsConverted} rows converted`,
  );
}

const biteInterval300 = migrateBiteCheckInterval300s(db);
if (biteInterval300.updated) {
  console.log(`Migration v0.3.1: FISH_BITE_CHECK_MS updated to 300000ms`);
}

const pondSupplementState = migratePondSupplementState(db);
if (pondSupplementState.columnAdded) {
  console.log('Migration v0.3.1: pond_state.last_supplement_at column added');
}

const absoluteGrowth = migrateAbsoluteGrowthCurve(db);
if (absoluteGrowth.sizesRecalculated > 0) {
  console.log(
    `Migration v0.3.1: recalculated size for ${absoluteGrowth.sizesRecalculated} pond_fish rows`,
  );
}

const pondFishSpotId = migratePondFishSpotId(db);
if (pondFishSpotId.columnAdded || pondFishSpotId.rowsBackfilled > 0) {
  console.log(
    `Migration v0.4.0: pond_fish.spot_id added, backfilled ${pondFishSpotId.rowsBackfilled} rows`,
  );
}

const pondOfflineEcology = migratePondOfflineEcology(db);
if (pondOfflineEcology.columnAdded || pondOfflineEcology.rowsBackfilled > 0) {
  console.log(
    `Migration v1.0-steam-desktop: pond_state.last_simulated_at added, ` +
      `backfilled ${pondOfflineEcology.rowsBackfilled} rows`,
  );
}

const biteInterval60 = migrateBiteCheckInterval60s(db);
if (biteInterval60.updated) {
  console.log(`Migration v0.4.1: FISH_BITE_CHECK_MS updated to ${FISH_BITE_CHECK_MS}ms`);
}

const fishingMetricsPlayerIdx = migrateFishingMetricsPlayerIdx(db);
if (fishingMetricsPlayerIdx.indexCreated) {
  console.log('Migration v0.4.3: idx_fishing_metrics_player_time created');
}

const metricsDedup = migrateMetricsDedup(db);
if (metricsDedup.columnAdded) {
  console.log('Migration: fishing_metrics.dedup_key column added');
}
if (metricsDedup.indexCreated) {
  console.log('Migration: idx_fishing_metrics_dedup_key created');
}

const playerPondSession = migratePlayerPondSession(db);
if (playerPondSession.tablesCreated.length > 0) {
  console.log(`Migration v0.5: ${playerPondSession.tablesCreated.join(', ')}`);
}

import { migratePlayerPondSessionReturnFee } from './migrations/player_pond_session_return_fee.js';
migratePlayerPondSessionReturnFee(db);
console.log('Migration: player_pond_session.return_fee_mode ensured');

import { migrateErrorLogs } from './migrations/error_logs.js';
import { migrateDailyStats } from './migrations/daily_stats.js';
migrateErrorLogs(db);
migrateDailyStats(db);
import { migrateDailyPondHookEscape } from './migrations/daily_pond_hook_escape.js';
migrateDailyPondHookEscape(db);
import { migrateDailyPondEcology } from './migrations/daily_pond_ecology.js';
migrateDailyPondEcology(db);

import { migrateCorrelationId } from './migrations/correlation_id.js';
import { migrateClientLogs } from './migrations/client_logs.js';
import { migrateAuditLog } from './migrations/audit_log.js';
import { migrateSocialV060 } from './migrations/social_v060.js';
import { migrateInventoryPondId } from './migrations/inventory_pond_id.js';
import { migratePlayerProgress } from './migrations/player_progress.js';
const invPond = migrateInventoryPondId(db);
if (invPond.columnAdded) {
  console.log('Migration: inventory.pond_id column added');
}
const playerProgressMig = migratePlayerProgress(db);
if (playerProgressMig.tablesCreated.length > 0) {
  console.log(`Migration FEAT-PROG-01: ${playerProgressMig.tablesCreated.join(', ')}`);
}
import { migrateForbiddenBans } from './migrations/player_forbidden_bans.js';
const forbiddenBanMig = migrateForbiddenBans(db);
if (forbiddenBanMig.tableCreated) {
  console.log('Migration FEAT-RISK-01: player_forbidden_bans');
}
const corrMigration = migrateCorrelationId(db);
if (corrMigration.columnAdded) {
  console.log('Migration: fishing_metrics.correlation_id column added');
}
const clMigration = migrateClientLogs(db);
if (clMigration.tableCreated) {
  console.log('Migration: client_logs table created');
}
const audMigration = migrateAuditLog(db);
if (audMigration.tableCreated) {
  console.log('Migration: audit_log table created');
}
const socialV060 = migrateSocialV060(db);
if (socialV060.likesTable || socialV060.commentsTable || socialV060.likeCountCol || socialV060.commentCountCol) {
  console.log(
    `Migration v0.6.0 social: likes=${socialV060.likesTable} comments=${socialV060.commentsTable} ` +
      `like_count=${socialV060.likeCountCol} comment_count=${socialV060.commentCountCol} ` +
      `snapshots=${socialV060.snapshotsTable}`,
  );
}

import { migrateLeaderboardWeeklyBiggest } from './migrations/leaderboard_weekly_biggest.js';
import { migrateSteamAccounts } from './migrations/steam_accounts.js';
const weeklyBiggestMigration = migrateLeaderboardWeeklyBiggest(db);
if (weeklyBiggestMigration.oldSnapshotsCleared) {
  console.log('Migration v0.6.1: cleared old weekly-king snapshots (board_type changed)');
}

const steamAccountsMigration = migrateSteamAccounts(db);
if (steamAccountsMigration.tableCreated) {
  console.log('Migration v1.0-steam-desktop: steam_accounts table created');
}

import { migratePlayerAlbumAchievements } from './migrations/player_album_achievements.js';
const albumAchMig = migratePlayerAlbumAchievements(db);
if (albumAchMig.tablesCreated.length > 0) {
  console.log(`Migration FEAT-ALBUM-01: ${albumAchMig.tablesCreated.join(', ')}`);
}

import { migrateFishCn01Species } from './migrations/fish_cn_01_species.js';
const fishCn01 = migrateFishCn01Species(db);
if (fishCn01.pondFishCleared > 0 || fishCn01.compensated > 0) {
  console.log(
    `Migration FEAT-FISH-CN-01: pond_fish cleared=${fishCn01.pondFishCleared} inventory compensated=${fishCn01.compensated}`,
  );
}

initGameConfig();

console.log(`Database ready: ${dbPath}`);

let dbClosed = false;
export function closeDb(): void {
  if (dbClosed) return;
  db.close();
  dbClosed = true;
}

