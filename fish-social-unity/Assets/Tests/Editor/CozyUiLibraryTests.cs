#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Editor;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyUiLibraryTests
    {
        [Test]
        public void EveryCatalogPrefabHasCompleteImportedComponentsAndLayout()
        {
            Assert.IsEmpty(CozyUiBuildGuard.CollectErrors());
        }

        [TestCase("PanelShop", "Items")]
        [TestCase("PanelEquipment", "Items")]
        [TestCase("PanelPondSelection", "PondCardScroll")]
        [TestCase("PanelLeaderboard", "Scroll")]
        [TestCase("PanelSocialFeed", "Scroll")]
        [TestCase("PanelProfileHub", "Shell/Content/Tab_展示柜/ShowcaseGrid")]
        [TestCase("PanelProfileHub", "Shell/Content/Tab_图鉴/CodexGrid")]
        [TestCase("PanelProfileHub", "Shell/Content/Tab_成就/Achievements")]
        [TestCase("PanelProfileEdit", "Avatars")]
        [TestCase("PanelProfileEdit", "Showcase")]
        [TestCase("PanelProfileEdit", "Picker")]
        public void PreviouslyMissingScrollScriptsResolveToRealUguiComponents(string name, string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.AssetPath(name));
            Assert.NotNull(prefab);
            var node = prefab.transform.Find(path);
            Assert.NotNull(node, name + "/" + path);
            var scroll = node.GetComponent<ScrollRect>();
            Assert.NotNull(scroll, name + "/" + path + ": ScrollRect was stripped or its script GUID is invalid.");
            Assert.NotNull(scroll.content);
            Assert.NotNull(scroll.viewport);
        }

        [TestCase("PanelWorldMap", "Details/ModeSell")]
        [TestCase("PanelWorldMap", "Details/ModeAuto")]
        public void MapFeeModeButtonsAreSerialized(string name, string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.AssetPath(name));
            Assert.NotNull(prefab.transform.Find(path).GetComponent<Button>());
        }

        [TestCase("PanelShop")]
        [TestCase("PanelEquipment")]
        public void ShopAndEquipmentHaveSerializedHeaderTitles(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.AssetPath(name));
            Assert.NotNull(prefab.transform.Find("Header/Title").GetComponent<Text>());
        }
    }
}
#endif
