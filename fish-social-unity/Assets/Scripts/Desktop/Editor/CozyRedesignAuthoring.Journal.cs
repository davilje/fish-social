#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        static void ApplyJournalRemainingPrefabs()
        {
            // Preserve the existing variants and business node contracts, including secondary UI.
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Overlay") && !path.EndsWith("/OverlayHud.prefab", StringComparison.Ordinal)) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ApplyJournal(root);
                    Record(root);
                    SaveJournalPrefab(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        static string AliasKey(string document)
        {
            var source = Regex.Match(document, @"m_CorrespondingSourceObject:\s*(?:\{\s*)?fileID:\s*(\d+),?\s*guid:\s*([a-f0-9]+)");
            var instance = Regex.Match(document, @"m_PrefabInstance:\s*(?:\{\s*)?fileID:\s*(\d+)");
            return source.Success && instance.Success ? source.Groups[1].Value + ":" + source.Groups[2].Value + ":" + instance.Groups[1].Value : null;
        }

        internal static void SaveJournalPrefab(GameObject root, string path)
        {
            // Keep one canonical alias per source object. Duplicate aliases can detach additions
            // or prevent nested overrides from applying even though their YAML still exists.
            var pattern = @"^--- !u!\d+ &(-?\d+) stripped\r?\n[\s\S]*?(?=^--- !u!|\z)";
            var originals = new System.Collections.Generic.Dictionary<string, Match>();
            foreach (Match doc in Regex.Matches(File.Exists(path) ? File.ReadAllText(path) : "", pattern, RegexOptions.Multiline))
            {
                var key = AliasKey(doc.Value);
                if (key != null) originals[key] = doc;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path, out var success);
            if (!success) throw new InvalidOperationException("Could not save journal prefab: " + path);
            var after = File.ReadAllText(path);
            var emitted = new System.Collections.Generic.HashSet<string>();
            var remap = new System.Collections.Generic.Dictionary<string, string>();
            foreach (Match doc in Regex.Matches(after, pattern, RegexOptions.Multiline))
            {
                var key = AliasKey(doc.Value);
                if (key == null) continue;
                emitted.Add(key);
                if (!originals.TryGetValue(key, out var original)) continue;
                var oldId = original.Groups[1].Value;
                var newId = doc.Groups[1].Value;
                if (oldId != newId) remap[newId] = oldId;
            }
            // Resolve all IDs against the original emitted text in one pass. Sequential replacement
            // corrupts mappings such as 1->2 and 2->3, or swaps, by rewriting earlier results again.
            var references = @"(?<header>^--- !u!\d+ &)(?<headerId>-?\d+)(?<suffix> stripped(?=\r?$))|(?<local>\{fileID:\s*)(?<localId>-?\d+)(?<close>\s*\})";
            after = Regex.Replace(after, references, match =>
            {
                var header = match.Groups["header"].Success;
                var id = match.Groups[header ? "headerId" : "localId"].Value;
                if (!remap.TryGetValue(id, out var canonical)) return match.Value;
                return header ? match.Groups["header"].Value + canonical + match.Groups["suffix"].Value
                    : match.Groups["local"].Value + canonical + match.Groups["close"].Value;
            }, RegexOptions.Multiline);
            foreach (var pair in originals)
            {
                if (emitted.Contains(pair.Key)) continue;
                var instanceId = pair.Key.Substring(pair.Key.LastIndexOf(':') + 1);
                if (Regex.IsMatch(after, @"^--- !u!1001 &" + instanceId + @"\r?$", RegexOptions.Multiline))
                    after += "\n" + pair.Value.Value;
            }
            after = Regex.Replace(after, @"[ \t]+\r?$", "", RegexOptions.Multiline);
            after = Regex.Replace(after, @"\n{3,}", "\n\n");
            if (after != File.ReadAllText(path))
            {
                File.WriteAllText(path, after);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
        }

        static Image JournalSurface(Transform root, string path, string surface,
            float x, float y, float w, float h)
        {
            var rect = Place(root, path, x, y, w, h);
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            CozyJournalTheme.ApplySurface(image, surface);
            image.raycastTarget = false;
            return image;
        }

        static Image JournalLayer(Transform parent, string name, string surface)
        {
            var rect = Node(parent, name);
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            CozyJournalTheme.ApplySurface(image, surface, true);
            image.raycastTarget = false;
            return image;
        }

        static bool IsIndex(Transform node)
        {
            var path = AnimationUtility.CalculateTransformPath(node, node.root);
            return path.StartsWith("Tabs/", StringComparison.Ordinal) || path.Contains("/Tabs/") || path.Contains("/Sidebar/") || path.Contains("SettingsTabs/")
                || node.name.StartsWith("Filter", StringComparison.Ordinal) || node.name.StartsWith("DesignCategory", StringComparison.Ordinal)
                || (node.parent != null && node.parent.name == "Header" && (node.name == "Public" || node.name == "Friends" || node.name == "My"));
        }

        static void ApplyJournal(GameObject root)
        {
            var node = root.transform;
            // Migrate old chrome only. Artwork, masks, filled bars and transparent hit targets retain semantics.
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null || image.type == Image.Type.Filled || image.GetComponent<Mask>() != null) continue;
                var path = AssetDatabase.GetAssetPath(image.sprite);
                if (!path.Contains("/Theme/Cozy/") || path.Contains("/journal/")) continue;
                var filename = Path.GetFileNameWithoutExtension(path);
                if (path.Contains("/panels/") && image.color.a > 0)
                {
                    var tint = image.color;
                    var wasEnabled = image.enabled;
                    var surface = filename == "window" ? "paper-page"
                        : image.name.IndexOf("Rail", StringComparison.OrdinalIgnoreCase) >= 0 ? "paper-rail"
                        : filename.Contains("track") ? "progress-track" : "paper-aside";
                    CozyJournalTheme.ApplySurface(image, "surfaces/" + surface);
                    image.enabled = wasEnabled;
                    // Keep meaningful tinted art wells and rules, never multiply the paper's cream twice.
                    if (image.name.Contains("ArtBacking") || image.name == "Rule") image.color = tint;
                }
            }
            foreach (var input in root.GetComponentsInChildren<InputField>(true)) CozyJournalTheme.ApplyInput(input);
            foreach (var label in root.GetComponentsInChildren<Text>(true)) label.fontStyle = FontStyle.Normal;
            foreach (var toggle in root.GetComponentsInChildren<Toggle>(true)) JournalToggle(toggle);
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                CatTheme.StyleButton(button);
                var visual = button.GetComponent<CozyButtonVisual>();
                if (visual == null) continue;
                if (IsIndex(button.transform))
                    visual.ConfigureSurface("surfaces/tab-index-default", "surfaces/tab-index-hover", null,
                        "surfaces/tab-index-selected", "surfaces/tab-index-disabled");
            }
            if (node.Find("DesignHeader") != null)
            {
                JournalPage(root);
                var title = node.Find("DesignHeader/Title")?.GetComponent<Text>();
                if (title != null) title.fontStyle = FontStyle.Normal;
            }
            switch (root.name)
            {
                case "DesktopShell": JournalShell(root); break;
                case "PanelPondSelection": JournalPondSelection(root); break;
                case "PanelWorldMap": JournalWorldMap(root); break;
                case "PanelSocialFeed": JournalFeed(root); break;
                case "PanelCatch": JournalInventory(root); break;
                case "PanelLeaderboard": JournalLeaderboard(root); break;
                case "LeaderboardRow": JournalRankRow(root); break;
                case "FriendRow": JournalFriendRow(root); break;
                case "PanelAchievements": JournalAchievements(root); break;
                case "WorldMapPondMarker": JournalMapMarker(root); break;
                case "CatchSlot": case "ShopItemCard": case "ShowcaseSlot": case "GallerySpeciesSlot":
                case "PondSelectionCard": case "AchievementRow": case "SocialPostCard":
                    if (root.name == "PondSelectionCard") JournalPondCard(root);
                    JournalCard(root);
                    if (root.name == "SocialPostCard") JournalComments(root);
                    if (root.name == "AchievementRow")
                    {
                        var label = node.Find("Label") as RectTransform;
                        Inset(label, 94, 14, 150, 14);
                        foreach (var name in new[] { "DesignStateBadge", "DesignState" })
                        {
                            var badge = node.Find(name) as RectTransform;
                            badge.anchorMin = badge.anchorMax = Vector2.one;
                            badge.pivot = Vector2.up;
                            badge.anchoredPosition = new Vector2(-144, -30);
                        }
                    }
                    break;
            }
            foreach (var label in root.GetComponentsInChildren<Text>(true)) label.fontStyle = FontStyle.Normal;
            ApplyVisualParity(root);
            ApplyDetailParity(root);
            ApplyPondParity(root);
            ApplyGalleryParity(root);
            ApplyInlineParity(root);
            ApplyPageMotionParity(root);
            ApplyToggleParity(root);
            CanonicalizeScrollContents(root);
            RemoveObsoleteLayouts(root);
            ApplyApprovedRefinements(root);
        }

        static void CanonicalizeScrollContents(GameObject root)
        {
            foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true))
            {
                var viewport=scroll.transform.Find("Viewport");
                if(viewport==null) continue;
                var actual=(viewport.Find("Slots") ?? viewport.Find("Species") ?? viewport.Find("Cards") ?? viewport.Find("List") ?? (scroll.content != null && scroll.content.IsChildOf(viewport) ? scroll.content : viewport.Find("Content"))) as RectTransform;
                if(actual==null) continue;
                scroll.viewport=viewport as RectTransform;
                scroll.content=actual;
                foreach(var path in new[]{"Content","Species","Slots","Viewport/Content"})
                {
                    var legacy=scroll.transform.Find(path);
                    if(legacy==null || legacy==actual || legacy.childCount>0) continue;
                    foreach(var group in legacy.GetComponents<LayoutGroup>()) UnityEngine.Object.DestroyImmediate(group);
                    foreach(var fit in legacy.GetComponents<ContentSizeFitter>()) UnityEngine.Object.DestroyImmediate(fit);
                    legacy.gameObject.SetActive(false);
                }
            }
        }

        static void RemoveObsoleteLayouts(GameObject root)
        {
            // Place() disables automatic ownership while the authored layout is assembled.
            // Keep only the owners explicitly re-enabled for dynamic grids/lists/scrolling text.
            // A disabled LayoutGroup still blocks another LayoutGroup via DisallowMultipleComponent.
            foreach (var group in root.GetComponentsInChildren<LayoutGroup>(true))
                if (!group.enabled) UnityEngine.Object.DestroyImmediate(group);
            foreach (var fitter in root.GetComponentsInChildren<ContentSizeFitter>(true))
                if (!fitter.enabled) UnityEngine.Object.DestroyImmediate(fitter);
        }

        static void JournalPage(GameObject root)
        {
            var node = root.transform;
            var background = root.GetComponent<Image>();
            if (background != null) CozyJournalTheme.ApplySurface(background, "surfaces/paper-page");
            var shadow = JournalLayer(node, "JournalShadow", "surfaces/paper-shadow");
            shadow.transform.SetAsFirstSibling();
            var edge = JournalLayer(node, "JournalEdge", "surfaces/paper-edge");
            edge.transform.SetSiblingIndex(1);
            var dots = JournalLayer(node, "JournalDots", "textures/paper-dots");
            dots.rectTransform.offsetMin = new Vector2(24, 24);
            dots.rectTransform.offsetMax = new Vector2(-24, -24);
            dots.transform.SetSiblingIndex(2);
            var grain = JournalLayer(node, "JournalGrain", "textures/paper-grain");
            grain.rectTransform.offsetMin = new Vector2(12, 12);
            grain.rectTransform.offsetMax = new Vector2(-12, -12);
            grain.material = Resources.Load<Material>(CozyJournalTheme.ResourceRoot + "materials/PaperMultiply");
            grain.color = new Color(1, 1, 1, .32f);
            grain.type = Image.Type.Tiled;
            grain.transform.SetSiblingIndex(3);
            for (var i = 0; i < 3; i++)
            {
                var ring = JournalSurface(node, "JournalRing" + i, "decor/binder-ring", -24, 130 + 178 * i, 54, 24);
                ring.transform.SetAsLastSibling();
            }
            var header = node.Find("DesignHeader");
            var headerImage = header.GetComponent<Image>();
            if (headerImage != null) headerImage.enabled = false;
            var title = header.Find("Title")?.GetComponent<Text>();
            var underline = JournalSurface(header, "JournalTitleUnderline", "surfaces/progress-fill", 96, 57,
                title != null ? Mathf.Min(280, Mathf.Max(68, title.text.Length * 34)) : 140, 14);
            underline.color = new Color(1, .75f, .35f, .45f);
            underline.transform.SetAsFirstSibling();
            var sub = header.Find("Subtitle")?.GetComponent<Text>();
            if (sub != null) sub.color = new Color(CatTheme.Ink.r, CatTheme.Ink.g, CatTheme.Ink.b, .68f);
        }

        static void JournalCard(GameObject root)
        {
            var visual = root.GetComponent<CozyButtonVisual>();
            if (visual != null)
                visual.ConfigureSurface("surfaces/sticker-card", "surfaces/sticker-card", null, "surfaces/sticker-card");
            else
            {
                var image = root.GetComponent<Image>();
                if (image != null) CozyJournalTheme.ApplySurface(image, "surfaces/sticker-card");
            }
            var tape = JournalSurface(root.transform, "JournalTape", "decor/tape-pink-stripe", 62, -9, 84, 26);
            tape.rectTransform.anchorMin = tape.rectTransform.anchorMax = new Vector2(.5f, 1);
            tape.rectTransform.anchoredPosition = new Vector2(-42, 9);
            tape.rectTransform.localEulerAngles = new Vector3(0, 0, -4);
            tape.color = new Color(1, 1, 1, .8f);
            tape.transform.SetAsLastSibling();
            if (visual != null)
            {
                var outline = JournalLayer(root.transform, "JournalSelection", "surfaces/selection-outline");
                outline.transform.SetAsLastSibling();
                var state = root.GetComponent<CozyJournalCardVisual>() ?? root.AddComponent<CozyJournalCardVisual>();
                state.Configure(visual, outline.gameObject, tape.gameObject);
            }
        }

        static void JournalShell(GameObject root)
        {
            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.referenceResolution = CozyJournalTheme.DesignResolution;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = .5f;
            }
            var header = root.transform.Find("MainRoot/Header");
            if (header == null) return;
            ButtonAt(header, "WorldMapEntry", "钓鱼世界", "map", 474, 29, 282, 88);
            Label(header, "WorldMapEntry/Label", "钓鱼世界", 72, 10, 194, 36, 24, true);
            Label(header, "WorldMapEntry/Hint", "探索地图 · 选择鱼塘", 72, 46, 194, 28, 15).gameObject.SetActive(true);
            header.Find("WorldMapEntry").GetComponent<CozyButtonVisual>()
                .ConfigureSurface("surfaces/world-entry-plate");
            Place(header, "ProfileCard", 33, 29, 415, 103);
            Place(header, "FishCoins", 983, 29, 235, 64);
            Place(header, "CatchCount", 1237, 29, 216, 64);
            foreach (var name in new[] { "ProfileCard", "FishCoins", "CatchCount" })
            {
                var target = header.Find(name);
                var visual = target.GetComponent<CozyButtonVisual>();
                if (visual != null) visual.ConfigureSurface("surfaces/header-pill");
                else
                {
                    var image = target.GetComponent<Image>();
                    if (image != null) { image.sprite = null; image.color = Color.clear; }
                    JournalLayer(target, "JournalPlate", "surfaces/header-pill").transform.SetAsFirstSibling();
                }
            }
            Place(header, "Mail", 1478, 29, 72, 64);
            Place(header, "Settings", 1570, 29, 72, 64);
            Hide(header, "Leaderboard", "Home");
            var badge = header.Find("ProfileCard/LevelBadge")?.GetComponent<Image>();
            if (badge != null) { CozyJournalTheme.ApplySurface(badge, "surfaces/category-chip"); badge.color = new Color(.47f,.78f,.42f); }
            DesktopModalUi.ConfigureXpBar(header.Find("ProfileCard/XpBackground")?.GetComponent<Image>(), DesktopModalUi.FindXpFill(header));
            var nav = root.transform.Find("MainRoot/Nav");
            if (nav != null)
            {
                var bg = nav.GetComponent<Image>();
                if (bg != null) CozyJournalTheme.ApplySurface(bg, "surfaces/dock-board");
            }
            CozyHomeRoom.Build(root.transform.Find("MainRoot"));
            CozyMotion.EnsureBobber(nav);
            var toast = root.transform.Find("Toast")?.GetComponent<Image>();
            if (toast != null) CozyJournalTheme.ApplySurface(toast, "surfaces/toast-note");
        }

        static void JournalPondDetails(Transform root, string prefix, string body, string enter)
        {
            Hide(root, prefix + "/Heading", prefix + "/Reflow_Heading");
            JournalSurface(root, prefix + "/PhotoFrame", "surfaces/polaroid-frame", 18, 18, 326, 194);
            Picture(root, prefix + "/Photo", CozyJournalTheme.Sprite("decor/missing-image"), 28, 28, 306, 166);
            Label(root, prefix + "/Name", "选择一处鱼塘", 24, 220, 314, 36, 24, true);
            var old = root.Find(prefix + "/" + body);
            if (old == null && root.Find(prefix + "/FactsScroll/" + body) == null)
                Label(root, prefix + "/" + body, "", 24, 264, 314, 80, 17);
            ScrollingText(root, prefix + "/FactsScroll", prefix + "/" + body, 24, 264, 314, 80);
            Label(root, prefix + "/FeeSummary", "", 24, 346, 314, 32, 16);
            ButtonAt(root, prefix + "/ModeSell", "仅出售", null, 24, 384, 150, 42);
            ButtonAt(root, prefix + "/ModeAuto", "自动回鱼", null, 188, 384, 150, 42);
            ButtonAt(root, prefix + "/" + enter, "进入鱼塘", "pond", 24, 438, 314, 42, true);
            Label(root, prefix + "/StatusText", "", 24, 480, 314, 16, 12);
            foreach (var mode in new[] { "ModeSell", "ModeAuto" })
            {
                var label = root.Find(prefix + "/" + mode + "/Label").GetComponent<Text>();
                label.fontSize = 16;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 12;
                label.resizeTextMaxSize = 16;
            }
        }

        static void JournalPondSelection(GameObject root)
        {
            var node = root.transform;
            JournalPondDetails(node, "DesignPondAside", "Body", "Enter");
            Hide(node, "DesignPondRail/Heading", "DesignPondRail/Body", "DesignPondRail/Tip");
            ButtonAt(node, "FilterAll", "全部鱼塘", "pond", 34, 124, 190, 76);
            ButtonAt(node, "FilterAccessible", "可进入", "map", 34, 212, 190, 76);
            ButtonAt(node, "FilterFree", "免费鱼塘", "fish", 34, 300, 190, 76);
            Label(node, "ResultCount", "正在读取鱼塘", 44, 414, 170, 38, 18);
            ButtonAt(node, "ClearFilters", "清除筛选", null, 44, 466, 170, 42);
            // Keep the established binding path while locating this action outside the fixed entry controls.
            ButtonAt(node, "DesignPondAside/Locate", "地图查看", "map", -940, 424, 170, 42);
            ButtonAt(node, "DesignPondAside/Retry", "重试", null, -940, 370, 170, 38).gameObject.SetActive(false);
            node.Find("DesignPondAside/Locate/Label").GetComponent<Text>().fontSize = 18;
            var search = Place(node, "Search", 254, 112, 710, 42);
            var image = search.GetComponent<Image>() ?? search.gameObject.AddComponent<Image>();
            image.raycastTarget = true;
            var input = search.GetComponent<InputField>() ?? search.gameObject.AddComponent<InputField>();
            input.textComponent = Label(search, "Text", "", 16, 0, 678, 42, 18);
            input.placeholder = Label(search, "Placeholder", "搜索鱼塘名称、分类或水域", 16, 0, 678, 42, 18);
            input.placeholder.color = new Color(CatTheme.Ink.r, CatTheme.Ink.g, CatTheme.Ink.b, .5f);
            input.targetGraphic = image;
            input.characterLimit = 80;
            CozyJournalTheme.ApplyInput(input);
            Place(node, "PondCardScroll", 254, 164, 710, 432);
            foreach (var button in new[] { "FilterAll", "FilterAccessible", "FilterFree" })
                node.Find(button).GetComponent<CozyButtonVisual>().ConfigureSurface("surfaces/tab-index-default",
                    "surfaces/tab-index-hover", null, "surfaces/tab-index-selected", "surfaces/tab-index-disabled");
        }

        static void JournalWorldMap(GameObject root)
        {
            JournalPondDetails(root.transform, "Details", "DetailsText", "EnterPond");
            Hide(root.transform, "Details/DesignHeading", "Details/DesignHint");
            ButtonAt(root.transform, "Details/ResetView", "重置视图", null, -940, 424, 170, 42);
            ButtonAt(root.transform, "ListPonds", "列表选塘", "pond", 44, 470, 170, 42);
            ButtonAt(root.transform, "Details/Retry", "重试", null, -940, 306, 170, 38).gameObject.SetActive(false);
            Hide(root.transform, "DesignMapRail/Tip");
            Label(root.transform, "DesignMapRail/Body", "野外 · 水库\n进阶 · 老手\n巨物 · 禁区\n\n锁标记：尚未解锁\n拖动浏览地图\n滚轮缩放", 22, 72, 166, 246, 18);
            JournalSurface(root.transform, "JournalMapTapeL", "decor/tape-gold-dots", 268, 151, 104, 28).rectTransform.localEulerAngles = new Vector3(0, 0, 12);
            JournalSurface(root.transform, "JournalMapTapeR", "decor/tape-blue-stripe", 844, 151, 104, 28).rectTransform.localEulerAngles = new Vector3(0, 0, -8);
        }

        static void JournalFeed(GameObject root)
        {
            var node = root.transform;
            ButtonAt(node, "Header/My", "我的动态", "user", 0, 176, 190, 76);
            Hide(node, "DesignFeedInfo/Body", "DesignFeedInfo/Note");
            Label(node, "DesignFeedInfo/DetailTitle", "选择动态查看评论", 20, 58, 322, 70, 20);
            Place(node, "DesignFeedInfo/CommentsHost", 20, 144, 322, 282);
            ButtonAt(node, "DesignFeedInfo/Share", "前往背包分享", "social", 24, 438, 314, 42, true);
        }

        static void JournalPondCard(GameObject root)
        {
            var node = root.transform;
            Place(node, "", 0, 0, 678, 206);
            var hit = root.GetComponent<Image>() ?? root.AddComponent<Image>();
            hit.raycastTarget = true;
            var select = root.GetComponent<Button>() ?? root.AddComponent<Button>();
            select.targetGraphic = hit;
            CatTheme.StyleButton(select);
            Hide(node, "DesignPaper");
            var element = root.GetComponent<LayoutElement>();
            if (element != null) element.minHeight = element.preferredHeight = 206;
            Hide(node, "DesignIconPaper");
            Hide(node, "Level");
            Picture(node, "Photo", CozyJournalTheme.Sprite("decor/missing-image"), 14, 18, 190, 142);
            Picture(node, "PondIcon", CatTheme.LoadCozy("icons/pond.png", Vector4.zero), 18, 166, 24, 24);
            Label(node, "Name", "", 224, 14, 432, 36, 24, true);
            Label(node, "Description", "", 224, 54, 432, 52, 17);
            Label(node, "Fee", "", 224, 110, 432, 42, 17);
            var access = Label(node, "Access", "", 48, 166, 270, 26, 16);
            access.gameObject.SetActive(true);
            ButtonAt(node, "FocusOnMap", "地图查看", "map", 450, 154, 206, 38);
        }

        static void JournalMapMarker(GameObject root)
        {
            var node = root.transform;
            Place(node, "", 0, 0, 64, 64);
            var image = root.GetComponent<Image>();
            if (image != null) { image.sprite = null; image.color = Color.clear; image.raycastTarget = true; }
            var visual = root.GetComponent<CozyButtonVisual>();
            if (visual != null) visual.enabled = false;
            Hide(node, "CozyVisual");
            Picture(node, "Icon", CozyJournalTheme.Sprite("motion/bobber"), 18, 8, 28, 44);
            Label(node, "Label", "", -44, 60, 152, 38, 16, true).alignment = TextAnchor.MiddleCenter;
            Picture(node, "Paw", CozyJournalTheme.Sprite("decor/paw-mark"), 24, 48, 16, 16);
        }

        static void JournalComments(GameObject root)
        {
            var panel = root.transform.Find("CommentsPanel") as RectTransform;
            if (panel == null) return;
            foreach (var group in panel.GetComponents<LayoutGroup>()) group.enabled = false;
            var viewport = Node(panel, "CommentsViewport");
            Inset(viewport, 0, 0, 0, 96);
            var background = viewport.GetComponent<Image>() ?? viewport.gameObject.AddComponent<Image>();
            background.color = Color.clear;
            background.raycastTarget = true;
            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
            var content = (panel.Find("CommentsContent") ?? viewport.Find("CommentsContent")) as RectTransform;
            if (content == null) throw new InvalidOperationException("Social comments content missing");
            content.SetParent(viewport, false);
            content.anchorMin = Vector2.up; content.anchorMax = Vector2.one;
            content.pivot = Vector2.up; content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.enabled = true; layout.spacing = 8; layout.childControlHeight = true; layout.childControlWidth = true;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.GetComponent<ScrollRect>() ?? viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = viewport;
            scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 24;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            foreach (var name in new[] { "CommentInput", "SendButton" })
            {
                var rect = panel.Find(name) as RectTransform;
                if (rect == null) continue;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.right; rect.pivot = Vector2.zero;
                rect.offsetMin = new Vector2(0, name == "CommentInput" ? 44 : 0);
                rect.offsetMax = new Vector2(0, name == "CommentInput" ? 88 : 36);
                foreach (var label in rect.GetComponentsInChildren<Text>(true))
                {
                    Inset(label.rectTransform, 8, 4, 8, 4);
                    label.fontSize = 16;
                }
            }
        }

        static void JournalInventory(GameObject root)
        {
            var node = root.transform;
            node.Find("DesignCategoryFish").GetComponent<Button>().interactable = true;
            ButtonAt(node, "DesignCategoryItems", "道具", "bag", 34, 212, 190, 76);
            node.Find("DesignCategoryItems/Label").GetComponent<Text>().fontStyle = FontStyle.Normal;
            Place(node, "DesignBagNote", 44, 320, 170, 142);
            var obsolete = node.Find("EquipItem");
            if (obsolete != null) UnityEngine.Object.DestroyImmediate(obsolete.gameObject);
            Label(node, "BagItemsEmpty", "道具袋空空的，小猫在里面睡着了。", 254, 320, 1092, 100, 22)
                .alignment = TextAnchor.MiddleCenter;
            node.Find("BagItemsEmpty").gameObject.SetActive(false);
        }

        static void JournalLeaderboard(GameObject root)
        {
            var node = root.transform;
            Hide(node, "DesignRailHeading", "DesignRailBody");
            JournalSurface(node, "Reflow_InfoRail", "surfaces/paper-rail", 24, 112, 210, 496).transform.SetSiblingIndex(4);
            Place(node, "Tabs", 34, 124, 190, 352);
            var names = new[] { "Daily", "Weekly", "Pond", "Rare" };
            var titles = new[] { "今日榜", "本周榜", "鱼塘榜", "稀有鱼榜" };
            for (var i = 0; i < names.Length; i++) ButtonAt(node, "Tabs/" + names[i], titles[i], "medal", 0, i * 88, 190, 76);
            Place(node, "Reflow_PodiumPaper", 254, 112, 710, 168);
            Place(node, "Podium", 254, 112, 710, 168);
            for (var rank = 1; rank <= 3; rank++)
            {
                var x = rank == 2 ? 10 : rank == 1 ? 252 : 494;
                var slot = Place(node, "Podium/Slot" + rank, x, rank == 1 ? 0 : 12, 206, rank == 1 ? 164 : 152);
                Place(slot, "DesignBacking", 0, 0, 206, rank == 1 ? 164 : 152);
                Place(slot, "Rank", 8, 2, 190, 26);
                Place(slot, "Avatar", 67, 28, 72, 72);
                Place(slot, "Nickname", 8, 102, 190, 22);
                Place(slot, "Value", 8, 124, 190, 22);
                var hit = slot.GetComponent<Image>() ?? slot.gameObject.AddComponent<Image>();
                hit.color = Color.clear; hit.raycastTarget = true;
                var button = slot.GetComponent<Button>() ?? slot.gameObject.AddComponent<Button>();
                button.targetGraphic = hit; button.transition = Selectable.Transition.None;
            }
            Place(node, "DesignTableHeading", 254, 288, 710, 34);
            Place(node, "DesignRankColumn", 270, 288, 70, 34);
            Place(node, "DesignNameColumn", 370, 288, 310, 34);
            Place(node, "DesignValueColumn", 700, 288, 244, 34);
            Place(node, "Scroll", 254, 328, 710, 244);
            Place(node, "MyRank", 270, 580, 678, 28);
            Place(node, "Status", 254, 614, 1092, 24);
            JournalSurface(node, "DesignPlayerCard", "surfaces/paper-aside", 984, 112, 362, 496);
            Label(node, "DesignPlayerCard/Heading", "钓友档案", 24, 16, 314, 36, 24, true);
            Picture(node, "DesignPlayerCard/Avatar", DesktopDefaultAvatars.Portrait("orange"), 121, 76, 120, 120);
            Label(node, "DesignPlayerCard/Name", "选择一位钓友", 24, 220, 314, 38, 24, true).alignment = TextAnchor.MiddleCenter;
            Label(node, "DesignPlayerCard/Value", "", 24, 266, 314, 32, 21).alignment = TextAnchor.MiddleCenter;
            Label(node, "DesignPlayerCard/Bio", "查看榜单上的钓友与收获", 24, 314, 314, 104, 18);
            ButtonAt(node, "DesignPlayerCard/OpenProfile", "查看资料", "user", 72, 428, 218, 42, true);
        }

        static void JournalFriendRow(GameObject root)
        {
            // Legacy source has dangling nested additions. Materialize the existing binding contract.
            var node = root.transform;
            var layout = root.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) layout.enabled = false;
            var label = Label(node, "Name", "", 14, 8, 338, 38, 16);
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMax = new Vector2(-14, -8);
            label.rectTransform.offsetMin = new Vector2(14, -46);
            ButtonAt(node, "Dm", "私聊", "social", 14, 54, 156, 38);
            ButtonAt(node, "Remove", "移除好友", null, 184, 54, 168, 38);
        }

        static void JournalRankRow(GameObject root)
        {
            var node = root.transform;
            Place(node, "", 0, 0, 678, 54);
            Place(node, "Rank", 12, 8, 52, 38);
            Place(node, "Avatar", 74, 7, 40, 40);
            Place(node, "Nickname", 126, 8, 290, 38);
            Place(node, "Value", 430, 8, 232, 38);
            var hit = root.GetComponent<Image>() ?? root.AddComponent<Image>();
            CozyJournalTheme.ApplySurface(hit, "surfaces/sticker-card"); hit.raycastTarget = true;
            var button = root.GetComponent<Button>() ?? root.AddComponent<Button>();
            button.targetGraphic = hit;
        }

        static void JournalAchievements(GameObject root)
        {
            var node = root.transform;
            JournalSurface(node, "JournalAchievementRail", "surfaces/paper-rail", 24, 112, 210, 496);
            Place(node, "DesignAchievementCategories", 0, 0, 1370, 650);
            var names = new[] { "All", "Fishing", "Collection", "Progress" };
            var titles = new[] { "全部成就", "钓鱼", "收集", "成长" };
            for (var i = 0; i < names.Length; i++)
                ButtonAt(node, "DesignAchievementCategories/" + names[i], titles[i], "medal", 34, 124 + i * 88, 190, 76)
                    .GetComponent<CozyButtonVisual>().ConfigureSurface("surfaces/tab-index-default", "surfaces/tab-index-hover", null, "surfaces/tab-index-selected");
            Place(node, "Shell/Content", 254, 112, 710, 496);
            var tab = node.Find("Shell/Content/Tab_成就");
            Place(tab, "DesignHeading", 8, 8, 140, 38);
            Place(tab, "DesignFilterAll", 156, 4, 164, 48);
            Place(tab, "DesignFilterCompleted", 334, 4, 164, 48);
            Place(tab, "DesignFilterPending", 512, 4, 164, 48);
            Place(tab, "Achievements", 8, 64, 694, 416);
            Place(node, "DesignAchievementSummary", 984, 112, 362, 496);
        }

        static void JournalToggle(Toggle toggle)
        {
            var node = toggle.transform;
            if (toggle.graphic != null) toggle.graphic.gameObject.SetActive(false);
            var original = toggle.targetGraphic;
            if (original != null && original.transform != node && original.name != "JournalTrack") original.gameObject.SetActive(false);
            var track = JournalSurface(node, "JournalTrack", "surfaces/toggle-track", 0, 0, 76, 40);
            track.rectTransform.anchorMin = track.rectTransform.anchorMax = Vector2.one;
            track.rectTransform.anchoredPosition = new Vector2(-88, -4);
            track.raycastTarget = true;
            var knob = JournalSurface(track.transform, "JournalKnob", "decor/toggle-knob", 4, 4, 32, 32);
            toggle.graphic = null;
            toggle.targetGraphic = track;
            var component = toggle.GetComponent<CozyJournalToggle>() ?? toggle.gameObject.AddComponent<CozyJournalToggle>();
            component.Configure(toggle, track, knob.rectTransform);
            foreach (var label in toggle.GetComponentsInChildren<Text>(true))
            {
                label.font = AssetFont(); label.color = CatTheme.Ink;
                Inset(label.rectTransform, 12, 0, 104, 0);
                label.alignment = TextAnchor.MiddleLeft;
            }
        }
    }
}
#endif
