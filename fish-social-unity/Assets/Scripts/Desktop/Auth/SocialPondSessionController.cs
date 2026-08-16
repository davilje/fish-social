using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishSocial.Desktop.Auth
{
    /// <summary>
    /// Owns the authenticated pond session so UI code does not send raw Socket.IO
    /// events or decide authoritative player state.
    /// </summary>
    public sealed class SocialPondSessionController : MonoBehaviour
    {
        const string DefaultPondId = "pond-calm";

        ISocialSocketClient _socket;
        SteamAuthController _auth;
        readonly Dictionary<string, PondUserDto> _users = new Dictionary<string, PondUserDto>();
        readonly List<ChatMessageDto> _messages = new List<ChatMessageDto>();
        PendingFishCatchDto _latestCatch;
        string _nickname;
        bool _joinRequested;

        public SocialSocketState State => _socket?.State ?? SocialSocketState.Disconnected;
        public PondSnapshotDto LatestSnapshot { get; private set; }
        public PondUserDto CurrentUser { get; private set; }
        public FishInventoryItemDto[] CurrentInventory { get; private set; } =
            new FishInventoryItemDto[0];
        public string CurrentPondId { get; private set; } = DefaultPondId;
        public string CurrentPhase => CurrentUser?.fishingPhase ?? "idle";
        public bool CanStopFishing =>
            CurrentPhase == "baiting" || CurrentPhase == "casting" ||
            CurrentPhase == "waiting" || CurrentPhase == "hooked" ||
            CurrentPhase == "resolving";
        public string FirstSpotId =>
            LatestSnapshot?.pond?.spots != null && LatestSnapshot.pond.spots.Length > 0
                ? LatestSnapshot.pond.spots[0].id
                : "calm-spot-1";
        public event Action<SocialSocketState, string> StateChanged;
        public event Action<PondSnapshotDto> SnapshotChanged;
        public event Action<PondUserDto> UserUpdated;
        public event Action UsersChanged;
        public event Action<PendingFishCatchDto> FishBiteReceived;
        public event Action<FishInventoryItemDto[]> InventoryUpdated;
        public event Action<ChatMessageDto> ChatMessageReceived;
        public event Action<CodexUnlockDto> CodexUnlocked;
        public event Action<FriendRequestDto> FriendRequestReceived;
        public event Action<DirectMessageDto> DmMessageReceived;
        public event Action<string> ErrorReceived;
        public string Nickname => string.IsNullOrWhiteSpace(_nickname) ? "Steam玩家" : _nickname;
        public ChatMessageDto[] PondMessages => _messages.ToArray();

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
            _socket.FishBiteReceived += OnFishBite;
            _socket.InventoryUpdated += OnInventoryUpdated;
            _socket.ChatMessageReceived += OnChatMessage;
            _socket.CodexUnlocked += OnCodexUnlocked;
            _socket.FriendRequestReceived += OnFriendRequest;
            _socket.DmMessageReceived += OnDirectMessage;
            _socket.ErrorReceived += OnError;
        }

        void Update()
        {
            _socket?.Pump();
        }

        public void ConnectAndJoin(string pondId = DefaultPondId, string nickname = "Steam玩家")
        {
            Debug.Log("[Pond] ConnectAndJoin requested. pondId=" + pondId);
            if (_auth == null || !_auth.IsAuthenticated)
            {
                Debug.LogWarning("[Pond] ConnectAndJoin rejected: Steam session is not authenticated.");
                ErrorReceived?.Invoke("请先完成 Steam 登录。");
                return;
            }

            CurrentPondId = string.IsNullOrWhiteSpace(pondId) ? DefaultPondId : pondId;
            _nickname = string.IsNullOrWhiteSpace(nickname) ? "Steam玩家" : nickname;
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
            if (CurrentUser != null && CurrentPhase != "seated" && CurrentPhase != "idle")
            {
                onCompleted?.Invoke(false, "当前 phase 为 " + CurrentPhase + "，请等收杆完成后再开始。");
                return;
            }
            _socket?.StartFishing(new StartFishingPayload
            {
                pondId = CurrentPondId,
                spotId = spotId,
            }, onCompleted);
        }

        public void StartFishingAtFirstSpot(Action<bool, string> onCompleted = null)
        {
            StartFishing(FirstSpotId, onCompleted);
        }

        public void StopFishing(Action<bool, string> onCompleted = null)
        {
            if (!CanStopFishing)
            {
                onCompleted?.Invoke(false, "当前没有正在进行的钓鱼。");
                return;
            }
            _socket?.StopFishing(CurrentPondId, onCompleted);
        }

        public void AcceptLatestCatch(Action<bool, string> onCompleted = null)
        {
            if (_latestCatch == null)
            {
                if (CurrentPhase == "hooked")
                    onCompleted?.Invoke(false, "鱼已咬钩，正在结算，请等待鱼获事件。");
                else
                    onCompleted?.Invoke(false, "当前没有待领取的鱼获。");
                return;
            }
            _socket?.AcceptCatch(_latestCatch.catchId, onCompleted);
        }

        public void SendChat(string text, Action<bool, string> onCompleted = null)
        {
            var value = text == null ? string.Empty : text.Trim();
            if (string.IsNullOrEmpty(value))
            {
                onCompleted?.Invoke(false, "不能发送空消息。");
                return;
            }
            if (value.Length > 200)
            {
                onCompleted?.Invoke(false, "鱼塘聊天最多 200 字。");
                return;
            }
            if (_socket == null || !_socket.IsConnected)
            {
                onCompleted?.Invoke(false, "实时服务未连接，请进入鱼塘后重试。");
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
                CurrentUser = user;
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
                CurrentUser = user;
                UserUpdated?.Invoke(user);
            }
            UsersChanged?.Invoke();
        }

        void OnFishBite(PendingFishCatchDto fishCatch)
        {
            _latestCatch = fishCatch;
            FishBiteReceived?.Invoke(fishCatch);
        }

        void OnInventoryUpdated(FishInventoryItemDto[] items)
        {
            CurrentInventory = items ?? new FishInventoryItemDto[0];
            InventoryUpdated?.Invoke(CurrentInventory);
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

        void OnFriendRequest(FriendRequestDto request)
        {
            FriendRequestReceived?.Invoke(request);
        }

        void OnDirectMessage(DirectMessageDto message)
        {
            DmMessageReceived?.Invoke(message);
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
            _socket.FishBiteReceived -= OnFishBite;
            _socket.InventoryUpdated -= OnInventoryUpdated;
            _socket.ChatMessageReceived -= OnChatMessage;
            _socket.CodexUnlocked -= OnCodexUnlocked;
            _socket.FriendRequestReceived -= OnFriendRequest;
            _socket.DmMessageReceived -= OnDirectMessage;
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
            _users.Clear();
            if (users == null)
                return;
            for (var i = 0; i < users.Length; i++)
                UpsertUser(users[i]);
        }

        void UpsertUser(PondUserDto user)
        {
            var key = UserKey(user);
            if (string.IsNullOrEmpty(key) || user == null)
                return;
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
