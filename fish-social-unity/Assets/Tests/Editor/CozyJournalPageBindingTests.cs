#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyJournalPageBindingTests
    {
        const string Folder = "Assets/Resources/Desktop/Prefabs/";

        [Test]
        public void LeaderboardBindingPreservesAuthoredPodiumAndScrollGeometry()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PanelLeaderboard.prefab"));
            try
            {
                var podium = (RectTransform)instance.transform.Find("Podium");
                var scroll = (RectTransform)instance.transform.Find("Scroll");
                var podiumPosition = podium.anchoredPosition;
                var podiumSize = podium.sizeDelta;
                var scrollPosition = scroll.anchoredPosition;
                var scrollSize = scroll.sizeDelta;
                instance.GetComponent<DesktopLeaderboardPanel>().Bind(null, null);
                Assert.AreEqual(podiumPosition, podium.anchoredPosition);
                Assert.AreEqual(podiumSize, podium.sizeDelta);
                Assert.AreEqual(scrollPosition, scroll.anchoredPosition);
                Assert.AreEqual(scrollSize, scroll.sizeDelta);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void AuthoredReduceMotionToggleIsBoundAndRepeatedBindingDoesNotCloneIt()
        {
            var root = new GameObject("Settings", typeof(RectTransform));
            var wasReduced = CozyMotion.Reduced;
            try
            {
                var node = new GameObject("减少动效", typeof(RectTransform), typeof(Toggle));
                node.transform.SetParent(root.transform, false);
                var view = root.AddComponent<DesktopSettingsModalView>();
                var bind = typeof(DesktopSettingsModalView).GetMethod("EnsureReduceMotionToggle", BindingFlags.Instance | BindingFlags.NonPublic);
                bind.Invoke(view, null);
                bind.Invoke(view, null);
                Assert.AreEqual(1, root.transform.childCount);
                var toggle = node.GetComponent<Toggle>();
                toggle.isOn = !CozyMotion.Reduced;
                Assert.AreEqual(toggle.isOn, CozyMotion.Reduced);
            }
            finally
            {
                CozyMotion.SetReduced(wasReduced);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OpeningAnotherPlayerClearsPreviousEditableProfile()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PanelProfileHub.prefab"));
            try
            {
                instance.SetActive(false);
                var view = instance.GetComponent<DesktopProfileHubPanel>();
                view.Bind(null, null, null);
                var hubField = typeof(DesktopProfileHubPanel).GetField("_hub", BindingFlags.Instance | BindingFlags.NonPublic);
                hubField.SetValue(view, new ProfileHubDto { isSelf = true, canEdit = true });
                var edit = instance.transform.Find("Shell/Header/Edit").gameObject;
                edit.SetActive(true);
                view.ShowOtherPlayer("another-player");
                Assert.IsNull(hubField.GetValue(view));
                Assert.IsFalse(edit.activeSelf);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void IncomingLikeUpdatesTheCardWithoutDiscardingCommentDraft()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SocialPostCard.prefab"));
            try
            {
                var card = instance.GetComponent<DesktopSocialPostCard>();
                card.Bind(new SocialPostDto { id = "post-a", nickname = "钓友", text = "今天的鱼获" }, null);
                var input = instance.transform.Find("CommentsPanel/CommentInput").GetComponent<InputField>();
                input.text = "还在输入的评论";
                card.ApplyLiked(new PostLikedDto { postId = "post-a", playerId = "someone", likeCount = 12, liked = true });
                Assert.AreEqual("还在输入的评论", input.text);
                Assert.That(instance.transform.Find("Actions/LikeButton").GetComponentInChildren<Text>().text, Does.Contain("12"));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void ComposerSavedPrefabRetainsReferencesAndBindsInputVisibilityAndSubmission()
        {
            CozyJournalComposerAuthoring.Bake();
            var saved = PrefabUtility.LoadPrefabContents(CozyJournalComposerAuthoring.PrefabPath);
            try
            {
                Assert.IsFalse(saved.activeSelf);
                var fields = new SerializedObject(saved.GetComponent<DesktopSocialComposeModal>());
                foreach (var name in new[] { "_status", "_counter", "_story", "_publish", "_close", "_public", "_friends", "_fishList" })
                    Assert.IsNotNull(fields.FindProperty(name).objectReferenceValue, name + " must survive prefab reload");
                Assert.AreEqual(0, saved.transform.Find("ModalPanel/FishChoices/Content").childCount);
                var logicalPanel = (RectTransform)saved.transform.Find("ModalPanel");
                Assert.AreEqual(new Vector2(690, 720), logicalPanel.sizeDelta);
                Assert.AreEqual(0f, logicalPanel.GetComponent<Image>().color.a);
                var visualPaper = (RectTransform)logicalPanel.Find("JournalPaper");
                Assert.AreEqual(Vector2.zero, visualPaper.anchorMin);
                Assert.AreEqual(Vector2.one, visualPaper.anchorMax);
                Assert.AreEqual(new Vector2(-80, -80), visualPaper.offsetMin);
                Assert.AreEqual(new Vector2(80, 80), visualPaper.offsetMax);
                Assert.AreEqual(0, visualPaper.GetSiblingIndex());
                Assert.IsFalse(visualPaper.GetComponent<Image>().raycastTarget);
                var fishChoices = logicalPanel.Find("FishChoices");
                Assert.Less(fishChoices.GetComponent<Image>().color.a, .3f);
                Assert.IsNotNull(fishChoices.GetComponent<RectMask2D>());
                Assert.AreSame(fishChoices, fishChoices.GetComponent<ScrollRect>().viewport);
                Assert.AreEqual(new Vector2(32, -122), ((RectTransform)fishChoices).anchoredPosition);
            }
            finally { PrefabUtility.UnloadPrefabContents(saved); }
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var composer = DesktopSocialComposeModal.Create(canvas.transform);
                Assert.NotNull(composer);
                var paper = composer.transform.Find("ModalPanel");
                var geometry = ((RectTransform)paper).sizeDelta;
                var nodeCount = composer.GetComponentsInChildren<Transform>(true).Length;
                Assert.IsTrue(composer.BindControls());
                Assert.AreEqual(nodeCount, composer.GetComponentsInChildren<Transform>(true).Length);
                Assert.AreEqual(geometry, ((RectTransform)paper).sizeDelta);
                paper.Find("Story").GetComponent<InputField>().text = "湖边日记";
                Assert.AreEqual("4 / 160", paper.Find("Counter").GetComponent<Text>().text);
                paper.Find("Friends").GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual("friends", typeof(DesktopSocialComposeModal).GetField("_visibility", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(composer));
                paper.Find("Public").GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual("public", typeof(DesktopSocialComposeModal).GetField("_visibility", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(composer));
                paper.Find("Publish").GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual("请选择鱼获，并确认已登录。", paper.Find("Status").GetComponent<Text>().text);
                composer.gameObject.SetActive(true);
                paper.Find("Close").GetComponent<Button>().onClick.Invoke();
                Assert.IsFalse(composer.gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void ComposerUsesNativeLimitedInputAndCannotDismissAnInFlightPublication()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CozyJournalComposerAuthoring.PrefabPath) == null)
                CozyJournalComposerAuthoring.Bake();
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var composer = DesktopSocialComposeModal.Create(canvas.transform);
                var input = composer.transform.Find("ModalPanel/Story").GetComponent<InputField>();
                Assert.AreEqual(160, input.characterLimit);
                Assert.AreEqual(InputField.LineType.MultiLineNewline, input.lineType);
                Assert.IsFalse(input.textComponent.supportRichText);
                Assert.NotNull(composer.transform.Find("ModalPanel/FishChoices").GetComponent<ScrollRect>());
                composer.gameObject.SetActive(true);
                typeof(DesktopSocialComposeModal).GetField("_busy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(composer, true);
                composer.Close();
                Assert.IsTrue(composer.gameObject.activeSelf);
                composer.Close(true);
                Assert.IsFalse(composer.gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void ClosingSharedCommentsRestoresOwnershipBeforeCardCleanup()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SocialPostCard.prefab"));
            var host = new GameObject("CommentsHost", typeof(RectTransform));
            try
            {
                var card = instance.GetComponent<DesktopSocialPostCard>();
                card.Bind(null, null);
                var comments = instance.transform.Find("CommentsPanel");
                card.ConfigureCommentsHost((RectTransform)host.transform, null);
                comments.SetParent(host.transform, false);
                comments.gameObject.SetActive(true);
                card.CloseComments();
                Assert.AreSame(instance.transform, comments.parent);
                Assert.IsFalse(comments.gameObject.activeSelf);
                Assert.AreEqual(0, host.transform.childCount);
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(host);
            }
        }
    }
}
#endif
