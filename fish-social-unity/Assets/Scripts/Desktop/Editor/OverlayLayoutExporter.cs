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
    public static class OverlayLayoutExporter
    {
        public const float CanvasWidth = 960f;
        public const float CanvasHeight = 560f;

        [MenuItem("Fish Social/Export Overlay Layout", false, 44)]
        public static void ExportCurrentMenu()
        {
            var error = ExportSelectionOrActive(out var wrote);
            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Export Overlay Layout", error, "确定");
                return;
            }

            EditorUtility.DisplayDialog("Export Overlay Layout", "已导出：\n" + wrote, "确定");
        }

        [MenuItem("Fish Social/Export Overlay Layout（全部塘）", false, 44)]
        public static void ExportAllMenu()
        {
            var error = ExportAll(out var count);
            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Export Overlay Layout", error, "确定");
                return;
            }

            EditorUtility.DisplayDialog(
                "Export Overlay Layout",
                "已导出 " + count + " 份布局到 OverlayResources/layouts/",
                "确定");
        }

        public static string ExportAll(out int count)
        {
            count = 0;
            OverlayPondLayoutBaker.EnsureAll();
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { OverlayPondLayoutBaker.Folder });
            if (guids == null || guids.Length == 0)
                return "找不到 Overlay 布局 Prefab。请先在 UI Prefab 管理中创建 OverlayPondLayout。";

            var outputDir = OverlayLayoutExporter.LayoutOutputDir();
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var error = ExportPrefabAsset(path, outputDir, out _);
                if (!string.IsNullOrEmpty(error))
                    return path + "：\n" + error;
                count++;
            }

            return null;
        }

        static string ExportSelectionOrActive(out string wrote)
        {
            wrote = string.Empty;
            var target = Selection.activeGameObject;
            string assetPath = null;
            if (target != null)
            {
                var view = target.GetComponentInParent<DesktopOverlayPondLayoutView>();
                if (view != null)
                    assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(view.gameObject);
                if (string.IsNullOrEmpty(assetPath))
                    assetPath = AssetDatabase.GetAssetPath(target);
            }

            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                var calm = OverlayPondLayoutBaker.PrefabPath("pond-calm");
                if (AssetDatabase.LoadAssetAtPath<GameObject>(calm) != null)
                    assetPath = calm;
            }

            if (string.IsNullOrEmpty(assetPath))
                return "请选中一份 Overlay 布局 Prefab，或先补齐 OverlayPondLayout。";

            return ExportPrefabAsset(assetPath, OverlayLayoutExporter.LayoutOutputDir(), out wrote);
        }

        static string ExportPrefabAsset(string prefabPath, string outputDir, out string wrote)
        {
            wrote = string.Empty;
            var rootGo = PrefabUtility.LoadPrefabContents(prefabPath);
            if (rootGo == null)
                return "无法加载 Prefab：" + prefabPath;

            try
            {
                var error = ExportLoadedPrefab(rootGo, outputDir, out wrote);
                return error;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rootGo);
            }
        }

        static string ExportLoadedPrefab(GameObject rootGo, string outputDir, out string wrote)
        {
            wrote = string.Empty;
            var view = rootGo.GetComponent<DesktopOverlayPondLayoutView>();
            if (view == null)
                return "Prefab 缺少 DesktopOverlayPondLayoutView。";
            if (string.IsNullOrWhiteSpace(view.pondId))
                return "pondId 为空。";

            var root = rootGo.GetComponent<RectTransform>();
            if (root == null)
                return "缺少 RectTransform。";
            var size = root.rect.size;
            if (Mathf.Abs(size.x - CanvasWidth) > 0.5f || Mathf.Abs(size.y - CanvasHeight) > 0.5f)
                return "画布必须是 960×560，当前为 " + size.x + "×" + size.y + "。";

            var objects = rootGo.GetComponentsInChildren<DesktopOverlayLayoutObject>(true);
            if (objects == null || objects.Length == 0)
                return "没有 DesktopOverlayLayoutObject。";

            var knownSpots = new HashSet<string>(DesktopGameData.GetSpotIds(view.pondId), StringComparer.Ordinal);
            if (knownSpots.Count == 0)
                return "GameData 中找不到 " + view.pondId + " 的钓位。";

            var byId = new Dictionary<string, DesktopOverlayLayoutObject>(StringComparer.Ordinal);
            var spotIds = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<LayoutEntry>();

            foreach (var item in objects)
            {
                if (item == null)
                    continue;
                var objectId = string.IsNullOrWhiteSpace(item.objectId) ? item.gameObject.name : item.objectId;
                if (string.IsNullOrWhiteSpace(objectId))
                    return "存在未设置 objectId 的布局物体。";
                if (byId.ContainsKey(objectId))
                    return "重复的 id：" + objectId;
                byId[objectId] = item;

                var kind = NormalizeKind(item.kind);
                if (kind == "spot")
                {
                    var spotId = string.IsNullOrWhiteSpace(item.spotId) ? objectId : item.spotId;
                    if (!knownSpots.Contains(spotId))
                        return "非法 spotId（不在 " + view.pondId + " 的钓位表）：" + spotId;
                    if (!spotIds.Add(spotId))
                        return "重复的 spotId：" + spotId;
                }
                else if (kind == "hud" || kind == "button" || kind == "menu")
                {
                    return "HUD 控件不能写入场景布局（见 STEAM-DESKTOP-ART-03）：" + objectId;
                }

                var rt = item.GetComponent<RectTransform>();
                if (rt == null)
                    return "缺少 RectTransform：" + objectId;
                var bounds = GetTopLeftBounds(root, rt);
                if (bounds.width < 1f || bounds.height < 1f)
                    return "尺寸无效：" + objectId;

                string spriteName = null;
                if (kind == "sprite")
                {
                    var spriteError = ResolveSpriteName(item, out spriteName);
                    if (!string.IsNullOrEmpty(spriteError))
                        return spriteError;
                }

                entries.Add(new LayoutEntry
                {
                    Id = objectId,
                    Kind = kind,
                    SpotId = kind == "spot"
                        ? (string.IsNullOrWhiteSpace(item.spotId) ? objectId : item.spotId)
                        : null,
                    X = bounds.x,
                    Y = bounds.y,
                    W = bounds.width,
                    H = bounds.height,
                    Z = item.zIndex,
                    Sprite = spriteName,
                    Anchor = string.IsNullOrEmpty(item.anchor)
                        ? (kind == "spot" ? "bottom-center" : "top-left")
                        : item.anchor,
                });
            }

            if (spotIds.Count == 0)
                return "至少需要一个 kind=spot 钓位。";

            var copyError = CopyReferencedSprites(objects, OverlayResourcesDir());
            if (!string.IsNullOrEmpty(copyError))
                return copyError;

            Directory.CreateDirectory(outputDir);
            var tempPath = Path.Combine(outputDir, view.pondId + ".json.tmp");
            var jsonPath = Path.Combine(outputDir, view.pondId + ".json");
            try
            {
                File.WriteAllText(tempPath, BuildDocumentJson(view.pondId, entries), new UTF8Encoding(false));
                if (File.Exists(jsonPath))
                    File.Delete(jsonPath);
                File.Move(tempPath, jsonPath);
            }
            catch (Exception ex)
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                return "写入失败：" + ex.Message;
            }

            wrote = jsonPath;
            AssetDatabase.Refresh();
            Debug.Log("[OverlayLayoutExporter] Wrote " + jsonPath);
            return null;
        }

        static string NormalizeKind(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
                return "sprite";
            return kind.Trim().ToLowerInvariant();
        }

        static Rect GetTopLeftBounds(RectTransform canvas, RectTransform target)
        {
            if (UsesTopLeftLayout(target))
            {
                return new Rect(
                    target.anchoredPosition.x,
                    -target.anchoredPosition.y,
                    target.rect.width,
                    target.rect.height);
            }

            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, target);
            var x = bounds.min.x;
            var y = canvas.rect.height + bounds.max.y;
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

        static string ResolveSpriteName(DesktopOverlayLayoutObject item, out string spriteName)
        {
            spriteName = null;
            if (!string.IsNullOrWhiteSpace(item.spriteFile))
            {
                spriteName = Path.GetFileName(item.spriteFile);
                return null;
            }

            var image = item.GetComponent<Image>();
            if (image == null || image.sprite == null)
                return null;

            var assetPath = AssetDatabase.GetAssetPath(image.sprite);
            if (string.IsNullOrEmpty(assetPath))
                return null;
            spriteName = Path.GetFileName(assetPath);
            return null;
        }

        static string CopyReferencedSprites(DesktopOverlayLayoutObject[] objects, string resourcesDir)
        {
            if (objects == null)
                return null;
            Directory.CreateDirectory(resourcesDir);
            for (var i = 0; i < objects.Length; i++)
            {
                var item = objects[i];
                if (item == null || !string.Equals(NormalizeKind(item.kind), "sprite", StringComparison.Ordinal))
                    continue;
                var image = item.GetComponent<Image>();
                if (image == null || image.sprite == null)
                    continue;
                var assetPath = AssetDatabase.GetAssetPath(image.sprite);
                if (string.IsNullOrEmpty(assetPath))
                    continue;
                var fullAsset = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
                if (!File.Exists(fullAsset))
                    continue;
                var fileName = Path.GetFileName(fullAsset);
                if (string.IsNullOrEmpty(fileName))
                    continue;
                var dest = Path.Combine(resourcesDir, fileName);
                if (string.Equals(fullAsset, dest, StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    File.Copy(fullAsset, dest, true);
                }
                catch (Exception ex)
                {
                    return "拷贝贴图失败：" + fileName + " — " + ex.Message;
                }
            }

            return null;
        }

        static string BuildDocumentJson(string pondId, List<LayoutEntry> entries)
        {
            var sb = new StringBuilder();
            sb.Append("{\"version\":1,\"pondId\":\"").Append(Escape(pondId)).Append("\",");
            sb.Append("\"canvas\":{\"width\":960,\"height\":560,\"origin\":\"top-left\"},");
            sb.Append("\"objects\":[");
            for (var i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                    sb.Append(",");
                sb.AppendLine();
                sb.Append("    ");
                sb.Append(BuildObjectJson(entries[i]));
            }

            sb.AppendLine();
            sb.Append("]}");
            return sb.ToString();
        }

        static string BuildObjectJson(LayoutEntry entry)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"id\":\"").Append(Escape(entry.Id)).Append("\",");
            sb.Append("\"kind\":\"").Append(Escape(entry.Kind)).Append("\",");
            if (!string.IsNullOrEmpty(entry.SpotId))
                sb.Append("\"spotId\":\"").Append(Escape(entry.SpotId)).Append("\",");
            sb.Append("\"x\":").Append(Mathf.Round(AnchorX(entry))).Append(",");
            sb.Append("\"y\":").Append(Mathf.Round(AnchorY(entry))).Append(",");
            sb.Append("\"w\":").Append(Mathf.Round(entry.W)).Append(",");
            sb.Append("\"h\":").Append(Mathf.Round(entry.H)).Append(",");
            sb.Append("\"z\":").Append(entry.Z);
            sb.Append(",\"anchor\":\"").Append(Escape(entry.Anchor)).Append("\"");
            if (!string.IsNullOrEmpty(entry.Sprite))
                sb.Append(",\"sprite\":\"").Append(Escape(entry.Sprite)).Append("\"");
            sb.Append("}");
            return sb.ToString();
        }

        static float AnchorX(LayoutEntry entry)
        {
            if (string.Equals(entry.Kind, "spot", StringComparison.Ordinal) &&
                string.Equals(entry.Anchor, "bottom-center", StringComparison.OrdinalIgnoreCase))
                return entry.X + entry.W * 0.5f;
            if (string.Equals(entry.Anchor, "center", StringComparison.OrdinalIgnoreCase))
                return entry.X + entry.W * 0.5f;
            return entry.X;
        }

        static float AnchorY(LayoutEntry entry)
        {
            if (string.Equals(entry.Kind, "spot", StringComparison.Ordinal) &&
                string.Equals(entry.Anchor, "bottom-center", StringComparison.OrdinalIgnoreCase))
                return entry.Y + entry.H;
            if (string.Equals(entry.Anchor, "center", StringComparison.OrdinalIgnoreCase))
                return entry.Y + entry.H * 0.5f;
            return entry.Y;
        }

        static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public static string LayoutOutputDir()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            return Path.Combine(repoRoot, "desktop-overlay", "OverlayResources", "layouts");
        }

        static string OverlayResourcesDir()
        {
            return Path.GetFullPath(Path.Combine(LayoutOutputDir(), ".."));
        }

        sealed class LayoutEntry
        {
            public string Id;
            public string Kind;
            public string SpotId;
            public float X;
            public float Y;
            public float W;
            public float H;
            public int Z;
            public string Sprite;
            public string Anchor;
        }
    }
}
#endif
