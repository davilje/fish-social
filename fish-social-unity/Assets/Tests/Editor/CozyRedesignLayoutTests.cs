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
        [TestCase("PanelProfileEdit")]
        [TestCase("PanelPondSelection")]
        [TestCase("PanelWorldMap")]
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

        [TestCase("CatchSlot", 214)]
        [TestCase("ShopItemCard", 214)]
        [TestCase("ShopItemCard", 326)]
        public void ItemArtworkStaysInsideResizedCard(string name, float width)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            var instance = Object.Instantiate(prefab);
            try
            {
                var card = instance.GetComponent<RectTransform>();
                card.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                foreach (var childName in new[] { "DesignArtBacking", "DesignItemArt" })
                {
                    var child = instance.transform.Find(childName).GetComponent<RectTransform>();
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(card, child);
                    Assert.GreaterOrEqual(bounds.min.x, card.rect.xMin, childName);
                    Assert.LessOrEqual(bounds.max.x, card.rect.xMax, childName);
                    Assert.AreEqual(card.rect.center.x, bounds.center.x, 0.01f, childName);
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void EveryDefaultCatHasItsOwnLocalPortrait()
        {
            var portraits = DesktopDefaultAvatars.All.Select(entry => DesktopDefaultAvatars.Portrait(entry.Path)).ToArray();
            Assert.IsTrue(portraits.All(sprite => sprite != null));
            Assert.AreEqual(DesktopDefaultAvatars.All.Length, portraits.Distinct().Count());
            foreach (var portrait in portraits)
                StringAssert.Contains("/characters/avatars/", AssetDatabase.GetAssetPath(portrait));
        }

        [Test]
        public void ProfileEditorRetainsFormPickerAndSixAvatarColumns()
        {
            var page = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PanelProfileEdit.prefab");
            Assert.AreEqual(6, page.transform.Find("Avatars/Viewport/Content").GetComponent<GridLayoutGroup>().constraintCount);
            Assert.NotNull(page.transform.Find("Form/Nickname").GetComponent<InputField>());
            Assert.NotNull(page.transform.Find("Form/Bio").GetComponent<InputField>());
            Assert.NotNull(page.transform.Find("Actions/Save").GetComponent<Button>());
            Assert.IsFalse(page.transform.Find("Picker").gameObject.activeSelf);
            foreach (var node in page.GetComponentsInChildren<RectTransform>(true))
                if (node.name.StartsWith("Reflow_")) Assert.IsFalse(node.gameObject.activeSelf, node.name);
        }

        [Test]
        public void MapBackdropDoesNotCoverViewportAndPondCardsKeepVisibleIcons()
        {
            var map = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PanelWorldMap.prefab");
            Assert.Less(map.transform.Find("DesignMapFrame").GetSiblingIndex(), map.transform.Find("Viewport").GetSiblingIndex());
            Assert.IsFalse(map.transform.Find("Details/Reflow_Heading").gameObject.activeSelf);
            var card = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "PondSelectionCard.prefab");
            Assert.IsTrue(card.transform.Find("PondIcon").GetComponent<Image>().enabled);
            Assert.GreaterOrEqual(card.GetComponent<LayoutElement>().preferredHeight, 190);
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
