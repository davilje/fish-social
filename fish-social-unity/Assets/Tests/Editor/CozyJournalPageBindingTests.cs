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
                var podium = (RectTransform)(instance.transform.Find("Scroll/Viewport/Content/Podium") ?? instance.transform.Find("Podium"));
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
        public void AlbumBindsBothPhotosAndDisablesPagingForEmptyAlbum()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PanelProfileHub.prefab"));
            try
            {
                instance.SetActive(false);
                var view = instance.GetComponent<DesktopProfileHubPanel>();
                view.Bind(null, null, null);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(DesktopProfileHubPanel).GetField("_hub", flags).SetValue(view,
                    new ProfileHubDto { albumPins = new AlbumCardDto[0] });
                typeof(DesktopProfileHubPanel).GetMethod("RenderAlbum", flags).Invoke(view, null);
                var album = instance.transform.Find("Shell/Content/Tab_相册");
                Assert.IsFalse(album.Find("Prev").GetComponent<Button>().interactable);
                Assert.IsFalse(album.Find("Next").GetComponent<Button>().interactable);
                var right = album.Find("AlbumStage/SlotR").GetComponent<Button>();
                Assert.IsNotNull(album.Find("AlbumStage/SlotL").GetComponent<Button>());
                Assert.IsNotNull(right);
                right.onClick.Invoke();
                Assert.AreEqual(1, typeof(DesktopProfileHubPanel).GetField("_albumSelection", flags).GetValue(view));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void AuthoredShowcaseActionsRespectReadOnlyAndCodexExploreUsesRoute()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PanelProfileHub.prefab"));
            try
            {
                instance.name = "PanelProfileHub";
                CozyRedesignAuthoring.ApplyDetailParity(instance);
                instance.SetActive(false);
                var view = instance.GetComponent<DesktopProfileHubPanel>();
                var openedMap = false;
                view.Bind(null, null, null, null, () => openedMap = true);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var hub = new ProfileHubDto { canEdit = true, showcaseFish = new FishInventoryItemDto[8] };
                typeof(DesktopProfileHubPanel).GetField("_hub", flags).SetValue(view, hub);
                typeof(DesktopProfileHubPanel).GetMethod("SelectShowcase", flags).Invoke(view, new object[] { 2 });
                var replace = instance.transform.Find("Shell/Content/Tab_展示柜/JournalReplace").GetComponent<Button>();
                var clear = instance.transform.Find("Shell/Content/Tab_展示柜/JournalClear").GetComponent<Button>();
                Assert.IsTrue(replace.gameObject.activeSelf);
                Assert.IsFalse(clear.interactable);
                hub.canEdit = false;
                typeof(DesktopProfileHubPanel).GetMethod("SelectShowcase", flags).Invoke(view, new object[] { 2 });
                Assert.IsFalse(replace.gameObject.activeSelf);
                Assert.IsFalse(clear.gameObject.activeSelf);
                Assert.IsNotNull(instance.transform.Find("JournalShowcasePicker/Viewport/Content"));
                Assert.IsFalse(instance.transform.Find("JournalShowcasePicker").gameObject.activeSelf);
                var species = DesktopFishCatalog.GetSpecies("koi");
                Assert.IsNotNull(species);
                typeof(DesktopProfileHubPanel).GetMethod("ShowCodexDetail", flags).Invoke(view, new object[] { species, null });
                var explore = instance.transform.Find("Shell/Content/Tab_图鉴/JournalExplore").GetComponent<Button>();
                Assert.IsTrue(explore.gameObject.activeSelf);
                explore.onClick.Invoke();
                Assert.IsTrue(openedMap);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void ProfileBiographyDraftUpdatesAuthoredPreviewImmediately()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PanelProfileEdit.prefab"));
            try
            {
                instance.name = "PanelProfileEdit";
                CozyRedesignAuthoring.ApplyDetailParity(instance);
                instance.GetComponent<DesktopProfileEditPanel>().Bind(null, null, null);
                var input = instance.transform.Find("Form/Bio").GetComponent<InputField>();
                input.text = "今天也想去湖边看看。";
                Assert.AreEqual(input.text, instance.transform.Find("JournalPreview/Bio").GetComponent<Text>().text);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void ComposerEntranceMatchesSourceAndImmediateCloseRestoresPaper()
        {
            var reduced = CozyMotion.Reduced;
            CozyMotion.SetPaused(false); CozyMotion.SetReduced(false);
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var composer = DesktopSocialComposeModal.Create(canvas.transform);
                Assert.IsNotNull(composer);
                composer.gameObject.SetActive(true);
                var paper = composer.transform.Find("ModalPanel") as RectTransform;
                var rest = paper.anchoredPosition;
                composer.PlayOpenMotion();
                Assert.AreEqual(rest + Vector2.down * 16, paper.anchoredPosition);
                Assert.AreEqual(0, paper.GetComponent<CanvasGroup>().alpha, .001f);
                CozyMotion.Tick(.1f);
                var t = CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutCubic, .5f);
                Assert.AreEqual(rest.y - 16 * (1 - t), paper.anchoredPosition.y, .001f);
                Assert.AreEqual(t, paper.GetComponent<CanvasGroup>().alpha, .001f);
                composer.Close(true);
                Assert.IsFalse(composer.gameObject.activeSelf);
                Assert.AreEqual(rest, paper.anchoredPosition);
                Assert.AreEqual(1, paper.GetComponent<CanvasGroup>().alpha, .001f);
                composer.gameObject.SetActive(true);
                composer.PlayOpenMotion();
                CozyMotion.Tick(.2f);
                Assert.AreEqual(rest, paper.anchoredPosition);
                Assert.AreEqual(1, paper.GetComponent<CanvasGroup>().alpha, .001f);
            }
            finally
            {
                CozyMotion.Cancel(canvas.transform as RectTransform, true);
                Object.DestroyImmediate(canvas); CozyMotion.SetReduced(reduced);
            }
        }

        [Test]
        public void LikeFailureDoesNotAnimateAndSuccessUsesSourceOutBackScale()
        {
            var reduced = CozyMotion.Reduced;
            CozyMotion.SetPaused(false); CozyMotion.SetReduced(false);
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SocialPostCard.prefab"));
            try
            {
                var card = instance.GetComponent<DesktopSocialPostCard>();
                card.Bind(new SocialPostDto { id = "post-motion", nickname = "钓友", text = "鱼获" }, null);
                var button = instance.transform.Find("Actions/LikeButton").GetComponent<CozyButtonVisual>();
                var apply = typeof(DesktopSocialPostCard).GetMethod("ApplyLikeResult", BindingFlags.NonPublic | BindingFlags.Instance);
                apply.Invoke(card, new object[] { false, true, 1 });
                Assert.AreEqual(Vector2.one, button.MotionScale);
                apply.Invoke(card, new object[] { true, true, 1 });
                Assert.AreEqual(Vector2.one * 1.1f, button.MotionScale);
                CozyMotion.Tick(.11f);
                var expected = Mathf.LerpUnclamped(1.1f, 1, CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutBack, .5f));
                Assert.AreEqual(expected, button.MotionScale.x, .001f);
                CozyMotion.Tick(.11f);
                Assert.AreEqual(Vector2.one, button.MotionScale);
                apply.Invoke(card, new object[] { true, false, 0 });
                typeof(DesktopSocialPostCard).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(card, null);
                Assert.AreEqual(Vector2.one, button.MotionScale);
            }
            finally
            {
                CozyMotion.Cancel(instance.transform as RectTransform, true);
                Object.DestroyImmediate(instance); CozyMotion.SetReduced(reduced);
            }
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
                foreach (var name in new[] { "_status", "_counter", "_story", "_publish", "_close", "_fishChoice", "_visibilityChoice" })
                    Assert.IsNotNull(fields.FindProperty(name).objectReferenceValue, name + " must survive prefab reload");
                var fishDropdown = saved.transform.Find("ModalPanel/FishChoice").GetComponent<Dropdown>();
                Assert.NotNull(fishDropdown.template);
                Assert.IsFalse(fishDropdown.template.gameObject.activeSelf);
                Assert.IsNotNull(fishDropdown.template.GetComponent<ScrollRect>().viewport.GetComponent<RectMask2D>());
                var logicalPanel = (RectTransform)saved.transform.Find("ModalPanel");
                Assert.AreEqual(new Vector2(1120, 752), logicalPanel.sizeDelta);
                Assert.AreEqual(0f, logicalPanel.GetComponent<Image>().color.a);
                var visualPaper = (RectTransform)logicalPanel.Find("JournalPaper");
                Assert.AreEqual(Vector2.zero, visualPaper.anchorMin);
                Assert.AreEqual(Vector2.one, visualPaper.anchorMax);
                Assert.AreEqual(new Vector2(-80, -80), visualPaper.offsetMin);
                Assert.AreEqual(new Vector2(80, 80), visualPaper.offsetMax);
                Assert.AreEqual(0, visualPaper.GetSiblingIndex());
                Assert.IsFalse(visualPaper.GetComponent<Image>().raycastTarget);

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
                paper.Find("Visibility").GetComponent<Dropdown>().value = 1;
                Assert.AreEqual("friends", typeof(DesktopSocialComposeModal).GetField("_visibility", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(composer));
                paper.Find("Visibility").GetComponent<Dropdown>().value = 0;
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
                Assert.NotNull(composer.transform.Find("ModalPanel/FishChoice").GetComponent<Dropdown>().template.GetComponent<ScrollRect>());
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
