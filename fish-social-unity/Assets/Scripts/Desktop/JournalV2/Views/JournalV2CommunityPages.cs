using System;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    public static partial class JournalV2Pages
    {
        public static JournalV2Page BuildCommunity(Transform parent, string id)
        {
            switch (id)
            {
                case "profile": return BuildProfile(parent);
                case "edit": return BuildEdit(parent);
                case "ach": return BuildAchievements(parent);
                case "rank": return BuildRank(parent);
                case "feed": return BuildFeed(parent);
                case "settings": return BuildSettings(parent);
                case "compose": return BuildCompose(parent);
                default: return null;
            }
        }

        internal static Sprite CommunityFish(string id)
        {
            id = DesktopGameData.ResolveSpeciesId(id);
            if (id == "largemouth_bass") id = "black_bass";
            return JournalV2Theme.Sprite(DesktopLocalization.Concat("illustrations/fish/", id));
        }
        internal static Sprite CommunityAvatar(string path, string playerId = null)
        {
            var id = DesktopDefaultAvatars.ResolvePetId(path, playerId);
            return JournalV2Theme.Sprite(DesktopLocalization.Concat(DesktopLocalization.Concat("characters/avatars/", id), ".png"));
        }
        internal static Transform CommunityVisual(Button button) => button.transform.Find("Visual") ?? button.transform;
        internal static void CommunityLabel(Button button, string value)
        {
            var label = CommunityVisual(button).Find("Label")?.GetComponent<Text>();
            if (label != null) DesktopLocalization.SetText(label, value);
        }
        internal static void CommunityClear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(child); else UnityEngine.Object.DestroyImmediate(child);
            }
        }
        internal static Button[] CommunityTabs(JournalV2Page page, string[] labels, string[] icons)
        {
            var tabs = new Button[labels.Length];
            for (var i = 0; i < tabs.Length; i++) tabs[i] = JournalV2Nodes.Button(page.Rail, DesktopLocalization.Concat("Tab", i), labels[i], icons[i], 10, 12 + i * 88, 190, 76, JournalV2ButtonKind.Index);
            return tabs;
        }
        internal static void CommunityPhoto(RectTransform photo, Sprite art, string caption)
        {
            var image = photo.Find("Visual/Art")?.GetComponent<Image>();
            if (image != null) { image.sprite = art; image.enabled = art != null; }
            var text = photo.Find("Visual/Caption")?.GetComponent<Text>(); if (text != null) DesktopLocalization.SetText(text, caption);
        }
        internal static Dropdown CommunityChoice(Transform parent, string name, float x, float y, float w, string[] options)
        {
            var root = JournalV2Nodes.Rect(parent, name, x, y, w, 46);
            var plate = JournalV2Nodes.Image(root, "Plate", JournalV2Theme.Sprite("surfaces/input-default"), 0, 0, w, 46);
            var dropdown = root.gameObject.AddComponent<Dropdown>(); dropdown.targetGraphic = plate;
            dropdown.captionText = JournalV2Nodes.Text(root, "Caption", "", 13, 0, w - 48, 46, 18);
            JournalV2Nodes.Text(root, "Arrow", "▾", w - 35, 0, 24, 46, 20, TextAnchor.MiddleCenter);
            var template = JournalV2Nodes.Rect(root, "Template", 0, 48, w, 210);
            JournalV2Nodes.Image(template, "Paper", JournalV2Theme.Sprite("surfaces/input-default"), 0, 0, w, 210);
            var viewport = JournalV2Nodes.Rect(template, "Viewport", 4, 4, w - 8, 202); viewport.gameObject.AddComponent<RectMask2D>();
            var content = JournalV2Nodes.Rect(viewport, "Content", 0, 0, w - 8, 36);
            var item = JournalV2Nodes.Rect(content, "Item", 0, 0, w - 8, 36);
            var toggle = item.gameObject.AddComponent<Toggle>();
            var background = JournalV2Nodes.Image(item, "Background", null, 0, 0, w - 8, 36); background.color = new Color(1,.97f,.9f); toggle.targetGraphic = background;
            var check = JournalV2Nodes.Text(item, "Check", "✓", 8, 0, 24, 36, 18); toggle.graphic = check;
            dropdown.itemText = JournalV2Nodes.Text(item, "Label", "", 38, 0, w - 50, 36, 16);
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            dropdown.template = template; dropdown.ClearOptions(); dropdown.AddOptions(new System.Collections.Generic.List<string>(options));
            template.gameObject.SetActive(false); return dropdown;
        }

        static JournalV2Page BuildRank(Transform parent)
        {
            var page = Frame(parent, "rank", "排行榜", "看看谁是本周的钓鱼王", "trophy", new Color32(247,182,200,255));
            var tabs = CommunityTabs(page, new[] { "今日大鱼", "每周钓王", "当前鱼塘", "稀有鱼获" }, new[] { "fish", "trophy", "pond", "star" });
            var scroll = JournalV2Nodes.VerticalScroll(page.Middle, "RankScroll", 0, 0, 710, 440, 17);
            var heading = JournalV2Nodes.Text(page.Aside, "Heading", "钓友名片", 24, 16, 314, 30, 22);
            var photo = JournalV2Nodes.Polaroid(page.Aside, "Portrait", null, "选择一位钓友", 30, 58);
            var bio = JournalV2Nodes.Text(page.Aside, "Bio", "", 24, 316, 314, 62, 16);
            var score = JournalV2Nodes.Text(page.Aside, "Score", "", 24, 382, 314, 28, 15);
            var open = JournalV2Nodes.Button(page.Aside, "OpenPlayer", "看看钓友的收藏", null, 18, 420, 326, 58, JournalV2ButtonKind.Primary);
            var view = page.Root.gameObject.AddComponent<JournalV2RankController>();
            view.Initialize(page, tabs, scroll, heading, photo, bio, score, open); return page;
        }

        static JournalV2Page BuildProfile(Transform parent)
        {
            var page = Frame(parent, "profile", "个人中心", "记录属于你的钓鱼时光", "user", JournalV2Theme.Coral);
            var tabs = CommunityTabs(page, new[] { "资料", "展示柜", "图鉴", "相册", "成就" }, new[] { "user", "fish", "map", "heart", "medal" });
            var overview = JournalV2Nodes.Rect(page.Middle, "Overview", 0, 0, 710, 440);
            var banner = JournalV2Nodes.Image(overview, "Banner", JournalV2Theme.Sprite("scenes/pond-afternoon.png"), 15, 12, 680, 150); banner.color = new Color(1,1,1,.34f);
            var avatar = JournalV2Nodes.Image(overview, "Avatar", null, 35, 34, 105, 105);
            var name = JournalV2Nodes.Text(overview, "Name", "", 160, 38, 500, 40, 30);
            var bio = JournalV2Nodes.Text(overview, "Bio", "", 160, 84, 515, 54, 16);
            var stats = new Text[3];
            for (var i = 0; i < 3; i++)
            {
                var box = JournalV2Nodes.Image(overview, DesktopLocalization.Concat("Stat", i), JournalV2Theme.Sprite("surfaces/input-default"), 15+i*230,179,220,76);
                stats[i] = JournalV2Nodes.Text(box.transform, "Value", "", 16, 8, 190, 60, 18);
            }
            var progress = JournalV2Nodes.Image(overview, "Progress", JournalV2Theme.Sprite("surfaces/input-default"),15,269,680,98);
            var xp = JournalV2Nodes.Text(progress.transform,"Text","",18,10,644,74,16);
            var edit = JournalV2Nodes.Button(overview,"Edit","编辑资料",null,15,381,216,43,JournalV2ButtonKind.Primary);
            var showcase = JournalV2Nodes.Button(overview,"Showcase","我的展示柜",null,243,381,220,43);
            var album = JournalV2Nodes.Button(overview,"Album","翻翻相册",null,475,381,220,43);
            var list = JournalV2Nodes.GridScroll(page.Middle,"Collection",0,0,710,440,3,new Vector2(215,250),new Vector2(22,22)); list.gameObject.SetActive(false);
            var photo = JournalV2Nodes.Polaroid(page.Aside,"Photo",null,"",30,58);
            var title = JournalV2Nodes.Text(page.Aside,"Heading","我的心头好",24,16,314,30,22);
            var detail = JournalV2Nodes.Text(page.Aside,"Detail","",24,306,314,104,16);
            var action = JournalV2Nodes.Button(page.Aside,"Action","整理我的展示柜",null,18,420,210,50,JournalV2ButtonKind.Primary);
            var secondary = JournalV2Nodes.Button(page.Aside,"Secondary","清空",null,240,420,104,50);
            var previous = JournalV2Nodes.Button(page.Body,"Previous","‹ 上一页",null,384,577,150,42);
            var next = JournalV2Nodes.Button(page.Body,"Next","下一页 ›",null,684,577,150,42);
            var pageNumber = JournalV2Nodes.Text(page.Body,"PageNumber","",554,577,110,42,18,TextAnchor.MiddleCenter);
            var picker = JournalV2Nodes.Rect(page.Body,"FishPicker",250,40,850,550);
            JournalV2Nodes.Image(picker,"Paper",JournalV2Theme.Sprite("surfaces/modal-paper"),0,0,850,550);
            var pickerTitle = JournalV2Nodes.Text(picker,"Title","选择展示鱼获",24,18,620,36,24);
            var pickerList = JournalV2Nodes.GridScroll(picker,"Fish",24,70,802,390,4,new Vector2(180,160),new Vector2(18,18));
            var closePicker = JournalV2Nodes.Button(picker,"Close","关闭",null,656,480,170,42);
            picker.gameObject.SetActive(false);
            var view = page.Root.gameObject.AddComponent<JournalV2ProfileController>();
            view.Initialize(page,tabs,overview,avatar,name,bio,stats,xp,edit,showcase,album,list,photo,title,detail,action,secondary,previous,next,pageNumber,picker,pickerTitle,pickerList,closePicker);
            return page;
        }

        static JournalV2Page BuildEdit(Transform parent)
        {
            var page = Frame(parent,"edit","个人中心","记录属于你的钓鱼时光","user",JournalV2Theme.Coral);
            DesktopLocalization.SetText(page.Heading, "编辑资料");
            JournalV2Nodes.Text(page.Middle,"NameLabel","游戏昵称 · 1–12 个字",18,6,674,26,18);
            var nickname=JournalV2Nodes.Input(page.Middle,"Nickname","游戏昵称",18,40,674,44);
            JournalV2Nodes.Text(page.Middle,"BioLabel","个人简介 · 最多 120 个字",18,98,674,26,18);
            var bio=JournalV2Nodes.Input(page.Middle,"Bio","介绍一下自己",18,132,674,70,18,true);
            JournalV2Nodes.Text(page.Middle,"AvatarLabel","选择头像",18,224,674,26,18);
            var avatarHost=JournalV2Nodes.Rect(page.Middle,"Avatars",18,264,674,100);
            var avatars=new Button[6]; var ids=new[]{"orange","white","black","calico","silver","blue"};
            for(var i=0;i<avatars.Length;i++)
            {
                avatars[i]=JournalV2Nodes.Button(avatarHost,DesktopLocalization.Concat("Avatar", i),new[]{"橘猫","白猫","黑猫","三花","银渐层","蓝猫"}[i],null,i*112,0,87,90);
                JournalV2Nodes.Image(CommunityVisual(avatars[i]),"Portrait",JournalV2Theme.Sprite(DesktopLocalization.Concat(DesktopLocalization.Concat("characters/avatars/", ids[i]), ".png")),13,5,60,54);
                var label=CommunityVisual(avatars[i]).Find("Label") as RectTransform; if(label!=null){label.anchorMin=label.anchorMax=Vector2.up;label.pivot=Vector2.up;label.anchoredPosition=new Vector2(5,-63);label.sizeDelta=new Vector2(77,20);}
            }
            var save=JournalV2Nodes.Button(page.Middle,"Save","保存资料",null,18,388,210,45,JournalV2ButtonKind.Primary);
            var cancel=JournalV2Nodes.Button(page.Middle,"Cancel","取消修改",null,244,388,170,45);
            JournalV2Nodes.Text(page.Aside,"Heading","资料预览",24,16,314,30,22);
            var photo=JournalV2Nodes.Polaroid(page.Aside,"Preview",null,"",30,58);
            var preview=JournalV2Nodes.Text(page.Aside,"Bio","",24,316,314,120,16);
            page.Root.gameObject.AddComponent<JournalV2EditController>().Initialize(page,nickname,bio,avatars,ids,save,cancel,photo,preview);
            return page;
        }

        static JournalV2Page BuildAchievements(Transform parent)
        {
            var page=Frame(parent,"ach","成就","每一枚奖章都是一个故事","medal",new Color32(242,191,71,255));
            var tabs=CommunityTabs(page,new[]{"全部","已完成"},new[]{"medal","check"});
            var list=JournalV2Nodes.GridScroll(page.Middle,"Medals",0,0,710,440,3,new Vector2(215,258),new Vector2(18,18));
            JournalV2Nodes.Text(page.Aside,"Heading","成就进度",24,16,314,30,22);
            var photo=JournalV2Nodes.Polaroid(page.Aside,"Progress",JournalV2Theme.Sprite("icons/trophy.png"),"0 / 0",30,58);
            JournalV2Nodes.Text(page.Aside,"Note","成就随真实收获解锁。",24,316,314,96,16);
            page.Root.gameObject.AddComponent<JournalV2AchievementController>().Initialize(page,tabs,list,photo);return page;
        }
    }
}
