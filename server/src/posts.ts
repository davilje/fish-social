import { randomUUID } from 'crypto';
import type { FishInventoryItem, ShareVisibility, SocialPost } from '@fish-social/shared';
import {
  formatEpicCatchPostText,
  formatPostFishText,
  isAnnounceQuality,
  resolveFishingPhotoPath,
} from '@fish-social/shared';
import { getFishById } from './inventory.js';
import { areFriends } from './friends.js';
import { getPlayer } from './players.js';
import { db } from './db.js';

const MAX_POSTS = 500;

interface PostRow {
  id: string;
  player_id: string;
  nickname: string;
  author_avatar_url: string | null;
  fish_json: string;
  text: string;
  photo_url: string | null;
  visibility: string;
  created_at: number;
  like_count?: number | null;
  comment_count?: number | null;
}

interface PostExtras {
  photoUrl?: string;
  authorAvatarUrl?: string;
  text?: string;
}

const insertPostStmt = db.prepare(`
  INSERT INTO social_posts (id, player_id, nickname, author_avatar_url, fish_json, text, photo_url, visibility, created_at)
  VALUES (@id, @playerId, @nickname, @authorAvatarUrl, @fishJson, @text, @photoUrl, @visibility, @createdAt)
`);
const getPostStmt = db.prepare(`SELECT * FROM social_posts WHERE id = ?`);
const listWallStmt = db.prepare(`
  SELECT * FROM social_posts WHERE visibility = 'public'
  ORDER BY created_at DESC LIMIT ? OFFSET ?
`);
const listWallByLikesStmt = db.prepare(`
  SELECT * FROM social_posts WHERE visibility = 'public'
  ORDER BY like_count DESC, created_at DESC LIMIT ? OFFSET ?
`);
const listFriendsFeedStmt = db.prepare(`
  SELECT p.* FROM social_posts p
  WHERE p.player_id = ?
     OR (
       EXISTS (
         SELECT 1 FROM friend_links f
         WHERE f.player_id = ? AND f.friend_id = p.player_id
       )
       AND p.visibility IN ('public', 'friends')
     )
  ORDER BY p.created_at DESC LIMIT ? OFFSET ?
`);
const listFriendsFeedByLikesStmt = db.prepare(`
  SELECT p.* FROM social_posts p
  WHERE p.player_id = ?
     OR (
       EXISTS (
         SELECT 1 FROM friend_links f
         WHERE f.player_id = ? AND f.friend_id = p.player_id
       )
       AND p.visibility IN ('public', 'friends')
     )
  ORDER BY p.like_count DESC, p.created_at DESC LIMIT ? OFFSET ?
`);
const listPlayerPostsStmt = db.prepare(`
  SELECT * FROM social_posts WHERE player_id = ?
  ORDER BY created_at DESC LIMIT ${MAX_POSTS}
`);
const trimPostsStmt = db.prepare(`
  DELETE FROM social_posts WHERE id NOT IN (
    SELECT id FROM social_posts ORDER BY created_at DESC LIMIT ${MAX_POSTS}
  )
`);

function rowToPost(row: PostRow): SocialPost {
  let fish: FishInventoryItem;
  try {
    fish = JSON.parse(row.fish_json) as FishInventoryItem;
  } catch {
    fish = {
      id: 'unknown',
      speciesId: 'crucian',
      quality: 'gray',
      sizeM: 0,
      caughtAt: row.created_at,
    };
  }
  return {
    id: row.id,
    playerId: row.player_id,
    nickname: row.nickname,
    ...(row.author_avatar_url ? { authorAvatarUrl: row.author_avatar_url } : {}),
    fish,
    text: row.text,
    ...(row.photo_url ? { photoUrl: row.photo_url } : {}),
    visibility: row.visibility as ShareVisibility,
    createdAt: row.created_at,
    likeCount: row.like_count ?? 0,
    commentCount: row.comment_count ?? 0,
  };
}

function enrichPostsWithLikes(posts: SocialPost[], viewerId?: string | null): SocialPost[] {
  if (posts.length === 0) return posts;
  if (!viewerId) {
    return posts.map((p) => ({
      ...p,
      likeCount: p.likeCount ?? 0,
      commentCount: p.commentCount ?? 0,
    }));
  }
  const ids = posts.map((p) => p.id);
  const placeholders = ids.map(() => '?').join(',');
  const likedRows = db
    .prepare(
      `SELECT post_id FROM post_likes WHERE player_id = ? AND post_id IN (${placeholders})`,
    )
    .all(viewerId, ...ids) as Array<{ post_id: string }>;
  const liked = new Set(likedRows.map((r) => r.post_id));
  return posts.map((p) => ({
    ...p,
    likeCount: p.likeCount ?? 0,
    commentCount: p.commentCount ?? 0,
    likedByMe: liked.has(p.id),
  }));
}

function buildPost(
  playerId: string,
  nickname: string,
  fish: FishInventoryItem,
  visibility: ShareVisibility,
  extras?: PostExtras,
): SocialPost {
  const player = getPlayer(playerId);
  const photoUrl =
    extras?.photoUrl ??
    (isAnnounceQuality(fish.quality)
      ? resolveFishingPhotoPath(extras?.authorAvatarUrl ?? player?.avatarUrl, playerId)
      : undefined);

  const text =
    extras?.text ??
    (photoUrl ? formatEpicCatchPostText(fish) : formatPostFishText(fish));

  return {
    id: randomUUID(),
    playerId,
    nickname: nickname.slice(0, 12),
    authorAvatarUrl: extras?.authorAvatarUrl ?? player?.avatarUrl,
    fish: { ...fish },
    text,
    photoUrl,
    visibility,
    createdAt: Date.now(),
    likeCount: 0,
    commentCount: 0,
  };
}

function savePost(post: SocialPost): SocialPost {
  insertPostStmt.run({
    id: post.id,
    playerId: post.playerId,
    nickname: post.nickname,
    authorAvatarUrl: post.authorAvatarUrl ?? null,
    fishJson: JSON.stringify(post.fish),
    text: post.text,
    photoUrl: post.photoUrl ?? null,
    visibility: post.visibility,
    createdAt: post.createdAt,
  });
  trimPostsStmt.run();
  return { ...post, likeCount: post.likeCount ?? 0, commentCount: post.commentCount ?? 0 };
}

export function getPostById(postId: string): SocialPost | null {
  const row = getPostStmt.get(postId) as PostRow | undefined;
  return row ? rowToPost(row) : null;
}

export function createPostFromFish(
  playerId: string,
  nickname: string,
  fish: FishInventoryItem,
  visibility: ShareVisibility = 'public',
  extras?: PostExtras,
): SocialPost {
  return savePost(buildPost(playerId, nickname, fish, visibility, extras));
}

export function autoShareEpicCatch(
  playerId: string,
  nickname: string,
  fish: FishInventoryItem,
): SocialPost | null {
  if (!isAnnounceQuality(fish.quality)) return null;
  const player = getPlayer(playerId);
  const visibility = player?.shareVisibility ?? 'public';
  return createPostFromFish(playerId, nickname, fish, visibility, {
    authorAvatarUrl: player?.avatarUrl,
  });
}

export function createPost(
  playerId: string,
  nickname: string,
  fishId: string,
  visibility?: ShareVisibility,
): { ok: true; post: SocialPost } | { ok: false; error: string } {
  const player = getPlayer(playerId);
  if (!player) return { ok: false, error: '玩家不存在' };

  const fish = getFishById(playerId, fishId);
  if (!fish) return { ok: false, error: '鱼不存在' };

  const vis = visibility ?? player.shareVisibility;
  const post = createPostFromFish(playerId, nickname, fish, vis, {
    authorAvatarUrl: player.avatarUrl,
  });
  return { ok: true, post };
}

export function getWallPosts(
  viewerId?: string | null,
  sort: 'time' | 'likes' = 'time',
  limit = 50,
  offset = 0,
): SocialPost[] {
  const safeLimit = Math.min(Math.max(1, limit), 50);
  const safeOffset = Math.max(0, offset);
  const rows = (
    sort === 'likes'
      ? listWallByLikesStmt.all(safeLimit, safeOffset)
      : listWallStmt.all(safeLimit, safeOffset)
  ) as PostRow[];
  return enrichPostsWithLikes(rows.map(rowToPost), viewerId);
}

export function getFriendsPosts(
  viewerPlayerId: string,
  sort: 'time' | 'likes' = 'time',
  limit = 50,
  offset = 0,
): SocialPost[] {
  const stmt = sort === 'likes' ? listFriendsFeedByLikesStmt : listFriendsFeedStmt;
  const safeLimit = Math.min(Math.max(1, limit), 50);
  const safeOffset = Math.max(0, offset);
  return enrichPostsWithLikes(
    (stmt.all(viewerPlayerId, viewerPlayerId, safeLimit, safeOffset) as PostRow[]).map(rowToPost),
    viewerPlayerId,
  );
}

export function getPlayerPosts(
  playerId: string,
  viewerPlayerId: string,
  limit = 20,
): SocialPost[] {
  return enrichPostsWithLikes(
    (listPlayerPostsStmt.all(playerId) as PostRow[])
      .map(rowToPost)
      .filter((p) => {
        if (p.visibility === 'public') return true;
        if (p.visibility === 'friends') {
          return playerId === viewerPlayerId || areFriends(viewerPlayerId, playerId);
        }
        return false;
      })
      .slice(0, limit),
    viewerPlayerId,
  );
}

export function clearAllPosts(): void {
  db.exec('DELETE FROM post_likes');
  db.exec('DELETE FROM post_comments');
  db.exec('DELETE FROM social_posts');
}
