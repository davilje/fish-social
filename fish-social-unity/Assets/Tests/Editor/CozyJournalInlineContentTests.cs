#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using FishSocial.Desktop;
using FishSocial.Desktop.Editor;
using UnityEditor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyJournalInlineContentTests
    {
        GameObject root;
        string assetPath;
        RectTransform CreateRoot(float width = 326, float height = 58)
        {
            root = new GameObject("InlineFixture",typeof(RectTransform));
            var rect = (RectTransform)root.transform; rect.sizeDelta = new Vector2(width,height);
            return rect;
        }
        Text Text(string name, string content, int size = 23)
        {
            var child = new GameObject(name,typeof(RectTransform),typeof(Text));
            child.transform.SetParent(root.transform,false);
            var label = child.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = size; label.text = content; return label;
        }
        Image Icon(string name)
        {
            var child = new GameObject(name,typeof(RectTransform),typeof(Image)); child.transform.SetParent(root.transform,false);
            var icon = child.GetComponent<Image>(); icon.sprite = CatTheme.LoadCozy("icons/coin.png",Vector4.zero);
            Assert.IsNotNull(icon.sprite); return icon;
        }
        static float Left(RectTransform rect) => rect.anchoredPosition.x;
        static float Right(RectTransform rect) => rect.anchoredPosition.x + rect.sizeDelta.x;
        [TearDown] public void Cleanup()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (!string.IsNullOrEmpty(assetPath)) AssetDatabase.DeleteAsset(assetPath);
        }

        [Test]
        public void AuthoredDesktopShellPersistsProfileInlineBindingsAfterSaveAndReload()
        {
            var sourcePath = DesktopPrefabCatalog.AssetPath("DesktopShell");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            Assert.IsNotNull(source);
            AssertProfileBindings(source.transform);
            assetPath = "Assets/Tests/Editor/InlineShellRoundTrip-" + System.Guid.NewGuid().ToString("N") + ".prefab";
            Assert.IsTrue(AssetDatabase.CopyAsset(sourcePath,assetPath));
            var contents = PrefabUtility.LoadPrefabContents(assetPath);
            try { CozyRedesignAuthoring.SaveJournalPrefab(contents,assetPath); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.ImportAsset(assetPath,ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath));
            AssertProfileBindings(root.transform);
            var card = (RectTransform)root.transform.Find("MainRoot/Header/ProfileCard");
            var name = card.Find("Name").GetComponent<Text>();
            name.text = "短名";
            CozyJournalInlineContent.ReflowTree(root.transform);
            var first = ((RectTransform)card.Find("LevelBadge")).anchoredPosition.x;
            name.text = "更长的真实昵称";
            CozyJournalInlineContent.ReflowTree(root.transform);
            var badge = (RectTransform)card.Find("LevelBadge");
            Assert.Greater(badge.anchoredPosition.x,first,"Authored component must respond to live nickname changes");
            Assert.AreEqual(14,badge.anchoredPosition.x - Right(name.rectTransform),.01f);
        }

        static void AssertProfileBindings(Transform shell)
        {
            var card = shell.Find("MainRoot/Header/ProfileCard");
            Assert.IsNotNull(card,"Check the actual production hierarchy, not a synthetic Header root");
            var inline = card.GetComponent<CozyJournalInlineContent>();
            Assert.IsNotNull(inline,"ApplyInlineParity must attach to the actual authored ProfileCard");
            var serialized = new SerializedObject(inline);
            Assert.AreEqual(card,serialized.FindProperty("row").objectReferenceValue);
            Assert.AreEqual(card.Find("Name").GetComponent<Text>(),serialized.FindProperty("label").objectReferenceValue);
            Assert.AreEqual(card.Find("LevelBadge").GetComponent<Image>(),serialized.FindProperty("icon").objectReferenceValue);
            Assert.AreEqual(card.Find("Level").GetComponent<Text>(),serialized.FindProperty("badgeLabel").objectReferenceValue);
        }

        [Test]
        public void PurchaseContentRecentersAfterTextAndThemeChangesWithoutMovingHitRoot()
        {
            var rect = CreateRoot(); var label = Text("Label","Buy 1200"); var coin = Icon("Icon"); var fish = Icon("InlineFish");
            root.AddComponent<Image>(); root.AddComponent<Button>();
            var inline = root.AddComponent<CozyJournalInlineContent>();
            inline.ConfigureCentered(rect,label,coin,new Vector2(28,28),10,12,fish);
            var position = rect.anchoredPosition; var size = rect.sizeDelta; var pivot = rect.pivot;
            foreach (var value in new[] { "Buy 1200", "Buy 1,200,000", "Buy" })
            {
                label.text = value;
                DesktopModalUi.StyleTree(root.transform);
                CozyJournalInlineContent.ReflowTree(root.transform);
                Assert.AreEqual(10,Left(coin.rectTransform)-Right(fish.rectTransform),.01f);
                Assert.AreEqual(10,Left(label.rectTransform)-Right(coin.rectTransform),.01f);
                Assert.AreEqual(rect.rect.width*.5f,(Left(fish.rectTransform)+Right(label.rectTransform))*.5f,.01f);
                Assert.AreEqual(position,rect.anchoredPosition); Assert.AreEqual(size,rect.sizeDelta); Assert.AreEqual(pivot,rect.pivot);
            }
            Assert.AreSame(root.transform,label.transform.parent,"Existing Label binding must stay direct");
        }

        [Test]
        public void PriceCoinDisappearsForNonPriceAndReturnsForNumericText()
        {
            var rect = CreateRoot(190,20); var label = Text("Meta","1,200",15); var coin = Icon("MetaCoin");
            var inline = root.AddComponent<CozyJournalInlineContent>();
            inline.ConfigureCentered(rect,label,coin,new Vector2(18,18),6,0,numericPriceOnly:true);
            Assert.IsTrue(coin.gameObject.activeSelf);
            Assert.AreEqual(6,Left(label.rectTransform)-Right(coin.rectTransform),.01f);
            label.text = "Owned"; inline.Reflow();
            Assert.IsFalse(coin.gameObject.activeSelf);
            Assert.AreEqual(95,(Left(label.rectTransform)+Right(label.rectTransform))*.5f,.01f);
            label.text = "0"; inline.Reflow();
            Assert.IsTrue(coin.gameObject.activeSelf);
            Assert.AreEqual(18,coin.rectTransform.rect.width,.01f);
        }

        [TestCase("1200",true)]
        [TestCase("1,200",true)]
        [TestCase("0",true)]
        [TestCase("已拥有",false)]
        [TestCase("Lv.12 解锁",false)]
        [TestCase("120 / 次",false)]
        [TestCase("",false)]
        [TestCase("-120",false)]
        public void OnlyNumericPricesReceiveCoins(string value, bool expected)
            => Assert.AreEqual(expected,CozyJournalInlineContent.IsNumericPrice(value));

        [Test]
        public void NicknameBadgeTracksMeasuredNameAndLongNamesStayAboveXp()
        {
            var rect = CreateRoot(415,103);
            var nickname = Text("Name","Li",26); var badge = Icon("LevelBadge"); var level = Text("Level","Lv.8",20);
            var xp = Icon("XpBackground"); xp.rectTransform.anchoredPosition = new Vector2(110,-64); xp.rectTransform.sizeDelta = new Vector2(285,18);
            var originalXp = xp.rectTransform.anchoredPosition;
            var inline = root.AddComponent<CozyJournalInlineContent>(); inline.ConfigureProfile(rect,nickname,badge,level);
            var shortWidth = nickname.rectTransform.rect.width;
            Assert.AreEqual(14,Left(badge.rectTransform)-Right(nickname.rectTransform),.01f);
            nickname.text = new string('W',80); level.text = "Lv.999"; inline.Reflow();
            Assert.Greater(nickname.rectTransform.rect.width,shortWidth);
            Assert.LessOrEqual(nickname.rectTransform.rect.width,201);
            Assert.AreEqual(14,Left(badge.rectTransform)-Right(nickname.rectTransform),.01f);
            Assert.LessOrEqual(Right(badge.rectTransform),395.01f);
            Assert.IsTrue(nickname.resizeTextForBestFit);
            Assert.AreEqual(HorizontalWrapMode.Wrap,nickname.horizontalOverflow);
            Assert.AreEqual(VerticalWrapMode.Truncate,nickname.verticalOverflow);
            Assert.AreEqual(new string('W',80),nickname.text,"Truncation must not overwrite the actual nickname");
            Assert.AreEqual(originalXp,xp.rectTransform.anchoredPosition);
            Assert.Less(-nickname.rectTransform.anchoredPosition.y + nickname.rectTransform.rect.height,64);
            nickname.text = "Li"; inline.Reflow();
            Assert.AreEqual(shortWidth,nickname.rectTransform.rect.width,.01f);
            Assert.IsFalse(nickname.resizeTextForBestFit);
        }

        [Test]
        public void NarrowActionKeepsTextInsidePaddingInsteadOfOverlappingIcon()
        {
            var rect = CreateRoot(142,58); var label = Text("Label",new string('W',30),20); var icon = Icon("Icon");
            var inline = root.AddComponent<CozyJournalInlineContent>();
            inline.ConfigureCentered(rect,label,icon,new Vector2(30,22),10,12);
            Assert.GreaterOrEqual(Left(icon.rectTransform),12);
            Assert.LessOrEqual(Right(label.rectTransform),130);
            Assert.AreEqual(10,Left(label.rectTransform)-Right(icon.rectTransform),.01f);
            Assert.IsTrue(label.resizeTextForBestFit);
        }
    }
}
#endif
