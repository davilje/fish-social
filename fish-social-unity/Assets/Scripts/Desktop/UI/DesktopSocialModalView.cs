using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Pet;
using FishSocial.Desktop.Social;

namespace FishSocial.Desktop
{
    /// <summary>Five separate destinations backed by the existing friends, messages and pond APIs.</summary>
    public sealed class DesktopSocialModalView : MonoBehaviour
    {
        IAuthenticatedApiClient _api;
        SocialPondSessionController _pond;
        SocialLobbyController _lobby;
        DesktopNotificationService _notifications;
        readonly DesktopSocialInbox _inbox = new DesktopSocialInbox();
        readonly HashSet<string> _pendingRequests = new HashSet<string>();
        FriendInfoDto[] _friends = new FriendInfoDto[0];
        FriendRequestDto[] _incoming = new FriendRequestDto[0], _outgoing = new FriendRequestDto[0];
        DmConversationDto[] _conversations = new DmConversationDto[0];
        bool _uiReady, _open, _sendingDm, _sendingChat, _socialReady, _friendsBuilt, _requestsBuilt, _steamRefreshQueued;
        int _friendsGeneration, _dmGeneration, _conversationGeneration, _friendsListGeneration, _requestsListGeneration, _chatListGeneration;
        Coroutine _friendsListBuild, _requestsListBuild, _chatListBuild;
        string _dmFriendId, _dmFriendName, _account;
        SocialSection _section = SocialSection.Friends;
        Text _status, _title, _hint, _dmTitle;
        Transform _friendsContent, _requestsContent, _notificationsContent, _conversationContent, _messageContent, _onlineContent, _chatContent;
        InputField _dmInput, _chatInput;
        Button _sendDm, _sendChat;
        public SocialSection Section => _section;
        public string DirectMessagePlayerId => _dmFriendId;
        public int UnreadCount => _inbox.UnreadCount;
        public DesktopSocialInbox Inbox => _inbox;
        public event Action<int> UnreadChanged;
        public event Action PondRequested;
        public event Action<SocialSection> SectionChanged;

        public void Bind(IAuthenticatedApiClient api, SocialPondSessionController pond, SocialLobbyController lobby)
        {
            Subscribe(false);
            if(_account!=api?.PlayerId)
            {
                _inbox.Clear();_friends=new FriendInfoDto[0];_incoming=_outgoing=new FriendRequestDto[0];_conversations=new DmConversationDto[0];_dmFriendId=null;
                _socialReady=false;_steamRefreshQueued=false;CancelSocialBuilds();Clear(_friendsContent);Clear(_requestsContent);
            }
            _api=api;_pond=pond;_lobby=lobby;_account=api?.PlayerId;
            _notifications=DesktopNotificationService.Instance;
            _inbox.Changed-=OnInboxChanged;_inbox.Changed+=OnInboxChanged;
            BindPrefab();Subscribe(true);
        }
        public void OnOpened()
        {
            _open=true;
            if(_uiReady)OpenSection(_section);
        }
        public void ArmSection(SocialSection section)
        {
            if(_open&&_uiReady)
            {
                if(_section!=section)OpenSection(section);
                return;
            }
            _section=section;
        }
        public void ArmDirectMessage(string playerId,string nickname)
        {
            if(string.IsNullOrEmpty(playerId))return;
            var changed=_dmFriendId!=playerId||_section!=SocialSection.DirectMessages;
            _dmFriendId=playerId;_dmFriendName=nickname??DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友");
            if(_open&&_uiReady)
            {
                if(changed)OpenSection(SocialSection.DirectMessages);
                return;
            }
            _section=SocialSection.DirectMessages;
        }
        public void OnClosed()
        {
            _open=false;_friendsGeneration++;_dmGeneration++;_conversationGeneration++;_chatListGeneration++;
            var friendsIncomplete=_friendsListBuild!=null;var requestsIncomplete=_requestsListBuild!=null;
            _friendsListGeneration++;_requestsListGeneration++;StopAllCoroutines();
            _friendsListBuild=_requestsListBuild=_chatListBuild=null;
            if(friendsIncomplete)_friendsBuilt=false;if(requestsIncomplete)_requestsBuilt=false;
            _sendingDm=_sendingChat=false;_pendingRequests.Clear();
        }
        void OnDestroy() {Subscribe(false);_inbox.Changed-=OnInboxChanged;}
        void Subscribe(bool on)
        {
            if(_pond!=null)
            {
                _pond.UsersChanged-=OnUsersChanged;_pond.ChatMessageReceived-=OnChat;_pond.FriendRequestReceived-=OnFriendPush;_pond.DmMessageReceived-=OnDmPush;_pond.StateChanged-=OnSocketState;
                if(on){_pond.UsersChanged+=OnUsersChanged;_pond.ChatMessageReceived+=OnChat;_pond.FriendRequestReceived+=OnFriendPush;_pond.DmMessageReceived+=OnDmPush;_pond.StateChanged+=OnSocketState;}
            }
            if(_lobby!=null){_lobby.FriendsChanged-=OnSteamFriendsChanged;_lobby.Error-=OnLobbyError;if(on){_lobby.FriendsChanged+=OnSteamFriendsChanged;_lobby.Error+=OnLobbyError;}}
            if(_notifications!=null){_notifications.NotificationReceived-=OnNotification;if(on)_notifications.NotificationReceived+=OnNotification;}
        }
        T Find<T>(string path)where T:Component => DesktopModalUi.FindComponent<T>(transform,path);
        Transform Content(string path)=>DesktopModalUi.FindScrollContent(transform,path);
        void BindPrefab()
        {
            DesktopModalUi.StyleTree(transform);
            _status=Find<Text>("Status");_title=Find<Text>("SocialHeading");_hint=Find<Text>("SocialHint");
            _friendsContent=Content("FriendsPage/Scroll");_requestsContent=Content("RequestsPage/Scroll");_notificationsContent=Content("NotificationsPage/Scroll");
            _conversationContent=Content("DirectMessagesPage/Scroll");
            // Preserve the original private-message binding path while presenting it as its own destination.
            _messageContent=Content("FriendsPage/FriendsChatPage/Messages");
            _dmInput=Find<InputField>("FriendsPage/FriendsChatPage/DmInput");_dmTitle=Find<Text>("FriendsPage/FriendsChatPage/DmTitle");_sendDm=Find<Button>("FriendsPage/FriendsChatPage/SendDm");
            _onlineContent=Content("PondPage/OnlinePage/Scroll");_chatContent=Content("PondPage/ChatPage/Scroll");
            _chatInput=Find<InputField>("PondPage/ChatPage/ChatInput");_sendChat=Find<Button>("PondPage/ChatPage/SendChat");
            var sections=new[]{SocialSection.Friends,SocialSection.Requests,SocialSection.DirectMessages,SocialSection.PondChat,SocialSection.Notifications};
            for(var i=0;i<sections.Length;i++){var section=sections[i];DesktopModalUi.BindButton(transform,DesktopLocalization.Concat("SocialRail/Tab", i),()=>OpenSection(section));}
            DesktopModalUi.BindButton(transform,"Tabs/T0",()=>OpenSection(SocialSection.PondChat));DesktopModalUi.BindButton(transform,"Tabs/T1",()=>OpenSection(SocialSection.Friends));
            DesktopModalUi.BindButton(transform,"Retry",ForceRefresh);
            DesktopModalUi.BindButton(transform,"NotificationsPage/ReadAll",()=>_inbox.MarkAllRead());
            DesktopModalUi.BindButton(transform,"PondPage/EnterPond",()=>PondRequested?.Invoke());
            DesktopModalUi.BindButton(transform,"FriendsPage/FriendsChatPage/SendDm",SendDm);
            DesktopModalUi.BindButton(transform,"PondPage/ChatPage/SendChat",SendPondChat);
            if(_dmInput!=null)_dmInput.characterLimit=300;if(_chatInput!=null)_chatInput.characterLimit=200;
            _uiReady=_friendsContent!=null&&_messageContent!=null&&_onlineContent!=null&&_chatContent!=null;
            if(!_uiReady)Debug.LogError("[DesktopUI] Social hand-book requires saved friends, private-message and pond controls.",this);
        }
        public void OpenSection(SocialSection section)
        {
            _section=section;_dmGeneration++;
            SetPage("PondPage",section==SocialSection.PondChat);
            SetPage("FriendsPage",section==SocialSection.Friends||section==SocialSection.DirectMessages);
            SetPage("FriendsPage/Scroll",section==SocialSection.Friends);
            SetPage("FriendsPage/Reflow_PeoplePaper",section==SocialSection.Friends);
            SetPage("FriendsPage/Reflow_Heading",false);
            SetPage("FriendsPage/FriendGuide",section==SocialSection.Friends);
            SetPage("FriendsPage/FriendsChatPage",section==SocialSection.DirectMessages);
            SetPage("RequestsPage",section==SocialSection.Requests);
            SetPage("DirectMessagesPage",section==SocialSection.DirectMessages);
            SetPage("NotificationsPage",section==SocialSection.Notifications);
            var titles=new[]{DesktopLocalization.Text("ui.social.hub.title.friends", "我的钓友"),DesktopLocalization.Text("ui.social.hub.title.friend_requests", "好友申请"),DesktopLocalization.Text("ui.social.hub.title.direct_messages", "私信"),DesktopLocalization.Text("ui.social.hub.title.pond_chat", "鱼塘聊天"),DesktopLocalization.Text("ui.social.hub.title.notifications", "消息通知")};
            var hints=new[]{DesktopLocalization.Text("ui.social.hub.hint.friend_actions", "从钓友名片发起私聊；Steam 邀请只用于一起进塘。"),DesktopLocalization.Text("ui.social.hub.hint.request_actions", "收到的申请需要明确接受或忽略；不会自动打开聊天。"),DesktopLocalization.Text("ui.social.hub.hint.direct_message_privacy", "只与你选择的钓友对话，消息不会发送到鱼塘。"),DesktopLocalization.Text("ui.social.hub.hint.pond_chat_access", "当前鱼塘的公共聊天；离塘后仍可查看好友与私信。"),DesktopLocalization.Text("ui.social.hub.hint.session_notifications", "只保留这次登录的提醒。申请和私信可以点进去处理，进塘邀请会去选鱼塘。")};
            if(_title!=null)DesktopLocalization.SetText(_title, titles[(int)section]);if(_hint!=null)DesktopLocalization.SetText(_hint, hints[(int)section]);
            for(var i=0;i<titles.Length;i++)Find<CozyButtonVisual>(DesktopLocalization.Concat("SocialRail/Tab", i))?.SetSelected(i==(int)section);
            SectionChanged?.Invoke(section);RefreshAll();UpdateSendState();
        }
        void SetPage(string path,bool active)
        {
            var child=transform.Find(path);if(child==null)return;
            if(child.gameObject.activeSelf!=active) child.gameObject.SetActive(active);
        }
        void ForceRefresh()
        {
            if(_section==SocialSection.Friends||_section==SocialSection.Requests){_socialReady=false;CancelSocialBuilds();}
            RefreshAll();
        }
        void CancelSocialBuilds()
        {
            _friendsListGeneration++;_requestsListGeneration++;
            if(Application.isPlaying)
            {
                if(_friendsListBuild!=null)StopCoroutine(_friendsListBuild);
                if(_requestsListBuild!=null)StopCoroutine(_requestsListBuild);
            }
            _friendsListBuild=_requestsListBuild=null;_friendsBuilt=_requestsBuilt=false;
        }
        public void OpenNotifications()=>OpenSection(SocialSection.Notifications);
        public void OpenFriendRequests()=>OpenSection(SocialSection.Requests);
        public void OpenDirectMessage(string playerId,string nickname)
        {if(string.IsNullOrEmpty(playerId))return;_dmFriendId=playerId;_dmFriendName=nickname??DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友");OpenSection(SocialSection.DirectMessages);}
        void RefreshAll()
        {
            if(!_uiReady)return;
            if(_section==SocialSection.Notifications){RenderNotifications();SetStatus(DesktopLocalization.Format("ui.social.hub.status.unread_notifications", (object)(UnreadCount)));return;}
            if(_section==SocialSection.PondChat){RenderOnline();RenderChat();SetStatus(SocketHint());return;}
            if(!_open){RenderFriends();RenderRequests();return;}
            if(_section==SocialSection.DirectMessages)
            {StartCoroutine(LoadConversations());if(!string.IsNullOrEmpty(_dmFriendId))StartCoroutine(LoadMessages(_dmFriendId,_dmFriendName));else RenderEmptyMessages();return;}
            if(_section!=SocialSection.Friends&&_section!=SocialSection.Requests)return;
            // Switching these two tabs only reveals the list already built. Refreshing here
            // destroys every row and restyles it on the click.
            if(_section==SocialSection.Friends)EnsureSteamFriends();
            if(!_socialReady){StartCoroutine(LoadFriends());return;}
            if(_section==SocialSection.Requests)
            {
                if(!_requestsBuilt) RenderRequests();
                else ReplayRows(_requestsContent);
            }
            else if(!_friendsBuilt) RenderFriends();
            else ReplayRows(_friendsContent);
            ShowSocialStatus();
        }
        static void ReplayRows(Transform root)
        {
            if(root==null||!Application.isPlaying)return;
            var stickers=root.GetComponentsInChildren<CozyStickerMotion>();
            for(var i=0;i<stickers.Length;i++) stickers[i].RestartEntrance();
        }
        void ShowSocialStatus()
        {
            SetStatus(_section==SocialSection.Requests
                ?DesktopLocalization.Format("ui.social.hub.status.request_counts", (object)(_incoming.Length), (object)(_outgoing.Length))
                :DesktopLocalization.Format("ui.social.hub.status.friend_count", (object)(_friends.Length)));
        }
        void EnsureSteamFriends()
        {
            if(_steamRefreshQueued||_lobby==null||_lobby.SteamFriendsLoaded)return;
            _steamRefreshQueued=true;StartCoroutine(RefreshSteamDeferred());
        }
        IEnumerator RefreshSteamDeferred()
        {
            yield return null;
            if(!_open||_lobby==null||_lobby.SteamFriendsLoaded){_steamRefreshQueued=false;yield break;}
            _lobby.RefreshFriends();
        }
        void OnInboxChanged(){UnreadChanged?.Invoke(UnreadCount);if(_open&&_section==SocialSection.Notifications)RenderNotifications();RefreshBadges();}
        void RefreshBadges()
        {
            var requests=Find<Text>("SocialRail/RequestCount");if(requests!=null)DesktopLocalization.SetText(requests, _incoming.Length>0?_incoming.Length.ToString():"");
            var notifications=Find<Text>("SocialRail/NotificationCount");if(notifications!=null)DesktopLocalization.SetText(notifications, UnreadCount>0?UnreadCount.ToString():"");
        }
        void OnNotification(DesktopNotification notice)
        {
            if(notice.Title=="好友请求"||notice.Title=="好友申请")return; // Socket request carries the actionable request id; avoid a second generic invite.
            _inbox.Add(new DesktopSocialInbox.Entry {Id=Guid.NewGuid().ToString("N"),Kind=notice.Kind==NotificationKind.FriendInvite?SocialNoticeKind.PondInvite:SocialNoticeKind.System,Title=notice.Title,Body=notice.Body,CreatedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()});
        }
        void OnFriendPush(FriendRequestDto request)
        {
            if(request==null)return;
            _inbox.Add(new DesktopSocialInbox.Entry {Id=DesktopLocalization.Concat("request:", request.id),Kind=SocialNoticeKind.FriendRequest,Title=DesktopLocalization.Text("ui.social.hub.notice.friend_request", "新的好友申请"),Body=DesktopLocalization.Format("ui.social.hub.notice.friend_request_body", (object)((request.fromNickname??DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友")))),PlayerId=request.fromPlayerId,Nickname=request.fromNickname,CreatedAt=request.createdAt});
            _socialReady=false;CancelSocialBuilds();
            if(_open&&(_section==SocialSection.Requests||_section==SocialSection.Friends))StartCoroutine(LoadFriends());
        }
        void OnDmPush(DirectMessageDto message)
        {
            if(message==null||message.fromPlayerId==_api?.PlayerId)return;
            var reading=_open&&_section==SocialSection.DirectMessages&&message.fromPlayerId==_dmFriendId;
            _inbox.Add(new DesktopSocialInbox.Entry {Id=DesktopLocalization.Concat("dm:", message.id),Kind=SocialNoticeKind.DirectMessage,Title=DesktopLocalization.Format("ui.social.hub.notice.direct_message", (object)((message.fromNickname??DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友")))),Body=message.text,PlayerId=message.fromPlayerId,Nickname=message.fromNickname,CreatedAt=message.createdAt,Read=reading});
            if(_open&&_section==SocialSection.DirectMessages){StartCoroutine(LoadConversations());if(reading)StartCoroutine(LoadMessages(_dmFriendId,_dmFriendName));}
        }
        void OnUsersChanged(){if(_open&&_section==SocialSection.PondChat)RenderOnline();}
        void OnChat(ChatMessageDto message){if(_open&&_section==SocialSection.PondChat)RenderChat();}
        void OnSteamFriendsChanged(IReadOnlyList<SteamFriendInfo> list)
        {
            _friendsBuilt=false;
            if(_open&&_section==SocialSection.Friends)RenderFriends();
        }
        void OnLobbyError(string message){if(_open)SetStatus(message);}
        void OnSocketState(SocialSocketState state,string reason){if(_open&&_section==SocialSection.PondChat){SetStatus(SocketHint());UpdateSendState();}}
        string SocketHint()=>_pond!=null&&_pond.State==SocialSocketState.Connected?DesktopLocalization.Text("ui.social.hub.chat.hint.message_limit", "鱼塘公共聊天 · 最多 200 字"):_pond!=null&&(_pond.State==SocialSocketState.Connecting||_pond.State==SocialSocketState.Reconnecting)?DesktopLocalization.Text("ui.social.hub.chat.status.connecting", "正在连接鱼塘聊天…"):DesktopLocalization.Text("ui.social.hub.chat.hint.pond_required", "尚未进入鱼塘。选择鱼塘后，才能发送塘内聊天。");
        void SetStatus(string text){ text = DesktopLocalization.Message(text);if(_status!=null){_status.supportRichText=false;DesktopLocalization.SetText(_status, text??"");}}
        void UpdateSendState()
        {
            if(_sendDm!=null)_sendDm.interactable=!_sendingDm&&!string.IsNullOrEmpty(_dmFriendId)&&_api!=null&&_api.CanUse;
            if(_dmInput!=null)_dmInput.interactable=!string.IsNullOrEmpty(_dmFriendId);
            if(_sendChat!=null)_sendChat.interactable=!_sendingChat&&_pond!=null&&_pond.State==SocialSocketState.Connected;
            if(_chatInput!=null)_chatInput.interactable=_pond!=null&&_pond.State==SocialSocketState.Connected;
            SetPage("PondPage/EnterPond",_pond==null||_pond.State!=SocialSocketState.Connected);
        }
        public void PresentSocialSnapshot(FriendInfoDto[] friends,FriendRequestDto[] incoming,FriendRequestDto[] outgoing)
        {
            _friends=friends??new FriendInfoDto[0];_incoming=incoming??new FriendRequestDto[0];_outgoing=outgoing??new FriendRequestDto[0];
            _socialReady=true;CancelSocialBuilds();
            if(!_open||_section==SocialSection.Friends)RenderFriends();
            if(!_open||_section==SocialSection.Requests)RenderRequests();
            RefreshBadges();
        }
        IEnumerator LoadFriends()
        {
            if(_api==null||!_api.CanUse){SetStatus(DesktopLocalization.Text("ui.social.hub.error.login_required", "请先完成登录。"));yield break;}
            var generation=++_friendsGeneration;SetStatus(DesktopLocalization.Text("ui.social.hub.status.loading_friends", "正在加载钓友资料…"));
            FriendInfoDto[] friends=null;FriendRequestDto[] incoming=null,outgoing=null;bool friendsOk=false,requestsOk=false;string error=null;
            yield return _api.GetFriends((ok,list,message)=>{friendsOk=ok;friends=list;if(!ok)error=message;});
            yield return _api.GetFriendRequests((ok,inc,sent,message)=>{requestsOk=ok;incoming=inc;outgoing=sent;if(!ok)error=message;});
            if(!_open||generation!=_friendsGeneration)yield break;
            PresentSocialSnapshot(friendsOk?friends:_friends,requestsOk?incoming:_incoming,requestsOk?outgoing:_outgoing);
            if(!friendsOk||!requestsOk)SetStatus(error??DesktopLocalization.Text("ui.social.hub.error.partial_load", "部分资料加载失败，请重试。"));
            else ShowSocialStatus();
        }
        void RenderFriends()
        {
            if(_friendsContent==null||_friendsBuilt)return;
            _friendsBuilt=true;
            BeginList(ref _friendsListBuild,FillFriends(++_friendsListGeneration));
        }
        IEnumerator FillFriends(int generation)
        {
            yield return null;
            if(generation!=_friendsListGeneration||_friendsContent==null)yield break;
            Clear(_friendsContent);
            if(_friends.Length==0)TextRow(_friendsContent,"TextStatusRow",DesktopLocalization.Text("ui.social.hub.empty.game_friends", "还没有游戏内好友\n可在钓友主页发起好友申请。"));
            for(var i=0;i<_friends.Length;i++)
            {
                if(generation!=_friendsListGeneration)yield break;
                var friend=_friends[i];
                var row=Row("FriendRow",_friendsContent,false);if(row==null)continue;
                Label(row,"Name",friend.nickname??DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友"));Label(row,"Detail",DesktopLocalization.Format("ui.social.hub.label.friend_since", (object)(Date(friend.since))));
                Bind(row,"Dm",()=>OpenDirectMessage(friend.playerId,friend.nickname));Bind(row,"Remove",()=>ConfirmRemove(row,friend));
                var avatar=DesktopModalUi.FindComponent<Image>(row.transform,"Avatar");if(avatar!=null)avatar.sprite=DesktopDefaultAvatars.Portrait(friend.avatarUrl,friend.playerId);
                yield return null;
            }
            if(generation!=_friendsListGeneration)yield break;
            TextRow(_friendsContent,"TextStatusRow",DesktopLocalization.Text("ui.social.hub.title.steam_invites", "Steam 好友 · 邀请进塘"));
            if(_lobby==null||!_lobby.SteamFriendsLoaded)TextRow(_friendsContent,"TextStatusRow",DesktopLocalization.Text("ui.social.hub.hint.steam_friends_pending", "Steam 好友资料尚未返回，可稍后刷新。"));
            else if(_lobby.Friends.Count==0)TextRow(_friendsContent,"TextStatusRow",DesktopLocalization.Text("ui.social.hub.empty.steam_friends", "Steam 好友列表为空。"));
            else
            {
                var steam=_lobby.Friends;
                for(var i=0;i<steam.Count;i++)
                {
                    if(generation!=_friendsListGeneration)yield break;
                    var friend=steam[i];
                    var row=Row("SteamInviteRow",_friendsContent,false);if(row==null)continue;
                    Label(row,"Name",friend.name);Label(row,"Detail",friend.online?DesktopLocalization.Text("ui.social.hub.label.steam_friend_online", "在线 · Steam 好友"):DesktopLocalization.Text("ui.social.hub.label.steam_friend_offline", "离线 · Steam 好友"));
                    var steamId=friend.steamId64;
                    Bind(row,"Invite",()=>StartCoroutine(InviteSteamRoutine(steamId)));
                    yield return null;
                }
            }
            if(generation==_friendsListGeneration)_friendsListBuild=null;
        }
        void RenderRequests()
        {
            if(_requestsContent==null||_requestsBuilt)return;
            _requestsBuilt=true;
            BeginList(ref _requestsListBuild,FillRequests(++_requestsListGeneration));
        }
        IEnumerator FillRequests(int generation)
        {
            yield return null;
            if(generation!=_requestsListGeneration||_requestsContent==null)yield break;
            Clear(_requestsContent);
            TextRow(_requestsContent,"TextStatusRow",DesktopLocalization.Text("ui.social.hub.title.received_requests", "收到的申请"));
            if(_incoming.Length==0)TextRow(_requestsContent,"TextStatusRow",DesktopLocalization.Text("ui.social.hub.empty.received_requests", "没有待处理申请。新的钓友来信会显示在这里。"));
            for(var i=0;i<_incoming.Length;i++)
            {
                if(generation!=_requestsListGeneration)yield break;
                var request=_incoming[i];
                var row=Row("FriendRequestRow",_requestsContent,false);if(row==null)continue;
                Label(row,"Name",request.fromNickname??DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友"));Label(row,"Detail",DesktopLocalization.Format("ui.social.hub.label.received_request", (object)(Date(request.createdAt))));
                Bind(row,"Accept",()=>RespondToRequest(request,true,row));Bind(row,"Reject",()=>RespondToRequest(request,false,row));
                yield return null;
            }
            if(generation!=_requestsListGeneration)yield break;
            TextRow(_requestsContent,"TextStatusRow",DesktopLocalization.Text("ui.social.hub.title.sent_requests", "我发出的申请"));
            if(_outgoing.Length==0)TextRow(_requestsContent,"TextStatusRow",DesktopLocalization.Text("ui.social.hub.empty.sent_requests", "尚未发出好友申请。"));
            for(var i=0;i<_outgoing.Length;i++)
            {
                if(generation!=_requestsListGeneration)yield break;
                TextRow(_requestsContent,"TextStatusRow",DesktopLocalization.Format("ui.social.hub.label.sent_request_pending", (object)((_outgoing[i].toNickname??DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友")))));
                yield return null;
            }
            if(generation==_requestsListGeneration)_requestsListBuild=null;
        }
        public void RespondToRequest(FriendRequestDto request,bool accept,GameObject row=null)
        {if(request==null||_api==null||!_pendingRequests.Add(request.id))return;SetButtons(row,false);StartCoroutine(RespondRoutine(request,accept,row));}
        IEnumerator RespondRoutine(FriendRequestDto request,bool accept,GameObject row)
        {
            var success=false;string error=null;
            Action<bool,string> done=(ok,message)=>{success=ok;error=message;};
            yield return accept?_api.AcceptFriendRequest(request.id,done):_api.RejectFriendRequest(request.id,done);
            _pendingRequests.Remove(request.id);SetButtons(row,true);
            if(!success){SetStatus(error??DesktopLocalization.Text("ui.social.hub.error.request_response_failed", "申请处理失败，请重试。"));CozyActionFeedback.Play(row!=null?row.transform:transform,false,"处理失败");yield break;}
            _inbox.MarkRead(DesktopLocalization.Concat("request:", request.id));
            var remaining=new List<FriendRequestDto>(_incoming);remaining.RemoveAll(x=>x.id==request.id);_incoming=remaining.ToArray();
            _requestsBuilt=false;RenderRequests();RefreshBadges();
            SetStatus(accept?DesktopLocalization.Text("ui.social.hub.status.request_accepted", "已接受好友申请，可在「我的钓友」发起私聊。"):DesktopLocalization.Text("ui.social.hub.status.request_ignored", "已忽略这条申请。"));
            // Stay on the request page; accepting is not opening a conversation.
            if(_open)yield return LoadFriends();
        }
        void ConfirmRemove(GameObject row,FriendInfoDto friend)
        {
            var button=DesktopModalUi.FindComponent<Button>(row.transform,"Remove");if(button==null)return;
            DesktopModalUi.SetButtonLabel(button,DesktopLocalization.Text("ui.social.hub.confirm.remove_friend", "确认移除"));button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>StartCoroutine(RemoveRoutine(friend.playerId,row)));
        }
        IEnumerator RemoveRoutine(string id,GameObject row)
        {
            if(_api==null)yield break;SetButtons(row,false);bool success=false;string error=null;
            yield return _api.RemoveFriend(id,(ok,message)=>{success=ok;error=message;});
            if(!success){SetButtons(row,true);SetStatus(error??DesktopLocalization.Text("ui.social.hub.error.remove_friend_failed", "移除失败，请重试。"));yield break;}
            if(_dmFriendId==id){_dmFriendId=null;_dmGeneration++;RenderEmptyMessages();}yield return LoadFriends();
        }
        IEnumerator LoadConversations()
        {
            if(_api==null||!_api.CanUse)yield break;var generation=++_conversationGeneration;bool success=false;string error=null;DmConversationDto[] items=null;
            yield return _api.GetConversations((ok,list,message)=>{success=ok;items=list;error=message;});
            if(!_open||generation!=_conversationGeneration)yield break;
            if(!success){SetStatus(error??DesktopLocalization.Text("ui.social.hub.error.conversations_load_failed", "会话列表加载失败。"));yield break;}
            _conversations=items??new DmConversationDto[0];RenderConversations();
        }
        void RenderConversations()
        {
            Clear(_conversationContent);if(_conversationContent==null)return;
            if(_conversations.Length==0)TextRow(_conversationContent,"TextStatusRow",DesktopLocalization.Text("ui.social.hub.empty.conversations", "暂无私信\n在「我的钓友」选择一位好友开始聊天。"));
            foreach(var conversation in _conversations)
            {
                var row=Row("FriendRow",_conversationContent);if(row==null)continue;
                Label(row,"Name",conversation.friendNickname??DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友"));Label(row,"Detail",conversation.lastMessage??DesktopLocalization.Text("ui.social.hub.empty.conversation_preview", "还没有消息"));
                var remove=row.transform.Find("Remove");if(remove!=null)remove.gameObject.SetActive(false);
                var dm=DesktopModalUi.FindComponent<Button>(row.transform,"Dm");if(dm!=null)DesktopModalUi.SetButtonLabel(dm,conversation.unread>0?DesktopLocalization.Format("ui.social.hub.button.open_unread_conversation", (object)(conversation.unread)):DesktopLocalization.Text("ui.social.hub.button.open_conversation", "打开会话"));
                Bind(row,"Dm",()=>OpenDirectMessage(conversation.friendPlayerId,conversation.friendNickname));
            }
        }
        IEnumerator LoadMessages(string playerId,string nickname)
        {
            if(_api==null||string.IsNullOrEmpty(playerId))yield break;var generation=++_dmGeneration;
            _dmFriendId=playerId;_dmFriendName=nickname??DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友");if(_dmTitle!=null)DesktopLocalization.SetText(_dmTitle, DesktopLocalization.Format("ui.social.hub.title.conversation", (object)(_dmFriendName)));UpdateSendState();
            bool success=false;string error=null;DirectMessageDto[] messages=null;
            yield return _api.GetDirectMessages(playerId,(ok,list,message)=>{success=ok;messages=list;error=message;});
            if(!_open||_section!=SocialSection.DirectMessages||generation!=_dmGeneration||playerId!=_dmFriendId)yield break;
            Clear(_messageContent);
            if(!success){SetStatus(error??DesktopLocalization.Text("ui.social.hub.error.messages_load_failed", "私信加载失败，请重试。"));TextRow(_messageContent,"DirectMessageRow",DesktopLocalization.Text("ui.social.hub.error.conversation_reload_required", "这段对话暂时没有加载成功，请点刷新。"));yield break;}
            _inbox.ReadConversation(playerId);
            if(messages==null||messages.Length==0)TextRow(_messageContent,"DirectMessageRow",DesktopLocalization.Text("ui.social.hub.empty.messages", "还没有消息，给钓友打个招呼吧。"));
            else foreach(var message in messages)MessageRow(_messageContent,"DirectMessageRow",message.fromPlayerId==_api.PlayerId?DesktopLocalization.Text("ui.social.hub.label.self", "我"):message.fromNickname,message.text,message.createdAt,message.fromPlayerId==_api.PlayerId);
            SetStatus(DesktopLocalization.Format("ui.social.hub.hint.conversation_limit", (object)(_dmFriendName)));
        }
        void RenderEmptyMessages(){Clear(_messageContent);if(_dmTitle!=null)DesktopLocalization.SetText(_dmTitle, DesktopLocalization.Text("ui.social.hub.title.select_conversation", "选择一段私信"));TextRow(_messageContent,"DirectMessageRow",DesktopLocalization.Text("ui.social.hub.hint.choose_conversation", "在左侧选择会话，或从好友名片发起私聊。"));UpdateSendState();}
        void RenderNotifications()
        {
            Clear(_notificationsContent);if(_notificationsContent==null)return;
            if(_inbox.Entries.Count==0){TextRow(_notificationsContent,"TextStatusRow",DesktopLocalization.Text("ui.social.hub.empty.notifications", "这次登录还没有新通知。\n好友申请、私信和进塘邀请会记在这里，离开游戏后不会保留。"));return;}
            var ordered=new List<DesktopSocialInbox.Entry>(_inbox.Entries);
            ordered.Sort((a,b)=>a.Read!=b.Read?(a.Read?1:-1):b.CreatedAt.CompareTo(a.CreatedAt));
            foreach(var notice in ordered)
            {
                var row=Row("FriendRequestRow",_notificationsContent,!Application.isPlaying);if(row==null)continue;
                var when=notice.CreatedAt>0?DesktopLocalization.Concat(DateTimeOffset.FromUnixTimeMilliseconds(notice.CreatedAt).ToLocalTime().ToString("HH:mm"), "  "):"";
                Label(row,"Name",DesktopLocalization.Concat(notice.Read ? "" : "● ", NoticeKind(notice.Kind), "  ", notice.Title));
                Label(row,"Detail",DesktopLocalization.Concat(when, notice.Body ?? ""));
                var open=DesktopModalUi.FindComponent<Button>(row.transform,"Accept");
                var read=DesktopModalUi.FindComponent<Button>(row.transform,"Reject");
                if(open!=null)DesktopModalUi.SetButtonLabel(open,NoticeAction(notice.Kind));
                if(read!=null)
                {
                    var dismiss=notice.Kind!=SocialNoticeKind.System;
                    read.gameObject.SetActive(dismiss);
                    read.interactable=dismiss&&!notice.Read;
                    if(dismiss)DesktopModalUi.SetButtonLabel(read,notice.Read?DesktopLocalization.Text("ui.social.hub.label.notification_read", "已读"):DesktopLocalization.Text("ui.social.hub.button.mark_read", "标为已读"));
                }
                var extra=row.transform.Find("Remove");if(extra!=null)extra.gameObject.SetActive(false);
                Bind(row,"Accept",()=>OpenNotice(notice));
                if(read!=null&&read.gameObject.activeSelf)Bind(row,"Reject",()=>_inbox.MarkRead(notice.Id));
            }
        }
        static string NoticeKind(SocialNoticeKind kind)
        {return kind==SocialNoticeKind.FriendRequest?DesktopLocalization.Text("ui.social.hub.title.friend_requests", "好友申请"):kind==SocialNoticeKind.DirectMessage?DesktopLocalization.Text("ui.social.hub.title.direct_messages", "私信"):kind==SocialNoticeKind.PondInvite?DesktopLocalization.Text("ui.social.hub.notice_kind.pond_invite", "进塘邀请"):DesktopLocalization.Text("ui.social.hub.notice_kind.system", "系统提醒");}
        static string NoticeAction(SocialNoticeKind kind)
        {return kind==SocialNoticeKind.FriendRequest?DesktopLocalization.Text("ui.social.hub.button.view_request", "查看申请"):kind==SocialNoticeKind.DirectMessage?DesktopLocalization.Text("ui.social.hub.button.open_direct_message", "打开私信"):kind==SocialNoticeKind.PondInvite?DesktopLocalization.Text("ui.social.hub.button.go_to_pond", "去鱼塘"):DesktopLocalization.Text("ui.social.hub.button.dismiss_notice", "知道了");}
        public void OpenNotice(DesktopSocialInbox.Entry notice)
        {
            if(notice==null)return;
            _inbox.MarkRead(notice.Id);
            if(notice.Kind==SocialNoticeKind.DirectMessage)OpenDirectMessage(notice.PlayerId,notice.Nickname);
            else if(notice.Kind==SocialNoticeKind.FriendRequest)OpenSection(SocialSection.Requests);
            else if(notice.Kind==SocialNoticeKind.PondInvite)PondRequested?.Invoke();
        }
        void RenderOnline()
        {
            Clear(_onlineContent);var others=_pond?.VisibleOthers;
            if(others==null||others.Length==0){TextRow(_onlineContent,"OnlinePlayerRow",DesktopLocalization.Text("ui.social.hub.empty.online_anglers", "当前鱼塘暂无其他在线钓友。"));return;}
            foreach(var user in others)TextRow(_onlineContent,"OnlinePlayerRow",DesktopLocalization.Concat(DesktopLocalization.Concat((user.nickname??DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友")), "\n"), PetStateController.ToChinese(PetStateController.FromFishingPhase(user.fishingPhase))));
        }
        void RenderChat()
        {
            if(_chatContent==null)return;
            var messages=_pond!=null?_pond.PondMessages:null;
            if(messages==null||messages.Length==0)
            {
                CancelList(ref _chatListBuild,ref _chatListGeneration);
                Clear(_chatContent);
                TextRow(_chatContent,"PondChatMessageRow",DesktopLocalization.Text("ui.social.hub.hint.public_chat", "这里是当前鱼塘的公共聊天。\n进入鱼塘后，和身边的钓友聊聊吧。"));
                if(_chatContent.childCount>0)_chatContent.GetChild(_chatContent.childCount-1).name="ChatEmpty";
                UpdateSendState();
                return;
            }
            var rows=CountNamed(_chatContent,"PondChatMessageRow");
            if(rows>0&&rows==messages.Length-1)
                AppendChat(messages[messages.Length-1]);
            else if(rows>=200&&rows==messages.Length)
            {
                RemoveFirstNamed(_chatContent,"PondChatMessageRow");
                AppendChat(messages[messages.Length-1]);
            }
            else if(rows!=messages.Length)
                BeginList(ref _chatListBuild,FillChat(messages,++_chatListGeneration));
            UpdateSendState();
        }
        void AppendChat(ChatMessageDto message)
        {
            if(message==null)return;
            MessageRow(_chatContent,"PondChatMessageRow",message.type=="announcement"?DesktopLocalization.Text("ui.social.hub.label.pond_announcement", "鱼塘公告"):message.nickname,message.text,message.createdAt,message.userId==_api?.PlayerId);
        }
        IEnumerator FillChat(ChatMessageDto[] messages,int generation)
        {
            Clear(_chatContent);
            for(var i=0;i<messages.Length;i++)
            {
                if(generation!=_chatListGeneration)yield break;
                AppendChat(messages[i]);
                if((i&3)==3)yield return null;
            }
        }
        void SendDm()
        {
            if(_sendingDm||_api==null||string.IsNullOrEmpty(_dmFriendId))return;var text=(_dmInput?.text??"").Trim();
            if(text.Length==0||text.Length>300){SetStatus(DesktopLocalization.Text("ui.social.hub.error.direct_message_length", "请输入 1–300 字的私信。"));return;}
            var recipient=_dmFriendId;var nickname=_dmFriendName;_sendingDm=true;UpdateSendState();
            StartCoroutine(_api.SendDirectMessage(recipient,_pond?.Nickname??DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友"),text,(ok,message,error)=>
            {
                if(this==null)return;_sendingDm=false;UpdateSendState();
                if(!ok){SetStatus(error??DesktopLocalization.Text("ui.social.hub.error.direct_message_send_failed", "私信发送失败，文字已保留。"));return;}
                if(_dmFriendId==recipient&&_dmInput!=null&&_dmInput.text.Trim()==text)DesktopLocalization.SetText(_dmInput, "");
                if(_open&&_section==SocialSection.DirectMessages&&_dmFriendId==recipient)StartCoroutine(LoadMessages(recipient,nickname));
            }));
        }
        void SendPondChat()
        {
            if(_sendingChat||_pond==null||_pond.State!=SocialSocketState.Connected){SetStatus(SocketHint());return;}
            var text=(_chatInput?.text??"").Trim();if(text.Length==0||text.Length>200){SetStatus(DesktopLocalization.Text("ui.social.hub.error.pond_message_length", "请输入 1–200 字的鱼塘消息。"));return;}
            _sendingChat=true;UpdateSendState();_pond.SendChat(text,(ok,error)=>{if(this==null)return;_sendingChat=false;UpdateSendState();if(ok){if(_chatInput!=null&&_chatInput.text.Trim()==text)DesktopLocalization.SetText(_chatInput, "");RenderChat();}else SetStatus(error??DesktopLocalization.Text("ui.social.hub.error.pond_message_send_failed", "鱼塘消息发送失败，文字已保留。"));});
        }
        IEnumerator InviteSteamRoutine(string steamId)
        {
            if(_lobby==null)yield break;
            if(string.IsNullOrEmpty(_lobby.CurrentLobbyId))_lobby.CreateLobby(_pond?.CurrentPondId??"pond-calm");
            var deadline=Time.unscaledTime+8;while(string.IsNullOrEmpty(_lobby.CurrentLobbyId)&&Time.unscaledTime<deadline)yield return null;
            if(string.IsNullOrEmpty(_lobby.CurrentLobbyId)){SetStatus(DesktopLocalization.Text("ui.social.hub.error.invite_not_ready", "邀请暂未准备好，请稍后重试。"));yield break;}
            _lobby.InviteFriend(steamId);SetStatus(DesktopLocalization.Text("ui.social.hub.status.steam_invite_sent", "已发送 Steam 进塘邀请。"));
        }
        public void PresentConversations(DmConversationDto[] conversations)
        { _conversations=conversations??new DmConversationDto[0];RenderConversations(); }

        public void PresentDirectMessages(string playerId,string nickname,DirectMessageDto[] messages)
        {
            _dmFriendId=playerId;_dmFriendName=nickname??DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友");
            if(_dmTitle!=null)DesktopLocalization.SetText(_dmTitle, DesktopLocalization.Format("ui.social.hub.title.conversation", (object)(_dmFriendName)));
            Clear(_messageContent);
            if(messages==null||messages.Length==0)TextRow(_messageContent,"DirectMessageRow",DesktopLocalization.Text("ui.social.hub.empty.messages", "还没有消息，给钓友打个招呼吧。"));
            else foreach(var message in messages)MessageRow(_messageContent,"DirectMessageRow",message.fromPlayerId==_api?.PlayerId?DesktopLocalization.Text("ui.social.hub.label.self", "我"):message.fromNickname,message.text,message.createdAt,message.fromPlayerId==_api?.PlayerId);
            UpdateSendState();
        }
#if UNITY_EDITOR
        public void PreviewSection(SocialSection section)
        {
            if(!_uiReady)Bind((IAuthenticatedApiClient)null,null,null);
            var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            PresentSocialSnapshot(new[]{new FriendInfoDto{playerId="preview-hua",nickname="阿花",since=now-86400000},new FriendInfoDto{playerId="preview-bai",nickname="小白",since=now-172800000}},
                new[]{new FriendRequestDto{id="preview-request",fromPlayerId="preview-lu",fromNickname="芦苇边的猫",createdAt=now-3600000}},
                new[]{new FriendRequestDto{id="preview-outgoing",toNickname="湖边钓手",createdAt=now}});
            PresentConversations(new[]{new DmConversationDto{friendPlayerId="preview-hua",friendNickname="阿花",lastMessage="明天一起去千岛湖吗？",unread=2,lastAt=now}});
            _inbox.Add(new DesktopSocialInbox.Entry{Id="preview-notice-request",Kind=SocialNoticeKind.FriendRequest,Title="新的好友申请",Body="芦苇边的猫 想和你成为好友",CreatedAt=now});
            _inbox.Add(new DesktopSocialInbox.Entry{Id="preview-notice-message",Kind=SocialNoticeKind.DirectMessage,Title="阿花发来私信",Body="明天一起去千岛湖吗？",PlayerId="preview-hua",Nickname="阿花",CreatedAt=now});
            _inbox.Add(new DesktopSocialInbox.Entry{Id="preview-notice-system",Kind=SocialNoticeKind.System,Title="连接已恢复",Body="现在可以继续查看鱼塘消息。",CreatedAt=now,Read=true});
            OpenSection(section);
            if(section==SocialSection.DirectMessages){RenderConversations();PresentDirectMessages("preview-hua","阿花",new[]{new DirectMessageDto{id="preview-dm1",fromPlayerId="preview-hua",fromNickname="阿花",text="今天在芦苇边钓到一条很漂亮的鲤鱼！",createdAt=now-60000},new DirectMessageDto{id="preview-dm2",fromPlayerId="preview-hua",fromNickname="阿花",text="明天一起去千岛湖吗？",createdAt=now}});}
            if(section==SocialSection.PondChat){Clear(_chatContent);MessageRow(_chatContent,"PondChatMessageRow","鱼塘公告","欢迎来到千岛湖，愿你今天满载而归。",now,false);MessageRow(_chatContent,"PondChatMessageRow","阿花","东边芦苇附近有鱼群，大家可以来看看。",now,false);}
        }
#endif

        void BeginList(ref Coroutine running,IEnumerator routine)
        {
            if(running!=null)StopCoroutine(running);
            if(!Application.isPlaying||!isActiveAndEnabled)
            {
                while(routine.MoveNext()){}
                running=null;
                return;
            }
            running=StartCoroutine(routine);
        }
        void CancelList(ref Coroutine running,ref int generation)
        {
            generation++;
            if(running==null)return;
            StopCoroutine(running);
            running=null;
        }
        static int CountNamed(Transform parent,string name)
        {
            if(parent==null)return 0;
            var count=0;
            for(var i=0;i<parent.childCount;i++)if(parent.GetChild(i).name==name)count++;
            return count;
        }
        static void RemoveFirstNamed(Transform parent,string name)
        {
            if(parent==null)return;
            for(var i=0;i<parent.childCount;i++)
            {
                var child=parent.GetChild(i);
                if(child.name!=name)continue;
                child.SetParent(null,false);
                if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);
                return;
            }
        }
        static void Clear(Transform parent){if(parent==null)return;for(var i=parent.childCount-1;i>=0;i--){var child=parent.GetChild(i);child.SetParent(null,false);if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);}}
        static GameObject Row(string prefab,Transform parent){return Row(prefab,parent,true);}
        static GameObject Row(string prefab,Transform parent,bool skin)
        {
            if(parent==null)return null;var row=DesktopUiPrefabFactory.Instantiate(prefab,parent);if(row==null)return null;row.name=prefab;
            if(skin||!Application.isPlaying)DesktopModalUi.StyleTree(row.transform);else FreezeRow(row);
            return row;
        }
        static void FreezeRow(GameObject row)
        {
            var skin=row.GetComponent<CozyPanelSkin>()??row.AddComponent<CozyPanelSkin>();
            skin.MarkSkinned();skin.RememberChildren();
        }
        static void Label(GameObject row,string path,string value){if(row==null)return;var text=DesktopModalUi.FindComponent<Text>(row.transform,path);if(text!=null){text.supportRichText=false;DesktopLocalization.SetText(text, value??"");}}
        static void Bind(GameObject row,string path,UnityEngine.Events.UnityAction action){if(row==null)return;var button=DesktopModalUi.FindComponent<Button>(row.transform,path);if(button==null)return;button.onClick.RemoveAllListeners();button.onClick.AddListener(action);}
        static void SetButtons(GameObject row,bool enabled){if(row!=null)foreach(var button in row.GetComponentsInChildren<Button>(true))button.interactable=enabled;}
        static void TextRow(Transform parent,string prefab,string value){var row=Row(prefab,parent);if(row==null)return;var label=row.transform.Find("Name")??row.transform.Find("Message");if(label!=null){var text=label.GetComponent<Text>();if(text!=null){text.supportRichText=false;DesktopLocalization.SetText(text, value??"");}}}
        static void MessageRow(Transform parent,string prefab,string sender,string body,long at,bool own)
        {
            var row=Row(prefab,parent);if(row==null)return;Label(row,"Sender",DesktopLocalization.Concat(DesktopLocalization.Concat((sender??DesktopLocalization.Text("ui.social.overlay_chat.default_nickname", "钓友")), " · "), Date(at)));Label(row,"Message",body);
            var text=DesktopModalUi.FindComponent<Text>(row.transform,"Message");if(text!=null){text.alignment=TextAnchor.UpperLeft;var layout=row.GetComponent<LayoutElement>();if(layout!=null)layout.preferredHeight=Mathf.Max(88,text.preferredHeight+50);}
            var mine=row.transform.Find("Mine");if(mine!=null)mine.gameObject.SetActive(own);
            var paper=DesktopModalUi.FindComponent<Image>(row.transform,"SocialPaper/JournalPaper");if(paper!=null)paper.color=own?(Color)new Color32(255,240,217,255):Color.white;
        }
        static string Date(long timestamp){if(timestamp<=0)return DesktopLocalization.Text("ui.social.feed.label.just_now", "刚刚");try{return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime.ToString("MM-dd HH:mm");}catch(ArgumentOutOfRangeException){return DesktopLocalization.Text("ui.social.feed.label.just_now", "刚刚");}}
    }
}
