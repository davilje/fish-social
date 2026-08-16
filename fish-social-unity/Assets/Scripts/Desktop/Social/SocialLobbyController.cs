using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop.Social
{
    public enum SocialLobbyState
    {
        SignedOut,
        LoadingFriends,
        Ready,
        Creating,
        WaitingForInvite,
        Joining,
        LobbyJoined,
        EnteringPond,
        InPond,
        Failed,
    }

    public sealed class SocialLobbyController : MonoBehaviour
    {
        public const string GameVersion = "1.0-steam-desktop";
        public const string ProtocolVersion = "1.0.0-draft";

        ISteamSocialLobbyAdapter _adapter;
        SocialLobbyApiClient _api;
        SocialPondSessionController _pondSession;
        SteamAuthController _auth;
        string _pendingPondId;
        string _pendingLobbyId;

        public SocialLobbyState State { get; private set; } = SocialLobbyState.SignedOut;
        public IReadOnlyList<SteamFriendInfo> Friends { get; private set; } =
            new List<SteamFriendInfo>();
        public IReadOnlyList<string> LobbyMembers { get; private set; } =
            new List<string>();
        public string CurrentLobbyId { get; private set; }
        public string CurrentPondId { get; private set; }
        public event Action<SocialLobbyState, string> StateChanged;
        public event Action<IReadOnlyList<SteamFriendInfo>> FriendsChanged;
        public event Action<IReadOnlyList<string>> LobbyMembersChanged;
        public event Action<string> Error;

        public void Configure(
            SteamAuthController auth,
            SocialPondSessionController pondSession,
            ISteamSocialLobbyAdapter adapter,
            string serverBaseUrl = "http://localhost:3001")
        {
            _auth = auth;
            _pondSession = pondSession;
            _adapter = adapter;
            _api = new SocialLobbyApiClient(auth, serverBaseUrl);
            _adapter.FriendsChanged += OnFriendsChanged;
            _adapter.LobbyCreated += OnLobbyCreated;
            _adapter.LobbyEntered += OnLobbyEntered;
            _adapter.LobbyInviteReceived += OnLobbyInviteReceived;
            _adapter.Error += OnAdapterError;
            if (_pondSession != null)
            {
                _pondSession.SnapshotChanged += OnPondSnapshot;
                _pondSession.ErrorReceived += OnPondError;
            }
            SetState(auth != null && auth.IsAuthenticated
                ? SocialLobbyState.Ready
                : SocialLobbyState.SignedOut, null);
        }

        public void RefreshFriends()
        {
            if (!EnsureAuthenticated())
                return;
            SetState(SocialLobbyState.LoadingFriends, "正在加载 Steam 好友。");
            _adapter.RefreshFriends();
        }

        public void CreateLobby(string pondId)
        {
            if (!EnsureAuthenticated())
                return;
            if (string.IsNullOrWhiteSpace(pondId))
            {
                Fail("请先选择鱼塘。");
                return;
            }
            _pendingPondId = pondId;
            SetState(SocialLobbyState.Creating, "正在创建 Lobby。");
            _adapter.CreateLobby(pondId, GameVersion, ProtocolVersion);
        }

        public void JoinLobby(string lobbyId)
        {
            if (!EnsureAuthenticated())
                return;
            if (string.IsNullOrWhiteSpace(lobbyId))
            {
                Fail("Lobby ID 为空。");
                return;
            }
            _pendingLobbyId = lobbyId;
            SetState(SocialLobbyState.Joining, "正在加入 Lobby。");
            _adapter.JoinLobby(lobbyId);
        }

        public void InviteFriend(string friendSteamId64)
        {
            if (!EnsureAuthenticated())
                return;
            if (string.IsNullOrWhiteSpace(CurrentLobbyId))
            {
                Fail("请先创建 Lobby。");
                return;
            }
            StartCoroutine(_api.Invite(CurrentLobbyId, friendSteamId64, (ok, tokenOrMessage) =>
            {
                if (!ok)
                {
                    Fail(tokenOrMessage);
                    return;
                }
                _adapter.SetLobbyInvite(friendSteamId64, tokenOrMessage);
                _adapter.InviteFriend(friendSteamId64);
            }));
        }

        public void CloseLobby()
        {
            if (string.IsNullOrWhiteSpace(CurrentLobbyId))
                return;
            StartCoroutine(_api.Close(CurrentLobbyId, (ok, message) =>
            {
                _adapter.CloseLobby();
                if (ok)
                {
                    CurrentLobbyId = null;
                        SetState(
                            string.IsNullOrWhiteSpace(CurrentPondId)
                                ? SocialLobbyState.Ready
                                : SocialLobbyState.InPond,
                            message + " 当前鱼塘不会关闭。");
                }
                else
                    Fail(message);
            }));
        }

        /// <summary>
        /// Leave the social Lobby without leaving the authoritative pond.
        /// Pond lifecycle is controlled by Node/DB player presence, not Steam.
        /// </summary>
        public void LeaveLobby()
        {
            if (string.IsNullOrWhiteSpace(CurrentLobbyId))
                return;
            _adapter.LeaveLobby();
            CurrentLobbyId = null;
            SetState(
                string.IsNullOrWhiteSpace(CurrentPondId)
                    ? SocialLobbyState.Ready
                    : SocialLobbyState.InPond,
                "已离开 Lobby；当前鱼塘数据仍由服务端持续维护。");
        }

        void OnFriendsChanged(IReadOnlyList<SteamFriendInfo> friends)
        {
            Friends = friends ?? new List<SteamFriendInfo>();
            FriendsChanged?.Invoke(Friends);
            SetState(SocialLobbyState.Ready, "好友列表已更新。");
        }

        void OnLobbyCreated(string lobbyId, string pondId)
        {
            _pendingLobbyId = lobbyId;
            _pendingPondId = pondId;
            StartCoroutine(_api.Create(
                lobbyId,
                pondId,
                GameVersion,
                ProtocolVersion,
                (ok, lobby, message) =>
                {
                    if (!ok)
                    {
                        RollbackPendingLobby(message);
                        return;
                    }
                    if (lobby == null)
                    {
                        RollbackPendingLobby("服务端返回了无效的 Lobby 状态。");
                        return;
                    }
                    CurrentLobbyId = lobby.lobbyId;
                    CurrentPondId = lobby.pondId;
                    _pendingLobbyId = null;
                    _pendingPondId = null;
                    RefreshLobbyMembers();
                    SetState(SocialLobbyState.LobbyJoined, "Lobby 已创建，等待好友加入。");
                }));
        }

        void OnLobbyEntered(string lobbyId)
        {
            // Steam also emits LobbyEntered for the local owner after
            // CreateLobby. That is not an invited join and has no invite
            // token; the create flow already performs Node authorization.
            // Treating it as /join would return LOBBY_INVITE_INVALID and
            // clear the valid CurrentLobbyId before the user can invite.
            if (lobbyId == _pendingLobbyId || lobbyId == CurrentLobbyId)
                return;

            _pendingLobbyId = lobbyId;
            if (!_adapter.TryReadLobbyMetadata(
                    lobbyId,
                    out var pondId,
                    out var gameVersion,
                    out var protocolVersion,
                    out var inviteToken))
            {
                RollbackPendingLobby("Lobby 元数据缺失或已失效。");
                return;
            }
            StartCoroutine(_api.Join(
                lobbyId,
                gameVersion,
                protocolVersion,
                inviteToken,
                (ok, lobby, message) =>
                {
                    if (!ok)
                    {
                        RollbackPendingLobby(message);
                        return;
                    }
                    if (lobby == null)
                    {
                        RollbackPendingLobby("服务端返回了无效的 Lobby 状态。");
                        return;
                    }
                    CurrentLobbyId = lobby.lobbyId;
                    CurrentPondId = lobby.pondId;
                    _pendingLobbyId = null;
                    _pendingPondId = null;
                    RefreshLobbyMembers();
                    EnterPond(lobby.pondId);
                }));
        }

        void OnLobbyInviteReceived(string lobbyId)
        {
            DesktopNotificationService.Instance?.Publish(new DesktopNotification(
                NotificationKind.FriendInvite, "好友邀请", "收到 Steam Lobby 进塘邀请。"));
            _pendingLobbyId = lobbyId;
            SetState(SocialLobbyState.WaitingForInvite, "收到 Steam Lobby 邀请。");
            JoinLobby(lobbyId);
        }

        void EnterPond(string pondId)
        {
            if (_pondSession == null)
            {
                Fail("鱼塘会话控制器不可用。");
                return;
            }
            SetState(SocialLobbyState.EnteringPond, "正在进入鱼塘 " + pondId + "。");
            _pondSession.ConnectAndJoin(pondId, "Steam玩家");
        }

        void OnPondSnapshot(PondSnapshotDto snapshot)
        {
            if (snapshot?.pond == null || snapshot.pond.id != CurrentPondId)
                return;
            SetState(SocialLobbyState.InPond, "已进入鱼塘 " + CurrentPondId + "。");
        }

        void OnPondError(string message)
        {
            Fail(message);
        }

        void RefreshLobbyMembers()
        {
            if (string.IsNullOrWhiteSpace(CurrentLobbyId))
                return;
            LobbyMembers = _adapter.ReadLobbyMembers(CurrentLobbyId);
            LobbyMembersChanged?.Invoke(LobbyMembers);
        }

        bool EnsureAuthenticated()
        {
            if (_auth != null && _auth.IsAuthenticated && _adapter != null && _adapter.IsAvailable)
                return true;
            Fail(_adapter == null || !_adapter.IsAvailable
                ? "Steam 客户端未运行。"
                : "请先完成 Steam 登录。");
            return false;
        }

        void OnAdapterError(string message)
        {
            if (State == SocialLobbyState.Creating ||
                State == SocialLobbyState.Joining ||
                State == SocialLobbyState.WaitingForInvite)
            {
                RollbackPendingLobby(message);
                return;
            }
            Fail(message);
        }

        void RollbackPendingLobby(string message)
        {
            _adapter?.CloseLobby();
            CurrentLobbyId = null;
            CurrentPondId = null;
            _pendingLobbyId = null;
            _pendingPondId = null;
            SetState(SocialLobbyState.Failed, message);
            Error?.Invoke(message);
        }

        void Fail(string message)
        {
            SetState(SocialLobbyState.Failed, message);
            Error?.Invoke(message);
        }

        void SetState(SocialLobbyState state, string message)
        {
            State = state;
            StateChanged?.Invoke(state, message);
        }

        void OnDestroy()
        {
            if (_adapter == null)
                return;
            _adapter.FriendsChanged -= OnFriendsChanged;
            _adapter.LobbyCreated -= OnLobbyCreated;
            _adapter.LobbyEntered -= OnLobbyEntered;
            _adapter.LobbyInviteReceived -= OnLobbyInviteReceived;
            _adapter.Error -= OnAdapterError;
            if (_pondSession != null)
            {
                _pondSession.SnapshotChanged -= OnPondSnapshot;
                _pondSession.ErrorReceived -= OnPondError;
            }
        }
    }
}
