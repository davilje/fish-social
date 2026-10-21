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
    public sealed class CozyJournalDetailParityTests
    {
        const string Folder = "Assets/Resources/Desktop/Prefabs/";

        [TestCase("PanelProfileHub")]
        [TestCase("PanelAchievements")]
        public void AchievementListConvertsToThreeColumnsAndRemainsIdempotent(string name)
        {
            var page = PrefabUtility.LoadPrefabContents(Folder + name + ".prefab");
            try
            {
                CozyRedesignAuthoring.ApplyDetailParity(page);
                CozyRedesignAuthoring.ApplyDetailParity(page);
                var scroll = page.transform.Find("Shell/Content/Tab_成就/Achievements").GetComponent<ScrollRect>();
                var grid = scroll.content.GetComponent<GridLayoutGroup>();
                Assert.NotNull(grid);
                Assert.IsTrue(grid.enabled);
                Assert.AreEqual(3, grid.constraintCount);
                Assert.AreEqual(new Vector2(215, 258), grid.cellSize);
                Assert.AreEqual(1, scroll.content.GetComponents<LayoutGroup>().Length);
                Assert.AreEqual(new Vector2(710, 440), ((RectTransform)scroll.transform).sizeDelta);
            }
            finally { PrefabUtility.UnloadPrefabContents(page); }
        }

        [Test]
        public void ProfileKeepsFiveTabsRealActionsAndDistinctCollectionTemplates()
        {
            var page = PrefabUtility.LoadPrefabContents(Folder + "PanelProfileHub.prefab");
            try
            {
                CozyRedesignAuthoring.ApplyDetailParity(page);
                foreach (var name in new[] { "资料", "展示柜", "图鉴", "相册", "成就" })
                    Assert.NotNull(page.transform.Find("Shell/Sidebar/Tab_" + name).GetComponent<Button>());
                var aside = page.transform.Find("Shell/Content/Tab_资料/DesignShowcase");
                Assert.Greater(aside.Find("JournalPhoto").GetSiblingIndex(), aside.Find("JournalPaper").GetSiblingIndex());
                Assert.Greater(aside.Find("JournalPhotoInset").GetSiblingIndex(), aside.Find("JournalPhoto").GetSiblingIndex());
                Assert.Greater(aside.Find("Art").GetSiblingIndex(), aside.Find("JournalPhotoInset").GetSiblingIndex());
                foreach (var tabName in new[] { "资料", "展示柜", "图鉴", "相册", "成就" })
                {
                    var icon = page.transform.Find("Shell/Sidebar/Tab_" + tabName + "/Icon").GetComponent<Image>();
                    Assert.IsTrue(icon.enabled && icon.gameObject.activeSelf);
                    Assert.NotNull(icon.sprite);
                }
                Assert.NotNull(page.transform.Find("Shell/Sidebar/JournalRail/JournalPaper").GetComponent<Image>().sprite);
                var landscape = page.transform.Find("Shell/Content/Tab_资料/JournalIdentity/Landscape").GetComponent<Image>();
                Assert.AreEqual(.34f, landscape.color.a, .001f);
                Assert.NotNull(landscape.GetComponent<CozyPondPhotoCover>());
                Assert.AreEqual(new Vector2(680, 150), ((RectTransform)page.transform.Find("Shell/Content/Tab_资料/JournalIdentity")).sizeDelta);
                Assert.NotNull(page.transform.Find("Shell/Content/Tab_资料/JournalProgress/Track/Fill").GetComponent<Image>());
                foreach (var name in new[] { "展示柜", "图鉴" })
                {
                    var template = page.transform.Find("Shell/Content/Tab_" + name + "/JournalCardTemplate");
                    Assert.NotNull(template.GetComponent<Button>());
                    Assert.IsFalse(template.gameObject.activeSelf);
                    Assert.NotNull(template.Find("DesignItemArt").GetComponent<Image>());
                    Assert.AreEqual(Quaternion.identity, template.localRotation);
                }
                foreach (var image in page.GetComponentsInChildren<Image>(true))
                    if (image.sprite != null) StringAssert.DoesNotContain("ReferenceAssets", AssetDatabase.GetAssetPath(image.sprite));
            }
            finally { PrefabUtility.UnloadPrefabContents(page); }
        }

        [Test]
        public void PostBindingKeepsNativeAvatarAndFishStripAndGrowsForLongStories()
        {
            var page = PrefabUtility.LoadPrefabContents(Folder + "SocialPostCard.prefab");
            try
            {
                CozyRedesignAuthoring.ApplyDetailParity(page);
                var card = page.GetComponent<DesktopSocialPostCard>();
                var post = new SocialPostDto
                {
                    id = "parity-post", playerId = "test-cat", nickname = "钓友甲", text = "湖边的收获", visibility = "friends",
                    fish = new FishInventoryItemDto { id = "fish-a", speciesId = "koi", quality = "rare", sizeM = .62f },
                };
                card.Bind(post, null);
                Assert.AreEqual("钓友甲", page.transform.Find("Header/AuthorText").GetComponent<Text>().text);
                Assert.AreEqual("仅好友", page.transform.Find("Header/JournalVisibility").GetComponent<Text>().text);
                Assert.NotNull(page.transform.Find("JournalCatch/Art").GetComponent<Image>().sprite);
                Assert.IsFalse(page.transform.Find("Photo").gameObject.activeSelf);
                var firstHeight = page.GetComponent<LayoutElement>().preferredHeight;
                post.text = new string('鱼', 160);
                card.Bind(post, null);
                Assert.Greater(page.GetComponent<LayoutElement>().preferredHeight, firstHeight);
                var footer = (RectTransform)page.transform.Find("Actions");
                var catchStrip = (RectTransform)page.transform.Find("JournalCatch");
                Assert.Greater(-footer.anchoredPosition.y, -catchStrip.anchoredPosition.y + catchStrip.sizeDelta.y);
                Assert.AreEqual(Quaternion.identity, page.transform.localRotation);
            }
            finally { PrefabUtility.UnloadPrefabContents(page); }
        }
    }
}
#endif
