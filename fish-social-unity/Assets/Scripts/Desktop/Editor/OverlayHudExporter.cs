#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
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
                return "找不到 OverlayHud.prefab，请先执行 DesktopPrefabCatalog → OverlayHud。";

            var rootGo = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (rootGo == null)
                return "无法加载 OverlayHud.prefab 进行导出。";

            try
            {
                return ExportLoadedPrefab(rootGo, outputDir);
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

            RepairInvalidWidgetRects(rootGo.transform);

            var widgets = rootGo.GetComponentsInChildren<DesktopOverlayHudWidget>(true);
            if (widgets == null || widgets.Length == 0)
                return "OverlayHud 没有 DesktopOverlayHudWidget 子节点。";

            var byId = new Dictionary<string, DesktopOverlayHudWidget>(StringComparer.Ordinal);
            foreach (var widget in widgets)
            {
                if (widget == null || string.IsNullOrWhiteSpace(widget.widgetId))
                    return "存在未设置 widgetId 的 HUD 控件。";
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
            var fontsDir = Path.Combine(outputDir, "fonts");
            Directory.CreateDirectory(fontsDir);
            var copiedSprites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var copiedFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries = new List<string>();

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
                           "。请在 Prefab 中检查 RectTransform，或执行 DesktopPrefabCatalog → OverlayHud。";

                var spriteName = CopySprite(widget, outputDir, copiedSprites, out var spriteError);
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
                    spriteName,
                    textStyle));
            }

            var jsonPath = Path.Combine(outputDir, "overlay-hud.json");
            var json = BuildDocumentJson(entries);
            File.WriteAllText(jsonPath, json, new UTF8Encoding(false));
            PrefabUtility.SaveAsPrefabAsset(rootGo, PrefabPath);
            AssetDatabase.Refresh();
            Debug.Log("[OverlayHudExporter] Wrote " + jsonPath);
            return null;
        }

        static void RepairInvalidWidgetRects(Transform root)
        {
            var byId = IndexHudWidgets(root);
            var repaired = 0;
            foreach (var spec in OverlayHudWidgetCatalog.All)
            {
                if (!byId.TryGetValue(spec.Id, out var widget))
                    continue;
                var rt = widget.GetComponent<RectTransform>();
                if (rt == null)
                    continue;

                var w = ResolveHudSize(rt.rect.width, rt.sizeDelta.x);
                var h = ResolveHudSize(rt.rect.height, rt.sizeDelta.y);
                if (!NeedsHudRectRepair(rt, w, h))
                    continue;

                PlaceOverlayHudRect(rt, spec.X, spec.Y, spec.W, spec.H);
                repaired++;
                Debug.LogWarning(
                    "[OverlayHudExporter] 已修复无效尺寸/锚点：" + spec.Id +
                    " → " + spec.W + "×" + spec.H);
            }

            if (repaired > 0)
                Canvas.ForceUpdateCanvases();
        }

        internal static Transform FindHudTransform(Transform root, string widgetId)
        {
            if (root == null || string.IsNullOrWhiteSpace(widgetId))
                return null;
            if (IndexHudWidgets(root).TryGetValue(widgetId, out var widget))
                return widget.transform;
            return null;
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

        static bool NeedsHudRectRepair(RectTransform rt, float w, float h)
        {
            if (w <= 0.5f || h <= 0.5f)
                return true;
            return !UsesTopLeftLayout(rt);
        }

        static void PlaceOverlayHudRect(RectTransform rt, float x, float y, float w, float h)
        {
            if (rt == null)
                return;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
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

        static string CopySprite(
            DesktopOverlayHudWidget widget,
            string outputDir,
            HashSet<string> copiedSprites,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(widget.spriteFile))
            {
                var image = widget.GetComponent<Image>();
                if (image == null || image.sprite == null)
                    return null;

                var assetPath = AssetDatabase.GetAssetPath(image.sprite);
                if (string.IsNullOrEmpty(assetPath))
                    return null;

                var fileName = Path.GetFileName(assetPath);
                if (string.IsNullOrEmpty(fileName))
                    return null;

                var dest = Path.Combine(outputDir, fileName);
                if (!copiedSprites.Contains(fileName))
                {
                    File.Copy(assetPath, dest, true);
                    copiedSprites.Add(fileName);
                }

                widget.spriteFile = fileName;
                return fileName;
            }

            var sourcePath = ResolveSpriteSourcePath(widget.spriteFile);
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                error = "找不到 sprite 文件：" + widget.spriteFile;
                return null;
            }

            var target = Path.Combine(outputDir, Path.GetFileName(widget.spriteFile));
            if (!copiedSprites.Contains(widget.spriteFile))
            {
                File.Copy(sourcePath, target, true);
                copiedSprites.Add(widget.spriteFile);
            }

            return Path.GetFileName(widget.spriteFile);
        }

        static string ResolveSpriteSourcePath(string spriteFile)
        {
            var fileName = Path.GetFileName(spriteFile);
            var streaming = Path.Combine(Application.dataPath, "StreamingAssets", "OverlayHud", fileName);
            if (File.Exists(streaming))
                return streaming;

            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var overlayHud = Path.Combine(repoRoot, "desktop-overlay", "OverlayResources", "hud", fileName);
            if (File.Exists(overlayHud))
                return overlayHud;

            return AssetDatabase.GetAssetPath(
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/StreamingAssets/OverlayHud/" + fileName));
        }

        struct HudTextStyleExport
        {
            public string FontFile;
            public int FontSize;
            public string FontColor;
            public string FontWeight;
            public string TextAlign;
            public string ContentAlign;
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
            if (!string.Equals(kind, "text", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(kind, "button", StringComparison.OrdinalIgnoreCase))
                return default;

            var text = FindWidgetText(widget);
            if (text == null)
            {
                if (!string.Equals(kind, "button", StringComparison.OrdinalIgnoreCase))
                    return default;

                return new HudTextStyleExport
                {
                    FontFile = CopyFontFile(Resources.GetBuiltinResource<Font>("Arial.ttf"), fontsDir, copiedFonts, out error),
                    FontSize = 12,
                    FontColor = "#FFFFFFFF",
                    FontWeight = "normal",
                    ContentAlign = "center",
                    HasStyle = string.IsNullOrEmpty(error),
                };
            }

            var fontFile = CopyFontFile(text.font, fontsDir, copiedFonts, out error);
            if (!string.IsNullOrEmpty(error))
                return default;

            var align = MapTextAlign(text.alignment);
            return new HudTextStyleExport
            {
                FontFile = fontFile,
                FontSize = text.fontSize,
                FontColor = FormatColor(text.color),
                FontWeight = text.fontStyle == FontStyle.Bold ? "bold" : "normal",
                TextAlign = string.Equals(kind, "text", StringComparison.OrdinalIgnoreCase) ? align : null,
                ContentAlign = string.Equals(kind, "button", StringComparison.OrdinalIgnoreCase) ? align : null,
                HasStyle = true,
            };
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
            var dest = Path.Combine(fontsDir, fileName);
            if (!copiedFonts.Contains(fileName))
            {
                File.Copy(sourcePath, dest, true);
                copiedFonts.Add(fileName);
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

        static void AppendTextStyleJson(StringBuilder sb, HudTextStyleExport style)
        {
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
        }

        static string BuildWidgetJson(
            DesktopOverlayHudWidget widget,
            string parentId,
            float x,
            float y,
            float w,
            float h,
            string sprite,
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
            if (!string.IsNullOrEmpty(sprite))
                sb.Append(",\"sprite\":\"").Append(Escape(sprite)).Append("\"");
            AppendTextStyleJson(sb, textStyle);
            sb.Append("}");
            return sb.ToString();
        }

        static string BuildDocumentJson(List<string> widgets)
        {
            var sb = new StringBuilder();
            sb.Append("{\"width\":960,\"height\":560,\"widgets\":[");
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
