#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using FishSocial.Desktop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyJournalProductionLayoutTests
    {
        const string Folder = "Assets/Resources/Desktop/Prefabs/";
        static GameObject Asset(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");

        [TestCase("PanelShop")]
        [TestCase("PanelEquipment")]
        [TestCase("PanelCatch")]
        [TestCase("PanelWorldMap")]
        [TestCase("PanelPondSelection")]
        [TestCase("PanelSettings")]
        [TestCase("PanelAchievements")]
        [TestCase("PanelLeaderboard")]
        [TestCase("PanelProfileHub")]
        [TestCase("PanelSocialFeed")]
        public void ProductionPagesUseSharedArtAndNativeControls(string name)
        {
            var page = Asset(name);
            Assert.NotNull(page);
            Assert.AreEqual(PrefabAssetType.Variant, PrefabUtility.GetPrefabAssetType(page));
            Assert.AreEqual("paper-page", page.GetComponent<Image>().sprite.name);
            Assert.NotNull(page.transform.Find("JournalRing2"));
            Assert.NotNull(page.transform.Find("DesignHeader/Close").GetComponent<Button>());
            var grain = page.transform.Find("JournalGrain").GetComponent<Image>();
            Assert.AreEqual(Image.Type.Tiled, grain.type);
            Assert.That(grain.color.a, Is.EqualTo(.32f).Within(.001f));
            Assert.NotNull(grain.material);
            foreach (var image in page.GetComponentsInChildren<Image>(true))
                if (image.sprite != null)
                    StringAssert.DoesNotContain("JournalSamples/ReferenceAssets", AssetDatabase.GetAssetPath(image.sprite));
        }

        [Test]
        public void WorldEntryIsOutsidePageContainerAndShellScaleIsStable()
        {
            var shell = Asset("DesktopShell");
            var entry = shell.transform.Find("MainRoot/Header/WorldMapEntry");
            Assert.NotNull(entry.GetComponent<Button>());
            Assert.IsTrue(entry.gameObject.activeSelf);
            Assert.IsFalse(entry.IsChildOf(shell.transform.Find("MainRoot/Content")));
            var scaler = shell.GetComponent<CanvasScaler>();
            Assert.AreEqual(CozyJournalTheme.DesignResolution, scaler.referenceResolution);
            Assert.AreEqual(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, scaler.screenMatchMode);
        }

        [TestCase("PanelWorldMap", "Details", "DetailsText", "EnterPond")]
        [TestCase("PanelPondSelection", "DesignPondAside", "Body", "Enter")]
        public void PondFactsScrollWithoutCoveringFeeModesAndEntry(string name, string path, string text, string enter)
        {
            var detail = Asset(name).transform.Find(path);
            var facts = detail.Find("FactsScroll").GetComponent<ScrollRect>();
            Assert.AreSame(detail.Find("FactsScroll/" + text), facts.content);
            Assert.NotNull(facts.viewport.GetComponent<RectMask2D>());
            var factsRect = (RectTransform)facts.transform;
            var modes = (RectTransform)detail.Find("ModeSell");
            var entry = (RectTransform)detail.Find(enter);
            var factsBottom = -factsRect.anchoredPosition.y + factsRect.sizeDelta.y;
            var modesTop = -modes.anchoredPosition.y;
            Assert.Less(factsBottom, modesTop);
            Assert.LessOrEqual(modesTop + modes.sizeDelta.y, -entry.anchoredPosition.y);
        }

        [Test]
        public void PondCardSelectionDecoratesTheCardInsteadOfTheMapAction()
        {
            var card = Object.Instantiate(Asset("PondSelectionCard"));
            try
            {
                CatTheme.SkinTree(card.transform);
                var visual = card.GetComponent<CozyButtonVisual>();
                Assert.NotNull(visual);
                visual.SetSelected(true);
                Assert.IsTrue(card.transform.Find("JournalTape").gameObject.activeSelf);
                Assert.IsTrue(card.transform.Find("JournalSelection").gameObject.activeSelf);
                Assert.IsFalse(card.transform.Find("FocusOnMap").GetComponent<CozyButtonVisual>().Selected);
                visual.SetSelected(false);
                Assert.IsFalse(card.transform.Find("JournalTape").gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(card); }
        }

        [Test]
        public void TransparentSurfaceInsetsNeverAccumulate()
        {
            var parent = new GameObject("LogicalButton", typeof(RectTransform));
            var child = new GameObject("Visual", typeof(RectTransform), typeof(Image));
            try
            {
                child.transform.SetParent(parent.transform, false);
                var image = child.GetComponent<Image>();
                CozyJournalTheme.ApplySurface(image, "surfaces/header-pill", true);
                var first = image.rectTransform.offsetMin;
                CozyJournalTheme.ApplySurface(image, "surfaces/header-pill", true);
                Assert.AreEqual(first, image.rectTransform.offsetMin);
                Assert.AreEqual(new Vector2(-32, -32), first);
                CozyJournalTheme.ApplySurface(image, "surfaces/button-quiet-default", true);
                Assert.AreEqual(new Vector2(-8, -8), image.rectTransform.offsetMin);
            }
            finally { Object.DestroyImmediate(parent); }
        }
    }
}
#endif
