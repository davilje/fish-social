import { randomUUID } from 'crypto';
import {
  POST_COMMENT_MAX_LENGTH,
  type PostComment,
  type PostLikeUser,
  type SocialPost,
} from '@fish-social/shared';
import { areFriends } from './friends.js';
import { getPlayer } from './players.js';
import { db } from './db.js';
import { getPostById } from './posts.js';

const COMMENT_COOLDOWN_MS = 3000;
const lastCommentAtByPlayer = new Map<string, number>();

export function canViewerSeePost(
  viewerId: string | null | undefined,
  post: Pick<SocialPost, 'playerId' | 'visibility'>,
): boolean {
  if (post.visibility === 'public') return true;
  if (post.visibility === 'friends') {
    if (!viewerId) return false;
    if (viewerId === post.playerId) return true;
    return areFriends(viewerId, post.playerId);
  }
  return false;
}

export function getVisiblePost(
  postId: string,
  viewerId: string | null | undefined,
): SocialPost | null {
  const post = getPostById(postId);
  if (!post) return null;
  if (!canViewerSeePost(viewerId, post)) return null;
  return post;
}

export function togglePostLike(
  postId: string,
  playerId: string,
): { ok: true; liked: boolean; likeCount: number } | { ok: false; error: string; status: number } {
  const post = getVisiblePost(postId, playerId);
  if (!post) return { ok: false, error: 'not_found', status: 404 };

  const existing = db
    .prepare('SELECT 1 FROM post_likes WHERE post_id = ? AND player_id = ?')
    .get(postId, playerId);

  const tx = db.transaction(() => {
    if (existing) {
      db.prepare('DELETE FROM post_likes WHERE post_id = ? AND player_id = ?').run(postId, playerId);
      db.prepare(
        'UPDATE social_posts SET like_count = MAX(0, like_count - 1) WHERE id = ?',
      ).run(postId);
      return false;
    }
    db.prepare(
      'INSERT INTO post_likes (post_id, player_id, created_at) VALUES (?, ?, ?)',
    ).run(postId, playerId, Date.now());
    db.prepare('UPDATE social_posts SET like_count = like_count + 1 WHERE id = ?').run(postId);
    return true;
  });

  const liked = tx();
  const row = db.prepare('SELECT like_count FROM social_posts WHERE id = ?').get(postId) as
    | { like_count: number }
    | undefined;
  return { ok: true, liked, likeCount: row?.like_count ?? 0 };
}

export function listPostLikes(
  postId: string,
  viewerId: string | null | undefined,
  limit = 50,
  offset = 0,
): { ok: true; likes: PostLikeUser[] } | { ok: false; error: string; status: number } {
  const post = getVisiblePost(postId, viewerId);
  if (!post) return { ok: false, error: 'not_found', status: 404 };

  const safeLimit = Math.min(Math.max(1, limit), 100);
  const safeOffset = Math.max(0, offset);
  const rows = db
    .prepare(
      `SELECT pl.player_id, pl.created_at, p.nickname, p.avatar_url
       FROM post_likes pl
       LEFT JOIN players p ON p.player_id = pl.player_id
       WHERE pl.post_id = ?
       ORDER BY pl.created_at DESC
       LIMIT ? OFFSET ?`,
    )
    .all(postId, safeLimit, safeOffset) as Array<{
    player_id: string;
    created_at: number;
    nickname: string | null;
    avatar_url: string | null;
  }>;

  const likes: PostLikeUser[] = rows.map((r) => ({
    playerId: r.player_id,
    nickname: r.nickname ?? '钓友',
    ...(r.avatar_url ? { avatarUrl: r.avatar_url } : {}),
    likedAt: r.created_at,
  }));
  return { ok: true, likes };
}

export function addPostComment(
  postId: string,
  playerId: string,
  textRaw: string,
):
  | { ok: true; comment: PostComment; commentCount: number }
  | { ok: false; error: string; status: number } {
  const post = getVisiblePost(postId, playerId);
  if (!post) return { ok: false, error: 'not_found', status: 404 };

  const text = textRaw.trim();
  if (!text) return { ok: false, error: 'empty_text', status: 400 };
  if (text.length > POST_COMMENT_MAX_LENGTH) {
    return { ok: false, error: 'text_too_long', status: 400 };
  }

  const now = Date.now();
  const last = lastCommentAtByPlayer.get(playerId) ?? 0;
  if (now - last < COMMENT_COOLDOWN_MS) {
    return { ok: false, error: 'rate_limited', status: 429 };
  }

  const player = getPlayer(playerId);
  const nickname = player?.nickname ?? '钓友';

  const comment: PostComment = {
    id: randomUUID(),
    postId,
    playerId,
    nickname,
    ...(player?.avatarUrl ? { avatarUrl: player.avatarUrl } : {}),
    text,
    createdAt: now,
  };

  const tx = db.transaction(() => {
    db.prepare(
      `INSERT INTO post_comments (id, post_id, player_id, nickname, text, created_at)
       VALUES (?, ?, ?, ?, ?, ?)`,
    ).run(comment.id, postId, playerId, nickname, text, now);
    db.prepare('UPDATE social_posts SET comment_count = comment_count + 1 WHERE id = ?').run(postId);
  });
  tx();
  lastCommentAtByPlayer.set(playerId, now);

  const row = db.prepare('SELECT comment_count FROM social_posts WHERE id = ?').get(postId) as
    | { comment_count: number }
    | undefined;
  return { ok: true, comment, commentCount: row?.comment_count ?? 0 };
}

export function listPostComments(
  postId: string,
  viewerId: string | null | undefined,
  limit = 50,
):
  | { ok: true; comments: PostComment[]; commentCount: number }
  | { ok: false; error: string; status: number } {
  const post = getVisiblePost(postId, viewerId);
  if (!post) return { ok: false, error: 'not_found', status: 404 };

  const safeLimit = Math.min(Math.max(1, limit), 100);
  const rows = db
    .prepare(
      `SELECT c.id, c.post_id, c.player_id, c.nickname, c.text, c.created_at, p.avatar_url
       FROM post_comments c
       LEFT JOIN players p ON p.player_id = c.player_id
       WHERE c.post_id = ?
       ORDER BY c.created_at ASC
       LIMIT ?`,
    )
    .all(postId, safeLimit) as Array<{
    id: string;
    post_id: string;
    player_id: string;
    nickname: string;
    text: string;
    created_at: number;
    avatar_url: string | null;
  }>;

  const comments: PostComment[] = rows.map((r) => ({
    id: r.id,
    postId: r.post_id,
    playerId: r.player_id,
    nickname: r.nickname,
    ...(r.avatar_url ? { avatarUrl: r.avatar_url } : {}),
    text: r.text,
    createdAt: r.created_at,
  }));

  const countRow = db.prepare('SELECT comment_count FROM social_posts WHERE id = ?').get(postId) as
    | { comment_count: number }
    | undefined;
  return { ok: true, comments, commentCount: countRow?.comment_count ?? comments.length };
}

export function deletePostComment(
  postId: string,
  commentId: string,
  actorPlayerId: string,
):
  | { ok: true; commentCount: number }
  | { ok: false; error: string; status: number } {
  const post = getVisiblePost(postId, actorPlayerId);
  if (!post) return { ok: false, error: 'not_found', status: 404 };

  const comment = db
    .prepare('SELECT id, player_id, post_id FROM post_comments WHERE id = ? AND post_id = ?')
    .get(commentId, postId) as { id: string; player_id: string; post_id: string } | undefined;
  if (!comment) return { ok: false, error: 'not_found', status: 404 };

  const canDelete = comment.player_id === actorPlayerId || post.playerId === actorPlayerId;
  if (!canDelete) return { ok: false, error: 'forbidden', status: 403 };

  const tx = db.transaction(() => {
    db.prepare('DELETE FROM post_comments WHERE id = ?').run(commentId);
    db.prepare(
      'UPDATE social_posts SET comment_count = MAX(0, comment_count - 1) WHERE id = ?',
    ).run(postId);
  });
  tx();

  const row = db.prepare('SELECT comment_count FROM social_posts WHERE id = ?').get(postId) as
    | { comment_count: number }
    | undefined;
  return { ok: true, commentCount: row?.comment_count ?? 0 };
}

/** Test helper: clear in-memory comment cooldown. */
export function clearCommentRateLimitForTests(): void {
  lastCommentAtByPlayer.clear();
}
