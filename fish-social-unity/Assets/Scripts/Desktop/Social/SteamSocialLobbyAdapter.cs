using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
using Steamworks;
#endif

namespace FishSocial.Desktop.Social
{
    [Serializable]
    public sealed class SteamFriendInfo
    {
        public string steamId64;
        public string name;
        public bool online;
    }

    public interface ISteamSocialLobbyAdapter
    {
        bool IsAvailable { get; }
        event Action<IReadOnlyList<SteamFriendInfo>> FriendsChanged;
        event Action<string, string> LobbyCreated;
        event Action<string> LobbyEntered;
        event Action<string> LobbyInviteReceived;
        event Action<string> Error;

        void RefreshFriends();
        void CreateLobby(string pondId, string gameVersion, string protocolVersion);
        void JoinLobby(string lobbyId);
        void InviteFriend(string friendSteamId64);
        void CloseLobby();
        void LeaveLobby();
        bool TryReadLobbyMetadata(
            string lobbyId,
            out string pondId,
            out string gameVersion,
            out string protocolVersion,
            out string inviteToken);
        IReadOnlyList<string> ReadLobbyMembers(string lobbyId);
        string GetLocalSteamId64();
        void SetLobbyInvite(string friendSteamId64, string inviteToken);
    }

    /// <summary>
    /// Steam friends/lobby adapter. It owns Steamworks callbacks; UI and
    /// SocialLobbyController never call Steamworks directly.
    /// </summary>
    #pragma warning disable 0067
    public sealed class SteamSocialLobbyAdapter : MonoBehaviour, ISteamSocialLobbyAdapter
    {
        const string PondKey = "pondId";
        const string GameVersionKey = "gameVersion";
        const string ProtocolVersionKey = "protocolVersion";

        public bool IsAvailable
        {
            get
            {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
                return _initialized && SteamAPI.IsSteamRunning();
#else
                return false;
#endif
            }
        }

        public event Action<IReadOnlyList<SteamFriendInfo>> FriendsChanged;
        public event Action<string, string> LobbyCreated;
        public event Action<string> LobbyEntered;
        public event Action<string> LobbyInviteReceived;
        public event Action<string> Error;

        string _pondId;
        string _gameVersion;
        string _protocolVersion;
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
        bool _initialized;
        CSteamID _currentLobby;
        Callback<LobbyCreated_t> _lobbyCreated;
        Callback<LobbyEnter_t> _lobbyEntered;
        Callback<GameLobbyJoinRequested_t> _lobbyJoinRequested;
#endif

        void Awake()
        {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            EnsureSteamCallbacks();
#endif
        }

        void EnsureSteamCallbacks()
        {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            if (_initialized)
                return;
            _initialized = SteamAPI.IsSteamRunning();
            if (!_initialized)
                return;
            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            _lobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested);
#endif
        }

        void Update()
        {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            if (_initialized)
                SteamAPI.RunCallbacks();
#endif
        }

        public void RefreshFriends()
        {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            EnsureSteamCallbacks();
            if (!IsAvailable)
            {
                Error?.Invoke("Steam 客户端未运行。");
                return;
            }
            var friends = new List<SteamFriendInfo>();
            var flags = EFriendFlags.k_EFriendFlagImmediate;
            var count = SteamFriends.GetFriendCount(flags);
            for (var i = 0; i < count; i++)
            {
                var id = SteamFriends.GetFriendByIndex(i, flags);
                friends.Add(new SteamFriendInfo
                {
                    steamId64 = id.m_SteamID.ToString(),
                    name = SteamFriends.GetFriendPersonaName(id),
                    online = SteamFriends.GetFriendPersonaState(id) != EPersonaState.k_EPersonaStateOffline,
                });
            }
            FriendsChanged?.Invoke(friends);
#else
            Error?.Invoke("Steam 好友功能仅支持 Windows Standalone。");
#endif
        }

        public void CreateLobby(string pondId, string gameVersion, string protocolVersion)
        {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            if (!IsAvailable)
            {
                Error?.Invoke("Steam 客户端未运行。");
                return;
            }
            _pondId = pondId;
            _gameVersion = gameVersion;
            _protocolVersion = protocolVersion;
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 8);
#else
            Error?.Invoke("Steam Lobby 仅支持 Windows Standalone。");
#endif
        }

        public void JoinLobby(string lobbyId)
        {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            if (!IsAvailable || !ulong.TryParse(lobbyId, out var rawId))
            {
                Error?.Invoke("Lobby ID 无效或 Steam 未运行。");
                return;
            }
            SteamMatchmaking.JoinLobby(new CSteamID(rawId));
#else
            Error?.Invoke("Steam Lobby 仅支持 Windows Standalone。");
#endif
        }

        public void InviteFriend(string friendSteamId64)
        {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            if (_currentLobby.m_SteamID == 0 || !ulong.TryParse(friendSteamId64, out var rawId))
            {
                Error?.Invoke("请先创建或加入 Lobby。");
                return;
            }
            if (!SteamMatchmaking.InviteUserToLobby(_currentLobby, new CSteamID(rawId)))
                Error?.Invoke("Steam 邀请发送失败。");
#else
            Error?.Invoke("Steam Lobby 仅支持 Windows Standalone。");
#endif
        }

        public void CloseLobby()
        {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            if (_currentLobby.m_SteamID != 0)
            {
                SteamMatchmaking.SetLobbyJoinable(_currentLobby, false);
                SteamMatchmaking.LeaveLobby(_currentLobby);
            }
            _currentLobby = new CSteamID(0);
#endif
        }

        public void SetLobbyInvite(string friendSteamId64, string inviteToken)
        {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            if (_currentLobby.m_SteamID == 0 || string.IsNullOrWhiteSpace(friendSteamId64))
                return;
            SteamMatchmaking.SetLobbyData(
                _currentLobby,
                "invite_" + friendSteamId64,
                inviteToken ?? "");
#endif
        }

        public void LeaveLobby()
        {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            if (_currentLobby.m_SteamID != 0)
                SteamMatchmaking.LeaveLobby(_currentLobby);
            _currentLobby = new CSteamID(0);
#endif
        }

        public bool TryReadLobbyMetadata(
            string lobbyId,
            out string pondId,
            out string gameVersion,
            out string protocolVersion,
            out string inviteToken)
        {
            pondId = null;
            gameVersion = null;
            protocolVersion = null;
            inviteToken = null;
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            if (!ulong.TryParse(lobbyId, out var rawId))
                return false;
            var lobby = new CSteamID(rawId);
            pondId = SteamMatchmaking.GetLobbyData(lobby, PondKey);
            gameVersion = SteamMatchmaking.GetLobbyData(lobby, GameVersionKey);
            protocolVersion = SteamMatchmaking.GetLobbyData(lobby, ProtocolVersionKey);
            var localSteamId = SteamUser.GetSteamID().m_SteamID.ToString();
            inviteToken = SteamMatchmaking.GetLobbyData(lobby, "invite_" + localSteamId);
            return !string.IsNullOrWhiteSpace(pondId);
#else
            return false;
#endif
        }

        public IReadOnlyList<string> ReadLobbyMembers(string lobbyId)
        {
            var members = new List<string>();
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            if (!ulong.TryParse(lobbyId, out var rawId))
                return members;
            var lobby = new CSteamID(rawId);
            var count = SteamMatchmaking.GetNumLobbyMembers(lobby);
            for (var i = 0; i < count; i++)
                members.Add(SteamMatchmaking.GetLobbyMemberByIndex(lobby, i).m_SteamID.ToString());
#endif
            return members;
        }

        public string GetLocalSteamId64()
        {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            return IsAvailable ? SteamUser.GetSteamID().m_SteamID.ToString() : null;
#else
            return null;
#endif
        }

#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
        void OnLobbyCreated(LobbyCreated_t callback)
        {
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                Error?.Invoke("Lobby 创建失败：" + callback.m_eResult);
                return;
            }
            _currentLobby = new CSteamID(callback.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(_currentLobby, PondKey, _pondId ?? "");
            SteamMatchmaking.SetLobbyData(_currentLobby, GameVersionKey, _gameVersion ?? "");
            SteamMatchmaking.SetLobbyData(_currentLobby, ProtocolVersionKey, _protocolVersion ?? "");
            SteamMatchmaking.SetLobbyJoinable(_currentLobby, true);
            LobbyCreated?.Invoke(_currentLobby.m_SteamID.ToString(), _pondId);
        }

        void OnLobbyEntered(LobbyEnter_t callback)
        {
            _currentLobby = new CSteamID(callback.m_ulSteamIDLobby);
            if (callback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Error?.Invoke("加入 Lobby 失败。");
                return;
            }
            LobbyEntered?.Invoke(_currentLobby.m_SteamID.ToString());
        }

        void OnLobbyJoinRequested(GameLobbyJoinRequested_t callback)
        {
            LobbyInviteReceived?.Invoke(callback.m_steamIDLobby.m_SteamID.ToString());
        }

        void OnDestroy()
        {
            _lobbyCreated?.Dispose();
            _lobbyEntered?.Dispose();
            _lobbyJoinRequested?.Dispose();
        }
#endif
    }
    #pragma warning restore 0067
}
