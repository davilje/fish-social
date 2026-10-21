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

        [MenuItem("Fish Social/Cozy Journal/Bake Publish Modal")]
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
            rect.sizeDelta = new Vector2(690, 720);
            var paperVisual = DesktopModalUi.Panel("JournalPaper", paper.transform, Color.white);
            paperVisual.GetComponent<Image>().raycastTarget = false;
            CozyJournalTheme.ApplySurface(paperVisual.GetComponent<Image>(), "surfaces/modal-paper", true);
            paperVisual.transform.SetAsFirstSibling();
            Label(paper.transform, "Title", "分享今天的鱼获", 30, 28, 560, 42, 27);
            var _close = Button(paper.transform, "Close", "×", 610, 26, 44, 44, null);
            Label(paper.transform, "FishHeading", "挑一条鱼", 32, 90, 620, 28, 19);
            var scroll = DesktopModalUi.MakeScroll(paper.transform, "FishChoices", out var _fishList);
            Place(scroll.GetComponent<RectTransform>(), 32, 122, 626, 164);
            scroll.viewport = scroll.GetComponent<RectTransform>();
            scroll.scrollSensitivity = 24f;
            scroll.GetComponent<Image>().color = new Color(1f, .94f, .82f, .24f);
            Label(paper.transform, "StoryHeading", "这一刻的心情（选填）", 32, 304, 480, 28, 19);
            var _counter = Label(paper.transform, "Counter", "0 / " + DesktopSocialComposeModal.StoryLimit, 530, 304, 128, 28, 16);
            _counter.alignment = TextAnchor.MiddleRight;
            var _story = DesktopModalUi.MakeInput(paper.transform, "Story", "今天在湖边，发生了什么开心事？", DesktopSocialComposeModal.StoryLimit);
            Place(_story.GetComponent<RectTransform>(), 32, 340, 626, 126);
            _story.lineType = InputField.LineType.MultiLineNewline;
            _story.textComponent.fontSize = 19;
            _story.textComponent.alignment = TextAnchor.UpperLeft;
            _story.textComponent.rectTransform.offsetMin = new Vector2(14, 12);
            _story.textComponent.rectTransform.offsetMax = new Vector2(-14, -12);
            var placeholder = _story.placeholder as Text;
            if (placeholder != null)
            {
                placeholder.fontSize = 19;
                placeholder.alignment = TextAnchor.UpperLeft;
                placeholder.rectTransform.offsetMin = new Vector2(14, 12);
                placeholder.rectTransform.offsetMax = new Vector2(-14, -12);
            }
            CozyJournalTheme.ApplyInput(_story);
            Label(paper.transform, "VisibilityHeading", "谁可以看到", 32, 480, 626, 28, 19);
            var _public = Button(paper.transform, "Public", "所有钓友", 32, 516, 302, 44, null);
            var _friends = Button(paper.transform, "Friends", "仅好友", 350, 516, 308, 44, null);
            var _status = Label(paper.transform, "Status", "", 32, 574, 626, 42, 17);
            var _publish = Button(paper.transform, "Publish", "贴进动态手账", 32, 630, 626, 50, null);
            CatTheme.StyleButton(_publish, "social", true);
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
                selectable.navigation = new Navigation { mode = Navigation.Mode.None };
            var serialized = new SerializedObject(root.GetComponent<DesktopSocialComposeModal>());
            serialized.FindProperty("_status").objectReferenceValue = _status;
            serialized.FindProperty("_counter").objectReferenceValue = _counter;
            serialized.FindProperty("_story").objectReferenceValue = _story;
            serialized.FindProperty("_publish").objectReferenceValue = _publish;
            serialized.FindProperty("_close").objectReferenceValue = _close;
            serialized.FindProperty("_public").objectReferenceValue = _public;
            serialized.FindProperty("_friends").objectReferenceValue = _friends;
            serialized.FindProperty("_fishList").objectReferenceValue = _fishList;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static Button Button(Transform parent, string name, string title, float x, float y, float width, float height, UnityEngine.Events.UnityAction action)
        {
            var button = DesktopModalUi.MakeButton(parent, name, title, action);
            Place(button.GetComponent<RectTransform>(), x, y, width, height);
            foreach (var label in button.GetComponentsInChildren<Text>()) label.fontSize = 20;
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
