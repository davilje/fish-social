#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop;
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

        [TestCase("DesktopShell", "MainRoot/Header/Home")]
        [TestCase("FeatureModalRoot", "ModalPanel/Header/Close")]
        [TestCase("PanelProfileEdit", "Header/Back")]
        [TestCase("PanelProfileEdit", "Picker/Close")]
        [TestCase("PanelPondSettlement", "Panel/Close")]
        public void ClosingActionsUseRightTopAnchors(string name, string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.AssetPath(name));
            var rect = prefab.transform.Find(path).GetComponent<RectTransform>();
            Assert.AreEqual(Vector2.one, rect.anchorMin);
            Assert.AreEqual(Vector2.one, rect.anchorMax);
            Assert.AreEqual(Vector2.one, rect.pivot);
            Assert.LessOrEqual(rect.anchoredPosition.x, 0f);
            Assert.LessOrEqual(rect.anchoredPosition.y, 0f);
        }

        [Test]
        public void RuntimeButtonStylingDoesNotDoubleTintCarvedIconsOrWrapActions()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.AssetPath("PanelLeaderboard"));
            var instance = Object.Instantiate(prefab);
            try
            {
                var button = instance.transform.Find("Tabs/Daily").GetComponent<Button>();
                var icon = button.transform.Find("Icon").GetComponent<Image>();
                var label = button.transform.Find("Label").GetComponent<Text>();
                var anchor = icon.rectTransform.anchoredPosition;
                icon.color = CatTheme.Ink;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                DesktopModalUi.StyleButton(button, "fish");
                Assert.AreEqual(Color.white, icon.color);
                Assert.AreSame(CatTheme.LoadCozy("icons/fish.png", Vector4.zero), icon.sprite);
                Assert.AreEqual(anchor, icon.rectTransform.anchoredPosition);
                Assert.AreEqual(HorizontalWrapMode.Overflow, label.horizontalOverflow);
                Assert.IsTrue(label.resizeTextForBestFit);
                Assert.IsFalse(icon.raycastTarget);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void LoginCardMatchesSmallWindowCanvas()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.AssetPath("DesktopShell"));
            Assert.NotNull(prefab);
            var login = prefab.transform.Find("LoginRoot").GetComponent<RectTransform>();
            var center = new Vector2(0.5f, 0.5f);
            Assert.AreEqual(center, login.anchorMin);
            Assert.AreEqual(center, login.anchorMax);
            Assert.AreEqual(center, login.pivot);
            Assert.AreEqual(Vector2.zero, login.anchoredPosition);
            Assert.AreEqual(new Vector2(WindowManager.LoginWidth, WindowManager.LoginHeight), login.sizeDelta);
        }

        [Test]
        public void VisibleLoginGraphicsStayInsideSmallWindow()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.AssetPath("DesktopShell"));
            var instance = Object.Instantiate(prefab);
            try
            {
                var scaler = instance.GetComponent<CanvasScaler>();
                scaler.referenceResolution = new Vector2(WindowManager.LoginWidth, WindowManager.LoginHeight);
                var login = instance.transform.Find("LoginRoot").GetComponent<RectTransform>();
                login.gameObject.SetActive(true);
                instance.transform.Find("MainRoot").gameObject.SetActive(false);
                Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4];
                foreach (var graphic in login.GetComponentsInChildren<Graphic>(false))
                {
                    if (!graphic.enabled)
                        continue;
                    graphic.rectTransform.GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = login.InverseTransformPoint(corner);
                        Assert.That(point.x, Is.InRange(login.rect.xMin - 0.5f, login.rect.xMax + 0.5f), graphic.name);
                        Assert.That(point.y, Is.InRange(login.rect.yMin - 0.5f, login.rect.yMax + 0.5f), graphic.name);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [TestCase(28f, 28f)]
        [TestCase(80f, 44f)]
        [TestCase(352f, 54f)]
        [TestCase(420f, 180f)]
        public void BorderedSurfacesGenerateSlicedMeshesInsteadOfStretchedQuads(float width, float height)
        {
            var root = new GameObject("NineSliceTest", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                var image = root.GetComponent<Image>();
                image.sprite = CatTheme.LoadCozy("buttons/plate-default.png", CatTheme.PlateBorder);
                Assert.NotNull(image.sprite);
                Assert.AreNotEqual(Vector4.zero, image.sprite.border);
                image.rectTransform.sizeDelta = new Vector2(width, height);
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                CatTheme.ApplySlicedImage(image);
                Assert.AreEqual(Image.Type.Sliced, image.type);
                Assert.IsFalse(image.preserveAspect);
                var populate = typeof(Image).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(populate);
                using (var vertices = new VertexHelper())
                {
                    populate.Invoke(image, new object[] { vertices });
                    Assert.Greater(vertices.currentVertCount, 4, "A four-vertex quad would stretch the entire texture.");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("buttons/plate-default.png", 64, 63, 10f, 28f, 28f)]
        [TestCase("buttons/plate-hover.png", 64, 63, 10f, 80f, 44f)]
        [TestCase("buttons/plate-pressed.png", 64, 63, 10f, 352f, 54f)]
        [TestCase("panels/paper-compact.png", 64, 63, 10f, 278f, 25f)]
        [TestCase("panels/paper.png", 128, 125, 20f, 420f, 180f)]
        public void ProductionSurfacesKeepUncompressedCornersAndPositiveCenters(string path, int sourceWidth,
            int sourceHeight, float border, float width, float height)
        {
            var root = new GameObject("SurfaceSizeTest", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                var image = root.GetComponent<Image>();
                image.sprite = CatTheme.LoadCozy(path, Vector4.zero);
                Assert.NotNull(image.sprite);
                Assert.AreEqual(sourceWidth, image.sprite.rect.width);
                Assert.AreEqual(sourceHeight, image.sprite.rect.height);
                Assert.AreEqual(new Vector4(border, border, border, border), image.sprite.border);
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 1f;
                image.rectTransform.sizeDelta = new Vector2(width, height);
                var populate = typeof(Image).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic);
                var horizontal = new SortedSet<float>();
                var vertical = new SortedSet<float>();
                using (var vertices = new VertexHelper())
                {
                    populate.Invoke(image, new object[] { vertices });
                    var vertex = default(UIVertex);
                    for (var vertexIndex = 0; vertexIndex < vertices.currentVertCount; vertexIndex++)
                    {
                        vertices.PopulateUIVertex(ref vertex, vertexIndex);
                        horizontal.Add(vertex.position.x);
                        vertical.Add(vertex.position.y);
                    }
                }
                Assert.AreEqual(4, horizontal.Count);
                Assert.AreEqual(4, vertical.Count);
                var columns = new float[horizontal.Count];
                var rows = new float[vertical.Count];
                horizontal.CopyTo(columns);
                vertical.CopyTo(rows);
                Assert.AreEqual(border, columns[1] - columns[0], 0.01f);
                Assert.AreEqual(border, rows[1] - rows[0], 0.01f);
                Assert.Greater(columns[2] - columns[1], 0f);
                Assert.Greater(rows[2] - rows[1], 0f);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InputAndScrollBackgroundsAreSlicedThroughSkinTree()
        {
            var root = new GameObject("NineSliceSkinTest", typeof(RectTransform));
            try
            {
                var input = new GameObject("Input", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
                input.transform.SetParent(root.transform, false);
                var scroll = new GameObject("Scroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
                scroll.transform.SetParent(root.transform, false);
                var sprite = CatTheme.LoadCozy("panels/paper.png", CatTheme.PaperBorder);
                Assert.NotNull(sprite);
                var backgrounds = new[] { input.GetComponent<Image>(), scroll.GetComponent<Image>() };
                foreach (var image in backgrounds)
                {
                    image.sprite = sprite;
                    image.type = Image.Type.Simple;
                    image.rectTransform.sizeDelta = new Vector2(300f, 48f);
                }
                CatTheme.SkinTree(root.transform);
                foreach (var image in backgrounds)
                {
                    Assert.AreSame(sprite, image.sprite);
                    Assert.AreEqual(Image.Type.Sliced, image.type);
                    Assert.AreEqual(new Vector2(300f, 48f), image.rectTransform.sizeDelta);
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NineSliceNormalizationPreservesFilledProgressAndPlainIcons()
        {
            var root = new GameObject("NineSliceExceptions", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                var image = root.GetComponent<Image>();
                image.sprite = CatTheme.LoadCozy("buttons/plate-default.png", CatTheme.PlateBorder);
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillAmount = 0.35f;
                CatTheme.ApplySlicedImage(image);
                Assert.AreEqual(Image.Type.Filled, image.type);
                Assert.AreEqual(0.35f, image.fillAmount);
                image.sprite = CatTheme.LoadCozy("icons/bag.png", Vector4.zero);
                Assert.NotNull(image.sprite);
                Assert.AreEqual(Vector4.zero, image.sprite.border);
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                CatTheme.ApplySlicedImage(image);
                Assert.AreEqual(Image.Type.Simple, image.type);
                Assert.IsTrue(image.preserveAspect);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
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
