#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    [InitializeOnLoad]
    public static class CozyRedesignAuthoring
    {
        const string Folder = "Assets/Resources/Desktop/Prefabs/";
        static readonly string[] Pages = { "DesktopShell", "PanelHome", "PanelLeaderboard", "PanelProfileHub", "PanelAchievements", "PanelSocial", "PanelCatch", "PanelEquipment", "PanelShop", "PanelSocialFeed", "PanelSettings", "PanelPondSelection", "PanelWorldMap", "PanelGallery", "CatchSlot", "ShopItemCard", "LeaderboardRow", "AchievementRow", "SocialPostCard" };
        static double nextCheck;

        static CozyRedesignAuthoring()
        {
            File.WriteAllText("Temp/CozyRedesign.version", "20261016n");
            EditorApplication.update += CheckRequest;
        }

        static void CheckRequest()
        {
            if (EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            nextCheck = EditorApplication.timeSinceStartup + 2;
            var request = "Temp/CozyRedesign.request";
            if (!File.Exists(request))
                return;
            var action = File.ReadAllText(request).Trim();
            File.Delete(request);
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Exit Play Mode before editing UI assets.");
                if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
                    throw new InvalidOperationException("Save and close Prefab Stage before editing assets.");
                if (action == "inspect")
                    Inspect();
                else if (action == "apply")
                    Apply();
                else if (action == "verify")
                    Verify();
                else if (action == "preview")
                    CozyRedesignPreview.RenderAll();
                else if (action == "overlayhud")
                {
                    DesktopPrefabValidator.EnsureOverlayHud();
                    var exportError = OverlayHudExporter.Export(out _);
                    if (!string.IsNullOrEmpty(exportError))
                        throw new InvalidOperationException(exportError);
                }
#if UNITY_INCLUDE_TESTS
                else if (action == "test")
                    RunEditModeTests();
#endif
                else if (action == "recompile")
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                }
                else if (action == "build-debug")
                    DesktopBuildMenu.BuildWindowsDevelopment();
                else
                    throw new InvalidOperationException("Unsupported request: " + action);
                File.WriteAllText("Temp/CozyRedesign.result", "OK " + action);
            }
            catch (Exception exception)
            {
                File.WriteAllText("Temp/CozyRedesign.result", exception.ToString());
                Debug.LogException(exception);
            }
        }

#if UNITY_INCLUDE_TESTS
        const string TestReport = "Temp/CozyRedesign.tests.txt";

        static void RunEditModeTests()
        {
            File.Delete(TestReport);
            var api = ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
            api.RegisterCallbacks(new TestReportWriter());
            api.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(
                new UnityEditor.TestTools.TestRunner.Api.Filter { testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode }));
        }

        sealed class TestReportWriter : UnityEditor.TestTools.TestRunner.Api.ICallbacks
        {
            public void RunStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor testsToRun) { }
            public void TestStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor test) { }
            public void TestFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result) { }

            public void RunFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result)
            {
                var report = new StringBuilder();
                report.AppendLine("passed=" + result.PassCount + " failed=" + result.FailCount + " skipped=" + result.SkipCount);
                Collect(result, report);
                File.WriteAllText(TestReport, report.ToString());
            }

            static void Collect(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result, StringBuilder report)
            {
                if (!result.HasChildren && result.TestStatus == UnityEditor.TestTools.TestRunner.Api.TestStatus.Failed)
                    report.AppendLine("== " + result.FullName).AppendLine(result.Message).AppendLine(result.StackTrace);
                foreach (var child in result.Children)
                    Collect(child, report);
            }
        }
#endif

        public static void BatchApplyAndPreview()
        {
            Apply();
            CozyRedesignPreview.RenderAll();
            Verify();
        }

        [MenuItem("Fish Social/Cozy Redesign/Inspect Saved UI")]
        public static void Inspect()
        {
            var output = new StringBuilder();
            foreach (var page in Pages)
            {
                var path = Folder + page + ".prefab";
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    output.AppendLine("MISSING " + path);
                    continue;
                }
                output.AppendLine("PAGE " + page + " " + PrefabUtility.GetPrefabAssetType(asset));
                foreach (var rect in asset.GetComponentsInChildren<RectTransform>(true))
                {
                    var relative = AnimationUtility.CalculateTransformPath(rect, asset.transform);
                    var text = rect.GetComponent<Text>();
                    var image = rect.GetComponent<Image>();
                    var groups = rect.GetComponents<LayoutGroup>();
                    output.Append(relative).Append(" | ").Append(rect.gameObject.activeSelf)
                        .Append(" | ").Append(rect.anchorMin).Append("/").Append(rect.anchorMax)
                        .Append(" | ").Append(rect.anchoredPosition).Append(" | ").Append(rect.sizeDelta);
                    if (text != null)
                        output.Append(" | TEXT ").Append(text.fontSize).Append(' ').Append(text.text.Replace("\n", "\\n"));
                    if (image != null)
                        output.Append(" | IMAGE ").Append(image.type).Append(' ').Append(image.sprite != null ? image.sprite.name : "null");
                    foreach (var group in groups)
                        output.Append(" | GROUP ").Append(group.GetType().Name).Append(' ').Append(group.enabled);
                    foreach (var component in rect.GetComponents<Component>())
                        if (component != null && !(component is RectTransform) && !(component is CanvasRenderer))
                            output.Append(" | COMPONENT ").Append(component.GetType().Name);
                    if (image != null) output.Append(" | ENABLED ").Append(image.enabled).Append(" COLOR ").Append(image.color);
                    output.AppendLine();
                }
            }
            File.WriteAllText("Library/CozyRedesign.inspect.txt", output.ToString());
        }

        [MenuItem("Fish Social/Cozy Redesign/Apply Approved Eight Page Layouts")]
        public static void Apply()
        {
            ImportAtlas("items-atlas", 4, 3, new[] { "rod-bamboo", "rod-light", "rod-wood", "rod-long", "bait-basic", "bait-wheat", "bait-berry", "bait-shrimp", "bait-mixed", "bait-pellet", "boat-wood", "boat-blue" });
            ImportAtlas("fish-atlas", 3, 2, new[] { "black_carp", "koi", "largemouth_bass", "grass_carp", "carp", "snakehead" });
            CreateHeaderLibrary();
            Edit("DesktopShell", Shell);
            Edit("PanelLeaderboard", Leaderboard);
            Edit("PanelProfileHub", root => Profile(root, false));
            Edit("PanelAchievements", root => Profile(root, true));
            Edit("PanelSocial", Chat);
            Edit("PanelCatch", Inventory);
            Edit("PanelEquipment", root => Shop(root, true));
            Edit("PanelShop", root => Shop(root, false));
            Edit("PanelSocialFeed", Feed);
            Edit("PanelSettings", Settings);
            Edit("PanelPondSelection", PondSelection);
            Edit("PanelWorldMap", WorldMap);
            Edit("PanelGallery", Gallery);
            Edit("PanelProfileEdit", ProfileEdit);
            Edit("AvatarChoice", AvatarCard);
            Edit("PondSelectionCard", PondCard);
            Edit("CatchSlot", root => ItemCard(root, false));
            Edit("ShopItemCard", root => ItemCard(root, true));
            Edit("ShowcaseSlot", ShowcaseCard);
            Edit("AchievementRow", AchievementCard);
            Edit("LeaderboardRow", RankRow);
            Edit("SocialPostCard", PostCard);
            AssetDatabase.SaveAssets();
            Inspect();
            Verify();
        }

        static void ImportAtlas(string name, int columns, int rows, string[] names)
        {
            var path = CozyPrefabRepair.ThemeFolder + "/items/" + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Missing art atlas: " + path);
            var pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Rect[] rects;
            try
            {
                pixels.LoadImage(File.ReadAllBytes(path));
                rects = new Rect[names.Length];
                for (var index = 0; index < names.Length; index++)
                {
                    var left = Mathf.RoundToInt((index % columns) * pixels.width / (float)columns);
                    var right = Mathf.RoundToInt((index % columns + 1) * pixels.width / (float)columns);
                    var top = Mathf.RoundToInt((index / columns) * pixels.height / (float)rows);
                    var bottom = Mathf.RoundToInt((index / columns + 1) * pixels.height / (float)rows);
                    rects[index] = TrimToArt(pixels, new RectInt(left, pixels.height - bottom, right - left, bottom - top), 12);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
            var sheet = importer.spritesheet;
            if (importer.spriteImportMode == SpriteImportMode.Multiple && sheet.Length == names.Length
                && Array.TrueForAll(names, cell => Array.Exists(sheet, sprite => sprite.name == cell
                    && sprite.rect == rects[Array.IndexOf(names, cell)])))
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.spritePixelsPerUnit = 100;
            var cells = new SpriteMetaData[names.Length];
            for (var index = 0; index < cells.Length; index++)
                cells[index] = new SpriteMetaData { name = names[index], alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f), rect = rects[index] };
            importer.spritesheet = cells;
            importer.SaveAndReimport();
        }

        static Rect TrimToArt(Texture2D pixels, RectInt cell, int padding)
        {
            var data = pixels.GetPixels32();
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (var y = cell.yMin; y < cell.yMax; y++)
                for (var x = cell.xMin; x < cell.xMax; x++)
                {
                    if (data[y * pixels.width + x].a <= 16)
                        continue;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
            if (maxX < 0)
                return new Rect(cell.x, cell.y, cell.width, cell.height);
            minX = Mathf.Max(cell.xMin, minX - padding); maxX = Mathf.Min(cell.xMax - 1, maxX + padding);
            minY = Mathf.Max(cell.yMin, minY - padding); maxY = Mathf.Min(cell.yMax - 1, maxY + padding);
            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        static void Edit(string name, Action<GameObject> action)
        {
            var path = Folder + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                action(root);
                Record(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void Record(GameObject root)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                if (PrefabUtility.IsPartOfPrefabInstance(rect.gameObject))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rect.gameObject);
        }

        static RectTransform Node(Transform root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return root as RectTransform;
            var existing = root.Find(path) as RectTransform;
            if (existing != null)
                return existing;
            var split = path.LastIndexOf('/');
            var parent = split >= 0 ? Node(root, path.Substring(0, split)) : root;
            var name = split >= 0 ? path.Substring(split + 1) : path;
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }

        static RectTransform Place(Transform root, string path, float left, float top, float width, float height)
        {
            var rect = Node(root, path);
            foreach (var group in rect.GetComponents<LayoutGroup>())
                group.enabled = false;
            foreach (var fitter in rect.GetComponents<ContentSizeFitter>())
                fitter.enabled = false;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
            return rect;
        }

        static void Stretch(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            rect.localScale = Vector3.one;
        }

        static void Hide(Transform root, params string[] paths)
        {
            foreach (var path in paths)
            {
                var node = root.Find(path);
                if (node != null)
                    node.gameObject.SetActive(false);
            }
        }

        static Text Label(Transform root, string path, string value, float left, float top, float width, float height, int size = 22, bool bold = false)
        {
            var rect = Place(root, path, left, top, width, height);
            var text = rect.GetComponent<Text>() ?? rect.gameObject.AddComponent<Text>();
            text.font = AssetFont();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = CatTheme.Ink;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        static Font AssetFont()
        {
            var font = CatTheme.Font;
            return font != null && AssetDatabase.Contains(font) ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        static Image Picture(Transform root, string path, Sprite sprite, float left, float top, float width, float height)
        {
            var rect = Place(root, path, left, top, width, height);
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(left + width / 2, -top - height / 2);
            return image;
        }

        static Image Paper(Transform root, string path, float left, float top, float width, float height)
        {
            var compact = Mathf.Min(width, height) < 64f;
            var image = Picture(root, path, compact
                ? CatTheme.LoadCozy("panels/paper-compact.png", CatTheme.CompactPaperBorder)
                : CatTheme.LoadCozy("panels/paper.png", CatTheme.PaperBorder), left, top, width, height);
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            return image;
        }

        static Sprite LoadButtonIcon(string icon, bool primary)
        {
            if (string.IsNullOrEmpty(icon))
                return null;
            var sprite = primary
                ? CatTheme.LoadCozy("icons-light/" + icon + ".png", Vector4.zero)
                : null;
            return sprite ?? CatTheme.LoadCozy("icons/" + icon + ".png", Vector4.zero);
        }

        static Button ButtonAt(Transform root, string path, string title, string icon, float left, float top, float width, float height, bool primary = false)
        {
            var rect = root.Find(path) as RectTransform;
            if (rect == null)
            {
                var split = path.LastIndexOf('/');
                var parent = split >= 0 ? Node(root, path.Substring(0, split)) : root;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Library/CozyButtonBase.prefab");
                var child = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                child.name = split >= 0 ? path.Substring(split + 1) : path;
            }
            rect = Place(root, path, left, top, width, height);
            var button = rect.GetComponent<Button>();
            if (button == null)
                throw new InvalidOperationException(path + " requires a Button");
            var persisted = rect.GetComponent<CozyButtonVisual>();
            if (persisted != null)
            {
                var state = new SerializedObject(persisted);
                state.FindProperty("selected").boolValue = false;
                state.ApplyModifiedPropertiesWithoutUndo();
            }
            DesktopModalUi.StyleButton(button, icon, primary);
            foreach (var text in rect.GetComponentsInChildren<Text>(true))
                text.gameObject.SetActive(text.transform.parent != rect || text.name == "Label");
            var label = Label(rect, "Label", title, string.IsNullOrEmpty(icon) ? 12 : 56, 0,
                width - (string.IsNullOrEmpty(icon) ? 24 : 68), height, height >= 62 ? 24 : 20, true);
            rect.GetComponent<CozyButtonVisual>()?.SetTint(primary ? CatTheme.Coral : Color.white);
            label.color = primary ? CatTheme.Cream : CatTheme.Ink;
            label.gameObject.SetActive(true);
            label.alignment = TextAnchor.MiddleCenter;
            var graphic = rect.Find("Icon")?.GetComponent<Image>();
            if (graphic != null)
            {
                graphic.gameObject.SetActive(!string.IsNullOrEmpty(icon));
                if (!string.IsNullOrEmpty(icon))
                {
                    var iconImage = Picture(rect, "Icon", LoadButtonIcon(icon, primary), 18, (height - 30) / 2, 30, 30);
                    iconImage.color = primary ? CatTheme.Cream : Color.white;
                }
            }
            var visual = rect.Find("CozyVisual")?.GetComponent<Image>();
            if (visual != null)
            {
                Stretch(visual.rectTransform);
                visual.type = Image.Type.Sliced;
                visual.pixelsPerUnitMultiplier = 0.7f;
            }
            return button;
        }

        static void CreateHeaderLibrary()
        {
            var path = Folder + "Library/CozyPageHeader.prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var root = asset != null ? PrefabUtility.LoadPrefabContents(path) :
                (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Library/CozyPanelToolbar.prefab"));
            try
            {
                root.name = "CozyPageHeader";
                var image = root.GetComponent<Image>();
                if (image != null)
                    image.enabled = false;
                Place(root.transform, "", 0, 0, 1370, 96);
                Picture(root.transform, "Icon", CatTheme.LoadCozy("icons/bag.png", Vector4.zero), 28, 25, 46, 46);
                Label(root.transform, "Title", "背包", 90, 23, 300, 50, 34, true);
                Label(root.transform, "Subtitle", "", 410, 28, 790, 42, 18);
                var close = ButtonAt(root.transform, "Close", "×", null, 1294, 22, 52, 52);
                DesktopModalUi.PlaceTopRight(close, 24, 22, 52);
                close.GetComponentInChildren<Text>(true).fontSize = 32;
                Paper(root.transform, "Rule", 24, 94, 1322, 1).color = new Color(0.84f, 0.72f, 0.55f, 0.45f);
                Record(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (asset != null) PrefabUtility.UnloadPrefabContents(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void Common(GameObject root, string title, string subtitle, string icon)
        {
            var transform = root.transform;
            foreach (var name in new[] { "Reflow_Title", "Reflow_Eyebrow", "Reflow_PageSeal", "Reflow_BlueSealPaper", "Reflow_HeaderRule", "Reflow_SectionMark", "Reflow_PagePaper", "Reflow_RailSeal", "Reflow_RailHeading", "Reflow_RailBody", "Retry" })
                Hide(transform, name);
            var background = root.GetComponent<Image>() ?? root.AddComponent<Image>();
            background.sprite = CatTheme.LoadCozy("panels/window.png", CatTheme.WindowBorder);
            background.type = Image.Type.Sliced;
            background.color = Color.white;
            background.raycastTarget = true;
            if (transform.Find("DesignHeader") == null)
            {
                var header = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Library/CozyPageHeader.prefab"), transform);
                header.name = "DesignHeader";
            }
            Place(transform, "DesignHeader", 0, 0, 1370, 96);
            Label(transform, "DesignHeader/Title", title, 90, 23, 310, 50, 34, true);
            Label(transform, "DesignHeader/Subtitle", subtitle, 410, 28, 790, 42, 18);
            Picture(transform, "DesignHeader/Icon", CatTheme.LoadCozy("icons/" + icon + ".png", Vector4.zero), 28, 25, 46, 46);
            Place(transform, "DesignHeader/Close", 1294, 22, 52, 52);
            DesktopModalUi.PlaceTopRight(transform.Find("DesignHeader/Close").GetComponent<Button>(), 24, 22, 52);
            transform.Find("DesignHeader").SetAsLastSibling();
            foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true))
            {
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                if (scroll.viewport != null)
                {
                    Stretch(scroll.viewport, 8);
                    if (scroll.viewport.GetComponent<RectMask2D>() == null && scroll.viewport.GetComponent<Mask>() == null)
                        scroll.viewport.gameObject.AddComponent<RectMask2D>();
                    var mask = scroll.viewport.GetComponent<Mask>();
                    if (mask != null) mask.showMaskGraphic = false;
                    else
                    {
                        var viewportImage = scroll.viewport.GetComponent<Image>();
                        if (viewportImage != null) viewportImage.color = Color.clear;
                    }
                }
                var backgroundImage = scroll.GetComponent<Image>();
                if (backgroundImage != null) backgroundImage.color = Color.clear;
            }
        }

        static void Grid(Transform root, string path, int columns, float width, float height)
        {
            var rect = Node(root, path);
            foreach (var group in rect.GetComponents<LayoutGroup>())
                if (!(group is GridLayoutGroup))
                    UnityEngine.Object.DestroyImmediate(group);
            var grid = rect.GetComponent<GridLayoutGroup>() ?? rect.gameObject.AddComponent<GridLayoutGroup>();
            grid.enabled = true;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(width, height);
            grid.spacing = new Vector2(14, 14);
            grid.padding = new RectOffset(4, 4, 4, 4);
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            var fitter = rect.GetComponent<ContentSizeFitter>() ?? rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.enabled = true;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        static void ScrollingText(Transform root, string scrollPath, string legacyTextPath, float left, float top, float width, float height)
        {
            var scrollRect = Place(root, scrollPath, left, top, width, height);
            var textName = legacyTextPath.Substring(legacyTextPath.LastIndexOf('/') + 1);
            var textRect = (root.Find(legacyTextPath) ?? scrollRect.Find(textName)) as RectTransform;
            if (textRect == null)
                throw new InvalidOperationException(legacyTextPath + " requires a Text");
            textRect.SetParent(scrollRect, false);
            textRect.anchorMin = Vector2.up;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(-8f, 0f);
            var fitters = textRect.GetComponents<ContentSizeFitter>();
            for (var index = 1; index < fitters.Length; index++)
                UnityEngine.Object.DestroyImmediate(fitters[index]);
            var fitter = fitters.Length > 0 ? fitters[0] : textRect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.enabled = true;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var text = textRect.GetComponent<Text>();
            text.fontSize = 17;
            text.alignment = TextAnchor.UpperLeft;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var hitArea = scrollRect.GetComponent<Image>();
            if (hitArea == null)
                hitArea = scrollRect.gameObject.AddComponent<Image>();
            hitArea.sprite = null;
            hitArea.color = Color.clear;
            hitArea.raycastTarget = true;
            var mask = scrollRect.GetComponent<RectMask2D>();
            if (mask == null)
                mask = scrollRect.gameObject.AddComponent<RectMask2D>();
            mask.enabled = true;
            var scroll = scrollRect.GetComponent<ScrollRect>();
            if (scroll == null)
                scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.enabled = true;
            scroll.viewport = scrollRect;
            scroll.content = textRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
        }

        static void Inventory(GameObject root)
        {
            var node = root.transform;
            Common(root, "背包", "把每一次收获，好好收藏", "bag");
            Paper(node, "Reflow_InfoRail", 24, 112, 210, 496);
            Hide(node, "Coins", "Reflow_CatchSeal");
            var category = ButtonAt(node, "DesignCategoryFish", "鱼获", "fish", 34, 124, 190, 76);
            category.interactable = false;
            var categoryVisual = new SerializedObject(category.GetComponent<CozyButtonVisual>());
            categoryVisual.FindProperty("selected").boolValue = true;
            categoryVisual.ApplyModifiedPropertiesWithoutUndo();
            Label(node, "DesignBagNote", "背包内展示真实鱼获\n选中后可出售、回鱼或分享", 44, 238, 170, 120, 18);
            Label(node, "Reflow_BagHeading", "我的鱼获", 254, 116, 690, 34, 25, true);
            Place(node, "Grid", 254, 164, 710, 432);
            var slots = node.Find("Grid/Viewport/Slots") as RectTransform;
            if (slots == null)
            {
                slots = Node(node, "Grid/Slots");
                slots.SetParent(Node(node, "Grid/Viewport"), false);
            }
            Grid(node, "Grid/Viewport/Slots", 3, 214, 196);
            node.Find("Grid").GetComponent<ScrollRect>().content = slots;
            Paper(node, "Reflow_CatchDetails", 984, 112, 362, 496);
            Label(node, "DesignDetailHeading", "鱼获详情", 1008, 124, 314, 36, 25, true);
            var backing = Paper(node, "DesignArtBacking", 1004, 166, 322, 146);
            backing.color = ArtBackingColor;
            backing.transform.SetSiblingIndex(node.Find("Reflow_CatchDetails").GetSiblingIndex() + 1);
            Picture(node, "DesignArt", null, 1012, 172, 306, 134);
            Label(node, "DesignName", "", 1008, 318, 314, 36, 26, true).alignment = TextAnchor.MiddleCenter;
            DetailRows(node, "DetailRows", 1008, 358, 314, 5, 27);
            var detail = Label(node, "Detail", "选中一条鱼获查看详情。", 1008, 358, 314, 132, 17);
            detail.alignment = TextAnchor.UpperLeft;
            ButtonAt(node, "Sell", "出售", "coin", 1008, 498, 314, 46, true);
            ButtonAt(node, "Return", "回鱼", "fish", 1008, 552, 150, 44);
            ButtonAt(node, "Share", "分享", "social", 1172, 552, 150, 44);
            Label(node, "Status", "", 254, 612, 1068, 26, 16);
        }

        static void Shop(GameObject root, bool equipment)
        {
            var node = root.transform;
            Common(root, equipment ? "装备" : "商店", equipment ? "选好装备，准备下一次出发" : "为下一次垂钓，备好小小惊喜", equipment ? "rod" : "shop");
            Hide(node, "Header/Title", "Reflow_ProductTableHeading", "Reflow_ProductNameColumn", "Reflow_ProductMetaColumn");
            Place(node, "Header", 0, 0, 1370, 650);
            Hide(node, "Header/Coins");
            Label(node, "Header/Equipped", "正在加载当前装备…", 44, 406, 170, 172, 17);
            Paper(node, "Reflow_InfoRail", 24, 112, 210, 496);
            node.Find("Reflow_InfoRail").SetAsFirstSibling();
            Place(node, "Tabs", 34, 124, 190, 260);
            var names = new[] { "Bait", "Tackle", "Vessel" };
            var titles = new[] { "鱼饵", "钓竿", "船具" };
            var icons = new[] { "leaf", "rod", "pond" };
            for (var index = 0; index < names.Length; index++)
                ButtonAt(node, "Tabs/" + names[index], titles[index], icons[index], 0, index * 88, 190, 76);
            Label(node, "DesignItemsHeading", equipment ? "我的装备" : "垂钓补给", 254, 116, 710, 34, 25, true);
            Place(node, "Items", 254, 164, 710, 432);
            Grid(node, "Items/Viewport/Content", equipment ? 2 : 3, equipment ? 326 : 214, 196);
            Place(node, "Details", 984, 112, 362, 496);
            foreach (var scroll in node.Find("Details").GetComponents<ScrollRect>()) scroll.enabled = false;
            foreach (var mask in node.Find("Details").GetComponentsInChildren<RectMask2D>(true)) mask.enabled = false;
            foreach (var mask in node.Find("Details").GetComponentsInChildren<Mask>(true)) mask.enabled = false;
            Paper(node, "Details/DesignPaper", 0, 0, 362, 496).transform.SetAsFirstSibling();
            Label(node, "Details/DesignHeading", equipment ? "装备详情" : "商品详情", 24, 12, 314, 36, 25, true);
            Picture(node, "Details/DesignArt", null, 24, 50, 314, 126);
            var artBacking = Paper(node, "Details/DesignArtBacking", 18, 52, 326, 126);
            artBacking.color = ArtBackingColor;
            artBacking.transform.SetSiblingIndex(1);
            node.Find("Details/DesignArt").SetAsLastSibling();
            Label(node, "Details/DesignName", "", 24, 180, 314, 36, 26, true).alignment = TextAnchor.MiddleCenter;
            ScrollingText(node, "Details/DescriptionScroll", "Details/Description", 24, 222, 314, 198);
            ButtonAt(node, "Details/Buy", "购买", "coin", 24, 432, 314, 48, true);
            ButtonAt(node, "Details/Equip", "装备", "rod", 24, 432, 314, 48, true)
                .GetComponent<CozyButtonVisual>().SetTint(new Color(0.35f, 0.56f, 0.95f, 1));
            Hide(node, "Details/Retry", "Details/Reflow_DetailSeal", "Details/Reflow_Heading", "Details/Reflow_DescriptionViewport");
            Label(node, "Status", "", 254, 612, 1068, 26, 16);
        }

        static void AvatarCard(GameObject root)
        {
            ShowcaseCard(root);
            var art = root.transform.Find("DesignItemArt").GetComponent<Image>();
            art.rectTransform.anchorMin = new Vector2(0, .28f);
            art.rectTransform.offsetMin = new Vector2(12, 4);
            art.rectTransform.offsetMax = new Vector2(-12, -8);
            var label = root.transform.Find("Label").GetComponent<Text>();
            label.rectTransform.anchorMax = new Vector2(1, .28f);
            label.fontSize = 18;
        }

        static void ProfileEdit(GameObject root)
        {
            var node = root.transform;
            Common(root, "编辑资料", "选择猫咪形象，整理你的展示鱼获", "user");
            foreach (var child in node.GetComponentsInChildren<RectTransform>(true))
                if (child.name.StartsWith("Reflow_", StringComparison.Ordinal)) child.gameObject.SetActive(false);
            Hide(node, "Header", "Actions/Retry");
            Paper(node, "DesignFormPaper", 24, 112, 402, 432).transform.SetAsFirstSibling();
            Place(node, "Form", 48, 132, 354, 382);
            Label(node, "Form/NicknameLabel", "游戏昵称", 0, 0, 354, 32, 22, true);
            Place(node, "Form/Nickname", 0, 42, 354, 54);
            Label(node, "Form/BioLabel", "个人简介", 0, 122, 354, 32, 22, true);
            Place(node, "Form/Bio", 0, 164, 354, 194);
            foreach (var input in node.Find("Form").GetComponentsInChildren<InputField>(true))
            {
                var background = input.GetComponent<Image>();
                background.sprite = CatTheme.LoadCozy("panels/paper.png", CatTheme.PaperBorder);
                background.type = Image.Type.Sliced;
                background.color = Color.white;
                foreach (var text in input.GetComponentsInChildren<Text>(true))
                {
                    if (text != input.textComponent && text != input.placeholder)
                    {
                        text.gameObject.SetActive(false);
                        continue;
                    }
                    Inset(text.rectTransform, 16, 12, 16, 12);
                    text.font = AssetFont();
                    text.fontSize = 20;
                    text.alignment = input.name == "Bio" ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
                }
            }
            Paper(node, "DesignAvatarPaper", 450, 112, 896, 194).transform.SetAsFirstSibling();
            Label(node, "AvatarsTitle", "选择头像", 474, 120, 600, 34, 24, true);
            Place(node, "Avatars", 466, 158, 864, 136);
            Grid(node, "Avatars/Viewport/Content", 6, 126, 112);
            Paper(node, "DesignShowcasePaper", 450, 326, 896, 264).transform.SetAsFirstSibling();
            Label(node, "ShowcaseTitle", "我的展示柜 · 点击格子更换鱼获", 474, 334, 830, 36, 24, true);
            Place(node, "Showcase", 466, 380, 864, 194);
            Grid(node, "Showcase/Viewport/Content", 4, 196, 164);
            Place(node, "Actions", 24, 558, 402, 48);
            ButtonAt(node, "Actions/Save", "保存资料", "user", 0, 0, 402, 48, true);
            Label(node, "Status", "", 24, 612, 1322, 28, 16);
            Paper(node, "Picker", 236, 108, 898, 498);
            Label(node, "Picker/Title", "选择展示鱼获", 24, 12, 700, 42, 28, true);
            Place(node, "Picker/Viewport", 20, 68, 858, 350);
            Grid(node, "Picker/Viewport/Content", 4, 192, 158);
            ButtonAt(node, "Picker/Clear", "清空此格", "fish", 24, 434, 240, 44);
            var pickerClose = ButtonAt(node, "Picker/Close", "×", null, 830, 12, 48, 48);
            DesktopModalUi.PlaceTopRight(pickerClose, 20, 12, 48);
            node.Find("Picker").SetAsLastSibling();
            node.Find("Picker").gameObject.SetActive(false);
        }

        static void PondCard(GameObject root)
        {
            var node = root.transform;
            Place(node, "", 0, 0, 1036, 196);
            var element = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
            element.preferredHeight = element.minHeight = 196;
            element.flexibleWidth = 1;
            Paper(node, "DesignPaper", 0, 0, 1036, 196).transform.SetAsFirstSibling();
            Stretch(node.Find("DesignPaper") as RectTransform);
            var backing = Paper(node, "DesignIconPaper", 16, 16, 156, 164);
            backing.color = ArtBackingColor;
            Picture(node, "PondIcon", CatTheme.LoadCozy("icons/pond.png", Vector4.zero), 50, 50, 88, 88);
            backing.transform.SetSiblingIndex(node.Find("PondIcon").GetSiblingIndex());
            Label(node, "Name", "", 196, 16, 540, 42, 28, true);
            Label(node, "Description", "", 196, 66, 520, 56, 18);
            Label(node, "Level", "", 196, 136, 250, 38, 17);
            Label(node, "Fee", "", 458, 130, 280, 50, 17);
            Label(node, "Access", "", 762, 24, 248, 74, 17);
            ButtonAt(node, "FocusOnMap", "查看鱼塘", "map", 762, 126, 248, 48, true);
        }

        static void Leaderboard(GameObject root)
        {
            var node = root.transform;
            Common(root, "钓友排行榜", "每一次收获，都值得被看见", "trophy");
            Hide(node, "Reflow_RankTableHeading", "Reflow_RankColumn", "Reflow_NameColumn", "Reflow_ValueColumn");
            // Podium stays a compact strip so the rank list below can show several rows.
            Paper(node, "Reflow_PodiumPaper", 24, 112, 980, 168).transform.SetAsFirstSibling();
            Place(node, "Podium", 24, 112, 980, 168);
            for (var rank = 1; rank <= 3; rank++)
            {
                var first = rank == 1;
                var x = rank == 2 ? 70 : first ? 358 : 646;
                var height = first ? 164 : 152;
                var slot = Place(node, "Podium/Slot" + rank, x, first ? 0 : 12, 264, height);
                Paper(slot, "DesignBacking", 0, 0, 264, height).transform.SetAsFirstSibling();
                Label(slot, "Rank", rank.ToString(), 16, 2, 232, 26, 22, true).alignment = TextAnchor.MiddleCenter;
                Picture(slot, "Avatar", CatTheme.LoadCozy("characters/portraits/orange.png", Vector4.zero), 96, 28, 72, 72);
                Label(slot, "Nickname", "等待榜单", 12, 102, 240, 22, 16, true).alignment = TextAnchor.MiddleCenter;
                Label(slot, "Value", "—", 12, 124, 240, 22, 14).alignment = TextAnchor.MiddleCenter;
            }
            Paper(node, "Reflow_InfoRail", 1024, 112, 322, 496).transform.SetAsFirstSibling();
            Place(node, "Tabs", 1040, 128, 290, 248);
            var names = new[] { "Daily", "Weekly", "Pond", "Rare" };
            var titles = new[] { "今日大鱼", "每周钓王", "鱼塘榜", "珍稀榜" };
            for (var index = 0; index < names.Length; index++)
                ButtonAt(node, "Tabs/" + names[index], titles[index], index == 2 ? "pond" : "trophy", 0, index * 60, 290, 50);
            Label(node, "DesignRailHeading", "榜单说明", 1048, 404, 274, 34, 25, true);
            Label(node, "DesignRailBody", "以实际榜单数据为准\n\n切换分类，查看不同榜单", 1048, 454, 274, 120, 19);
            Paper(node, "DesignTableHeading", 24, 288, 980, 34);
            Label(node, "DesignRankColumn", "排名", 44, 288, 100, 34, 18);
            Label(node, "DesignNameColumn", "钓友", 164, 288, 410, 34, 18);
            Label(node, "DesignValueColumn", "榜单成绩", 650, 288, 330, 34, 18).alignment = TextAnchor.MiddleRight;
            Place(node, "Scroll", 24, 328, 980, 244);
            Label(node, "MyRank", "", 40, 580, 940, 28, 19);
            Label(node, "Status", "", 40, 614, 940, 24, 15);
        }

        static void Profile(GameObject root, bool achievements)
        {
            var node = root.transform;
            Common(root, achievements ? "成就中心" : "个人中心", achievements ? "把每一次进步，收进自己的旅程" : "记录属于你的钓鱼时光", achievements ? "medal" : "user");
            var shellImage = node.Find("Shell")?.GetComponent<Image>();
            if (shellImage != null) shellImage.enabled = false;
            var contentImage = node.Find("Shell/Content")?.GetComponent<Image>();
            if (contentImage != null) contentImage.enabled = false;
            Place(node, "Shell/Header", 0, 0, 1370, 650);
            var headerImage = node.Find("Shell/Header").GetComponent<Image>();
            if (headerImage != null) headerImage.enabled = false;
            Hide(node, "Shell/Header/Title", "Shell/Header/Retry");
            Place(node, "Shell/Sidebar", 24, 112, 210, 496);
            var names = new[] { "资料", "展示柜", "图鉴", "相册", "成就" };
            var icons = new[] { "user", "star", "fish", "heart", "medal" };
            for (var index = 0; index < names.Length; index++)
                ButtonAt(node, "Shell/Sidebar/Tab_" + names[index], names[index], icons[index], 10, 12 + index * 94, 190, 80);
            Place(node, "Shell/Content", 254, 112, 1092, 496);
            foreach (var name in names)
            {
                var tab = node.Find("Shell/Content/Tab_" + name);
                if (tab == null) continue;
                Hide(tab, "Reflow_TabPaper");
                var image = tab.GetComponent<Image>();
                if (image != null) image.enabled = false;
            }
            var profile = Node(node, "Shell/Content/Tab_资料");
            Hide(profile, "Body", "Reflow_ProfileCaption", "Avatar/Label");
            Picture(profile, "Avatar", null, 20, 20, 156, 156);
            Label(profile, "DesignName", "—", 198, 26, 430, 42, 30, true);
            Label(profile, "DesignBio", "", 198, 80, 430, 70, 19);
            Label(profile, "DesignId", "", 198, 140, 430, 30, 16);
            Label(profile, "DesignStats", "", 20, 252, 630, 92, 24, true);
            Label(profile, "DesignPrivacy", "", 20, 374, 630, 80, 18);
            Hide(profile, "DesignStats", "DesignPrivacy");
            var cards = Place(profile, "DesignStatCards", 20, 196, 650, 96);
            for (var index = 0; index < 3; index++)
            {
                var card = Place(cards, "Card" + index, index * 221, 0, 208, 96);
                Paper(card, "Backing", 0, 0, 208, 96).transform.SetAsFirstSibling();
                Picture(card, "Icon", null, 16, 28, 40, 40).enabled = false;
                Label(card, "Label", "", 68, 16, 130, 26, 16);
                var value = Label(card, "Value", "—", 68, 44, 134, 38, 26, true);
                value.resizeTextForBestFit = true;
                value.resizeTextMinSize = 16;
                value.resizeTextMaxSize = 26;
            }
            var infoPaper = Paper(profile, "DesignInfoPaper", 20, 308, 650, 168).transform;
            if (infoPaper.GetSiblingIndex() > cards.GetSiblingIndex())
                infoPaper.SetSiblingIndex(cards.GetSiblingIndex());
            Label(profile, "DesignInfoHeading", "钓鱼档案", 40, 318, 300, 34, 22, true);
            DetailRows(profile, "DesignInfoRows", 40, 358, 610, 3, 36);
            Paper(profile, "DesignShowcase", 710, 0, 382, 496);
            Label(profile, "DesignShowcase/Heading", "我的展示柜", 24, 14, 334, 36, 25, true);
            Picture(profile, "DesignShowcase/Art", null, 28, 72, 326, 214);
            Label(profile, "DesignShowcase/Species", "暂无展示鱼获", 24, 308, 334, 42, 27, true).alignment = TextAnchor.MiddleCenter;
            Label(profile, "DesignShowcase/Size", "", 24, 360, 334, 38, 23).alignment = TextAnchor.MiddleCenter;
            ButtonAt(profile, "DesignShowcase/Open", "查看展示柜", "star", 24, 430, 334, 48, true);
            ButtonAt(node, "Shell/Header/Edit", "编辑资料", "user", 744, 130, 174, 48);
            var achievementTab = Node(node, "Shell/Content/Tab_成就");
            Hide(achievementTab, "Reflow_AchievementHeading");
            Label(achievementTab, "DesignHeading", "成就进度", 16, 10, 150, 38, 25, true);
            ButtonAt(achievementTab, "DesignFilterAll", "全部", null, 180, 4, 180, 48);
            ButtonAt(achievementTab, "DesignFilterCompleted", "已完成", null, 376, 4, 180, 48);
            ButtonAt(achievementTab, "DesignFilterPending", "未完成", null, 572, 4, 180, 48);
            Place(achievementTab, "Achievements", 8, 64, 1068, 416);
            Label(node, "Shell/Status", "", 254, 612, 1092, 24, 16);
            if (achievements)
            {
                Hide(node, "Shell/Sidebar", "Shell/Header/Edit");
                Place(node, "Shell/Content", 24, 112, 980, 496);
                Place(achievementTab, "Achievements", 8, 64, 964, 416);
                Paper(node, "DesignAchievementSummary", 1024, 112, 322, 496);
                Label(node, "DesignAchievementSummary/Heading", "成就档案", 24, 20, 274, 40, 27, true);
                Picture(node, "DesignAchievementSummary/Icon", CatTheme.LoadCozy("icons/medal.png", Vector4.zero), 86, 92, 150, 150);
                Label(node, "DesignAchievementSummary/Value", "—", 24, 268, 274, 58, 36, true).alignment = TextAnchor.MiddleCenter;
                Label(node, "DesignAchievementSummary/Caption", "已点亮的成就", 24, 336, 274, 38, 22).alignment = TextAnchor.MiddleCenter;
                Label(node, "DesignAchievementSummary/Note", "每一点进步，都值得纪念。", 24, 414, 274, 52, 18);
                node.Find("Shell/Content/Tab_成就").gameObject.SetActive(true);
            }
            else
                profile.gameObject.SetActive(true);
            StyleProfileTabs(node);
        }

        static void Chat(GameObject root)
        {
            var node = root.transform;
            Common(root, "好友与聊天", "与塘友同行，也和好友聊聊", "social");
            Place(node, "Tabs", 24, 112, 590, 48);
            ButtonAt(node, "Tabs/T0", "鱼塘交流", "social", 0, 0, 230, 48);
            ButtonAt(node, "Tabs/T1", "好友私信", "user", 244, 0, 230, 48);
            foreach (var name in new[] { "FriendsPage", "PondPage" })
                Place(node, name, 24, 176, 1322, 424);
            Paper(node, "FriendsPage/Reflow_PeoplePaper", 0, 0, 322, 424);
            Label(node, "FriendsPage/Reflow_Heading", "好友与邀请", 18, 12, 286, 36, 23, true);
            Place(node, "FriendsPage/Scroll", 10, 60, 302, 354);
            Place(node, "FriendsPage/FriendsChatPage", 342, 0, 980, 424);
            ConfigureChat(node, "FriendsPage/FriendsChatPage", "Messages", "DmInput", "SendDm", 980);
            Label(node, "FriendsPage/FriendsChatPage/DmTitle", "选择一个好友会话", 16, 10, 948, 38, 23, true);
            Place(node, "PondPage/OnlinePage", 0, 0, 322, 424);
            Paper(node, "PondPage/Reflow_PeoplePaper", 0, 0, 322, 424).transform.SetAsFirstSibling();
            Label(node, "PondPage/OnlinePage/Reflow_Heading", "在线钓友", 18, 12, 286, 36, 23, true);
            Place(node, "PondPage/OnlinePage/Scroll", 10, 60, 302, 354);
            Place(node, "PondPage/ChatPage", 342, 0, 980, 424);
            ConfigureChat(node, "PondPage/ChatPage", "Scroll", "ChatInput", "SendChat", 980);
            Label(node, "PondPage/ChatPage/Reflow_Heading", "鱼塘聊天", 16, 10, 948, 38, 23, true);
            Label(node, "Status", "", 32, 612, 1306, 24, 16);
        }

        static void ConfigureChat(Transform root, string path, string scrollName, string inputName, string sendName, float width)
        {
            Paper(root, path + "/DesignChatPaper", 0, 0, width, 424).transform.SetAsFirstSibling();
            Place(root, path + "/" + scrollName, 12, 60, width - 24, 280);
            var input = Place(root, path + "/" + inputName, 12, 354, width - 176, 56).GetComponent<InputField>();
            if (input != null)
            {
                var background = input.GetComponent<Image>();
                if (background != null)
                {
                    background.sprite = CatTheme.LoadCozy("panels/paper-compact.png", CatTheme.CompactPaperBorder);
                    background.type = Image.Type.Sliced;
                    background.color = Color.white;
                }
                foreach (var text in input.GetComponentsInChildren<Text>(true))
                {
                    Stretch(text.rectTransform);
                    text.rectTransform.offsetMin = new Vector2(16, 8);
                    text.rectTransform.offsetMax = new Vector2(-16, -8);
                    text.alignment = TextAnchor.MiddleLeft;
                    text.fontSize = 20;
                }
                input.lineType = InputField.LineType.SingleLine;
            }
            ButtonAt(root, path + "/" + sendName, "发送", null, width - 150, 354, 138, 56, true);
        }

        static void Feed(GameObject root)
        {
            var node = root.transform;
            Common(root, "钓友动态", "分享收获，也看看塘友的日常", "social");
            Paper(node, "Reflow_InfoRail", 24, 112, 210, 496).transform.SetAsFirstSibling();
            Place(node, "Header", 34, 124, 190, 180);
            ButtonAt(node, "Header/Public", "公开动态", "social", 0, 0, 190, 76);
            ButtonAt(node, "Header/Friends", "好友动态", "user", 0, 88, 190, 76);
            Place(node, "Scroll", 254, 112, 710, 496);
            Paper(node, "DesignFeedInfo", 984, 112, 362, 496).transform.SetAsFirstSibling();
            Label(node, "DesignFeedInfo/Heading", "分享钓鱼时光", 24, 26, 314, 40, 26, true);
            Label(node, "DesignFeedInfo/Body", "在背包选中鱼获后\n使用「分享」发布动态。\n\n分享鱼获与沿途风景，\n与塘友友好交流。", 24, 112, 314, 220, 21);
            ButtonAt(node, "DesignFeedInfo/Share", "前往背包分享", "social", 24, 344, 314, 52, true);
            Label(node, "DesignFeedInfo/Note", "动态内容以实际发布为准", 24, 414, 314, 50, 17);
            Label(node, "Status", "", 254, 612, 1068, 24, 16);
        }

        static void Settings(GameObject root)
        {
            var node = root.transform;
            var viewport = node.Find("SettingsViewport") as RectTransform;
            var pageScroll = root.GetComponent<ScrollRect>();
            if (pageScroll != null && viewport != null)
                pageScroll.viewport = viewport;
            Common(root, "设置", "调整窗口、通知与服务器连接", "settings");
            Stretch((RectTransform)node);
            root.GetComponent<Image>().color = Color.white;
            Hide(node, "Reflow_Title", "Reflow_PageSeal", "Reflow_Eyebrow", "SettingsHeader", "Reflow_InfoRail", "DesignSettingsRail");
            Paper(node, "DesignSettingsCard", 24, 112, 1322, 496).transform.SetAsFirstSibling();
            if (viewport != null)
                Place(node, "SettingsViewport", 48, 132, 1274, 452);
            var content = node.Find("SettingsViewport/SettingsContent") as RectTransform;
            if (content != null)
            {
                content.anchorMin = Vector2.up;
                content.anchorMax = Vector2.up;
                content.pivot = Vector2.up;
                content.anchoredPosition = Vector2.zero;
                content.sizeDelta = new Vector2(1274, 1080);
                var contentImage = content.GetComponent<Image>();
                if (contentImage != null)
                    contentImage.color = Color.clear;
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                DesktopModalUi.StyleButton(button, null, false);
            Label(node, "DesignSettingsHint", "修改后按页面内的保存按钮应用设置。", 24, 614, 1322, 24, 15);
        }

        static void PondSelection(GameObject root)
        {
            var node = root.transform;
            Common(root, "选择鱼塘", "选择一处水域，开始今天的垂钓", "pond");
            Hide(node, "PondSelectionHeader", "Reflow_RailHeading", "Reflow_RailBody", "Reflow_InfoRail", "Reflow_RailPaper");
            Paper(node, "DesignPondRail", 24, 112, 210, 496);
            Label(node, "DesignPondRail/Heading", "水域指南", 22, 22, 166, 36, 24, true);
            Label(node, "DesignPondRail/Body", "每个鱼塘都有\n不同的鱼种与\n垂钓节奏。", 22, 86, 166, 140, 20);
            Paper(node, "DesignPondRail/Tip", 18, 330, 174, 126);
            Label(node, "DesignPondRail/Tip/Body", "准备好鱼竿，\n再出发。", 18, 18, 138, 86, 18, true);
            var scroll = node.Find("PondCardScroll") as RectTransform;
            if (scroll != null)
            {
                Place(node, "PondCardScroll", 254, 112, 1092, 496);
                var viewport = node.Find("PondCardScroll/Viewport") as RectTransform;
                if (viewport != null)
                    Stretch(viewport, 12);
                var cards = node.Find("PondCardScroll/Viewport/Cards") as RectTransform;
                if (cards != null)
                {
                    foreach (var stale in cards.GetComponents<GridLayoutGroup>())
                        UnityEngine.Object.DestroyImmediate(stale);
                    var list = cards.GetComponent<VerticalLayoutGroup>() ?? cards.gameObject.AddComponent<VerticalLayoutGroup>();
                    list.enabled = true;
                    list.padding = new RectOffset(8, 8, 8, 8);
                    list.spacing = 14;
                    list.childAlignment = TextAnchor.UpperLeft;
                    list.childControlWidth = true;
                    list.childForceExpandWidth = true;
                    list.childControlHeight = true;
                    list.childForceExpandHeight = false;
                    cards.anchorMin = Vector2.up;
                    cards.anchorMax = Vector2.one;
                    cards.pivot = Vector2.up;
                    cards.anchoredPosition = Vector2.zero;
                    cards.sizeDelta = Vector2.zero;
                    var fitter = cards.GetComponent<ContentSizeFitter>() ?? cards.gameObject.AddComponent<ContentSizeFitter>();
                    fitter.enabled = true;
                    fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
            }
            Label(node, "Status", "", 254, 612, 1092, 24, 16);
        }

        static void WorldMap(GameObject root)
        {
            var node = root.transform;
            Common(root, "探索世界", "在地图上发现新的鱼塘", "map");
            Hide(node, "Reflow_RailHeading", "Reflow_RailBody", "Reflow_ModeCaption");
            Paper(node, "DesignMapFrame", 24, 112, 900, 496).transform.SetAsFirstSibling();
            Hide(node, "Details/Reflow_Heading", "Details/Reflow_ModeCaption", "Reflow_InfoRail");
            var viewport = node.Find("Viewport") as RectTransform;
            if (viewport != null)
            {
                Place(node, "Viewport", 34, 122, 880, 476);
                var content = node.Find("Viewport/MapContent") as RectTransform;
                if (content != null)
                {
                    content.anchorMin = Vector2.up;
                    content.anchorMax = Vector2.up;
                    content.pivot = Vector2.up;
                    content.anchoredPosition = Vector2.zero;
                    content.sizeDelta = new Vector2(2200, 1300);
                    var mapImage = node.Find("Viewport/MapContent/MapImage") as RectTransform;
                    if (mapImage != null)
                    {
                        mapImage.anchorMin = Vector2.zero;
                        mapImage.anchorMax = Vector2.one;
                        mapImage.offsetMin = Vector2.zero;
                        mapImage.offsetMax = Vector2.zero;
                        var image = mapImage.GetComponent<Image>();
                        image.sprite = CatTheme.LoadCozy("scenes/world-map.png", Vector4.zero);
                        image.color = Color.white;
                        image.type = Image.Type.Simple;
                    }
                }
            }
            Paper(node, "DesignDetailsPaper", 944, 112, 402, 496).transform.SetAsFirstSibling();
            Place(node, "Details", 944, 112, 402, 496);
            Label(node, "Details/DesignHeading", "鱼塘详情", 24, 18, 354, 36, 25, true);
            Label(node, "Details/DesignHint", "点击地图上的鱼塘标记", 24, 62, 354, 30, 18);
            Place(node, "Details/DetailsText", 24, 106, 354, 160);
            Place(node, "Details/StatusText", 24, 274, 354, 48);
            ButtonAt(node, "Details/ModeSell", "回鱼出售", "coin", 24, 338, 166, 46);
            ButtonAt(node, "Details/ModeAuto", "自动回鱼", "pond", 202, 338, 166, 46);
            ButtonAt(node, "Details/ResetView", "重置视图", "map", 24, 398, 166, 46);
            ButtonAt(node, "Details/EnterPond", "进入鱼塘", "pond", 202, 398, 166, 46, true);
        }

        static void Gallery(GameObject root)
        {
            var node = root.transform;
            Common(root, "鱼类图鉴", "记录每一次相遇的鱼种", "fish");
            Hide(node, "Reflow_GalleryHeading", "Reflow_RailHeading", "Reflow_RailBody", "Reflow_SpecimenNotes", "Reflow_SpecimenSeal", "Reflow_SpecimenHeading", "Reflow_SpeciesHeading");
            Paper(node, "DesignGalleryGrid", 24, 112, 900, 496);
            Place(node, "Grid", 38, 132, 872, 458);
            Grid(node, "Grid/Viewport/Species", 4, 204, 170);
            Paper(node, "DesignDetailPaper", 944, 112, 402, 496).transform.SetAsFirstSibling();
            Place(node, "Detail", 968, 182, 354, 370);
            Label(node, "Detail/DesignHeading", "鱼种详情", 24, 18, 354, 36, 25, true);
            Place(node, "Detail/Species", 24, 70, 354, 78);
            Place(node, "Detail/Body", 24, 164, 354, 220);
            Label(node, "Status", "", 24, 612, 1322, 24, 16);
        }

        static void StyleProfileTabs(Transform node)
        {
            foreach (var name in new[] { "展示柜", "图鉴", "相册" })
            {
                var tab = node.Find("Shell/Content/Tab_" + name);
                if (tab == null) continue;
                Hide(tab, "Reflow_Title", "Reflow_Eyebrow", "Reflow_PageSeal", "Reflow_HeaderRule", "Reflow_TabPaper", "Reflow_DetailPaper");
                var image = tab.GetComponent<Image>();
                if (image != null) image.enabled = false;
            }

            var showcase = node.Find("Shell/Content/Tab_展示柜");
            if (showcase != null)
            {
                Paper(showcase, "DesignShowcaseGrid", 0, 0, 680, 496).transform.SetAsFirstSibling();
                Label(showcase, "DesignGridHeading", "我的展示柜", 24, 14, 400, 36, 25, true);
                Place(showcase, "ShowcaseGrid", 18, 58, 644, 418);
                Grid(showcase, "ShowcaseGrid/Viewport/Content", 4, 140, 178);
                Paper(showcase, "DesignDetailPaper", 704, 0, 388, 496).transform.SetAsFirstSibling();
                DetailPanel(showcase, "展示详情");
                Place(showcase, "Detail", 728, 200, 340, 36);
                DetailRows(showcase, "ShowcaseDetailRows", 728, 242, 340, 8, 29);
            }

            var codex = node.Find("Shell/Content/Tab_图鉴");
            if (codex != null)
            {
                Paper(codex, "DesignCodexGrid", 0, 0, 680, 496).transform.SetAsFirstSibling();
                Label(codex, "DesignGridHeading", "鱼类图鉴", 24, 14, 400, 36, 25, true);
                Place(codex, "CodexGrid", 18, 58, 644, 418);
                Grid(codex, "CodexGrid/Viewport/Content", 4, 140, 140);
                Paper(codex, "DesignDetailPaper", 704, 0, 388, 496).transform.SetAsFirstSibling();
                DetailPanel(codex, "图鉴详情");
            }

            var album = node.Find("Shell/Content/Tab_相册");
            if (album != null)
            {
                Paper(album, "DesignAlbumStage", 0, 0, 1092, 390).transform.SetAsFirstSibling();
                Place(album, "AlbumStage", 18, 18, 1056, 354);
                var stage = album.Find("AlbumStage") as RectTransform;
                if (stage != null)
                {
                    var left = stage.Find("SlotL") as RectTransform;
                    var right = stage.Find("SlotR") as RectTransform;
                    if (left != null) { left.anchorMin = new Vector2(0, 0); left.anchorMax = new Vector2(.5f, 1); left.offsetMin = new Vector2(12, 12); left.offsetMax = new Vector2(-8, -12); }
                    if (right != null) { right.anchorMin = new Vector2(.5f, 0); right.anchorMax = new Vector2(1, 1); right.offsetMin = new Vector2(8, 12); right.offsetMax = new Vector2(-12, -12); }
                    AlbumSlot(left);
                    AlbumSlot(right);
                }
                Place(album, "Prev", 24, 414, 88, 32);
                Place(album, "Next", 980, 414, 88, 32);
                Place(album, "Prev_0", 24, 414, 88, 32);
                Place(album, "Next_0", 980, 414, 88, 32);
                Place(album, "Page", 458, 414, 176, 32);
            }
        }

        static void DetailPanel(Transform tab, string heading)
        {
            Hide(tab, "Detail/DesignHeading");
            Label(tab, "DesignDetailHeading", heading, 728, 18, 340, 36, 25, true);
            var backing = Paper(tab, "DesignDetailArtBacking", 722, 62, 352, 130);
            backing.color = ArtBackingColor;
            Picture(tab, "DesignDetailArt", null, 738, 70, 320, 114).enabled = false;
            var detail = Place(tab, "Detail", 728, 204, 340, 272).GetComponent<Text>();
            detail.alignment = TextAnchor.UpperLeft;
            detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            detail.verticalOverflow = VerticalWrapMode.Truncate;
            detail.resizeTextForBestFit = true;
            detail.resizeTextMinSize = 13;
            detail.resizeTextMaxSize = 17;
        }

        static void AlbumSlot(RectTransform slot)
        {
            if (slot == null)
                return;
            var photo = slot.Find("Photo") as RectTransform;
            if (photo != null)
            {
                photo.anchorMin = Vector2.up;
                photo.anchorMax = Vector2.one;
                photo.pivot = new Vector2(0.5f, 1f);
                photo.offsetMin = new Vector2(12f, -182f);
                photo.offsetMax = new Vector2(-12f, -12f);
            }
            Inset(slot.Find("Scrim") as RectTransform, 8f, 188f, 8f, 8f);
            var body = slot.Find("Body") as RectTransform;
            Inset(body, 20f, 196f, 20f, 12f);
            var text = body != null ? body.GetComponent<Text>() : null;
            if (text == null)
                return;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 13;
            text.resizeTextMaxSize = 18;
        }

        static void Inset(RectTransform rect, float left, float top, float right, float bottom)
        {
            if (rect == null)
                return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        static void ItemCard(GameObject root, bool shop)
        {
            var node = root.transform;
            Place(node, "", 0, 0, 214, 196);
            {
                var backing = Paper(node, "DesignArtBacking", 12, 10, 190, 112);
                backing.color = ArtBackingColor;
                backing.transform.SetSiblingIndex(node.Find("CozyVisual") != null ? node.Find("CozyVisual").GetSiblingIndex() + 1 : 0);
            }
            Picture(node, "DesignItemArt", null, 16, 12, 182, 108);
            Label(node, "Label", "", 12, 126, 190, shop ? 28 : 58, 21, true).alignment = TextAnchor.MiddleCenter;
            if (shop)
                Label(node, "Meta", "", 12, 158, 190, 28, 16).alignment = TextAnchor.MiddleCenter;
            Hide(node, "Icon", "CozyIcon");
            SelectedCard(root);
            foreach (var name in new[] { "DesignArtBacking", "DesignItemArt", "Label", "Meta" })
            {
                var rect = node.Find(name) as RectTransform;
                if (rect == null) continue;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = new Vector2(name == "DesignItemArt" ? -32 : -24, rect.sizeDelta.y);
                rect.anchoredPosition = new Vector2(rect.pivot.x == 0.5f ? 0 : 12, rect.anchoredPosition.y);
            }
            var element = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
            element.preferredHeight = 196;
            element.minHeight = 196;
            element.flexibleWidth = 0;
        }

        static readonly Color ArtBackingColor = new Color(0.88f, 0.94f, 0.97f, 1f);

        static Color AchievementBadge(bool unlocked)
        {
            return unlocked ? CozyItemArt.CompletedBadge : CozyItemArt.PendingBadge;
        }

        static void SelectedCard(GameObject root)
        {
            var visual = root.GetComponent<CozyButtonVisual>();
            if (visual == null)
                return;
            var serialized = new SerializedObject(visual);
            serialized.FindProperty("selectedSprite").objectReferenceValue =
                CatTheme.LoadCozy("buttons/plate-selected.png", CatTheme.PlateBorder);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ShowcaseCard(GameObject root)
        {
            var node = root.transform;
            var backing = Paper(node, "DesignArtBacking", 0, 0, 180, 100);
            backing.color = ArtBackingColor;
            backing.rectTransform.anchorMin = new Vector2(0, .42f);
            backing.rectTransform.anchorMax = Vector2.one;
            backing.rectTransform.offsetMin = new Vector2(8, 0);
            backing.rectTransform.offsetMax = new Vector2(-8, -8);
            backing.transform.SetSiblingIndex(node.Find("CozyVisual") != null ? node.Find("CozyVisual").GetSiblingIndex() + 1 : 0);
            var art = Picture(node, "DesignItemArt", null, 0, 0, 10, 10);
            art.rectTransform.anchorMin = new Vector2(0f, 0.42f);
            art.rectTransform.anchorMax = Vector2.one;
            art.rectTransform.offsetMin = new Vector2(10f, 0f);
            art.rectTransform.offsetMax = new Vector2(-10f, -8f);
            art.enabled = false;
            var label = node.Find("Label") as RectTransform;
            if (label == null)
                throw new InvalidOperationException("ShowcaseSlot requires Label");
            label.anchorMin = Vector2.zero;
            label.anchorMax = new Vector2(1f, 0.42f);
            label.pivot = new Vector2(0.5f, 0.5f);
            label.offsetMin = new Vector2(6f, 6f);
            label.offsetMax = new Vector2(-6f, 0f);
            var text = label.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.font = AssetFont();
            text.fontSize = 18;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = 18;
            Hide(node, "Icon", "CozyIcon");
            SelectedCard(root);
        }

        static void DetailRows(Transform root, string path, float left, float top, float width, int count, float rowHeight)
        {
            var rows = Place(root, path, left, top, width, count * rowHeight);
            for (var index = 0; index < count; index++)
            {
                var row = Place(rows, "Row" + index, 0, index * rowHeight, width, rowHeight);
                Picture(row, "Icon", null, 2, (rowHeight - 20) / 2, 20, 20).enabled = false;
                Label(row, "Label", "", 30, 0, 96, rowHeight, 16);
                var value = Label(row, "Value", "", 126, 0, width - 126, rowHeight, 16, true);
                value.alignment = TextAnchor.MiddleRight;
                value.resizeTextForBestFit = true;
                value.resizeTextMinSize = 11;
                value.resizeTextMaxSize = 16;
                if (index < count - 1)
                    Paper(row, "Rule", 0, rowHeight - 1, width, 1).color = new Color(0.84f, 0.72f, 0.55f, 0.35f);
                row.gameObject.SetActive(false);
            }
        }

        static void AchievementCard(GameObject root)
        {
            var node = root.transform;
            Place(node, "", 0, 0, 928, 100);
            Picture(node, "Reflow_AchievementSeal", CatTheme.LoadCozy("icons/medal.png", Vector4.zero), 18, 22, 56, 56);
            Label(node, "Label", "", 94, 14, 646, 72, 20);
            var badge = Paper(node, "DesignStateBadge", 770, 30, 124, 40);
            badge.color = AchievementBadge(false);
            var state = Label(node, "DesignState", "", 770, 30, 124, 40, 18, true);
            state.alignment = TextAnchor.MiddleCenter;
            if (badge.transform.GetSiblingIndex() > state.transform.GetSiblingIndex())
                badge.transform.SetSiblingIndex(state.transform.GetSiblingIndex());
            var element = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
            element.preferredHeight = element.minHeight = 100;
        }

        static void RankRow(GameObject root)
        {
            var node = root.transform;
            Place(node, "", 0, 0, 948, 54);
            Label(node, "Rank", "", 16, 8, 80, 38, 21, true);
            Picture(node, "Avatar", null, 112, 7, 40, 40);
            node.Find("Avatar").GetComponent<Image>().enabled = false;
            Label(node, "Nickname", "", 166, 8, 410, 38, 21);
            Label(node, "Value", "", 590, 8, 338, 38, 20).alignment = TextAnchor.MiddleRight;
            var element = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
            element.preferredHeight = element.minHeight = 54;
        }

        static void PostCard(GameObject root)
        {
            var node = root.transform;
            Node(node, "DesignCardVersion").gameObject.SetActive(false);
            Place(node, "", 0, 0, 678, 378);
            foreach (var group in root.GetComponents<LayoutGroup>()) group.enabled = false;
            Place(node, "Header", 18, 14, 642, 46);
            Label(node, "Header/AuthorText", "", 0, 0, 642, 46, 21, true);
            Label(node, "BodyText", "", 18, 70, 642, 64, 20);
            Picture(node, "Photo", null, 18, 144, 476, 148);
            Label(node, "FishInfoText", "", 18, 302, 642, 28, 16);
            Place(node, "Actions", 18, 336, 642, 34);
            ButtonAt(node, "Actions/LikeButton", "点赞", "heart", 0, 0, 144, 34);
            ButtonAt(node, "Actions/CommentsButton", "评论", "social", 158, 0, 144, 34);
            Place(node, "CommentsPanel", 18, 390, 642, 250);
            var element = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
            element.preferredHeight = element.minHeight = 378;
        }

        static void StyleLevelProgress(Transform header)
        {
            if (header == null)
                return;
            var badge = header.Find("ProfileCard/LevelBadge")?.GetComponent<Image>();
            if (badge != null)
            {
                badge.sprite = CatTheme.LoadCozy("panels/chip.png", CatTheme.ChipBorder);
                badge.type = Image.Type.Sliced;
                badge.preserveAspect = false;
                badge.color = Color.white;
            }
            var level = header.Find("ProfileCard/Level")?.GetComponent<Text>();
            if (level != null)
                level.color = Color.white;
            var caption = header.Find("ProfileCard/XpLabel")?.GetComponent<Text>();
            if (caption != null)
            {
                caption.color = CatTheme.Ink;
                caption.alignment = TextAnchor.MiddleCenter;
                caption.fontSize = 15;
            }
            var background = header.Find("ProfileCard/XpBackground")?.GetComponent<Image>();
            var fill = DesktopModalUi.FindXpFill(header);
            DesktopModalUi.ConfigureXpBar(background, fill);
            DesktopModalUi.SetXpFill(fill, 0f);
        }

        static void Shell(GameObject root)
        {
            var node = root.transform;
            foreach (var page in new[] { "Leaderboard", "Profile", "Achievements", "Friends", "CatchBag", "Equipment", "Shop", "SocialFeed", "Settings" })
                Place(node, "MainRoot/Content/" + page, 160, 150, 1370, 650);
            foreach (var name in new[] { "Mail", "Settings" })
            {
                var button = node.Find("MainRoot/Header/" + name);
                if (button == null) continue;
                Picture(button, "Icon", CatTheme.LoadCozy("icons/" + (name == "Mail" ? "bell" : "settings") + ".png", Vector4.zero), 16, 16, 38, 38);
                Hide(button, "Label");
            }
            StyleLevelProgress(node.Find("MainRoot/Header"));
            var amount = node.Find("MainRoot/Header/FishCoins/Amount")?.GetComponent<Text>();
            if (amount != null)
            {
                amount.resizeTextForBestFit = true;
                amount.resizeTextMinSize = 17;
                amount.resizeTextMaxSize = 27;
                amount.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            foreach (var label in root.GetComponentsInChildren<Text>(true))
                if (label.transform.IsChildOf(node.Find("MainRoot/Nav"))) label.fontStyle = FontStyle.Bold;
        }

        [MenuItem("Fish Social/Cozy Redesign/Validate Saved Layouts")]
        public static void Verify()
        {
            var errors = CozyUiBuildGuard.CollectErrors();
            foreach (var name in new[] { "PanelLeaderboard", "PanelProfileHub", "PanelAchievements", "PanelSocial", "PanelCatch", "PanelEquipment", "PanelShop", "PanelSocialFeed", "PanelSettings", "PanelPondSelection", "PanelWorldMap", "PanelGallery", "PanelProfileEdit" })
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
                var close = root.transform.Find("DesignHeader/Close");
                if (close == null || close.GetComponent<Button>() == null)
                    errors.Add(name + ": missing authored close Button");
                foreach (var image in root.GetComponentsInChildren<Image>(true))
                    if (image.sprite != null && image.sprite.border != Vector4.zero && image.type != Image.Type.Sliced && image.type != Image.Type.Filled)
                        errors.Add(name + ": bordered image is not sliced: " + image.name);
            }
            File.WriteAllText("Library/CozyRedesign.validation.txt", errors.Count == 0 ? "PASS" : string.Join("\n", errors));
            if (errors.Count != 0)
                throw new InvalidOperationException(string.Join("\n", errors));
        }
    }
}
#endif
