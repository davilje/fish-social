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
        Text _status;
        Transform _onlineContent;
        Transform _chatContent;
        Transform _friendsContent;
        Transform _dmContent;
        GameObject _onlinePage;
        GameObject _chatPage;
        GameObject _friendsPage;
        GameObject _dmPage;
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
            if (transform.childCount == 0)
                Build();
        }

        public void OnOpened()
        {
            Subscribe(true);
            ShowTab(_tab);
            RefreshAll();
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
            if (_pond == null)
                return;
            _pond.UsersChanged -= OnUsersChanged;
            _pond.ChatMessageReceived -= OnChat;
            _pond.FriendRequestReceived -= OnFriendPush;
            _pond.DmMessageReceived -= OnDmPush;
            _pond.StateChanged -= OnSocketState;
            if (!on)
                return;
            _pond.UsersChanged += OnUsersChanged;
            _pond.ChatMessageReceived += OnChat;
            _pond.FriendRequestReceived += OnFriendPush;
            _pond.DmMessageReceived += OnDmPush;
            _pond.StateChanged += OnSocketState;
        }

        void OnUsersChanged() => RenderOnline();
        void OnChat(ChatMessageDto _) => RenderChat();
        void OnFriendPush(FriendRequestDto request)
        {
            if (_tab == 2)
                StartCoroutine(LoadFriends());
        }

        void OnDmPush(DirectMessageDto message)
        {
            if (_tab == 3)
            {
                StartCoroutine(LoadConversations());
                if (message != null && (message.fromPlayerId == _dmFriendId || message.toPlayerId == _dmFriendId))
                    StartCoroutine(LoadMessages(_dmFriendId, _dmFriendName));
            }
        }

        void OnSocketState(SocialSocketState _, string __)
        {
            if (_tab == 0 || _tab == 1)
                SetStatus(SocketHint());
        }

        void Build()
        {
            var tabs = new GameObject("Tabs", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabs.transform.SetParent(transform, false);
            var tabsRt = tabs.GetComponent<RectTransform>();
            tabsRt.anchorMin = new Vector2(0f, 1f);
            tabsRt.anchorMax = Vector2.one;
            tabsRt.pivot = new Vector2(0.5f, 1f);
            tabsRt.sizeDelta = new Vector2(0f, 40f);
            var tabLayout = tabs.GetComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 8;
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;
            DesktopModalUi.MakeButton(tabs.transform, "T0", "在线钓友", () => ShowTab(0));
            DesktopModalUi.MakeButton(tabs.transform, "T1", "鱼塘聊天", () => ShowTab(1));
            DesktopModalUi.MakeButton(tabs.transform, "T2", "好友", () => ShowTab(2));
            DesktopModalUi.MakeButton(tabs.transform, "T3", "私聊", () => ShowTab(3));

            _status = DesktopModalUi.Label(transform, "Status", string.Empty, 14, TextAnchor.MiddleLeft);
            var statusRt = _status.rectTransform;
            statusRt.anchorMin = new Vector2(0f, 1f);
            statusRt.anchorMax = new Vector2(1f, 1f);
            statusRt.pivot = new Vector2(0f, 1f);
            statusRt.anchoredPosition = new Vector2(0f, -44f);
            statusRt.sizeDelta = new Vector2(0f, 24f);

            var retry = DesktopModalUi.MakeButton(transform, "Retry", "重试", RefreshAll);
            var retryRt = retry.GetComponent<RectTransform>();
            retryRt.anchorMin = new Vector2(1f, 1f);
            retryRt.anchorMax = new Vector2(1f, 1f);
            retryRt.pivot = new Vector2(1f, 1f);
            retryRt.sizeDelta = new Vector2(72f, 28f);
            retryRt.anchoredPosition = new Vector2(0f, -42f);

            _onlinePage = Page("OnlinePage", out _onlineContent);
            _chatPage = ChatPage();
            _friendsPage = Page("FriendsPage", out _friendsContent);
            _dmPage = DmPage();
        }

        GameObject Page(string name, out Transform content)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = DesktopModalUi.Stretch(go);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, -72f);
            DesktopModalUi.MakeScroll(go.transform, "Scroll", out content);
            DesktopModalUi.Stretch(content.parent.gameObject);
            return go;
        }

        GameObject ChatPage()
        {
            var go = new GameObject("ChatPage", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = DesktopModalUi.Stretch(go);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, -72f);
            var scroll = DesktopModalUi.MakeScroll(go.transform, "Scroll", out _chatContent);
            var scrollRt = scroll.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(0f, 48f);
            scrollRt.offsetMax = Vector2.zero;
            _chatInput = DesktopModalUi.MakeInput(go.transform, "ChatInput", "输入鱼塘消息（最多 200 字）", 200);
            var inputRt = _chatInput.GetComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0f, 0f);
            inputRt.anchorMax = new Vector2(1f, 0f);
            inputRt.pivot = new Vector2(0.5f, 0f);
            inputRt.offsetMin = new Vector2(0f, 0f);
            inputRt.offsetMax = new Vector2(-88f, 40f);
            var send = DesktopModalUi.MakeButton(go.transform, "SendChat", "发送", SendPondChat);
            var sendRt = send.GetComponent<RectTransform>();
            sendRt.anchorMin = new Vector2(1f, 0f);
            sendRt.anchorMax = new Vector2(1f, 0f);
            sendRt.pivot = new Vector2(1f, 0f);
            sendRt.sizeDelta = new Vector2(80f, 40f);
            sendRt.anchoredPosition = Vector2.zero;
            return go;
        }

        GameObject DmPage()
        {
            var go = new GameObject("DmPage", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = DesktopModalUi.Stretch(go);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, -72f);
            DesktopModalUi.MakeScroll(go.transform, "List", out _dmContent);
            var listRt = _dmContent.parent.GetComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0f, 0f);
            listRt.anchorMax = new Vector2(0.34f, 1f);
            listRt.offsetMin = Vector2.zero;
            listRt.offsetMax = new Vector2(-6f, -48f);
            _dmTitle = DesktopModalUi.Label(go.transform, "DmTitle", "选择一个好友会话", 16, TextAnchor.MiddleLeft);
            var titleRt = _dmTitle.rectTransform;
            titleRt.anchorMin = new Vector2(0.34f, 1f);
            titleRt.anchorMax = Vector2.one;
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.sizeDelta = new Vector2(0f, 28f);
            titleRt.anchoredPosition = Vector2.zero;
            Transform messages;
            var msgScroll = DesktopModalUi.MakeScroll(go.transform, "Messages", out messages);
            _messageContent = messages;
            var msgRt = msgScroll.GetComponent<RectTransform>();
            msgRt.anchorMin = new Vector2(0.34f, 0f);
            msgRt.anchorMax = Vector2.one;
            msgRt.offsetMin = new Vector2(6f, 48f);
            msgRt.offsetMax = Vector2.zero;
            _dmInput = DesktopModalUi.MakeInput(go.transform, "DmInput", "输入私聊（最多 300 字）", 300);
            var inputRt = _dmInput.GetComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0.34f, 0f);
            inputRt.anchorMax = Vector2.one;
            inputRt.pivot = new Vector2(0.5f, 0f);
            inputRt.offsetMin = new Vector2(6f, 0f);
            inputRt.offsetMax = new Vector2(-88f, 40f);
            var send = DesktopModalUi.MakeButton(go.transform, "SendDm", "发送", SendDm);
            var sendRt = send.GetComponent<RectTransform>();
            sendRt.anchorMin = new Vector2(1f, 0f);
            sendRt.anchorMax = new Vector2(1f, 0f);
            sendRt.pivot = new Vector2(1f, 0f);
            sendRt.sizeDelta = new Vector2(80f, 40f);
            return go;
        }

        Transform _messageContent;

        void ShowTab(int tab)
        {
            _tab = tab;
            _onlinePage.SetActive(tab == 0);
            _chatPage.SetActive(tab == 1);
            _friendsPage.SetActive(tab == 2);
            _dmPage.SetActive(tab == 3);
            RefreshAll();
        }

        void RefreshAll()
        {
            switch (_tab)
            {
                case 0:
                    SetStatus(SocketHint());
                    RenderOnline();
                    break;
                case 1:
                    SetStatus(SocketHint());
                    RenderChat();
                    break;
                case 2:
                    StartCoroutine(LoadFriends());
                    break;
                default:
                    StartCoroutine(LoadConversations());
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
            DesktopModalUi.Clear(_onlineContent);
            var others = _pond != null ? _pond.VisibleOthers : null;
            if (others == null || others.Length == 0)
            {
                DesktopModalUi.Row(_onlineContent, "当前鱼塘没有其他在线钓友。");
                return;
            }
            DesktopModalUi.Row(_onlineContent, "在线钓友 " + others.Length + " 人（点击查看摘要，不会离塘）");
            for (var i = 0; i < others.Length; i++)
            {
                var user = others[i];
                var name = string.IsNullOrEmpty(user.nickname) ? user.playerId : user.nickname;
                var phase = PetStateController.ToChinese(PetStateController.FromFishingPhase(user.fishingPhase));
                var bot = user.isBot ? " ·机" : string.Empty;
                DesktopModalUi.Row(_onlineContent, name + bot + " · " + phase + " · 钓位 " + (user.spotId ?? "未选择"), 32);
            }
        }

        void RenderChat()
        {
            DesktopModalUi.Clear(_chatContent);
            var messages = _pond != null ? _pond.PondMessages : null;
            if (messages == null || messages.Length == 0)
            {
                DesktopModalUi.Row(_chatContent, "还没有鱼塘聊天。进入鱼塘后即可发送，上限 200 字。");
                return;
            }
            for (var i = 0; i < messages.Length; i++)
            {
                var msg = messages[i];
                var prefix = msg.type == "announcement" ? "[公告] " : (msg.nickname ?? "钓友") + "：";
                DesktopModalUi.Row(_chatContent, prefix + msg.text, 36);
            }
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
            DesktopModalUi.Clear(_friendsContent);
            if (!friendsOk && !requestsOk)
            {
                SetStatus(error ?? "好友数据加载失败。");
                DesktopModalUi.Row(_friendsContent, error ?? "加载失败，请点击重试。");
                yield break;
            }
            SetStatus("好友数据已更新。");
            DesktopModalUi.Row(_friendsContent, "待处理请求");
            if (incoming == null || incoming.Length == 0)
                DesktopModalUi.Row(_friendsContent, "没有待处理的好友请求。");
            else
            {
                for (var i = 0; i < incoming.Length; i++)
                    AddRequestRow(incoming[i]);
            }
            DesktopModalUi.Row(_friendsContent, "好友列表");
            if (friends == null || friends.Length == 0)
                DesktopModalUi.Row(_friendsContent, "还没有游戏内好友。");
            else
            {
                for (var i = 0; i < friends.Length; i++)
                    AddFriendRow(friends[i]);
            }
            if (outgoing != null && outgoing.Length > 0)
                DesktopModalUi.Row(_friendsContent, "已发出请求：" + outgoing.Length);
            DesktopModalUi.Row(_friendsContent, "Steam 好友邀请进塘");
            if (_lobby == null || _lobby.Friends == null || _lobby.Friends.Count == 0)
            {
                DesktopModalUi.Row(_friendsContent, "尚未加载 Steam 好友。");
                var refresh = DesktopModalUi.MakeButton(_friendsContent, "RefreshSteam", "刷新 Steam 好友",
                    () => _lobby?.RefreshFriends());
                refresh.GetComponent<LayoutElement>().preferredHeight = 36;
            }
            else
            {
                for (var i = 0; i < _lobby.Friends.Count; i++)
                    AddSteamInviteRow(_lobby.Friends[i]);
            }
        }

        void AddRequestRow(FriendRequestDto request)
        {
            var row = new GameObject("Request", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(_friendsContent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 36;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childForceExpandWidth = false;
            DesktopModalUi.Row(row.transform, request.fromNickname + " 请求加好友", 32);
            DesktopModalUi.MakeButton(row.transform, "Accept", "接受", () => StartCoroutine(Accept(request.id)));
            DesktopModalUi.MakeButton(row.transform, "Reject", "拒绝", () => StartCoroutine(Reject(request.id)));
        }

        void AddFriendRow(FriendInfoDto friend)
        {
            var row = new GameObject("Friend", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(_friendsContent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 36;
            row.GetComponent<HorizontalLayoutGroup>().spacing = 8;
            DesktopModalUi.Row(row.transform, friend.nickname + " · " + friend.playerId, 32);
            DesktopModalUi.MakeButton(row.transform, "Dm", "私聊", () =>
            {
                _tab = 3;
                ShowTab(3);
                StartCoroutine(LoadMessages(friend.playerId, friend.nickname));
            });
            DesktopModalUi.MakeButton(row.transform, "Remove", "移除", () => StartCoroutine(Remove(friend.playerId)));
        }

        void AddSteamInviteRow(SteamFriendInfo friend)
        {
            var row = new GameObject("SteamFriend", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(_friendsContent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 36;
            row.GetComponent<HorizontalLayoutGroup>().spacing = 8;
            DesktopModalUi.Row(row.transform, friend.name + (friend.online ? " · 在线" : " · 离线"), 32);
            DesktopModalUi.MakeButton(row.transform, "Invite", "邀请进塘", () => InviteSteam(friend.steamId64));
        }

        void InviteSteam(string steamId)
        {
            if (_lobby == null)
                return;
            if (string.IsNullOrEmpty(_lobby.CurrentLobbyId))
                _lobby.CreateLobby(_pond != null ? _pond.CurrentPondId : "pond-calm");
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

        IEnumerator LoadConversations()
        {
            if (_api == null || !_api.CanUse)
            {
                SetStatus("请先完成 Steam 登录。");
                yield break;
            }
            SetStatus("正在加载私聊…");
            DmConversationDto[] list = null;
            var ok = false;
            string error = null;
            yield return _api.GetConversations((success, items, message) =>
            {
                ok = success;
                list = items;
                error = message;
            });
            DesktopModalUi.Clear(_dmContent);
            if (!ok)
            {
                SetStatus(error);
                DesktopModalUi.Row(_dmContent, error ?? "私聊加载失败。");
                yield break;
            }
            SetStatus(list != null && list.Length > 0 ? "选择一个会话。" : "还没有私聊会话。可从好友页打开。");
            if (list == null || list.Length == 0)
            {
                DesktopModalUi.Row(_dmContent, "空");
                yield break;
            }
            for (var i = 0; i < list.Length; i++)
            {
                var item = list[i];
                var captured = item;
                var button = DesktopModalUi.MakeButton(_dmContent, captured.friendPlayerId, captured.friendNickname + "：" + captured.lastMessage,
                    () => StartCoroutine(LoadMessages(captured.friendPlayerId, captured.friendNickname)));
                button.GetComponent<LayoutElement>().preferredHeight = 40;
            }
        }

        IEnumerator LoadMessages(string friendId, string friendName)
        {
            _dmFriendId = friendId;
            _dmFriendName = friendName;
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
                DesktopModalUi.Row(_messageContent, error ?? "消息加载失败。");
                yield break;
            }
            if (list == null || list.Length == 0)
                DesktopModalUi.Row(_messageContent, "还没有消息。");
            else
            {
                for (var i = 0; i < list.Length; i++)
                    DesktopModalUi.Row(_messageContent, list[i].fromNickname + "：" + list[i].text, 34);
            }
        }

        void SendPondChat()
        {
            if (_sending)
                return;
            var text = _chatInput != null ? _chatInput.text : string.Empty;
            _sending = true;
            SetStatus("发送中…");
            _pond?.SendChat(text, (ok, message) =>
            {
                _sending = false;
                SetStatus(message);
                if (ok && _chatInput != null)
                    _chatInput.text = string.Empty;
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
