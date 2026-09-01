#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using FishSocial.Desktop;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    /// <summary>
    /// Creates one 960×560 Overlay scene prefab per pond under Assets/Desktop/OverlayLayouts.
    /// Existing spot positions are preserved; only missing objects are added.
    /// </summary>
    public static class OverlayPondLayoutBaker
    {
        public const string Folder = "Assets/Desktop/OverlayLayouts";
        public const float CanvasWidth = 960f;
        public const float CanvasHeight = 560f;
        public const float SpotSize = 24f;
        public const float SeatHostW = OverlayPondActorBaker.SeatHostW;
        public const float SeatHostH = OverlayPondActorBaker.SeatHostH;

        public static string PrefabPath(string pondId)
        {
            return Folder + "/" + pondId + ".prefab";
        }

        [MenuItem("Fish Social/补齐 Overlay 布局 Prefab", false, 43)]
        public static void EnsureAllMenu()
        {
            EnsureAll();
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Overlay 布局 Prefab",
                    "已补齐 Assets/Desktop/OverlayLayouts/<pondId>.prefab。\n每个 spot 下嵌套 OverlayPondActor 预制体实例。\n先改 OverlayPondActor，再拖塘内 spot，最后 Export Overlay Layout。",
                    "确定");
            }
        }

        public static void EnsureAll()
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "Desktop", "OverlayLayouts")));
            if (!AssetDatabase.IsValidFolder("Assets/Desktop"))
                AssetDatabase.CreateFolder("Assets", "Desktop");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Desktop", "OverlayLayouts");

            var ponds = DesktopGameData.Ponds;
            if (ponds == null || ponds.Length == 0)
                throw new InvalidOperationException("GameData 中没有鱼塘，无法生成 Overlay 布局 Prefab。");

            var created = 0;
            var updated = 0;
            for (var i = 0; i < ponds.Length; i++)
            {
                var pond = ponds[i];
                if (pond == null || string.IsNullOrEmpty(pond.pondId))
                    continue;
                if (EnsurePond(pond.pondId, pond.name))
                    created++;
                else
                    updated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[OverlayPondLayout] Ensured " + (created + updated) +
                      " prefabs in " + Folder + " (new " + created + ").");
        }

        static bool EnsurePond(string pondId, string pondName)
        {
            var path = PrefabPath(pondId);
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing == null)
            {
                var root = CreateRoot(pondId, pondName);
                ApplyExistingJson(root.transform, pondId);
                PopulateMissing(root.transform, pondId);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                UnityEngine.Object.DestroyImmediate(root);
                return true;
            }

            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureRoot(contents, pondId, pondName);
                PopulateMissing(contents.transform, pondId);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            return false;
        }

        static GameObject CreateRoot(string pondId, string pondName)
        {
            var root = new GameObject(
                pondId,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(DesktopOverlayPondLayoutView));
            ConfigureRoot(root, pondId, pondName);
            return root;
        }

        static void ConfigureRoot(GameObject root, string pondId, string pondName)
        {
            var view = root.GetComponent<DesktopOverlayPondLayoutView>();
            if (view == null)
                view = root.AddComponent<DesktopOverlayPondLayoutView>();
            view.pondId = pondId;
            root.name = pondId;

            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);

            EnsureNamedObject(
                root.transform,
                "pond-bg",
                "sprite",
                0f,
                0f,
                CanvasWidth,
                CanvasHeight,
                0,
                null,
                "pond.png");
            var bg = root.transform.Find("pond-bg");
            if (bg != null)
            {
                var image = bg.GetComponent<Image>();
                if (image != null && image.sprite == null)
                    image.color = new Color(0.16f, 0.42f, 0.48f, 1f);
                var label = bg.Find("Label");
                if (label == null)
                {
                    var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                    labelGo.transform.SetParent(bg, false);
                    var lrt = labelGo.GetComponent<RectTransform>();
                    lrt.anchorMin = Vector2.zero;
                    lrt.anchorMax = Vector2.one;
                    lrt.offsetMin = Vector2.zero;
                    lrt.offsetMax = Vector2.zero;
                    var text = labelGo.GetComponent<Text>();
                    text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    text.fontSize = 18;
                    text.alignment = TextAnchor.UpperCenter;
                    text.color = new Color(1f, 1f, 1f, 0.7f);
                    text.text = string.IsNullOrEmpty(pondName) ? pondId : pondName + "  ·  " + pondId;
                }
            }

            EnsureNamedObject(
                root.transform,
                "waiting",
                "waiting",
                40f,
                24f,
                880f,
                64f,
                5,
                null,
                null);
            EnsureNamedObject(
                root.transform,
                "cat-size",
                "pet-size",
                8f,
                520f,
                128f,
                128f,
                20,
                null,
                null);
        }

        public static void BatchEnsure()
        {
            try
            {
                EnsureAll();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                throw;
            }
        }

        public static string LayoutJsonPath(string pondId)
        {
            return Path.Combine(OverlayLayoutExporter.LayoutOutputDir(), pondId + ".json");
        }

        static void ApplyExistingJson(Transform root, string pondId)
        {
            var path = LayoutJsonPath(pondId);
            if (!File.Exists(path))
                return;

            OverlayLayoutDocument document;
            try
            {
                document = JsonUtility.FromJson<OverlayLayoutDocument>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[OverlayPondLayout] Could not seed " + pondId + " from JSON: " + ex.Message);
                return;
            }

            if (document == null || document.objects == null)
                return;

            for (var i = 0; i < document.objects.Length; i++)
            {
                var item = document.objects[i];
                if (item == null || string.IsNullOrEmpty(item.id))
                    continue;
                var kind = string.IsNullOrEmpty(item.kind) ? "sprite" : item.kind;
                var topLeftX = item.x;
                var topLeftY = item.y;
                RectFromAnchor(item, out topLeftX, out topLeftY);
                EnsureNamedObject(
                    root,
                    item.id,
                    kind,
                    topLeftX,
                    topLeftY,
                    item.w > 0 ? item.w : SpotSize,
                    item.h > 0 ? item.h : SpotSize,
                    item.z,
                    string.IsNullOrEmpty(item.spotId) ? null : item.spotId,
                    string.IsNullOrEmpty(item.sprite) ? null : item.sprite);
                var child = root.Find(item.id);
                if (child == null)
                    continue;
                var marker = child.GetComponent<DesktopOverlayLayoutObject>();
                if (marker != null && !string.IsNullOrEmpty(item.anchor))
                    marker.anchor = item.anchor;
                PlaceTopLeft(
                    child.GetComponent<RectTransform>(),
                    topLeftX,
                    topLeftY,
                    item.w > 0 ? item.w : SpotSize,
                    item.h > 0 ? item.h : SpotSize);
            }
        }

        static void RectFromAnchor(OverlayLayoutObject item, out float x, out float y)
        {
            var w = item.w > 0 ? item.w : SpotSize;
            var h = item.h > 0 ? item.h : SpotSize;
            var anchor = (item.anchor ?? string.Empty).Trim().ToLowerInvariant();
            if (anchor == "bottom-center")
            {
                x = item.x - w * 0.5f;
                y = item.y - h;
                return;
            }

            if (anchor == "center")
            {
                x = item.x - w * 0.5f;
                y = item.y - h * 0.5f;
                return;
            }

            x = item.x;
            y = item.y;
        }

        [Serializable]
        sealed class OverlayLayoutDocument
        {
            public int version;
            public string pondId;
            public OverlayLayoutObject[] objects;
        }

        [Serializable]
        sealed class OverlayLayoutObject
        {
            public string id;
            public string kind;
            public string spotId;
            public float x;
            public float y;
            public float w;
            public float h;
            public int z;
            public string sprite;
            public string anchor;
        }

        static void PopulateMissing(Transform root, string pondId)
        {
            var spotIds = DesktopGameData.GetSpotIds(pondId);
            for (var i = 0; i < spotIds.Length; i++)
            {
                var spotId = spotIds[i];
                if (FindSpot(root, spotId) != null)
                    continue;
                DefaultSpotPosition(i, spotIds.Length, out var x, out var y);
                EnsureNamedObject(
                    root,
                    spotId,
                    "spot",
                    x - SeatHostW * 0.5f,
                    y - SeatHostH,
                    SeatHostW,
                    SeatHostH,
                    10,
                    spotId,
                    "seats/_default.png");
            }

            OverlayPondActorBaker.Ensure();
            var spots = root.GetComponentsInChildren<DesktopOverlayLayoutObject>(true);
            for (var i = 0; i < spots.Length; i++)
            {
                var item = spots[i];
                if (item == null || !string.Equals(item.kind, "spot", System.StringComparison.Ordinal))
                    continue;
                var id = string.IsNullOrWhiteSpace(item.spotId) ? item.objectId : item.spotId;
                OverlayPondActorBaker.EnsureOnSpot(item.transform, id);
            }
        }

        static Transform FindSpot(Transform root, string spotId)
        {
            var objects = root.GetComponentsInChildren<DesktopOverlayLayoutObject>(true);
            for (var i = 0; i < objects.Length; i++)
            {
                var item = objects[i];
                if (item == null)
                    continue;
                if (string.Equals(item.kind, "spot", StringComparison.Ordinal) &&
                    string.Equals(item.spotId, spotId, StringComparison.Ordinal))
                    return item.transform;
                if (string.Equals(item.objectId, spotId, StringComparison.Ordinal))
                    return item.transform;
            }

            return root.Find(spotId);
        }

        static void EnsureNamedObject(
            Transform root,
            string objectId,
            string kind,
            float x,
            float y,
            float w,
            float h,
            int z,
            string spotId,
            string spriteFile)
        {
            var child = root.Find(objectId);
            var created = false;
            if (child == null)
            {
                var go = new GameObject(objectId, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root, false);
                child = go.transform;
                created = true;
                var image = go.GetComponent<Image>();
                image.color = ColorForKind(kind);
                if (kind == "spot" || kind == "waiting" || kind == "pet-size")
                {
                    var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                    labelGo.transform.SetParent(go.transform, false);
                    var lrt = labelGo.GetComponent<RectTransform>();
                    lrt.anchorMin = Vector2.zero;
                    lrt.anchorMax = Vector2.one;
                    lrt.offsetMin = Vector2.zero;
                    lrt.offsetMax = Vector2.zero;
                    var text = labelGo.GetComponent<Text>();
                    text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    text.fontSize = kind == "spot" ? 8 : 11;
                    text.alignment = TextAnchor.MiddleCenter;
                    text.color = Color.white;
                    text.text = objectId;
                }
            }

            var marker = child.GetComponent<DesktopOverlayLayoutObject>();
            if (marker == null)
                marker = child.gameObject.AddComponent<DesktopOverlayLayoutObject>();
            marker.objectId = objectId;
            marker.kind = kind;
            if (!string.IsNullOrEmpty(spotId))
                marker.spotId = spotId;
            if (!string.IsNullOrEmpty(spriteFile) && string.IsNullOrEmpty(marker.spriteFile))
                marker.spriteFile = spriteFile;
            if (marker.zIndex == 0)
                marker.zIndex = z;
            if (string.IsNullOrEmpty(marker.anchor))
                marker.anchor = kind == "spot" ? "bottom-center" : "top-left";

            var rt = child.GetComponent<RectTransform>();
            if (created || (rt != null && rt.sizeDelta.x < 1f))
                PlaceTopLeft(rt, x, y, w, h);
        }

        public static void PlaceTopLeft(RectTransform rt, float x, float y, float w, float h)
        {
            if (rt == null)
                return;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void DefaultSpotPosition(int index, int count, out float x, out float y)
        {
            var n = Mathf.Max(1, count);
            var angle = (2f * Mathf.PI * index / n) - (Mathf.PI * 0.5f);
            x = Mathf.Clamp(480f + 360f * Mathf.Cos(angle), 40f, 920f);
            y = Mathf.Clamp(360f + 140f * Mathf.Sin(angle), 80f, 520f);
        }

        static Color ColorForKind(string kind)
        {
            switch (kind)
            {
                case "sprite":
                    return new Color(0.16f, 0.42f, 0.48f, 1f);
                case "waiting":
                    return new Color(0.2f, 0.28f, 0.22f, 0.45f);
                case "pet-size":
                    return new Color(0.85f, 0.55f, 0.2f, 0.35f);
                default:
                    return new Color(0.95f, 0.79f, 0.41f, 0.92f);
            }
        }
    }
}
#endif
