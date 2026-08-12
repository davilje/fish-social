using System;
using UnityEngine;

#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
using Steamworks;
#endif

namespace FishSocial.Desktop.Auth
{
    /// <summary>
    /// Steamworks.NET adapter for the Steam Web API authentication ticket.
    /// The editor keeps a safe unavailable fallback; the real implementation
    /// is compiled for Windows standalone builds.
    /// </summary>
    public sealed class SteamworksTicketProvider : MonoBehaviour, ISteamTicketProvider
    {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
        Callback<GetTicketForWebApiResponse_t> _ticketCallback;
        HAuthTicket _activeTicket;
        bool _steamInitialized;
        Action<byte[]> _onSuccess;
        Action<string> _onFailure;
#endif

        public bool IsSteamRunning
        {
            get
            {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
                return _steamInitialized && SteamAPI.IsSteamRunning();
#else
                return false;
#endif
            }
        }

        void Awake()
        {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            string error;
            var result = SteamAPI.InitEx(out error);
            _steamInitialized = result == ESteamAPIInitResult.k_ESteamAPIInitResult_OK;
            if (!_steamInitialized)
            {
                Debug.LogWarning("[SteamAuth] SteamAPI 初始化失败: " + error);
                return;
            }

            _ticketCallback = Callback<GetTicketForWebApiResponse_t>.Create(OnTicketReceived);
            Debug.Log("[SteamAuth] SteamAPI initialized.");
#else
            Debug.Log("[SteamAuth] Steamworks provider is unavailable in the Unity Editor.");
#endif
        }

        void Update()
        {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            if (_steamInitialized)
                SteamAPI.RunCallbacks();
#endif
        }

        public void RequestTicket(string identity, Action<byte[]> onSuccess, Action<string> onFailure)
        {
#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
            if (!IsSteamRunning)
            {
                onFailure?.Invoke("Steam 客户端未运行或 SteamAPI 初始化失败。");
                return;
            }

            if (string.IsNullOrWhiteSpace(identity))
            {
                onFailure?.Invoke("Steam 登录 identity 未配置。");
                return;
            }

            _onSuccess = onSuccess;
            _onFailure = onFailure;
            _activeTicket = SteamUser.GetAuthTicketForWebApi(identity);
#else
            onFailure?.Invoke("请在 Windows Standalone 构建中运行 Steam 登录。");
#endif
        }

#if UNITY_STANDALONE_WIN || STEAMWORKS_WIN
        void OnTicketReceived(GetTicketForWebApiResponse_t response)
        {
            if (response.m_eResult != EResult.k_EResultOK)
            {
                CompleteFailure("Steam 返回登录票据失败: " + response.m_eResult);
                return;
            }

            if (response.m_rgubTicket == null || response.m_cubTicket <= 0)
            {
                CompleteFailure("Steam 返回了空登录票据。");
                return;
            }

            var ticket = new byte[response.m_cubTicket];
            Array.Copy(response.m_rgubTicket, ticket, response.m_cubTicket);
            var success = _onSuccess;
            ClearPending();
            success?.Invoke(ticket);
        }

        void CompleteFailure(string message)
        {
            var failure = _onFailure;
            ClearPending();
            failure?.Invoke(message);
        }

        void ClearPending()
        {
            _onSuccess = null;
            _onFailure = null;
        }

        void OnDestroy()
        {
            if (_activeTicket != HAuthTicket.Invalid && _steamInitialized)
                SteamUser.CancelAuthTicket(_activeTicket);
            _ticketCallback?.Dispose();
            if (_steamInitialized)
                SteamAPI.Shutdown();
        }
#endif
    }
}
