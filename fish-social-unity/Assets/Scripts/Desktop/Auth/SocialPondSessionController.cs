using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishSocial.Desktop.Auth
{
    /// <summary>
    /// Owns the authenticated pond session so UI code does not send raw Socket.IO
    /// events or decide authoritative player state.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class SocialPondSessionController : MonoBehaviour
    {
        const string DefaultPondId = "pond-calm";

        ISocialSocketClient _socket;
        SteamAuthController _auth;
        readonly Dictionary<string, PondUserDto> _users = new Dictionary<string, PondUserDto>();
        readonly List<ChatMessageDto> _messages = new List<ChatMessageDto>();
        PendingFishCatchDto _latestCatch;
        OverlayHookPromptState _hookPrompt;
        string _nickname;
        bool _joinRequested;
        bool _transitionBusy;
        bool _switchingPond;
        string _pendingReturnFeeMode = "sell_only";

        public SocialSocketState State => _socket?.State ?? SocialSocketState.Disconnected;
        public PondSnapshotDto LatestSnapshot { get; private set; }
        public PondUserDto CurrentUser { get; private set; }
        public FishInventoryItemDto[] CurrentInventory { get; private set; } =
            new FishInventoryItemDto[0];
        public string CurrentPondId { get; private set; } = DefaultPondId;
        public string CurrentPhase => CurrentUser?.fishingPhase ?? "idle";
        public bool IsFishingAsleep => CurrentUser != null && CurrentUser.fishingAsleep;
        public bool HasPendingCatch => !IsFishingAsleep && _latestCatch != null;
        public string LastFishingOutcome { get; private set; } = string.Empty;
        public static readonly string DailyQuotaExhaustedMessage = DesktopLocalization.Text("ui.fishing.session.error.daily_time_exhausted", "今日钓鱼时长已用完");
        public bool CanStartFishing =>
            CurrentUser == null ||
            CurrentPhase == "idle" ||
            CurrentPhase == "seated";
        /// <summary>
        /// Server rejects start_fishing once todayFishingMs hits the 8h cap.
        /// todayRemainingMs defaults to 0 before the first snapshot, so that
        /// alone must not lock a brand-new seat.
        /// </summary>
        public bool IsDailyQuotaExhausted =>
            CurrentUser != null &&
            CurrentUser.todayRemainingMs <= 0 &&
            CurrentUser.todayFishingMs > 0;
        public bool CanGroundbait =>
            HasSpot && CurrentPhase == "seated" && !IsGroundbaitStackFull;
        public bool IsGroundbaiting => CurrentPhase == "groundbaiting";
        public bool IsGroundbaitStackFull
        {
            get
            {
                var gb = CurrentUser != null ? CurrentUser.groundbait : null;
                return gb != null && gb.stackCount >= 50;
            }
        }
        public bool CanStopFishing =>
            CurrentPhase == "baiting" || CurrentPhase == "casting" ||
            CurrentPhase == "waiting" || CurrentPhase == "hooked" ||
            CurrentPhase == "resolving";
        public bool IsTransitioning => _transitionBusy;
        /// <summary>
        /// When set (e.g. pond-novice during onboarding), ConnectAndJoin/SwitchPond
        /// to any other pond is rejected.
        /// </summary>
        public string AllowedPondIdOnly { get; set; }
        public bool HasSpot => CurrentUser != null && !string.IsNullOrEmpty(CurrentUser.spotId);
        public string FirstSpotId =>
            LatestSnapshot?.pond?.spots != null && LatestSnapshot.pond.spots.Length > 0
                ? LatestSnapshot.pond.spots[0].id
                : "calm-spot-1";
        public event Action<SocialSocketState, string> StateChanged;
        public event Action<PondSnapshotDto> SnapshotChanged;
        public event Action<PondUserDto> UserUpdated;
        public event Action UsersChanged;
        public event Action<PendingFishCatchDto> FishBiteReceived;
        public event Action<FishingMissDto> FishMissReceived;
        public event Action<FishCatchSettledDto> FishCatchSettled;
        public event Action<PondSessionSummaryDto> PondSessionSummaryReceived;
        public event Action CatchAccepted;
        public event Action<FishInventoryItemDto[]> InventoryUpdated;
        public event Action<ChatMessageDto> ChatMessageReceived;
        public event Action<CodexUnlockDto> CodexUnlocked;
        public event Action<AchievementUnlockDto> AchievementUnlocked;
        public event Action<FriendRequestDto> FriendRequestReceived;
        public event Action<DirectMessageDto> DmMessageReceived;
        public event Action<PostLikedDto> PostLikedReceived;
        public event Action<PostCommentedDto> PostCommentedReceived;
        public event Action<PostCommentDeletedDto> PostCommentDeletedReceived;
        public event Action<PoliceRaidDto> PoliceRaidReceived;
        public event Action<string> ErrorReceived;
        public PoliceRaidDto ActivePoliceRaid { get; private set; }
        public PondSessionSummaryDto LastSessionSummary { get; private set; }
        public string Nickname => string.IsNullOrWhiteSpace(_nickname) ? "Steam玩家" : _nickname;
        public ChatMessageDto[] PondMessages => _messages.ToArray();
        public const int OverlayRecentChatLimit = 20;

        public ChatMessageDto[] OverlayRecentChats()
        {
            var count = Math.Min(OverlayRecentChatLimit, _messages.Count);
            if (count <= 0)
                return new ChatMessageDto[0];
            var start = _messages.Count - count;
            var slice = new ChatMessageDto[count];
            _messages.CopyTo(start, slice, 0, count);
            return slice;
        }

        public void Configure(SteamAuthController auth)
        {
            if (_socket != null)
                _socket.Disconnect();
            _auth = auth;
            _socket = auth?.CreateSocialSocketClient();
            if (_socket == null)
                return;

            _socket.StateChanged += OnStateChanged;
            _socket.PondSnapshotReceived += OnSnapshot;
            _socket.PondUserJoined += OnUserJoined;
            _socket.PondUserLeft += OnUserLeft;
            _socket.PondUserUpdated += OnUserUpdated;
            _socket.SessionTimerTick += OnSessionTimerTick;
            _socket.FishBiteReceived += OnFishBite;
            _socket.FishMissReceived += OnFishMiss;
            _socket.FishCatchSettled += OnFishCatchSettled;
            _socket.PondSessionSummaryReceived += OnPondSessionSummary;
            _socket.InventoryUpdated += OnInventoryUpdated;
            _socket.ChatMessageReceived += OnChatMessage;
            _socket.CodexUnlocked += OnCodexUnlocked;
            _socket.AchievementUnlocked += OnAchievementUnlocked;
            _socket.FriendRequestReceived += OnFriendRequest;
            _socket.DmMessageReceived += OnDirectMessage;
            _socket.PostLikedReceived += OnPostLiked;
            _socket.PostCommentedReceived += OnPostCommented;
            _socket.PostCommentDeletedReceived += OnPostCommentDeleted;
            _socket.PoliceRaidReceived += OnPoliceRaid;
            _socket.ErrorReceived += OnError;
        }

        void Update()
        {
            _socket?.Pump();
        }

        public void ApplyGameNickname(string nickname)
        {
            if (!string.IsNullOrWhiteSpace(nickname))
                _nickname = nickname.Trim();
        }

        public void ApplyGameAvatarUrl(string avatarUrl)
        {
            if (CurrentUser == null)
                return;
            CurrentUser.avatarUrl = avatarUrl ?? string.Empty;
        }

        public void SetPendingReturnFeeMode(string mode)
        {
            _pendingReturnFeeMode = string.Equals(mode, "auto_return", StringComparison.Ordinal)
                ? "auto_return"
                : "sell_only";
        }

        public void ConnectAndJoin(string pondId = DefaultPondId, string nickname = null, string returnFeeMode = null)
        {
            if (!string.IsNullOrEmpty(returnFeeMode))
                SetPendingReturnFeeMode(returnFeeMode);
            Debug.Log("[Pond] ConnectAndJoin requested. pondId=" + pondId);
            if (_transitionBusy)
            {
                ErrorReceived?.Invoke(DesktopLocalization.Text("ui.fishing.session.error.operation_pending", "鱼塘操作正在进行，请稍候。"));
                return;
            }
            if (_auth == null || !_auth.IsAuthenticated)
            {
                Debug.LogWarning("[Pond] ConnectAndJoin rejected: Steam session is not authenticated.");
                ErrorReceived?.Invoke(DesktopLocalization.Text("ui.auth.pond_session.error.steam_login_required", "请先完成 Steam 登录。"));
                return;
            }

            pondId = string.IsNullOrWhiteSpace(pondId) ? DefaultPondId : pondId;
            string reject;
            if (TryRejectRestrictedPond(pondId, out reject))
            {
                ErrorReceived?.Invoke(reject);
                return;
            }
            if (_socket != null && _socket.IsConnected &&
                !string.Equals(CurrentPondId, pondId, StringComparison.Ordinal))
            {
                SwitchPond(pondId, null, _pendingReturnFeeMode);
                return;
            }
            CurrentPondId = pondId;
            if (!string.IsNullOrWhiteSpace(nickname))
                ApplyGameNickname(nickname);
            if (string.IsNullOrWhiteSpace(_nickname))
                _nickname = "Steam玩家";
            _joinRequested = true;
            var token = _auth.GetAccessTokenForSession();
            Debug.Log("[Pond] Starting Socket.IO connection.");
            _socket.Connect(token, (ok, message) =>
            {
                Debug.Log("[Pond] Socket.IO connect completed. ok=" + ok + " message=" + message);
                if (!ok) ErrorReceived?.Invoke(message);
            });
        }

        public void TakeSpot(string spotId, Action<bool, string> onCompleted = null)
        {
            _socket?.TakeSpot(new TakeSpotPayload
            {
                pondId = CurrentPondId,
                spotId = spotId,
            }, onCompleted);
        }

        public void TakeFirstSpot(Action<bool, string> onCompleted = null)
        {
            TakeSpot(FirstSpotId, onCompleted);
        }

        public void StartFishing(string spotId = null, Action<bool, string> onCompleted = null)
        {
            if (IsDailyQuotaExhausted)
            {
                onCompleted?.Invoke(false, DailyQuotaExhaustedMessage);
                return;
            }
            if (CurrentUser != null && CurrentPhase != "seated" && CurrentPhase != "idle")
            {
                onCompleted?.Invoke(false, DesktopLocalization.Format("ui.fishing.session.error.finish_current_phase", (object)(CurrentPhase)));
                return;
            }
            _socket?.StartFishing(new StartFishingPayload
            {
                pondId = CurrentPondId,
                spotId = spotId,
            }, onCompleted);
        }

        public void StartGroundbait(string groundbaitId, Action<bool, string> onCompleted = null)
        {
            if (!CanGroundbait)
            {
                if (IsGroundbaitStackFull)
                    onCompleted?.Invoke(false, DesktopLocalization.Text("ui.fishing.session.error.groundbait_limit", "已达打窝上限"));
                else
                    onCompleted?.Invoke(false, DesktopLocalization.Text("ui.fishing.session.error.seat_required", "请先坐席后再打窝"));
                return;
            }
            if (string.IsNullOrEmpty(groundbaitId))
            {
                onCompleted?.Invoke(false, DesktopLocalization.Text("ui.fishing.session.error.groundbait_required", "请选择窝料"));
                return;
            }
            _socket?.StartGroundbait(new GroundbaitStartPayload
            {
                pondId = CurrentPondId,
                groundbaitId = groundbaitId,
            }, onCompleted);
        }

        public void StartFishingAtFirstSpot(Action<bool, string> onCompleted = null)
        {
            StartFishing(FirstSpotId, onCompleted);
        }

        public void FishingAttention(Action<bool, string> onCompleted = null)
        {
            if (_socket == null || !_socket.IsConnected || CurrentUser == null)
            {
                onCompleted?.Invoke(false, DesktopLocalization.Text("ui.fishing.session.error.attention_disconnected", "鱼塘会话尚未连接。"));
                return;
            }
            _socket.FishingAttention(onCompleted);
        }

        public void StopFishing(Action<bool, string> onCompleted = null)
        {
            if (!CanStopFishing)
            {
                onCompleted?.Invoke(false, DesktopLocalization.Text("ui.fishing.session.error.not_fishing", "当前没有正在进行的钓鱼。"));
                return;
            }
            _socket?.StopFishing(CurrentPondId, onCompleted);
        }

        public void AcceptLatestCatch(Action<bool, string> onCompleted = null)
        {
            if (_latestCatch == null)
            {
                if (CurrentPhase == "hooked")
                    onCompleted?.Invoke(false, DesktopLocalization.Text("ui.fishing.session.status.catch_settling", "鱼已咬钩，正在结算，请等待鱼获事件。"));
                else
                    onCompleted?.Invoke(false, DesktopLocalization.Text("ui.catch.pond_session.error.no_pending_catch", "当前没有待领取的鱼获。"));
                return;
            }
            _socket?.AcceptCatch(_latestCatch.catchId, (ok, message) =>
            {
                if (ok)
                {
                    _latestCatch = null;
                    CatchAccepted?.Invoke();
                }
                onCompleted?.Invoke(ok, message);
            });
        }

        public void DismissOverlayHookPrompt()
        {
            _hookPrompt = null;
        }

        public OverlayHookPromptState ConsumeExpiredOverlayHookPrompt()
        {
            if (_hookPrompt == null)
                return null;
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (!_hookPrompt.IsVisible(nowMs))
            {
                _hookPrompt = null;
                return null;
            }
            return _hookPrompt;
        }

        public void LeaveSpot(Action<bool, string> onCompleted = null)
        {
            if (!HasSpot)
            {
                onCompleted?.Invoke(true, DesktopLocalization.Text("ui.fishing.session.error.seat_not_occupied", "当前没有占用钓位。"));
                return;
            }
            _socket?.LeaveSpot(CurrentPondId, onCompleted);
        }

        public void ExitPond(Action<bool, string> onCompleted = null)
        {
            if (_transitionBusy)
            {
                onCompleted?.Invoke(false, DesktopLocalization.Text("ui.fishing.session.error.operation_pending", "鱼塘操作正在进行，请稍候。"));
                return;
            }
            _transitionBusy = true;
            _switchingPond = false;
            ExitPondStopFishing(onCompleted);
        }

        void ExitPondStopFishing(Action<bool, string> onCompleted)
        {
            if (!CanStopFishing)
            {
                ExitPondAcceptCatch(onCompleted);
                return;
            }
            StopFishing((ok, message) =>
            {
                if (!ok)
                {
                    FinishTransition(false, message, onCompleted);
                    return;
                }
                ExitPondAcceptCatch(onCompleted);
            });
        }

        void ExitPondAcceptCatch(Action<bool, string> onCompleted)
        {
            if (!HasPendingCatch)
            {
                ExitPondLeaveSpot(onCompleted);
                return;
            }
            AcceptLatestCatch((ok, message) =>
            {
                // 正式路径已自动结算；若仍有 pending（Debug），领取失败且已入包则继续离塘。
                if (!ok && !IsCatchAlreadyClaimed(message))
                {
                    FinishTransition(false, message, onCompleted);
                    return;
                }
                ExitPondLeaveSpot(onCompleted);
            });
        }

        void ExitPondLeaveSpot(Action<bool, string> onCompleted)
        {
            LeaveSpot((ok, message) =>
            {
                if (!ok)
                {
                    FinishTransition(false, message, onCompleted);
                    return;
                }
                if (_socket == null || !_socket.IsConnected)
                {
                    ClearLocalPondState();
                    FinishTransition(true, DesktopLocalization.Text("ui.fishing.session.status.pond_exited", "已退出鱼塘。"), onCompleted);
                    return;
                }
                _socket.LeavePond(CurrentPondId, "user_explicit", (left, leaveMessage) =>
                {
                    if (!left)
                    {
                        FinishTransition(false, leaveMessage, onCompleted);
                        return;
                    }
                    _joinRequested = _switchingPond;
                    if (!_switchingPond)
                        _socket.Disconnect();
                    ClearLocalPondState();
                    FinishTransition(true, DesktopLocalization.Text("ui.fishing.session.status.pond_exited", "已退出鱼塘。"), onCompleted);
                });
            });
        }

        public void SwitchPond(string pondId, Action<bool, string> onCompleted = null, string returnFeeMode = null)
        {
            if (!string.IsNullOrEmpty(returnFeeMode))
                SetPendingReturnFeeMode(returnFeeMode);
            if (string.IsNullOrWhiteSpace(pondId))
            {
                onCompleted?.Invoke(false, DesktopLocalization.Text("ui.fishing.session.error.invalid_target_pond", "目标鱼塘无效。"));
                return;
            }
            if (_transitionBusy)
            {
                onCompleted?.Invoke(false, DesktopLocalization.Text("ui.fishing.session.status.switching_pond", "鱼塘切换正在进行，请稍候。"));
                return;
            }
            string reject;
            if (TryRejectRestrictedPond(pondId, out reject))
            {
                ErrorReceived?.Invoke(reject);
                onCompleted?.Invoke(false, reject);
                return;
            }
            // The fee mode belongs to the joined server session. A different selection must
            // finish the existing session and rejoin, even when the pond id stays the same.
            if (string.Equals(CurrentPondId, pondId, StringComparison.Ordinal) &&
                _socket != null && _socket.IsConnected && CurrentUser != null &&
                SameReturnFeeMode(CurrentUser.returnFeeMode, _pendingReturnFeeMode))
            {
                onCompleted?.Invoke(true, DesktopLocalization.Text("ui.fishing.session.status.already_in_pond", "已在目标鱼塘。"));
                return;
            }

            _transitionBusy = true;
            _switchingPond = true;
            ExitPondStopFishing((ok, message) =>
            {
                if (!ok)
                {
                    FinishTransition(false, message, onCompleted);
                    return;
                }
                JoinTargetPond(pondId, onCompleted);
            });
        }

        public static bool SameReturnFeeMode(string current, string requested)
        {
            var currentMode = string.Equals(current, "auto_return", StringComparison.Ordinal) ? "auto_return" : "sell_only";
            var requestedMode = string.Equals(requested, "auto_return", StringComparison.Ordinal) ? "auto_return" : "sell_only";
            return string.Equals(currentMode, requestedMode, StringComparison.Ordinal);
        }

        void JoinTargetPond(string pondId, Action<bool, string> onCompleted)
        {
            CurrentPondId = pondId;
            _joinRequested = true;
            if (_socket == null || !_socket.IsConnected)
            {
                FinishTransition(false, DesktopLocalization.Text("ui.fishing.session.error.realtime_disconnected", "实时服务已断开，请重新进入鱼塘。"), onCompleted);
                return;
            }
            _socket.RegisterPlayer(_auth.AuthenticatedPlayerId);
            _socket.JoinPond(new JoinPondPayload
            {
                pondId = CurrentPondId,
                nickname = Nickname,
                playerId = _auth.AuthenticatedPlayerId,
                returnFeeMode = _pendingReturnFeeMode,
            }, (ok, message) =>
            {
                if (!ok)
                {
                    FinishTransition(false, message, onCompleted);
                    return;
                }
                FinishTransition(true, DesktopLocalization.Text("ui.fishing.session.status.pond_entered", "已进入新鱼塘。"), onCompleted);
            });
        }

        void FinishTransition(bool ok, string message, Action<bool, string> onCompleted)
        {
            _transitionBusy = false;
            if (!ok)
                _switchingPond = false;
            onCompleted?.Invoke(ok, message);
        }

        bool TryRejectRestrictedPond(string pondId, out string message)
        {
            message = null;
            if (string.IsNullOrEmpty(AllowedPondIdOnly))
                return false;
            if (string.Equals(pondId, AllowedPondIdOnly, StringComparison.Ordinal))
                return false;
            message = DesktopLocalization.Text("ui.fishing.session.error.tutorial_required", "新手引导进行中，请先完成新手塘流程（不可跳过）。");
            return true;
        }

        static bool IsCatchAlreadyClaimed(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;
            return message.IndexOf("没有待领取", StringComparison.Ordinal) >= 0
                || message.IndexOf("鱼已过期", StringComparison.Ordinal) >= 0;
        }

        void ClearLocalPondState()
        {
            LatestSnapshot = null;
            CurrentUser = null;
            CurrentInventory = new FishInventoryItemDto[0];
            _latestCatch = null;
            _hookPrompt = null;
            LastFishingOutcome = string.Empty;
            _users.Clear();
            _messages.Clear();
            ActivePoliceRaid = null;
            UsersChanged?.Invoke();
        }

        public void SendChat(string text, Action<bool, string> onCompleted = null)
        {
            var value = text == null ? string.Empty : text.Trim();
            if (string.IsNullOrEmpty(value))
            {
                onCompleted?.Invoke(false, DesktopLocalization.Text("ui.fishing.session.error.empty_chat_message", "不能发送空消息。"));
                return;
            }
            if (value.Length > 200)
            {
                onCompleted?.Invoke(false, DesktopLocalization.Text("ui.fishing.session.error.chat_message_too_long", "鱼塘聊天最多 200 字。"));
                return;
            }
            if (_socket == null || !_socket.IsConnected)
            {
                onCompleted?.Invoke(false, DesktopLocalization.Text("ui.fishing.session.error.chat_disconnected", "实时服务未连接，请进入鱼塘后重试。"));
                return;
            }
            _socket.SendChat(new SendChatPayload
            {
                pondId = CurrentPondId,
                text = value,
            }, onCompleted);
        }

        public void Disconnect()
        {
            _joinRequested = false;
            _socket?.Disconnect();
        }

        void OnStateChanged(SocialSocketState state, string message)
        {
            Debug.Log("[Pond] Socket state changed: " + state + " message=" + message);
            if (state == SocialSocketState.Connected && _joinRequested &&
                _auth != null && _auth.IsAuthenticated)
            {
                Debug.Log("[Pond] Socket connected; registering player and joining pond " + CurrentPondId);
                _socket.RegisterPlayer(_auth.AuthenticatedPlayerId);
                _socket.JoinPond(new JoinPondPayload
                {
                    pondId = CurrentPondId,
                    nickname = _nickname,
                    playerId = _auth.AuthenticatedPlayerId,
                    returnFeeMode = _pendingReturnFeeMode,
                }, (joined, joinMessage) =>
                {
                    if (!joined)
                        ErrorReceived?.Invoke(joinMessage);
                });
            }
            if (state == SocialSocketState.Disconnected ||
                state == SocialSocketState.Failed)
            {
                if (_users.Count > 0)
                {
                    _users.Clear();
                    UsersChanged?.Invoke();
                }
            }

            StateChanged?.Invoke(state, message);
        }

        void OnSnapshot(PondSnapshotDto snapshot)
        {
            LatestSnapshot = snapshot;
            ReplaceUsers(snapshot?.users);
            CurrentUser = FindCurrentUser(snapshot);
            ReconcileFishingSleep();
            CurrentInventory = snapshot?.inventory ?? new FishInventoryItemDto[0];
            ReplaceMessages(snapshot?.messages);
            SnapshotChanged?.Invoke(snapshot);
            UsersChanged?.Invoke();
        }

        void OnUserJoined(PondUserDto user)
        {
            if (user == null)
                return;
            UpsertUser(user);
            if (IsSelf(user))
            {
                var key = UserKey(user);
                CurrentUser = !string.IsNullOrEmpty(key) && _users.TryGetValue(key, out var merged)
                    ? merged
                    : user;
                ReconcileFishingSleep();
            }
            UsersChanged?.Invoke();
        }

        void OnUserLeft(string userId)
        {
            if (string.IsNullOrEmpty(userId) || !RemoveUser(userId))
                return;
            if (CurrentUser != null &&
                (CurrentUser.id == userId || CurrentUser.playerId == userId))
                CurrentUser = null;
            UsersChanged?.Invoke();
        }

        void OnUserUpdated(PondUserDto user)
        {
            if (user == null)
                return;
            UpsertUser(user);
            if (IsSelf(user))
            {
                var key = UserKey(user);
                CurrentUser = !string.IsNullOrEmpty(key) && _users.TryGetValue(key, out var merged)
                    ? merged
                    : user;
                ReconcileFishingSleep();
                if (CurrentUser.fishingPhase == "hooked" || CurrentUser.fishingPhase == "baiting" ||
                    CurrentUser.fishingPhase == "casting" || CurrentUser.fishingPhase == "waiting")
                    LastFishingOutcome = string.Empty;
            }
            UserUpdated?.Invoke(IsSelf(user) ? CurrentUser : user);
            UsersChanged?.Invoke();
        }

        void OnSessionTimerTick(SessionTimerTickDto tick)
        {
            if (tick == null || string.IsNullOrEmpty(tick.userId))
                return;

            PondUserDto user = null;
            if (_users.TryGetValue(tick.userId, out user))
            {
                ApplySessionTimerTick(user, tick);
            }
            else
            {
                foreach (var pair in _users)
                {
                    if (pair.Value != null &&
                        (pair.Value.id == tick.userId || pair.Value.playerId == tick.userId))
                    {
                        user = pair.Value;
                        ApplySessionTimerTick(user, tick);
                        break;
                    }
                }
            }

            if (user == null)
                return;

            if (IsSelf(user))
                CurrentUser = user;
            UsersChanged?.Invoke();
        }

        static void ApplySessionTimerTick(PondUserDto user, SessionTimerTickDto tick)
        {
            var nextMs = Math.Max(0, tick.sessionFishingMs);
            if (nextMs == 0 && user.sessionFishingMs > 0 &&
                IsFishingPhaseActive(user.fishingPhase))
                return;

            user.sessionFishingMs = nextMs;
        }

        static bool IsFishingPhaseActive(string phase)
        {
            return phase == "waiting" ||
                   phase == "baiting" ||
                   phase == "casting" ||
                   phase == "hooked" ||
                   phase == "resolving";
        }

        void ReconcileFishingSleep()
        {
            if (!IsFishingAsleep)
                return;
            _latestCatch = null;
            _hookPrompt = null;
            LastFishingOutcome = string.Empty;
        }

        void OnFishBite(PendingFishCatchDto fishCatch)
        {
            if (IsFishingAsleep)
                return;
            LastFishingOutcome = "catch";
            _latestCatch = fishCatch;
            // Apply hooked locally as soon as fish_bite arrives so Overlay does
            // not wait for the companion pond_user_updated packet.
            if (CurrentUser != null && fishCatch != null)
            {
                CurrentUser.fishingPhase = "hooked";
                if (CurrentUser.phaseContext == null) CurrentUser.phaseContext = new PondUserPhaseContextDto();
                CurrentUser.phaseContext.catchId = fishCatch.catchId;
                CurrentUser.phaseContext.sizeM = fishCatch.sizeM;
                var duration = fishCatch.hookDurationMs > 0 ? fishCatch.hookDurationMs : 5000;
                CurrentUser.phaseEndsAt =
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + duration;
                UserUpdated?.Invoke(CurrentUser);
            }
            FishBiteReceived?.Invoke(fishCatch);
        }

        void OnFishMiss(FishingMissDto miss)
        {
            if (miss == null || (!string.IsNullOrEmpty(miss.actorId) && miss.actorId != CurrentUser?.id) ||
                (!string.IsNullOrEmpty(miss.pondId) && miss.pondId != CurrentPondId)) return;
            if (IsFishingAsleep)
                return;
            LastFishingOutcome = "escape";
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _hookPrompt = OverlayHookPrompt.Escape(nowMs);
            if (CurrentUser != null)
            {
                CurrentUser.fishingPhase = "resolving";
                if (CurrentUser.phaseContext == null)
                    CurrentUser.phaseContext = new PondUserPhaseContextDto();
                CurrentUser.phaseContext.outcome = "escape";
                CurrentUser.phaseContext.resultId = miss.resultId;
                UserUpdated?.Invoke(CurrentUser);
            }
            FishMissReceived?.Invoke(miss);
        }

        void OnFishCatchSettled(FishCatchSettledDto settled)
        {
            LastFishingOutcome = "catch";
            _latestCatch = null;
            if (CurrentUser != null)
                CurrentUser.sessionCatchCount = Math.Max(0, CurrentUser.sessionCatchCount) + 1;
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (settled != null && settled.autoReturned && settled.gold > 0)
                _hookPrompt = OverlayHookPrompt.Return(settled.sizeM, settled.gold, nowMs);
            else if (settled != null)
                _hookPrompt = OverlayHookPrompt.Keep(settled.sizeM, nowMs);
            CatchAccepted?.Invoke();
            FishCatchSettled?.Invoke(settled);
        }

        void OnPondSessionSummary(PondSessionSummaryDto summary)
        {
            LastSessionSummary = summary;
            PondSessionSummaryReceived?.Invoke(summary);
        }

        void OnInventoryUpdated(FishInventoryItemDto[] items)
        {
            CurrentInventory = items ?? new FishInventoryItemDto[0];
            InventoryUpdated?.Invoke(CurrentInventory);
        }

        public void ReplaceInventory(FishInventoryItemDto[] items)
        {
            OnInventoryUpdated(items);
        }

        void OnChatMessage(ChatMessageDto message)
        {
            if (message == null)
                return;
            AppendMessage(message);
            ChatMessageReceived?.Invoke(message);
        }

        void OnCodexUnlocked(CodexUnlockDto unlock)
        {
            CodexUnlocked?.Invoke(unlock);
        }

        void OnAchievementUnlocked(AchievementUnlockDto unlock)
        {
            AchievementUnlocked?.Invoke(unlock);
        }

        void OnFriendRequest(FriendRequestDto request)
        {
            FriendRequestReceived?.Invoke(request);
        }

        void OnDirectMessage(DirectMessageDto message)
        {
            DmMessageReceived?.Invoke(message);
        }

        void OnPostLiked(PostLikedDto message)
        {
            PostLikedReceived?.Invoke(message);
        }

        void OnPostCommented(PostCommentedDto message)
        {
            PostCommentedReceived?.Invoke(message);
        }

        void OnPostCommentDeleted(PostCommentDeletedDto message)
        {
            PostCommentDeletedReceived?.Invoke(message);
        }

        void OnPoliceRaid(PoliceRaidDto raid)
        {
            if (raid == null)
                return;
            if (raid.status == "warning")
            {
                ActivePoliceRaid = raid;
                PoliceRaidReceived?.Invoke(raid);
                return;
            }

            ActivePoliceRaid = null;
            if (raid.status == "fined")
                ApplyPoliceEject(raid.message);
            PoliceRaidReceived?.Invoke(raid);
        }

        void ApplyPoliceEject(string message)
        {
            if (_socket != null && _socket.IsConnected)
                _socket.Disconnect();
            ClearLocalPondState();
            if (!string.IsNullOrEmpty(message))
                ErrorReceived?.Invoke(message);
        }

        void OnError(string message)
        {
            ErrorReceived?.Invoke(message);
        }

        void OnDestroy()
        {
            Debug.Log("[Shutdown] SocialPondSessionController.OnDestroy begin.");
            if (_socket == null)
            {
                Debug.Log("[Shutdown] SocialPondSessionController has no socket.");
                return;
            }
            _socket.StateChanged -= OnStateChanged;
            _socket.PondSnapshotReceived -= OnSnapshot;
            _socket.PondUserJoined -= OnUserJoined;
            _socket.PondUserLeft -= OnUserLeft;
            _socket.PondUserUpdated -= OnUserUpdated;
            _socket.SessionTimerTick -= OnSessionTimerTick;
            _socket.FishBiteReceived -= OnFishBite;
            _socket.FishMissReceived -= OnFishMiss;
            _socket.FishCatchSettled -= OnFishCatchSettled;
            _socket.PondSessionSummaryReceived -= OnPondSessionSummary;
            _socket.InventoryUpdated -= OnInventoryUpdated;
            _socket.ChatMessageReceived -= OnChatMessage;
            _socket.CodexUnlocked -= OnCodexUnlocked;
            _socket.AchievementUnlocked -= OnAchievementUnlocked;
            _socket.FriendRequestReceived -= OnFriendRequest;
            _socket.DmMessageReceived -= OnDirectMessage;
            _socket.PostLikedReceived -= OnPostLiked;
            _socket.PostCommentedReceived -= OnPostCommented;
            _socket.PostCommentDeletedReceived -= OnPostCommentDeleted;
            _socket.PoliceRaidReceived -= OnPoliceRaid;
            _socket.ErrorReceived -= OnError;
            _socket.Disconnect();
            Debug.Log("[Shutdown] SocialPondSessionController.OnDestroy complete.");
        }

        public PondUserDto[] VisibleOthers
        {
            get
            {
                var others = new List<PondUserDto>(_users.Count);
                foreach (var user in _users.Values)
                {
                    if (user != null && !IsSelf(user))
                        others.Add(user);
                }
                return others.ToArray();
            }
        }

        void ReplaceUsers(PondUserDto[] users)
        {
            var previous = new Dictionary<string, PondUserDto>(_users);
            _users.Clear();
            if (users == null)
                return;
            for (var i = 0; i < users.Length; i++)
            {
                var incoming = users[i];
                var key = UserKey(incoming);
                if (string.IsNullOrEmpty(key) || incoming == null)
                    continue;
                if (previous.TryGetValue(key, out var prev))
                    _users[key] = PondUserMerge.Merge(prev, incoming);
                else
                    _users[key] = incoming;
            }
        }

        void UpsertUser(PondUserDto user)
        {
            var key = UserKey(user);
            if (string.IsNullOrEmpty(key) || user == null)
                return;
            if (_users.TryGetValue(key, out var prev))
                _users[key] = PondUserMerge.Merge(prev, user);
            else
                _users[key] = user;
        }

        bool RemoveUser(string idOrPlayerId)
        {
            if (_users.Remove(idOrPlayerId))
                return true;
            string match = null;
            foreach (var pair in _users)
            {
                if (pair.Value != null &&
                    (pair.Value.id == idOrPlayerId || pair.Value.playerId == idOrPlayerId))
                {
                    match = pair.Key;
                    break;
                }
            }

            return match != null && _users.Remove(match);
        }

        static string UserKey(PondUserDto user)
        {
            if (user == null)
                return null;
            if (!string.IsNullOrEmpty(user.id))
                return user.id;
            return user.playerId;
        }

        bool IsSelf(PondUserDto user)
        {
            return user != null && _auth != null &&
                   !string.IsNullOrEmpty(_auth.AuthenticatedPlayerId) &&
                   user.playerId == _auth.AuthenticatedPlayerId;
        }

        PondUserDto FindCurrentUser(PondSnapshotDto snapshot)
        {
            foreach (var user in _users.Values)
            {
                if (IsSelf(user))
                    return user;
            }

            if (snapshot?.users == null || _auth == null)
                return null;
            foreach (var user in snapshot.users)
            {
                if (IsSelf(user))
                    return user;
            }

            return null;
        }

        void ReplaceMessages(ChatMessageDto[] messages)
        {
            _messages.Clear();
            if (messages == null)
                return;
            for (var i = 0; i < messages.Length; i++)
                AppendMessage(messages[i]);
        }

        void AppendMessage(ChatMessageDto message)
        {
            if (message == null)
                return;
            for (var i = 0; i < _messages.Count; i++)
            {
                if (!string.IsNullOrEmpty(message.id) && _messages[i] != null && _messages[i].id == message.id)
                    return;
            }
            _messages.Add(message);
            while (_messages.Count > 200)
                _messages.RemoveAt(0);
        }
    }
}
