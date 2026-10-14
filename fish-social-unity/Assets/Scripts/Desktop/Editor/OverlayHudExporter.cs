#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using FishSocial.Desktop;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static class OverlayHudExporter
    {
        public const float CanvasWidth = 960f;
        public const float CanvasHeight = 560f;
        const string PrefabPath = DesktopPrefabCatalog.Folder + "/OverlayHud.prefab";

        static readonly string[] RequiredWidgetIds = OverlayHudWidgetCatalog.Required;

        [MenuItem("Fish Social/补齐 OverlayHud", false, 44)]
        public static void EnsureMenu()
        {
            try
            {
                CozyPrefabRepair.Ensure("OverlayHud", typeof(DesktopOverlayHudView));
                EditorUtility.DisplayDialog(
                    "补齐 OverlayHud",
                    "已补齐 OverlayHud.prefab（含 btn_pan_left / btn_pan_right）。\n" +
                    "接下来请执行：Fish Social → Export Overlay HUD。",
                    "确定");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("补齐 OverlayHud", "失败：" + ex.Message, "确定");
            }
        }

        [MenuItem("Fish Social/Export Overlay HUD", false, 45)]
        public static void ExportMenu()
        {
            var error = Export(out var outputDir);
            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Export Overlay HUD", error, "确定");
                return;
            }

            EditorUtility.DisplayDialog(
                "Export Overlay HUD",
                "已导出至：\n" + outputDir,
                "确定");
        }

        public static string Export(out string outputDir)
        {
            outputDir = ResolveHudOutputDir();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                return "找不到 OverlayHud.prefab，请先执行 Fish Social → 补齐 OverlayHud。";

            var rootGo = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (rootGo == null)
                return "无法加载 OverlayHud.prefab 进行导出。";

            try
            {
                return ExportLoadedPrefab(rootGo, outputDir);
            }
            catch (Exception ex)
            {
                return "Overlay HUD 导出失败：" + ex.Message;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rootGo);
            }
        }

        static string ExportLoadedPrefab(GameObject rootGo, string outputDir)
        {
            var root = rootGo.GetComponent<RectTransform>();
            if (root == null)
                return "OverlayHud 缺少 RectTransform。";

            var size = root.rect.size;
            if (Mathf.Abs(size.x - CanvasWidth) > 0.5f || Mathf.Abs(size.y - CanvasHeight) > 0.5f)
                return "OverlayHud 画布必须是 960×560，当前为 " + size.x + "×" + size.y + "。";

            // Layout Group / stretch children need a canvas pass before measuring.
            // Do not rewrite artist rects to catalog defaults.
            Canvas.ForceUpdateCanvases();

            var widgets = rootGo.GetComponentsInChildren<DesktopOverlayHudWidget>(true);
            if (widgets == null || widgets.Length == 0)
                return "OverlayHud 没有 DesktopOverlayHudWidget 子节点。";

            var byId = new Dictionary<string, DesktopOverlayHudWidget>(StringComparer.Ordinal);
            foreach (var widget in widgets)
            {
                if (widget == null || string.IsNullOrWhiteSpace(widget.widgetId))
                {
                    var name = widget != null ? widget.gameObject.name : "(null)";
                    return "存在未设置 widgetId 的 HUD 控件：" + name +
                           "。请执行 Fish Social → 补齐 OverlayHud 后重试。";
                }
                if (byId.ContainsKey(widget.widgetId))
                    return "重复的 widgetId：" + widget.widgetId;
                byId[widget.widgetId] = widget;
            }

            foreach (var required in RequiredWidgetIds)
            {
                if (!byId.ContainsKey(required))
                    return "缺少强制 widgetId：" + required;
            }

            Directory.CreateDirectory(outputDir);
            var fontsDir = ResolveContainedPath(outputDir, "fonts");
            Directory.CreateDirectory(fontsDir);
            var copiedSprites = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var copiedFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries = new List<string>();
            var components = LoadComponentTemplates(outputDir);
            var componentError = CopyComponentSprites(components, outputDir, copiedSprites);
            if (!string.IsNullOrEmpty(componentError))
                return componentError;

            foreach (var widget in widgets)
            {
                var rt = widget.GetComponent<RectTransform>();
                if (rt == null)
                    return "控件缺少 RectTransform：" + widget.widgetId;

                DesktopOverlayHudWidget parentWidget;
                var hasParent = TryGetParentWidget(widget, out parentWidget);
                GetWidgetLayout(
                    root,
                    rt,
                    hasParent ? parentWidget : null,
                    out var x,
                    out var y,
                    out var w,
                    out var h,
                    out var parentId);

                if (w <= 0.5f || h <= 0.5f)
                {
                    var abs = GetCanvasAbsoluteBounds(root, rt);
                    w = Mathf.Max(w, abs.width);
                    h = Mathf.Max(h, abs.height);
                }

                if (w <= 0.5f || h <= 0.5f)
                    return "控件尺寸无效（w/h 为 0）：" + widget.widgetId +
                           "。请在 Prefab 中检查 RectTransform，或执行 Fish Social → 补齐 OverlayHud。";

                var sprites = CopyWidgetSprites(widget, components, outputDir, copiedSprites, out var spriteError);
                if (!string.IsNullOrEmpty(spriteError))
                    return spriteError;

                var textStyle = ExtractTextStyle(widget, fontsDir, copiedFonts, out var textStyleError);
                if (!string.IsNullOrEmpty(textStyleError))
                    return textStyleError;

                entries.Add(BuildWidgetJson(
                    widget,
                    parentId,
                    x,
                    y,
                    w,
                    h,
                    sprites,
                    ExtractSpriteSlice(widget),
                    components,
                    textStyle));
            }

            var jsonPath = ResolveContainedPath(outputDir, "overlay-hud.json");
            var json = BuildDocumentJson(entries, components);
            WriteDocumentSafely(jsonPath, json);
            AssetDatabase.Refresh();
            Debug.Log("[OverlayHudExporter] Wrote " + jsonPath);
            return null;
        }

        internal static Transform FindHudTransform(Transform root, string widgetId)
        {
            if (root == null || string.IsNullOrWhiteSpace(widgetId))
                return null;
            if (IndexHudWidgets(root).TryGetValue(widgetId, out var widget))
                return widget.transform;

            // Fallback: GameObject name (covers widgets created with empty widgetId).
            foreach (var candidate in root.GetComponentsInChildren<DesktopOverlayHudWidget>(true))
            {
                if (candidate == null)
                    continue;
                if (string.Equals(candidate.gameObject.name, widgetId, StringComparison.Ordinal))
                    return candidate.transform;
            }

            var byName = root.Find(widgetId);
            return byName;
        }

        static Dictionary<string, DesktopOverlayHudWidget> IndexHudWidgets(Transform root)
        {
            var byId = new Dictionary<string, DesktopOverlayHudWidget>(StringComparer.Ordinal);
            foreach (var widget in root.GetComponentsInChildren<DesktopOverlayHudWidget>(true))
            {
                if (widget == null || string.IsNullOrWhiteSpace(widget.widgetId))
                    continue;
                byId[widget.widgetId] = widget;
            }
            return byId;
        }

        static bool TryGetParentWidget(
            DesktopOverlayHudWidget widget,
            out DesktopOverlayHudWidget parent)
        {
            parent = null;
            if (widget == null)
                return false;
            var current = widget.transform.parent;
            while (current != null)
            {
                var candidate = current.GetComponent<DesktopOverlayHudWidget>();
                if (candidate != null && candidate != widget)
                {
                    parent = candidate;
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        static void GetWidgetLayout(
            RectTransform root,
            RectTransform rt,
            DesktopOverlayHudWidget parentWidget,
            out float x,
            out float y,
            out float w,
            out float h,
            out string parentId)
        {
            parentId = null;
            w = ResolveHudSize(rt.rect.width, rt.sizeDelta.x);
            h = ResolveHudSize(rt.rect.height, rt.sizeDelta.y);

            if (UsesTopLeftLayout(rt))
            {
                x = rt.anchoredPosition.x;
                y = -rt.anchoredPosition.y;
                if (parentWidget != null)
                    parentId = parentWidget.widgetId;
                return;
            }

            var abs = GetCanvasAbsoluteBounds(root, rt);
            x = abs.x;
            y = abs.y;
            w = ResolveHudSize(abs.width, rt.sizeDelta.x);
            h = ResolveHudSize(abs.height, rt.sizeDelta.y);
            if (parentWidget == null)
                return;

            var parentRt = parentWidget.GetComponent<RectTransform>();
            var parentAbs = GetCanvasAbsoluteBounds(root, parentRt);
            parentId = parentWidget.widgetId;
            x = abs.x - parentAbs.x;
            y = abs.y - parentAbs.y;
        }

        static float ResolveHudSize(float rectSize, float sizeDelta)
        {
            if (rectSize > 0.5f)
                return rectSize;
            if (Mathf.Abs(sizeDelta) > 0.5f)
                return Mathf.Abs(sizeDelta);
            return 0f;
        }

        static Rect GetCanvasAbsoluteBounds(RectTransform canvas, RectTransform target)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, target);
            var x = bounds.min.x;
            var y = canvas.rect.height - bounds.max.y;
            return new Rect(x, y, bounds.size.x, bounds.size.y);
        }

        static bool UsesTopLeftLayout(RectTransform rt)
        {
            if (rt == null)
                return false;
            const float eps = 0.001f;
            return Mathf.Abs(rt.anchorMin.x) < eps &&
                   Mathf.Abs(rt.anchorMin.y - 1f) < eps &&
                   Mathf.Abs(rt.anchorMax.x) < eps &&
                   Mathf.Abs(rt.anchorMax.y - 1f) < eps &&
                   Mathf.Abs(rt.pivot.x) < eps &&
                   Mathf.Abs(rt.pivot.y - 1f) < eps;
        }

        struct HudSpriteExport
        {
            public string Normal;
            public string Hover;
            public string Pressed;
            public string Icon;
        }

        static Sprite VisualSprite(DesktopOverlayHudWidget widget, string propertyName)
        {
            var visual = widget.GetComponent<CozyButtonVisual>();
            if (visual == null)
                return null;
            var property = new SerializedObject(visual).FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as Sprite : null;
        }

        static Image VisualImage(DesktopOverlayHudWidget widget, string propertyName)
        {
            var visual = widget.GetComponent<CozyButtonVisual>();
            if (visual == null)
                return null;
            var property = new SerializedObject(visual).FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as Image : null;
        }

        static Image FindPlateImage(DesktopOverlayHudWidget widget)
        {
            return VisualImage(widget, "plate")
                ?? widget.transform.Find("CozyVisual")?.GetComponent<Image>()
                ?? widget.GetComponent<Image>();
        }

        static HudSpriteExport CopyWidgetSprites(
            DesktopOverlayHudWidget widget,
            HudComponents components,
            string outputDir,
            Dictionary<string, string> copiedSprites,
            out string error)
        {
            var result = new HudSpriteExport();
            var plate = FindPlateImage(widget);
            result.Normal = CopySpriteResource(widget.spriteFile,
                VisualSprite(widget, "normalSprite") ?? (plate != null ? plate.sprite : null),
                false, outputDir, copiedSprites, out error);
            if (error != null)
                return result;
            var button = widget.GetComponent<Button>();
            var hoverFile = string.IsNullOrWhiteSpace(widget.spriteHoverFile) && IsCozyPlate(result.Normal)
                ? components.button.spriteHover : widget.spriteHoverFile;
            result.Hover = CopySpriteResource(hoverFile,
                VisualSprite(widget, "hoverSprite") ?? (button != null ? button.spriteState.highlightedSprite : null),
                true, outputDir, copiedSprites, out error);
            if (error != null)
                return result;
            var pressedFile = string.IsNullOrWhiteSpace(widget.spritePressedFile) && IsCozyPlate(result.Normal)
                ? components.button.spritePressed : widget.spritePressedFile;
            result.Pressed = CopySpriteResource(pressedFile,
                VisualSprite(widget, "pressedSprite") ?? (button != null ? button.spriteState.pressedSprite : null),
                true, outputDir, copiedSprites, out error);
            if (error != null)
                return result;
            var icon = VisualImage(widget, "icon")
                ?? widget.transform.Find("Icon")?.GetComponent<Image>()
                ?? widget.transform.Find("CozyIcon")?.GetComponent<Image>();
            result.Icon = CopySpriteResource(widget.iconSpriteFile, icon != null ? icon.sprite : null,
                true, outputDir, copiedSprites, out error);
            return result;
        }

        /// <summary>
        /// Scoped declarations preserve their complete relative path and take precedence over
        /// transparent hit Images. Legacy filename-only normal sprites retain Image-first export.
        /// State/icon declarations remain independent of the plate and never mutate prefab fields.
        /// </summary>
        static string CopySpriteResource(
            string declaredFile,
            Sprite fallback,
            bool preferDeclared,
            string outputDir,
            Dictionary<string, string> copiedSprites,
            out string error)
        {
            error = null;
            var exportName = string.IsNullOrWhiteSpace(declaredFile) ? null : NormalizeHudRelativePath(declaredFile);
            var assetPath = fallback != null ? AssetDatabase.GetAssetPath(fallback) : null;
            string sourcePath = null;
            if (!string.IsNullOrEmpty(assetPath) &&
                (exportName == null || (!preferDeclared && exportName.IndexOf('/') < 0)))
            {
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var assetSource = ResolveContainedPath(projectRoot, assetPath);
                if (File.Exists(assetSource))
                {
                    sourcePath = assetSource;
                    exportName = HudPathForAsset(assetPath);
                }
            }
            if (exportName == null)
                return null;
            if (!string.Equals(Path.GetExtension(exportName), ".png", StringComparison.OrdinalIgnoreCase))
            {
                error = "HUD sprite 必须是 PNG：" + exportName;
                return null;
            }
            sourcePath = sourcePath ?? ResolveSpriteSourcePath(exportName, outputDir);
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                error = "找不到 HUD PNG：" + exportName + "（Cozy、OverlayHud、OverlayArt 或 OverlayResources/hud）";
                return null;
            }
            var target = ResolveContainedPath(outputDir, exportName);
            if (copiedSprites.TryGetValue(exportName, out var previousSource))
            {
                if (!SameFileContents(previousSource, sourcePath))
                {
                    error = "HUD 资源相对路径冲突：" + exportName;
                    return null;
                }
                return exportName;
            }
            if (!TryCopySpriteFile(sourcePath, target, out error))
                return null;
            copiedSprites[exportName] = sourcePath;
            return exportName;
        }

        static string HudPathForAsset(string assetPath)
        {
            var normalized = NormalizeHudRelativePath(assetPath);
            foreach (var prefix in new[]
            {
                "Assets/Resources/Desktop/Theme/Cozy/", "Assets/StreamingAssets/DesktopTheme/Cozy/", "Assets/UnityCozy/"
            })
            {
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return "cozy/" + normalized.Substring(prefix.Length);
            }
            foreach (var prefix in new[] { "Assets/StreamingAssets/OverlayHud/", "Assets/Desktop/OverlayArt/" })
            {
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return normalized.Substring(prefix.Length);
            }
            return Path.GetFileName(normalized);
        }

        /// <summary>
        /// Portable relative paths only. Normalizes separators without flattening directories;
        /// rejects parent/current segments, drive/UNC paths and Windows path aliases on every OS.
        /// </summary>
        static string NormalizeHudRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("HUD 资源路径不能为空");
            var normalized = value.Replace('\\', '/');
            if (Path.IsPathRooted(normalized) || normalized.StartsWith("/", StringComparison.Ordinal))
                throw new ArgumentException("HUD 资源不能使用绝对路径：" + value);
            foreach (var segment in normalized.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == ".." ||
                    segment.EndsWith(".", StringComparison.Ordinal) || segment.EndsWith(" ", StringComparison.Ordinal))
                    throw new ArgumentException("HUD 资源路径包含非法段：" + value);
                foreach (var character in segment)
                {
                    if (character < 32 || "<>:\"|?*".IndexOf(character) >= 0)
                        throw new ArgumentException("HUD 资源路径包含非法字符：" + value);
                }
                var stem = segment.Split('.')[0].ToUpperInvariant();
                if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" ||
                    (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) ||
                     stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] >= '1' && stem[3] <= '9'))
                    throw new ArgumentException("HUD 资源路径包含保留设备名：" + value);
            }
            return normalized;
        }

        static string ResolveContainedPath(string root, string relativePath)
        {
            var relative = NormalizeHudRelativePath(relativePath);
            var rootFull = Path.GetFullPath(root);
            var prefix = rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.Combine(rootFull, relative.Replace('/', Path.DirectorySeparatorChar)));
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!full.StartsWith(prefix, comparison))
                throw new ArgumentException("HUD 资源路径越界：" + relativePath);
            var current = rootFull;
            RejectReparsePoint(current);
            foreach (var segment in relative.Split('/'))
            {
                current = Path.Combine(current, segment);
                RejectReparsePoint(current);
            }
            return full;
        }

        static void RejectReparsePoint(string path)
        {
            if ((File.Exists(path) || Directory.Exists(path)) &&
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("HUD 资源路径不能经过符号链接/reparse point：" + path);
        }

        static string ResolveSpriteSourcePath(string spriteFile, string outputDir)
        {
            var relative = NormalizeHudRelativePath(spriteFile);
            if (relative.StartsWith("cozy/", StringComparison.OrdinalIgnoreCase))
            {
                var suffix = relative.Substring("cozy/".Length);
                foreach (var root in new[]
                {
                    Path.Combine(Application.dataPath, "Resources", "Desktop", "Theme", "Cozy"),
                    Path.Combine(Application.dataPath, "StreamingAssets", "DesktopTheme", "Cozy"),
                    Path.Combine(Application.dataPath, "UnityCozy")
                })
                {
                    var candidate = ResolveContainedPath(root, suffix);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            foreach (var root in new[]
            {
                Path.Combine(Application.dataPath, "StreamingAssets", "OverlayHud"),
                Path.Combine(Application.dataPath, "Desktop", "OverlayArt"),
                Path.Combine(repoRoot, "desktop-overlay", "OverlayResources", "hud"),
                outputDir
            })
            {
                var candidate = ResolveContainedPath(root, relative);
                if (File.Exists(candidate))
                    return candidate;
            }
            return null;
        }

        static bool SameFileContents(string first, string second)
        {
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), comparison))
                return true;
            if (new FileInfo(first).Length != new FileInfo(second).Length)
                return false;
            using (var algorithm = SHA256.Create())
            using (var firstStream = File.OpenRead(first))
            using (var secondStream = File.OpenRead(second))
                return Convert.ToBase64String(algorithm.ComputeHash(firstStream)) ==
                    Convert.ToBase64String(algorithm.ComputeHash(secondStream));
        }

        static void CommitTemporaryFile(string temporary, string destination)
        {
            RejectReparsePoint(destination);
            if (File.Exists(destination))
                File.Replace(temporary, destination, null);
            else
                File.Move(temporary, destination);
        }

        static bool TryCopySpriteFile(string sourcePath, string destPath, out string error)
        {
            error = null;
            string temporary = null;
            try
            {
                var source = Path.GetFullPath(sourcePath);
                var destination = Path.GetFullPath(destPath);
                var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (string.Equals(source, destination, comparison))
                    return true;
                Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? ".");
                temporary = destination + ".export-" + Guid.NewGuid().ToString("N") + ".tmp";
                File.Copy(source, temporary, false);
                CommitTemporaryFile(temporary, destination);
                return true;
            }
            catch (IOException ex)
            {
                error = "无法安全写入 HUD 资源（文件可能被占用）：" + destPath + " — " + ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = "无法写入 HUD 资源（权限）：" + destPath + " — " + ex.Message;
                return false;
            }
            finally
            {
                if (temporary != null && File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        static void WriteDocumentSafely(string destination, string json)
        {
            var temporary = destination + ".export-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                CommitTemporaryFile(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        struct HudTextStyleExport
        {
            public string FontFile;
            public int FontSize;
            public string FontColor;
            public string FontWeight;
            public string TextAlign;
            public string ContentAlign;
            public string VerticalAlign;
            public string Label;
            public bool ExportLabel;
            public bool HasStyle;
        }

        static HudTextStyleExport ExtractTextStyle(
            DesktopOverlayHudWidget widget,
            string fontsDir,
            HashSet<string> copiedFonts,
            out string error)
        {
            error = null;
            var kind = string.IsNullOrEmpty(widget.kind) ? "button" : widget.kind;
            var isButton = string.Equals(kind, "button", StringComparison.OrdinalIgnoreCase);
            var isText = string.Equals(kind, "text", StringComparison.OrdinalIgnoreCase);
            var isLogPanel = string.Equals(widget.widgetId, "chat_log", StringComparison.Ordinal);
            if (!isButton && !isText && !isLogPanel)
                return default;
            var text = FindWidgetText(widget);
            if (text == null)
            {
                if (isButton)
                {
                    return new HudTextStyleExport
                    {
                        Label = string.Empty,
                        ExportLabel = true,
                    };
                }

                return default;
            }

            var fontFile = CopyFontFile(text.font, fontsDir, copiedFonts, out error);
            if (!string.IsNullOrEmpty(error))
                return default;

            var align = MapTextAlign(text.alignment);
            var verticalAlign = MapVerticalAlign(text.alignment);
            return new HudTextStyleExport
            {
                FontFile = fontFile,
                FontSize = text.fontSize,
                FontColor = FormatColor(text.color),
                FontWeight = text.fontStyle == FontStyle.Bold ? "bold" : "normal",
                TextAlign = (isText || isLogPanel) ? align : null,
                ContentAlign = isButton ? align : null,
                VerticalAlign = verticalAlign,
                Label = (isButton || isText) ? (text.text ?? string.Empty) : null,
                ExportLabel = isButton || isText,
                HasStyle = true,
            };
        }

        static int[] ExtractSpriteSlice(DesktopOverlayHudWidget widget)
        {
            var image = FindPlateImage(widget);
            if (image == null || image.sprite == null || image.type != Image.Type.Sliced)
                return null;

            var border = image.sprite.border;
            var slice = new[]
            {
                Mathf.RoundToInt(border.x),
                Mathf.RoundToInt(border.y),
                Mathf.RoundToInt(border.z),
                Mathf.RoundToInt(border.w),
            };
            if (slice[0] <= 0 && slice[1] <= 0 && slice[2] <= 0 && slice[3] <= 0)
                return null;
            return slice;
        }

        static Text FindWidgetText(DesktopOverlayHudWidget widget)
        {
            if (widget == null)
                return null;
            return widget.GetComponentInChildren<Text>(true);
        }

        static string CopyFontFile(Font font, string fontsDir, HashSet<string> copiedFonts, out string error)
        {
            error = null;
            if (font == null)
            {
                error = "控件缺少字体";
                return null;
            }

            var sourcePath = ResolveFontSourcePath(font);
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                error = "找不到字体文件：" + font.name;
                return null;
            }

            var fileName = Path.GetFileName(sourcePath);
            var dest = ResolveContainedPath(fontsDir, fileName);
            if (!copiedFonts.Contains(fileName))
            {
                if (!TryCopySpriteFile(sourcePath, dest, out error))
                    return null;
                copiedFonts.Add(fileName);
            }
            else if (!SameFileContents(sourcePath, dest))
            {
                error = "HUD 字体文件名冲突：" + fileName;
                return null;
            }

            return fileName;
        }

        static string ResolveFontSourcePath(Font font)
        {
            var assetPath = AssetDatabase.GetAssetPath(font);
            if (!string.IsNullOrEmpty(assetPath) &&
                !assetPath.Contains("unity_builtin", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(assetPath))
                return assetPath;

            if (font.name.IndexOf("Arial", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var windowsFont = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                    "arial.ttf");
                if (File.Exists(windowsFont))
                    return windowsFont;
            }

            return null;
        }

        static string FormatColor(Color color)
        {
            var c = (Color32)color;
            return "#" + c.a.ToString("X2") + c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2");
        }

        static string MapTextAlign(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperCenter:
                case TextAnchor.MiddleCenter:
                case TextAnchor.LowerCenter:
                    return "center";
                case TextAnchor.UpperRight:
                case TextAnchor.MiddleRight:
                case TextAnchor.LowerRight:
                    return "right";
                default:
                    return "left";
            }
        }

        static string MapVerticalAlign(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                case TextAnchor.UpperCenter:
                case TextAnchor.UpperRight:
                    return "top";
                case TextAnchor.LowerLeft:
                case TextAnchor.LowerCenter:
                case TextAnchor.LowerRight:
                    return "bottom";
                default:
                    return "center";
            }
        }

        static void AppendTextStyleJson(StringBuilder sb, HudTextStyleExport style)
        {
            if (style.ExportLabel)
                sb.Append(",\"label\":\"").Append(Escape(style.Label ?? string.Empty)).Append("\"");

            if (!style.HasStyle)
                return;
            if (!string.IsNullOrEmpty(style.FontFile))
                sb.Append(",\"fontFile\":\"").Append(Escape(style.FontFile)).Append("\"");
            if (style.FontSize > 0)
                sb.Append(",\"fontSize\":").Append(style.FontSize);
            if (!string.IsNullOrEmpty(style.FontColor))
                sb.Append(",\"fontColor\":\"").Append(Escape(style.FontColor)).Append("\"");
            if (!string.IsNullOrEmpty(style.FontWeight))
                sb.Append(",\"fontWeight\":\"").Append(Escape(style.FontWeight)).Append("\"");
            if (!string.IsNullOrEmpty(style.TextAlign))
                sb.Append(",\"textAlign\":\"").Append(Escape(style.TextAlign)).Append("\"");
            if (!string.IsNullOrEmpty(style.ContentAlign))
                sb.Append(",\"contentAlign\":\"").Append(Escape(style.ContentAlign)).Append("\"");
            if (!string.IsNullOrEmpty(style.VerticalAlign))
            {
                sb.Append(",\"verticalAlign\":\"").Append(Escape(style.VerticalAlign)).Append("\"");
                if (!string.IsNullOrEmpty(style.ContentAlign))
                    sb.Append(",\"contentVAlign\":\"").Append(Escape(style.VerticalAlign)).Append("\"");
            }
        }

        static void AppendSpriteSliceJson(StringBuilder sb, int[] spriteSlice)
        {
            if (spriteSlice == null || spriteSlice.Length != 4)
                return;
            if (spriteSlice[0] <= 0 && spriteSlice[1] <= 0 && spriteSlice[2] <= 0 && spriteSlice[3] <= 0)
                return;
            sb.Append(",\"spriteSlice\":[")
                .Append(spriteSlice[0]).Append(",")
                .Append(spriteSlice[1]).Append(",")
                .Append(spriteSlice[2]).Append(",")
                .Append(spriteSlice[3]).Append("]");
        }

        static string BuildWidgetJson(
            DesktopOverlayHudWidget widget,
            string parentId,
            float x,
            float y,
            float w,
            float h,
            HudSpriteExport sprites,
            int[] spriteSlice,
            HudComponents components,
            HudTextStyleExport textStyle)
        {
            var sb = new StringBuilder();
            sb.Append("    {");
            sb.Append("\"id\":\"").Append(Escape(widget.widgetId)).Append("\",");
            if (!string.IsNullOrEmpty(parentId))
                sb.Append("\"parentId\":\"").Append(Escape(parentId)).Append("\",");
            sb.Append("\"kind\":\"").Append(Escape(string.IsNullOrEmpty(widget.kind) ? "button" : widget.kind)).Append("\",");
            sb.Append("\"x\":").Append(Mathf.Round(x)).Append(",");
            sb.Append("\"y\":").Append(Mathf.Round(y)).Append(",");
            sb.Append("\"w\":").Append(Mathf.Round(w)).Append(",");
            sb.Append("\"h\":").Append(Mathf.Round(h)).Append(",");
            sb.Append("\"z\":").Append(widget.zIndex).Append(",");
            sb.Append("\"visibleDefault\":").Append(widget.visibleDefault ? "true" : "false");
            AppendSpriteFileJson(sb, "sprite", sprites.Normal);
            AppendSpriteFileJson(sb, "spriteHover", sprites.Hover);
            AppendSpriteFileJson(sb, "spritePressed", sprites.Pressed);
            AppendSpriteFileJson(sb, "iconSprite", sprites.Icon);
            if (string.Equals(widget.kind, "button", StringComparison.OrdinalIgnoreCase))
            {
                var offset = widget.pressedOffsetY > 0 ? widget.pressedOffsetY : components.button.pressedOffsetY;
                var iconSize = widget.iconSize > 0 ? widget.iconSize : components.button.iconSize;
                RequirePositiveFinite(offset, "pressedOffsetY");
                RequirePositiveFinite(iconSize, "iconSize");
                sb.Append(",\"pressedOffsetY\":").Append(offset.ToString("0.###", CultureInfo.InvariantCulture));
                sb.Append(",\"iconSize\":").Append(iconSize.ToString("0.###", CultureInfo.InvariantCulture));
            }
            if (IsCozyPlate(sprites.Normal))
                spriteSlice = components.button.spriteSlice;
            else if (string.Equals(sprites.Normal, components.paper.sprite, StringComparison.OrdinalIgnoreCase))
                spriteSlice = components.paper.spriteSlice;
            else if (string.Equals(sprites.Normal, components.input.sprite, StringComparison.OrdinalIgnoreCase))
                spriteSlice = components.input.spriteSlice;
            AppendSpriteSliceJson(sb, spriteSlice);
            AppendTextStyleJson(sb, textStyle);
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Matches WPF components (slice order L/B/R/T, padding order L/T/R/B).
        /// Optional Cozy/data/overlay-components.json overrides these shared defaults.
        /// ExportContractChecks is a read-only Editor regression entry point; it writes no assets.
        /// </summary>
        [Serializable]
        sealed class HudComponents
        {
            public ButtonTemplate button = new ButtonTemplate();
            public PaperTemplate paper = new PaperTemplate();
            public PaperTemplate input = new PaperTemplate
            {
                sprite = "cozy/panels/paper-compact.png", spriteSlice = new int[] { 10, 10, 10, 10 },
                padding = new float[] { 8, 6, 8, 6 }
            };
            public ScrollTemplate scroll = new ScrollTemplate();
        }

        [Serializable]
        sealed class ButtonTemplate
        {
            public string sprite = "cozy/buttons/plate-default.png";
            public string spriteHover = "cozy/buttons/plate-hover.png";
            public string spritePressed = "cozy/buttons/plate-pressed.png";
            public int[] spriteSlice = { 10, 10, 10, 10 };
            public float pressedOffsetY = 3;
            public float iconSize = 16;
            public float disabledOpacity = 0.45f;
            public float[] padding = { 2, 0, 2, 0 };
        }

        [Serializable]
        sealed class PaperTemplate
        {
            public string sprite = "cozy/panels/paper.png";
            public int[] spriteSlice = { 20, 20, 20, 20 };
            public float[] padding = { 12, 8, 12, 8 };
        }

        [Serializable]
        sealed class ScrollTemplate
        {
            public float width = 6;
        }

        static HudComponents LoadComponentTemplates(string outputDir)
        {
            var components = new HudComponents();
            var source = ResolveSpriteSourcePath("cozy/data/overlay-components.json", outputDir);
            if (source != null)
                JsonUtility.FromJsonOverwrite(File.ReadAllText(source), components);
            if (components.button == null || components.paper == null || components.input == null || components.scroll == null)
                throw new InvalidDataException("Cozy components 必须包含 button/paper/input/scroll");
            ValidateComponentBorder(components.button.spriteSlice, 10);
            ValidateComponentBorder(components.paper.spriteSlice, 20);
            ValidateComponentBorder(components.input.spriteSlice, 10);
            ValidateComponentPadding(components.button.padding);
            ValidateComponentPadding(components.paper.padding);
            ValidateComponentPadding(components.input.padding);
            RequirePositiveFinite(components.button.pressedOffsetY, "components.button.pressedOffsetY");
            RequirePositiveFinite(components.button.iconSize, "components.button.iconSize");
            RequirePositiveFinite(components.scroll.width, "components.scroll.width");
            RequirePositiveFinite(components.button.disabledOpacity, "components.button.disabledOpacity");
            if (components.button.disabledOpacity > 1)
                throw new InvalidDataException("disabledOpacity 必须在 (0,1] 范围");
            return components;
        }

        static void RequirePositiveFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                throw new InvalidDataException(name + " 必须是有限正数");
        }

        static void ValidateComponentBorder(int[] border, int expected)
        {
            if (border == null || border.Length != 4)
                throw new InvalidDataException("Cozy spriteSlice 必须包含四个 " + expected + " 像素边距");
            foreach (var value in border)
                if (value != expected)
                    throw new InvalidDataException("Cozy spriteSlice 必须包含四个 " + expected + " 像素边距");
        }

        static void ValidateComponentPadding(float[] padding)
        {
            if (padding == null || padding.Length != 4)
                throw new InvalidDataException("Cozy padding 必须有四个值");
            foreach (var value in padding)
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                    throw new InvalidDataException("Cozy padding 必须是有限非负数");
        }

        static string CopyComponentSprites(HudComponents components, string outputDir, Dictionary<string, string> copiedSprites)
        {
            foreach (var path in new[]
            {
                components.button.sprite, components.button.spriteHover, components.button.spritePressed,
                components.paper.sprite, components.input.sprite
            })
            {
                if (string.IsNullOrWhiteSpace(path))
                    return "Cozy component 缺少 PNG 路径";
                CopySpriteResource(path, null, true, outputDir, copiedSprites, out var error);
                if (error != null)
                    return error;
            }
            return null;
        }

        static bool IsCozyPlate(string path)
        {
            return !string.IsNullOrEmpty(path) && path.StartsWith("cozy/buttons/", StringComparison.OrdinalIgnoreCase);
        }

        static void AppendSpriteFileJson(StringBuilder builder, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
                builder.Append(",\"").Append(key).Append("\":\"").Append(Escape(value)).Append("\"");
        }

        [MenuItem("Fish Social/Validate Overlay HUD Export Contract", false, 46)]
        public static void ExportContractChecks()
        {
            GameObject temporary = null;
            try
            {
                if (NormalizeHudRelativePath("cozy\\buttons\\plate-default.png") != "cozy/buttons/plate-default.png" ||
                    NormalizeHudRelativePath("图层 2.png") != "图层 2.png" ||
                    HudPathForAsset("Assets/Resources/Desktop/Theme/Cozy/icons-light/map.png") != "cozy/icons-light/map.png" ||
                    HudPathForAsset("Assets/StreamingAssets/OverlayHud/cozy/panels/paper.png") != "cozy/panels/paper.png")
                    throw new InvalidDataException("HUD path regression");
                foreach (var invalid in new[]
                {
                    "../escape.png", "cozy/../escape.png", "cozy/./icon.png", "/tmp/escape.png",
                    "C:\\escape.png", "\\\\server\\share\\escape.png", "cozy//icon.png", "cozy/CON.png", "cozy/alias./icon.png"
                })
                {
                    var rejected = false;
                    try { NormalizeHudRelativePath(invalid); }
                    catch (ArgumentException) { rejected = true; }
                    if (!rejected)
                        throw new InvalidDataException("HUD traversal not rejected: " + invalid);
                }
                var components = new HudComponents();
                var document = BuildDocumentJson(new List<string>(), components);
                if (!document.Contains("\"components\":") || !document.Contains("cozy/buttons/plate-hover.png") ||
                    !document.Contains("\"spriteSlice\":[10,10,10,10]") ||
                    !document.Contains("\"spriteSlice\":[20,20,20,20]") ||
                    FormatColor(new Color32(0x75, 0x47, 0x2E, 0xFF)) != "#FF75472E")
                    throw new InvalidDataException("HUD components/ARGB regression");
                temporary = new GameObject("OverlayHudExportContractCheck");
                var widget = temporary.AddComponent<DesktopOverlayHudWidget>();
                widget.widgetId = "chat_send";
                var sprites = new HudSpriteExport
                {
                    Normal = components.button.sprite, Hover = components.button.spriteHover,
                    Pressed = components.button.spritePressed, Icon = "cozy/icons-light/check.png"
                };
                var entry = BuildWidgetJson(widget, "dock_chat", 282, 120, 28, 28, sprites, null, components, default);
                foreach (var token in new[]
                {
                    "\"id\":\"chat_send\"", "\"parentId\":\"dock_chat\"", "\"x\":282", "\"y\":120",
                    "\"spriteHover\":\"cozy/buttons/plate-hover.png\"", "\"spritePressed\":\"cozy/buttons/plate-pressed.png\"",
                    "\"iconSprite\":\"cozy/icons-light/check.png\"", "\"pressedOffsetY\":3", "\"iconSize\":16"
                })
                    if (!entry.Contains(token))
                        throw new InvalidDataException("HUD widget JSON regression: " + token);
                Debug.Log("[OverlayHudExporter] Cozy export contract checks passed (no files/prefabs written).");
            }
            finally
            {
                if (temporary != null)
                    UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        static string BuildDocumentJson(List<string> widgets, HudComponents components)
        {
            var sb = new StringBuilder();
            sb.Append("{\"width\":960,\"height\":560,\"components\":");
            sb.Append(JsonUtility.ToJson(components));
            sb.Append(",\"widgets\":[");
            for (var i = 0; i < widgets.Count; i++)
            {
                if (i > 0)
                    sb.Append(",");
                sb.AppendLine();
                sb.Append(widgets[i]);
            }
            sb.AppendLine();
            sb.Append("]}");
            return sb.ToString();
        }

        static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        static string ResolveHudOutputDir()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            return Path.Combine(repoRoot, "desktop-overlay", "OverlayResources", "hud");
        }
    }
}
#endif
