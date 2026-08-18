using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop.Social
{
    #pragma warning disable 0649
    [Serializable]
    public sealed class SocialLobbyDto
    {
        public string lobbyId;
        public string ownerPlayerId;
        public string pondId;
        public string gameVersion;
        public string protocolVersion;
        public long createdAt;
    }

    [Serializable]
    sealed class SocialLobbyResponse
    {
        public bool ok;
        public string error;
        public string code;
        public SocialLobbyDto lobby;
    }

    [Serializable]
    sealed class SocialLobbyErrorResponse
    {
        public bool ok;
        public string code;
        public string error;
    }

    public sealed class SocialLobbyApiClient
    {
        readonly SteamAuthController _auth;
        readonly string _baseUrl;

        public bool CanUse => _auth != null && _auth.IsAuthenticated;

        public SocialLobbyApiClient(SteamAuthController auth, string baseUrl)
        {
            _auth = auth;
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
        }

        public IEnumerator Create(
            string lobbyId,
            string pondId,
            string gameVersion,
            string protocolVersion,
            Action<bool, SocialLobbyDto, string> completed)
        {
            yield return Send(
                "/api/social/lobby/create",
                lobbyId,
                pondId,
                gameVersion,
                protocolVersion,
                null,
                completed);
        }

        public IEnumerator Join(
            string lobbyId,
            string gameVersion,
            string protocolVersion,
            string inviteToken,
            Action<bool, SocialLobbyDto, string> completed)
        {
            yield return Send(
                "/api/social/lobby/join",
                lobbyId,
                null,
                gameVersion,
                protocolVersion,
                inviteToken,
                completed);
        }

        public IEnumerator Invite(
            string lobbyId,
            string friendSteamId64,
            Action<bool, string> completed)
        {
            if (!CanUse)
            {
                completed?.Invoke(false, "请先完成 Steam 登录。");
                yield break;
            }
            var body = JsonUtility.ToJson(new LobbyInvitePayload
            {
                lobbyId = lobbyId,
                friendSteamId64 = friendSteamId64,
            });
            using (var request = CreateRequest("/api/social/lobby/invite", body))
            {
                yield return request.SendWebRequest();
                if (IsSuccess(request))
                {
                    SocialLobbyInviteResponse response;
                    string error;
                    var ok = TryParseResponse(
                        request.downloadHandler == null ? null : request.downloadHandler.text,
                        out response, out error, "inviteToken");
                    completed?.Invoke(ok, ok ? response.inviteToken : error);
                }
                else
                    completed?.Invoke(false, ReadError(request));
            }
        }

        public IEnumerator Close(string lobbyId, Action<bool, string> completed)
        {
            if (!CanUse)
            {
                completed?.Invoke(false, "请先完成 Steam 登录。");
                yield break;
            }
            var body = JsonUtility.ToJson(new LobbyClosePayload { lobbyId = lobbyId });
            using (var request = CreateRequest("/api/social/lobby/close", body))
            {
                yield return request.SendWebRequest();
                if (IsSuccess(request))
                    completed?.Invoke(true, "Lobby 已关闭。");
                else
                    completed?.Invoke(false, ReadError(request));
            }
        }

        IEnumerator Send(
            string path,
            string lobbyId,
            string pondId,
            string gameVersion,
            string protocolVersion,
            string inviteToken,
            Action<bool, SocialLobbyDto, string> completed)
        {
            if (!CanUse)
            {
                completed?.Invoke(false, null, "请先完成 Steam 登录。");
                yield break;
            }
            var body = JsonUtility.ToJson(new LobbyPayload
            {
                lobbyId = lobbyId,
                pondId = pondId,
                gameVersion = gameVersion,
                protocolVersion = protocolVersion,
                inviteToken = inviteToken,
            });
            using (var request = CreateRequest(path, body))
            {
                yield return request.SendWebRequest();
                if (IsSuccess(request))
                {
                    SocialLobbyResponse response;
                    string error;
                    var ok = TryParseResponse(
                        request.downloadHandler == null ? null : request.downloadHandler.text,
                        out response, out error, "lobby");
                    if (ok && response.lobby != null &&
                        !string.IsNullOrEmpty(response.lobby.lobbyId) &&
                        !string.IsNullOrEmpty(response.lobby.pondId))
                    {
                        completed?.Invoke(true, response.lobby, "Lobby 权限校验通过。");
                    }
                    else
                    {
                        completed?.Invoke(false, null,
                            ok ? "服务端 Lobby 响应缺少 lobbyId 或 pondId。" : error);
                    }
                }
                else
                {
                    completed?.Invoke(false, null, ReadError(request));
                }
            }
        }

        UnityWebRequest CreateRequest(string path, string body)
        {
            var request = new UnityWebRequest(_baseUrl + path, "POST");
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + _auth.GetAccessTokenForRequest());
            return request;
        }

        static bool IsSuccess(UnityWebRequest request)
        {
            return request.result == UnityWebRequest.Result.Success &&
                   request.responseCode >= 200 && request.responseCode < 300;
        }

        static string ReadError(UnityWebRequest request)
        {
            SocialLobbyErrorResponse response = null;
            try
            {
                response = JsonUtility.FromJson<SocialLobbyErrorResponse>(
                    request.downloadHandler == null ? string.Empty : request.downloadHandler.text);
            }
            catch
            {
                // Keep the user-facing fallback stable for malformed responses.
            }

            var code = response == null ? null : response.code;
            if (!string.IsNullOrEmpty(code))
                Debug.LogWarning("[SocialLobby] request rejected code=" + code);

            switch (code)
            {
                case "LOBBY_STEAM_BINDING_REQUIRED":
                    return "当前 Steam 账号尚未完成服务端绑定，请重新登录 Steam。";
                case "LOBBY_ID_INVALID":
                    return "Steam Lobby ID 无效，请重新创建 Lobby。";
                case "POND_NOT_FOUND":
                    return "选择的鱼塘不存在，请重新选择。";
                case "LOBBY_GAME_VERSION_MISMATCH":
                case "LOBBY_PROTOCOL_VERSION_MISMATCH":
                    return "Lobby 版本不兼容，请更新客户端后重试。";
                case "LOBBY_OWNER_REQUIRED":
                    return "只有 Lobby 创建者可以执行此操作。";
                case "LOBBY_CACHE_MISSING":
                    return "Lobby 已失效，请重新创建；鱼塘仍可通过其他入口进入。";
                case "LOBBY_INVITE_INVALID":
                    return "Lobby 邀请无效或已过期，请重新邀请。";
            }

            if (request.responseCode == 401 || request.responseCode == 403)
                return "服务端拒绝当前 Lobby 操作，请重新登录后重试。";
            if (request.responseCode == 404)
                return "Lobby 或鱼塘不存在，请重新创建。";
            if (request.responseCode == 409)
                return "Lobby 版本不兼容，请更新客户端后重试。";
            return "无法连接 Lobby 权限服务。";
        }

        static bool TryParseResponse<T>(
            string json,
            out T parsed,
            out string error,
            params string[] requiredFields)
            where T : class
        {
            parsed = null;
            error = null;
            if (string.IsNullOrEmpty(json))
            {
                error = "服务端响应为空。";
                return false;
            }

            try
            {
                parsed = JsonUtility.FromJson<T>(json);
            }
            catch (Exception exception)
            {
                error = "服务端响应格式错误：" + exception.Message;
                return false;
            }
            if (parsed == null)
            {
                error = "服务端响应格式错误。";
                return false;
            }
            for (var i = 0; i < requiredFields.Length; i++)
            {
                var field = requiredFields[i];
                if (!System.Text.RegularExpressions.Regex.IsMatch(
                    json,
                    "\"" + System.Text.RegularExpressions.Regex.Escape(field) +
                    "\"\\s*:"))
                {
                    error = "服务端响应缺少字段：" + field;
                    return false;
                }
            }
            return true;
        }

        [Serializable]
        sealed class LobbyPayload
        {
            public string lobbyId;
            public string pondId;
            public string gameVersion;
            public string protocolVersion;
            public string inviteToken;
        }

        [Serializable]
        sealed class LobbyClosePayload
        {
            public string lobbyId;
        }

        [Serializable]
        sealed class LobbyInvitePayload
        {
            public string lobbyId;
            public string friendSteamId64;
        }

        [Serializable]
        sealed class SocialLobbyInviteResponse
        {
            public bool ok;
            public string inviteToken;
        }
        #pragma warning restore 0649
    }
}
