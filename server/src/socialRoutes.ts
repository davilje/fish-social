import type { Express } from 'express';
import { createHmac, randomUUID, timingSafeEqual } from 'crypto';
import type { Server } from 'socket.io';
import { calcFishSellPrice, PONDS } from '@fish-social/shared';
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
import { getInventory, sellFish, getFishById, addFishToInventory } from './inventory.js';
import { returnFishToPond } from './returnFish.js';
import { recordFishingMetric } from './fishingMetrics.js';
import {
  completeOnboarding,
  ensurePlayerProgress,
  getProgressPublicView,
  grantCatchProgress,
  resetOnboarding,
} from './playerProgress.js';
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
import { getJwtSecret } from './auth.js';
import { db } from './db.js';
import { logStructuredEvent } from './fishingObservability.js';

const SOCIAL_LOBBY_GAME_VERSION = process.env.GAME_VERSION ?? '1.0-steam-desktop';
const SOCIAL_LOBBY_PROTOCOL_VERSION = '1.0.0-draft';
const SOCIAL_LOBBY_TTL_MS = 30 * 60 * 1000;
const SOCIAL_LOBBY_INVITE_TTL_MS = 10 * 60 * 1000;
const socialLobbies = new Map<string, {
  lobbyId: string;
  ownerPlayerId: string;
  pondId: string;
  gameVersion: string;
  protocolVersion: string;
  createdAt: number;
  ownerSteamId64: string;
}>();

// This registry is only the temporary Steam invitation/authorization layer.
// It must never own pond lifecycle or pond ecology state; those remain in the
// persistent pond/session modules and continue offline when no player is in a pond.

interface SteamBindingRow {
  player_id: string;
  steam_id64: string;
  app_id: string;
  revoked_at: number | null;
}

function getSteamBinding(playerId: string): SteamBindingRow | null {
  const row = db.prepare(
    'SELECT player_id, steam_id64, app_id, revoked_at FROM steam_accounts WHERE player_id = ?',
  ).get(playerId) as SteamBindingRow | undefined;
  return row ?? null;
}

function maskPlayerId(playerId: string | null): string | null {
  if (!playerId) return null;
  if (playerId.length <= 8) return '***';
  return `${playerId.slice(0, 4)}…${playerId.slice(-4)}`;
}

function logLobbyCreateRejected(
  playerId: string | null,
  binding: SteamBindingRow | null,
  body: { lobbyId?: unknown; pondId?: unknown },
  code: string,
): void {
  logStructuredEvent('social_lobby', 'social_lobby_create_rejected', {
    eventType: 'social_lobby_create_rejected',
    playerId: maskPlayerId(playerId),
    steamBindingFound: Boolean(binding),
    steamIdFound: Boolean(binding?.steam_id64 && /^\d{17}$/.test(binding.steam_id64) && !binding.revoked_at),
    appId: binding?.app_id ?? process.env.STEAM_APP_ID ?? null,
    lobbyId: typeof body.lobbyId === 'string' ? body.lobbyId : null,
    pondId: typeof body.pondId === 'string' ? body.pondId : null,
    code,
  });
}

function encodeInvite(payload: Record<string, unknown>): string {
  const body = Buffer.from(JSON.stringify(payload)).toString('base64url');
  const signature = createHmac('sha256', getJwtSecret()).update(body).digest('base64url');
  return `${body}.${signature}`;
}

function decodeInvite(token: unknown): Record<string, unknown> | null {
  if (typeof token !== 'string') return null;
  const parts = token.split('.');
  if (parts.length !== 2) return null;
  const expected = createHmac('sha256', getJwtSecret()).update(parts[0]).digest('base64url');
  const actual = Buffer.from(parts[1]);
  const expectedBytes = Buffer.from(expected);
  if (actual.length !== expectedBytes.length || !timingSafeEqual(actual, expectedBytes)) return null;
  try {
    return JSON.parse(Buffer.from(parts[0], 'base64url').toString('utf8')) as Record<string, unknown>;
  } catch {
    return null;
  }
}

function cleanupSocialLobbies(): void {
  const cutoff = Date.now() - SOCIAL_LOBBY_TTL_MS;
  for (const [lobbyId, lobby] of socialLobbies) {
    if (lobby.createdAt < cutoff) socialLobbies.delete(lobbyId);
  }
}

function validateLobbyVersions(body: { gameVersion?: unknown; protocolVersion?: unknown }): string | null {
  if (body.gameVersion !== SOCIAL_LOBBY_GAME_VERSION) return 'LOBBY_GAME_VERSION_MISMATCH';
  if (body.protocolVersion !== SOCIAL_LOBBY_PROTOCOL_VERSION) return 'LOBBY_PROTOCOL_VERSION_MISMATCH';
  return null;
}

function mintPlayerId(): string {
  return `p_${randomUUID().replace(/-/g, '').slice(0, 16)}`;
}

export function registerSocialRoutes(
  app: Express,
  io: Server<ClientToServerEvents, ServerToClientEvents>,
): void {
  app.post('/api/social/lobby/create', requireAuth, (req, res) => {
    cleanupSocialLobbies();
    const playerId = resolveAuthedPlayerId(req);
    const body = req.body as {
      lobbyId?: unknown;
      pondId?: unknown;
      gameVersion?: unknown;
      protocolVersion?: unknown;
    };
    const binding = playerId ? getSteamBinding(playerId) : null;
    const steamId64 = binding?.revoked_at ? null : binding?.steam_id64 ?? null;
    const configuredSteamAppId = process.env.STEAM_APP_ID?.trim();
    const bindingAppMatches = !configuredSteamAppId || binding?.app_id === configuredSteamAppId;
    if (
      !playerId ||
      !getPlayer(playerId) ||
      !binding ||
      !steamId64 ||
      !/^\d{17}$/.test(steamId64) ||
      !bindingAppMatches
    ) {
      logLobbyCreateRejected(playerId, binding, body, 'LOBBY_STEAM_BINDING_REQUIRED');
      res.status(403).json({ ok: false, code: 'LOBBY_STEAM_BINDING_REQUIRED', error: '需要有效的 Steam 账号绑定' });
      return;
    }
    if (typeof body.lobbyId !== 'string' || !/^\d{5,25}$/.test(body.lobbyId)) {
      logLobbyCreateRejected(playerId, binding, body, 'LOBBY_ID_INVALID');
      res.status(400).json({ ok: false, code: 'LOBBY_ID_INVALID', error: 'Lobby ID 无效' });
      return;
    }
    const pondId = typeof body.pondId === 'string' ? body.pondId : '';
    if (!PONDS.some((pond) => pond.id === pondId)) {
      logLobbyCreateRejected(playerId, binding, body, 'POND_NOT_FOUND');
      res.status(404).json({ ok: false, code: 'POND_NOT_FOUND', error: '鱼塘不存在' });
      return;
    }
    const versionError = validateLobbyVersions(body);
    if (versionError) {
      logLobbyCreateRejected(playerId, binding, body, versionError);
      res.status(409).json({ ok: false, code: versionError, error: 'Lobby 版本不兼容' });
      return;
    }
    const lobby = {
      lobbyId: body.lobbyId,
      ownerPlayerId: playerId,
      pondId,
      gameVersion: SOCIAL_LOBBY_GAME_VERSION,
      protocolVersion: SOCIAL_LOBBY_PROTOCOL_VERSION,
      createdAt: Date.now(),
      ownerSteamId64: steamId64,
    };
    socialLobbies.set(lobby.lobbyId, lobby);
    res.status(201).json({ ok: true, lobby });
  });

  app.post('/api/social/lobby/invite', requireAuth, (req, res) => {
    cleanupSocialLobbies();
    const playerId = resolveAuthedPlayerId(req);
    const body = req.body as { lobbyId?: unknown; friendSteamId64?: unknown };
    const lobbyId = typeof body.lobbyId === 'string' ? body.lobbyId : '';
    const lobby = socialLobbies.get(lobbyId);
    const ownerBinding = playerId ? getSteamBinding(playerId) : null;
    const ownerSteamId64 = ownerBinding?.revoked_at ? null : ownerBinding?.steam_id64 ?? null;
    if (!lobby) {
      res.status(404).json({ ok: false, code: 'LOBBY_CACHE_MISSING', error: 'Lobby 已失效，请重新创建' });
      return;
    }
    if (!ownerSteamId64 || lobby.ownerPlayerId !== playerId || lobby.ownerSteamId64 !== ownerSteamId64) {
      res.status(403).json({ ok: false, code: 'LOBBY_OWNER_REQUIRED', error: '只有 Lobby 创建者可以邀请' });
      return;
    }
    if (typeof body.friendSteamId64 !== 'string' || !/^\d{17}$/.test(body.friendSteamId64)) {
      res.status(400).json({ ok: false, code: 'LOBBY_TARGET_STEAM_INVALID', error: '被邀请 Steam 账号无效' });
      return;
    }
    const token = encodeInvite({
      steamId64: body.friendSteamId64,
      lobbyId,
      pondId: lobby.pondId,
      gameVersion: lobby.gameVersion,
      protocolVersion: lobby.protocolVersion,
      expiresAt: Date.now() + SOCIAL_LOBBY_INVITE_TTL_MS,
      nonce: randomUUID(),
    });
    res.json({ ok: true, inviteToken: token, expiresAt: Date.now() + SOCIAL_LOBBY_INVITE_TTL_MS });
  });

  app.post('/api/social/lobby/join', requireAuth, (req, res) => {
    cleanupSocialLobbies();
    const playerId = resolveAuthedPlayerId(req);
    const body = req.body as {
      lobbyId?: unknown;
      gameVersion?: unknown;
      protocolVersion?: unknown;
      inviteToken?: unknown;
    };
    const binding = playerId ? getSteamBinding(playerId) : null;
    const steamId64 = binding?.revoked_at ? null : binding?.steam_id64 ?? null;
    if (!playerId || !binding || !steamId64) {
      res.status(403).json({ ok: false, code: 'LOBBY_STEAM_BINDING_REQUIRED', error: '需要有效的 Steam 账号绑定' });
      return;
    }
    const lobbyId = typeof body.lobbyId === 'string' ? body.lobbyId : '';
    const lobby = socialLobbies.get(lobbyId);
    if (!lobby) {
      res.status(404).json({ ok: false, code: 'LOBBY_CACHE_MISSING', error: 'Lobby 已失效，请重新创建' });
      return;
    }
    const versionError = validateLobbyVersions(body);
    if (versionError) {
      res.status(409).json({ ok: false, code: versionError, error: 'Lobby 版本不兼容' });
      return;
    }
    const invite = decodeInvite(body.inviteToken);
    if (
      !invite ||
      invite.steamId64 !== steamId64 ||
      invite.lobbyId !== lobbyId ||
      invite.pondId !== lobby.pondId ||
      invite.gameVersion !== lobby.gameVersion ||
      invite.protocolVersion !== lobby.protocolVersion ||
      typeof invite.expiresAt !== 'number' ||
      invite.expiresAt < Date.now()
    ) {
      res.status(403).json({ ok: false, code: 'LOBBY_INVITE_INVALID', error: 'Lobby 邀请凭证无效或已过期' });
      return;
    }
    res.json({ ok: true, lobby });
  });

  app.post('/api/social/lobby/close', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req);
    const lobbyId = String((req.body as { lobbyId?: unknown }).lobbyId ?? '');
    const lobby = socialLobbies.get(lobbyId);
    if (!lobby) {
      res.status(404).json({ ok: false, code: 'LOBBY_CACHE_MISSING', error: 'Lobby 已失效，请重新创建' });
      return;
    }
    if (lobby.ownerPlayerId !== playerId) {
      res.status(403).json({ ok: false, code: 'LOBBY_OWNER_REQUIRED', error: '只有 Lobby 创建者可以关闭' });
      return;
    }
    socialLobbies.delete(lobbyId);
    // Closing a Steam Lobby only revokes future Lobby joins. It does not
    // disconnect players, delete the pond, or stop offline ecology simulation.
    res.json({ ok: true });
  });

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

  app.get('/api/players/:playerId', requireSelf('playerId'), (req, res) => {
    const profile = getPlayer(req.params.playerId);
    if (!profile) return res.status(404).json({ error: '玩家不存在' });
    res.json({ profile });
  });

  /** FEAT-PROG-01：钓鱼等级 / 引导 / 扣费进度 */
  app.get('/api/progress/me', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req);
    if (!playerId) return res.status(401).json({ error: 'unauthorized' });
    res.json({ progress: getProgressPublicView(playerId) });
  });

  app.post('/api/progress/complete-onboarding', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req);
    if (!playerId) return res.status(401).json({ error: 'unauthorized' });
    const before = ensurePlayerProgress(playerId);
    const progress = completeOnboarding(playerId);
    if (!before.onboardingCompleted) {
      addFishToInventory(
        playerId,
        {
          speciesId: 'crucian',
          quality: 'gray',
          sizeM: 0.18,
          caughtAt: Date.now(),
          pondId: 'pond-novice',
        },
        { pondId: 'pond-novice' },
      );
      grantCatchProgress(playerId, 'pond-novice', 'crucian', 'gray');
    }
    res.json({ progress: getProgressPublicView(playerId), completed: progress.onboardingCompleted });
  });

  app.post('/api/progress/reset-onboarding', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req);
    if (!playerId) return res.status(401).json({ error: 'unauthorized' });
    const progress = resetOnboarding(playerId);
    res.json({ progress: getProgressPublicView(playerId), completed: progress.onboardingCompleted });
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
    const limit = Number(req.query.limit ?? 50);
    const offset = Number(req.query.offset ?? 0);
    res.json({
      posts: getWallPosts(
        viewerId,
        sort,
        Number.isFinite(limit) ? limit : 50,
        Number.isFinite(offset) ? offset : 0,
      ),
    });
  });

  app.get('/api/posts/friends/:playerId', requireSelf('playerId'), (req, res) => {
    const sort = req.query.sort === 'likes' ? 'likes' : 'time';
    const limit = Number(req.query.limit ?? 50);
    const offset = Number(req.query.offset ?? 0);
    res.json({
      posts: getFriendsPosts(
        req.params.playerId,
        sort,
        Number.isFinite(limit) ? limit : 50,
        Number.isFinite(offset) ? offset : 0,
      ),
    });
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

  app.post('/api/inventory/return-to-pond', requireAuth, (req, res) => {
    const playerId = resolveAuthedPlayerId(req, (req.body as { playerId?: string }).playerId);
    const body = req.body as { inventoryItemId?: string; fishId?: string };
    const inventoryItemId = body.inventoryItemId || body.fishId;
    if (!playerId || !inventoryItemId) {
      return res.status(400).json({ error: '参数不完整', code: 'ITEM_NOT_FOUND' });
    }
    const result = returnFishToPond(playerId, inventoryItemId);
    if (!result.ok) {
      return res.status(400).json({ error: result.error, code: result.code });
    }
    res.json({
      gold: result.gold,
      playerXp: result.playerXp,
      pondXp: result.pondXp,
      newSizeM: result.newSizeM,
      sizeGainM: result.sizeGainM,
      totalCoins: result.totalCoins,
      items: result.items,
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
