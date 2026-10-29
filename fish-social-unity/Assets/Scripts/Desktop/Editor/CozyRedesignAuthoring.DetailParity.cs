#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        // Coordinates are local to the 1370 x 650 notebook: layouts.json stage origin (160,150).
        // CSS sources: style.css .mid/.polaroid/.sticker and pages.css .j-profile/.j-rank/.j-post.
        internal static void ApplyDetailParity(GameObject root)
        {
            switch (root.name)
            {
                case "PanelProfileHub": DpProfile(root.transform); break;
                case "PanelProfileEdit": DpProfileEdit(root.transform); break;
                case "AvatarChoice": DpAvatarChoice(root.transform); break;
                case "PanelAchievements": DpAchievements(root.transform, true); break;
                case "AchievementRow": DpMedal(root.transform); break;
                case "PanelLeaderboard": DpLeaderboard(root.transform); break;
                case "LeaderboardRow": DpRankRow(root.transform); break;
                case "PanelSocialFeed": DpFeed(root.transform); break;
                case "SocialPostCard": DpPost(root.transform); break;
                case "PostCommentRow": DpComment(root.transform); break;
                case "PanelSettings": DpSettings(root.transform); break;
                default: return;
            }
            Node(root.transform, "JournalDetailParity").gameObject.SetActive(false);
        }

        static readonly Color DpInk = new Color(117f / 255, 71f / 255, 46f / 255);
        static readonly Color DpSoft = new Color(117f / 255, 71f / 255, 46f / 255, .68f);

        static Text DpText(Transform root, string path, float x, float y, float w, float h, int size, bool soft = false, TextAnchor align = TextAnchor.MiddleLeft, string value = null)
        {
            var prior = root.Find(path)?.GetComponent<Text>();
            var label = Label(root, path, value ?? (prior != null ? prior.text : ""), x, y, w, h, size);
            label.fontStyle = FontStyle.Normal; label.color = soft ? DpSoft : DpInk;
            label.alignment = align; label.resizeTextForBestFit = false;
            label.supportRichText = false; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        static RectTransform DpSurface(Transform root, string path, string asset, float x, float y, float w, float h)
        {
            var logical = Place(root, path, x, y, w, h);
            var old = logical.GetComponent<Image>();
            if (old != null) { old.sprite = null; old.color = Color.clear; }
            var art = Node(logical, "JournalPaper");
            var image = art.GetComponent<Image>() ?? art.gameObject.AddComponent<Image>();
            CozyJournalTheme.ApplySurface(image, asset, true); image.raycastTarget = false;
            art.SetAsFirstSibling();
            return logical;
        }

        static void DpTape(Transform root, string path, float x, float y, float angle = 4, string style = "decor/tape-gold-dots")
        {
            var image = JournalSurface(root, path, style, x, y, 104, 26);
            image.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
            image.raycastTarget = false;
        }

        static void DpCenter(RectTransform rect)
        {
            var size = rect.sizeDelta;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition += new Vector2(size.x / 2, -size.y / 2);
        }

        static void DpPhoto(Transform root, string framePath, string imagePath, string captionPath, float x = 30, float y = 58, Sprite sprite = null)
        {
            var frame = DpSurface(root, framePath, "surfaces/polaroid-frame", x, y, 302, 236);
            DpCenter(frame);
            frame.localRotation = Quaternion.identity;
            frame.SetAsLastSibling();
            var bg = JournalSurface(root, framePath + "Inset", "surfaces/sticker-card", x + 12, y + 12, 278, 168);
            bg.color = new Color(.985f, .953f, .895f); bg.rectTransform.localRotation = Quaternion.identity;
            bg.transform.SetAsLastSibling();
            var art = root.Find(imagePath)?.GetComponent<Image>();
            var picture = Picture(root, imagePath, sprite ?? (art != null ? art.sprite : null), x + 29, y + 25, 244, 142);
            picture.rectTransform.localRotation = Quaternion.identity;
            picture.transform.SetAsLastSibling();
            var caption = DpText(root, captionPath, x + 12, y + 190, 278, 34, 24, false, TextAnchor.MiddleCenter);
            caption.rectTransform.localRotation = Quaternion.identity;
            caption.transform.SetAsLastSibling();
            DpTape(root, framePath + "Tape", x + 104, y - 14);
            root.Find(framePath + "Tape").SetAsLastSibling();
            var pose = frame.GetComponent<CozyJournalStaticPose>() ?? frame.gameObject.AddComponent<CozyJournalStaticPose>();
            pose.Configure(-2, frame.Find("JournalPaper").GetComponent<Image>(), bg, picture, caption, root.Find(framePath + "Tape").GetComponent<Image>());
        }

        static void DpButton(Transform root, string path, float x, float y, float w, float h, int size, string text = null, bool primary = false)
        {
            var prior = root.Find(path)?.GetComponentInChildren<Text>(true);
            var button = ButtonAt(root, path, text ?? (prior != null ? prior.text : ""), null, x, y, w, h, primary);
            foreach (var label in button.GetComponentsInChildren<Text>(true)) { label.fontSize = size; label.fontStyle = FontStyle.Normal; }
        }

        static void DpScroll(Transform root, string path, float x, float y, float w, float h, int columns, float cellW, float cellH, float gapX = 18, float gapY = 18)
        {
            var viewport = Place(root, path, x, y, w, h);
            var scroll = viewport.GetComponent<ScrollRect>();
            var content = scroll != null ? scroll.content : null;
            if (content == null) return;
            // LayoutGroup disallows sibling layout components, even when they are disabled.
            foreach (var old in content.GetComponents<LayoutGroup>())
                if (!(old is GridLayoutGroup)) UnityEngine.Object.DestroyImmediate(old);
            var grid = content.GetComponent<GridLayoutGroup>() ?? content.gameObject.AddComponent<GridLayoutGroup>();
            grid.enabled = true; grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = columns;
            grid.cellSize = new Vector2(cellW, cellH); grid.spacing = new Vector2(gapX, gapY);
            grid.padding = new RectOffset(14, 14, 14, 22); grid.childAlignment = TextAnchor.UpperLeft;
            content.anchorMin = Vector2.up; content.anchorMax = Vector2.one; content.pivot = Vector2.up;
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.enabled = true;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var image = viewport.GetComponent<Image>(); if (image != null) image.color = Color.clear;
        }

        static void DpProfile(Transform root)
        {
            var sidebar = root.Find("Shell/Sidebar");
            if (sidebar != null)
            {
                var sidebarImage = sidebar.GetComponent<Image>();
                if (sidebarImage != null) sidebarImage.color = Color.clear;
                DpSurface(sidebar, "JournalRail", "surfaces/paper-rail", 0, 0, 210, 496).SetAsFirstSibling();
            }
            for (var i = 0; i < 5; i++)
            {
                var tabPath = "Shell/Sidebar/Tab_" + new[] { "资料", "展示柜", "图鉴", "相册", "成就" }[i];
                DpButton(root, tabPath, 10, 12 + i * 88, 190, 76, 22);
                var button = root.Find(tabPath).GetComponent<Button>();
                var iconName = new[] { "user", "fish", "map", "heart", "medal" }[i];
                var icon = Picture(button.transform, "Icon", CatTheme.LoadCozy("icons/" + iconName + ".png", Vector4.zero), 30, 23, 30, 30);
                icon.enabled = true; icon.gameObject.SetActive(true);
                CatTheme.StyleButton(button, iconName);
                DpText(button.transform, "Label", 74, 0, 116, 76, 22, false, TextAnchor.MiddleLeft);
            }
            var profile = root.Find("Shell/Content/Tab_资料");
            DpText(profile, "JournalHeading", 0, 2, 710, 40, 24, value: "我的钓鱼时光");
            var banner = DpSurface(profile, "JournalIdentity", "surfaces/sticker-card", 15, 60, 680, 150);
            banner.SetAsFirstSibling();
            var landscape = Picture(banner, "Landscape", CatTheme.LoadCozy("scenes/pond-afternoon.png", Vector4.zero), 0, 0, 680, 150);
            var identityBase = JournalSurface(banner, "LandscapeBase", "surfaces/input-default", 0, 0, 680, 150);
            identityBase.color = new Color(245 / 255f, 228 / 255f, 200 / 255f);
            identityBase.transform.SetSiblingIndex(1);
            landscape.transform.SetAsLastSibling();
            landscape.color = new Color(1, 1, 1, .34f); landscape.preserveAspect = false;
            if (landscape.GetComponent<CozyPondPhotoCover>() == null) landscape.gameObject.AddComponent<CozyPondPhotoCover>();
            Picture(profile, "Avatar", null, 35, 82, 105, 105);
            DpText(profile, "DesignName", 160, 86, 500, 40, 30);
            DpText(profile, "DesignBio", 160, 132, 515, 54, 16);
            Hide(profile, "DesignId", "DesignInfoPaper", "DesignInfoHeading", "DesignInfoRows");
            Place(profile, "DesignStatCards", 15, 227, 680, 76);
            for (var i = 0; i < 3; i++)
            {
                var card = Place(profile, "DesignStatCards/Card" + i, i * 230, 0, 220, 76);
                DpSurface(card, "Backing", "surfaces/input-default", 0, 0, 220, 76).SetAsFirstSibling();
                Place(card, "Icon", 16, 23, 30, 30);
                DpText(card, "Value", 61, 12, 145, 29, 23);
                DpText(card, "Label", 61, 41, 145, 23, 14, true);
            }
            DpSurface(profile, "JournalProgress", "surfaces/input-default", 15, 317, 680, 98).SetAsFirstSibling();
            DpText(profile, "JournalProgress/Heading", 18, 10, 410, 25, 16, value: "距离下一个钓鱼小目标");
            DpText(profile, "JournalProgress/Level", 460, 10, 202, 25, 14, true, TextAnchor.MiddleRight);
            var bar = JournalSurface(profile, "JournalProgress/Track", "surfaces/progress-track", 18, 44, 644, 14);
            var fill = JournalSurface(bar.transform, "Fill", "surfaces/progress-fill", 0, 0, 644, 14);
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0;
            DpText(profile, "JournalProgress/Caption", 18, 65, 644, 23, 13, true);
            DpButton(root, "Shell/Header/Edit", 269, 541, 216, 43, 18, "编辑资料", true);
            DpButton(profile, "JournalShowcaseLink", 243, 429, 220, 43, 18, "我的展示柜");
            DpButton(profile, "JournalAlbumLink", 475, 429, 220, 43, 18, "翻翻相册");
            var aside = DpSurface(profile, "DesignShowcase", "surfaces/paper-aside", 730, 0, 362, 496);
            DpText(aside, "Heading", 24, 16, 314, 30, 22, value: "我的心头好");
            DpPhoto(aside, "JournalPhoto", "Art", "Species");
            DpText(aside, "Size", 24, 316, 314, 36, 18, true);
            DpButton(aside, "Open", 18, 420, 326, 58, 23, "整理我的展示柜", true);
            DpCollection(root, "展示柜", "ShowcaseGrid", 3, 215, 250, "我的展示柜");
            DpCollection(root, "图鉴", "CodexGrid", 4, 156, 188, "鱼类图鉴");
            DpAlbum(root);
            DpAchievements(root, false);
        }

        static void DpProfileEdit(Transform root)
        {
            Hide(root, "DesignFormPaper", "DesignAvatarPaper", "DesignShowcasePaper");
            DpText(root, "JournalEditHeading", 254, 114, 710, 40, 24, value: "编辑资料");
            DpSurface(root, "JournalEditRail", "surfaces/paper-rail", 24, 112, 210, 496).SetAsFirstSibling();
            DpText(root, "JournalEditRail/Heading", 20, 20, 170, 38, 22, value: "资料手账");
            DpText(root, "JournalEditRail/Note", 20, 80, 170, 120, 17, true, TextAnchor.UpperLeft, "喜欢的名字、头像，\n和一句话介绍。\n保存后更新资料。");
            Place(root, "Form", 272, 166, 674, 210);
            DpText(root, "Form/NicknameLabel", 0, 10, 674, 24, 18, value: "游戏昵称");
            Place(root, "Form/Nickname", 0, 42, 674, 44);
            DpText(root, "Form/BioLabel", 0, 98, 674, 24, 18, value: "个人简介");
            Place(root, "Form/Bio", 0, 130, 674, 70);
            foreach (var input in root.Find("Form").GetComponentsInChildren<InputField>(true))
            {
                CozyJournalTheme.ApplyInput(input);
                foreach (var text in input.GetComponentsInChildren<Text>(true)) { text.fontSize = 18; Inset(text.rectTransform, 13, 10, 13, 10); }
            }
            DpText(root, "AvatarsTitle", 272, 384, 674, 24, 18, value: "选择头像");
            DpScroll(root, "Avatars", 272, 416, 674, 100, 6, 87, 90, 18, 9);
            var grid = root.Find("Avatars/Viewport/Content").GetComponent<GridLayoutGroup>(); grid.padding = new RectOffset();
            Place(root, "Actions", 272, 548, 674, 45);
            DpButton(root, "Actions/Save", 0, 0, 210, 45, 19, "保存资料", true);
            var header = Place(root, "Header", 0, 0, 1370, 650); header.gameObject.SetActive(true);
            var headerImage = header.GetComponent<Image>(); if (headerImage != null) headerImage.color = Color.clear;
            Hide(header, "Title");
            DpButton(root, "Header/Back", 498, 548, 170, 45, 19, "取消修改");
            DpSurface(root, "JournalPreview", "surfaces/paper-aside", 984, 112, 362, 496).SetAsFirstSibling();
            DpText(root, "JournalPreview/Heading", 24, 16, 314, 30, 22, value: "资料预览");
            DpPhoto(root.Find("JournalPreview"), "JournalPhoto", "Avatar", "Name");
            DpText(root, "JournalPreview/Bio", 24, 314, 314, 52, 15, true, TextAnchor.UpperLeft);
            DpText(root, "ShowcaseTitle", 1008, 480, 314, 26, 17, value: "展示鱼获 · 点击更换");
            Place(root, "Showcase", 1002, 514, 326, 76);
            Grid(root, "Showcase/Viewport/Content", 2, 146, 120);
            root.Find("Picker").SetAsLastSibling();
        }

        static void DpAvatarChoice(Transform root)
        {
            Place(root, "", 0, 0, 87, 90);
            Place(root, "DesignItemArt", 13, 5, 60, 54);
            DpText(root, "Label", 5, 63, 77, 20, 12, false, TextAnchor.MiddleCenter);
        }

        static void DpCollection(Transform root, string name, string scroll, int columns, int w, int h, string title)
        {
            var tab = root.Find("Shell/Content/Tab_" + name); if (tab == null) return;
            Hide(tab, "DesignShowcaseGrid", "DesignCodexGrid", "DesignDetailArtBacking");
            DpText(tab, "DesignGridHeading", 0, 2, 710, 40, 24, value: title);
            DpScroll(tab, scroll, 0, 48, 710, 440, columns, w, h);
            DpSurface(tab, "DesignDetailPaper", "surfaces/paper-aside", 730, 0, 362, 496).SetAsFirstSibling();
            DpText(tab, "DesignDetailHeading", 754, 16, 314, 30, 22);
            DpPhoto(tab, "JournalCollectionPhoto", "DesignDetailArt", "JournalPhotoCaption", 760, 58);
            DpText(tab, "Detail", 754, 316, 314, 160, 15, true, TextAnchor.UpperLeft);
            if (name == "展示柜")
            {
                DpText(tab, "Detail", 754, 300, 314, 26, 18);
                var detailRows = tab.Find("ShowcaseDetailRows");
                if (detailRows != null) for (var rowIndex = detailRows.childCount - 1; rowIndex >= 5; rowIndex--) UnityEngine.Object.DestroyImmediate(detailRows.GetChild(rowIndex).gameObject);
                DetailRows(tab, "ShowcaseDetailRows", 754, 332, 314, 5, 16);
                DpButton(tab, "JournalReplace", 748, 420, 210, 43, 18, "更换鱼获", true);
                DpButton(tab, "JournalClear", 970, 420, 104, 43, 18, "清空");
                DpShowcasePicker(root);
                foreach (var label in tab.Find("ShowcaseDetailRows").GetComponentsInChildren<Text>(true)) label.fontSize = 13;
            }
            if (name == "图鉴")
            {
                DpButton(tab, "JournalExplore", 802, 428, 218, 42, 18, "去探索新的水域", true);
                tab.Find("JournalExplore").gameObject.SetActive(false);
            }
            var template = Place(tab, "JournalCardTemplate", 0, 0, w, h);
            var image = template.GetComponent<Image>() ?? template.gameObject.AddComponent<Image>(); image.color = Color.clear; image.raycastTarget = true;
            var button = template.GetComponent<Button>() ?? template.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            CatTheme.StyleButton(button);
            DpSurface(template, "JournalCardPaper", "surfaces/sticker-card", 0, 0, w, h).SetAsFirstSibling();
            Picture(template, "DesignItemArt", null, 12, 12, w - 24, name == "展示柜" ? 148 : 100);
            DpText(template, "Label", 10, name == "展示柜" ? 174 : 124, w - 20, 60, 18, false, TextAnchor.MiddleCenter);
            DpTape(template, "JournalTape", 34, -9);
            DpText(template, "JournalStamp/Label", 0, 0, 54, 54, 13, false, TextAnchor.MiddleCenter, "已收录");
            JournalSurface(template, "JournalStamp", "decor/stamp-ring", w - 43, -12, 54, 54).transform.SetAsLastSibling();
            template.Find("JournalStamp").localEulerAngles = new Vector3(0, 0, 14);
            template.Find("JournalStamp").gameObject.SetActive(false);
            DpCenter(template);
            template.gameObject.SetActive(false);
        }

        static void DpShowcasePicker(Transform root)
        {
            var picker = root.Find("JournalShowcasePicker");
            if (picker == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Desktop/Prefabs/PanelProfileEdit.prefab")?.transform.Find("Picker");
                if (source == null) return;
                picker = UnityEngine.Object.Instantiate(source.gameObject, root, false).transform;
                picker.name = "JournalShowcasePicker";
            }
            DpSurface(root, "JournalShowcasePicker", "surfaces/modal-paper", 250, 42, 850, 554);
            DpText(picker, "Title", 26, 20, 690, 35, 23, value: "选择展示鱼获");
            DpScroll(picker, "", 250, 42, 850, 554, 4, 180, 150);
            Place(picker, "Viewport", 24, 70, 802, 394);
            DpButton(picker, "Clear", 24, 488, 190, 42, 18, "清空展示格");
            DpButton(picker, "Close", 656, 488, 170, 42, 18, "关闭");
            picker.SetAsLastSibling(); picker.gameObject.SetActive(false);
        }

        static void DpAlbum(Transform root)
        {
            var tab = root.Find("Shell/Content/Tab_相册"); if (tab == null) return;
            Hide(tab, "DesignAlbumStage");
            DpText(tab, "JournalHeading", 0, 2, 710, 40, 24, value: "湖畔相册");
            Place(tab, "AlbumStage", 18, 64, 674, 332);
            var stage = tab.Find("AlbumStage"); var bg = stage.GetComponent<Image>(); if (bg != null) bg.color = Color.clear;
            var i = 0;
            foreach (var name in new[] { "SlotL", "SlotR" })
            {
                var card = DpSurface(stage, name, "surfaces/polaroid-frame", 7 + i * 337, 13, 311, 300);
                DpCenter(card); card.localRotation = Quaternion.identity;
                Place(card, "Photo", 12, 12, 287, 190);
                DpText(card, "Body", 12, 211, 287, 80, 16, true, TextAnchor.UpperCenter);
                DpTape(card, "JournalTape", 100, -14, i == 0 ? 4 : -4, i == 0 ? "decor/tape-gold-dots" : "decor/tape-blue-stripe");
                var pose = card.GetComponent<CozyJournalStaticPose>() ?? card.gameObject.AddComponent<CozyJournalStaticPose>();
                pose.Configure(i == 0 ? -1.3f : 1.3f, card.GetComponentsInChildren<Graphic>(true));
                i++;
            }
            DpButton(tab, "Prev", 130, 417, 150, 42, 18, "‹ 上一页");
            DpButton(tab, "Next", 430, 417, 150, 42, 18, "下一页 ›");
            Hide(tab, "Prev_0", "Next_0");
            DpText(tab, "Page", 300, 417, 110, 42, 18, false, TextAnchor.MiddleCenter);
            var aside = DpSurface(tab, "JournalAlbumAside", "surfaces/paper-aside", 730, 0, 362, 496);
            DpText(aside, "Heading", 24, 16, 314, 30, 22, value: "回忆便签");
            DpPhoto(aside, "JournalPhoto", "Art", "Caption");
            DpText(aside, "Detail", 24, 316, 314, 130, 16, true, TextAnchor.UpperLeft);
        }

        static void DpAchievements(Transform root, bool standalone)
        {
            var tab = root.Find("Shell/Content/Tab_成就"); if (tab == null) return;
            DpText(tab, "DesignHeading", 0, 2, 220, 40, 24, value: "全部成就");
            DpButton(tab, "DesignFilterAll", 246, 5, 136, 34, 16, "全部");
            DpButton(tab, "DesignFilterCompleted", 396, 5, 136, 34, 16, "已完成");
            DpButton(tab, "DesignFilterPending", 546, 5, 136, 34, 16, "未完成");
            DpScroll(tab, "Achievements", 0, 48, 710, 440, 3, 215, 258);
            var aside = standalone ? root.Find("DesignAchievementSummary") : Node(tab, "JournalAchievementSummary");
            if (!standalone) DpSurface(tab, "JournalAchievementSummary", "surfaces/paper-aside", 730, 0, 362, 496);
            DpText(aside, "Heading", 24, 16, 314, 30, 22, value: "成就进度");
            DpPhoto(aside, "JournalPhoto", "Icon", "Value", sprite: CatTheme.LoadCozy("icons/trophy.png", Vector4.zero));
            Hide(aside, "Caption");
            DpText(aside, "Note", 24, 316, 314, 96, 16, true, TextAnchor.UpperLeft, "每一枚奖章，都是一段钓鱼故事。\n成就随真实收获解锁。");
        }

        static void DpMedal(Transform root)
        {
            Place(root, "", 0, 0, 215, 258);
            DpSurface(root, "JournalMedal", "surfaces/sticker-card", 56, 28, 92, 92).SetAsFirstSibling();
            Picture(root, "Reflow_AchievementSeal", CatTheme.LoadCozy("icons/medal.png", Vector4.zero), 73, 45, 58, 58);
            DpText(root, "Label", 10, 148, 195, 28, 20, false, TextAnchor.MiddleCenter);
            DpText(root, "JournalDescription", 10, 182, 195, 48, 15, true, TextAnchor.UpperCenter);
            var stamp = JournalSurface(root, "DesignStateBadge", "decor/stamp-ring", 153, -16, 74, 74);
            stamp.rectTransform.localEulerAngles = new Vector3(0, 0, 14);
            DpText(root, "DesignState", 153, -16, 74, 74, 18, false, TextAnchor.MiddleCenter).rectTransform.localEulerAngles = new Vector3(0, 0, 14);
            var element = root.GetComponent<LayoutElement>() ?? root.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 258;
        }

        static void DpLeaderboard(Transform root)
        {
            var nestedPodium = root.Find("Scroll/Viewport/Content/Podium");
            if (nestedPodium != null)
            {
                if (root.Find("Podium") != null) UnityEngine.Object.DestroyImmediate(nestedPodium.gameObject);
                else nestedPodium.SetParent(root, false);
            }
            Hide(root, "Reflow_PodiumPaper", "DesignTableHeading", "DesignRankColumn", "DesignNameColumn", "DesignValueColumn");
            DpText(root, "JournalBoardHeading", 254, 114, 710, 40, 24, value: "今日大鱼");
            Place(root, "Podium", 269, 164, 680, 220);
            for (var rank = 1; rank <= 3; rank++)
            {
                var first = rank == 1;
                var card = Place(root, "Podium/Slot" + rank, rank == 2 ? 7 : rank == 1 ? 236 : 465, first ? 12 : 39, 208, first ? 192 : 165);
                card.localRotation = Quaternion.identity;
                CatTheme.StyleButton(card.GetComponent<Button>());
                JournalCard(card.gameObject);
                DpSurface(card, "DesignBacking", "surfaces/sticker-card", 0, 0, 208, first ? 192 : 165).SetAsFirstSibling();
                Place(card, "Avatar", first ? 66 : 72, 11, first ? 76 : 65, first ? 76 : 65);
                DpText(card, "Rank", 14, 10, 34, 34, 27, true, TextAnchor.MiddleCenter);
                DpText(card, "Nickname", 12, first ? 94 : 83, 184, 31, 22, false, TextAnchor.MiddleCenter);
                DpText(card, "Value", 12, first ? 127 : 116, 184, 26, 19, true, TextAnchor.MiddleCenter);
                DpText(card, "JournalRankLabel", 34, first ? 172 : 145, 140, 30, 13, false, TextAnchor.MiddleCenter, "第" + rank + "名");
                Hide(card, "DesignBacking");
                DpTape(card, "JournalTape", -18, -8, 28);
                card.Find("JournalTape").GetComponent<Image>().color = new Color(1, 1, 1, .95f);
                card.GetComponent<CozyJournalCardVisual>()?.RefreshNow();
                DpCenter(card);
                var pose = card.GetComponent<CozyJournalStaticPose>() ?? card.gameObject.AddComponent<CozyJournalStaticPose>();
                pose.Configure(first ? .3f : rank == 2 ? -.41f : .42f, card.GetComponentsInChildren<Graphic>(true));
            }
            Place(root, "Scroll", 254, 160, 710, 440);
            var scroll = root.Find("Scroll").GetComponent<ScrollRect>();
            var layout = scroll != null && scroll.content != null ? scroll.content.GetComponent<VerticalLayoutGroup>() : null;
            if (layout != null) { layout.spacing = 17; layout.padding = new RectOffset(21, 21, 232, 18); }
            if (scroll != null && scroll.content != null)
            {
                var podium = root.Find("Podium"); podium.SetParent(scroll.content, false);
                var element = podium.GetComponent<LayoutElement>() ?? podium.gameObject.AddComponent<LayoutElement>();
                element.ignoreLayout = true;
                Place(scroll.content, "Podium", 15, 4, 680, 220);
            }
            DpText(root, "MyRank", 254, 610, 1068, 26, 17, true);
            var aside = root.Find("DesignPlayerCard");
            DpText(aside, "Heading", 24, 16, 314, 30, 22, value: "鱼获详情");
            DpPhoto(aside, "JournalPhoto", "Avatar", "Name");
            DpText(aside, "Bio", 24, 302, 314, 90, 16, true, TextAnchor.UpperLeft).lineSpacing = 1.15f;
            DpText(aside, "Value", 24, 394, 314, 24, 15);
            DpButton(aside, "OpenProfile", 72, 428, 218, 42, 18, "看看钓友的收藏", true);
            var motion = root.GetComponent<CozyJournalPageMotion>() ?? root.gameObject.AddComponent<CozyJournalPageMotion>();
            motion.Configure(root.Find("JournalBoardHeading"), aside, root.Find("MyRank"), root.Find("Scroll"));
        }

        static void DpRankRow(Transform root)
        {
            CatTheme.StyleButton(root.GetComponent<Button>());
            JournalCard(root.gameObject);
            Place(root, "", 0, 0, 668, 75);
            DpText(root, "Rank", 18, 7, 38, 61, 25, true, TextAnchor.MiddleCenter);
            Place(root, "Avatar", 72, 13, 48, 48);
            DpText(root, "Nickname", 136, 7, 330, 61, 19);
            DpText(root, "Value", 482, 7, 168, 61, 23, false, TextAnchor.MiddleRight);
            var element = root.GetComponent<LayoutElement>() ?? root.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 75;
            DpTape(root, "JournalTape", -14, -5, 28, "decor/tape-pink-stripe");
            root.Find("JournalTape").GetComponent<Image>().color = new Color(1, 1, 1, .95f);
            root.GetComponent<CozyJournalCardVisual>()?.RefreshNow();
            var tape = root.Find("JournalTape") as RectTransform; tape.sizeDelta = new Vector2(60, 18);
        }

        static void DpFeed(Transform root)
        {
            DpText(root, "JournalFeedHeading", 254, 114, 550, 40, 24, value: "公开动态");
            DpButton(root, "DesignFeedInfo/Share", -136, 1, 108, 38, 18, "分享鱼获", true);
            Place(root, "Scroll", 254, 160, 710, 440);
            var content = root.Find("Scroll").GetComponent<ScrollRect>()?.content;
            var layout = content != null ? content.GetComponent<VerticalLayoutGroup>() : null;
            if (layout != null) { layout.padding = new RectOffset(17, 17, 19, 22); layout.spacing = 29; }
            DpText(root, "DesignFeedInfo/Heading", 24, 16, 314, 30, 22, value: "钓友留言");
            DpSurface(root, "DesignFeedInfo/JournalSummary", "surfaces/input-default", 20, 60, 322, 82).SetAsFirstSibling();
            Picture(root, "DesignFeedInfo/JournalSummaryArt", null, 34, 69, 97, 64);
            DpText(root, "DesignFeedInfo/DetailTitle", 141, 69, 181, 64, 16);
            Place(root, "DesignFeedInfo/CommentsHost", 20, 154, 322, 310);
        }

        static void DpPost(Transform root)
        {
            Place(root, "", 0, 0, 676, 271);
            Place(root, "Header", 18, 16, 640, 48);
            Picture(root, "Header/JournalAvatar", null, 0, 0, 48, 48);
            DpText(root, "Header/AuthorText", 60, 0, 470, 26, 20);
            DpText(root, "Header/JournalTime", 60, 30, 470, 17, 13, true);
            DpText(root, "Header/JournalVisibility", 548, 11, 92, 26, 12, true, TextAnchor.MiddleRight);
            DpText(root, "BodyText", 18, 76, 640, 29, 17).lineSpacing = 1.7f;
            DpSurface(root, "JournalCatch", "surfaces/input-default", 18, 117, 640, 88).SetAsFirstSibling();
            Picture(root, "JournalCatch/Art", null, 16, 8, 116, 72);
            DpText(root, "JournalCatch/Name", 149, 12, 310, 26, 20);
            DpText(root, "JournalCatch/Pond", 473, 24, 151, 40, 14, true, TextAnchor.MiddleRight);
            DpText(root, "FishInfoText", 167, 163, 320, 20, 15, true);
            Hide(root, "Photo");
            Place(root, "Actions", 18, 219, 640, 36);
            DpButton(root, "Actions/LikeButton", 0, 0, 120, 36, 15);
            DpButton(root, "Actions/CommentsButton", 132, 0, 100, 36, 15);
            DpText(root, "Actions/JournalHint", 370, 5, 270, 26, 12, true, TextAnchor.MiddleRight, "看看钓友的湖畔来信");
            var comments = root.Find("CommentsPanel");
            var viewport = comments.Find("CommentsViewport") as RectTransform;
            Inset(viewport, 0, 0, 0, 106);
            foreach (var name in new[] { "CommentInput", "SendButton" })
            {
                var rect = comments.Find(name) as RectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.right; rect.pivot = Vector2.zero;
                rect.offsetMin = new Vector2(0, name == "CommentInput" ? 62 : 9);
                rect.offsetMax = new Vector2(0, name == "CommentInput" ? 101 : 52);
                foreach (var text in rect.GetComponentsInChildren<Text>(true)) text.fontSize = name == "CommentInput" ? 15 : 19;
            }
        }

        static void DpComment(Transform root)
        {
            var label = root.Find("Text") ?? root.Find("CommentText");
            if (label != null) { var text = label.GetComponent<Text>(); text.fontSize = 15; text.color = DpInk; text.lineSpacing = 1.6f; }
            var layout = root.GetComponent<LayoutElement>(); if (layout != null) layout.minHeight = 75;
        }

        static void DpSettings(Transform root)
        {
            DpText(root, "DesignSettingsHeading", 254, 114, 710, 40, 24);
            Place(root, "SettingsViewport", 254, 160, 710, 440);
            Hide(root, "DesignSettingsListPaper");
            var content = root.Find("SettingsViewport/SettingsContent");
            Place(content, "窗口模式", 18, -42, 674, 30);
            Place(content, "通知", 18, -42, 674, 30);
            var names = new[] { "1280×720", "1600×900", "1920×1080", "2560×1440" };
            for (var i = 0; i < names.Length; i++) DpButton(content, names[i], 18 + (i % 2) * 349, 18 + (i / 2) * 78, 325, 58, 23);
            DpText(content, "DesignDisplayHeading", 22, 186, 650, 26, 20, value: "显示模式");
            DpSurface(content, "JournalDisplayTrack", "surfaces/pond-segment-track", 18, 222, 674, 48).SetAsFirstSibling();
            DpButton(content, "JournalWindowed", 22, 226, 222, 40, 18, "窗口");
            DpButton(content, "无边框", 244, 226, 222, 40, 18);
            DpButton(content, "全屏", 466, 226, 222, 40, 18);
            foreach (var mode in new[] { "JournalWindowed", "无边框", "全屏" })
                content.Find(mode).GetComponent<CozyButtonVisual>().ConfigureSurface("surfaces/pond-segment-empty", "surfaces/pond-segment-empty", null, "surfaces/pond-segment-selected");
            var toggles = new[] { "启用通知", "免打扰", "鱼咬钩通知", "好友邀请通知", "连接错误通知" };
            for (var i = 0; i < toggles.Length; i++)
            {
                var toggle = Place(content, toggles[i], 18, 14 + i * 78, 674, 64);
                foreach (var label in toggle.GetComponentsInChildren<Text>(true)) { label.fontSize = 21; label.color = DpInk; }
                var track = toggle.Find("JournalTrack") as RectTransform;
                if (track != null) { track.anchoredPosition = new Vector2(-92, -13); track.sizeDelta = new Vector2(74, 38); }
            }
            Place(content, "模拟连接错误", 18, 412, 325, 48);
            DpText(root, "DesignSettingsInfo/Heading", 24, 16, 314, 30, 22, value: "当前设置");
            DpText(root, "DesignSettingsInfo/Body", 26, 76, 310, 280, 18, true, TextAnchor.UpperLeft);
            var paw = Picture(root, "DesignSettingsInfo/JournalPaw", CozyJournalTheme.Sprite("decor/paw-mark"), 240, 372, 92, 92);
            paw.color = new Color(.84f, .73f, .55f, .4f); paw.rectTransform.localEulerAngles = new Vector3(0, 0, 16);
        }
    }
}
#endif
