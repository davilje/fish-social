#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyJournalLeaderboardMotionTests
    {
        [Test]
        public void PageTabFadeMatchesSourceTimingWithoutMovingPaper()
        {
            var reduced = CozyMotion.Reduced; CozyMotion.SetReduced(false);
            var page = new GameObject("Page", typeof(RectTransform));
            try
            {
                var title = new GameObject("Title", typeof(RectTransform)); title.transform.SetParent(page.transform);
                var aside = new GameObject("Aside", typeof(RectTransform)); aside.transform.SetParent(page.transform);
                var middle = new GameObject("Middle", typeof(RectTransform)); middle.transform.SetParent(page.transform);
                var motion = page.AddComponent<CozyJournalPageMotion>();
                motion.Configure(title.transform, aside.transform, null, middle.transform);
                var position = page.transform.position;
                motion.SampleExit(.05f);
                Assert.AreEqual(.5f, motion.HeadingAlpha, .001f);
                motion.SampleEntrance(.16f, 11);
                Assert.AreEqual(1, motion.HeadingAlpha, .001f);
                Assert.AreEqual(0, motion.AsideAlpha, .001f);
                motion.SampleEntrance(.32f, 11);
                Assert.AreEqual(.5f, motion.AsideAlpha, .001f);
                motion.SampleEntrance(.4f, 11);
                Assert.AreEqual(1, motion.AsideAlpha, .001f);
                Assert.AreEqual(position, page.transform.position);
            }
            finally { Object.DestroyImmediate(page); CozyMotion.SetReduced(reduced); }
        }

        [Test]
        public void RankSnapshotIncludesTopThreeRowsAndSelectionUpdatesBothCopiesWithReveal()
        {
            var priorReduced = CozyMotion.Reduced;
            CozyMotion.SetReduced(false);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Desktop/Prefabs/PanelLeaderboard.prefab");
            var page = Object.Instantiate(asset);
            try
            {
                page.name = "PanelLeaderboard";
                CozyRedesignAuthoring.ApplyDetailParity(page);
                var panel = page.GetComponent<DesktopLeaderboardPanel>();
                var entries = new[]
                {
                    new LeaderboardEntryDto { playerId = "cat-flower", nickname = "阿花", rank = 1, value = 1.28f },
                    new LeaderboardEntryDto { playerId = "cat-snow", nickname = "小白", rank = 2, value = 1.16f },
                    new LeaderboardEntryDto { playerId = "cat-black", nickname = "煤球", rank = 3, value = 1.05f },
                };
                panel.PresentRows(entries);
                var content = page.transform.Find("Scroll/Viewport/Content");
                Assert.AreEqual(4, content.childCount, "One ignored-layout podium plus all three player rows");
                var podium = content.Find("Podium");
                Assert.IsTrue(podium.GetComponent<LayoutElement>().ignoreLayout);
                var motion = podium.Find("Slot2").GetComponent<CozyStickerMotion>();
                Assert.AreEqual(CozyStickerMotion.StableAngle("cat-snow"), motion.CssAngle);
                panel.SelectPlayer(entries[1]);
                panel.SampleMotion(0);
                var photo = page.transform.Find("DesignPlayerCard/JournalPhoto").GetComponent<CozyJournalStaticPose>();
                Assert.AreEqual(.9f, photo.AppliedScale, .001f);
                Assert.AreEqual(.3f, photo.AppliedOpacity, .001f);
                Assert.IsTrue(podium.Find("Slot2").GetComponent<CozyButtonVisual>().Selected);
                Assert.IsFalse(podium.Find("Slot1").GetComponent<CozyButtonVisual>().Selected);
                Assert.IsTrue(content.GetChild(2).GetComponent<CozyButtonVisual>().Selected);
                panel.SampleMotion(.22f);
                Assert.AreEqual(6, motion.CurrentVisualOffset.y, .01f);
                Assert.AreEqual(1, photo.AppliedScale, .001f);
                Assert.AreEqual(Vector2.zero, photo.AppliedOffset);
            }
            finally { Object.DestroyImmediate(page); CozyMotion.SetReduced(priorReduced); }
        }
    }
}
#endif
