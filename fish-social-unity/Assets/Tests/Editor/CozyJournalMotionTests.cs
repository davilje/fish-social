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
            Assert.AreEqual(1f, presenter.CurrentDuration);
            presenter.Advance(.9f);
            Assert.IsTrue(presenter.IsPlaying);
            presenter.Skip();
            presenter.Advance(3);
            Assert.AreEqual(1, completed);
            Assert.IsFalse(presenter.IsPlaying);
            presenter.Enqueue("catch-2", "鲶鱼", null, true);
            Assert.AreEqual(1.6f, presenter.CurrentDuration);
            presenter.Advance(1.5f);
            Assert.IsTrue(presenter.IsPlaying);
            presenter.Advance(.11f);
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
            var panel = root.transform.Find("CatchReceiptOverlay/Paper") as RectTransform;
            Assert.AreEqual(Vector3.one, panel.localScale);
            Assert.AreEqual(new Vector2(0, 10), panel.anchoredPosition);
            Assert.AreEqual(Quaternion.identity, panel.Find("Fish").localRotation);
            Assert.IsFalse(panel.Find("AwardRays").gameObject.activeSelf);
            Assert.IsFalse(panel.Find("Confetti0").gameObject.activeSelf);
        }

        [Test]
        public void CharacterRequiresExplicitCompleteManifestMapping()
        {
            var target = Rect("Cat");
            target.sizeDelta = new Vector2(220, 176);
            var image = target.gameObject.AddComponent<Image>();
            var original = Resources.Load<Sprite>("Desktop/Theme/Cozy/characters/poses/orange-ready");
            image.sprite = original;
            var cat = target.gameObject.AddComponent<CozyCornerCharacter>();
            Lifecycle(cat, "OnEnable");
            Assert.IsFalse(cat.SetSkin("unknown"));
            cat.SetPose(CozyCornerCharacter.Pose.Jump);
            cat.Advance(.15f);
            Assert.AreSame(original, image.sprite);
            Assert.AreEqual(Vector2.zero, target.anchoredPosition);
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
            Assert.AreEqual(CozyStickerMotion.StableAngle("fish:123"), CozyStickerMotion.StableAngle("fish:123"));
            Assert.LessOrEqual(Mathf.Abs(CozyStickerMotion.StableAngle("fish:123")), 1.5f);
        }

        [Test]
        public void ImportedBobberResourceIsUsedWithoutRuntimeTextureGeneration()
        {
            Assert.AreSame(Resources.Load<Sprite>("Desktop/Theme/Cozy/journal/motion/bobber"), CozyMotion.BobberSprite());
        }
    }
}
#endif
