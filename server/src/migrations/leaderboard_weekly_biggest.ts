import type Database from 'better-sqlite3';

export function migrateLeaderboardWeeklyBiggest(db: Database): {
  oldSnapshotsCleared: boolean;
} {
  const result = { oldSnapshotsCleared: false };

  const hasTable = (name: string) =>
    !!db.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name=?").get(name);

  if (!hasTable('leaderboard_snapshots')) {
    return result;
  }

  const info = db.prepare(
    "SELECT COUNT(*) AS cnt FROM leaderboard_snapshots WHERE board_type = 'weekly-king'",
  ).get() as { cnt: number } | undefined;

  if (info && info.cnt > 0) {
    db.prepare("DELETE FROM leaderboard_snapshots WHERE board_type = 'weekly-king'").run();
    result.oldSnapshotsCleared = true;
    console.log(`Migration: cleared ${info.cnt} old weekly-king snapshots (board_type changed)`);
  }

  return result;
}
