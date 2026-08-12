using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop.Social
{
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
                completed);
        }

        public IEnumerator Join(
            string lobbyId,
            string gameVersion,
            string protocolVersion,
            Action<bool, SocialLobbyDto, string> completed)
        {
            yield return Send(
                "/api/social/lobby/join",
                lobbyId,
                null,
                gameVersion,
                protocolVersion,
                completed);
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
            });
            using (var request = CreateRequest(path, body))
            {
                yield return request.SendWebRequest();
                if (IsSuccess(request))
                {
                    var response = JsonUtility.FromJson<SocialLobbyResponse>(request.downloadHandler.text);
                    completed?.Invoke(true, response?.lobby, "Lobby 权限校验通过。");
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
            if (request.responseCode == 401 || request.responseCode == 403)
                return "服务端拒绝当前 Lobby 权限。";
            if (request.responseCode == 404)
                return "Lobby 不存在或已失效。";
            if (request.responseCode == 409)
                return "Lobby 版本不兼容。";
            return "无法连接 Lobby 权限服务。";
        }

        [Serializable]
        sealed class LobbyPayload
        {
            public string lobbyId;
            public string pondId;
            public string gameVersion;
            public string protocolVersion;
        }

        [Serializable]
        sealed class LobbyClosePayload
        {
            public string lobbyId;
        }
    }
}
