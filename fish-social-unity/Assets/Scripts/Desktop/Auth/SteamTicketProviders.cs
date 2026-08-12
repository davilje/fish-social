using System;
using UnityEngine;

namespace FishSocial.Desktop.Auth
{
    /// <summary>
    /// Used until a Steamworks package is installed. It fails closed instead
    /// of silently entering demo mode.
    /// </summary>
    public sealed class UnavailableSteamTicketProvider : ISteamTicketProvider
    {
        public bool IsSteamRunning => false;

        public void RequestTicket(string identity, Action<byte[]> onSuccess, Action<string> onFailure)
        {
            onFailure?.Invoke("Steamworks 尚未安装或 Steam 未启动。");
        }
    }

    /// <summary>
    /// Editor/local-test-only seam. This is not a real Steam ticket and must
    /// never be used as a production fallback.
    /// </summary>
    public sealed class LocalFakeSteamTicketProvider : ISteamTicketProvider
    {
        readonly byte[] _ticket;
        public bool IsSteamRunning => true;

        public LocalFakeSteamTicketProvider(string ticket = "local-fake-ticket")
        {
            _ticket = System.Text.Encoding.UTF8.GetBytes(ticket);
        }

        public void RequestTicket(string identity, Action<byte[]> onSuccess, Action<string> onFailure)
        {
            Debug.LogWarning("[SteamAuth] Using local fake ticket; real Steam validation is not covered.");
            onSuccess?.Invoke(_ticket);
        }
    }
}

