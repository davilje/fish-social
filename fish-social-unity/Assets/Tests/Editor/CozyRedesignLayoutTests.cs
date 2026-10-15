#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Linq;
using System.Reflection;
using FishSocial.Desktop;
using FishSocial.Desktop.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyRedesignLayoutTests
    {
        const string Folder = "Assets/Resources/Desktop/Prefabs/";

        [TestCase("PanelLeaderboard")]
        [TestCase("PanelProfileHub")]
        [TestCase("PanelAchievements")]
        [TestCase("PanelSocial")]
        [TestCase("PanelCatch")]
        [TestCase("PanelEquipment")]
        [TestCase("PanelShop")]
        [TestCase("PanelSocialFeed")]
        public void SavedPageKeepsHeaderCloseAndSlicedSurfaces(string name)
        {
            var page = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            Assert.NotNull(page);
            Assert.AreEqual(PrefabAssetType.Variant, PrefabUtility.GetPrefabAssetType(page));
            var close = page.transform.Find("DesignHeader/Close") as RectTransform;
            Assert.NotNull(close);
            Assert.NotNull(close.GetComponent<Button>());
            Assert.IsTrue(close.gameObject.activeSelf);
            Assert.AreEqual(Vector2.one, close.anchorMin, name);
            Assert.AreEqual(Vector2.one, close.anchorMax, name);
            Assert.AreEqual(new Vector2(-24, -22), close.anchoredPosition, name);
            Assert.AreEqual(52, close.sizeDelta.x);
            Assert.That(page.transform.Find("Reflow_PageSeal")?.gameObject.activeSelf, Is.Not.True);
            foreach (var image in page.GetComponentsInChildren<Image>(true))
                if (image.sprite != null && image.sprite.border != Vector4.zero)
                    Assert.That(image.type, Is.EqualTo(Image.Type.Sliced).Or.EqualTo(Image.Type.Filled), image.name);
            foreach (var button in page.GetComponentsInChildren<Button>(true))
                foreach (var label in button.GetComponentsInChildren<Text>())
                    Assert.AreNotEqual("按钮", label.text, name + "/" + button.name);
        }

        [TestCase("PanelCatch", "Grid/Viewport/Slots", 3)]
        [TestCase("PanelEquipment", "Items/Viewport/Content", 2)]
        [TestCase("PanelShop", "Items/Viewport/Content", 3)]
        public void ItemGridKeepsExplicitColumnCount(string name, string path, int columns)
        {
            var page = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            var grid = page.transform.Find(path).GetComponent<GridLayoutGroup>();
            Assert.NotNull(grid);
            Assert.IsTrue(grid.enabled);
            Assert.AreEqual(GridLayoutGroup.Constraint.FixedColumnCount, grid.constraint);
            Assert.AreEqual(columns, grid.constraintCount);
        }

        [Test]
        public void AtlasContainsIndependentFishAndProductSprites()
        {
            Assert.AreEqual(12, Resources.LoadAll<Sprite>("Desktop/Theme/Cozy/items/items-atlas").Length);
            Assert.AreEqual(6, Resources.LoadAll<Sprite>("Desktop/Theme/Cozy/items/fish-atlas").Length);
            Assert.NotNull(CozyItemArt.Fish("koi"));
            Assert.IsNull(CozyItemArt.Fish("unillustrated_species"));
        }

        [Test]
        public void LeaderboardBindingDoesNotReapplyLegacyGeometry()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PanelLeaderboard.prefab");
            var instance = Object.Instantiate(asset);
            try
            {
                var podium = instance.transform.Find("Podium/Slot1") as RectTransform;
                var before = podium.sizeDelta;
                instance.GetComponent<DesktopLeaderboardPanel>().Bind(null, null);
                Assert.AreEqual(before, podium.sizeDelta);
                Assert.IsFalse(instance.transform.Find("Tabs").GetComponents<HorizontalLayoutGroup>().Any(group => group.enabled));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void ProfileRefinementPreservesSavedSidebarWidth()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PanelProfileHub.prefab");
            var instance = Object.Instantiate(asset);
            try
            {
                var tabs = instance.transform.Find("Shell/Sidebar") as RectTransform;
                var before = tabs.sizeDelta;
                typeof(DesktopProfileHubPanel).GetMethod("RefineLayout", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(instance.GetComponent<DesktopProfileHubPanel>(), null);
                Assert.AreEqual(before, tabs.sizeDelta);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void AllPlayerPrefabContractsPass()
        {
            CollectionAssert.IsEmpty(CozyUiBuildGuard.CollectErrors());
        }

        [TestCase("PanelEquipment")]
        [TestCase("PanelShop")]
        public void DetailPanelIsNotDrivenByOldScrollMask(string name)
        {
            var page = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            var details = page.transform.Find("Details");
            Assert.IsFalse(details.GetComponents<ScrollRect>().Any(scroll => scroll.enabled));
            Assert.IsFalse(details.GetComponents<RectMask2D>().Any(mask => mask.enabled));
            Assert.IsNull(details.Find("Description"));
            var descriptionScroll = (RectTransform)details.Find("DescriptionScroll");
            var scroll = descriptionScroll.GetComponent<ScrollRect>();
            Assert.IsTrue(scroll.enabled && scroll.vertical && !scroll.horizontal);
            Assert.AreSame(descriptionScroll, scroll.viewport);
            Assert.AreSame(descriptionScroll.Find("Description"), scroll.content);
            Assert.IsTrue(descriptionScroll.GetComponent<RectMask2D>().enabled);
            var buy = (RectTransform)details.Find("Buy");
            var scrollBottom = -descriptionScroll.anchoredPosition.y + descriptionScroll.sizeDelta.y;
            Assert.LessOrEqual(scrollBottom, -buy.anchoredPosition.y);
        }
    }
}
#endif
