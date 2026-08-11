import { db } from './db.js';
import { anonymizePlayerId } from './playerAnonymize.js';
import { getPlayerCodex } from './codex.js';
import { getFriends } from './friends.js';
import { getInventory } from './inventory.js';
import { getPlayerGear } from './gear.js';
import { getPlayer } from './players.js';
import { getPlayerPosts } from './posts.js';
import { stopDebugSampling } from './debugSampler.js';

export interface EraseTableCounts {
  [table: string]: number;
}

export interface ErasePlan {
  playerId: string;
  exists: boolean;
  toDelete: EraseTableCounts;
  toAnonymize: EraseTableCounts;
}

export interface EraseResult {
  ok: boolean;
  dryRun: boolean;
  playerId: string;
  anonymizedId?: string;
  deletedTables: EraseTableCounts;
  anonymizedRows: EraseTableCounts;
}

function count(sql: string, ...params: unknown[]): number {
  const row = db.prepare(sql).get(...params) as { c: number } | undefined;
  return row?.c ?? 0;
}

function tableExists(name: string): boolean {
  const row = db.prepare("SELECT name FROM sqlite_master WHERE type='table' AND name=?").get(name);
  return !!row;
}

export function playerExists(playerId: string): boolean {
  return !!getPlayer(playerId);
}

function metricsSummaryByDay(playerId: string) {
  const rows = db
    .prepare(
      `SELECT created_at, event_type FROM fishing_metrics WHERE player_id = ? ORDER BY created_at`,
    )
    .all(playerId) as Array<{ created_at: number; event_type: string }>;

  const byDay = new Map<string, { eventCount: number; catchCount: number }>();
  for (const row of rows) {
    const dateKey = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Shanghai' }).format(
      new Date(row.created_at),
    );
    const bucket = byDay.get(dateKey) ?? { eventCount: 0, catchCount: 0 };
    bucket.eventCount += 1;
    if (row.event_type === 'catch_accept' || row.event_type === 'pending_catch_accept') {
      bucket.catchCount += 1;
    }
    byDay.set(dateKey, bucket);
  }
  return [...byDay.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([dateKey, v]) => ({ dateKey, ...v }));
}

export function buildPlayerExport(playerId: string) {
  const profile = getPlayer(playerId);
  if (!profile) return null;

  const friends = getFriends(playerId).map((f) => f.playerId);
  const posts = getPlayerPosts(playerId, playerId, 50);
  const dmPeers = db
    .prepare(
      `SELECT DISTINCT CASE WHEN from_player_id = ? THEN to_player_id ELSE from_player_id END as peer
       FROM dm_messages WHERE from_player_id = ? OR to_player_id = ?`,
    )
    .all(playerId, playerId, playerId) as Array<{ peer: string }>;

  const postLikes = tableExists('post_likes')
    ? (db
        .prepare(
          `SELECT post_id, created_at FROM post_likes WHERE player_id = ? ORDER BY created_at DESC`,
        )
        .all(playerId) as Array<{ post_id: string; created_at: number }>)
    : [];
  const postComments = tableExists('post_comments')
    ? (db
        .prepare(
          `SELECT id, post_id, text, created_at FROM post_comments WHERE player_id = ? ORDER BY created_at DESC`,
        )
        .all(playerId) as Array<{
        id: string;
        post_id: string;
        text: string;
        created_at: number;
      }>)
    : [];

  return {
    exportedAt: new Date().toISOString(),
    playerId,
    profile,
    inventory: getInventory(playerId),
    gear: getPlayerGear(playerId) ?? null,
    codex: getPlayerCodex(playerId),
    social: {
      friendIds: friends,
      postCount: posts.length,
      postIds: posts.map((p) => p.id),
      dmPeerIds: dmPeers.map((r) => r.peer),
      postLikes: postLikes.map((r) => ({ postId: r.post_id, likedAt: r.created_at })),
      postComments: postComments.map((r) => ({
        id: r.id,
        postId: r.post_id,
        text: r.text,
        createdAt: r.created_at,
      })),
    },
    metricsSummary: metricsSummaryByDay(playerId),
  };
}

export function planPlayerErase(playerId: string): ErasePlan {
  const exists = playerExists(playerId);
  const toDelete: EraseTableCounts = {
    inventory: count('SELECT COUNT(*) as c FROM inventory WHERE player_id = ?', playerId),
    player_gear: count('SELECT COUNT(*) as c FROM player_gear WHERE player_id = ?', playerId),
    fish_codex: count('SELECT COUNT(*) as c FROM fish_codex WHERE player_id = ?', playerId),
    social_posts: count('SELECT COUNT(*) as c FROM social_posts WHERE player_id = ?', playerId),
    friend_links: count(
      'SELECT COUNT(*) as c FROM friend_links WHERE player_id = ? OR friend_id = ?',
      playerId,
      playerId,
    ),
    friend_requests: count(
      'SELECT COUNT(*) as c FROM friend_requests WHERE from_player_id = ? OR to_player_id = ?',
      playerId,
      playerId,
    ),
    dm_messages: count(
      'SELECT COUNT(*) as c FROM dm_messages WHERE from_player_id = ? OR to_player_id = ?',
      playerId,
      playerId,
    ),
    dm_read_cursor: count(
      'SELECT COUNT(*) as c FROM dm_read_cursor WHERE player_id = ? OR friend_player_id = ?',
      playerId,
      playerId,
    ),
    daily_fishing: count('SELECT COUNT(*) as c FROM daily_fishing WHERE user_id = ?', playerId),
    players: exists ? 1 : 0,
  };

  if (tableExists('post_likes')) {
    toDelete.post_likes = count('SELECT COUNT(*) as c FROM post_likes WHERE player_id = ?', playerId);
  }
  if (tableExists('post_comments')) {
    toDelete.post_comments = count(
      'SELECT COUNT(*) as c FROM post_comments WHERE player_id = ?',
      playerId,
    );
  }

  if (tableExists('player_pond_session')) {
    toDelete.player_pond_session = count(
      'SELECT COUNT(*) as c FROM player_pond_session WHERE player_id = ?',
      playerId,
    );
  }
  if (tableExists('pending_catch_locks')) {
    toDelete.pending_catch_locks = count(
      'SELECT COUNT(*) as c FROM pending_catch_locks WHERE player_id = ? OR user_id = ?',
      playerId,
      playerId,
    );
  }
  if (tableExists('client_logs')) {
    toDelete.client_logs = count('SELECT COUNT(*) as c FROM client_logs WHERE player_id = ?', playerId);
  }

  const toAnonymize: EraseTableCounts = {
    fishing_metrics: count('SELECT COUNT(*) as c FROM fishing_metrics WHERE player_id = ?', playerId),
    daily_player_stats: count(
      'SELECT COUNT(*) as c FROM daily_player_stats WHERE player_id = ?',
      playerId,
    ),
  };

  return { playerId, exists, toDelete, toAnonymize };
}

export function erasePlayerData(playerId: string, opts: { dryRun?: boolean } = {}): EraseResult {
  const dryRun = !!opts.dryRun;
  const plan = planPlayerErase(playerId);

  if (!plan.exists) {
    return {
      ok: false,
      dryRun,
      playerId,
      deletedTables: plan.toDelete,
      anonymizedRows: plan.toAnonymize,
    };
  }

  if (dryRun) {
    return {
      ok: true,
      dryRun: true,
      playerId,
      deletedTables: plan.toDelete,
      anonymizedRows: plan.toAnonymize,
    };
  }

  stopDebugSampling(playerId);
  const anonId = anonymizePlayerId(playerId);
  const deletedTables: EraseTableCounts = {};
  const anonymizedRows: EraseTableCounts = {};

  const tx = db.transaction(() => {
    if (tableExists('player_pond_session')) {
      deletedTables.player_pond_session = db
        .prepare('DELETE FROM player_pond_session WHERE player_id = ?')
        .run(playerId).changes;
    }
    if (tableExists('pending_catch_locks')) {
      deletedTables.pending_catch_locks = db
        .prepare('DELETE FROM pending_catch_locks WHERE player_id = ? OR user_id = ?')
        .run(playerId, playerId).changes;
    }

    deletedTables.inventory = db.prepare('DELETE FROM inventory WHERE player_id = ?').run(playerId).changes;
    deletedTables.player_gear = db.prepare('DELETE FROM player_gear WHERE player_id = ?').run(playerId).changes;
    deletedTables.fish_codex = db.prepare('DELETE FROM fish_codex WHERE player_id = ?').run(playerId).changes;

    if (tableExists('post_likes')) {
      const likePosts = db
        .prepare('SELECT post_id FROM post_likes WHERE player_id = ?')
        .all(playerId) as Array<{ post_id: string }>;
      const decLike = db.prepare(
        'UPDATE social_posts SET like_count = MAX(0, like_count - 1) WHERE id = ?',
      );
      for (const row of likePosts) decLike.run(row.post_id);
      deletedTables.post_likes = db
        .prepare('DELETE FROM post_likes WHERE player_id = ?')
        .run(playerId).changes;
    }

    if (tableExists('post_comments')) {
      const commentPosts = db
        .prepare('SELECT post_id FROM post_comments WHERE player_id = ?')
        .all(playerId) as Array<{ post_id: string }>;
      const decComment = db.prepare(
        'UPDATE social_posts SET comment_count = MAX(0, comment_count - 1) WHERE id = ?',
      );
      for (const row of commentPosts) decComment.run(row.post_id);
      deletedTables.post_comments = db
        .prepare('DELETE FROM post_comments WHERE player_id = ?')
        .run(playerId).changes;
    }

    if (tableExists('post_likes')) {
      db.prepare(
        `DELETE FROM post_likes WHERE post_id IN (SELECT id FROM social_posts WHERE player_id = ?)`,
      ).run(playerId);
    }
    if (tableExists('post_comments')) {
      db.prepare(
        `DELETE FROM post_comments WHERE post_id IN (SELECT id FROM social_posts WHERE player_id = ?)`,
      ).run(playerId);
    }

    deletedTables.social_posts = db.prepare('DELETE FROM social_posts WHERE player_id = ?').run(playerId).changes;
    deletedTables.friend_links = db
      .prepare('DELETE FROM friend_links WHERE player_id = ? OR friend_id = ?')
      .run(playerId, playerId).changes;
    deletedTables.friend_requests = db
      .prepare('DELETE FROM friend_requests WHERE from_player_id = ? OR to_player_id = ?')
      .run(playerId, playerId).changes;
    deletedTables.dm_messages = db
      .prepare('DELETE FROM dm_messages WHERE from_player_id = ? OR to_player_id = ?')
      .run(playerId, playerId).changes;
    deletedTables.dm_read_cursor = db
      .prepare('DELETE FROM dm_read_cursor WHERE player_id = ? OR friend_player_id = ?')
      .run(playerId, playerId).changes;
    deletedTables.daily_fishing = db
      .prepare('DELETE FROM daily_fishing WHERE user_id = ?')
      .run(playerId).changes;

    if (tableExists('client_logs')) {
      deletedTables.client_logs = db
        .prepare('DELETE FROM client_logs WHERE player_id = ?')
        .run(playerId).changes;
    }

    deletedTables.players = db.prepare('DELETE FROM players WHERE player_id = ?').run(playerId).changes;

    anonymizedRows.fishing_metrics = db
      .prepare('UPDATE fishing_metrics SET player_id = ? WHERE player_id = ?')
      .run(anonId, playerId).changes;
    anonymizedRows.daily_player_stats = db
      .prepare('UPDATE daily_player_stats SET player_id = ? WHERE player_id = ?')
      .run(anonId, playerId).changes;
  });

  tx();

  return {
    ok: true,
    dryRun: false,
    playerId,
    anonymizedId: anonId,
    deletedTables,
    anonymizedRows,
  };
}
