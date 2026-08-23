import type { FishInventoryItem, ShareVisibility } from './types';

export interface PlayerProfile {
  playerId: string;
  nickname: string;
  coins: number;
  shareVisibility: ShareVisibility;
  createdAt: number;
  /** 头像（Base64 data URL 或默认头像路径 /image/profile/...） */
  avatarUrl?: string;
  /** 个人简介 */
  bio?: string;
  /** 收藏品展示槽（8 格），存鱼 id 或 null */
  showcaseFishIds: (string | null)[];
}

export const SHOWCASE_SLOT_COUNT = 8;

export type FriendRequestStatus = 'pending' | 'accepted' | 'rejected';

export interface FriendRequest {
  id: string;
  fromPlayerId: string;
  fromNickname: string;
  toPlayerId: string;
  toNickname: string;
  status: FriendRequestStatus;
  createdAt: number;
}

export interface FriendInfo {
  playerId: string;
  nickname: string;
  avatarUrl?: string;
  since: number;
}

export interface SocialPost {
  id: string;
  playerId: string;
  nickname: string;
  /** 发布者 profile 头像 */
  authorAvatarUrl?: string;
  fish: FishInventoryItem;
  text: string;
  /** 史诗及以上鱼获的钓鱼纪念照 */
  photoUrl?: string;
  visibility: ShareVisibility;
  createdAt: number;
  /** v0.6.0 FEAT-SOC-01 */
  likeCount?: number;
  /** v0.6.0 FEAT-SOC-02 */
  commentCount?: number;
  /** 当前登录用户是否已赞（需鉴权列表才有） */
  likedByMe?: boolean;
}

export interface PostLikeUser {
  playerId: string;
  nickname: string;
  avatarUrl?: string;
  likedAt: number;
}

export interface PostComment {
  id: string;
  postId: string;
  playerId: string;
  nickname: string;
  avatarUrl?: string;
  text: string;
  createdAt: number;
}

export type LeaderboardBoardType =
  | 'daily_biggest'
  | 'weekly_king'
  | 'pond'
  | 'rare';

export interface LeaderboardEntry {
  rank: number;
  playerId: string;
  nickname: string;
  avatarUrl?: string;
  value: number;
  extra?: {
    speciesId?: string;
    sizeM?: number;
    pondId?: string;
    catchCount?: number;
    caughtAt?: number;
  };
}

export interface LeaderboardMyRank {
  rank: number | null;
  value: number;
  entry?: LeaderboardEntry;
}

export const POST_COMMENT_MAX_LENGTH = 200;

export interface DirectMessage {
  id: string;
  fromPlayerId: string;
  fromNickname: string;
  toPlayerId: string;
  text: string;
  createdAt: number;
}

export interface DmConversation {
  friendPlayerId: string;
  friendNickname: string;
  lastMessage: string;
  lastAt: number;
  unread: number;
}

export const SHARE_VISIBILITY_LABELS: Record<ShareVisibility, string> = {
  public: '所有人可见',
  friends: '仅好友可见',
  private: '仅自己可见',
};

/** 他人主页对外展示（不含金币等敏感字段） */
export interface PublicPlayerProfile {
  playerId: string;
  nickname: string;
  avatarUrl?: string;
  bio: string;
  showcaseFishIds: (string | null)[];
}

export interface PublicPlayerView {
  profile: PublicPlayerProfile;
  showcaseFish: (FishInventoryItem | null)[];
  posts: SocialPost[];
}
