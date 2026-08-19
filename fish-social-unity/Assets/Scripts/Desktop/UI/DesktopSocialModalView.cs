using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Pet;
using FishSocial.Desktop.Social;

namespace FishSocial.Desktop
{
    public sealed class DesktopSocialModalView : MonoBehaviour
    {
        IAuthenticatedApiClient _api;
        SocialPondSessionController _pond;
        SocialLobbyController _lobby;
        int _tab;
        bool _legacyLayout;
        Text _status;
        Transform _onlineContent;
        Transform _chatContent;
        Transform _friendsContent;
        GameObject _pondPage;
        GameObject _friendsPage;
        InputField _chatInput;
        InputField _dmInput;
        Text _dmTitle;
        string _dmFriendId;
        string _dmFriendName;
        bool _sending;

        public void Bind(IAuthenticatedApiClient api, SocialPondSessionController pond, SocialLobbyController lobby)
        {
            _api = api;
            _pond = pond;
            _lobby = lobby;
            BindPrefab();
        }

        public void OnOpened()
        {
            Subscribe(true);
            ShowTab(_tab);
        }

        public void OpenDirectMessage(string playerId, string nickname)
        {
            if (string.IsNullOrEmpty(playerId))
                return;
            _tab = 1;
            ShowTab(1);
            StartCoroutine(LoadMessages(playerId, nickname ?? "钓友"));
        }

        public void OnClosed()
        {
            Subscribe(false);
        }

        void OnDestroy()
        {
            Subscribe(false);
        }

        void Subscribe(bool on)
        {
            if (_pond != null)
            {
                _pond.UsersChanged -= OnUsersChanged;
                _pond.ChatMessageReceived -= OnChat;
                _pond.FriendRequestReceived -= OnFriendPush;
                _pond.DmMessageReceived -= OnDmPush;
                _pond.StateChanged -= OnSocketState;
            }
            if (_lobby != null)
            {
                _lobby.FriendsChanged -= OnSteamFriendsChanged;
                _lobby.Error -= OnLobbyError;
            }
            if (!on)
                return;
            if (_pond != null)
            {
                _pond.UsersChanged += OnUsersChanged;
                _pond.ChatMessageReceived += OnChat;
                _pond.FriendRequestReceived += OnFriendPush;
                _pond.DmMessageReceived += OnDmPush;
                _pond.StateChanged += OnSocketState;
            }
            if (_lobby != null)
            {
                _lobby.FriendsChanged += OnSteamFriendsChanged;
                _lobby.Error += OnLobbyError;
            }
        }

        void OnSteamFriendsChanged(System.Collections.Generic.IReadOnlyList<SteamFriendInfo> _)
        {
            if (_tab == 1)
                StartCoroutine(LoadFriends());
        }

        void OnLobbyError(string message)
        {
            SetStatus(message);
        }

        void BindPrefab()
        {
            var panelImage = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.07f, 0.10f, 0.14f, 1f);
            _status = DesktopModalUi.FindComponent<Text>(transform, "Status");
            _pondPage = DesktopModalUi.FindChild(transform, "PondPage")?.gameObject;
            _friendsPage = DesktopModalUi.FindChild(transform, "FriendsPage")?.gameObject;
            _legacyLayout = _pondPage == null;
            if (_legacyLayout)
            {
                _pondPage = DesktopModalUi.FindChild(transform, "OnlinePage")?.gameObject;
                _onlineContent = DesktopModalUi.FindChild(transform, "OnlinePage/Scroll/Content");
                _chatContent = DesktopModalUi.FindChild(transform, "ChatPage/Scroll/Content");
                _chatInput = DesktopModalUi.FindComponent<InputField>(transform, "ChatPage/ChatInput");
            }
            else
            {
                _onlineContent = DesktopModalUi.FindChild(transform, "PondPage/OnlinePage/Scroll/Content");
                _chatContent = DesktopModalUi.FindChild(transform, "PondPage/ChatPage/Scroll/Content");
                _chatInput = DesktopModalUi.FindComponent<InputField>(transform, "PondPage/ChatPage/ChatInput");
            }
            _friendsContent = DesktopModalUi.FindChild(transform, "FriendsPage/Scroll/Content");
            var friendsChatPrefix = DesktopModalUi.FindChild(
                transform,
                "FriendsPage/FriendsChatPage") != null
                ? "FriendsPage/FriendsChatPage/"
                : "FriendsPage/";
            _messageContent = DesktopModalUi.FindChild(
                transform,
                friendsChatPrefix + "Messages/Content");
            _dmInput = DesktopModalUi.FindComponent<InputField>(
                transform,
                friendsChatPrefix + "DmInput");
            _dmTitle = DesktopModalUi.FindComponent<Text>(
                transform,
                friendsChatPrefix + "DmTitle");
            if (_legacyLayout)
            {
                _messageContent = DesktopModalUi.FindChild(transform, "DmPage/Messages/Content");
                _dmInput = DesktopModalUi.FindComponent<InputField>(transform, "DmPage/DmInput");
                _dmTitle = DesktopModalUi.FindComponent<Text>(transform, "DmPage/DmTitle");
            }

            DesktopModalUi.BindButton(transform, "Tabs/T0", () => ShowTab(0));
            DesktopModalUi.BindButton(transform, "Tabs/T1", () => ShowTab(1));
            DesktopModalUi.BindButton(transform, "Retry", RefreshAll);
            var chatSendPath = _legacyLayout ? "ChatPage/SendChat" : "PondPage/ChatPage/SendChat";
            var dmSendPath = _legacyLayout
                ? "DmPage/SendDm"
                : friendsChatPrefix + "SendDm";
            DesktopModalUi.BindButton(transform, chatSendPath, SendPondChat);
            DesktopModalUi.BindButton(transform, dmSendPath, SendDm);
            if (_status == null || _pondPage == null || _friendsPage == null || _onlineContent == null ||
                _chatContent == null || _friendsContent == null ||
                _messageContent == null || _chatInput == null || _dmInput == null ||
                _dmTitle == null)
                Debug.LogError("[DesktopUI] PanelSocial prefab is missing required controls.");
        }

        void OnUsersChanged() => RenderOnline();
        void OnChat(ChatMessageDto _) => RenderChat();
        void OnFriendPush(FriendRequestDto request)
        {
            if (_tab == 1)
                StartCoroutine(LoadFriends());
        }

        void OnDmPush(DirectMessageDto message)
        {
            if (_tab == 1)
            {
                StartCoroutine(LoadFriends());
                if (message != null && (message.fromPlayerId == _dmFriendId || message.toPlayerId == _dmFriendId))
                    StartCoroutine(LoadMessages(_dmFriendId, _dmFriendName));
            }
        }

        void OnSocketState(SocialSocketState _, string __)
        {
            if (_tab == 0 || _tab == 1)
                SetStatus(SocketHint());
        }

        Transform _messageContent;

        void ShowTab(int tab)
        {
            _tab = Mathf.Clamp(tab, 0, 1);
            if (_legacyLayout)
            {
                var online = DesktopModalUi.FindChild(transform, "OnlinePage");
                var chat = DesktopModalUi.FindChild(transform, "ChatPage");
                var dm = DesktopModalUi.FindChild(transform, "DmPage");
                if (online != null) online.gameObject.SetActive(_tab == 0);
                if (chat != null) chat.gameObject.SetActive(_tab == 0);
                if (_friendsPage != null) _friendsPage.SetActive(_tab == 1);
                if (dm != null) dm.gameObject.SetActive(_tab == 1);
            }
            else
            {
                if (_pondPage != null) _pondPage.SetActive(_tab == 0);
                if (_friendsPage != null) _friendsPage.SetActive(_tab == 1);
            }
            RefreshAll();
        }

        void RefreshAll()
        {
            switch (_tab)
            {
                case 0:
                    SetStatus(SocketHint());
                    RenderOnline();
                    RenderChat();
                    break;
                case 1:
                    SetStatus("好友与私聊");
                    StartCoroutine(LoadFriends());
                    break;
                default:
                    break;
            }
        }

        string SocketHint()
        {
            if (_pond == null)
                return "鱼塘会话尚未初始化。";
            switch (_pond.State)
            {
                case SocialSocketState.Connected: return "实时服务已连接。";
                case SocialSocketState.Connecting:
                case SocialSocketState.Reconnecting: return "实时服务连接中…";
                case SocialSocketState.Failed: return "实时服务连接失败，请进入鱼塘后重试。";
                default: return "尚未进入鱼塘。可查看好友和私聊；鱼塘聊天需先进入鱼塘。";
            }
        }

        void SetStatus(string text)
        {
            if (_status != null)
                _status.text = text ?? string.Empty;
        }

        void RenderOnline()
        {
            if (_onlineContent == null)
                return;
            DesktopModalUi.Clear(_onlineContent);
            var others = _pond != null ? _pond.VisibleOthers : null;
            if (others == null || others.Length == 0)
            {
                AddTextRow(_onlineContent, "OnlinePlayerRow", "当前鱼塘没有其他在线钓友。");
                return;
            }
            AddTextRow(
                _onlineContent,
                "OnlinePlayerRow",
                "在线钓友 " + others.Length + " 人（点击查看摘要，不会离塘）");
            for (var i = 0; i < others.Length; i++)
            {
                var user = others[i];
                var name = string.IsNullOrEmpty(user.nickname) ? user.playerId : user.nickname;
                var phase = PetStateController.ToChinese(PetStateController.FromFishingPhase(user.fishingPhase));
                var bot = user.isBot ? " ·机" : string.Empty;
                AddTextRow(
                    _onlineContent,
                    "OnlinePlayerRow",
                    name + bot + " · " + phase + " · 钓位 " + (user.spotId ?? "未选择"),
                    40);
            }
        }

        void RenderChat()
        {
            if (_chatContent == null)
                return;
            DesktopModalUi.Clear(_chatContent);
            var messages = _pond != null ? _pond.PondMessages : null;
            if (messages == null || messages.Length == 0)
            {
                AddTextRow(
                    _chatContent,
                    "PondChatMessageRow",
                    "还没有鱼塘聊天。进入鱼塘后即可发送，上限 200 字。");
                return;
            }
            for (var i = 0; i < messages.Length; i++)
            {
                var msg = messages[i];
                var prefix = msg.type == "announcement" ? "[公告] " : (msg.nickname ?? "钓友") + "：";
                AddTextRow(_chatContent, "PondChatMessageRow", prefix + msg.text, 40);
            }
        }

        static void AddTextRow(Transform parent, string prefabName, string text, int height = 36)
        {
            var row = DesktopUiPrefabFactory.Instantiate(prefabName, parent);
            if (row == null)
                return;
            var label = DesktopUiPrefabFactory.Child(row, "Name") ??
                        DesktopUiPrefabFactory.Child(row, "Message");
            var textComponent = label != null ? label.GetComponent<Text>() : null;
            if (textComponent != null)
                textComponent.text = text ?? string.Empty;
        }

        IEnumerator LoadFriends()
        {
            if (_api == null || !_api.CanUse)
            {
                SetStatus("请先完成 Steam 登录。");
                yield break;
            }
            SetStatus("正在加载好友…");
            FriendInfoDto[] friends = null;
            FriendRequestDto[] incoming = null;
            FriendRequestDto[] outgoing = null;
            string error = null;
            var friendsOk = false;
            var requestsOk = false;
            yield return _api.GetFriends((ok, list, message) =>
            {
                friendsOk = ok;
                friends = list;
                error = message;
            });
            yield return _api.GetFriendRequests((ok, inc, outg, message) =>
            {
                requestsOk = ok;
                incoming = inc;
                outgoing = outg;
                if (!ok) error = message;
            });
            if (_friendsContent == null)
                yield break;
            DesktopModalUi.Clear(_friendsContent);
            if (!friendsOk && !requestsOk)
            {
                SetStatus(error ?? "好友数据加载失败。");
                AddTextRow(_friendsContent, "TextStatusRow", error ?? "加载失败，请点击重试。");
                yield break;
            }
            SetStatus("好友数据已更新。");
            AddTextRow(_friendsContent, "TextStatusRow", "待处理请求");
            if (incoming == null || incoming.Length == 0)
                AddTextRow(_friendsContent, "TextStatusRow", "没有待处理的好友请求。");
            else
            {
                for (var i = 0; i < incoming.Length; i++)
                    AddRequestRow(incoming[i]);
            }
            AddTextRow(_friendsContent, "TextStatusRow", "好友列表");
            if (friends == null || friends.Length == 0)
                AddTextRow(_friendsContent, "TextStatusRow", "还没有游戏内好友。");
            else
            {
                for (var i = 0; i < friends.Length; i++)
                    AddFriendRow(friends[i]);
            }
            if (outgoing != null && outgoing.Length > 0)
                AddTextRow(_friendsContent, "TextStatusRow", "已发出请求：" + outgoing.Length);
            AddTextRow(_friendsContent, "TextStatusRow", "Steam 好友邀请进塘");
            if (_lobby == null || _lobby.Friends == null || _lobby.Friends.Count == 0)
            {
                AddTextRow(_friendsContent, "TextStatusRow", "尚未加载 Steam 好友。");
                SetStatus("尚未加载 Steam 好友，可点击顶部重试。");
            }
            else
            {
                for (var i = 0; i < _lobby.Friends.Count; i++)
                    AddSteamInviteRow(_lobby.Friends[i]);
            }
        }

        void AddRequestRow(FriendRequestDto request)
        {
            var row = DesktopUiPrefabFactory.Instantiate("FriendRequestRow", _friendsContent);
            if (row == null)
                return;
            var label = DesktopUiPrefabFactory.Child(row, "Name");
            var labelText = label != null ? label.GetComponent<Text>() : null;
            if (labelText != null)
                labelText.text = request.fromNickname + " 请求加好友";
            var accept = DesktopUiPrefabFactory.Child(row, "Accept");
            var reject = DesktopUiPrefabFactory.Child(row, "Reject");
            BindActionButton(accept, () => StartCoroutine(Accept(request.id)));
            BindActionButton(reject, () => StartCoroutine(Reject(request.id)));
        }

        void AddFriendRow(FriendInfoDto friend)
        {
            var row = DesktopUiPrefabFactory.Instantiate("FriendRow", _friendsContent);
            if (row == null)
                return;
            var label = DesktopUiPrefabFactory.Child(row, "Name");
            var labelText = label != null ? label.GetComponent<Text>() : null;
            if (labelText != null)
                labelText.text = friend.nickname + " · " + friend.playerId;
            var select = row.GetComponent<Button>();
            BindActionButton(select != null ? select.transform : null, () => SelectFriend(friend));
            var dm = DesktopUiPrefabFactory.Child(row, "Dm");
            BindActionButton(dm, () => SelectFriend(friend));
            var remove = DesktopUiPrefabFactory.Child(row, "Remove");
            BindActionButton(remove, () => StartCoroutine(Remove(friend.playerId)));
        }

        void SelectFriend(FriendInfoDto friend)
        {
            if (friend == null)
                return;
            _tab = 1;
            ShowTab(1);
            StartCoroutine(LoadMessages(friend.playerId, friend.nickname));
        }

        void AddSteamInviteRow(SteamFriendInfo friend)
        {
            var row = DesktopUiPrefabFactory.Instantiate("SteamInviteRow", _friendsContent);
            if (row == null)
                return;
            var label = DesktopUiPrefabFactory.Child(row, "Name");
            var labelText = label != null ? label.GetComponent<Text>() : null;
            if (labelText != null)
                labelText.text = friend.name + (friend.online ? " · 在线" : " · 离线");
            var invite = DesktopUiPrefabFactory.Child(row, "Invite");
            BindActionButton(invite, () => InviteSteam(friend.steamId64));
        }

        static void BindActionButton(Transform buttonTransform, UnityEngine.Events.UnityAction action)
        {
            if (buttonTransform == null)
                return;
            var button = buttonTransform.GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(action);
        }

        void InviteSteam(string steamId)
        {
            StartCoroutine(InviteSteamRoutine(steamId));
        }

        IEnumerator InviteSteamRoutine(string steamId)
        {
            if (_lobby == null)
                yield break;
            if (string.IsNullOrEmpty(_lobby.CurrentLobbyId))
            {
                SetStatus("正在创建 Lobby…");
                _lobby.CreateLobby(_pond != null ? _pond.CurrentPondId : "pond-calm");
            }

            var deadline = Time.unscaledTime + 8f;
            while (string.IsNullOrEmpty(_lobby.CurrentLobbyId) && Time.unscaledTime < deadline)
                yield return null;

            if (string.IsNullOrEmpty(_lobby.CurrentLobbyId))
            {
                SetStatus("Lobby 尚未就绪，无法邀请 Steam 好友。");
                yield break;
            }

            _lobby.InviteFriend(steamId);
            SetStatus("已发送 Steam 进塘邀请。");
        }

        IEnumerator Accept(string id)
        {
            yield return _api.AcceptFriendRequest(id, (ok, message) => SetStatus(message));
            yield return LoadFriends();
        }

        IEnumerator Reject(string id)
        {
            yield return _api.RejectFriendRequest(id, (ok, message) => SetStatus(message));
            yield return LoadFriends();
        }

        IEnumerator Remove(string friendId)
        {
            yield return _api.RemoveFriend(friendId, (ok, message) => SetStatus(message));
            yield return LoadFriends();
        }

        IEnumerator LoadMessages(string friendId, string friendName)
        {
            _dmFriendId = friendId;
            _dmFriendName = friendName;
            if (_dmTitle != null)
                _dmTitle.text = "与 " + friendName + " 的私聊";
            DirectMessageDto[] list = null;
            var ok = false;
            string error = null;
            yield return _api.GetDirectMessages(friendId, (success, items, message) =>
            {
                ok = success;
                list = items;
                error = message;
            });
            DesktopModalUi.Clear(_messageContent);
            if (!ok)
            {
                SetStatus(error);
                AddTextRow(_messageContent, "DirectMessageRow", error ?? "消息加载失败。");
                yield break;
            }
            if (list == null || list.Length == 0)
                AddTextRow(_messageContent, "DirectMessageRow", "还没有消息。");
            else
            {
                for (var i = 0; i < list.Length; i++)
                    AddTextRow(
                        _messageContent,
                        "DirectMessageRow",
                        list[i].fromNickname + "：" + list[i].text,
                        40);
            }
        }

        void SendPondChat()
        {
            if (_sending)
                return;
            if (_pond == null || _pond.State != SocialSocketState.Connected)
            {
                SetStatus("请先进入鱼塘后再发送聊天。");
                return;
            }
            var text = _chatInput != null ? _chatInput.text : string.Empty;
            _sending = true;
            SetStatus("发送中…");
            _pond.SendChat(text, (ok, message) =>
            {
                _sending = false;
                SetStatus(message);
                if (ok && _chatInput != null)
                    _chatInput.text = string.Empty;
                if (ok)
                    RenderChat();
            });
        }

        void SendDm()
        {
            if (_sending)
                return;
            if (string.IsNullOrEmpty(_dmFriendId))
            {
                SetStatus("请先选择私聊对象。");
                return;
            }
            var text = _dmInput != null ? _dmInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                SetStatus("不能发送空消息。");
                return;
            }
            if (text.Length > 300)
            {
                SetStatus("私聊最多 300 字。");
                return;
            }
            _sending = true;
            SetStatus("发送中…");
            StartCoroutine(_api.SendDirectMessage(_dmFriendId, _pond != null ? _pond.Nickname : "Steam玩家", text,
                (ok, _, message) =>
                {
                    _sending = false;
                    SetStatus(message);
                    if (ok)
                    {
                        if (_dmInput != null)
                            _dmInput.text = string.Empty;
                        StartCoroutine(LoadMessages(_dmFriendId, _dmFriendName));
                    }
                }));
        }
    }
}
