using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace FishSocial.Desktop.Auth
{
    public enum SteamLoginState
    {
        SignedOut,
        Initializing,
        RequestingTicket,
        Authenticating,
        Authenticated,
        Failed
    }

    public enum SteamLoginError
    {
        None,
        SteamNotRunning,
        MissingTicket,
        InvalidAppId,
        InvalidTicket,
        RateLimited,
        ServerRejected,
        ServerUnavailable,
        AuthDisabled,
        Unknown
    }

    [Serializable]
    public sealed class SteamAuthResponse
    {
        public bool ok;
        public string playerId;
        public string accessToken;
        public bool created;
        public string code;
        public string error;
    }

    /// <summary>
    /// Narrow seam for Steamworks. The real adapter can be supplied when the
    /// Steamworks Unity package is installed; this project intentionally does
    /// not contain Steam Web API credentials or a fake production fallback.
    /// </summary>
    public interface ISteamTicketProvider
    {
        bool IsSteamRunning { get; }
        void RequestTicket(string identity, Action<byte[]> onSuccess, Action<string> onFailure);
    }

    public sealed class SteamAuthController : MonoBehaviour
    {
        [SerializeField] string serverBaseUrl = "http://localhost:3001";
        [SerializeField] string steamAppId = "";
        [SerializeField] string steamAuthIdentity = "fish-social-server-v1";

        public SteamLoginState State { get; private set; } = SteamLoginState.SignedOut;
        public SteamLoginError LastError { get; private set; } = SteamLoginError.None;
        public string PlayerId { get; private set; }
        public string AccessToken { get; private set; }
        public string AuthenticatedPlayerId => IsAuthenticated ? PlayerId : null;
        public string ServerBaseUrl => serverBaseUrl;
        public bool IsAuthenticated => State == SteamLoginState.Authenticated &&
                                       !string.IsNullOrEmpty(AccessToken);
        public event Action<SteamLoginState> StateChanged;
        public event Action<string> ErrorMessage;

        ISteamTicketProvider _ticketProvider;
        Coroutine _loginRoutine;

        public void Configure(
            ISteamTicketProvider ticketProvider,
            string appId,
            string authIdentity = "fish-social-server-v1",
            string resolvedServerBaseUrl = null)
        {
            _ticketProvider = ticketProvider;
            steamAppId = appId ?? "";
            steamAuthIdentity = string.IsNullOrWhiteSpace(authIdentity)
                ? "fish-social-server-v1"
                : authIdentity;
            if (FishSocial.Desktop.DesktopServerConfig.TryNormalize(resolvedServerBaseUrl, out var normalized))
                serverBaseUrl = normalized;
            else if (FishSocial.Desktop.DesktopServerConfig.TryNormalize(serverBaseUrl, out var existing))
                serverBaseUrl = existing;
            else
                serverBaseUrl = FishSocial.Desktop.DesktopServerConfig.DefaultServerBaseUrl;
            SetState(SteamLoginState.SignedOut);
        }

        public void BeginLogin()
        {
            Debug.Log("[SteamAuth] BeginLogin started.");
            if (_loginRoutine != null)
                StopCoroutine(_loginRoutine);
            _loginRoutine = StartCoroutine(LoginRoutine());
        }

        public void SignOut()
        {
            if (_loginRoutine != null)
                StopCoroutine(_loginRoutine);
            PlayerId = null;
            AccessToken = null;
            LastError = SteamLoginError.None;
            SetState(SteamLoginState.SignedOut);
        }

        public IAuthenticatedApiClient CreateAuthenticatedApiClient()
        {
            return new AuthenticatedApiClient(this, serverBaseUrl);
        }

        public ISocialSocketClient CreateSocialSocketClient()
        {
            return new SocketIoSocialSocketClient(serverBaseUrl);
        }

        internal string GetAccessTokenForRequest()
        {
            return IsAuthenticated ? AccessToken : null;
        }

        internal string GetAccessTokenForSession()
        {
            return GetAccessTokenForRequest();
        }

        IEnumerator LoginRoutine()
        {
            LastError = SteamLoginError.None;
            Debug.Log("[SteamAuth] LoginRoutine started. provider=" +
                      (_ticketProvider == null ? "null" : _ticketProvider.GetType().Name) +
                      " appId=" + steamAppId);
            if (_ticketProvider == null || !_ticketProvider.IsSteamRunning)
            {
                Debug.LogWarning("[SteamAuth] LoginRoutine stopped: Steam provider is unavailable.");
                Fail(SteamLoginError.SteamNotRunning, "请先启动 Steam 客户端。");
                yield break;
            }
            if (string.IsNullOrWhiteSpace(steamAppId))
            {
                Fail(SteamLoginError.InvalidAppId, "游戏尚未配置 Steam App ID。");
                yield break;
            }

            SetState(SteamLoginState.Initializing);
            byte[] ticket = null;
            string ticketError = null;
            bool finished = false;
            Debug.Log("[SteamAuth] Requesting Steam Web API ticket. identity=" + steamAuthIdentity);
            _ticketProvider.RequestTicket(
                steamAuthIdentity,
                bytes => { ticket = bytes; finished = true; },
                error => { ticketError = error; finished = true; });
            SetState(SteamLoginState.RequestingTicket);
            float ticketDeadline = Time.unscaledTime + 15f;
            while (!finished)
            {
                if (Time.unscaledTime >= ticketDeadline)
                {
                    Fail(SteamLoginError.InvalidTicket, "Steam 登录票据获取超时，请确认 Steam 客户端已启动。");
                    yield break;
                }
                yield return null;
            }
            if (ticket == null || ticket.Length == 0)
            {
                Debug.LogWarning("[SteamAuth] Steam ticket request failed: " + ticketError);
                Fail(SteamLoginError.MissingTicket, string.IsNullOrEmpty(ticketError)
                    ? "无法从 Steam 获取登录票据。"
                    : ticketError);
                yield break;
            }

            SetState(SteamLoginState.Authenticating);
            Debug.Log("[SteamAuth] Steam ticket received. bytes=" + ticket.Length +
                      "; authenticating against " + serverBaseUrl);
            var payload = new SteamLoginRequest
            {
                ticket = BytesToHex(ticket),
                appId = steamAppId,
                identity = steamAuthIdentity
            };
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            using (var request = new UnityWebRequest(serverBaseUrl.TrimEnd('/') + "/api/auth/steam", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    Debug.LogWarning("[SteamAuth] Authentication request connection error: " + request.error);
                    Fail(SteamLoginError.ServerUnavailable, "无法连接 Fish Social 服务，请稍后重试。");
                    yield break;
                }

                SteamAuthResponse response;
                try
                {
                    response = JsonUtility.FromJson<SteamAuthResponse>(request.downloadHandler.text);
                }
                catch
                {
                    Fail(SteamLoginError.Unknown, "服务器返回了无法识别的登录结果。");
                    yield break;
                }
                if (request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogWarning("[SteamAuth] Authentication request rejected. code=" +
                                     (response == null ? "null" : response.code));
                    Fail(MapError(response == null ? null : response.code),
                        UserMessage(response == null ? null : response.code,
                            response == null ? "服务器拒绝了 Steam 登录。" : response.error));
                    yield break;
                }
                if (response == null || !response.ok || string.IsNullOrEmpty(response.accessToken))
                {
                    Fail(MapError(response == null ? null : response.code),
                        UserMessage(response == null ? null : response.code, response == null ? null : response.error));
                    yield break;
                }
                PlayerId = response.playerId;
                AccessToken = response.accessToken;
                Debug.Log("[SteamAuth] Authentication succeeded. playerId=" + PlayerId);
                SetState(SteamLoginState.Authenticated);
            }
        }

        static string BytesToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
                builder.Append(value.ToString("x2"));
            return builder.ToString();
        }

        void Fail(SteamLoginError error, string message)
        {
            LastError = error;
            SetState(SteamLoginState.Failed);
            ErrorMessage?.Invoke(message);
        }

        void SetState(SteamLoginState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        static SteamLoginError MapError(string code)
        {
            switch (code)
            {
                case "STEAM_AUTH_DISABLED": return SteamLoginError.AuthDisabled;
                case "STEAM_INVALID_APP_ID": return SteamLoginError.InvalidAppId;
                case "STEAM_INVALID_IDENTITY": return SteamLoginError.ServerRejected;
                case "STEAM_MISSING_TICKET": return SteamLoginError.MissingTicket;
                case "STEAM_TICKET_INVALID": return SteamLoginError.InvalidTicket;
                case "STEAM_RATE_LIMITED": return SteamLoginError.RateLimited;
                default: return SteamLoginError.ServerRejected;
            }
        }

        static string UserMessage(string code, string fallback)
        {
            switch (code)
            {
                case "STEAM_AUTH_DISABLED": return "Steam 登录服务当前未启用。";
                case "STEAM_INVALID_APP_ID": return "游戏版本与 Steam App ID 不匹配。";
                case "STEAM_INVALID_IDENTITY": return "Steam 登录配置不匹配，请更新客户端。";
                case "STEAM_TICKET_INVALID": return "Steam 登录票据已失效，请重试。";
                case "STEAM_RATE_LIMITED": return "登录请求过于频繁，请稍后再试。";
                default: return string.IsNullOrEmpty(fallback) ? "Steam 登录被服务器拒绝。" : fallback;
            }
        }

        [Serializable]
        sealed class SteamLoginRequest
        {
            public string ticket;
            public string appId;
            public string identity;
        }
    }
}

