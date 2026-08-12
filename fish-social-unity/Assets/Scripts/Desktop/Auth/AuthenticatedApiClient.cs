using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace FishSocial.Desktop.Auth
{
    public interface IAuthenticatedApiClient
    {
        bool CanUse { get; }
        IEnumerator GetInventory(Action<bool, string> onCompleted);
    }

    /// <summary>
    /// Minimal authenticated REST seam for the post-Steam-login session.
    /// The access token exists only in SteamAuthController memory and is never
    /// written to PlayerPrefs, assets, logs, or error text.
    /// </summary>
    public sealed class AuthenticatedApiClient : IAuthenticatedApiClient
    {
        readonly SteamAuthController _auth;
        readonly string _baseUrl;

        public bool CanUse => _auth != null && _auth.IsAuthenticated;

        public AuthenticatedApiClient(SteamAuthController auth, string baseUrl)
        {
            _auth = auth;
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
        }

        public IEnumerator GetInventory(Action<bool, string> onCompleted)
        {
            if (!CanUse)
            {
                onCompleted?.Invoke(false, "当前没有有效的 Steam 会话。");
                yield break;
            }

            var url = _baseUrl + "/api/inventory/" + Uri.EscapeDataString(_auth.AuthenticatedPlayerId);
            using (var request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Authorization", "Bearer " + _auth.GetAccessTokenForRequest());
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success &&
                    request.responseCode >= 200 && request.responseCode < 300)
                {
                    onCompleted?.Invoke(true, "会话验证成功");
                }
                else if (request.responseCode == 401 || request.responseCode == 403)
                {
                    onCompleted?.Invoke(false, "服务端拒绝当前会话，请重新登录。");
                }
                else
                {
                    onCompleted?.Invoke(false, "无法连接 Fish Social 服务。");
                }
            }
        }
    }
}

