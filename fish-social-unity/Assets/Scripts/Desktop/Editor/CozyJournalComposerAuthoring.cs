#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    /// <summary>Authors the native journal composer once; runtime only binds its saved controls.</summary>
    public static class CozyJournalComposerAuthoring
    {
        public const string PrefabPath = "Assets/Resources/Desktop/Prefabs/JournalComposeModal.prefab";

        // Batch/authoring API retained; historical operation is not a daily menu command.
        public static void Bake()
        {
            var root = new GameObject("JournalComposeModal", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(DesktopSocialComposeModal));
            try
            {
                DesktopModalUi.Stretch(root);
                root.GetComponent<Image>().color = new Color(.33f, .21f, .12f, .56f);
                root.GetComponent<Image>().raycastTarget = true;
                Build(root);
                root.SetActive(false);
                CozyRedesignAuthoring.SaveJournalPrefab(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (saved == null || !saved.GetComponent<DesktopSocialComposeModal>().BindControls())
                throw new InvalidOperationException("Journal composer prefab did not preserve control references.");
        }

        static void Build(GameObject root)
        {
            var paper = DesktopModalUi.Panel("ModalPanel", root.transform, Color.clear);
            var rect = paper.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(1120, 752);
            var visual = DesktopModalUi.Panel("JournalPaper", paper.transform, Color.white);
            visual.GetComponent<Image>().raycastTarget = false;
            CozyJournalTheme.ApplySurface(visual.GetComponent<Image>(), "surfaces/modal-paper", true);
            visual.transform.SetAsFirstSibling();
            Label(paper.transform, "Title", "把这一刻，贴进动态手账", 32, 28, 950, 40, 28);
            var close = Button(paper.transform, "Close", "×", 1038, 26, 44, 44, null);
            Rule(paper.transform, "HeaderRule", 32, 82, 1056);
            Label(paper.transform, "Subtitle", "选一条鱼获，写下今天的湖光与好心情。", 32, 95, 1056, 27, 16);

            var card = DesktopModalUi.Panel("CatchPreview", paper.transform, Color.white);
            CozyJournalTheme.ApplySurface(card.GetComponent<Image>(), "surfaces/sticker-card");
            Place(card.GetComponent<RectTransform>(), 32, 138, 430, 310);
            card.transform.localEulerAngles = new Vector3(0, 0, 1);
            var well = new GameObject("ArtWell", typeof(RectTransform), typeof(CanvasRenderer), typeof(CozyProfileArtWell));
            well.transform.SetParent(card.transform, false);
            Place(well.GetComponent<RectTransform>(), 18, 16, 394, 176);
            var fill = well.GetComponent<CozyProfileArtWell>();
            fill.Top = new Color32(240, 229, 204, 255); fill.Bottom = new Color32(255, 248, 235, 255); fill.raycastTarget = false;
            Picture(card.transform, "FishArt", 42, 22, 346, 165, null);
            var name = Label(card.transform, "FishName", "挑一条今天的鱼获", 18, 201, 394, 36, 25);
            name.alignment = TextAnchor.MiddleCenter;
            Label(card.transform, "Size", "—", 75, 241, 130, 24, 18).alignment = TextAnchor.MiddleCenter;
            Label(card.transform, "Weight", "—", 225, 241, 130, 24, 18).alignment = TextAnchor.MiddleCenter;
            Label(card.transform, "SizeLabel", "体长", 75, 265, 130, 18, 12).alignment = TextAnchor.MiddleCenter;
            Label(card.transform, "WeightLabel", "体重", 225, 265, 130, 18, 12).alignment = TextAnchor.MiddleCenter;
            Label(card.transform, "CaughtAt", "选择后查看记录时间与水域", 18, 288, 394, 22, 13).alignment = TextAnchor.MiddleCenter;
            Picture(card.transform, "Tape", 165, -12, 100, 28, CozyJournalTheme.Sprite("decor/tape-gold-dots"));
            BuildFishGrid(paper.transform);

            Picture(paper.transform, "AuthorAvatar", 488, 138, 48, 48, DesktopDefaultAvatars.Portrait(null));
            Label(paper.transform, "AuthorName", "我的钓鱼手账", 548, 136, 540, 29, 20);
            Label(paper.transform, "AuthorSubtitle", "今天的湖畔来信", 548, 165, 540, 23, 14);
            Label(paper.transform, "StoryHeading", "这一刻的心情（选填）", 488, 207, 600, 28, 18);
            var story = DesktopModalUi.MakeInput(paper.transform, "Story", "比如：等了好久的一口，终于等到了这条漂亮的锦鲤。", DesktopSocialComposeModal.StoryLimit);
            Place(story.GetComponent<RectTransform>(), 488, 244, 600, 210);
            story.lineType = InputField.LineType.MultiLineNewline;
            story.textComponent.fontSize = 18; story.textComponent.lineSpacing = 1.9f;
            story.textComponent.alignment = TextAnchor.UpperLeft;
            story.textComponent.rectTransform.offsetMin = new Vector2(18, 14);
            story.textComponent.rectTransform.offsetMax = new Vector2(-18, -14);
            var placeholder = story.placeholder as Text;
            if (placeholder != null)
            {
                placeholder.fontSize = 18; placeholder.lineSpacing = 1.9f; placeholder.alignment = TextAnchor.UpperLeft;
                placeholder.rectTransform.offsetMin = new Vector2(18, 14); placeholder.rectTransform.offsetMax = new Vector2(-18, -14);
            }
            CozyJournalTheme.ApplyInput(story);
            for (var row = 1; row <= 6; row++)
            {
                Rule(story.transform, "WritingRule" + row, 4, row * 34, 592);
                story.transform.Find("WritingRule" + row).SetAsFirstSibling();
            }
            var counter = Label(paper.transform, "Counter", "0 / " + DesktopSocialComposeModal.StoryLimit, 948, 461, 140, 23, 13);
            counter.alignment = TextAnchor.MiddleRight;
            Label(paper.transform, "VisibilityHeading", "谁可以看", 488, 505, 90, 42, 16);
            StyleChoice(Button(paper.transform, "VisibilityPublic", "所有钓友", 585, 505, 134, 42, null));
            StyleChoice(Button(paper.transform, "VisibilityFriends", "仅好友", 731, 505, 122, 42, null));
            Label(paper.transform, "VisibilityHint", "出现在公开动态里，和更多钓友分享快乐。", 488, 558, 600, 34, 15);
            Label(paper.transform, "PublicationNote", "分享会保留背包里的鱼获。留空心情将使用鱼获描述。", 488, 601, 600, 40, 13);
            Rule(paper.transform, "FooterRule", 32, 669, 1056);
            var status = Label(paper.transform, "Status", "", 32, 688, 714, 42, 15);
            status.color = new Color(.75f, .35f, .24f);
            Button(paper.transform, "Cancel", "先不分享", 770, 688, 140, 44, null);
            var publish = Button(paper.transform, "Publish", "发布动态", 926, 688, 162, 44, null);
            CatTheme.StyleButton(publish, "social", true);

            // Kept inactive as the stable selected-index binding for existing integrations.
            // Players use the visible art cards and visibility buttons exclusively.
            var fishChoice = Choice(paper.transform, "FishChoice", 32, 122, 430, new[] { "正在读取背包鱼获…" });
            var visibilityChoice = Choice(paper.transform, "Visibility", 488, 505, 300, new[] { "所有钓友", "仅好友" });
            fishChoice.gameObject.SetActive(false); visibilityChoice.gameObject.SetActive(false);
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
                selectable.navigation = new Navigation { mode = Navigation.Mode.None };
            var serialized = new SerializedObject(root.GetComponent<DesktopSocialComposeModal>());
            serialized.FindProperty("_status").objectReferenceValue = status;
            serialized.FindProperty("_counter").objectReferenceValue = counter;
            serialized.FindProperty("_story").objectReferenceValue = story;
            serialized.FindProperty("_publish").objectReferenceValue = publish;
            serialized.FindProperty("_close").objectReferenceValue = close;
            serialized.FindProperty("_fishChoice").objectReferenceValue = fishChoice;
            serialized.FindProperty("_visibilityChoice").objectReferenceValue = visibilityChoice;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildFishGrid(Transform parent)
        {
            var scrollObject = new GameObject("FishCards", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollObject.transform.SetParent(parent, false);
            Place(scrollObject.GetComponent<RectTransform>(), 32, 468, 430, 180);
            scrollObject.GetComponent<Image>().color = new Color(1, 1, 1, .01f);
            var content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(scrollObject.transform, false);
            var r = content.GetComponent<RectTransform>(); r.anchorMin = Vector2.up; r.anchorMax = Vector2.one; r.pivot = Vector2.up; r.sizeDelta = Vector2.zero;
            var grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(100.75f, 82); grid.spacing = new Vector2(9, 9); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollObject.GetComponent<ScrollRect>(); scroll.content = r; scroll.viewport = scrollObject.GetComponent<RectTransform>();
            scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 28; scroll.movementType = ScrollRect.MovementType.Clamped;
            var template = Button(content.transform, "FishCardTemplate", "", 0, 0, 100.75f, 82, null);
            StyleChoice(template);
            var label = template.GetComponentInChildren<Text>();
            label.name = "Name"; label.fontSize = 13; Place(label.rectTransform, 3, 59, 94.75f, 20);
            Picture(template.transform, "Art", 6, 4, 88.75f, 54, null);
            template.gameObject.SetActive(false);
        }

        static void StyleChoice(Button button)
        {
            // Flat CSS card rather than a raised action button. The native selected wash
            // is linked to CozyButtonVisual so re-skinning preserves both fill and border.
            var visual = button.GetComponent<CozyButtonVisual>();
            visual.ConfigureSurface("surfaces/input-default", "surfaces/input-default", "surfaces/input-default",
                "surfaces/input-default", "surfaces/input-default");
            var selected = new GameObject("SelectionWash", typeof(RectTransform), typeof(CanvasRenderer), typeof(CozyCssGraphic));
            selected.transform.SetParent(button.transform, false);
            var rect = selected.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2, 2); rect.offsetMax = new Vector2(-2, -2);
            selected.transform.SetSiblingIndex(1);
            var wash = selected.GetComponent<CozyCssGraphic>();
            wash.shape = CozyCssGraphic.Shape.Paper; wash.radius = 10; wash.border = 2;
            wash.color = new Color32(255, 240, 216, 255); wash.borderColor = new Color32(252, 146, 60, 255); wash.raycastTarget = false;
            var state = button.gameObject.AddComponent<CozyJournalCardVisual>();
            state.Configure(visual, selected, null);
        }

        static Image Picture(Transform parent, string name, float x, float y, float width, float height, Sprite sprite)
        {
            var image = DesktopModalUi.Panel(name, parent, Color.white).GetComponent<Image>();
            Place(image.rectTransform, x, y, width, height); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            image.enabled = sprite != null; return image;
        }

        static void Rule(Transform parent, string name, float x, float y, float width)
        {
            var line = DesktopModalUi.Panel(name, parent, new Color32(219, 196, 161, 130));
            Place(line.GetComponent<RectTransform>(), x, y, width, 1); line.GetComponent<Image>().raycastTarget = false;
        }

        static Dropdown Choice(Transform parent, string name, float x, float y, float width, string[] options)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Dropdown));
            root.transform.SetParent(parent, false);
            Place(root.GetComponent<RectTransform>(), x, y, width, 46);
            CozyJournalTheme.ApplySurface(root.GetComponent<Image>(), "surfaces/input-default");
            var dropdown = root.GetComponent<Dropdown>();
            dropdown.targetGraphic = root.GetComponent<Image>();
            dropdown.captionText = Label(root.transform, "Caption", "", 13, 0, width - 54, 46, 18);
            Label(root.transform, "Arrow", "▾", width - 39, 0, 26, 46, 24);
            var template = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(root.transform, false);
            Place(template.GetComponent<RectTransform>(), 0, 48, width, 240);
            CozyJournalTheme.ApplySurface(template.GetComponent<Image>(), "surfaces/input-default");
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(template.transform, false);
            DesktopModalUi.Stretch(viewport);
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.up; contentRect.anchorMax = Vector2.one; contentRect.pivot = Vector2.up;
            contentRect.anchoredPosition = Vector2.zero; contentRect.sizeDelta = new Vector2(0, 46);
            var item = new GameObject("Item", typeof(RectTransform), typeof(Image), typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            var itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = Vector2.up; itemRect.anchorMax = Vector2.one; itemRect.pivot = Vector2.up;
            itemRect.sizeDelta = new Vector2(0, 46); itemRect.anchoredPosition = Vector2.zero;
            item.GetComponent<Image>().color = new Color(1, .98f, .94f);
            var toggle = item.GetComponent<Toggle>(); toggle.targetGraphic = item.GetComponent<Image>();
            var check = Label(item.transform, "Check", "✓", 8, 0, 24, 46, 18);
            toggle.graphic = check;
            dropdown.itemText = Label(item.transform, "Label", "", 38, 0, width - 50, 46, 18);
            var scroll = template.GetComponent<ScrollRect>();
            scroll.content = contentRect; scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            dropdown.template = template.GetComponent<RectTransform>();
            dropdown.options.Clear();
            foreach (var option in options) dropdown.options.Add(new Dropdown.OptionData(option));
            dropdown.RefreshShownValue();
            template.SetActive(false);
            return dropdown;
        }

        static Button Button(Transform parent, string name, string title, float x, float y, float width, float height, UnityEngine.Events.UnityAction action)
        {
            var button = DesktopModalUi.MakeButton(parent, name, title, action);
            Place(button.GetComponent<RectTransform>(), x, y, width, height);
            foreach (var label in button.GetComponentsInChildren<Text>()) { label.fontSize = 18; label.fontStyle = FontStyle.Normal; }
            return button;
        }

        static Text Label(Transform parent, string name, string title, float x, float y, float width, float height, int size)
        {
            var label = DesktopModalUi.Label(parent, name, title, size, TextAnchor.MiddleLeft);
            label.supportRichText = false;
            label.raycastTarget = false;
            Place(label.rectTransform, x, y, width, height);
            return label;
        }

        static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
#endif
