import type Database from 'better-sqlite3';

export function migrateSocialV060(db: Database): {
  likesTable: boolean;
  commentsTable: boolean;
  likeCountCol: boolean;
  commentCountCol: boolean;
  snapshotsTable: boolean;
} {
  const result = {
    likesTable: false,
    commentsTable: false,
    likeCountCol: false,
    commentCountCol: false,
    snapshotsTable: false,
  };

  const hasTable = (name: string) =>
    !!db.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name=?").get(name);

  if (!hasTable('post_likes')) {
    db.exec(`
      CREATE TABLE post_likes (
        post_id TEXT NOT NULL,
        player_id TEXT NOT NULL,
        created_at INTEGER NOT NULL,
        PRIMARY KEY (post_id, player_id)
      );
      CREATE INDEX IF NOT EXISTS idx_post_likes_post ON post_likes(post_id, created_at DESC);
    `);
    result.likesTable = true;
  }

  if (!hasTable('post_comments')) {
    db.exec(`
      CREATE TABLE post_comments (
        id TEXT PRIMARY KEY,
        post_id TEXT NOT NULL,
        player_id TEXT NOT NULL,
        nickname TEXT NOT NULL,
        text TEXT NOT NULL,
        created_at INTEGER NOT NULL
      );
      CREATE INDEX IF NOT EXISTS idx_post_comments_post ON post_comments(post_id, created_at ASC);
    `);
    result.commentsTable = true;
  }

  const cols = db.prepare("PRAGMA table_info('social_posts')").all() as Array<{ name: string }>;
  if (!cols.some((c) => c.name === 'like_count')) {
    db.exec('ALTER TABLE social_posts ADD COLUMN like_count INTEGER NOT NULL DEFAULT 0');
    result.likeCountCol = true;
  }
  if (!cols.some((c) => c.name === 'comment_count')) {
    db.exec('ALTER TABLE social_posts ADD COLUMN comment_count INTEGER NOT NULL DEFAULT 0');
    result.commentCountCol = true;
  }

  if (!hasTable('leaderboard_snapshots')) {
    db.exec(`
      CREATE TABLE leaderboard_snapshots (
        board_type TEXT NOT NULL,
        period_key TEXT NOT NULL,
        rank INTEGER NOT NULL,
        player_id TEXT NOT NULL,
        value REAL NOT NULL,
        extra_json TEXT,
        snapshot_at INTEGER NOT NULL,
        PRIMARY KEY (board_type, period_key, rank)
      );
      CREATE INDEX IF NOT EXISTS idx_lb_player ON leaderboard_snapshots(board_type, period_key, player_id);
    `);
    result.snapshotsTable = true;
  }

  return result;
}
