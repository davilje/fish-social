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
        static readonly string[] Pages = { "DesktopShell", "PanelHome", "PanelLeaderboard", "PanelProfileHub", "PanelAchievements", "PanelSocial", "PanelCatch", "PanelEquipment", "PanelShop", "PanelSocialFeed", "CatchSlot", "ShopItemCard", "LeaderboardRow", "AchievementRow", "SocialPostCard" };
        static double nextCheck;

        static CozyRedesignAuthoring()
        {
            File.WriteAllText("Temp/CozyRedesign.version", "20261015b");
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
            Edit("CatchSlot", root => ItemCard(root, false));
            Edit("ShopItemCard", root => ItemCard(root, true));
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
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.spritePixelsPerUnit = 100;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var cells = new SpriteMetaData[names.Length];
            for (var index = 0; index < cells.Length; index++)
            {
                var left = Mathf.RoundToInt((index % columns) * texture.width / (float)columns);
                var right = Mathf.RoundToInt((index % columns + 1) * texture.width / (float)columns);
                var top = Mathf.RoundToInt((index / columns) * texture.height / (float)rows);
                var bottom = Mathf.RoundToInt((index / columns + 1) * texture.height / (float)rows);
                cells[index] = new SpriteMetaData { name = names[index], alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f), rect = new Rect(left, texture.height - bottom, right - left, bottom - top) };
            }
            importer.spritesheet = cells;
            importer.SaveAndReimport();
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
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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

        static Image Picture(Transform root, string path, Sprite sprite, float left, float top, float width, float height)
        {
            var rect = Place(root, path, left, top, width, height);
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        static Image Paper(Transform root, string path, float left, float top, float width, float height)
        {
            var image = Picture(root, path, CatTheme.LoadCozy("panels/paper.png", CatTheme.PaperBorder), left, top, width, height);
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
            return image;
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
                    Picture(rect, "Icon", CatTheme.LoadCozy((primary ? "icons-light/" : "icons/") + icon + ".png", Vector4.zero), 18, (height - 30) / 2, 30, 30);
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
            background.sprite = CatTheme.LoadCozy("panels/paper.png", CatTheme.PaperBorder);
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

        static void Inventory(GameObject root)
        {
            var node = root.transform;
            Common(root, "背包", "把每一次收获，好好收藏", "bag");
            Paper(node, "Reflow_InfoRail", 24, 112, 210, 496);
            Hide(node, "Coins", "Reflow_CatchSeal");
            ButtonAt(node, "DesignCategoryFish", "鱼获", "fish", 34, 124, 190, 76).interactable = false;
            Label(node, "DesignBagNote", "背包内展示真实鱼获\n选中后可出售、回鱼或分享", 44, 238, 170, 120, 18);
            Label(node, "Reflow_BagHeading", "我的鱼获", 254, 116, 690, 34, 25, true);
            Place(node, "Grid", 254, 164, 710, 432);
            var slots = Node(node, "Grid/Slots");
            slots.SetParent(Node(node, "Grid/Viewport"), false);
            slots.name = "Slots";
            Grid(node, "Grid/Viewport/Slots", 3, 214, 196);
            node.Find("Grid").GetComponent<ScrollRect>().content = slots;
            Paper(node, "Reflow_CatchDetails", 984, 112, 362, 496);
            Label(node, "DesignDetailHeading", "鱼获详情", 1008, 124, 314, 36, 25, true);
            Picture(node, "DesignArt", null, 1012, 166, 306, 122);
            Label(node, "Detail", "选中一条鱼获查看详情。", 1008, 294, 314, 190, 17);
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
            var description = Place(node, "Details/Description", 24, 190, 314, 230).GetComponent<Text>();
            description.fontSize = 17;
            description.alignment = TextAnchor.UpperLeft;
            description.resizeTextForBestFit = true;
            description.resizeTextMinSize = 14;
            description.resizeTextMaxSize = 17;
            ButtonAt(node, "Details/Buy", "购买", "coin", 24, 432, 314, 48, true);
            ButtonAt(node, "Details/Equip", "装备", "rod", 24, 432, 314, 48, true)
                .GetComponent<CozyButtonVisual>().SetTint(new Color(0.38f, 0.57f, 0.83f, 1));
            Hide(node, "Details/Retry", "Details/Reflow_DetailSeal", "Details/Reflow_Heading", "Details/Reflow_DescriptionViewport");
            Label(node, "Status", "", 254, 612, 1068, 26, 16);
        }

        static void Leaderboard(GameObject root)
        {
            var node = root.transform;
            Common(root, "钓友排行榜", "每一次收获，都值得被看见", "trophy");
            Hide(node, "Reflow_RankTableHeading", "Reflow_RankColumn", "Reflow_NameColumn", "Reflow_ValueColumn");
            Paper(node, "Reflow_PodiumPaper", 24, 112, 980, 284).transform.SetAsFirstSibling();
            Place(node, "Podium", 24, 112, 980, 284);
            for (var rank = 1; rank <= 3; rank++)
            {
                var x = rank == 2 ? 70 : rank == 1 ? 358 : 646;
                var slot = Place(node, "Podium/Slot" + rank, x, rank == 1 ? 0 : 14, 264, rank == 1 ? 280 : 266);
                Paper(slot, "DesignBacking", 0, 0, 264, rank == 1 ? 280 : 266).transform.SetAsFirstSibling();
                Label(slot, "Rank", rank.ToString(), 100, 8, 64, 40, 32, true).alignment = TextAnchor.MiddleCenter;
                Picture(slot, "Avatar", CatTheme.LoadCozy("characters/portraits/orange.png", Vector4.zero), 70, 52, 124, 124);
                Label(slot, "Nickname", "等待榜单", 16, 182, 232, 32, 23, true).alignment = TextAnchor.MiddleCenter;
                Label(slot, "Value", "—", 16, 220, 232, 36, 20).alignment = TextAnchor.MiddleCenter;
            }
            Paper(node, "Reflow_InfoRail", 1024, 112, 322, 496).transform.SetAsFirstSibling();
            Place(node, "Tabs", 1040, 128, 290, 248);
            var names = new[] { "Daily", "Weekly", "Pond", "Rare" };
            var titles = new[] { "今日大鱼", "每周钓王", "鱼塘榜", "珍稀榜" };
            for (var index = 0; index < names.Length; index++)
                ButtonAt(node, "Tabs/" + names[index], titles[index], index == 2 ? "pond" : "trophy", 0, index * 60, 290, 50);
            Label(node, "DesignRailHeading", "榜单说明", 1048, 404, 274, 34, 25, true);
            Label(node, "DesignRailBody", "以实际榜单数据为准\n\n切换分类，查看不同榜单", 1048, 454, 274, 120, 19);
            Paper(node, "DesignTableHeading", 24, 410, 980, 38);
            Label(node, "DesignRankColumn", "排名", 44, 412, 100, 34, 18);
            Label(node, "DesignNameColumn", "钓友", 164, 412, 410, 34, 18);
            Label(node, "DesignValueColumn", "榜单成绩", 650, 412, 330, 34, 18).alignment = TextAnchor.MiddleRight;
            Place(node, "Scroll", 24, 458, 980, 116);
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
            Label(profile, "DesignId", "", 20, 190, 630, 32, 16);
            Label(profile, "DesignStats", "", 20, 252, 630, 92, 24, true);
            Label(profile, "DesignPrivacy", "", 20, 374, 630, 80, 18);
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
                    background.sprite = CatTheme.LoadCozy("panels/paper.png", CatTheme.PaperBorder);
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

        static void ItemCard(GameObject root, bool shop)
        {
            var node = root.transform;
            Place(node, "", 0, 0, 214, 196);
            Picture(node, "DesignItemArt", null, 16, 10, 182, 112);
            Label(node, "Label", "", 12, 126, 190, shop ? 28 : 58, 21, true).alignment = TextAnchor.MiddleCenter;
            if (shop)
                Label(node, "Meta", "", 12, 158, 190, 28, 16).alignment = TextAnchor.MiddleCenter;
            Hide(node, "Icon", "CozyIcon");
            foreach (var name in new[] { "DesignItemArt", "Label", "Meta" })
            {
                var rect = node.Find(name) as RectTransform;
                if (rect == null) continue;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = new Vector2(name == "DesignItemArt" ? -32 : -24, rect.sizeDelta.y);
            }
            var element = root.GetComponent<LayoutElement>() ?? root.AddComponent<LayoutElement>();
            element.preferredHeight = 196;
            element.minHeight = 196;
            element.flexibleWidth = 0;
        }

        static void AchievementCard(GameObject root)
        {
            var node = root.transform;
            Place(node, "", 0, 0, 928, 100);
            Picture(node, "Reflow_AchievementSeal", CatTheme.LoadCozy("icons/medal.png", Vector4.zero), 18, 22, 56, 56);
            Label(node, "Label", "", 94, 14, 646, 72, 20);
            Label(node, "DesignState", "", 758, 26, 148, 48, 20, true).alignment = TextAnchor.MiddleCenter;
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

        static void Shell(GameObject root)
        {
            var node = root.transform;
            foreach (var page in new[] { "Leaderboard", "Profile", "Achievements", "Friends", "CatchBag", "Equipment", "Shop", "SocialFeed" })
                Place(node, "MainRoot/Content/" + page, 160, 150, 1370, 650);
            foreach (var name in new[] { "Mail", "Settings" })
            {
                var button = node.Find("MainRoot/Header/" + name);
                if (button == null) continue;
                Picture(button, "Icon", CatTheme.LoadCozy("icons/" + (name == "Mail" ? "bell" : "settings") + ".png", Vector4.zero), 16, 16, 38, 38);
                Hide(button, "Label");
            }
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
            foreach (var name in new[] { "PanelLeaderboard", "PanelProfileHub", "PanelAchievements", "PanelSocial", "PanelCatch", "PanelEquipment", "PanelShop", "PanelSocialFeed" })
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
