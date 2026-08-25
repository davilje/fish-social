export type UserStatus = 'idle' | 'fishing';

/** C6 完整钓鱼阶段 */
export type FishingPhase =
  | 'idle'
  | 'seated'
  | 'groundbaiting'
  | 'baiting'
  | 'casting'
  | 'waiting'
  | 'hooked'
  | 'resolving'
  | 'stopping'
  | 'disconnected';

/** FEAT-GROUND-01：会话内窝效（内存，可不落库） */
export interface PondUserGroundbaitState {
  groundbaitId: string;
  stackCount: number;
  expiresAt: number;
  bitesLeft: number;
  biteBonus: number;
  sizeBonus: number;
}

export interface PondUserPhaseContext {
  isRebait?: boolean;
  outcome?: 'catch' | 'escape';
  disconnectedFromPhase?: FishingPhase;
}

export type ChatMessageType = 'user' | 'announcement';

import type { FishQuality, FishSpeciesId } from './fish';
import type { DirectMessage, FriendRequest, PostComment } from './social';
import type { PondEcologySummary } from './pondEcology';
import type { FishingFloatTextPayload, PlayerGearState, BaitDepletedPayload } from './fishing';

export type { FishQuality, FishSpeciesId };
export type { FishingFloatTextPayload, FishingFloatTextKind } from './fishing';
export type {
  BaitId,
  TackleId,
  BaitConfig,
  TackleConfig,
  PlayerGearState,
  BaitDepletedPayload,
  ShopErrorCode,
} from './fishing';
export type {
  DirectMessage,
  FriendRequest,
  FriendInfo,
  SocialPost,
  DmConversation,
  PlayerProfile,
  PublicPlayerProfile,
  PublicPlayerView,
} from './social';

export type ShareVisibility = 'public' | 'friends' | 'private';

/** 与 fishingStateMachine 一致：计入活跃钓鱼的 phase */
export function isFishingActive(phase?: FishingPhase): boolean {
  return (
    phase === 'baiting' ||
    phase === 'casting' ||
    phase === 'waiting' ||
    phase === 'hooked' ||
    phase === 'resolving' ||
    phase === 'stopping'
  );
}

export interface FishInventoryItem {
  id: string;
  speciesId: FishSpeciesId;
  quality: FishQuality;
  sizeM: number;
  caughtAt: number;
  /** 入库时鱼塘（看板分塘；历史行可空） */
  pondId?: string | null;
}

export interface PendingFishCatch {
  catchId: string;
  /** 鱼塘中的实体鱼 id，领取后从鱼塘移除 */
  pondFishId: string;
  speciesId: FishSpeciesId;
  quality: FishQuality;
  sizeM: number;
  /** A0+ 咬钩后收杆窗口（毫秒），客户端可忽略 */
  hookDurationMs?: number;
  /** 本次钓获是否为该鱼种首次收录图鉴 */
  isCodexNew?: boolean;
}

export interface FishingMiss {
  resultId: string;
  /** 空军 | 脱钩 | 本 tick 无咬钩（A0 服务端触发均为 escaped） */
  reason: 'empty' | 'escaped' | 'nobite';
}

export type FishingPrompt =
  | { kind: 'catch'; data: PendingFishCatch }
  | { kind: 'miss'; data: FishingMiss };

export interface WorldPondRegion {
  id: string;
  name: string;
  /** 世界地图上的区域矩形 */
  x: number;
  y: number;
  width: number;
  height: number;
  color: string;
}

export interface FishingSpot {
  id: string;
  x: number;
  y: number;
}

export interface PondConfig {
  id: string;
  name: string;
  regionId: string;
  spots: FishingSpot[];
}

export interface PondUser {
  id: string;
  /** 账号 playerId，用于社交加好友 */
  playerId?: string;
  nickname: string;
  color: string;
  /** 与 profile 一致的头像（默认头像路径或 data URL） */
  avatarUrl?: string;
  spotId: string | null;
  status: UserStatus;
  /**
   * 兼容字段：本局展示锚点（与 sessionStartedAt 同步）。
   * BUG-19：禁止因分段落账被前移；额度计算以 sessionStartedAt + checkpoint 为准。
   */
  fishingStartedAt: number | null;
  /** BUG-19：本局展示锚点（开钓设定，收杆/离塘清空；checkpoint 不得修改） */
  sessionStartedAt?: number | null;
  /**
   * 兼容只读派生：今日已用展示值 = todayFishingBaseMs + 未落账段。
   * 禁止当作「仅 DB」读写。
   */
  todayFishingMs: number;
  /** BUG-19：当前上海日已写入 daily_fishing 的 ms */
  todayFishingBaseMs?: number;
  /** BUG-19：今日剩余 = max(0, MAX - base - 未落账段) */
  todayRemainingMs?: number;
  /** 当前 todayFishingMs 所属上海日 YYYY-MM-DD（服务端跨日 rollover 用） */
  fishingDayKey?: string;
  /** 本局墙钟时长 now - sessionStartedAt（头顶秒表） */
  sessionFishingMs?: number;
  /** 服务端机器人用户 */
  isBot?: boolean;
  /** C6 钓鱼阶段（比 status 更细） */
  fishingPhase?: FishingPhase;
  phaseEndsAt?: number | null;
  phaseContext?: PondUserPhaseContext;
  equippedBaitId?: string;
  equippedTackleId?: string;
  disconnectedAt?: number | null;
  /** FEAT-GROUND-01：当前钓位窝效 */
  groundbait?: PondUserGroundbaitState | null;
  /** FEAT-RETURN-02：进塘时选定的收费/回鱼模式，本局不可改 */
  returnFeeMode?: 'sell_only' | 'auto_return';
}

export interface ChatMessage {
  id: string;
  pondId: string;
  userId: string;
  nickname: string;
  text: string;
  createdAt: number;
  type?: ChatMessageType;
}

export interface PondSnapshot {
  pond: PondConfig;
  users: PondUser[];
  messages: ChatMessage[];
  inventory?: FishInventoryItem[];
  ecology?: PondEcologySummary;
}

export interface FishCodexEntry {
  speciesId: FishSpeciesId;
  totalCaught: number;
  maxSizeM: number;
  firstCaughtAt: number | null;
  lastCaughtAt: number | null;
}

export interface CodexUnlockPayload {
  speciesId: FishSpeciesId;
  speciesName: string;
  isFirstCatch: boolean;
}

export interface GameConfigEntryView {
  key: string;
  effectiveValue: string;
  defaultValue: string;
  source: 'runtime' | 'default';
  updatedAt?: number;
}

export interface FishingMetricsSummary {
  periodHours: number;
  totalEvents: number;
  escapeStreakPlayers: number;
  abandonRate: number;
  catchCount: number;
  escapeCount: number;
  baitPurchases: Record<string, number>;
  tacklePurchases: Record<string, number>;
  faucetCoinsEstimate: number;
  sinkCoinsEstimate: number;
  alerts: string[];
}

export type LeavePondReason =
  | 'unmount'
  | 'navigation_back'
  | 'navigation_social'
  | 'navigation_profile'
  | 'pond_change'
  | 'auth_redirect'
  | 'user_explicit'
  | 'legacy_unknown';

export interface LeavePondPayload {
  pondId: string;
  reason: LeavePondReason;
}

export interface PlayerFishingTimelineEvent {
  id: string;
  eventType: string;
  pondId: string | null;
  payload: Record<string, unknown>;
  createdAt: number;
}

export interface PlayerFishingTimelineSummary {
  socketConnectCount: number;
  socketConnectErrorCount: number;
  joinPondAttemptCount: number;
  joinPondSuccessCount: number;
  joinPondFailCount: number;
  spotTakeSuccessCount: number;
  spotTakeFailCount: number;
  spotReleaseCount: number;
  pondFullRejectCount: number;
  botEvictedForHumanCount: number;
  disconnectCount: number;
  reconnectCount: number;
  disconnectTimeoutCount: number;
  leavePondCount: number;
  fishingStartCount: number;
  biteHookCount: number;
  biteTickMissCount: number;
  biteTickHitCount: number;
  pendingCatchCreatedCount: number;
  pendingCatchExpiredCount: number;
  baitDepletedCount: number;
  phaseTransitionCount: number;
  phaseTransitionInvalidCount: number;
  lastEventAt: number | null;
}

export interface PlayerFishingTimeline {
  playerId: string;
  hours: number;
  events: PlayerFishingTimelineEvent[];
  summary: PlayerFishingTimelineSummary;
}

export interface JoinPondPayload {
  pondId: string;
  nickname: string;
  playerId: string;
  /** FEAT-RETURN-02：双价塘必选 sell_only | auto_return */
  returnFeeMode?: 'sell_only' | 'auto_return';
}

export interface StartFishingPayload {
  pondId: string;
  /** Optional legacy field; new clients must already be seated. */
  spotId?: string;
}

export interface TakeSpotPayload {
  pondId: string;
  spotId: string;
}

export interface SendChatPayload {
  pondId: string;
  text: string;
}

export interface SessionTimerTickPayload {
  userId: string;
  /** 本局已钓时长；相位变化走 pond_user_updated，不在此重复 */
  sessionFishingMs: number;
}

export interface FishCatchSettledPayload {
  speciesId: string;
  quality: string;
  sizeM: number;
  autoReturned: boolean;
  gold?: number;
  playerXp?: number;
  pondXp?: number;
  newSizeM?: number;
  sizeGainM?: number;
  totalCoins?: number;
  message: string;
}

export interface PondSessionCatchEntry {
  speciesId: string;
  quality: string;
  sizeM: number;
  outcome: 'returned' | 'kept';
  returnGold?: number;
  catchPlayerXp?: number;
  catchPondXp?: number;
  returnPlayerXp?: number;
  returnPondXp?: number;
  caughtAt: number;
}

export interface PondSessionSummaryPayload {
  pondId: string;
  pondName: string;
  returnFeeMode: 'sell_only' | 'auto_return';
  catches: PondSessionCatchEntry[];
  feesPaid: number;
  totalReturnGold: number;
  totalCatchPlayerXp: number;
  totalCatchPondXp: number;
  totalReturnPlayerXp: number;
  totalReturnPondXp: number;
  netProfit: number;
  joinedAt: number;
  leftAt: number;
}

export interface ServerToClientEvents {
  pond_snapshot: (snapshot: PondSnapshot) => void;
  pond_ecology_updated: (summary: PondEcologySummary) => void;
  pond_user_joined: (user: PondUser) => void;
  pond_user_left: (userId: string) => void;
  pond_user_updated: (user: PondUser) => void;
  /** PERF-03: lightweight 1s duration sync (do not use dirty merge — see BUG-07) */
  session_timer_tick: (payload: SessionTimerTickPayload) => void;
  chat_message: (message: ChatMessage) => void;
  fish_bite: (catchData: PendingFishCatch) => void;
  /** FEAT-RETURN-02：回鱼档钓获即时结算（含自动回塘反馈） */
  fish_catch_settled: (payload: FishCatchSettledPayload) => void;
  /** FEAT-RETURN-02：离塘 session 汇总 */
  pond_session_summary: (payload: PondSessionSummaryPayload) => void;
  fish_miss: (miss: FishingMiss) => void;
  fishing_float_text: (payload: FishingFloatTextPayload) => void;
  bait_depleted: (payload: BaitDepletedPayload) => void;
  gear_updated: (gear: PlayerGearState) => void;
  codex_unlocked: (payload: CodexUnlockPayload) => void;
  /** FEAT-ALBUM-01 */
  achievement_unlocked: (payload: {
    achievementId: string;
    name: string;
    desc: string;
  }) => void;
  inventory_updated: (items: FishInventoryItem[]) => void;
  dm_message: (message: DirectMessage) => void;
  friend_request: (request: FriendRequest) => void;
  /** v0.6.0 动态点赞 */
  post_liked: (payload: {
    postId: string;
    playerId: string;
    liked: boolean;
    likeCount: number;
  }) => void;
  /** v0.6.0 动态评论 */
  post_commented: (payload: { postId: string; comment: PostComment }) => void;
  post_comment_deleted: (payload: {
    postId: string;
    commentId: string;
    commentCount: number;
  }) => void;
  /** FEAT-RISK-01：禁止塘巡警事件（仅发给当事玩家） */
  police_raid: (payload: PoliceRaidPayload) => void;
  error: (message: string) => void;
}

export type PoliceRaidStatus = 'warning' | 'escaped' | 'fined';

export interface PoliceRaidPayload {
  status: PoliceRaidStatus;
  raidId: string;
  pondId: string;
  text: string;
  deadlineMs: number;
  coinsAfter?: number;
  charged?: number;
  message: string;
}

export interface ClientToServerEvents {
  join_pond: (
    payload: JoinPondPayload,
    ack?: (result: {
      ok: boolean;
      userId?: string;
      error?: string;
      /** BUG-19：进塘即可展示的上海日已用（未选钓点也要正确） */
      todayFishingBaseMs?: number;
      todayRemainingMs?: number;
      quotaDateKey?: string;
      /** FEAT-PROG-01 入场费提示 */
      feePer2h?: number;
      /** FEAT-RETURN-02 */
      feePer2hSellOnly?: number;
      feePer2hAutoReturn?: number;
      allowsAutoReturn?: boolean;
      returnFeeMode?: 'sell_only' | 'auto_return';
      maxFeeChargesPerDay?: number;
      todayFeeCharges?: number;
      feeProgressMs?: number;
      needsFeeToContinue?: boolean;
      coins?: number;
      pondCategory?: string | null;
      onboardingCompleted?: boolean;
      playerLevel?: number;
    }) => void,
  ) => void;
  leave_pond: (
    payload: string | LeavePondPayload,
    ack?: (result: {
      ok: boolean;
      error?: string;
      sessionSummary?: PondSessionSummaryPayload;
    }) => void,
  ) => void;
  leave_spot: (
    payload: { pondId: string },
    ack?: (result: { ok: boolean; error?: string }) => void,
  ) => void;
  start_fishing: (payload: StartFishingPayload, ack?: (result: { ok: boolean; error?: string }) => void) => void;
  groundbait_start: (
    payload: { pondId: string; groundbaitId: string },
    ack?: (result: { ok: boolean; error?: string; code?: string }) => void,
  ) => void;
  take_spot: (payload: TakeSpotPayload, ack?: (result: { ok: boolean; error?: string }) => void) => void;
  stop_fishing: (
    pondId: string,
    ack?: (result: {
      ok: boolean;
      error?: string;
      todayFishingMs?: number;
      todayFishingBaseMs?: number;
      todayRemainingMs?: number;
      quotaDateKey?: string;
    }) => void,
  ) => void;
  send_chat: (payload: SendChatPayload, ack?: (result: { ok: boolean; error?: string }) => void) => void;
  accept_catch: (
    catchId: string,
    ack?: (result: {
      ok: boolean;
      error?: string;
      item?: FishInventoryItem;
      autoReturned?: boolean;
      gold?: number;
      playerXp?: number;
      pondXp?: number;
      newSizeM?: number;
      sizeGainM?: number;
      totalCoins?: number;
    }) => void,
  ) => void;
  register_player: (playerId: string) => void;
}
