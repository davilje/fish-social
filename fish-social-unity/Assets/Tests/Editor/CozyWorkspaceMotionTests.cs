#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using FishSocial.Desktop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyWorkspaceMotionTests
    {
        GameObject root;
        bool reduced;
        [SetUp] public void Setup()
        {
            reduced = CozyMotion.Reduced;
            CozyMotion.SetPaused(false); CozyMotion.SetReduced(false);
            root = new GameObject("WorkspaceMotionTest", typeof(RectTransform));
        }
        [TearDown] public void Cleanup()
        {
            CozyMotion.CancelPageTransition();
            CozyMotion.Cancel(root.transform as RectTransform, true);
            Object.DestroyImmediate(root);
            CozyMotion.SetPaused(false); CozyMotion.SetReduced(reduced);
        }
        RectTransform Rect(string name, Transform parent = null)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent != null ? parent : root.transform, false); return r;
        }

        [TestCase("PanelShop")]
        [TestCase("PanelEquipment")]
        [TestCase("PanelCatch")]
        [TestCase("PanelProfileHub")]
        [TestCase("PanelProfileEdit")]
        [TestCase("PanelLeaderboard")]
        [TestCase("PanelSocialFeed")]
        [TestCase("PanelWorldMap")]
        [TestCase("PanelPondSelection")]
        [TestCase("PanelSettings")]
        [TestCase("PanelAchievements")]
        [TestCase("PanelSocial")]
        [TestCase("PanelGallery")]
        public void ProductionPagesKeepAllContentCoveredUntilPaperHasRisen(string resource)
        {
            var prefab = Resources.Load<GameObject>("Desktop/Prefabs/" + resource);
            Assert.IsNotNull(prefab);
            var page = Object.Instantiate(prefab, root.transform);
            var home = Rect("Home");
            var calls = 0;
            CozyMotion.Flip(home.gameObject, page, () => calls++);
            var pose = page.GetComponent<CozyJournalFlipPose>();
            Assert.IsTrue(CozyMotion.EntranceHeld);
            Assert.AreEqual(0, pose.ContentAlpha);
            CozyMotion.Tick(.1f);
            Assert.That(page.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0).And.LessThan(1));
            Assert.AreEqual(0, pose.ContentAlpha, "Plain text, workspace cards and the rail must not show then disappear.");
            CozyMotion.Tick(.1f);
            Assert.AreEqual(1, pose.ContentAlpha);
            Assert.IsFalse(CozyMotion.EntranceHeld);
            Assert.AreEqual(1, calls);
            CozyMotion.Tick(1);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void ContentArrivingDuringRiseIsCoveredAndCancelledOpenRestoresIt()
        {
            var home = Rect("Home"); var page = Rect("Page");
            Rect("JournalShadow", page).gameObject.AddComponent<Image>();
            CozyMotion.Flip(home.gameObject, page.gameObject, null);
            var late = Rect("LateData", page).gameObject.AddComponent<Image>();
            var pose = page.GetComponent<CozyJournalFlipPose>();
            typeof(CozyJournalFlipPose).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pose, null);
            var effect = late.GetComponent<CozyJournalFlipGraphic>();
            Assert.IsNotNull(effect);
            using (var mesh = new VertexHelper())
            {
                var v = UIVertex.simpleVert; mesh.AddVert(v); effect.ModifyMesh(mesh); mesh.PopulateUIVertex(ref v, 0);
                Assert.AreEqual(0, v.color.a);
            }
            CozyMotion.CancelPageTransition();
            Assert.IsFalse(CozyMotion.EntranceHeld); Assert.AreEqual(1, pose.ContentAlpha);
            Assert.AreEqual(Vector2.zero, page.anchoredPosition);
        }

        [Test]
        public void AsyncFeedOrRankCompletionDoesNotReplayVisibleHeadingAndAside()
        {
            var title = Rect("Title"); var aside = Rect("Aside");
            var motion = root.AddComponent<CozyJournalPageMotion>(); motion.Configure(title, aside, null);
            motion.PlayEntrance(2); motion.Advance(.5f);
            Assert.AreEqual(1, motion.HeadingAlpha); Assert.AreEqual(1, motion.AsideAlpha);
            motion.ContentReady(20);
            Assert.AreEqual(1, motion.HeadingAlpha); Assert.AreEqual(1, motion.AsideAlpha);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DialogUsesWebTimingAndKeepsItsDimStationary(bool reduce)
        {
            CozyMotion.SetReduced(reduce);
            var paper = Rect("Paper"); var dim = Rect("Dim", paper);
            paper.gameObject.AddComponent<Image>(); dim.gameObject.AddComponent<Image>();
            paper.anchoredPosition = new Vector2(30, 40); dim.anchoredPosition = new Vector2(-30, -40);
            var position = dim.position;
            CozyWorkspaceDialogMotion.Play(paper, dim);
            var motion = paper.GetComponent<CozyWorkspaceDialogMotion>();
            Assert.AreEqual(0, motion.Opacity);
            Assert.AreEqual(1, paper.GetComponent<CanvasGroup>().alpha, "The dim stays visible even in the first frame.");
            using (var mesh = new VertexHelper())
            {
                var vertex = UIVertex.simpleVert; mesh.AddVert(vertex);
                paper.GetComponent<CozyDialogFadeGraphic>().ModifyMesh(mesh); mesh.PopulateUIVertex(ref vertex, 0);
                Assert.AreEqual(0, vertex.color.a, "Only the paper is transparent at frame zero.");
            }
            Assert.IsNull(dim.GetComponent<CozyDialogFadeGraphic>());
            Assert.AreEqual(position, dim.position);
            Assert.AreEqual(reduce ? 40 : 24, paper.anchoredPosition.y);
            CozyMotion.Tick(.06f);
            Assert.That(motion.Opacity, Is.GreaterThan(0).And.LessThan(1));
            Assert.AreEqual(position, dim.position);
            CozyMotion.Tick(reduce ? .06f : .14f);
            Assert.AreEqual(1, motion.Opacity);
            Assert.AreEqual(new Vector2(30, 40), paper.anchoredPosition);
            CozyWorkspaceDialogMotion.Play(paper, dim);
            paper.GetComponent<CozyWorkspaceDialogMotion>().Finish();
            Assert.AreEqual(1, paper.GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(position, dim.position);
        }

        [Test]
        public void ReducedEntrancesKeepShortFadesWithoutStaggerOrScale()
        {
            CozyMotion.SetReduced(true);
            var index = new CozyIndexMotion(true); index.BeginEntrance(4, true);
            Assert.AreEqual(-16, index.Current.X); Assert.AreEqual(0, index.Current.Alpha);
            index.Advance(.06f); Assert.That(index.Current.Alpha, Is.GreaterThan(0).And.LessThan(1));
            Assert.AreEqual(-16, index.Current.X);
            index.Advance(.06f); Assert.AreEqual(1, index.Current.Alpha);
            var card = Rect("Card"); card.gameObject.AddComponent<Image>();
            var sticker = CozyStickerMotion.Bind(card.gameObject, "reduced-new-card", 5);
            sticker.RestartEntrance(); Assert.IsFalse(sticker.EntranceComplete);
            sticker.Advance(.06f); Assert.IsFalse(sticker.EntranceComplete); Assert.AreEqual(1, sticker.AppliedScale);
            sticker.Advance(.06f); Assert.IsTrue(sticker.EntranceComplete);
            var photo = Rect("Photo"); var image = photo.gameObject.AddComponent<Image>();
            var pose = photo.gameObject.AddComponent<CozyJournalStaticPose>(); pose.Configure(-2, image);
            pose.PlayReveal(new Vector2(24, 0));
            Assert.AreEqual(1, pose.AppliedScale); Assert.AreEqual(Vector2.zero, pose.AppliedOffset);
            Assert.AreEqual(.3f, pose.AppliedOpacity, .001f);
            pose.Advance(.12f); Assert.AreEqual(1, pose.AppliedOpacity);
        }

        [Test]
        public void WorkspaceButtonUsesTwoPixelTravelWithoutPressSquash()
        {
            var rect = Rect("Action"); rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>(); CatTheme.StyleButton(button);
            CozyWorkspaceInteraction.Bind(button);
            var motion = button.GetComponent<CozyWorkspaceInteraction>();
            var visual = button.GetComponent<CozyButtonVisual>();
            var data = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
            visual.OnPointerEnter(data); motion.OnPointerEnter(data); motion.Advance(.16f);
            Assert.AreEqual(2, motion.Progress);
            visual.OnPointerDown(data); motion.OnPointerDown(data); CozyMotion.Tick(.06f); motion.Advance(.16f);
            Assert.AreEqual(-2, motion.Progress); Assert.AreEqual(Vector2.one, visual.MotionScale);
            Assert.AreEqual(Vector2.zero, rect.anchoredPosition);
            motion.OnPointerExit(data); motion.Advance(.16f); Assert.AreEqual(0, motion.Progress);
        }

        [Test]
        public void AvatarHoverRotatesOnlyThePortraitAndReducedMotionSettlesIt()
        {
            var rect = Rect("Avatar"); var button = rect.gameObject.AddComponent<Button>();
            var art = Rect("Art", rect).gameObject.AddComponent<Image>();
            CozyWorkspaceInteraction.Bind(button, CozyWorkspaceInteraction.Kind.Avatar, art);
            var motion = button.GetComponent<CozyWorkspaceInteraction>();
            motion.OnPointerEnter(new PointerEventData(null)); motion.Advance(.2f);
            using (var mesh = new VertexHelper())
            {
                var v = UIVertex.simpleVert; v.position = new Vector3(20, 0, 0); mesh.AddVert(v);
                art.GetComponent<CozyGraphicOffset>().ModifyMesh(mesh); mesh.PopulateUIVertex(ref v, 0);
                var expected = Quaternion.Euler(0, 0, 5) * new Vector3(22, 0, 0);
                Assert.That(Vector3.Distance(expected, v.position), Is.LessThan(.001f));
            }
            Assert.AreEqual(Vector3.one, rect.localScale);
            CozyMotion.SetReduced(true); motion.Advance(0); Assert.AreEqual(0, motion.Progress);
        }
    }
}
#endif
