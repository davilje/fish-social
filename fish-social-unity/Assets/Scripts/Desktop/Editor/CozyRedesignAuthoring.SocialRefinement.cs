#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        /// <summary>Approved social destinations; all controls are saved native Unity UI, not runtime-generated layout.</summary>
        public static void ApplySocialRefinement(GameObject root)
        {
            switch(root.name)
            {
                case "PanelSocial": SocialRefinementPage(root.transform);break;
                case "PanelSocialFeed": SocialRefinementFeed(root.transform);break;
                case "SocialPostCard": SocialRefinementPost(root.transform);break;
                case "FriendRow": SocialRefinementFriend(root.transform,false);break;
                case "SteamInviteRow": SocialRefinementFriend(root.transform,true);break;
                case "FriendRequestRow": SocialRefinementRequest(root.transform);break;
                case "DirectMessageRow": case "PondChatMessageRow": SocialRefinementMessage(root.transform);break;
                case "OnlinePlayerRow": case "TextStatusRow": SocialRefinementStatus(root.transform);break;
                case "PostCommentRow": SocialRefinementComment(root.transform);break;
                default:return;
            }
            SocialRefinementButtons(root.transform);
            Node(root.transform,"SocialRefinementVersion").gameObject.SetActive(false);
        }
        static void SocialRefinementButtons(Transform root)
        {
            foreach(var button in root.GetComponentsInChildren<Button>(true))
            {
                var label=button.transform.Find("Label")?.GetComponent<Text>();
                if(label==null)continue;
                label.fontStyle=FontStyle.Normal;
                var index=button.transform.parent!=null&&button.transform.parent.name=="SocialRail";
                if(index)label.fontSize=22;
                var icon=button.transform.Find("Icon")?.GetComponent<Image>()??button.transform.Find("CozyIcon")?.GetComponent<Image>();
                foreach(var layout in button.GetComponents<LayoutGroup>())layout.enabled=false;
                var inline=button.GetComponent<CozyJournalInlineContent>()??button.gameObject.AddComponent<CozyJournalInlineContent>();
                var size=index?28:label.fontSize>=19?24:20;
                inline.ConfigureCentered((RectTransform)button.transform,label,icon,new Vector2(size,size),index?12:8,index?14:8);
            }
        }
        static void SocialRefinementPage(Transform root)
        {
            DpText(root,"DesignHeader/Title",96,0,500,58,34,value:"钓友来信");
            DpText(root,"DesignHeader/Subtitle",96,64,900,25,16,true,value:"好友、申请、私信与鱼塘聊天，各有自己的位置");
            Hide(root,"Tabs");
            JournalSurface(root,"SocialRailPaper","surfaces/paper-rail",24,112,210,496).transform.SetAsFirstSibling();
            Place(root,"SocialRail",24,112,210,496);
            var labels=new[]{"我的钓友","好友申请","私信","鱼塘聊天","消息通知"};
            var icons=new[]{"user","heart","social","fish","bell"};
            for(var i=0;i<labels.Length;i++)
            {
                var button=ButtonAt(root,"SocialRail/Tab"+i,labels[i],icons[i],10,12+i*88,190,76);
                button.GetComponent<CozyButtonVisual>()?.ConfigureSurface("surfaces/tab-index-default","surfaces/tab-index-hover",null,"surfaces/tab-index-selected");
                button.GetComponent<CozyButtonVisual>()?.SetSelected(i==0);
            }
            DpText(root,"SocialRail/RequestCount",170,100,24,24,13,false,TextAnchor.MiddleCenter);
            DpText(root,"SocialRail/NotificationCount",170,364,24,24,13,false,TextAnchor.MiddleCenter);
            DpText(root,"SocialHeading",254,112,710,36,26,value:"我的钓友");
            DpText(root,"SocialHint",254,150,1092,24,15,true,value:"从钓友名片发起私聊；Steam 邀请只用于一起进塘。");
            Place(root,"FriendsPage",254,184,1092,416);
            Place(root,"FriendsPage/Reflow_PeoplePaper",0,0,710,416);
            Hide(root,"FriendsPage/Reflow_Heading");
            Place(root,"FriendsPage/Scroll",0,0,710,416);SocialRefinementScrollLayout(root.Find("FriendsPage/Scroll"),12);
            DpSurface(root,"FriendsPage/FriendGuide","surfaces/paper-aside",730,0,362,416);
            DpText(root,"FriendsPage/FriendGuide/Heading",24,20,314,34,23,value:"认识新的钓友");
            Picture(root,"FriendsPage/FriendGuide/Portrait",DesktopDefaultAvatars.Portrait("orange","social"),91,76,180,160);
            DpText(root,"FriendsPage/FriendGuide/Body",24,250,314,130,18,true,TextAnchor.UpperLeft,"好友申请在「好友申请」处理。\n私信只发给选中的好友。\n邀请进塘会通过 Steam 发送。");
            Place(root,"FriendsPage/FriendsChatPage",342,0,750,416);
            SocialRefinementChat(root,"FriendsPage/FriendsChatPage","Messages","DmInput","SendDm",750);
            DpText(root,"FriendsPage/FriendsChatPage/DmTitle",18,12,714,34,23,value:"选择一段私信");
            foreach(var page in new[]{"RequestsPage","NotificationsPage","DirectMessagesPage"})Place(root,page,254,184,page=="DirectMessagesPage"?322:1092,416);
            SocialRefinementScroll(root,"RequestsPage/Scroll",0,0,1092,416);
            SocialRefinementScroll(root,"DirectMessagesPage/Scroll",0,0,322,416);
            ButtonAt(root,"NotificationsPage/ReadAll","全部标记已读","check",872,0,220,40);
            SocialRefinementScroll(root,"NotificationsPage/Scroll",0,52,1092,364);
            Place(root,"PondPage",254,184,1092,416);
            Place(root,"PondPage/OnlinePage",0,0,322,416);
            Place(root,"PondPage/Reflow_PeoplePaper",0,0,322,416);
            Place(root,"PondPage/OnlinePage/Scroll",10,58,302,348);
            DpText(root,"PondPage/OnlinePage/Reflow_Heading",18,12,286,32,23,value:"同塘钓友");
            Place(root,"PondPage/ChatPage",342,0,750,416);
            SocialRefinementChat(root,"PondPage/ChatPage","Scroll","ChatInput","SendChat",750);
            DpText(root,"PondPage/ChatPage/Reflow_Heading",18,12,714,34,23,value:"鱼塘公共聊天");
            ButtonAt(root,"PondPage/EnterPond","选择鱼塘","fish",852,10,220,42,true);
            DpText(root,"Status",254,612,1092,25,15,true);
            ButtonAt(root,"Retry","刷新","clock",1236,108,110,38);
            foreach(var page in new[]{"RequestsPage","NotificationsPage","DirectMessagesPage","PondPage"})root.Find(page).gameObject.SetActive(false);
            root.Find("FriendsPage/FriendsChatPage").gameObject.SetActive(false);
        }
        static void SocialRefinementChat(Transform root,string path,string scroll,string input,string send,float width)
        {
            DpSurface(root,path+"/DesignChatPaper","surfaces/paper-aside",0,0,width,416).SetAsFirstSibling();
            Place(root,path+"/"+scroll,14,60,width-28,270);SocialRefinementScrollLayout(root.Find(path+"/"+scroll),10);
            var field=Place(root,path+"/"+input,14,344,width-166,58).GetComponent<InputField>();
            if(field!=null){field.lineType=InputField.LineType.MultiLineNewline;field.characterLimit=input=="DmInput"?300:200;foreach(var text in field.GetComponentsInChildren<Text>(true)){text.fontSize=17;text.supportRichText=false;}}
            ButtonAt(root,path+"/"+send,"发送","check",width-140,344,126,58,true);
        }
        static void SocialRefinementScroll(Transform root,string path,float x,float y,float w,float h)
        {
            var rect=Place(root,path,x,y,w,h);var scroll=rect.GetComponent<ScrollRect>()??rect.gameObject.AddComponent<ScrollRect>();
            var viewport=Place(rect,"Viewport",0,0,w,h);Stretch(viewport);
            var image=viewport.GetComponent<Image>()??viewport.gameObject.AddComponent<Image>();image.color=Color.clear;image.raycastTarget=true;
            if(viewport.GetComponent<RectMask2D>()==null)viewport.gameObject.AddComponent<RectMask2D>();
            var content=Place(viewport,"Content",0,0,w,0);content.anchorMin=Vector2.up;content.anchorMax=Vector2.one;content.pivot=Vector2.up;content.sizeDelta=Vector2.zero;
            var layout=content.GetComponent<VerticalLayoutGroup>()??content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.enabled=true;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;layout.spacing=12;layout.padding=new RectOffset(10,10,10,14);
            var fitter=content.GetComponent<ContentSizeFitter>()??content.gameObject.AddComponent<ContentSizeFitter>();fitter.enabled=true;fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=30;scroll.movementType=ScrollRect.MovementType.Clamped;
        }
        static void SocialRefinementScrollLayout(Transform root,float gap)
        {
            var scroll=root?.GetComponent<ScrollRect>();var layout=scroll?.content?.GetComponent<VerticalLayoutGroup>();
            if(layout!=null){layout.enabled=true;layout.childForceExpandWidth=true;layout.spacing=gap;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.padding=new RectOffset(10,10,10,14);}
            var fitter=scroll?.content?.GetComponent<ContentSizeFitter>();if(fitter!=null){fitter.enabled=true;fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;}
        }
        static void SocialRefinementBaseRow(Transform root,float width,float height)
        {
            Place(root,"",0,0,width,height);
            foreach(var layout in root.GetComponents<LayoutGroup>())layout.enabled=false;
            var element=root.GetComponent<LayoutElement>()??root.gameObject.AddComponent<LayoutElement>();element.ignoreLayout=false;element.minHeight=element.preferredHeight=height;
            var background=root.GetComponent<Image>();if(background!=null)background.color=Color.clear;
            DpSurface(root,"SocialPaper","surfaces/input-default",0,0,width,height).SetAsFirstSibling();Stretch((RectTransform)root.Find("SocialPaper"));
        }
        static void SocialHorizontalText(Text text,float left,float top,float right,float height)
        {var r=text.rectTransform;r.anchorMin=Vector2.up;r.anchorMax=Vector2.one;r.pivot=Vector2.up;r.offsetMin=new Vector2(left,-top-height);r.offsetMax=new Vector2(-right,-top);text.verticalOverflow=VerticalWrapMode.Truncate;}
        static void SocialRefinementFriend(Transform root,bool steam)
        {
            SocialRefinementBaseRow(root,680,134);
            var name=DpText(root,"Name",18,12,644,26,21);SocialHorizontalText(name,18,12,18,26);
            var detail=DpText(root,"Detail",18,44,644,30,15,true);SocialHorizontalText(detail,18,44,18,30);
            if(steam){var b=ButtonAt(root,"Invite","邀请进塘","fish",18,86,230,36);b.GetComponent<RectTransform>().sizeDelta=new Vector2(230,36);}
            else
            {
                var dm=ButtonAt(root,"Dm","发私信","social",18,86,126,36);var remove=ButtonAt(root,"Remove","移除好友",null,156,86,126,36);
                foreach(var button in new[]{dm,remove})foreach(var text in button.GetComponentsInChildren<Text>(true))text.fontSize=15;
            }
        }
        static void SocialRefinementRequest(Transform root)
        {
            SocialRefinementBaseRow(root,1072,126);
            var name=DpText(root,"Name",20,14,740,28,21);SocialHorizontalText(name,20,14,260,28);
            var detail=DpText(root,"Detail",20,52,740,56,16,true,TextAnchor.UpperLeft);SocialHorizontalText(detail,20,52,260,58);
            var accept=ButtonAt(root,"Accept","接受","check",800,38,116,46,true);var reject=ButtonAt(root,"Reject","忽略",null,932,38,116,46);
            foreach(var button in new[]{accept,reject})
            {
                var r=(RectTransform)button.transform;r.anchorMin=r.anchorMax=Vector2.one;r.pivot=Vector2.up;r.anchoredPosition=new Vector2(button==accept?-258:-130,-38);
                foreach(var text in button.GetComponentsInChildren<Text>(true))text.fontSize=16;
            }
        }
        static void SocialRefinementMessage(Transform root)
        {
            SocialRefinementBaseRow(root,702,96);
            var sender=DpText(root,"Sender",16,10,670,24,14,true);SocialHorizontalText(sender,16,10,16,24);
            var message=DpText(root,"Message",16,42,670,42,17,false,TextAnchor.UpperLeft);SocialHorizontalText(message,16,42,16,42);
            message.verticalOverflow=VerticalWrapMode.Overflow;message.lineSpacing=1.3f;
            var mine=DpText(root,"Mine",0,0,42,22,12,true,TextAnchor.MiddleRight,"我");mine.rectTransform.anchorMin=mine.rectTransform.anchorMax=Vector2.one;mine.rectTransform.anchoredPosition=new Vector2(-64,-10);
        }
        static void SocialRefinementStatus(Transform root)
        {
            SocialRefinementBaseRow(root,680,80);
            var text=DpText(root,root.Find("Message")!=null?"Message":"Name",18,12,644,56,17,true,TextAnchor.MiddleLeft);SocialHorizontalText(text,18,12,18,56);
        }
        static void SocialRefinementFeed(Transform root)
        {
            // Same 710px column and literal 15/17/22 CSS padding; the 4px first margin combines to 19px.
            SocialRefinementScrollLayout(root.Find("Scroll"),29);
            var layout=root.Find("Scroll").GetComponent<ScrollRect>()?.content?.GetComponent<VerticalLayoutGroup>();
            if(layout!=null)layout.padding=new RectOffset(17,17,19,22);
            DpText(root,"DesignFeedInfo/DetailTitle",141,69,181,28,19);
            DpText(root,"DesignFeedInfo/CommentCount",141,105,181,18,12,true);
            DpText(root,"DesignFeedInfo/Heading",24,16,314,30,22,value:"钓友留言");
            DpText(root,"DesignFeedInfo/CommentEmpty",20,188,322,94,17,true,TextAnchor.MiddleCenter,"选择一条鱼获故事\n在这里留下鼓励");
        }
        static void SocialRefinementPost(Transform root)
        {
            DpText(root,"BodyText",18,76,640,29,17,false,TextAnchor.UpperLeft).lineSpacing=1.7f;
            DpText(root,"Header/AuthorText",60,0,470,26,20,false,TextAnchor.UpperLeft);
            DpText(root,"Header/JournalTime",60,30,470,17,13,true,TextAnchor.UpperLeft);
            DpSurface(root,"Header/VisibilityPaper","surfaces/input-default",548,11,92,26).SetAsFirstSibling();
            DpText(root,"Header/JournalVisibility",548,11,92,26,12,true,TextAnchor.MiddleCenter);
            DpText(root,"Actions/JournalHint",370,5,270,26,12,true,TextAnchor.MiddleRight,"点击卡片，看看钓友来信");
            var comments=root.Find("CommentsPanel");
            if(comments!=null)DpText(comments,"CommentStatus",0,0,300,66,16,true,TextAnchor.MiddleCenter);
        }
        static void SocialRefinementComment(Transform root)
        {
            SocialRefinementBaseRow(root,304,92);
            DpText(root,"Author",12,10,250,22,15,true);
            var body=DpText(root,root.Find("Text")!=null?"Text":"CommentText",12,38,280,42,15,false,TextAnchor.UpperLeft);body.lineSpacing=1.6f;
            var delete=root.Find("Delete") as RectTransform;if(delete!=null){Place(root,"Delete",250,8,42,24);foreach(var text in delete.GetComponentsInChildren<Text>(true))text.fontSize=12;}
        }
    }
}
#endif
