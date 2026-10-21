#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Linq;
using FishSocial.Desktop;
using FishSocial.Desktop.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyJournalPersistenceTests
    {
        GameObject root;
        string assetPath;

        [TearDown]
        public void Cleanup()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (!string.IsNullOrEmpty(assetPath)) AssetDatabase.DeleteAsset(assetPath);
        }

        Button CreateButton()
        {
            root = new GameObject("JournalPersistenceFixture", typeof(RectTransform), typeof(Image), typeof(Button));
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(root.transform, false);
            label.GetComponent<Text>().text = "鱼塘";
            label.GetComponent<Text>().font = CatTheme.Font;
            var button = root.GetComponent<Button>();
            CatTheme.StyleButton(button);
            button.GetComponent<CozyButtonVisual>().ConfigureSurface("surfaces/tab-index-default", "surfaces/tab-index-hover",
                null, "surfaces/tab-index-selected", "surfaces/tab-index-disabled");
            return button;
        }

        [TestCase(false, true, "default")]
        [TestCase(true, true, "selected")]
        [TestCase(true, false, "disabled")]
        public void RepairKeepsJournalConfiguredStatesAndInsets(bool selected, bool interactable, string state)
        {
            var button = CreateButton();
            button.GetComponent<CozyButtonVisual>().SetSelected(selected);
            button.interactable = interactable;
            CozyPrefabRepair.RepairContents(root);
            CozyPrefabRepair.RepairContents(root);
            var plate = root.transform.Find("CozyVisual").GetComponent<Image>();
            Assert.AreSame(CozyJournalTheme.Sprite("surfaces/tab-index-" + state), plate.sprite);
            Assert.AreSame(CozyJournalTheme.Sprite("surfaces/tab-index-selected"), button.spriteState.selectedSprite);
            Assert.AreSame(CozyJournalTheme.Sprite("surfaces/tab-index-disabled"), button.spriteState.disabledSprite);
            Assert.AreEqual(1f, plate.pixelsPerUnitMultiplier);
            Assert.AreEqual(state == "disabled" ? Vector2.zero : new Vector2(-6, -6), plate.rectTransform.offsetMin);
            Assert.IsEmpty(CozyPrefabRepair.ValidateContents(root, root.name));
        }

        [Test]
        public void PersistentPrefabUsesSerializedStateWithoutAPlayModeActiveFlag()
        {
            CreateButton();
            CozyPrefabRepair.RepairContents(root);
            assetPath = "Assets/Tests/Editor/JournalPersistence-" + System.Guid.NewGuid().ToString("N") + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Assert.IsNotNull(saved);
            Assert.IsTrue(saved.GetComponent<Button>().interactable);
            Assert.IsEmpty(CozyPrefabRepair.ValidateContents(saved, saved.name));
        }

        GameObject RoundTripProductionCopy(string prefabName)
        {
            var source = DesktopPrefabCatalog.AssetPath(prefabName);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(source), source);
            assetPath = "Assets/Tests/Editor/JournalRoundTrip-" + System.Guid.NewGuid().ToString("N") + ".prefab";
            Assert.IsTrue(AssetDatabase.CopyAsset(source, assetPath), "Copy the real authored variant, including nested overrides");
            for (var pass = 0; pass < 2; pass++)
            {
                var contents = PrefabUtility.LoadPrefabContents(assetPath);
                try { CozyRedesignAuthoring.SaveJournalPrefab(contents, assetPath); }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Assert.IsNotNull(saved);
            root = (GameObject)PrefabUtility.InstantiatePrefab(saved);
            Assert.IsNotNull(root);
            root.SetActive(true);
            return root;
        }

        static void AssertFamily(GameObject instance, string nodePath, string family)
        {
            var node = string.IsNullOrEmpty(nodePath) ? instance.transform : instance.transform.Find(nodePath);
            Assert.IsNotNull(node, "Live hierarchy must retain " + nodePath);
            var visual = node.GetComponent<CozyButtonVisual>();
            Assert.IsNotNull(visual, nodePath);
            var serialized = new SerializedObject(visual);
            Assert.AreEqual(family, serialized.FindProperty("surfaceNormal").stringValue, nodePath + " lost its nested family override");
            Assert.AreSame(CozyJournalTheme.Sprite(family), serialized.FindProperty("normalSprite").objectReferenceValue);
            var plate = node.Find("CozyVisual")?.GetComponent<Image>();
            Assert.IsNotNull(plate, nodePath);
            var path = AssetDatabase.GetAssetPath(plate.sprite);
            StringAssert.Contains(family.StartsWith("surfaces/tab-index") ? "/journal/surfaces/tab-index-" : "/journal/" + family, path);
        }

        [Test]
        public void FriendRowKeepsRealNameAndActionsAfterRepeatedSaveReloadAndSkin()
        {
            var instance = RoundTripProductionCopy("FriendRow");
            for (var pass = 0; pass < 2; pass++)
            {
                Assert.IsNotNull(instance.transform.Find("Name")?.GetComponent<Text>(), "Friend nickname detached from its live root");
                Assert.IsNotNull(instance.transform.Find("Dm")?.GetComponent<Button>(), "Friend direct-message action detached");
                Assert.IsNotNull(instance.transform.Find("Remove")?.GetComponent<Button>(), "Friend removal action detached");
                foreach (var name in new[] { "Name", "Dm", "Remove" })
                    Assert.AreEqual(1, instance.GetComponentsInChildren<Transform>(true).Count(node => node.name == name),
                        name + " was duplicated by noncanonical aliases");
                CatTheme.SkinTree(instance.transform);
            }
        }

        [TestCase("PanelShop")]
        [TestCase("PanelEquipment")]
        public void ProductionTackleTabKeepsIndexFamilyAfterRepeatedSaveReloadAndSkin(string prefabName)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.AssetPath(prefabName));
            AssertFamily(source, "Tabs/Tackle", "surfaces/tab-index-default");
            var instance = RoundTripProductionCopy(prefabName);
            AssertFamily(instance, "Tabs/Tackle", "surfaces/tab-index-default");
            CatTheme.SkinTree(instance.transform);
            DesktopModalUi.StyleTree(instance.transform);
            AssertFamily(instance, "Tabs/Tackle", "surfaces/tab-index-default");
        }

        [Test]
        public void ProductionShopCardKeepsStickerFamilyAfterRepeatedSaveReloadAndSkin()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.AssetPath("ShopItemCard"));
            AssertFamily(source, "", "surfaces/sticker-card");
            var instance = RoundTripProductionCopy("ShopItemCard");
            AssertFamily(instance, "", "surfaces/sticker-card");
            CatTheme.SkinTree(instance.transform);
            DesktopModalUi.StyleTree(instance.transform);
            AssertFamily(instance, "", "surfaces/sticker-card");
        }

        [Test]
        public void GuardRejectsLegacyDisabledStateOnJournalButton()
        {
            var button = CreateButton();
            CozyPrefabRepair.RepairContents(root);
            var states = button.spriteState;
            states.disabledSprite = CozyJournalTheme.Sprite("surfaces/tab-index-default");
            button.spriteState = states;
            Assert.IsTrue(CozyPrefabRepair.ValidateContents(root, root.name).Any(error => error.Contains("SpriteState")));
        }

        [Test]
        public void NativeMapMarkerDoesNotAcquireAButtonPlateDuringRepair()
        {
            root = new GameObject("JournalMarkerFixture", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CozyPondMarkerView));
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(root.transform, false);
            icon.GetComponent<Image>().sprite = CozyJournalTheme.Sprite("motion/bobber");
            CozyPrefabRepair.RepairContents(root);
            CatTheme.StyleButton(root.GetComponent<Button>());
            CatTheme.SkinTree(root.transform);
            DesktopModalUi.StyleTree(root.transform);
            Assert.IsNull(root.GetComponent<CozyButtonVisual>());
            Assert.IsNull(root.transform.Find("CozyVisual"));
            Assert.IsEmpty(CozyPrefabRepair.ValidateContents(root, root.name));
        }
    }
}
#endif
