#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace FishSocial.Tests.Editor
{
    public sealed class CozySocialRefinementTests
    {
        const string Folder="Assets/Resources/Desktop/Prefabs/";
        GameObject page;
        DesktopSocialModalView view;
        SocialApi api;
        sealed class SocialApi:RealProxy
        {
            public bool AcceptOk=true;
            public int AcceptCalls,RejectCalls,DmCalls;
            public string RequestedPlayer;
            public SocialApi():base(typeof(IAuthenticatedApiClient)){}
            public IAuthenticatedApiClient Client=>(IAuthenticatedApiClient)GetTransparentProxy();
            public override IMessage Invoke(IMessage message)
            {
                var call=(IMethodCallMessage)message;object result=null;
                switch(call.MethodName)
                {
                    case "get_CanUse":result=true;break;
                    case "get_PlayerId":result="me";break;
                    case "get_BaseUrl":result="https://example.invalid";break;
                    case "AcceptFriendRequest":AcceptCalls++;result=Done(()=>((Action<bool,string>)call.Args[1])(AcceptOk,AcceptOk?"ok":"network failed"));break;
                    case "RejectFriendRequest":RejectCalls++;result=Done(()=>((Action<bool,string>)call.Args[1])(true,"ok"));break;
                    case "GetDirectMessages":
                        DmCalls++;RequestedPlayer=(string)call.Args[0];var player=RequestedPlayer;
                        result=Done(()=>((Action<bool,DirectMessageDto[],string>)call.Args[1])(true,new[]{new DirectMessageDto{id="message-"+player,fromPlayerId=player,fromNickname=player,text="message from "+player}},null));break;
                    default:result=Done(()=>{});break;
                }
                return new ReturnMessage(result,null,0,call.LogicalCallContext,call);
            }
            static IEnumerator Done(Action action){action();yield break;}
        }
        [SetUp]
        public void SetUp()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"PanelSocial.prefab");Assert.NotNull(prefab);
            page=Object.Instantiate(prefab);page.name="PanelSocial";
            CozyRedesignAuthoring.ApplySocialRefinement(page);
            view=page.GetComponent<DesktopSocialModalView>();api=new SocialApi();view.Bind(api.Client,null,null);
        }
        [TearDown]
        public void TearDown(){if(page!=null){view.OnClosed();Object.DestroyImmediate(page);}CozyMotion.CancelPageTransition();}
        static object Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(target,args);
        static void Drain(IEnumerator routine){while(routine.MoveNext())if(routine.Current is IEnumerator nested)Drain(nested);}
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        [Test]
        public void FiveDestinationsSeparateRequestsFromFriendsAndBothFromPrivateAndPondChat()
        {
            view.OpenSection(SocialSection.Requests);
            Assert.IsTrue(page.transform.Find("RequestsPage").gameObject.activeSelf);
            Assert.IsFalse(page.transform.Find("FriendsPage").gameObject.activeSelf);
            Assert.IsFalse(page.transform.Find("PondPage").gameObject.activeSelf);
            view.OpenDirectMessage("alice","Alice");
            Assert.AreEqual(SocialSection.DirectMessages,view.Section);Assert.AreEqual("alice",view.DirectMessagePlayerId);
            Assert.IsTrue(page.transform.Find("FriendsPage/FriendsChatPage").gameObject.activeInHierarchy);
            Assert.IsFalse(page.transform.Find("FriendsPage/Scroll").gameObject.activeSelf);
            Assert.IsFalse(page.transform.Find("FriendsPage/FriendGuide").gameObject.activeSelf);
            view.OpenSection(SocialSection.PondChat);
            Assert.IsFalse(page.transform.Find("FriendsPage/FriendsChatPage").gameObject.activeInHierarchy);
            Assert.IsFalse(page.transform.Find("PondPage/ChatPage/SendChat").GetComponent<Button>().interactable);
            Assert.IsTrue(page.transform.Find("PondPage/EnterPond").gameObject.activeSelf);
        }
        [Test]
        public void NotificationReadNeverAcceptsFriendOrSendsMessageAndHasTypedDestinations()
        {
            var request=new DesktopSocialInbox.Entry{Id="request:one",Kind=SocialNoticeKind.FriendRequest,Title="好友申请"};
            view.Inbox.Add(request);view.Inbox.Add(request);Assert.AreEqual(1,view.UnreadCount);
            view.OpenNotice(request);Assert.AreEqual(0,view.UnreadCount);Assert.AreEqual(SocialSection.Requests,view.Section);
            Assert.AreEqual(0,api.AcceptCalls);Assert.AreEqual(0,api.RejectCalls);
            var dm=new DesktopSocialInbox.Entry{Id="dm:one",Kind=SocialNoticeKind.DirectMessage,PlayerId="alice",Nickname="Alice"};
            view.Inbox.Add(dm);view.OpenNotice(dm);Assert.AreEqual(SocialSection.DirectMessages,view.Section);Assert.AreEqual("alice",view.DirectMessagePlayerId);
            Assert.AreEqual(0,api.DmCalls,"Opening preview while closed does not issue a network request.");
        }
        [Test]
        public void AcceptRequiresSuccessfulApiResultAndStaysOnRequestDestination()
        {
            var request=new FriendRequestDto{id="request-1",fromPlayerId="alice",fromNickname="Alice"};
            view.PresentSocialSnapshot(new FriendInfoDto[0],new[]{request},new FriendRequestDto[0]);view.OpenSection(SocialSection.Requests);
            api.AcceptOk=false;Drain((IEnumerator)Invoke(view,"RespondRoutine",request,true,null));
            var incoming=(FriendRequestDto[])typeof(DesktopSocialModalView).GetField("_incoming",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
            Assert.AreEqual(1,incoming.Length);Assert.AreEqual(1,api.AcceptCalls);
            api.AcceptOk=true;Drain((IEnumerator)Invoke(view,"RespondRoutine",request,true,null));
            incoming=(FriendRequestDto[])typeof(DesktopSocialModalView).GetField("_incoming",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
            Assert.AreEqual(0,incoming.Length);Assert.AreEqual(2,api.AcceptCalls);
            Assert.AreEqual(SocialSection.Requests,view.Section);Assert.IsNull(view.DirectMessagePlayerId);
        }
        [Test]
        public void LatePrivateMessageResponseCannotOverwriteNewlySelectedConversation()
        {
            view.OpenDirectMessage("alice","Alice");
            var alice=(IEnumerator)Invoke(view,"LoadMessages","alice","Alice");Assert.IsTrue(alice.MoveNext());var aliceRequest=(IEnumerator)alice.Current;
            view.OpenDirectMessage("bob","Bob");
            var bob=(IEnumerator)Invoke(view,"LoadMessages","bob","Bob");Assert.IsTrue(bob.MoveNext());Drain((IEnumerator)bob.Current);
            Set(view,"_open",true);Drain(bob);
            Drain(aliceRequest);Drain(alice);
            Assert.AreEqual("bob",view.DirectMessagePlayerId);
            Assert.AreEqual("与 Bob 的私聊",page.transform.Find("FriendsPage/FriendsChatPage/DmTitle").GetComponent<Text>().text);
            var content=page.transform.Find("FriendsPage/FriendsChatPage/Messages").GetComponent<ScrollRect>().content;
            var text=content.GetComponentInChildren<Text>();Assert.NotNull(text);
            var all=string.Join(" ",Array.ConvertAll(content.GetComponentsInChildren<Text>(),x=>x.text));
            StringAssert.Contains("message from bob",all);StringAssert.DoesNotContain("message from alice",all);
        }
        [Test]
        public void ClosingDoesNotDiscardIncomingNotificationsAndRebindingAnAccountClearsThem()
        {
            view.OnClosed();Invoke(view,"OnFriendPush",new FriendRequestDto{id="r1",fromNickname="Alice",fromPlayerId="alice"});
            Assert.AreEqual(1,view.UnreadCount);
            Invoke(view,"OnNotification",new DesktopNotification(NotificationKind.FriendInvite,"好友请求","duplicate"));
            Assert.AreEqual(1,view.UnreadCount);
            view.Bind(null,null,null);Assert.AreEqual(0,view.UnreadCount);
        }
        [Test]
        public void RepeatedAuthoringKeepsRequestAndNotificationRowsInSeparateLayoutSlots()
        {
            CozyRedesignAuthoring.ApplySocialRefinement(page);
            view.PreviewSection(SocialSection.Requests);
            foreach(var path in new[]{"RequestsPage/Scroll","NotificationsPage/Scroll","DirectMessagesPage/Scroll"})
            {
                var content=page.transform.Find(path).GetComponent<ScrollRect>().content;
                Assert.IsTrue(content.GetComponent<VerticalLayoutGroup>().enabled,path);
                Assert.IsTrue(content.GetComponent<ContentSizeFitter>().enabled,path);
            }
            var requests=page.transform.Find("RequestsPage/Scroll").GetComponent<ScrollRect>().content;
            LayoutRebuilder.ForceRebuildLayoutImmediate(requests);
            Assert.AreEqual(4,requests.childCount,"Two section headings, one incoming request, and one outgoing request.");
            for(var i=1;i<requests.childCount;i++)
            {
                var previous=(RectTransform)requests.GetChild(i-1);var current=(RectTransform)requests.GetChild(i);
                Assert.Greater(-current.anchoredPosition.y,-previous.anchoredPosition.y+previous.rect.height,
                    "Request row "+i+" must start below the full preceding row.");
            }
            view.PreviewSection(SocialSection.Notifications);
            var notices=page.transform.Find("NotificationsPage/Scroll").GetComponent<ScrollRect>().content;
            LayoutRebuilder.ForceRebuildLayoutImmediate(notices);
            Assert.AreEqual(3,notices.childCount);
            for(var i=1;i<notices.childCount;i++)
            {
                var previous=(RectTransform)notices.GetChild(i-1);var current=(RectTransform)notices.GetChild(i);
                Assert.Greater(-current.anchoredPosition.y,-previous.anchoredPosition.y+previous.rect.height,
                    "Notification row "+i+" must start below the full preceding row.");
            }
        }
        [Test]
        public void CommentSnapshotSelectsCardHidesPromptAndUsesSeparateAuthorAndBody()
        {
            var feed=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"PanelSocialFeed.prefab"));feed.name="PanelSocialFeed";
            try
            {
                CozyRedesignAuthoring.ApplySocialRefinement(feed);
                var content=feed.transform.Find("Scroll/Viewport/Content");
                var post=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"SocialPostCard.prefab"),content);post.name="SocialPostCard";
                CozyRedesignAuthoring.ApplySocialRefinement(post);
                var card=post.GetComponent<DesktopSocialPostCard>();card.Bind(new SocialPostDto{id="sample",nickname="阿花",text="鱼获记录"},null);
                var presenter=feed.GetComponent<DesktopSocialFeedPanel>();
                presenter.PresentSelectedComments(card,new[]{new PostCommentDto{id="c1",nickname="小白",text="下次一起钓鱼吧"}},1);
                Assert.IsFalse(feed.transform.Find("DesignFeedInfo/CommentEmpty").gameObject.activeSelf);
                Assert.AreEqual("1 条评论",feed.transform.Find("DesignFeedInfo/CommentCount").GetComponent<Text>().text);
                var panel=feed.transform.Find("DesignFeedInfo/CommentsHost/CommentsPanel");Assert.NotNull(panel);
                var author=Array.Find(panel.GetComponentsInChildren<Text>(),x=>x.name=="Author");
                var body=Array.Find(panel.GetComponentsInChildren<Text>(),x=>x.name=="Text"&&x.text=="下次一起钓鱼吧");
                Assert.NotNull(author);Assert.AreEqual("小白",author.text);Assert.NotNull(body);
                presenter.PresentSelectedComments(card,new PostCommentDto[0],0);
                Assert.AreEqual("0 条评论",feed.transform.Find("DesignFeedInfo/CommentCount").GetComponent<Text>().text);
                Assert.IsTrue(panel.Find("CommentStatus").gameObject.activeSelf);
                StringAssert.Contains("还没有留言",panel.Find("CommentStatus").GetComponent<Text>().text);
            }
            finally{Object.DestroyImmediate(feed);}
        }

        [Test]
        public void SourcePostLineHeightUsesPixelMetricsAndLongTextPushesCatchAndFooterDown()
        {
            var post=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"SocialPostCard.prefab"));post.name="SocialPostCard";
            try
            {
                CozyRedesignAuthoring.ApplySocialRefinement(post);var binder=post.GetComponent<DesktopSocialPostCard>();
                var data=new SocialPostDto{id="post",nickname="Alice",text="第一行\n第二行",visibility="friends"};binder.Bind(data,null);
                var body=post.transform.Find("BodyText").GetComponent<Text>();
                Assert.That(body.rectTransform.rect.height,Is.EqualTo(57.8f).Within(.1f));Assert.IsFalse(body.supportRichText);
                var initial=post.GetComponent<LayoutElement>().preferredHeight;
                data.text=new string('鱼',250);binder.Bind(data,null);
                Assert.Greater(post.GetComponent<LayoutElement>().preferredHeight,initial);
                var catchRect=(RectTransform)post.transform.Find("JournalCatch");var footer=(RectTransform)post.transform.Find("Actions");
                Assert.Greater(-footer.anchoredPosition.y,-catchRect.anchoredPosition.y+catchRect.rect.height);
            }
            finally{Object.DestroyImmediate(post);}
        }
    }
}
#endif
