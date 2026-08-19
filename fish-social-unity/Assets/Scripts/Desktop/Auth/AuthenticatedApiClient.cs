using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace FishSocial.Desktop.Auth
{
    public interface IAuthenticatedApiClient
    {
        bool CanUse { get; }
        string PlayerId { get; }
        IEnumerator GetInventory(Action<bool, string> onCompleted);
        IEnumerator GetInventoryItems(Action<bool, FishInventoryItemDto[], string> onCompleted);
        IEnumerator GetCoins(Action<bool, int, string> onCompleted);
        IEnumerator GetCodex(Action<bool, FishCodexEntryDto[], string> onCompleted);
        IEnumerator GetFriends(Action<bool, FriendInfoDto[], string> onCompleted);
        IEnumerator GetFriendRequests(Action<bool, FriendRequestDto[], FriendRequestDto[], string> onCompleted);
        IEnumerator AcceptFriendRequest(string requestId, Action<bool, string> onCompleted);
        IEnumerator RejectFriendRequest(string requestId, Action<bool, string> onCompleted);
        IEnumerator RemoveFriend(string friendPlayerId, Action<bool, string> onCompleted);
        IEnumerator GetConversations(Action<bool, DmConversationDto[], string> onCompleted);
        IEnumerator GetDirectMessages(string friendPlayerId, Action<bool, DirectMessageDto[], string> onCompleted);
        IEnumerator SendDirectMessage(string toPlayerId, string fromNickname, string text, Action<bool, DirectMessageDto, string> onCompleted);
        IEnumerator SellFish(string fishId, Action<bool, int, int, FishInventoryItemDto[], string> onCompleted);
        IEnumerator ShareFish(string fishId, string nickname, Action<bool, string> onCompleted);
        IEnumerator GetShopCatalog(Action<bool, ShopBaitDto[], ShopTackleDto[], string> onCompleted);
        IEnumerator GetShopGear(Action<bool, ShopGearDto, int, string> onCompleted);
        IEnumerator BuyBait(string baitId, int quantity, Action<bool, ShopGearDto, int, string> onCompleted);
        IEnumerator BuyTackle(string tackleId, Action<bool, ShopGearDto, int, string> onCompleted);
        IEnumerator EquipBait(string baitId, Action<bool, ShopGearDto, string> onCompleted);
        IEnumerator EquipTackle(string tackleId, Action<bool, ShopGearDto, string> onCompleted);
        string BaseUrl { get; }
        IEnumerator GetPlayerProfile(Action<bool, PlayerProfileDto, string> onCompleted);
        IEnumerator UpdatePlayerProfile(
            string nickname, string bio, string avatarUrl,
            Action<bool, PlayerProfileDto, string> onCompleted);
        IEnumerator SetShowcase(string[] slots, Action<bool, PlayerProfileDto, string> onCompleted);
        IEnumerator GetSocialFeed(
            bool friendsOnly, int limit, int offset,
            Action<bool, SocialPostDto[], string> onCompleted);
        IEnumerator GetPostComments(string postId, Action<bool, PostCommentDto[], int, string> onCompleted);
        IEnumerator TogglePostLike(string postId, Action<bool, bool, int, string> onCompleted);
        IEnumerator AddPostComment(string postId, string text, Action<bool, PostCommentDto, int, string> onCompleted);
        IEnumerator DeletePostComment(string postId, string commentId, Action<bool, int, string> onCompleted);
        IEnumerator GetLeaderboard(
            string boardType, string pondId, int limit,
            Action<bool, LeaderboardEntryDto[], string, string> onCompleted);
        IEnumerator GetMyLeaderboardRank(
            string boardType, string pondId,
            Action<bool, LeaderboardMyRankDto, string> onCompleted);
    }

    /// <summary>
    /// Authenticated REST seam for Steam JWT. Token stays in SteamAuthController memory.
    /// </summary>
    public sealed class AuthenticatedApiClient : IAuthenticatedApiClient
    {
        const int TimeoutSeconds = 15;
        const string ProtocolError = "服务端响应格式错误，请重试。";
        readonly SteamAuthController _auth;
        readonly string _baseUrl;

        public bool CanUse => _auth != null && _auth.IsAuthenticated;
        public string PlayerId => _auth != null ? _auth.AuthenticatedPlayerId : null;
        public string BaseUrl => _baseUrl;

        public AuthenticatedApiClient(SteamAuthController auth, string baseUrl)
        {
            _auth = auth;
            _baseUrl = (baseUrl ?? "").TrimEnd('/');
        }

        public IEnumerator GetInventory(Action<bool, string> onCompleted)
        {
            yield return GetInventoryItems((ok, _, message) =>
                onCompleted?.Invoke(ok, ok ? "会话验证成功" : message));
        }

        public IEnumerator GetInventoryItems(Action<bool, FishInventoryItemDto[], string> onCompleted)
        {
            InventoryItemsResponse parsed = null;
            string error = null;
            var ok = false;
            yield return GetJson("/api/inventory/" + Uri.EscapeDataString(PlayerId ?? ""),
                (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (success)
                        ok = TryParseResponse(json, out parsed, out error, "items");
                });
            onCompleted?.Invoke(ok, parsed != null && parsed.items != null ? parsed.items : new FishInventoryItemDto[0], error);
        }

        public IEnumerator GetCoins(Action<bool, int, string> onCompleted)
        {
            GearResponse parsed = null;
            string error = null;
            var ok = false;
            yield return GetJson("/api/player/gear?playerId=" + Uri.EscapeDataString(PlayerId ?? ""),
                (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (success)
                        ok = TryParseResponse(json, out parsed, out error, "coins");
                });
            onCompleted?.Invoke(ok, parsed != null ? parsed.coins : 0, error);
        }

        public IEnumerator GetCodex(Action<bool, FishCodexEntryDto[], string> onCompleted)
        {
            CodexResponse parsed = null;
            string error = null;
            var ok = false;
            yield return GetJson("/api/player/codex?playerId=" + Uri.EscapeDataString(PlayerId ?? ""),
                (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (success)
                        ok = TryParseResponse(json, out parsed, out error, "entries");
                });
            onCompleted?.Invoke(ok, parsed != null && parsed.entries != null ? parsed.entries : new FishCodexEntryDto[0], error);
        }

        public IEnumerator GetFriends(Action<bool, FriendInfoDto[], string> onCompleted)
        {
            FriendsResponse parsed = null;
            string error = null;
            var ok = false;
            yield return GetJson("/api/friends/" + Uri.EscapeDataString(PlayerId ?? ""),
                (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (success)
                        ok = TryParseResponse(json, out parsed, out error, "friends");
                });
            onCompleted?.Invoke(ok, parsed != null && parsed.friends != null ? parsed.friends : new FriendInfoDto[0], error);
        }

        public IEnumerator GetFriendRequests(Action<bool, FriendRequestDto[], FriendRequestDto[], string> onCompleted)
        {
            FriendRequestsResponse parsed = null;
            string error = null;
            var ok = false;
            yield return GetJson("/api/friends/" + Uri.EscapeDataString(PlayerId ?? "") + "/requests",
                (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (success)
                        ok = TryParseResponse(json, out parsed, out error, "incoming", "outgoing");
                });
            onCompleted?.Invoke(
                ok,
                parsed != null && parsed.incoming != null ? parsed.incoming : new FriendRequestDto[0],
                parsed != null && parsed.outgoing != null ? parsed.outgoing : new FriendRequestDto[0],
                error);
        }

        public IEnumerator AcceptFriendRequest(string requestId, Action<bool, string> onCompleted)
        {
            yield return PostJson("/api/friends/accept",
                JsonUtility.ToJson(new FriendActionPayload { playerId = PlayerId, requestId = requestId }),
                (ok, _, message) => onCompleted?.Invoke(ok, message));
        }

        public IEnumerator RejectFriendRequest(string requestId, Action<bool, string> onCompleted)
        {
            yield return PostJson("/api/friends/reject",
                JsonUtility.ToJson(new FriendActionPayload { playerId = PlayerId, requestId = requestId }),
                (ok, _, message) => onCompleted?.Invoke(ok, message));
        }

        public IEnumerator RemoveFriend(string friendPlayerId, Action<bool, string> onCompleted)
        {
            yield return PostJson("/api/friends/remove",
                JsonUtility.ToJson(new RemoveFriendPayload { playerId = PlayerId, friendPlayerId = friendPlayerId }),
                (ok, _, message) => onCompleted?.Invoke(ok, message));
        }

        public IEnumerator GetConversations(Action<bool, DmConversationDto[], string> onCompleted)
        {
            ConversationsResponse parsed = null;
            string error = null;
            var ok = false;
            yield return GetJson("/api/dm/conversations/" + Uri.EscapeDataString(PlayerId ?? ""),
                (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (success)
                        ok = TryParseResponse(json, out parsed, out error, "conversations");
                });
            onCompleted?.Invoke(ok, parsed != null && parsed.conversations != null ? parsed.conversations : new DmConversationDto[0], error);
        }

        public IEnumerator GetDirectMessages(string friendPlayerId, Action<bool, DirectMessageDto[], string> onCompleted)
        {
            MessagesResponse parsed = null;
            string error = null;
            var ok = false;
            yield return GetJson(
                "/api/dm/" + Uri.EscapeDataString(PlayerId ?? "") + "/" + Uri.EscapeDataString(friendPlayerId ?? ""),
                (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (success)
                        ok = TryParseResponse(json, out parsed, out error, "messages");
                });
            onCompleted?.Invoke(ok, parsed != null && parsed.messages != null ? parsed.messages : new DirectMessageDto[0], error);
        }

        public IEnumerator SendDirectMessage(string toPlayerId, string fromNickname, string text, Action<bool, DirectMessageDto, string> onCompleted)
        {
            SendDmResponse parsed = null;
            string error = null;
            var ok = false;
            yield return PostJson("/api/dm",
                JsonUtility.ToJson(new SendDmPayload
                {
                    fromPlayerId = PlayerId,
                    fromNickname = fromNickname,
                    toPlayerId = toPlayerId,
                    text = text,
                }),
                (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (success)
                        ok = TryParseResponse(json, out parsed, out error, "message");
                });
            onCompleted?.Invoke(ok, parsed != null ? parsed.message : null, error);
        }

        public IEnumerator SellFish(string fishId, Action<bool, int, int, FishInventoryItemDto[], string> onCompleted)
        {
            SellResponse parsed = null;
            string error = null;
            var ok = false;
            yield return PostJson("/api/inventory/sell",
                JsonUtility.ToJson(new SellPayload { playerId = PlayerId, fishId = fishId }),
                (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (success)
                        ok = TryParseResponse(json, out parsed, out error,
                            "coinsEarned", "totalCoins", "items");
                });
            onCompleted?.Invoke(
                ok,
                parsed != null ? parsed.coinsEarned : 0,
                parsed != null ? parsed.totalCoins : 0,
                parsed != null && parsed.items != null ? parsed.items : new FishInventoryItemDto[0],
                error);
        }

        public IEnumerator ShareFish(string fishId, string nickname, Action<bool, string> onCompleted)
        {
            yield return PostJson("/api/posts",
                JsonUtility.ToJson(new SharePayload
                {
                    playerId = PlayerId,
                    nickname = nickname,
                    fishId = fishId,
                }),
                (ok, _, message) => onCompleted?.Invoke(ok, ok ? "已分享到动态。" : message));
        }

        public IEnumerator GetShopCatalog(
            Action<bool, ShopBaitDto[], ShopTackleDto[], string> onCompleted)
        {
            ShopBaitDto[] baits = new ShopBaitDto[0];
            ShopTackleDto[] tackles = new ShopTackleDto[0];
            string error = null;
            var ok = false;
            yield return GetJson("/api/shop/baits", (success, json, message) =>
            {
                ok = success;
                error = message;
                if (success)
                {
                    BaitCatalogResponse parsed;
                    if (!TryParseResponse(json, out parsed, out error, "baits"))
                        ok = false;
                    baits = parsed != null && parsed.baits != null
                        ? parsed.baits : new ShopBaitDto[0];
                }
            });
            if (ok)
            {
                yield return GetJson("/api/shop/tackle", (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (success)
                    {
                        TackleCatalogResponse parsed;
                        if (!TryParseResponse(json, out parsed, out error, "tackles"))
                            ok = false;
                        tackles = parsed != null && parsed.tackles != null
                            ? parsed.tackles : new ShopTackleDto[0];
                    }
                });
            }
            onCompleted?.Invoke(ok, baits, tackles, error);
        }

        public IEnumerator GetSocialFeed(
            bool friendsOnly, int limit, int offset,
            Action<bool, SocialPostDto[], string> onCompleted)
        {
            SocialPostsResponse parsed = null;
            string error = null;
            var ok = false;
            var path = friendsOnly
                ? "/api/posts/friends/" + Uri.EscapeDataString(PlayerId ?? "") +
                  "?limit=" + Mathf.Clamp(limit, 1, 50) + "&offset=" + Mathf.Max(0, offset)
                : "/api/posts/wall?limit=" + Mathf.Clamp(limit, 1, 50) +
                  "&offset=" + Mathf.Max(0, offset);
            yield return GetJson(path, (success, json, message) =>
            {
                ok = success;
                error = message;
                    if (success)
                        ok = TryParseResponse(json, out parsed, out error, "posts");
            });
            onCompleted?.Invoke(
                ok,
                parsed != null && parsed.posts != null ? parsed.posts : new SocialPostDto[0],
                error);
        }

        public IEnumerator GetPostComments(
            string postId, Action<bool, PostCommentDto[], int, string> onCompleted)
        {
            PostCommentsResponse parsed = null;
            string error = null;
            var ok = false;
            yield return GetJson(
                "/api/posts/" + Uri.EscapeDataString(postId ?? "") + "/comments",
                (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (success)
                        ok = TryParseResponse(json, out parsed, out error,
                            "comments", "commentCount");
                });
            onCompleted?.Invoke(
                ok,
                parsed != null && parsed.comments != null
                    ? parsed.comments : new PostCommentDto[0],
                parsed != null ? parsed.commentCount : 0,
                error);
        }

        public IEnumerator TogglePostLike(
            string postId, Action<bool, bool, int, string> onCompleted)
        {
            LikeResponse parsed = null;
            string error = null;
            var ok = false;
            yield return PostJson(
                "/api/posts/" + Uri.EscapeDataString(postId ?? "") + "/like",
                "{}",
                (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (success)
                        ok = TryParseResponse(json, out parsed, out error,
                            "liked", "likeCount");
                });
            onCompleted?.Invoke(ok, parsed != null && parsed.liked,
                parsed != null ? parsed.likeCount : 0, error);
        }

        public IEnumerator AddPostComment(
            string postId, string text,
            Action<bool, PostCommentDto, int, string> onCompleted)
        {
            PostCommentResponse parsed = null;
            string error = null;
            var ok = false;
            yield return PostJson(
                "/api/posts/" + Uri.EscapeDataString(postId ?? "") + "/comments",
                JsonUtility.ToJson(new CommentPayload { text = text ?? "" }),
                (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (success)
                        ok = TryParseResponse(json, out parsed, out error,
                            "comment", "commentCount");
                });
            onCompleted?.Invoke(ok, parsed != null ? parsed.comment : null,
                parsed != null ? parsed.commentCount : 0, error);
        }

        public IEnumerator DeletePostComment(
            string postId, string commentId, Action<bool, int, string> onCompleted)
        {
            DeleteCommentResponse parsed = null;
            string error = null;
            var ok = false;
            yield return DeleteJson(
                "/api/posts/" + Uri.EscapeDataString(postId ?? "") +
                "/comments/" + Uri.EscapeDataString(commentId ?? ""),
                (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (success)
                        ok = TryParseResponse(json, out parsed, out error,
                            "ok", "commentCount");
                });
            onCompleted?.Invoke(ok, parsed != null ? parsed.commentCount : 0, error);
        }

        public IEnumerator GetLeaderboard(
            string boardType, string pondId, int limit,
            Action<bool, LeaderboardEntryDto[], string, string> onCompleted)
        {
            LeaderboardListResponse parsed = null;
            string error = null;
            var ok = false;
            var safeLimit = Mathf.Clamp(limit, 1, 50);
            string path;
            if (boardType == "daily_biggest")
                path = "/api/leaderboard/daily-biggest?limit=" + safeLimit;
            else if (boardType == "weekly_king")
                path = "/api/leaderboard/weekly-king?limit=" + safeLimit;
            else if (boardType == "rare")
                path = "/api/leaderboard/rare?limit=" + safeLimit;
            else if (boardType == "pond")
            {
                if (string.IsNullOrEmpty(pondId))
                {
                    onCompleted?.Invoke(false, new LeaderboardEntryDto[0], null,
                        "请先进入鱼塘后再查看鱼塘榜。");
                    yield break;
                }
                path = "/api/leaderboard/pond/" + Uri.EscapeDataString(pondId) +
                       "?limit=" + safeLimit;
            }
            else
            {
                onCompleted?.Invoke(false, new LeaderboardEntryDto[0], null, "未知榜单类型。");
                yield break;
            }

            yield return GetJson(path, (success, json, message) =>
            {
                ok = success;
                error = message;
                if (success)
                    ok = TryParseResponse(json, out parsed, out error, "entries");
            });
            onCompleted?.Invoke(
                ok,
                parsed != null && parsed.entries != null
                    ? parsed.entries : new LeaderboardEntryDto[0],
                parsed != null ? parsed.periodKey : null,
                error);
        }

        public IEnumerator GetMyLeaderboardRank(
            string boardType, string pondId,
            Action<bool, LeaderboardMyRankDto, string> onCompleted)
        {
            LeaderboardMyRankDto parsed = null;
            string error = null;
            var ok = false;
            var path = "/api/leaderboard/my-rank?boardType=" +
                       Uri.EscapeDataString(boardType ?? "") +
                       "&limit=50";
            if (boardType == "pond" && !string.IsNullOrEmpty(pondId))
                path += "&pondId=" + Uri.EscapeDataString(pondId);

            yield return GetJson(path, (success, json, message) =>
            {
                ok = success;
                error = message;
                if (!success)
                    return;
                if (!TryParseResponse(json, out parsed, out error, "value"))
                {
                    ok = false;
                    return;
                }
                parsed.hasRank = json != null &&
                                 !System.Text.RegularExpressions.Regex.IsMatch(
                                     json, "\"rank\"\\s*:\\s*null") &&
                                 parsed.rank > 0;
            });
            onCompleted?.Invoke(ok, parsed ?? new LeaderboardMyRankDto(), error);
        }
        public IEnumerator GetShopGear(Action<bool, ShopGearDto, int, string> onCompleted)
        {
            ShopGearDto gear = null;
            var coins = 0;
            string error = null;
            var ok = false;
            yield return GetJson(
                "/api/player/gear?playerId=" + Uri.EscapeDataString(PlayerId ?? ""),
                (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (!success)
                        return;
                    GearEnvelope parsed;
                    if (!TryParseResponse(json, out parsed, out error, "gear", "coins"))
                        ok = false;
                    gear = parsed != null ? parsed.gear : null;
                    coins = parsed != null ? parsed.coins : 0;
                    ParseBaitInventory(json, gear);
                });
            onCompleted?.Invoke(ok, gear ?? new ShopGearDto(), coins, error);
        }

        public IEnumerator BuyBait(
            string baitId, int quantity,
            Action<bool, ShopGearDto, int, string> onCompleted)
        {
            yield return ShopMutation(
                "/api/shop/baits/buy",
                JsonUtility.ToJson(new BuyBaitPayload
                {
                    playerId = PlayerId,
                    baitId = baitId,
                    quantity = quantity,
                }),
                onCompleted);
        }

        public IEnumerator BuyTackle(
            string tackleId,
            Action<bool, ShopGearDto, int, string> onCompleted)
        {
            yield return ShopMutation(
                "/api/shop/tackle/buy",
                JsonUtility.ToJson(new BuyTacklePayload
                {
                    playerId = PlayerId,
                    tackleId = tackleId,
                }),
                onCompleted);
        }

        public IEnumerator EquipBait(
            string baitId, Action<bool, ShopGearDto, string> onCompleted)
        {
            yield return EquipMutation(
                "/api/player/equip/bait",
                JsonUtility.ToJson(new EquipPayload { playerId = PlayerId, baitId = baitId }),
                onCompleted);
        }

        public IEnumerator EquipTackle(
            string tackleId, Action<bool, ShopGearDto, string> onCompleted)
        {
            yield return EquipMutation(
                "/api/player/equip/tackle",
                JsonUtility.ToJson(new EquipPayload { playerId = PlayerId, tackleId = tackleId }),
                onCompleted);
        }

        public IEnumerator GetPlayerProfile(Action<bool, PlayerProfileDto, string> onCompleted)
        {
            PlayerProfileDto profile = null;
            string error = null;
            var ok = false;
            yield return GetJson(
                "/api/players/" + Uri.EscapeDataString(PlayerId ?? ""),
                (success, json, message) =>
                {
                    ok = success;
                    error = message;
                    if (success)
                        ok = TryParsePlayerProfile(json, out profile, out error);
                });
            onCompleted?.Invoke(ok, profile, error);
        }

        public IEnumerator UpdatePlayerProfile(
            string nickname, string bio, string avatarUrl,
            Action<bool, PlayerProfileDto, string> onCompleted)
        {
            var body = "{\"nickname\":" + Quote(nickname) +
                       ",\"bio\":" + Quote(bio ?? "");
            if (!string.IsNullOrEmpty(avatarUrl))
                body += ",\"avatarUrl\":" + Quote(avatarUrl);
            body += "}";
            yield return MutateProfile(
                "/api/players/" + Uri.EscapeDataString(PlayerId ?? "") + "/profile",
                body, onCompleted);
        }

        public IEnumerator SetShowcase(
            string[] slots, Action<bool, PlayerProfileDto, string> onCompleted)
        {
            yield return MutateProfile(
                "/api/players/" + Uri.EscapeDataString(PlayerId ?? "") + "/showcase",
                BuildShowcaseBody(slots), onCompleted);
        }

        IEnumerator MutateProfile(
            string path, string body, Action<bool, PlayerProfileDto, string> onCompleted)
        {
            PlayerProfileDto profile = null;
            string error = null;
            var ok = false;
            yield return PutJson(path, body, (success, json, message) =>
            {
                ok = success;
                error = message;
                if (success)
                    ok = TryParsePlayerProfile(json, out profile, out error);
            });
            onCompleted?.Invoke(ok, profile, error);
        }

        IEnumerator ShopMutation(
            string path, string body,
            Action<bool, ShopGearDto, int, string> onCompleted)
        {
            ShopMutationResponse parsed = null;
            string raw = null;
            string error = null;
            var ok = false;
            yield return PostJson(path, body, (success, json, message) =>
            {
                ok = success;
                raw = json;
                error = message;
                if (success)
                    ok = TryParseResponse(json, out parsed, out error, "gear", "coins");
            }, Guid.NewGuid().ToString("N"));
            var gear = parsed != null ? parsed.gear : null;
            ParseBaitInventory(raw, gear);
            onCompleted?.Invoke(ok, gear ?? new ShopGearDto(),
                parsed != null ? parsed.coins : 0, error);
        }

        IEnumerator EquipMutation(
            string path, string body,
            Action<bool, ShopGearDto, string> onCompleted)
        {
            ShopGearResponse parsed = null;
            string raw = null;
            string error = null;
            var ok = false;
            yield return PostJson(path, body, (success, json, message) =>
            {
                ok = success;
                raw = json;
                error = message;
                if (success)
                    ok = TryParseResponse(json, out parsed, out error, "gear");
            });
            var gear = parsed != null ? parsed.gear : null;
            ParseBaitInventory(raw, gear);
            onCompleted?.Invoke(ok, gear ?? new ShopGearDto(), error);
        }

        IEnumerator GetJson(string path, Action<bool, string, string> onCompleted)
        {
            if (!CanUse)
            {
                onCompleted?.Invoke(false, null, "当前没有有效的 Steam 会话。");
                yield break;
            }

            using (var request = UnityWebRequest.Get(_baseUrl + path))
            {
                request.timeout = TimeoutSeconds;
                request.SetRequestHeader("Authorization", "Bearer " + _auth.GetAccessTokenForRequest());
                yield return request.SendWebRequest();
                Complete(request, onCompleted);
            }
        }

        IEnumerator PutJson(string path, string body, Action<bool, string, string> onCompleted)
        {
            if (!CanUse)
            {
                onCompleted?.Invoke(false, null, "当前没有有效的 Steam 会话。");
                yield break;
            }

            using (var request = new UnityWebRequest(_baseUrl + path, "PUT"))
            {
                request.timeout = TimeoutSeconds;
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body ?? "{}"));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + _auth.GetAccessTokenForRequest());
                yield return request.SendWebRequest();
                Complete(request, onCompleted);
            }
        }

        IEnumerator DeleteJson(string path, Action<bool, string, string> onCompleted)
        {
            if (!CanUse)
            {
                onCompleted?.Invoke(false, null, "当前没有有效的 Steam 会话。");
                yield break;
            }

            using (var request = UnityWebRequest.Delete(_baseUrl + path))
            {
                request.timeout = TimeoutSeconds;
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization", "Bearer " + _auth.GetAccessTokenForRequest());
                yield return request.SendWebRequest();
                Complete(request, onCompleted);
            }
        }

        IEnumerator PostJson(
            string path, string body, Action<bool, string, string> onCompleted,
            string idempotencyKey = null)
        {
            if (!CanUse)
            {
                onCompleted?.Invoke(false, null, "当前没有有效的 Steam 会话。");
                yield break;
            }

            using (var request = new UnityWebRequest(_baseUrl + path, "POST"))
            {
                request.timeout = TimeoutSeconds;
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body ?? "{}"));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + _auth.GetAccessTokenForRequest());
                if (!string.IsNullOrEmpty(idempotencyKey))
                    request.SetRequestHeader("X-Idempotency-Key", idempotencyKey);
                yield return request.SendWebRequest();
                Complete(request, onCompleted);
            }
        }

        static void Complete(UnityWebRequest request, Action<bool, string, string> onCompleted)
        {
            if (request.result == UnityWebRequest.Result.Success &&
                request.responseCode >= 200 && request.responseCode < 300)
            {
                onCompleted?.Invoke(true, request.downloadHandler != null ? request.downloadHandler.text : "{}", "成功");
                return;
            }

            onCompleted?.Invoke(false, null, ReadError(request));
        }

        static string ReadError(UnityWebRequest request)
        {
            if (request.responseCode == 401 || request.responseCode == 403)
                return "服务端拒绝当前会话（" + request.responseCode + "），请重新登录。";
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
                return "无法连接 Fish Social 服务，请检查网络后重试。";
            if (request.responseCode == 0)
                return "请求超时或服务不可达，请稍后重试。";

            ApiErrorResponse parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<ApiErrorResponse>(
                    request.downloadHandler == null ? string.Empty : request.downloadHandler.text);
            }
            catch
            {
            }

            if (parsed != null && !string.IsNullOrEmpty(parsed.error))
                return parsed.error;
            return "请求失败（HTTP " + request.responseCode + "）。";
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
                Debug.LogWarning("[AuthenticatedApi] Empty successful response.");
                error = ProtocolError;
                return false;
            }

            try
            {
                parsed = JsonUtility.FromJson<T>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[AuthenticatedApi] Invalid JSON response: " + exception.Message);
                error = ProtocolError;
                return false;
            }

            if (parsed == null)
            {
                Debug.LogWarning("[AuthenticatedApi] Response deserialized to null.");
                error = ProtocolError;
                return false;
            }

            for (var i = 0; i < requiredFields.Length; i++)
            {
                var field = requiredFields[i];
                if (string.IsNullOrEmpty(field) ||
                    !System.Text.RegularExpressions.Regex.IsMatch(
                        json,
                        "\"" + System.Text.RegularExpressions.Regex.Escape(field) +
                        "\"\\s*:"))
                {
                    Debug.LogWarning("[AuthenticatedApi] Missing response field: " + field);
                    error = ProtocolError;
                    return false;
                }
            }
            return true;
        }

        static bool TryParsePlayerProfile(
            string json,
            out PlayerProfileDto profile,
            out string error)
        {
            profile = null;
            ProfileEnvelope envelope;
            if (!TryParseResponse(json, out envelope, out error, "profile") ||
                envelope.profile == null)
            {
                Debug.LogWarning("[AuthenticatedApi] Missing profile envelope.");
                error = ProtocolError;
                return false;
            }

            profile = envelope.profile;
            if (string.IsNullOrEmpty(profile.playerId) ||
                string.IsNullOrEmpty(profile.nickname))
            {
                profile = null;
                Debug.LogWarning("[AuthenticatedApi] Profile missing playerId or nickname.");
                error = ProtocolError;
                return false;
            }
            profile.showcaseFishIds = NormalizeShowcase(profile.showcaseFishIds);
            if (profile.avatarUrl == null)
                profile.avatarUrl = string.Empty;
            if (profile.bio == null)
                profile.bio = string.Empty;
            return true;
        }

        static void ParseBaitInventory(string json, ShopGearDto gear)
        {
            if (gear == null || string.IsNullOrEmpty(json))
                return;
            gear.basic = ExtractInt(json, "basic");
            gear.corn = ExtractInt(json, "corn");
            gear.pellet = ExtractInt(json, "pellet");
            gear.live = ExtractInt(json, "live");
        }

        static int ExtractInt(string json, string key)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                json, "\"" + key + "\"\\s*:\\s*(-?\\d+)");
            int value;
            return match.Success && int.TryParse(match.Groups[1].Value, out value)
                ? value : 0;
        }

        static string[] NormalizeShowcase(string[] source)
        {
            var slots = new string[PlayerProfileDto.ShowcaseSlotCount];
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = source != null && i < source.Length && !string.IsNullOrEmpty(source[i])
                    ? source[i]
                    : string.Empty;
            }
            return slots;
        }

        static string BuildShowcaseBody(string[] slots)
        {
            var normalized = NormalizeShowcase(slots);
            var builder = new StringBuilder("{\"slots\":[");
            for (var i = 0; i < normalized.Length; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append(string.IsNullOrEmpty(normalized[i])
                    ? "null"
                    : Quote(normalized[i]));
            }
            builder.Append("]}");
            return builder.ToString();
        }

        static string Quote(string value)
        {
            return JsonUtility.ToJson(new StringValue { value = value ?? "" })
                .Replace("{\"value\":", "")
                .TrimEnd('}');
        }

        #pragma warning disable 0649
        [Serializable] sealed class InventoryItemsResponse { public FishInventoryItemDto[] items; }
        [Serializable] sealed class GearResponse { public int coins; }
        [Serializable] sealed class CodexResponse { public FishCodexEntryDto[] entries; }
        [Serializable] sealed class FriendsResponse { public FriendInfoDto[] friends; }
        [Serializable] sealed class FriendRequestsResponse { public FriendRequestDto[] incoming; public FriendRequestDto[] outgoing; }
        [Serializable] sealed class ConversationsResponse { public DmConversationDto[] conversations; }
        [Serializable] sealed class MessagesResponse { public DirectMessageDto[] messages; }
        [Serializable] sealed class SendDmResponse { public DirectMessageDto message; }
        [Serializable] sealed class SellResponse { public int coinsEarned; public int totalCoins; public FishInventoryItemDto[] items; }
        [Serializable] sealed class FriendActionPayload { public string playerId; public string requestId; }
        [Serializable] sealed class RemoveFriendPayload { public string playerId; public string friendPlayerId; }
        [Serializable] sealed class SendDmPayload { public string fromPlayerId; public string fromNickname; public string toPlayerId; public string text; }
        [Serializable] sealed class SellPayload { public string playerId; public string fishId; }
        [Serializable] sealed class SharePayload { public string playerId; public string nickname; public string fishId; }
        [Serializable] sealed class BaitCatalogResponse { public ShopBaitDto[] baits; }
        [Serializable] sealed class TackleCatalogResponse { public ShopTackleDto[] tackles; }
        [Serializable] sealed class GearEnvelope { public ShopGearDto gear; public int coins; }
        [Serializable] sealed class ShopMutationResponse { public ShopGearDto gear; public int coins; }
        [Serializable] sealed class ShopGearResponse { public ShopGearDto gear; }
        [Serializable] sealed class BuyBaitPayload { public string playerId; public string baitId; public int quantity; }
        [Serializable] sealed class BuyTacklePayload { public string playerId; public string tackleId; }
        [Serializable] sealed class EquipPayload { public string playerId; public string baitId; public string tackleId; }
        [Serializable] sealed class ApiErrorResponse { public string error; }
        [Serializable] sealed class ProfileEnvelope { public PlayerProfileDto profile; }
        [Serializable] sealed class SocialPostsResponse { public SocialPostDto[] posts; }
        [Serializable] sealed class PostCommentsResponse { public PostCommentDto[] comments; public int commentCount; }
        [Serializable] sealed class LikeResponse { public bool liked; public int likeCount; }
        [Serializable] sealed class PostCommentResponse { public PostCommentDto comment; public int commentCount; }
        [Serializable] sealed class DeleteCommentResponse { public bool ok; public int commentCount; }
        [Serializable] sealed class CommentPayload { public string text; }
        [Serializable] sealed class StringValue { public string value; }
        [Serializable] sealed class LeaderboardListResponse
        {
            public LeaderboardEntryDto[] entries;
            public string periodKey;
        }
        #pragma warning restore 0649
    }
}
