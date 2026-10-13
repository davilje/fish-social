#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using FishSocial.Desktop;
using FishSocial.Desktop.Editor;
using FishSocial.Desktop.Pet;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyPrefabPersistenceTests
    {
        GameObject root;
        GameObject events;
        string fixtureFolder;
        bool legacyFallback;

        [Test]
        public void AssetMetadataHasValidUniqueGuids()
        {
            var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in Directory.GetFiles(Application.dataPath, "*.meta", SearchOption.AllDirectories))
            {
                var match = Regex.Match(File.ReadAllText(path), @"^guid: ([0-9a-fA-F]{32})\r?$", RegexOptions.Multiline);
                Assert.IsTrue(match.Success, "Asset metadata must contain exactly 32 hexadecimal GUID characters: " + path);
                var guid = match.Groups[1].Value;
                Assert.AreNotEqual(new string('0', 32), guid, "Asset GUID must not be empty: " + path);
                Assert.IsTrue(guids.Add(guid), "Duplicate asset GUID: " + path);
            }
        }

        [Test]
        public void RepairScriptIsImportedWithItsMetadataGuid()
        {
            const string path = "Assets/Scripts/Desktop/Editor/CozyPrefabRepair.cs";
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            Assert.NotNull(script, "CozyPrefabRepair must be imported; check its .meta GUID.");
            var match = Regex.Match(File.ReadAllText(path + ".meta"), @"^guid: ([0-9a-fA-F]{32})\r?$", RegexOptions.Multiline);
            Assert.IsTrue(match.Success);
            Assert.AreEqual(match.Groups[1].Value, AssetDatabase.AssetPathToGUID(path));
        }

        [Test]
        public void LegacyBakerDoesNotRebuildPrefabsOnDomainInitialization()
        {
            var methods = typeof(DesktopPrefabValidator).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var method in methods)
                Assert.IsFalse(Attribute.IsDefined(method, typeof(InitializeOnLoadMethodAttribute)),
                    "Prefab inspection and rebuilding must be explicitly invoked, not run during import: " + method.Name);
        }

        [SetUp]
        public void SetUp()
        {
            fixtureFolder = null;
            legacyFallback = CatTheme.AllowLegacyStreamingAssetsFallback;
            root = new GameObject("PersistenceFixture", typeof(RectTransform));
            events = new GameObject("PersistenceEvents", typeof(EventSystem));
            CatTheme.AllowLegacyStreamingAssetsFallback = false;
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(events);
            if (!string.IsNullOrEmpty(fixtureFolder))
                AssetDatabase.DeleteAsset(fixtureFolder);
            CatTheme.AllowLegacyStreamingAssetsFallback = legacyFallback;
        }

        [TestCase("FeatureModalRoot")]
        [TestCase("ProductContextMenu")]
        [TestCase("PopupProductMenu")]
        [TestCase("ModalPanel")]
        [TestCase("Panel")]
        [TestCase("Dim")]
        [TestCase("MenuPanel")]
        public void ModalClickBlockersAreNotSkinnedOrRequiredToHaveButtonPlates(string name)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            node.transform.SetParent(root.transform, false);
            var image = node.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.42f);
            image.raycastTarget = true;
            image.rectTransform.anchoredPosition = new Vector2(19f, 23f);
            image.rectTransform.sizeDelta = new Vector2(331f, 217f);
            var position = image.rectTransform.anchoredPosition;
            var size = image.rectTransform.sizeDelta;
            var color = image.color;
            var button = node.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var clicks = 0;
            button.onClick.AddListener(() => clicks++);
            var components = node.GetComponents<Component>().Length;
            CozyPrefabRepair.RepairContents(root);
            CozyPrefabRepair.RepairContents(root);
            Assert.IsNull(node.GetComponent<CozyButtonVisual>());
            Assert.IsNull(node.GetComponent<CozyPanelSkin>());
            Assert.IsNull(node.transform.Find("CozyVisual"));
            Assert.AreEqual(0, node.transform.childCount);
            Assert.AreEqual(components, node.GetComponents<Component>().Length);
            Assert.IsNull(image.sprite);
            Assert.AreEqual(Image.Type.Simple, image.type);
            Assert.AreEqual(color, image.color);
            Assert.IsTrue(image.raycastTarget);
            Assert.AreEqual(position, image.rectTransform.anchoredPosition);
            Assert.AreEqual(size, image.rectTransform.sizeDelta);
            Assert.AreSame(image, button.targetGraphic);
            Assert.AreEqual(Selectable.Transition.ColorTint, button.transition);
            Assert.IsEmpty(CozyPrefabRepair.ValidateContents(root, root.name));
            button.onClick.Invoke();
            Assert.AreEqual(1, clicks);
            DesktopModalUi.StyleTree(root.transform);
            CatTheme.SkinTree(root.transform);
            Assert.IsNull(node.GetComponent<CozyButtonVisual>());
            Assert.IsNull(node.transform.Find("CozyVisual"));
            Assert.AreEqual(0, node.transform.childCount);
            Assert.IsNull(image.sprite);
            Assert.AreEqual(color, image.color);
            Assert.IsTrue(image.raycastTarget);
            Assert.AreEqual(position, image.rectTransform.anchoredPosition);
            Assert.AreEqual(size, image.rectTransform.sizeDelta);
            button.onClick.Invoke();
            Assert.AreEqual(2, clicks);
        }

        [TestCase("PanelWorldMap", 0.07f, 0.1f, 0.14f)]
        [TestCase("Shell", 0.09f, 0.12f, 0.16f)]
        [TestCase("Tab_资料", 0.1f, 0.13f, 0.17f)]
        [TestCase("AlbumStage", 0.06f, 0.08f, 0.1f)]
        public void KnownLegacyChromeIsPaperWithoutMovingAuthoredGeometry(string name, float red, float green, float blue)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image));
            node.transform.SetParent(root.transform, false);
            var image = node.GetComponent<Image>();
            image.color = new Color(red, green, blue, 0.91f);
            image.rectTransform.anchoredPosition = new Vector2(13f, 29f);
            image.rectTransform.sizeDelta = new Vector2(321f, 123f);
            image.pixelsPerUnitMultiplier = 1.4f;
            CozyPrefabRepair.RepairContents(root);
            Assert.AreSame(Imported("panels/paper.png"), image.sprite);
            Assert.AreEqual(Image.Type.Sliced, image.type);
            Assert.AreEqual(new Color(1f, 1f, 1f, 0.91f), image.color);
            Assert.AreEqual(new Vector2(13f, 29f), image.rectTransform.anchoredPosition);
            Assert.AreEqual(new Vector2(321f, 123f), image.rectTransform.sizeDelta);
            Assert.AreEqual(1.4f, image.pixelsPerUnitMultiplier);
        }

        [Test]
        public void ToggleBackgroundAndEmptyPhotoAreSafeLegacySurfaceExceptions()
        {
            var toggleNode = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
            toggleNode.transform.SetParent(root.transform, false);
            var backgroundNode = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            backgroundNode.transform.SetParent(toggleNode.transform, false);
            var background = backgroundNode.GetComponent<Image>();
            background.color = new Color(0.25f, 0.3f, 0.35f, 0.93f);
            var toggle = toggleNode.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            var photoNode = new GameObject("Photo", typeof(RectTransform), typeof(Image));
            photoNode.transform.SetParent(root.transform, false);
            var photo = photoNode.GetComponent<Image>();
            photo.color = new Color(0.08f, 0.11f, 0.14f, 0.95f);
            CatTheme.SkinTree(root.transform);
            Assert.AreSame(Imported("panels/paper.png"), background.sprite);
            Assert.AreEqual(Image.Type.Sliced, background.type);
            Assert.AreSame(background, toggle.targetGraphic);
            Assert.AreEqual(0.93f, background.color.a);
            Assert.IsNull(photo.sprite);
            Assert.AreEqual(new Color(CatTheme.Cream.r, CatTheme.Cream.g, CatTheme.Cream.b, 0.95f), photo.color);
        }

        [TestCase("MapImage", false, false)]
        [TestCase("Avatar", false, false)]
        [TestCase("Fish", false, false)]
        [TestCase("Dim", false, false)]
        [TestCase("Photo", true, false)]
        [TestCase("Shell", false, true)]
        public void SafeSurfaceRepairDoesNotReplaceContentOrFilledImages(string name, bool imported, bool filled)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image));
            node.transform.SetParent(root.transform, false);
            var image = node.GetComponent<Image>();
            image.color = new Color(0.08f, 0.11f, 0.14f, 1f);
            if (imported)
                image.sprite = Imported("scenes/home-sakura.png");
            if (filled)
                image.type = Image.Type.Filled;
            var sprite = image.sprite;
            var color = image.color;
            Assert.IsFalse(CatTheme.ApplyLegacySurface(image));
            CozyPrefabRepair.RepairContents(root);
            Assert.AreSame(sprite, image.sprite);
            Assert.AreEqual(color, image.color);
        }

        [Test]
        public void CatalogMissingAuthoredPrefabNeverInvokesItsLegacyBuilder()
        {
            var rebuilt = false;
            var name = "__MissingCozyPrefab_" + Guid.NewGuid().ToString("N");
            var entry = new DesktopPrefabCatalog.Entry(name, "Missing authored fixture", null, () => rebuilt = true);
            Assert.Throws<InvalidOperationException>(() => entry.Ensure());
            Assert.IsFalse(rebuilt);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.Folder + "/" + name + ".prefab"));
            var overlay = new DesktopPrefabCatalog.Entry("OverlayFixture", "Overlay fixture", null, () => rebuilt = true);
            overlay.Ensure();
            Assert.IsTrue(rebuilt);
        }

        [TestCase("buttons/plate-default.png", "Desktop/Theme/Cozy/buttons/plate-default")]
        [TestCase("buttons\\plate-hover.PNG", "Desktop/Theme/Cozy/buttons/plate-hover")]
        [TestCase("panels/paper", "Desktop/Theme/Cozy/panels/paper")]
        public void ResourcePathRemovesPngAndNormalizesSeparators(string path, string expected)
        {
            Assert.AreEqual(expected, CatTheme.CozyResourcePath(path));
        }

        [TestCase("../paper.png")]
        [TestCase("/paper.png")]
        [TestCase("icons/../paper.png")]
        [TestCase("icons//bag.png")]
        [TestCase("icons/./bag.png")]
        [TestCase("file:paper.png")]
        [TestCase("paper.jpg")]
        public void InvalidResourcePathsAreRejected(string path)
        {
            Assert.IsNull(CatTheme.CozyResourcePath(path));
            Assert.IsNull(CatTheme.LoadCozy(path, Vector4.zero));
        }

        [Test]
        public void NormalLoadReturnsTheImportedResourceSpriteNotATransientClone()
        {
            foreach (var state in new[] { "default", "hover", "pressed" })
            {
                var path = "buttons/plate-" + state + ".png";
                var imported = Imported(path);
                Assert.AreSame(imported, CatTheme.LoadCozy(path, CatTheme.PlateBorder));
                Assert.AreSame(imported, CatTheme.LoadCozy(path, Vector4.zero));
                Assert.AreNotEqual(Vector4.zero, imported.border);
            }
            Assert.IsNull(CatTheme.LoadCozy("not-packaged/persistence-fixture.png", Vector4.zero));
        }

        [Test]
        public void PaperUsesItsImportedBorderAndSlicedImageType()
        {
            var image = root.AddComponent<Image>();
            CatTheme.ApplyPaper(image);
            Assert.AreSame(Imported("panels/paper.png"), image.sprite);
            Assert.AreNotEqual(Vector4.zero, image.sprite.border);
            Assert.AreEqual(Image.Type.Sliced, image.type);
        }

        [Test]
        public void RepairPreservesAnExistingImportedNonzeroBorder()
        {
            foreach (var relative in new[] { "buttons/plate-default.png", "panels/paper.png" })
            {
                var path = CozyPrefabRepair.ThemeFolder + "/" + relative;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.NotNull(importer);
                Assert.NotNull(texture);
                Assert.AreNotEqual(Vector4.zero, importer.spriteBorder);
                Assert.AreEqual(importer.spriteBorder, CozyPrefabRepair.ExpectedBorder(path, texture.width, texture.height));
            }
        }

        [Test]
        public void EffectiveImageOverrideIsBakedIntoTheSerializedSprite()
        {
            var image = root.AddComponent<Image>();
            image.sprite = Imported("icons/bag.png");
            image.overrideSprite = Imported("icons/home.png");
            CozyPrefabRepair.RepairContents(root);
            Assert.AreSame(Imported("icons/home.png"), image.sprite);
        }

        [Test]
        public void ExistingPrefabNodesAndEditedLayoutArePreservedDuringRuntimeBinding()
        {
            var button = FixtureButton();
            var plate = button.transform.Find("CozyVisual").GetComponent<Image>();
            var icon = button.transform.Find("Icon").GetComponent<Image>();
            var label = button.transform.Find("Label").GetComponent<Text>();
            var platePosition = plate.rectTransform.anchoredPosition;
            var iconPosition = icon.rectTransform.anchoredPosition;
            var iconSize = icon.rectTransform.sizeDelta;
            plate.pixelsPerUnitMultiplier = 1.4f;
            var labelPosition = label.rectTransform.anchoredPosition;
            var rootPosition = button.GetComponent<RectTransform>().anchoredPosition;
            var clicks = 0;
            button.onClick.AddListener(() => clicks++);

            CatTheme.StyleButton(button, "bag");
            CatTheme.StyleButton(button, "bag");
            var visual = button.GetComponent<CozyButtonVisual>();
            visual.SetStackedIcon(true);
            Assert.AreEqual(3, button.transform.childCount);
            Assert.AreSame(plate, button.transform.Find("CozyVisual").GetComponent<Image>());
            Assert.AreSame(icon, button.transform.Find("Icon").GetComponent<Image>());
            Assert.AreEqual(iconPosition, icon.rectTransform.anchoredPosition);
            Assert.AreEqual(iconSize, icon.rectTransform.sizeDelta);
            Assert.AreEqual(1.4f, plate.pixelsPerUnitMultiplier);
            Assert.AreEqual(labelPosition, label.rectTransform.anchoredPosition);

            var pointer = new PointerEventData(events.GetComponent<EventSystem>())
            {
                button = PointerEventData.InputButton.Left
            };
            visual.OnPointerEnter(pointer);
            Assert.AreSame(Imported("buttons/plate-hover.png"), plate.sprite);
            visual.OnPointerDown(pointer);
            Assert.AreSame(Imported("buttons/plate-pressed.png"), plate.sprite);
            Assert.AreEqual(platePosition + new Vector2(0f, -3f), plate.rectTransform.anchoredPosition);
            Assert.AreEqual(rootPosition, button.GetComponent<RectTransform>().anchoredPosition);
            visual.OnPointerUp(pointer);
            Assert.AreEqual(platePosition, plate.rectTransform.anchoredPosition);
            Assert.IsEmpty(plate.GetComponents<Shadow>());
            button.onClick.Invoke();
            Assert.AreEqual(1, clicks);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepairSaveAndReloadPersistsAllButtonStatesWithoutRebuildingLayout(bool flat)
        {
            var button = FixtureButton();
            button.gameObject.AddComponent<CozyButtonVisual>().SetFlat(flat);
            var plate = button.transform.Find("CozyVisual").GetComponent<Image>();
            plate.pixelsPerUnitMultiplier = 1.4f;
            var position = plate.rectTransform.anchoredPosition;
            var anchors = plate.rectTransform.anchorMin;
            var iconPosition = button.transform.Find("Icon").GetComponent<RectTransform>().anchoredPosition;
            CozyPrefabRepair.RepairContents(root);
            CozyPrefabRepair.RepairContents(root);
            CozyPrefabRepair.RequireValid(root, root.name);

            Assert.AreEqual(3, button.transform.childCount);
            Assert.AreEqual(position, plate.rectTransform.anchoredPosition);
            Assert.AreEqual(anchors, plate.rectTransform.anchorMin);
            var path = FixturePath();
            bool success;
            PrefabUtility.SaveAsPrefabAsset(root, path, out success);
            Assert.IsTrue(success);
            CozyPrefabRepair.Repair(path);
            var reloaded = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var savedButton = reloaded.transform.Find("Action").GetComponent<Button>();
                var savedPlate = savedButton.transform.Find("CozyVisual").GetComponent<Image>();
                var visual = new SerializedObject(savedButton.GetComponent<CozyButtonVisual>());
                Assert.AreEqual(flat, visual.FindProperty("flat").boolValue);
                Assert.AreEqual(flat ? 0f : 1f, savedPlate.color.a);
                Assert.AreSame(savedPlate, visual.FindProperty("plate").objectReferenceValue);
                Assert.AreSame(Imported("buttons/plate-default.png"), visual.FindProperty("normalSprite").objectReferenceValue);
                Assert.AreSame(Imported("buttons/plate-hover.png"), visual.FindProperty("hoverSprite").objectReferenceValue);
                Assert.AreSame(Imported("buttons/plate-pressed.png"), visual.FindProperty("pressedSprite").objectReferenceValue);
                Assert.AreSame(Imported("buttons/plate-default.png"), savedPlate.sprite);
                Assert.AreEqual(Image.Type.Sliced, savedPlate.type);
                Assert.AreEqual(1.4f, savedPlate.pixelsPerUnitMultiplier);
                Assert.AreEqual(position, savedPlate.rectTransform.anchoredPosition);
                Assert.AreEqual(anchors, savedPlate.rectTransform.anchorMin);
                Assert.AreEqual(iconPosition, savedButton.transform.Find("Icon").GetComponent<RectTransform>().anchoredPosition);
                Assert.AreEqual(Selectable.Transition.None, savedButton.transition);
                Assert.AreSame(Imported("buttons/plate-hover.png"), savedButton.spriteState.highlightedSprite);
                Assert.AreSame(Imported("buttons/plate-pressed.png"), savedButton.spriteState.pressedSprite);
                Assert.IsEmpty(CozyPrefabRepair.ValidateContents(reloaded, "PersistenceFixture"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(reloaded);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SynchronousSkinTreeConfiguresAuthoredButtonsAndPreservesLabelColorsAndGeometry(bool inactive)
        {
            var previousFont = CatTheme.Font;
            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            Assert.NotNull(font);
            try
            {
                CatTheme.Font = font;
                var button = FixtureButton();
                button.gameObject.SetActive(!inactive);
                var visual = button.gameObject.AddComponent<CozyButtonVisual>();
                var plate = button.transform.Find("CozyVisual").GetComponent<Image>();
                var icon = button.transform.Find("Icon").GetComponent<Image>();
                plate.color = CatTheme.Coral;
                var plateSprite = plate.sprite;
                var iconSprite = icon.sprite;
                var label = button.transform.Find("Label").GetComponent<Text>();
                label.text = "Primary";
                label.color = new Color(1f, 1f, 1f, 0.83f);
                var labels = new[] { label, FixtureLabel(button.transform, "Brown", "Brown", CatTheme.Ink),
                    FixtureLabel(button.transform, "Level", "Level 5", new Color(1f, 1f, 1f, 0.7f)),
                    FixtureLabel(button.transform, "XpLabel", "XP 120/360", new Color(1f, 1f, 1f, 0.35f)) };
                var colors = new Color[labels.Length];
                var positions = new Vector3[labels.Length];
                var sizes = new Vector2[labels.Length];
                var minimums = new Vector2[labels.Length];
                var maximums = new Vector2[labels.Length];
                var pivots = new Vector2[labels.Length];
                for (var index = 0; index < labels.Length; index++)
                {
                    var text = labels[index];
                    text.font = null;
                    colors[index] = text.color;
                    positions[index] = text.rectTransform.anchoredPosition3D;
                    sizes[index] = text.rectTransform.sizeDelta;
                    minimums[index] = text.rectTransform.anchorMin;
                    maximums[index] = text.rectTransform.anchorMax;
                    pivots[index] = text.rectTransform.pivot;
                }
                CatTheme.SkinTree(root.transform);
                CatTheme.SkinTree(root.transform);
                Assert.IsTrue((bool)typeof(CozyButtonVisual).GetField("initialized",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(visual));
                var skinText = typeof(CatTheme).GetMethod("SkinText", BindingFlags.Static | BindingFlags.NonPublic);
                for (var index = 0; index < labels.Length; index++)
                {
                    var text = labels[index];
                    Assert.NotNull(text.GetComponent<CozyGraphicOffset>());
                    Assert.AreSame(font, text.font);
                    text.font = null;
                    skinText.Invoke(null, new object[] { text });
                    Assert.AreSame(font, text.font);
                    Assert.AreEqual(colors[index], text.color);
                    Assert.AreEqual(positions[index], text.rectTransform.anchoredPosition3D);
                    Assert.AreEqual(sizes[index], text.rectTransform.sizeDelta);
                    Assert.AreEqual(minimums[index], text.rectTransform.anchorMin);
                    Assert.AreEqual(maximums[index], text.rectTransform.anchorMax);
                    Assert.AreEqual(pivots[index], text.rectTransform.pivot);
                }
                Assert.AreSame(plateSprite, plate.sprite);
                Assert.AreSame(iconSprite, icon.sprite);
            }
            finally
            {
                CatTheme.Font = previousFont;
            }
        }

        [Test]
        public void AvailableCjkThemeFontIsAppliedToAuthoredWhiteButtonLabels()
        {
            var font = CatTheme.Font;
            if (font == null)
                Assert.Ignore("A CJK resource or OS font is required for this font integration test.");
            var button = FixtureButton();
            button.gameObject.AddComponent<CozyButtonVisual>();
            var label = button.transform.Find("Label").GetComponent<Text>();
            label.text = "中文等级";
            label.color = new Color(1f, 1f, 1f, 0.63f);
            var color = label.color;
            CatTheme.SkinTree(root.transform);
            Assert.AreSame(font, label.font);
            Assert.AreEqual(color, label.color);
        }

        [Test]
        public void ConfigureRetainsAnAuthoredIconInsteadOfReplacingItWithTheRequestedDefault()
        {
            var button = FixtureButton();
            var icon = button.transform.Find("Icon").GetComponent<Image>();
            var sprite = icon.sprite;
            var color = new Color(0.4f, 0.5f, 0.6f, 0.37f);
            icon.color = color;
            var position = icon.rectTransform.anchoredPosition;
            var size = icon.rectTransform.sizeDelta;
            CatTheme.StyleButton(button, "home");
            CatTheme.StyleButton(button, "home");
            Assert.AreSame(sprite, icon.sprite);
            Assert.AreEqual(color, icon.color);
            Assert.AreEqual(position, icon.rectTransform.anchoredPosition);
            Assert.AreEqual(size, icon.rectTransform.sizeDelta);
        }

        [Test]
        public void ConfigureDoesNotFillOrEnableAnAuthoredEmptyIconWithADefaultName()
        {
            var button = FixtureButton();
            var icon = button.transform.Find("Icon").GetComponent<Image>();
            icon.sprite = null;
            icon.enabled = false;
            CatTheme.StyleButton(button, "home");
            CatTheme.SkinTree(root.transform);
            Assert.IsNull(icon.sprite);
            Assert.IsFalse(icon.enabled);
        }

        [Test]
        public void AuthoredHomeBackgroundRemainsAnIllustrationThroughSkinTree()
        {
            var image = FixtureImage(root.transform, "Background", "scenes/home-sakura.png");
            var sprite = image.sprite;
            var color = new Color(1f, 1f, 1f, 0.91f);
            image.color = color;
            CatTheme.SkinTree(root.transform);
            Assert.AreSame(sprite, image.sprite);
            Assert.AreEqual(Image.Type.Simple, image.type);
            Assert.AreEqual(color, image.color);
        }

        [Test]
        public void FlatDockShowsOnlyHoverSelectedAndPressedWithoutMovingHitTarget()
        {
            var button = FixtureButton();
            CatTheme.StyleButton(button);
            var plate = button.transform.Find("CozyVisual").GetComponent<Image>();
            var visual = button.GetComponent<CozyButtonVisual>();
            var position = plate.rectTransform.anchoredPosition;
            var rootPosition = button.GetComponent<RectTransform>().anchoredPosition;
            var target = button.targetGraphic;
            var plateRaycast = plate.raycastTarget;
            var targetRaycast = target.raycastTarget;
            var pointer = new PointerEventData(events.GetComponent<EventSystem>())
            {
                button = PointerEventData.InputButton.Left
            };
            visual.SetFlat(true);
            Assert.AreEqual(0f, plate.color.a);
            visual.OnSelect(new BaseEventData(events.GetComponent<EventSystem>()));
            Assert.AreEqual(0f, plate.color.a);
            visual.OnPointerEnter(pointer);
            Assert.Greater(plate.color.a, 0f);
            Assert.AreSame(Imported("buttons/plate-hover.png"), plate.sprite);
            visual.OnPointerDown(pointer);
            Assert.AreSame(Imported("buttons/plate-pressed.png"), plate.sprite);
            Assert.AreEqual(position + new Vector2(0f, -3f), plate.rectTransform.anchoredPosition);
            visual.OnPointerUp(pointer);
            visual.OnPointerExit(pointer);
            Assert.AreEqual(0f, plate.color.a);
            visual.SetSelected(true);
            Assert.Greater(plate.color.a, 0f);
            Assert.AreSame(Imported("buttons/plate-hover.png"), plate.sprite);
            visual.SetSelected(false);
            Assert.AreEqual(0f, plate.color.a);
            visual.SetFlat(false);
            Assert.Greater(plate.color.a, 0f);
            Assert.AreEqual(position, plate.rectTransform.anchoredPosition);
            Assert.AreEqual(rootPosition, button.GetComponent<RectTransform>().anchoredPosition);
            Assert.AreSame(target, button.targetGraphic);
            Assert.AreEqual(plateRaycast, plate.raycastTarget);
            Assert.AreEqual(targetRaycast, target.raycastTarget);
        }

        [Test]
        public void DisabledEmptyPrefabIconRemainsDisabledThroughConfigureRepairAndReload()
        {
            var button = FixtureButton();
            var icon = button.transform.Find("Icon").GetComponent<Image>();
            icon.sprite = null;
            icon.enabled = false;
            var position = icon.rectTransform.anchoredPosition;
            CatTheme.StyleButton(button);
            CatTheme.StyleButton(button);
            CozyPrefabRepair.RepairContents(root);
            Assert.IsFalse(icon.enabled);
            Assert.IsNull(icon.sprite);
            Assert.AreEqual(position, icon.rectTransform.anchoredPosition);
            var path = FixturePath();
            bool success;
            PrefabUtility.SaveAsPrefabAsset(root, path, out success);
            Assert.IsTrue(success);
            var reloaded = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var savedIcon = reloaded.transform.Find("Action/Icon").GetComponent<Image>();
                Assert.IsFalse(savedIcon.enabled);
                Assert.IsNull(savedIcon.sprite);
                Assert.AreEqual(position, savedIcon.rectTransform.anchoredPosition);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(reloaded);
            }
        }

        [Test]
        public void SerializedShellUsesSevenFlatDockButtonsAndAllFifteenRouteSlots()
        {
            var prefab = Resources.Load<GameObject>("Desktop/Prefabs/DesktopShell");
            Assert.NotNull(prefab);
            var toast = prefab.transform.Find("Toast/Label");
            Assert.NotNull(toast);
            Assert.NotNull(toast.GetComponent<Text>());
            var nav = prefab.transform.Find("MainRoot/Nav");
            var content = prefab.transform.Find("MainRoot/Content");
            Assert.NotNull(nav);
            Assert.NotNull(content);
            var routes = new[] { "CatchBag", "Equipment", "Shop", "Achievements", "Leaderboard", "Profile", "SocialFeed" };
            Assert.AreEqual(routes.Length, nav.GetComponentsInChildren<Button>(true).Length);
            foreach (var route in routes)
            {
                var node = nav.Find(route);
                Assert.NotNull(node, route);
                Assert.NotNull(node.GetComponent<Button>(), route);
                Assert.NotNull(node.Find("CozyVisual").GetComponent<Image>(), route);
                Assert.NotNull(node.Find("Icon").GetComponent<Image>(), route);
                Assert.NotNull(node.Find("Label").GetComponent<Text>(), route);
                var visual = node.GetComponent<CozyButtonVisual>();
                Assert.NotNull(visual, route);
                Assert.IsTrue(new SerializedObject(visual).FindProperty("flat").boolValue, route);
            }
            var values = Enum.GetValues(typeof(ShellPanelId));
            Assert.AreEqual(15, values.Length);
            foreach (ShellPanelId route in values)
            {
                var slot = content.Find(route.ToString());
                Assert.NotNull(slot, route.ToString());
                Assert.NotNull(slot.GetComponent<RectTransform>(), route.ToString());
            }
        }

        [Test]
        public void SerializedHomeMatchesHeroAndMenuCardContract()
        {
            var prefab = Resources.Load<GameObject>("Desktop/Prefabs/PanelHome");
            Assert.NotNull(prefab);
            AssertAuthoredHero(prefab);
            var logo = prefab.transform.Find("MenuCard/Logo");
            Assert.NotNull(logo);
            Assert.NotNull(logo.GetComponent<Image>());
            Assert.NotNull(logo.GetComponent<Image>().sprite);
            foreach (var name in new[] { "Tagline", "SessionState", "CurrentPond" })
            {
                var node = prefab.transform.Find("MenuCard/" + name);
                Assert.NotNull(node, name);
                Assert.NotNull(node.GetComponent<Text>(), name);
            }
            foreach (var name in new[] { "StartFishing", "ExploreWorld", "DailyGift", "SelectPond" })
            {
                var node = prefab.transform.Find("MenuCard/" + name);
                Assert.NotNull(node, name);
                Assert.NotNull(node.GetComponent<Button>(), name);
            }
            Assert.IsEmpty(CozyPrefabRepair.ValidateContents(prefab, "PanelHome"));
        }

        [Test]
        public void SerializedPondMatchesHeroAndFishingControlsContract()
        {
            var prefab = Resources.Load<GameObject>("Desktop/Prefabs/PanelPond");
            Assert.NotNull(prefab);
            AssertAuthoredHero(prefab);
            foreach (var name in new[] { "Title", "PondSession" })
            {
                var node = prefab.transform.Find("FishingControls/" + name);
                Assert.NotNull(node, name);
                Assert.NotNull(node.GetComponent<Text>(), name);
            }
            foreach (var name in new[] { "EnterOverlay", "SelectPond", "ConnectPond", "TakeSpot",
                "StartFishing", "StopFishing", "CatchBag" })
            {
                var node = prefab.transform.Find("FishingControls/" + name);
                Assert.NotNull(node, name);
                Assert.NotNull(node.GetComponent<Button>(), name);
            }
            Assert.IsEmpty(CozyPrefabRepair.ValidateContents(prefab, "PanelPond"));
        }

        static void AssertAuthoredHero(GameObject prefab)
        {
            var hero = prefab.transform.Find("HeroCat");
            Assert.NotNull(hero);
            Assert.NotNull(hero.GetComponent<Image>());
            Assert.NotNull(hero.GetComponent<SpriteFramePetRenderer>());
        }

        [Test]
        public void UnresolvableTransientSpritesAreRejectedBeforeSaving()
        {
            var texture = new Texture2D(8, 8);
            var transient = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f));
            transient.name = "not-packaged/persistence-transient-fixture";
            var image = root.AddComponent<Image>();
            image.sprite = transient;
            try
            {
                Assert.Throws<InvalidOperationException>(() => CozyPrefabRepair.RepairContents(root));
            }
            finally
            {
                image.sprite = null;
                UnityEngine.Object.DestroyImmediate(transient);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void MissingFeaturePrefabDoesNotCreateAnEntireFallbackPage()
        {
            var factory = typeof(CatTheme).Assembly.GetType("FishSocial.Desktop.DesktopFeaturePanelFactory", true);
            var mount = factory.GetMethod("Mount", BindingFlags.Public | BindingFlags.Static)
                .MakeGenericMethod(typeof(DesktopShopPanel));
            LogAssert.Expect(LogType.Error,
                "[DesktopUI] Required prefab is missing: Desktop/Prefabs/__CozyPersistenceMissingPanel");
            var result = mount.Invoke(null, new object[] { root.transform, null, "__CozyPersistenceMissingPanel" });
            Assert.IsNull(result);
            Assert.AreEqual(0, root.transform.childCount);
        }

        [Test]
        public void FeaturePrefabWithoutRequiredComponentIsNotInstantiated()
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(root.transform, false);
            var path = FixturePath();
            bool success;
            PrefabUtility.SaveAsPrefabAsset(root, path, out success);
            Assert.IsTrue(success);
            var name = path.Substring((DesktopPrefabCatalog.Folder + "/").Length);
            name = name.Substring(0, name.Length - ".prefab".Length);
            var factory = typeof(CatTheme).Assembly.GetType("FishSocial.Desktop.DesktopFeaturePanelFactory", true);
            var mount = factory.GetMethod("Mount", BindingFlags.Public | BindingFlags.Static)
                .MakeGenericMethod(typeof(DesktopShopPanel));
            LogAssert.Expect(LogType.Error,
                "[DesktopUI] Required component DesktopShopPanel is missing from prefab: " + name);
            var result = mount.Invoke(null, new object[] { root.transform, null, name });
            Assert.IsNull(result);
            Assert.AreEqual(1, root.transform.childCount);
        }

        [Test]
        public void NewPageCatalogEntriesExistAndEmptyShellsFailValidation()
        {
            foreach (var name in new[] { "DesktopShell", "PanelHome", "PanelPond", "PanelPondSelection", "PanelEquipment",
                "PanelAchievements", "FeatureModalRoot", "ProductContextMenu", "PanelPondSettlement",
                "PondSelectionCard", "WorldMapPondMarker" })
            {
                Assert.NotNull(DesktopPrefabCatalog.Find(name), name);
                Assert.IsNotEmpty(CozyPrefabRepair.ValidateContents(root, name));
            }
        }

        Button FixtureButton()
        {
            var node = new GameObject("Action", typeof(RectTransform), typeof(Image), typeof(Button));
            node.transform.SetParent(root.transform, false);
            var button = node.GetComponent<Button>();
            button.targetGraphic = node.GetComponent<Image>();
            node.GetComponent<RectTransform>().anchoredPosition = new Vector2(37f, 82f);
            var plate = FixtureImage(node.transform, "CozyVisual", "buttons/plate-default.png");
            plate.type = Image.Type.Sliced;
            plate.rectTransform.anchorMin = new Vector2(0.1f, 0.2f);
            plate.rectTransform.anchorMax = new Vector2(0.9f, 0.8f);
            plate.rectTransform.anchoredPosition = new Vector2(9f, 11f);
            var icon = FixtureImage(node.transform, "Icon", "icons/bag.png");
            icon.rectTransform.anchoredPosition = new Vector2(17f, 23f);
            icon.rectTransform.sizeDelta = new Vector2(41f, 43f);
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(node.transform, false);
            label.GetComponent<RectTransform>().anchoredPosition = new Vector2(13f, 19f);
            label.GetComponent<Text>().text = "Edited layout";
            return button;
        }

        static Image FixtureImage(Transform parent, string name, string path)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Image));
            node.transform.SetParent(parent, false);
            var image = node.GetComponent<Image>();
            image.sprite = Imported(path);
            return image;
        }

        Text FixtureLabel(Transform parent, string name, string text, Color color)
        {
            var node = new GameObject(name, typeof(RectTransform), typeof(Text));
            node.transform.SetParent(parent, false);
            var label = node.GetComponent<Text>();
            label.text = text;
            label.color = color;
            label.rectTransform.anchoredPosition3D = new Vector3(13f, 17f, 2f);
            label.rectTransform.sizeDelta = new Vector2(93f, 27f);
            return label;
        }

        static Sprite Imported(string relativePath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CozyPrefabRepair.ThemeFolder + "/" + relativePath);
            Assert.NotNull(sprite, "Install the complete imported Cozy theme before running persistence tests: " + relativePath);
            Assert.IsTrue(EditorUtility.IsPersistent(sprite));
            return sprite;
        }

        string FixturePath()
        {
            var name = "__CozyPersistenceTests_" + Guid.NewGuid().ToString("N");
            Assert.IsTrue(AssetDatabase.IsValidFolder(DesktopPrefabCatalog.Folder));
            var guid = AssetDatabase.CreateFolder(DesktopPrefabCatalog.Folder, name);
            Assert.IsNotEmpty(guid);
            fixtureFolder = AssetDatabase.GUIDToAssetPath(guid);
            return fixtureFolder + "/PersistenceFixture.prefab";
        }
    }
}
#endif
