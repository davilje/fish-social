import type {
  DirectMessage,
  DmConversation,
  FriendInfo,
  FriendRequest,
  LeaderboardBoardType,
  LeaderboardEntry,
  LeaderboardMyRank,
  PlayerProfile,
  PostComment,
  PostLikeUser,
  PublicPlayerView,
  ShareVisibility,
  SocialPost,
} from '@fish-social/shared';
import { apiFetch } from './apiClient';

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  return apiFetch<T>(path, init);
}

export const socialApi = {
  register(playerId: string | undefined, nickname: string) {
    return api<{ profile: PlayerProfile; token?: string }>('/api/players/register', {
      method: 'POST',
      body: JSON.stringify({ playerId, nickname }),
    });
  },

  getProfile(playerId: string) {
    return api<{ profile: PlayerProfile }>(`/api/players/${playerId}`);
  },

  getPublicView(playerId: string, viewerPlayerId: string, limit = 20) {
    const params = new URLSearchParams({
      viewer: viewerPlayerId,
      limit: String(limit),
    });
    return api<{ view: PublicPlayerView }>(
      `/api/players/${playerId}/public-view?${params.toString()}`,
    );
  },

  setVisibility(playerId: string, shareVisibility: ShareVisibility) {
    return api<{ profile: PlayerProfile }>(`/api/players/${playerId}/settings`, {
      method: 'PUT',
      body: JSON.stringify({ shareVisibility }),
    });
  },

  updateProfile(
    playerId: string,
    patch: { nickname?: string; bio?: string; avatarUrl?: string | null },
  ) {
    return api<{ profile: PlayerProfile }>(`/api/players/${playerId}/profile`, {
      method: 'PUT',
      body: JSON.stringify(patch),
    });
  },

  setShowcase(playerId: string, slots: (string | null)[]) {
    return api<{ profile: PlayerProfile }>(`/api/players/${playerId}/showcase`, {
      method: 'PUT',
      body: JSON.stringify({ slots }),
    });
  },

  searchPlayers(q: string, exclude: string) {
    return api<{ players: PlayerProfile[] }>(
      `/api/players/search?q=${encodeURIComponent(q)}&exclude=${encodeURIComponent(exclude)}`,
    );
  },

  getFriends(playerId: string) {
    return api<{ friends: FriendInfo[] }>(`/api/friends/${playerId}`);
  },

  getRequests(playerId: string) {
    return api<{ incoming: FriendRequest[]; outgoing: FriendRequest[] }>(
      `/api/friends/${playerId}/requests`,
    );
  },

  sendFriendRequest(fromPlayerId: string, fromNickname: string, toPlayerId: string) {
    return api<{ request: FriendRequest; autoAccepted?: boolean }>('/api/friends/request', {
      method: 'POST',
      body: JSON.stringify({ fromPlayerId, fromNickname, toPlayerId }),
    });
  },

  acceptRequest(playerId: string, requestId: string) {
    return api<{ ok: boolean }>('/api/friends/accept', {
      method: 'POST',
      body: JSON.stringify({ playerId, requestId }),
    });
  },

  rejectRequest(playerId: string, requestId: string) {
    return api<{ ok: boolean }>('/api/friends/reject', {
      method: 'POST',
      body: JSON.stringify({ playerId, requestId }),
    });
  },

  removeFriend(playerId: string, friendPlayerId: string) {
    return api<{ ok: boolean }>('/api/friends/remove', {
      method: 'POST',
      body: JSON.stringify({ playerId, friendPlayerId }),
    });
  },

  getWall(sort: 'time' | 'likes' = 'time') {
    const q = sort === 'likes' ? '?sort=likes' : '';
    return api<{ posts: SocialPost[] }>(`/api/posts/wall${q}`);
  },

  getFriendsFeed(playerId: string) {
    return api<{ posts: SocialPost[] }>(`/api/posts/friends/${playerId}`);
  },

  shareFish(playerId: string, nickname: string, fishId: string, visibility?: ShareVisibility) {
    return api<{ post: SocialPost }>('/api/posts', {
      method: 'POST',
      body: JSON.stringify({ playerId, nickname, fishId, visibility }),
    });
  },

  /** JWT 鉴权；body 不含 playerId */
  toggleLike(postId: string) {
    return api<{ liked: boolean; likeCount: number }>(`/api/posts/${postId}/like`, {
      method: 'POST',
      body: JSON.stringify({}),
    });
  },

  getPostLikes(postId: string, limit = 50, offset = 0) {
    const params = new URLSearchParams({
      limit: String(limit),
      offset: String(offset),
    });
    return api<{ likes: PostLikeUser[]; likeCount: number }>(
      `/api/posts/${postId}/likes?${params.toString()}`,
    );
  },

  getComments(postId: string, limit = 50) {
    const params = new URLSearchParams({ limit: String(limit) });
    return api<{ comments: PostComment[]; commentCount: number }>(
      `/api/posts/${postId}/comments?${params.toString()}`,
    );
  },

  /** JWT 鉴权；body 仅 { text } */
  postComment(postId: string, text: string) {
    return api<{ comment: PostComment; commentCount: number }>(
      `/api/posts/${postId}/comments`,
      {
        method: 'POST',
        body: JSON.stringify({ text }),
      },
    );
  },

  deleteComment(postId: string, commentId: string) {
    return api<{ ok: boolean; commentCount: number }>(
      `/api/posts/${postId}/comments/${commentId}`,
      { method: 'DELETE' },
    );
  },

  getLeaderboardDailyBiggest(opts?: { date?: string; limit?: number }) {
    const params = new URLSearchParams();
    if (opts?.date) params.set('date', opts.date);
    if (opts?.limit != null) params.set('limit', String(opts.limit));
    const q = params.toString();
    return api<{ entries: LeaderboardEntry[]; periodKey: string }>(
      `/api/leaderboard/daily-biggest${q ? `?${q}` : ''}`,
    );
  },

  getLeaderboardWeeklyKing(opts?: { week?: string; limit?: number }) {
    const params = new URLSearchParams();
    if (opts?.week) params.set('week', opts.week);
    if (opts?.limit != null) params.set('limit', String(opts.limit));
    const q = params.toString();
    return api<{ entries: LeaderboardEntry[]; periodKey: string }>(
      `/api/leaderboard/weekly-king${q ? `?${q}` : ''}`,
    );
  },

  getLeaderboardPond(pondId: string, opts?: { week?: string; limit?: number }) {
    const params = new URLSearchParams();
    if (opts?.week) params.set('week', opts.week);
    if (opts?.limit != null) params.set('limit', String(opts.limit));
    const q = params.toString();
    return api<{ entries: LeaderboardEntry[]; periodKey: string }>(
      `/api/leaderboard/pond/${encodeURIComponent(pondId)}${q ? `?${q}` : ''}`,
    );
  },

  getLeaderboardRare(opts?: { week?: string; limit?: number }) {
    const params = new URLSearchParams();
    if (opts?.week) params.set('week', opts.week);
    if (opts?.limit != null) params.set('limit', String(opts.limit));
    const q = params.toString();
    return api<{ entries: LeaderboardEntry[]; periodKey: string }>(
      `/api/leaderboard/rare${q ? `?${q}` : ''}`,
    );
  },

  getMyRank(
    boardType: LeaderboardBoardType,
    opts?: { date?: string; week?: string; pondId?: string },
  ) {
    const params = new URLSearchParams({ boardType });
    if (opts?.date) params.set('date', opts.date);
    if (opts?.week) params.set('week', opts.week);
    if (opts?.pondId) params.set('pondId', opts.pondId);
    return api<LeaderboardMyRank>(`/api/leaderboard/my-rank?${params.toString()}`);
  },

  sellFish(playerId: string, fishId: string) {
    return api<{
      coinsEarned: number;
      totalCoins: number;
      items: import('@fish-social/shared').FishInventoryItem[];
    }>('/api/inventory/sell', {
      method: 'POST',
      body: JSON.stringify({ playerId, fishId }),
    });
  },

  getConversations(playerId: string) {
    return api<{ conversations: DmConversation[] }>(`/api/dm/conversations/${playerId}`);
  },

  getMessages(playerId: string, friendPlayerId: string) {
    return api<{ messages: DirectMessage[] }>(`/api/dm/${playerId}/${friendPlayerId}`);
  },

  sendDm(fromPlayerId: string, fromNickname: string, toPlayerId: string, text: string) {
    return api<{ message: DirectMessage }>('/api/dm', {
      method: 'POST',
      body: JSON.stringify({ fromPlayerId, fromNickname, toPlayerId, text }),
    });
  },
};
