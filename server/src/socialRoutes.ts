import type { Express } from 'express';
import { randomUUID } from 'crypto';
import type { Server } from 'socket.io';
import { calcFishSellPrice } from '@fish-social/shared';
import type {
  ClientToServerEvents,
  LeaderboardBoardType,
  ServerToClientEvents,
} from '@fish-social/shared';
import {
  acceptFriendRequest,
  getFriends,
  getIncomingRequests,
  getOutgoingRequests,
  isBotPlayerId,
  rejectFriendRequest,
  removeFriend,
  sendFriendRequest,
} from './friends.js';
import {
  getConversation,
  listConversations,
  markConversationRead,
  sendDirectMessage,
} from './dm.js';
import { getInventory, sellFish, getFishById } from './inventory.js';
import { recordFishingMetric } from './fishingMetrics.js';
import { createPost, getFriendsPosts, getWallPosts } from './posts.js';
import {
  addPostComment,
  deletePostComment,
  listPostComments,
  listPostLikes,
  togglePostLike,
} from './postEngagement.js';
import {
  getDailyBiggestLeaderboard,
  getMyLeaderboardRank,
  getPondLeaderboard,
  getRareLeaderboard,
  getWeeklyKingLeaderboard,
} from './leaderboard.js';
import {
  addCoins,
  ensurePlayer,
  getPlayer,
  getPublicPlayerView,
  searchPlayers,
  setShareVisibility,
  setShowcaseFish,
  updatePlayerProfile,
} from './players.js';
import {
  isAuthDisabled,
  requireAuth,
  requireSelf,
  resolveAuthedPlayerId,
  signPlayerToken,
  tryResolvePlayerId,
} from './auth.js';
import { resolveSocketByPlayer } from './sessionRegistry.js';

function mintPlayerId(): string {
  return `p_${randomUUID().replace(/-/g, '').slice(0, 16)}`;
}

export function registerSocialRoutes(
  app: Express,
  io: Server<ClientToServerEvents, ServerToClientEvents>,
): void {
  /**
   * SEC-01 注册发 JWT：
   * - AUTH_DISABLED（仅 development）：旧行为，任意 playerId（含已有）可签 token
   * - production：忽略客户端 playerId，服务端 mint UUID + 返回 token（方案②）
   * - development + JWT：允许客户端指定**新** id；已有 id → 403 player_id_taken
   * 详见 docs/ops/auth-register.md
   */
  app.post('/api/players/register', (req, res) => {
    const { playerId: requestedId, nickname } = req.body as {
      playerId?: string;
      nickname?: string;
    };
    const nick = nickname ?? '钓友';

    if (isAuthDisabled()) {
      if (!requestedId) return res.status(400).json({ error: '缺少 playerId' });
      const profile = ensurePlayer(requestedId, nick);
      res.json({ profile, token: signPlayerToken(requestedId) });
      return;
    }

    let playerId: string;
    if (process.env.NODE_ENV === 'production') {
      playerId = mintPlayerId();
    } else {
      playerId = requestedId?.trim() || '';
      if (playerId && getPlayer(playerId)) {
        return res.status(403).json({ error: 'forbidden', code: 'player_id_taken' });
      }
      if (!playerId) playerId = mintPlayerId();
    }

    const profile = ensurePlayer(playerId, nick);
    res.json({ profile, token: signPlayerToken(playerId) });
  });

  // SEC-02: private profile (coins etc.) — self only
  app.get('/api/players/:playerId', requireSelf('playerId'), (req, res) => {
    const profile = getPlayer(req.params.playerId);
    if (!profile) return res.status(404).json({ error: '玩家不存在' });
    res.json({ profile });
  });

  // Public: others' profile page — no PII beyond public view (SEC §2.1)
  app.get('/api/players/:playerId/public-view', (req, res) => {
    const viewer = String(req.query.viewer ?? '');
    if (!viewer) return res.status(400).json({ error: '缺少 viewer 参数' });
    const limit = Math.min(50, Math.max(1, Number(req.query.limit) || 20));
    const view = getPublicPlayerView(req.params.playerId, viewer, limit);
    if (!view) return res.status(404).json({ error: '玩家不存在' });
    res.json({ view });
  });

  app.put('/api/players/:playerId/settings', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req, String(req.params.playerId));
    if (!playerId) return res.status(401).json({ error: 'unauthorized' });
    const { shareVisibility } = req.body as { shareVisibility?: 'public' | 'friends' };
    if (!shareVisibility) return res.status(400).json({ error: '缺少设置' });
    const profile = setShareVisibility(playerId, shareVisibility);
    if (!profile) return res.status(404).json({ error: '玩家不存在' });
    res.json({ profile });
  });

  app.put('/api/players/:playerId/profile', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req, String(req.params.playerId));
    if (!playerId) return res.status(401).json({ error: 'unauthorized' });
    const { nickname, bio, avatarUrl } = req.body as {
      nickname?: string;
      bio?: string;
      avatarUrl?: string | null;
    };
    const result = updatePlayerProfile(playerId, { nickname, bio, avatarUrl });
    if (!result.ok) return res.status(400).json({ error: result.error });
    res.json({ profile: result.profile });
  });

  app.put('/api/players/:playerId/showcase', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req, String(req.params.playerId));
    if (!playerId) return res.status(401).json({ error: 'unauthorized' });
    const { slots } = req.body as { slots?: (string | null)[] };
    if (!Array.isArray(slots)) return res.status(400).json({ error: '缺少收藏品数据' });
    for (const fishId of slots) {
      if (fishId && !getFishById(playerId, fishId)) {
        return res.status(400).json({ error: '收藏品中的鱼不存在于背包' });
      }
    }
    const result = setShowcaseFish(playerId, slots);
    if (!result.ok) return res.status(400).json({ error: result.error });
    res.json({ profile: result.profile });
  });

  // Public: nickname search only (SEC §2.1)
  app.get('/api/players/search', (req, res) => {
    const q = String(req.query.q ?? '');
    const exclude = String(req.query.exclude ?? '');
    res.json({ players: searchPlayers(q, exclude || undefined) });
  });

  app.get('/api/friends/:playerId', requireSelf('playerId'), (req, res) => {
    res.json({ friends: getFriends(req.params.playerId) });
  });

  app.get('/api/friends/:playerId/requests', requireSelf('playerId'), (req, res) => {
    res.json({
      incoming: getIncomingRequests(req.params.playerId),
      outgoing: getOutgoingRequests(req.params.playerId),
    });
  });

  app.post('/api/friends/request', requireAuth, (req, res) => {
    const fromPlayerId = resolveAuthedPlayerId(req, (req.body as { fromPlayerId?: string }).fromPlayerId);
    const { fromNickname, toPlayerId } = req.body as {
      fromNickname?: string;
      toPlayerId?: string;
    };
    if (!fromPlayerId || !toPlayerId) {
      return res.status(400).json({ error: '参数不完整' });
    }
    const result = sendFriendRequest(fromPlayerId, fromNickname ?? '钓友', toPlayerId);
    if (!result.ok) return res.status(400).json({ error: result.error });

    if (isBotPlayerId(toPlayerId)) {
      const accepted = acceptFriendRequest(toPlayerId, result.request.id);
      if (accepted.ok) {
        return res.json({ request: { ...result.request, status: 'accepted' as const }, autoAccepted: true });
      }
    }

    const toSocket = resolveSocketByPlayer(toPlayerId);
    if (toSocket) io.to(toSocket).emit('friend_request', result.request);
    res.json({ request: result.request });
  });

  app.post('/api/friends/accept', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req, (req.body as { playerId?: string }).playerId);
    const { requestId } = req.body as { requestId?: string };
    if (!playerId || !requestId) return res.status(400).json({ error: '参数不完整' });
    const result = acceptFriendRequest(playerId, requestId);
    if (!result.ok) return res.status(400).json({ error: result.error });
    res.json({ ok: true });
  });

  app.post('/api/friends/reject', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req, (req.body as { playerId?: string }).playerId);
    const { requestId } = req.body as { requestId?: string };
    if (!playerId || !requestId) return res.status(400).json({ error: '参数不完整' });
    const result = rejectFriendRequest(playerId, requestId);
    if (!result.ok) return res.status(400).json({ error: result.error });
    res.json({ ok: true });
  });

  app.post('/api/friends/remove', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req, (req.body as { playerId?: string }).playerId);
    const { friendPlayerId } = req.body as { friendPlayerId?: string };
    if (!playerId || !friendPlayerId) {
      return res.status(400).json({ error: '参数不完整' });
    }
    const result = removeFriend(playerId, friendPlayerId);
    if (!result.ok) return res.status(400).json({ error: result.error });
    res.json({ ok: true });
  });

  // Public wall (SEC §2.1)
  app.get('/api/posts/wall', (req, res) => {
    const viewerId = tryResolvePlayerId(req);
    const sort = req.query.sort === 'likes' ? 'likes' : 'time';
    res.json({ posts: getWallPosts(viewerId, sort) });
  });

  app.get('/api/posts/friends/:playerId', requireSelf('playerId'), (req, res) => {
    const sort = req.query.sort === 'likes' ? 'likes' : 'time';
    res.json({ posts: getFriendsPosts(req.params.playerId, sort) });
  });

  app.post('/api/posts', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req, (req.body as { playerId?: string }).playerId);
    const { nickname, fishId, visibility } = req.body as {
      nickname?: string;
      fishId?: string;
      visibility?: 'public' | 'friends';
    };
    if (!playerId || !fishId) return res.status(400).json({ error: '参数不完整' });
    ensurePlayer(playerId, nickname ?? '钓友');
    const result = createPost(playerId, nickname ?? '钓友', fishId, visibility);
    if (!result.ok) return res.status(400).json({ error: result.error });
    res.json({ post: result.post });
  });

  app.post('/api/posts/:postId/like', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req);
    if (!playerId) return res.status(401).json({ error: 'unauthorized' });
    const result = togglePostLike(req.params.postId, playerId);
    if (!result.ok) return res.status(result.status).json({ error: result.error });
    io.emit('post_liked', {
      postId: req.params.postId,
      playerId,
      liked: result.liked,
      likeCount: result.likeCount,
    });
    res.json({ liked: result.liked, likeCount: result.likeCount });
  });

  app.get('/api/posts/:postId/likes', (req, res) => {
    const viewerId = tryResolvePlayerId(req);
    const limit = Number(req.query.limit ?? 50);
    const offset = Number(req.query.offset ?? 0);
    const result = listPostLikes(
      req.params.postId,
      viewerId,
      Number.isFinite(limit) ? limit : 50,
      Number.isFinite(offset) ? offset : 0,
    );
    if (!result.ok) return res.status(result.status).json({ error: result.error });
    res.json({ likes: result.likes });
  });

  app.post('/api/posts/:postId/comments', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req);
    if (!playerId) return res.status(401).json({ error: 'unauthorized' });
    const text = String((req.body as { text?: string }).text ?? '');
    const result = addPostComment(req.params.postId, playerId, text);
    if (!result.ok) return res.status(result.status).json({ error: result.error });
    io.emit('post_commented', { postId: req.params.postId, comment: result.comment });
    res.json({ comment: result.comment, commentCount: result.commentCount });
  });

  app.get('/api/posts/:postId/comments', (req, res) => {
    const viewerId = tryResolvePlayerId(req);
    const limit = Number(req.query.limit ?? 50);
    const result = listPostComments(
      req.params.postId,
      viewerId,
      Number.isFinite(limit) ? limit : 50,
    );
    if (!result.ok) return res.status(result.status).json({ error: result.error });
    res.json({ comments: result.comments, commentCount: result.commentCount });
  });

  app.delete('/api/posts/:postId/comments/:commentId', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req);
    if (!playerId) return res.status(401).json({ error: 'unauthorized' });
    const result = deletePostComment(req.params.postId, req.params.commentId, playerId);
    if (!result.ok) return res.status(result.status).json({ error: result.error });
    io.emit('post_comment_deleted', {
      postId: req.params.postId,
      commentId: req.params.commentId,
      commentCount: result.commentCount,
    });
    res.json({ ok: true, commentCount: result.commentCount });
  });

  app.get('/api/leaderboard/daily-biggest', (req, res) => {
    const date = typeof req.query.date === 'string' ? req.query.date : undefined;
    const limit = Number(req.query.limit ?? 20);
    res.json({
      entries: getDailyBiggestLeaderboard({
        date,
        limit: Number.isFinite(limit) ? limit : 20,
      }),
    });
  });

  app.get('/api/leaderboard/weekly-king', (req, res) => {
    const week = typeof req.query.week === 'string' ? req.query.week : undefined;
    const limit = Number(req.query.limit ?? 20);
    res.json({
      entries: getWeeklyKingLeaderboard({
        week,
        limit: Number.isFinite(limit) ? limit : 20,
      }),
    });
  });

  app.get('/api/leaderboard/pond/:pondId', (req, res) => {
    const week = typeof req.query.week === 'string' ? req.query.week : undefined;
    const limit = Number(req.query.limit ?? 10);
    res.json({
      entries: getPondLeaderboard(req.params.pondId, {
        week,
        limit: Number.isFinite(limit) ? limit : 10,
      }),
    });
  });

  app.get('/api/leaderboard/rare', (req, res) => {
    const week = typeof req.query.week === 'string' ? req.query.week : undefined;
    const limit = Number(req.query.limit ?? 20);
    res.json({
      entries: getRareLeaderboard({
        week,
        limit: Number.isFinite(limit) ? limit : 20,
      }),
    });
  });

  app.get('/api/leaderboard/my-rank', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req);
    if (!playerId) return res.status(401).json({ error: 'unauthorized' });
    const boardType = String(req.query.boardType ?? '') as LeaderboardBoardType;
    const allowed: LeaderboardBoardType[] = [
      'daily_biggest',
      'weekly_king',
      'pond',
      'rare',
    ];
    if (!allowed.includes(boardType)) {
      return res.status(400).json({ error: 'invalid_boardType' });
    }
    const date = typeof req.query.date === 'string' ? req.query.date : undefined;
    const week = typeof req.query.week === 'string' ? req.query.week : undefined;
    const pondId = typeof req.query.pondId === 'string' ? req.query.pondId : undefined;
    const limit = Number(req.query.limit ?? 20);
    res.json(
      getMyLeaderboardRank(playerId, boardType, {
        date,
        week,
        pondId,
        limit: Number.isFinite(limit) ? limit : 20,
      }),
    );
  });

  app.post('/api/inventory/sell', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req, (req.body as { playerId?: string }).playerId);
    const { fishId } = req.body as { fishId?: string };
    if (!playerId || !fishId) return res.status(400).json({ error: '参数不完整' });
    const result = sellFish(playerId, fishId);
    if (!result.ok) return res.status(400).json({ error: result.error });
    const coins = calcFishSellPrice(result.fish);
    const total = addCoins(playerId, coins);
    recordFishingMetric('gold_earn', {
      playerId,
      payload: {
        amount: coins,
        source: 'fish_sell',
        fishId,
        quality: result.fish.quality,
        sizeM: result.fish.sizeM,
      },
    });
    res.json({
      coinsEarned: coins,
      totalCoins: total,
      items: getInventory(playerId),
    });
  });

  // SEC-04: DM list/read require self; mark-read only for authed subject
  app.get('/api/dm/conversations/:playerId', requireSelf('playerId'), (req, res) => {
    res.json({ conversations: listConversations(req.params.playerId) });
  });

  app.get('/api/dm/:playerId/:friendPlayerId', requireSelf('playerId'), (req, res) => {
    const { playerId, friendPlayerId } = req.params;
    markConversationRead(playerId, friendPlayerId);
    res.json({ messages: getConversation(playerId, friendPlayerId) });
  });

  app.post('/api/dm', requireAuth, (req, res) => {
    const fromPlayerId = resolveAuthedPlayerId(req, (req.body as { fromPlayerId?: string }).fromPlayerId);
    const { fromNickname, toPlayerId, text } = req.body as {
      fromNickname?: string;
      toPlayerId?: string;
      text?: string;
    };
    if (!fromPlayerId || !toPlayerId || !text) {
      return res.status(400).json({ error: '参数不完整' });
    }
    const result = sendDirectMessage(fromPlayerId, fromNickname ?? '钓友', toPlayerId, text);
    if (!result.ok) return res.status(400).json({ error: result.error });
    const toSocket = resolveSocketByPlayer(toPlayerId);
    if (toSocket) io.to(toSocket).emit('dm_message', result.message);
    res.json({ message: result.message });
  });
}
