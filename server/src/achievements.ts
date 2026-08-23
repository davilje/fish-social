import type { AchievementDef, AchievementView, PlayerAchievementUnlock } from '@fish-social/shared';
import { listAchievements, getAchievementDef } from '@fish-social/shared';
import { db } from './db.js';
import { getPlayerCodex } from './codex.js';
import { countAlbumPins } from './album.js';
import { recordFishingMetric } from './fishingMetrics.js';

type UnlockRow = { achievement_id: string; unlocked_at: number };

const listUnlocksStmt = db.prepare(
  `SELECT achievement_id, unlocked_at FROM player_achievements WHERE player_id = ?`,
);
const hasUnlockStmt = db.prepare(
  `SELECT 1 AS ok FROM player_achievements WHERE player_id = ? AND achievement_id = ?`,
);
const insertUnlockStmt = db.prepare(
  `INSERT OR IGNORE INTO player_achievements (player_id, achievement_id, unlocked_at)
   VALUES (?, ?, ?)`,
);

export function listPlayerUnlocks(playerId: string): PlayerAchievementUnlock[] {
  const rows = listUnlocksStmt.all(playerId) as UnlockRow[];
  return rows.map((r) => ({
    achievementId: r.achievement_id,
    unlockedAt: r.unlocked_at,
  }));
}

function countCodexUnlocked(playerId: string): number {
  return getPlayerCodex(playerId).filter((e) => e.totalCaught > 0).length;
}

function countReturns(playerId: string): number {
  const row = db
    .prepare(
      `SELECT COUNT(*) AS c FROM fishing_metrics
       WHERE player_id = ? AND event_type = 'fish_returned_to_pond'`,
    )
    .get(playerId) as { c: number } | undefined;
  return row?.c ?? 0;
}

function countCatches(playerId: string): number {
  const row = db
    .prepare(
      `SELECT COUNT(*) AS c FROM inventory WHERE player_id = ?`,
    )
    .get(playerId) as { c: number } | undefined;
  const inv = row?.c ?? 0;
  // Also count historical via codex totals as proxy for "first catch" progress
  const codex = getPlayerCodex(playerId).reduce((s, e) => s + e.totalCaught, 0);
  return Math.max(inv, codex);
}

function maxCatchSizeM(playerId: string): number {
  const inv = db
    .prepare(`SELECT MAX(size_m) AS m FROM inventory WHERE player_id = ?`)
    .get(playerId) as { m: number | null } | undefined;
  const codex = db
    .prepare(`SELECT MAX(max_size_m) AS m FROM fish_codex WHERE player_id = ?`)
    .get(playerId) as { m: number | null } | undefined;
  return Math.max(inv?.m ?? 0, codex?.m ?? 0);
}

function progressValue(playerId: string, conditionType: string): number {
  switch (conditionType) {
    case 'catch_count':
      return countCatches(playerId);
    case 'codex_count':
      return countCodexUnlocked(playerId);
    case 'return_count':
      return countReturns(playerId);
    case 'max_size':
      return maxCatchSizeM(playerId);
    case 'album_pins':
      return countAlbumPins(playerId);
    default:
      return 0;
  }
}

export function buildAchievementViews(
  playerId: string,
  opts?: { forPublic?: boolean },
): AchievementView[] {
  const unlocks = new Map(listPlayerUnlocks(playerId).map((u) => [u.achievementId, u.unlockedAt]));
  const defs = listAchievements();
  const views: AchievementView[] = [];
  for (const def of defs) {
    const unlockedAt = unlocks.get(def.achievementId) ?? null;
    const unlocked = unlockedAt != null;
    if (opts?.forPublic && !unlocked) continue;
    if (opts?.forPublic && def.isHidden && !unlocked) continue;
    views.push({
      ...def,
      unlocked,
      unlockedAt,
      // Hide condition text for locked hidden achievements to viewers — keep fields but UI must respect isHidden
      desc: opts?.forPublic && !unlocked && def.isHidden ? '' : def.desc,
    });
  }
  return views.sort((a, b) => a.sortOrder - b.sortOrder);
}

export function tryUnlockAchievements(
  playerId: string,
): AchievementDef[] {
  const newly: AchievementDef[] = [];
  const now = Date.now();
  for (const def of listAchievements()) {
    if (hasUnlockStmt.get(playerId, def.achievementId)) continue;
    const value = progressValue(playerId, def.conditionType);
    const met =
      def.conditionType === 'max_size'
        ? value + 1e-9 >= def.conditionValue
        : value >= def.conditionValue;
    if (!met) continue;
    const info = insertUnlockStmt.run(playerId, def.achievementId, now);
    if (info.changes > 0) {
      newly.push(def);
      recordFishingMetric('achievement_unlocked', {
        playerId,
        payload: { achievementId: def.achievementId },
      });
    }
  }
  return newly;
}

export function getAchievementCatalog(): AchievementDef[] {
  return listAchievements();
}

export function resolveAchievement(achievementId: string): AchievementDef | undefined {
  return getAchievementDef(achievementId);
}
