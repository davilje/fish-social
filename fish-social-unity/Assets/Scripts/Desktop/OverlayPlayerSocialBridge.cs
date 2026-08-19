using System.Collections;
using UnityEngine;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Handles Overlay player context-menu commands without leaving pond or rebuilding Overlay.
    /// </summary>
    public sealed class OverlayPlayerSocialBridge
    {
        readonly MonoBehaviour _runner;
        readonly IAuthenticatedApiClient _api;
        readonly SocialPondSessionController _pond;
        readonly DesktopShellUi _shell;
        readonly System.Action _publishState;
        readonly System.Action<string> _setError;

        public OverlayPlayerSocialBridge(
            MonoBehaviour runner,
            IAuthenticatedApiClient api,
            SocialPondSessionController pond,
            DesktopShellUi shell,
            System.Action publishState,
            System.Action<string> setError)
        {
            _runner = runner;
            _api = api;
            _pond = pond;
            _shell = shell;
            _publishState = publishState;
            _setError = setError;
        }

        public void Handle(NativeOverlayCommandDto message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.playerId))
            {
                _setError?.Invoke("缺少目标玩家。");
                _publishState?.Invoke();
                return;
            }

            switch (message.command)
            {
                case "player_open_profile":
                    _setError?.Invoke(string.Empty);
                    _publishState?.Invoke();
                    _shell?.OpenOtherPlayerProfile(message.playerId);
                    return;
                case "player_add_friend":
                    _runner.StartCoroutine(AddFriendRoutine(message.playerId));
                    return;
                case "player_open_dm":
                    _runner.StartCoroutine(OpenDmRoutine(message.playerId));
                    return;
                case "player_like_recent":
                    _runner.StartCoroutine(LikeRecentRoutine(message.playerId));
                    return;
            }
        }

        IEnumerator AddFriendRoutine(string playerId)
        {
            if (_api == null || !_api.CanUse)
            {
                Fail("当前没有有效的 Steam 会话，请重新登录。");
                yield break;
            }

            if (IsBot(playerId))
            {
                Fail("暂不支持向机器人发送好友请求。");
                yield break;
            }

            if (playerId == _api.PlayerId)
            {
                Fail("不能添加自己为好友。");
                yield break;
            }

            var friendsDone = false;
            var friendsOk = false;
            FriendInfoDto[] friends = null;
            yield return _api.GetFriends((ok, loaded, _) =>
            {
                friendsOk = ok;
                friends = loaded;
                friendsDone = true;
            });
            while (!friendsDone)
                yield return null;

            if (friendsOk && ContainsFriend(friends, playerId))
            {
                Fail("你们已经是好友了。");
                yield break;
            }

            var requestsDone = false;
            var requestsOk = false;
            FriendRequestDto[] outgoing = null;
            yield return _api.GetFriendRequests((ok, _, loaded, _) =>
            {
                requestsOk = ok;
                outgoing = loaded;
                requestsDone = true;
            });
            while (!requestsDone)
                yield return null;

            if (requestsOk && HasOutgoingRequest(outgoing, playerId))
            {
                Fail("好友请求已发送，请等待对方确认。");
                yield break;
            }

            var requestDone = false;
            var requestOk = false;
            string error = null;
            yield return _api.SendFriendRequest(
                playerId,
                _pond != null ? _pond.Nickname : "钓友",
                (ok, message) =>
                {
                    requestOk = ok;
                    error = message;
                    requestDone = true;
                });
            while (!requestDone)
                yield return null;

            if (!requestOk)
            {
                Fail(error ?? "好友请求发送失败。");
                yield break;
            }

            Succeed("好友请求已发送。");
        }

        IEnumerator OpenDmRoutine(string playerId)
        {
            if (_api == null || !_api.CanUse)
            {
                Fail("当前没有有效的 Steam 会话，请重新登录。");
                yield break;
            }

            if (IsBot(playerId))
            {
                Fail("暂不支持与机器人私聊。");
                yield break;
            }

            var friendsDone = false;
            var friendsOk = false;
            FriendInfoDto[] friends = null;
            yield return _api.GetFriends((ok, loaded, _) =>
            {
                friendsOk = ok;
                friends = loaded;
                friendsDone = true;
            });
            while (!friendsDone)
                yield return null;

            if (!friendsOk || !ContainsFriend(friends, playerId))
            {
                Fail("请先添加好友后再私聊。");
                yield break;
            }

            var nickname = ResolveNickname(friends, playerId);
            _setError?.Invoke(string.Empty);
            _publishState?.Invoke();
            _shell?.OpenDirectMessage(playerId, nickname);
        }

        IEnumerator LikeRecentRoutine(string playerId)
        {
            if (_api == null || !_api.CanUse)
            {
                Fail("当前没有有效的 Steam 会话，请重新登录。");
                yield break;
            }

            var viewDone = false;
            var viewOk = false;
            PublicPlayerViewDto view = null;
            string error = null;
            yield return _api.GetPublicPlayerView(playerId, 10, (ok, loaded, message) =>
            {
                viewOk = ok;
                view = loaded;
                error = message;
                viewDone = true;
            });
            while (!viewDone)
                yield return null;

            if (!viewOk || view == null || view.posts == null || view.posts.Length == 0)
            {
                Fail(viewOk ? "暂无动态" : (error ?? "动态加载失败。"));
                yield break;
            }

            SocialPostDto target = null;
            for (var i = 0; i < view.posts.Length; i++)
            {
                var post = view.posts[i];
                if (post != null && !string.IsNullOrEmpty(post.id))
                {
                    target = post;
                    break;
                }
            }

            if (target == null)
            {
                Fail("暂无动态");
                yield break;
            }

            var likeDone = false;
            var likeOk = false;
            var liked = false;
            yield return _api.TogglePostLike(target.id, (ok, nowLiked, _, message) =>
            {
                likeOk = ok;
                liked = nowLiked;
                error = message;
                likeDone = true;
            });
            while (!likeDone)
                yield return null;

            if (!likeOk)
            {
                Fail(error ?? "点赞失败。");
                yield break;
            }

            Succeed(liked ? "已点赞。" : "已取消点赞。");
        }

        void Fail(string message)
        {
            _setError?.Invoke(message ?? "操作失败。");
            _publishState?.Invoke();
        }

        void Succeed(string message)
        {
            _setError?.Invoke(message ?? string.Empty);
            _publishState?.Invoke();
        }

        bool IsBot(string playerId)
        {
            var others = _pond != null ? _pond.VisibleOthers : null;
            if (others == null)
                return false;
            for (var i = 0; i < others.Length; i++)
            {
                var user = others[i];
                if (user != null && user.playerId == playerId)
                    return user.isBot;
            }

            return false;
        }

        static bool ContainsFriend(FriendInfoDto[] friends, string playerId)
        {
            if (friends == null)
                return false;
            for (var i = 0; i < friends.Length; i++)
            {
                if (friends[i] != null && friends[i].playerId == playerId)
                    return true;
            }

            return false;
        }

        static bool HasOutgoingRequest(FriendRequestDto[] outgoing, string playerId)
        {
            if (outgoing == null)
                return false;
            for (var i = 0; i < outgoing.Length; i++)
            {
                var request = outgoing[i];
                if (request != null &&
                    request.toPlayerId == playerId &&
                    request.status == "pending")
                    return true;
            }

            return false;
        }

        static string ResolveNickname(FriendInfoDto[] friends, string playerId)
        {
            if (friends != null)
            {
                for (var i = 0; i < friends.Length; i++)
                {
                    if (friends[i] != null && friends[i].playerId == playerId)
                        return friends[i].nickname ?? "钓友";
                }
            }

            return "钓友";
        }
    }
}
