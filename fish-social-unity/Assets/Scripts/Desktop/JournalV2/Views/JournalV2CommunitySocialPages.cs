using UnityEngine;
using UnityEngine.UI;
namespace FishSocial.Desktop.JournalV2
{
    public static partial class JournalV2Pages
    {
        static JournalV2Page BuildFeed(Transform parent)
        {
            var page=Frame(parent,"feed","钓友动态","钓友们最近在忙什么","social",new Color32(107,184,107,255));
            var tabs=CommunityTabs(page,new[]{"公开动态","好友动态","我的动态"},new[]{"world","heart","user"});
            var posts=JournalV2Nodes.VerticalScroll(page.Middle,"Posts",0,0,710,440,20);
            var heading=JournalV2Nodes.Text(page.Aside,"Heading","钓友留言",20,16,322,32,22);
            var comments=JournalV2Nodes.VerticalScroll(page.Aside,"Comments",12,62,338,270,14);
            var input=JournalV2Nodes.Input(page.Aside,"Comment","",20,342,224,42);input.characterLimit=200;((Text)input.placeholder).text="留下你的鼓励…";
            var send=JournalV2Nodes.Button(page.Aside,"Send","发送",null,252,342,90,42,JournalV2ButtonKind.Primary);
            var note=JournalV2Nodes.Text(page.Aside,"Status","",20,390,322,30,14);
            var compose=JournalV2Nodes.Button(page.Aside,"Compose","晒一晒我的鱼获","camera",18,430,326,48,JournalV2ButtonKind.Primary);
            var loadMore=JournalV2Nodes.Button(page.Body,"LoadMore","更多动态",null,754,604,210,34);
            page.Root.gameObject.AddComponent<JournalV2FeedController>().Initialize(page,tabs,posts,heading,comments,input,send,note,compose,loadMore);return page;
        }
        static JournalV2Page BuildCompose(Transform parent)
        {
            var root=JournalV2Nodes.Rect(parent,"compose",0,0,1672,941);var page=root.gameObject.AddComponent<JournalV2Page>();page.PageId="compose";page.Modal=true;
            var shade=JournalV2Nodes.Image(root,"Backdrop",null,0,0,1672,941);shade.color=new Color(.24f,.15f,.1f,.36f);shade.raycastTarget=true;var dismiss=shade.gameObject.AddComponent<Button>();dismiss.transition=Selectable.Transition.None;
            var paper=JournalV2Nodes.Rect(root,"Paper",491,110.5f,690,720);page.Body=paper;page.Middle=paper;
            JournalV2Nodes.Plate(paper,"Surface",0,0,690,720,JournalV2Theme.Paper,22,2);
            page.HeaderTitle=JournalV2Nodes.Text(paper,"Title","晒一晒我的鱼获",30,20,570,46,30);
            page.CloseButton=JournalV2Nodes.Button(paper,"Close","×",null,618,22,44,44);
            JournalV2Nodes.Text(paper,"FishLabel","选择今天的主角",30,86,630,28,20);
            var choices=JournalV2Nodes.GridScroll(paper,"FishChoices",22,126,646,250,3,new Vector2(190,176),new Vector2(16,16));
            JournalV2Nodes.Text(paper,"TextLabel","写下这一刻的心情",30,394,630,28,20);
            var content=JournalV2Nodes.Input(paper,"Text","",30,434,630,94,18,true);content.characterLimit=160;((Text)content.placeholder).text="今天钓到了一条…";
            var visibility=CommunityChoice(paper,"Visibility",30,548,268,new[]{"公开 · 所有钓友可见","好友 · 仅好友可见"});
            page.Status=JournalV2Nodes.Text(paper,"Status","",30,602,630,32,16);
            var cancel=JournalV2Nodes.Button(paper,"Cancel","再想想",null,300,650,150,44);
            var submit=JournalV2Nodes.Button(paper,"Publish","发布动态",null,466,650,194,44,JournalV2ButtonKind.Primary);
            root.gameObject.AddComponent<JournalV2DialogMotion>().Configure(paper);
            page.Root.gameObject.AddComponent<JournalV2ComposeController>().Initialize(page,paper,choices,content,visibility,submit,cancel,dismiss);return page;
        }
        internal static Button CommunityToggle(Transform parent,string name,string label,float y,string description="")
        {
            var button=JournalV2Nodes.Button(parent,name,"",null,18,y,674,64);
            var v=CommunityVisual(button);JournalV2Nodes.Text(v,"Name",label,18,0,460,64,21);
            if(!string.IsNullOrEmpty(description))JournalV2Nodes.Text(v,"Description",description,220,0,338,64,15).color=JournalV2Theme.Soft;
            var track=JournalV2Nodes.Plate(v,"Track",580,13,74,38,new Color32(234,220,196,255),19,0);track.ShadowColor=Color.clear;
            var knob=JournalV2Nodes.Plate(track.transform,"Knob",4,4,30,30,Color.white,15,0);knob.ShadowOffset=new Vector2(0,-2);
            var paw=JournalV2Nodes.Image(track.transform,"Paw",JournalV2Theme.Sprite("icons/paw"),-30,2,34,34);
            button.gameObject.AddComponent<JournalV2Toggle>().Configure(knob.rectTransform,track,paw.rectTransform);
            return button;
        }
        static JournalV2Page BuildSettings(Transform parent)
        {
            var page=Frame(parent,"settings","设置","按你的习惯调整，改动会自动保存","settings",new Color32(199,178,153,255));
            var tabs=CommunityTabs(page,new[]{"窗口显示","通知偏好","托盘与其他"},new[]{"settings","bell","home"});var panels=new RectTransform[3];
            for(var i=0;i<3;i++)panels[i]=JournalV2Nodes.Rect(page.Middle,"Panel"+i,0,0,710,440);
            var presets=new Button[4];
            for(var i=0;i<4;i++){presets[i]=JournalV2Nodes.Button(panels[0],"Size"+i,WindowSettings.Presets[i].Label,null,18+i%2*349,18+i/2*78,325,58);var stamp=JournalV2Nodes.Text(CommunityVisual(presets[i]),"Check","✓",287,-12,48,48,30,TextAnchor.MiddleCenter);stamp.color=JournalV2Theme.Coral;stamp.gameObject.SetActive(false);}
            JournalV2Nodes.Text(panels[0],"ModeHeading","显示模式",22,186,674,28,20);
            var segment=JournalV2Nodes.Rect(panels[0],"Modes",18,222,674,48);
            JournalV2Nodes.Plate(segment,"Track",0,0,674,48,new Color32(243,230,207,255),16,0).ShadowColor=Color.clear;
            var knob=JournalV2Nodes.Plate(segment,"Knob",4,4,222,40,new Color32(93,173,226,255),12,0);knob.ShadowColor=new Color32(63,143,196,255);knob.ShadowOffset=new Vector2(0,-3);
            segment.gameObject.AddComponent<JournalV2Segment>().Configure(knob.rectTransform,new[]{new Vector2(4,-4),new Vector2(226,-4),new Vector2(448,-4)});
            var modes=new Button[3];for(var i=0;i<3;i++){modes[i]=JournalV2Nodes.Button(segment,"Mode"+i,new[]{"窗口","无边框","全屏"}[i],null,4+i*222,4,222,40,JournalV2ButtonKind.Segment);CommunityVisual(modes[i]).Find("Plate").gameObject.SetActive(false);CommunityVisual(modes[i]).Find("Label").GetComponent<Text>().fontSize=18;}
            JournalV2Nodes.Text(panels[0],"AutoSave","切换后立即生效并保存，不需要再点保存。",22,300,674,28,16).color=JournalV2Theme.Soft;
            var notify=new Button[5];var labels=new[]{"启用桌面通知","免打扰","鱼咬钩提醒","好友邀请提醒","连接异常提醒"};
            for(var i=0;i<5;i++)notify[i]=CommunityToggle(panels[1],"Notify"+i,labels[i],14+i*78);
            var tray=CommunityToggle(panels[2],"Tray","关闭窗口时隐藏到托盘",14);
            var hide=JournalV2Nodes.Button(panels[2],"Hide","立即隐藏到托盘",null,18,104,325,58);
            var reset=JournalV2Nodes.Button(panels[2],"Reset","重置新手引导",null,367,104,325,58);
            var quit=JournalV2Nodes.Button(panels[2],"Quit","退出游戏",null,18,184,325,58);
            var heading=JournalV2Nodes.Text(page.Aside,"Heading","当前设置",24,16,314,32,22);
            var photo=JournalV2Nodes.Rect(page.Aside,"Current",24,70,314,98);
            JournalV2Nodes.Text(photo,"Resolution","",0,0,314,48,40);
            JournalV2Nodes.Text(photo,"Mode","",2,56,312,28,20).color=JournalV2Theme.Soft;
            var detail=JournalV2Nodes.Text(page.Aside,"Detail","所有改动都会自动保存。\n开关由猫爪拨动，选中的尺寸会盖一个小勾章。",26,180,312,160,16,TextAnchor.UpperLeft);detail.color=JournalV2Theme.Soft;
            var watermark=JournalV2Nodes.Image(page.Aside,"Paw",JournalV2Theme.Sprite("icons/paw"),242,376,90,90);watermark.color=new Color(1,1,1,.12f);watermark.rectTransform.localEulerAngles=new Vector3(0,0,16);
            page.Root.gameObject.AddComponent<JournalV2SettingsController>().Initialize(page,tabs,panels,presets,modes,notify,tray,hide,reset,quit,heading,photo,detail);return page;
        }
    }
}
