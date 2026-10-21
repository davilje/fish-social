#if UNITY_INCLUDE_TESTS
using FishSocial.Desktop;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyJournalMotionTests
    {
        GameObject root;
        int preference;

        [SetUp]
        public void SetUp()
        {
            preference = PlayerPrefs.GetInt(CozyMotion.ReducePrefsKey, 0);
            CozyMotion.SetPaused(false);
            CozyMotion.SetReduced(false);
            root = new GameObject("JournalMotionTests", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            CozyMotion.CancelPageTransition();
            CozyMotion.Cancel(root.transform as RectTransform, true);
            // These are deterministic EditMode tests; explicitly deliver runtime lifecycle callbacks.
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour.GetType().Namespace == "FishSocial.Desktop")
                    Lifecycle(behaviour, "OnDisable");
            Object.DestroyImmediate(root);
            CozyMotion.SetPaused(false);
            CozyMotion.SetReduced(preference == 1);
        }

        static void Lifecycle(MonoBehaviour behaviour, string method)
        {
            behaviour.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic
                | BindingFlags.Public | BindingFlags.DeclaredOnly)?.Invoke(behaviour, null);
        }

        RectTransform Rect(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            return go.transform as RectTransform;
        }

        [Test]
        public void OneLoopOwnsMotionAndHiddenOwnerRestoresThenYields()
        {
            var navigation = Rect("Navigation");
            var marker = Rect("SelectedMarker");
            marker.anchoredPosition = new Vector2(37, 61);
            marker.localRotation = Quaternion.Euler(0, 0, -7);
            CozyMotion.Float(navigation, 0, CozyMotion.NavigationPriority);
            CozyMotion.Float(marker, 0, CozyMotion.MapPriority);
            CozyMotion.Tick(.3f);
            Assert.AreSame(marker, CozyMotion.ActiveLoopOwner);
            Assert.AreEqual(Vector2.zero, navigation.anchoredPosition);
            marker.gameObject.SetActive(false);
            CozyMotion.Tick(.1f);
            Assert.AreSame(navigation, CozyMotion.ActiveLoopOwner);
            Assert.AreEqual(new Vector2(37, 61), marker.anchoredPosition);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0, 0, -7), marker.localRotation), .01f);
            marker.gameObject.SetActive(true);
            CozyMotion.Tick(.1f);
            Assert.AreSame(marker, CozyMotion.ActiveLoopOwner);
        }

        [Test]
        public void ReduceDuringPageTransitionSettlesLatestPageAndRestoresInput()
        {
            var oldPage = Rect("OldPage");
            var newPage = Rect("NewPage");
            newPage.anchoredPosition = new Vector2(100, 200);
            var callbacks = 0;
            CozyMotion.Flip(oldPage.gameObject, newPage.gameObject, () => callbacks++);
            CozyMotion.Tick(.05f);
            CozyMotion.SetReduced(true);
            Assert.IsFalse(oldPage.gameObject.activeSelf);
            Assert.IsTrue(newPage.gameObject.activeSelf);
            Assert.AreEqual(new Vector2(100, 200), newPage.anchoredPosition);
            Assert.IsTrue(newPage.GetComponent<CanvasGroup>().blocksRaycasts);
            Assert.AreEqual(1, newPage.GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(1, callbacks);
            CozyMotion.Tick(1);
            Assert.AreEqual(1, callbacks);
        }

        [Test]
        public void RapidPageTransitionsDoNotRunStaleCompletion()
        {
            var first = Rect("First");
            var second = Rect("Second");
            var third = Rect("Third");
            var stale = 0;
            var current = 0;
            CozyMotion.Flip(first.gameObject, second.gameObject, () => stale++);
            CozyMotion.Tick(.03f);
            CozyMotion.Flip(second.gameObject, third.gameObject, () => current++);
            CozyMotion.Tick(.15f);
            CozyMotion.Tick(.3f);
            Assert.AreEqual(0, stale);
            Assert.AreEqual(1, current);
            Assert.IsFalse(first.gameObject.activeSelf);
            Assert.IsFalse(second.gameObject.activeSelf);
            Assert.AreEqual(Vector2.zero, second.anchoredPosition);
            Assert.AreEqual(Vector2.zero, third.anchoredPosition);
        }

        [Test]
        public void ReduceDuringPressReturnsToRestAndFuturePressDoesNotMove()
        {
            var target = Rect("PressTarget");
            CozyMotion.PressDown(target, true);
            CozyMotion.Tick(.03f);
            CozyMotion.SetReduced(true);
            Assert.AreEqual(Vector3.one, target.localScale);
            CozyMotion.PressDown(target, true);
            CozyMotion.Tick(.1f);
            Assert.AreEqual(Vector3.one, target.localScale);
            CozyMotion.Shake(target);
            CozyMotion.Tick(.1f);
            Assert.AreEqual(Vector2.zero, target.anchoredPosition);
        }

        [Test]
        public void PauseSettlesPendingToastAndResumesOneLoop()
        {
            var toast = Rect("Toast");
            var navigation = Rect("Navigation");
            toast.anchoredPosition = new Vector2(-20, 300);
            CozyMotion.Float(navigation, 0, CozyMotion.NavigationPriority);
            CozyMotion.SlideToast(toast);
            CozyMotion.Tick(.05f);
            CozyMotion.SetPaused(true);
            Assert.IsNull(CozyMotion.ActiveLoopOwner);
            Assert.AreEqual(new Vector2(-20, 300), toast.anchoredPosition);
            Assert.AreEqual(Vector2.zero, navigation.anchoredPosition);
            Assert.AreEqual(1, toast.GetComponent<CanvasGroup>().alpha);
            CozyMotion.SetPaused(false);
            CozyMotion.Tick(.3f);
            Assert.AreSame(navigation, CozyMotion.ActiveLoopOwner);
            Assert.AreNotEqual(Vector2.zero, navigation.anchoredPosition);
        }

        [Test]
        public void CancellingShakeRestoresBaselineRatherThanAnIntermediatePeak()
        {
            var target = Rect("Shake");
            target.anchoredPosition = new Vector2(37, 56);
            CozyMotion.Shake(target);
            CozyMotion.Tick(.02f);
            CozyMotion.Cancel(target);
            Assert.AreEqual(new Vector2(37, 56), target.anchoredPosition);
            CozyMotion.Tick(1);
            Assert.AreEqual(new Vector2(37, 56), target.anchoredPosition);
        }

        [Test]
        public void AnimationBurstsKeepBoundedJobsAndCancellationRemovesThem()
        {
            for (var i = 0; i < 120; i++) CozyMotion.Punch(Rect("Burst" + i));
            Assert.LessOrEqual(CozyMotion.ActiveJobCount, CozyMotion.MaxJobs);
            CozyMotion.Cancel(root.transform as RectTransform, true);
            Assert.AreEqual(0, CozyMotion.ActiveJobCount);
        }

        [Test]
        public void RouterRepeatedCurrentTabCancelsOldFlipWithoutHidingLatestPage()
        {
            var router = root.AddComponent<PanelRouter>();
            var home = Rect("Home").gameObject;
            var shop = Rect("Shop").gameObject;
            router.Register(ShellPanelId.Home, home);
            router.Register(ShellPanelId.Shop, shop);
            router.Show(ShellPanelId.Shop);
            CozyMotion.Tick(.03f);
            router.Show(ShellPanelId.Shop);
            CozyMotion.Tick(1);
            CozyMotion.Tick(1);
            Assert.IsTrue(shop.activeSelf);
            Assert.IsFalse(home.activeSelf);
            Assert.AreEqual(1, shop.GetComponent<CanvasGroup>().alpha);
            Assert.IsTrue(shop.GetComponent<CanvasGroup>().blocksRaycasts);
            Assert.AreEqual(Vector2.zero, ((RectTransform)shop.transform).anchoredPosition);
        }

        [Test]
        public void RouterSynchronousRedirectWinsOverOuterRoute()
        {
            var router = root.AddComponent<PanelRouter>();
            var home = Rect("Home").gameObject;
            var shop = Rect("Shop").gameObject;
            var map = Rect("Map").gameObject;
            router.Register(ShellPanelId.Home, home);
            router.Register(ShellPanelId.Shop, shop);
            router.Register(ShellPanelId.WorldMap, map);
            router.PanelChanged += id => { if (id == ShellPanelId.Shop) router.Show(ShellPanelId.WorldMap); };
            router.Show(ShellPanelId.Shop);
            CozyMotion.Tick(1);
            CozyMotion.Tick(1);
            Assert.AreEqual(ShellPanelId.WorldMap, router.Current);
            Assert.IsTrue(map.activeSelf);
            Assert.IsFalse(home.activeSelf);
            Assert.IsFalse(shop.activeSelf);
        }

        [Test]
        public void DragCancelsSmoothMapFocusAtCurrentPosition()
        {
            var map = Rect("Map");
            CozyMotion.MoveTo(map, new Vector2(100, 100));
            CozyMotion.Tick(.1f);
            var inFlight = map.anchoredPosition;
            CozyMotion.Cancel(map);
            CozyMotion.Tick(1);
            Assert.AreEqual(inFlight, map.anchoredPosition);
            Assert.AreNotEqual(new Vector2(100, 100), inFlight);
        }

        [Test]
        public void CatchSequenceUsesReceiptDurationAndSkipCompletesOnlyOnce()
        {
            var presenter = root.AddComponent<CozyCatchCelebration>();
            Lifecycle(presenter, "OnEnable");
            var completed = 0;
            presenter.Completed += id => completed++;
            Assert.IsTrue(presenter.Enqueue("catch-1", "鲤鱼", null, false, "+5 金币"));
            Assert.AreEqual(2.82f, presenter.CurrentDuration);
            presenter.Advance(.9f);
            Assert.IsTrue(presenter.IsPlaying);
            presenter.Skip();
            presenter.Advance(3);
            Assert.AreEqual(1, completed);
            Assert.IsFalse(presenter.IsPlaying);
            presenter.Enqueue("catch-2", "鲶鱼", null, true);
            Assert.AreEqual(3.06f, presenter.CurrentDuration);
            presenter.Advance(2.95f);
            Assert.IsTrue(presenter.IsPlaying);
            presenter.Advance(.12f);
            Assert.IsFalse(presenter.IsPlaying);
            Assert.AreEqual(2, completed);
        }

        [Test]
        public void HiddenCatchQueueIsBoundedDeduplicatedAndResumes()
        {
            var presenter = root.AddComponent<CozyCatchCelebration>();
            Lifecycle(presenter, "OnEnable");
            CozyMotion.SetPaused(true);
            for (var i = 0; i < 12; i++) presenter.Enqueue("hidden-" + i, "鲤鱼", null, false);
            Assert.IsFalse(presenter.Enqueue("hidden-11", "鲤鱼", null, false));
            presenter.Advance(10);
            Assert.IsFalse(presenter.IsPlaying);
            Assert.AreEqual(CozyCatchCelebration.QueueLimit, presenter.QueuedCount);
            CozyMotion.SetPaused(false);
            Assert.AreEqual("hidden-4", presenter.CurrentCatchId);
            Assert.AreEqual(7, presenter.QueuedCount);
            presenter.Clear();
            Assert.IsFalse(presenter.IsPlaying);
            Assert.AreEqual(0, presenter.QueuedCount);
            Assert.IsTrue(presenter.Enqueue("hidden-11", "鲤鱼", null, false));
        }

        [Test]
        public void ReduceDuringCelebrationImmediatelyRemovesTransformsAndParticles()
        {
            var presenter = root.AddComponent<CozyCatchCelebration>();
            Lifecycle(presenter, "OnEnable");
            presenter.Enqueue("new", "鲤鱼", null, true);
            presenter.Advance(.08f);
            CozyMotion.SetReduced(true);
            var stage = root.transform.Find("CatchReceiptOverlay/Stage");
            var panel = stage.Find("Paper") as RectTransform;
            Assert.AreEqual(Vector3.one, panel.localScale);
            Assert.AreEqual(new Vector2(836, -400), panel.anchoredPosition);
            Assert.AreEqual(Quaternion.identity, panel.localRotation);
            Assert.AreEqual(Quaternion.identity, stage.Find("Fish").localRotation);
            Assert.AreEqual(new Vector2(836, -420), ((RectTransform)stage.Find("Fish")).anchoredPosition);
            Assert.IsFalse(stage.Find("Drop0").gameObject.activeSelf);
            Assert.IsFalse(stage.Find("Coin0").gameObject.activeSelf);
            Assert.IsFalse(stage.Find("Ripple0").gameObject.activeSelf);
        }

        [Test]
        public void CharacterRequiresExplicitCompleteManifestMapping()
        {
            var target = Rect("Cat");
            target.sizeDelta = new Vector2(220, 176);
            var image = target.gameObject.AddComponent<Image>();
            var original = Resources.Load<Sprite>("Desktop/Theme/Cozy/journal/illustrations/cat-fishing");
            image.sprite = original;
            var cat = target.gameObject.AddComponent<CozyCornerCharacter>();
            Lifecycle(cat, "OnEnable");
            Assert.IsFalse(cat.SetSkin("unknown"));
            cat.SetPose(CozyCornerCharacter.Pose.Jump);
            cat.Advance(.15f);
            Assert.AreSame(original, image.sprite);
            Assert.Greater(target.anchoredPosition.y, 0, "The original cat keeps its approved sprite and performs the webpage transform animation.");
            Assert.IsTrue(cat.SetSkin("black"));
            cat.SetPose(CozyCornerCharacter.Pose.Error);
            Assert.AreSame(Resources.Load<Sprite>("Desktop/Theme/Cozy/journal/characters/black/error"), image.sprite);
            cat.Advance(1.3f);
            Assert.AreEqual(CozyCornerCharacter.Pose.Rest, cat.CurrentPose);
        }

        [Test]
        public void CharacterPauseAndReduceRestorePoseTransform()
        {
            var target = Rect("Cat");
            target.sizeDelta = new Vector2(220, 176);
            target.gameObject.AddComponent<Image>();
            var cat = target.gameObject.AddComponent<CozyCornerCharacter>();
            Lifecycle(cat, "OnEnable");
            cat.SetSkin("black");
            cat.SetPose(CozyCornerCharacter.Pose.Jump);
            cat.Advance(.1f);
            Assert.Greater(target.anchoredPosition.y, 0);
            CozyMotion.SetReduced(true);
            Assert.AreEqual(Vector2.zero, target.anchoredPosition);
            CozyMotion.SetPaused(true);
            Assert.AreEqual(CozyCornerCharacter.Pose.Sleep, cat.CurrentPose);
            CozyMotion.SetPaused(false);
            Assert.AreEqual(CozyCornerCharacter.Pose.Rest, cat.CurrentPose);
        }

        [Test]
        public void ReadReceiptsAreIsolatedByAccountAndRequireExplicitSelection()
        {
            const string a = "journal-read-test-a";
            const string b = "journal-read-test-b";
            CozyJournalReadState.ClearAccount(a);
            CozyJournalReadState.ClearAccount(b);
            try
            {
                Assert.IsTrue(CozyJournalReadState.IsUnread(a, "fish-1"));
                Assert.IsTrue(CozyJournalReadState.MarkRead(a, "fish-1"));
                Assert.IsFalse(CozyJournalReadState.IsUnread(a, "fish-1"));
                Assert.IsTrue(CozyJournalReadState.IsUnread(b, "fish-1"));
                Assert.IsFalse(CozyJournalReadState.IsUnread(null, "fish-1"));
                Assert.IsFalse(CozyJournalReadState.MarkRead(null, "fish-1"));
            }
            finally { CozyJournalReadState.ClearAccount(a); CozyJournalReadState.ClearAccount(b); }
        }

        [Test]
        public void StickerEntranceDoesNotMoveLayoutOrButtonHitRect()
        {
            var card = Rect("Sticker");
            card.gameObject.AddComponent<Image>();
            card.gameObject.AddComponent<Button>();
            var art = Rect("Art");
            art.SetParent(card, false);
            art.gameObject.AddComponent<Image>();
            var position = card.anchoredPosition;
            var rotation = card.localRotation;
            var scale = card.localScale;
            var motion = CozyStickerMotion.Bind(card.gameObject, "test-sticker-" + System.Guid.NewGuid(), 0);
            motion.Advance(.08f);
            Assert.AreEqual(position, card.anchoredPosition);
            Assert.AreEqual(rotation, card.localRotation);
            Assert.AreEqual(scale, card.localScale);
            Assert.That(CozyStickerMotion.StableAngle("carp"), Is.EqualTo(.69f).Within(.0001f));
            Assert.That(CozyStickerMotion.StableAngle("crucian"), Is.EqualTo(1.12f).Within(.0001f));
            Assert.That(CozyStickerMotion.StableAngle("golden_line"), Is.EqualTo(-.53f).Within(.0001f));
            Assert.LessOrEqual(Mathf.Abs(CozyStickerMotion.StableAngle("fish:123")), 1.5f);
        }

        [Test]
        public void FinalStickerPoseUsesCssCenterAndSelectedLiftWithFixedHitGeometry()
        {
            var card = Rect("Card"); card.pivot = new Vector2(0, 1); card.sizeDelta = new Vector2(200, 150);
            card.gameObject.AddComponent<Image>(); card.gameObject.AddComponent<Button>();
            var visual = card.gameObject.AddComponent<CozyButtonVisual>(); visual.SetSelected(true);
            var art = Rect("Art"); art.SetParent(card, false); art.gameObject.AddComponent<Image>();
            var motion = CozyStickerMotion.Bind(card.gameObject, "account:carp", 0, sourceId: "carp", cssAngle: 10);
            motion.ApplyFinalPose();
            using (var mesh = new VertexHelper())
            {
                var vertex = UIVertex.simpleVert; vertex.position = Vector3.zero; mesh.AddVert(vertex);
                art.GetComponent<CozyGraphicOffset>().ModifyMesh(mesh);
                mesh.PopulateUIVertex(ref vertex, 0);
                Assert.That(vertex.position.x, Is.EqualTo(0).Within(.001f));
                Assert.That(vertex.position.y, Is.EqualTo(6).Within(.001f));
            }
            Assert.AreEqual(Vector2.zero, card.anchoredPosition);
            Assert.AreEqual(Quaternion.identity, card.localRotation);
            Assert.AreEqual(Vector3.one, card.localScale);
        }

        [Test]
        public void WebEasingUsesBezierXInversionAndPreservesOvershoot()
        {
            Assert.That(CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutCubic, .5f), Is.EqualTo(.8722f).Within(.002f));
            Assert.Greater(CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutBack, .7f), 1);
            Assert.That(CozyWebMotion.Evaluate(CozyWebMotion.Ease.InOutSine, .5f), Is.EqualTo(.5f).Within(.0001f));
            Assert.AreEqual(0, CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutBack, 0));
            Assert.AreEqual(1, CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutBack, 1));
        }

        [Test]
        public void ToastQueuePreservesWebHoldAndPausesWithoutDiscardingCurrentNotice()
        {
            var presenter = CozyJournalToast.Show(root.transform, "保存完成"); Lifecycle(presenter, "OnEnable");
            CozyJournalToast.Show(root.transform, "网络错误", true);
            Assert.AreEqual(1, presenter.QueuedCount);
            presenter.Advance(2.8f); Assert.IsTrue(presenter.IsPlaying);
            CozyMotion.SetPaused(true); presenter.Advance(5); Assert.AreEqual(1, presenter.QueuedCount);
            CozyMotion.SetPaused(false); presenter.Advance(.45f);
            Assert.IsTrue(presenter.IsPlaying); Assert.AreEqual(0, presenter.QueuedCount);
            presenter.Advance(CozyJournalToast.Duration); Assert.IsFalse(presenter.IsPlaying);
        }

        [Test]
        public void SoldCardCutsReconstructTheWholeCardWithoutLosingConcaveEdges()
        {
            var total = 0f;
            for (var i = 0; i < 3; i++)
            {
                var polygon = CozyCardFragments.ClipPolygon(i);
                var expected = Mathf.Abs(CozyPolygonGraphic.SignedArea(polygon)); total += expected;
                var rect = Rect("Polygon" + i); rect.sizeDelta = new Vector2(200, 150);
                var graphic = rect.gameObject.AddComponent<CozyPolygonGraphic>(); graphic.SetPolygon(polygon);
                using (var mesh = new VertexHelper())
                {
                    typeof(CozyPolygonGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(graphic, new object[] { mesh });
                    var triangles = new System.Collections.Generic.List<UIVertex>(); mesh.GetUIVertexStream(triangles);
                    var area = 0f;
                    for (var v = 0; v < triangles.Count; v += 3)
                        area += Mathf.Abs(Vector3.Cross(triangles[v + 1].position - triangles[v].position,
                            triangles[v + 2].position - triangles[v].position).z) / 2;
                    Assert.That(area, Is.EqualTo(expected * 200 * 150).Within(.02f));
                }
            }
            Assert.That(total, Is.EqualTo(1).Within(.00001f));
        }

        [Test]
        public void RareGlintDefaultsOffAndItsExplicitPreviewHonorsReducedMotion()
        {
            var card = Rect("RareCard"); card.sizeDelta = new Vector2(224, 244);
            var glint = CozyRareGlint.Bind(card.gameObject, 0); Lifecycle(glint, "OnEnable");
            glint.PreviewAt(6.25f); Assert.IsFalse(glint.IsVisible);
            glint.SetMotionEnabled(true); glint.PreviewAt(6.25f); Assert.IsTrue(glint.IsVisible);
            CozyMotion.SetReduced(true); Assert.IsFalse(glint.IsVisible);
        }

        [Test]
        public void CelebrationUsesOriginalPaperShadowAndMultiplyStampMaterial()
        {
            var presenter = root.AddComponent<CozyCatchCelebration>();
            presenter.Enqueue("preview", "鲤鱼", null, true, sizeM: .58f); presenter.PreviewAt(1.2f);
            var paper = root.transform.Find("CatchReceiptOverlay/Stage/Paper");
            var image = paper.Find("OriginalPaperAndShadow").GetComponent<Image>();
            Assert.AreSame(CozyJournalTheme.Sprite("motion/celebration-paper"), image.sprite);
            Assert.AreEqual(new Vector2(660, 540), image.rectTransform.sizeDelta);
            var stamp = paper.Find("NewStamp").GetComponent<CozyCssGraphic>();
            Assert.AreSame(Resources.Load<Material>(CozyJournalTheme.ResourceRoot + "materials/PaperMultiply"), stamp.material);
        }

        [Test]
        public void BobberCreatesAndReacquiresMissingCanvasGroupInEditor()
        {
            var rect = Rect("CozyBobber");
            var bobber = rect.gameObject.AddComponent<CozyBobberNavigation>();
            Assert.IsNull(rect.GetComponent<CanvasGroup>());
            Assert.DoesNotThrow(() => bobber.Move(new Vector2(30, 40), null));
            var first = rect.GetComponent<CanvasGroup>();
            Assert.IsNotNull(first); Assert.AreEqual(1, first.alpha);
            Object.DestroyImmediate(first);
            Assert.DoesNotThrow(() => bobber.Move(new Vector2(80, 40), null));
            var second = rect.GetComponent<CanvasGroup>();
            Assert.IsNotNull(second); Assert.AreNotSame(first, second);
            bobber.Advance(1);
            Assert.AreEqual(new Vector2(80, 40), rect.anchoredPosition);
            Assert.AreEqual(1, second.alpha);
        }

        [Test]
        public void BobberTeardownWithDestroyedCanvasGroupDoesNotRecreateOrRestartLoop()
        {
            var rect = Rect("CozyBobber");
            var bobber = rect.gameObject.AddComponent<CozyBobberNavigation>();
            bobber.Move(new Vector2(30, 40), null);
            Assert.AreSame(rect, CozyMotion.ActiveLoopOwner);
            Object.DestroyImmediate(rect.GetComponent<CanvasGroup>());
            Assert.DoesNotThrow(() => Lifecycle(bobber, "OnDisable"));
            Assert.IsNull(rect.GetComponent<CanvasGroup>());
            Assert.IsNull(CozyMotion.ActiveLoopOwner);
            Assert.AreEqual(Vector3.one, rect.localScale);
            Assert.AreEqual(Quaternion.identity, rect.localRotation);
            Assert.DoesNotThrow(() => Object.DestroyImmediate(bobber));
        }

        [Test]
        public void RebindingTheSameCardKeepsEntranceSettledAndSelectionOnlyAnimatesTapeLiftAndShadow()
        {
            var card = Rect("StableCard"); card.sizeDelta = new Vector2(200, 150);
            card.gameObject.AddComponent<Image>(); card.gameObject.AddComponent<Button>();
            var visual = card.gameObject.AddComponent<CozyButtonVisual>(); visual.ApplyFinalPose();
            var tape = Rect("JournalTape"); tape.SetParent(card, false); tape.gameObject.AddComponent<Image>();
            var shadow = Rect("JournalCardShadow"); shadow.SetParent(card, false); shadow.gameObject.AddComponent<Image>();
            var motion = CozyStickerMotion.Bind(card.gameObject, "account:inventory-uuid", 0, sourceId: "carp");
            motion.Advance(.4f); Assert.IsTrue(motion.EntranceComplete);
            CozyStickerMotion.Bind(card.gameObject, "account:inventory-uuid", 0, sourceId: "carp");
            Assert.IsTrue(motion.EntranceComplete); Assert.AreEqual(1, motion.AppliedScale);
            Assert.AreEqual("carp", motion.SourceId);
            Assert.That(motion.CssAngle, Is.EqualTo(.69f).Within(.0001f));
            visual.SetSelected(true); motion.Advance(0);
            Assert.AreEqual(0, motion.TapeReveal);
            motion.Advance(.08f);
            Assert.That(motion.TapeReveal, Is.GreaterThan(0).And.LessThan(1));
            Assert.That(motion.CurrentVisualOffset.y, Is.GreaterThan(0).And.LessThan(6));
            Assert.That(motion.ShadowBlend, Is.GreaterThan(0).And.LessThan(1));
            Assert.AreEqual(1, motion.AppliedScale, "Selecting does not restart the 1.12 entrance.");
            motion.Advance(.1f);
            Assert.AreEqual(1, motion.TapeReveal); Assert.AreEqual(6, motion.CurrentVisualOffset.y);
            Assert.AreEqual(Vector2.zero, card.anchoredPosition);
        }

        [Test]
        public void StickerPoseComposesWithButtonPressInsteadOfReplacingItsMeshChannel()
        {
            var card = Rect("PressedCard"); card.sizeDelta = new Vector2(200, 150);
            card.gameObject.AddComponent<Image>(); card.gameObject.AddComponent<Button>();
            var art = Rect("Art"); art.SetParent(card, false); art.gameObject.AddComponent<Image>();
            var visual = card.gameObject.AddComponent<CozyButtonVisual>(); visual.ApplyFinalPose();
            var motion = CozyStickerMotion.Bind(card.gameObject, "press", 0, cssAngle: 10);
            visual.SetSelected(true); motion.ApplyFinalPose();
            CozyMotion.PressDown(card, true); CozyMotion.Tick(.06f); motion.Advance(.02f);
            Assert.AreEqual(new Vector2(1.02f, .96f), visual.MotionScale);
            using (var mesh = new VertexHelper())
            {
                var vertex = UIVertex.simpleVert; vertex.position = new Vector3(20, 15, 0); mesh.AddVert(vertex);
                art.GetComponent<CozyGraphicOffset>().ModifyMesh(mesh); mesh.PopulateUIVertex(ref vertex, 0);
                var expected = Quaternion.Euler(0, 0, -10) * new Vector3(20 * 1.02f, 15 * .96f, 0) + Vector3.up * 6;
                Assert.That(Vector3.Distance(vertex.position, expected), Is.LessThan(.001f));
            }
            Assert.AreEqual(Vector3.one, card.localScale); Assert.AreEqual(Vector2.zero, card.anchoredPosition);
        }

        [Test]
        public void PolaroidRevealRetainsStaticTiltAndSettlesWhenReduced()
        {
            var owner = Rect("Polaroid"); var art = Rect("Photo"); art.SetParent(owner, false);
            var image = art.gameObject.AddComponent<Image>();
            var pose = owner.gameObject.AddComponent<CozyJournalStaticPose>(); pose.Configure(-2, image);
            Lifecycle(pose, "OnEnable"); pose.PlayReveal(new Vector2(24, 0), .26f, .3f);
            Assert.AreEqual(.9f, pose.AppliedScale); Assert.AreEqual(.3f, pose.AppliedOpacity);
            Assert.AreEqual(new Vector2(24, 0), pose.AppliedOffset);
            pose.Advance(.1f); Assert.IsTrue(pose.IsRevealing); Assert.AreEqual(-2, pose.CssAngle);
            CozyMotion.SetReduced(true);
            Assert.IsFalse(pose.IsRevealing); Assert.AreEqual(1, pose.AppliedScale);
            Assert.AreEqual(1, pose.AppliedOpacity); Assert.AreEqual(Vector2.zero, pose.AppliedOffset);
            using (var mesh = new VertexHelper())
            {
                var vertex = UIVertex.simpleVert; vertex.position = new Vector3(30, 0, 0); mesh.AddVert(vertex);
                image.GetComponent<CozyGraphicOffset>().ModifyMesh(mesh); mesh.PopulateUIVertex(ref vertex, 0);
                Assert.That(Vector3.Distance(vertex.position, Quaternion.Euler(0, 0, 2) * new Vector3(30, 0, 0)), Is.LessThan(.001f));
            }
        }

        [Test]
        public void RestartEntranceReplaysOnlyFirstEightCardsAndNeverReplaysObservedPonds()
        {
            var first = Rect("First"); var ninth = Rect("Ninth"); var pond = Rect("Pond");
            var a = CozyStickerMotion.Bind(first.gameObject, "restart-first", 0);
            var b = CozyStickerMotion.Bind(ninth.gameObject, "restart-ninth", 8);
            var c = CozyStickerMotion.Bind(pond.gameObject, "restart-pond-" + System.Guid.NewGuid(), 0, pondObserver: true);
            a.Advance(1); b.Advance(1); c.Advance(1);
            a.RestartEntrance(.2f); b.RestartEntrance(); c.RestartEntrance();
            Assert.IsFalse(a.EntranceComplete); Assert.AreEqual(1.12f, a.AppliedScale);
            Assert.IsTrue(b.EntranceComplete); Assert.AreEqual(1, b.AppliedScale);
            Assert.IsTrue(c.EntranceComplete); Assert.AreEqual(1, c.AppliedScale);
            a.Advance(.1f); Assert.AreEqual(1.12f, a.AppliedScale);
            a.Advance(.4f); Assert.IsTrue(a.EntranceComplete);
        }

        [Test]
        public void PointerOnlyCardsSupportSelectionAndRepeatedTapeWithoutReplayingEntrance()
        {
            var card = Rect("PointerOnly");
            var tape = Rect("JournalTape"); tape.SetParent(card, false); tape.gameObject.AddComponent<Image>();
            var outline = Rect("JournalSelection"); outline.SetParent(card, false); outline.gameObject.AddComponent<Image>();
            var motion = CozyStickerMotion.Bind(card.gameObject, "pointer-only", 0);
            motion.ApplyFinalPose(); motion.SetSelected(true);
            Assert.IsTrue(motion.IsSelected); Assert.IsTrue(tape.gameObject.activeSelf);
            Assert.AreEqual(0, motion.TapeReveal); Assert.IsTrue(motion.EntranceComplete);
            motion.Advance(.18f);
            Assert.AreEqual(6, motion.CurrentVisualOffset.y); Assert.AreEqual(1, motion.TapeReveal);
            motion.ReplayTape();
            Assert.AreEqual(0, motion.TapeReveal); Assert.AreEqual(6, motion.CurrentVisualOffset.y);
            Assert.AreEqual(1, motion.AppliedScale); Assert.IsTrue(motion.EntranceComplete);
            motion.Advance(.08f); Assert.That(motion.TapeReveal, Is.GreaterThan(0).And.LessThan(1));
            motion.SetSelected(false); motion.Advance(.18f);
            Assert.IsFalse(tape.gameObject.activeSelf); Assert.IsFalse(outline.gameObject.activeSelf);
            Assert.IsFalse(motion.IsSelected); Assert.AreEqual(0, motion.CurrentVisualOffset.y);
            CozyMotion.SetReduced(true); motion.SetSelected(true); motion.ReplayTape();
            Assert.AreEqual(1, motion.TapeReveal); Assert.AreEqual(6, motion.CurrentVisualOffset.y);
            Assert.AreEqual(Vector2.zero, card.anchoredPosition);
        }

        [Test]
        public void ImportedBobberResourceIsUsedWithoutRuntimeTextureGeneration()
        {
            Assert.AreSame(Resources.Load<Sprite>("Desktop/Theme/Cozy/journal/motion/bobber"), CozyMotion.BobberSprite());
        }
    }
}
#endif
