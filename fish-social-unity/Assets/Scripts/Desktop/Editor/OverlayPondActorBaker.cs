#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using FishSocial.Desktop;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    /// <summary>
    /// Seat template (OverlayPondActor) nested as a prefab instance under each kind=spot.
    /// Edit OverlayPondActor.prefab to change seat/cat/name/status/ring for every pond.
    /// </summary>
    public static class OverlayPondActorBaker
    {
        public const string PrefabPath = DesktopPrefabCatalog.Folder + "/OverlayPondActor.prefab";
        public const string NestedInstanceName = "OverlayPondActor";
        public const string ArtFolder = "Assets/Desktop/OverlayArt";
        public const string HookRingAsset = ArtFolder + "/hook-ring.png";

        public const float SeatHostW = 84f;
        public const float SeatHostH = 122f;
        public const float SeatW = 64f;
        public const float SeatH = 32f;
        public const float PetW = 64f;
        public const float PetH = 64f;

        // Relative top-left inside the seat host (cat sitting on chair).
        public const float PetX = 10f;
        public const float PetY = 18f;
        public const float SeatX = 10f;
        public const float SeatY = 82f;
        public const float RingX = 4f;
        public const float RingY = 12f;
        public const float StatusX = 33f;
        public const float StatusY = 0f;
        public const float NameX = 2f;
        public const float NameY = 102f;
        public const float BubbleX = 4f;
        public const float BubbleY = 0f;
        public const float BubbleW = 80f;
        public const float BubbleH = 20f;
        public const float HintX = 2f;
        public const float HintY = 0f;
        public const float HintW = 84f;
        public const float HintH = 20f;

        public static void Ensure()
        {
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "Resources", "Desktop", "Prefabs")));
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "Desktop", "OverlayArt")));
            CopyHookRingPng();

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing == null)
            {
                var root = CreateRoot();
                BuildParts(root.transform, string.Empty);
                TryApplyDefaultSeatSprite(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Object.DestroyImmediate(root);
            }
            else
            {
                var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    if (contents.GetComponent<OverlayPondActorView>() == null)
                        contents.AddComponent<OverlayPondActorView>();
                    BuildParts(contents.transform, string.Empty);
                    TryApplyDefaultSeatSprite(contents.transform);
                    PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Nest OverlayPondActor.prefab under the spot host (prefab instance, not a copy).
        /// Removes legacy inline actor-* children from older bakers.
        /// </summary>
        public static void EnsureOnSpot(Transform spot, string spotId)
        {
            if (spot == null || string.IsNullOrEmpty(spotId))
                return;

            Ensure();
            ExpandSpotHost(spot);
            StripLegacyInlineParts(spot);

            var actor = FindNestedActorInstance(spot);
            if (actor == null)
                actor = InstantiateNestedActor(spot);

            if (actor == null)
                return;

            FitActorToHost(spot, actor.transform);
            BuildParts(actor.transform, spotId);
            BindSpotId(actor, spotId);
        }

        static GameObject FindNestedActorInstance(Transform spot)
        {
            if (spot == null)
                return null;

            for (var i = 0; i < spot.childCount; i++)
            {
                var child = spot.GetChild(i);
                if (child == null)
                    continue;
                if (!IsOverlayPondActorInstance(child.gameObject))
                    continue;
                return child.gameObject;
            }

            // Fallback: any OverlayPondActorView child that is a prefab instance of our asset.
            var views = spot.GetComponentsInChildren<OverlayPondActorView>(true);
            for (var i = 0; i < views.Length; i++)
            {
                var view = views[i];
                if (view == null || view.transform == spot)
                    continue;
                if (!IsOverlayPondActorInstance(view.gameObject))
                    continue;
                if (view.transform.parent != spot)
                    view.transform.SetParent(spot, false);
                return view.gameObject;
            }

            return null;
        }

        static bool IsOverlayPondActorInstance(GameObject go)
        {
            if (go == null)
                return false;
            var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            return string.Equals(path, PrefabPath, System.StringComparison.OrdinalIgnoreCase);
        }

        static GameObject InstantiateNestedActor(Transform spot)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[OverlayPondActor] Missing prefab at " + PrefabPath);
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, spot) as GameObject;
            if (instance == null)
                return null;

            instance.name = NestedInstanceName;
            Undo.RegisterCreatedObjectUndo(instance, "Nest OverlayPondActor");
            return instance;
        }

        static void FitActorToHost(Transform spot, Transform actor)
        {
            if (spot == null || actor == null)
                return;

            var hostRt = spot.GetComponent<RectTransform>();
            var actorRt = actor.GetComponent<RectTransform>();
            if (hostRt == null || actorRt == null)
                return;

            var w = hostRt.rect.width > 0.5f ? hostRt.rect.width : SeatHostW;
            var h = hostRt.rect.height > 0.5f ? hostRt.rect.height : SeatHostH;
            OverlayPondLayoutBaker.PlaceTopLeft(actorRt, 0f, 0f, w, h);
        }

        public static void BindSpotId(GameObject actorRoot, string spotId)
        {
            if (actorRoot == null || string.IsNullOrEmpty(spotId))
                return;

            var view = actorRoot.GetComponent<OverlayPondActorView>();
            if (view == null)
                view = actorRoot.AddComponent<OverlayPondActorView>();
            view.spotId = spotId;

            var markers = actorRoot.GetComponentsInChildren<DesktopOverlayLayoutObject>(true);
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null)
                    continue;
                var kind = (marker.kind ?? string.Empty).Trim().ToLowerInvariant();
                if (!kind.StartsWith("actor-", System.StringComparison.Ordinal))
                    continue;

                var suffix = kind.Substring("actor-".Length);
                marker.spotId = spotId;
                marker.objectId = spotId + "-" + suffix;
                marker.anchor = "top-left";
                marker.gameObject.name = marker.objectId;
                PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            PrefabUtility.RecordPrefabInstancePropertyModifications(actorRoot);
        }

        /// <summary>
        /// Older bakers copied actor-* parts onto the spot itself. Remove those copies
        /// so only a nested OverlayPondActor prefab instance remains.
        /// </summary>
        static void StripLegacyInlineParts(Transform spot)
        {
            if (spot == null)
                return;

            // View belongs on the nested instance, not the spot host.
            var hostView = spot.GetComponent<OverlayPondActorView>();
            if (hostView != null)
                Object.DestroyImmediate(hostView);

            var toDestroy = new List<GameObject>();
            for (var i = 0; i < spot.childCount; i++)
            {
                var child = spot.GetChild(i);
                if (child == null)
                    continue;

                // Keep a real OverlayPondActor prefab instance.
                if (IsOverlayPondActorInstance(child.gameObject))
                    continue;

                var marker = child.GetComponent<DesktopOverlayLayoutObject>();
                var kind = marker != null
                    ? (marker.kind ?? string.Empty).Trim().ToLowerInvariant()
                    : string.Empty;
                if (kind.StartsWith("actor-", System.StringComparison.Ordinal))
                {
                    toDestroy.Add(child.gameObject);
                    continue;
                }

                // Orphaned OverlayPondActorView that is not our prefab instance.
                if (child.GetComponent<OverlayPondActorView>() != null)
                    toDestroy.Add(child.gameObject);
            }

            for (var i = 0; i < toDestroy.Count; i++)
            {
                if (toDestroy[i] != null)
                    Object.DestroyImmediate(toDestroy[i]);
            }
        }

        static void ExpandSpotHost(Transform spot)
        {
            var rt = spot.GetComponent<RectTransform>();
            if (rt == null)
                return;

            var w = rt.rect.width > 0.5f ? rt.rect.width : rt.sizeDelta.x;
            var h = rt.rect.height > 0.5f ? rt.rect.height : rt.sizeDelta.y;
            // Only grow default 24×24 anchors; leave artist-resized hosts alone.
            if (w > OverlayPondLayoutBaker.SpotSize + 1f || h > OverlayPondLayoutBaker.SpotSize + 1f)
                return;

            var x = rt.anchoredPosition.x;
            var y = -rt.anchoredPosition.y;
            // Preserve bottom-center of the tiny spot when expanding.
            var bottomCenterX = x + w * 0.5f;
            var bottomY = y + h;
            var newX = bottomCenterX - SeatHostW * 0.5f;
            var newY = bottomY - SeatHostH;
            OverlayPondLayoutBaker.PlaceTopLeft(rt, newX, newY, SeatHostW, SeatHostH);

            var spotImage = spot.GetComponent<Image>();
            if (spotImage != null)
                spotImage.color = new Color(1f, 1f, 1f, 0f);
        }

        static GameObject CreateRoot()
        {
            var root = new GameObject(
                "OverlayPondActor",
                typeof(RectTransform),
                typeof(OverlayPondActorView));
            var rt = root.GetComponent<RectTransform>();
            OverlayPondLayoutBaker.PlaceTopLeft(rt, 0f, 0f, SeatHostW, SeatHostH);
            return root;
        }

        static void BuildParts(Transform parent, string spotId)
        {
            var prefix = string.IsNullOrEmpty(spotId) ? "actor" : spotId;
            EnsurePart(
                parent,
                prefix + "-seat",
                "actor-seat",
                spotId,
                SeatX,
                SeatY,
                SeatW,
                SeatH,
                8,
                new Color(0.72f, 0.55f, 0.32f, 0.85f),
                false,
                "seats/_default.png");
            EnsurePart(
                parent,
                prefix + "-pet",
                "actor-pet",
                spotId,
                PetX,
                PetY,
                PetW,
                PetH,
                10,
                new Color(0.85f, 0.55f, 0.2f, 0.55f),
                false,
                null);
            EnsurePart(
                parent,
                prefix + "-ring",
                "actor-ring",
                spotId,
                RingX,
                RingY,
                76f,
                76f,
                12,
                new Color(0.91f, 0.61f, 0.25f, 0.95f),
                true,
                "status/hook-ring.png");
            EnsurePart(
                parent,
                prefix + "-status",
                "actor-status",
                spotId,
                StatusX,
                StatusY,
                18f,
                18f,
                14,
                new Color(0.35f, 0.66f, 0.84f, 0.95f),
                false,
                "status/fishing.png");
            EnsurePart(
                parent,
                prefix + "-name",
                "actor-name",
                spotId,
                NameX,
                NameY,
                88f,
                20f,
                16,
                new Color(0.06f, 0.09f, 0.12f, 0.75f),
                false,
                null);
            EnsurePart(
                parent,
                prefix + "-bubble",
                "actor-bubble",
                spotId,
                BubbleX,
                BubbleY,
                BubbleW,
                BubbleH,
                18,
                new Color(0.06f, 0.09f, 0.12f, 0.35f),
                false,
                null);
            EnsurePart(
                parent,
                prefix + "-hint",
                "actor-hint",
                spotId,
                HintX,
                HintY,
                HintW,
                HintH,
                19,
                new Color(0.10f, 0.23f, 0.29f, 0.35f),
                false,
                null);
        }

        static Transform FindPart(Transform parent, string objectId, string kind)
        {
            if (parent == null)
                return null;

            var named = parent.Find(objectId);
            if (named != null)
                return named;

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child == null)
                    continue;
                if (child.name == objectId)
                    return child;

                var marker = child.GetComponent<DesktopOverlayLayoutObject>();
                if (marker != null &&
                    string.Equals(marker.kind, kind, System.StringComparison.OrdinalIgnoreCase))
                    return child;
            }

            return null;
        }

        static void EnsurePart(
            Transform parent,
            string objectId,
            string kind,
            string spotId,
            float x,
            float y,
            float w,
            float h,
            int z,
            Color color,
            bool radialFill,
            string spriteFile)
        {
            var child = FindPart(parent, objectId, kind);
            var created = false;
            if (child == null)
            {
                var go = new GameObject(objectId, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                child = go.transform;
                created = true;
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
            // Nested actor parts always use top-left; spot host keeps bottom-center.
            if (kind != null && kind.StartsWith("actor-", System.StringComparison.Ordinal))
                marker.anchor = "top-left";
            else if (string.IsNullOrEmpty(marker.anchor))
                marker.anchor = "top-left";

            var image = child.GetComponent<Image>();
            if (image != null)
            {
                if (created)
                    image.color = color;
                if (radialFill)
                {
                    image.type = Image.Type.Filled;
                    image.fillMethod = Image.FillMethod.Radial360;
                    image.fillOrigin = (int)Image.Origin360.Top;
                    image.fillClockwise = true;
                    image.fillAmount = 0.75f;
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HookRingAsset);
                    if (sprite != null)
                        image.sprite = sprite;
                }
            }

            if ((kind == "actor-name" || kind == "actor-bubble" || kind == "actor-hint") &&
                child.Find("Label") == null)
            {
                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelGo.transform.SetParent(child, false);
                var lrt = labelGo.GetComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
                var text = labelGo.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.fontSize = kind == "actor-hint" ? 12 : 11;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.text = kind == "actor-bubble"
                    ? "聊天"
                    : kind == "actor-hint"
                        ? "提示"
                        : "昵称";
            }

            var rt = child.GetComponent<RectTransform>();
            if (created || (rt != null && rt.sizeDelta.x < 1f))
                OverlayPondLayoutBaker.PlaceTopLeft(rt, x, y, w, h);
        }

        static void TryApplyDefaultSeatSprite(Transform parent)
        {
            if (parent == null)
                return;
            Transform seat = null;
            foreach (Transform child in parent)
            {
                var marker = child.GetComponent<DesktopOverlayLayoutObject>();
                if (marker != null &&
                    string.Equals(marker.kind, "actor-seat", System.StringComparison.Ordinal))
                {
                    seat = child;
                    break;
                }
            }

            if (seat == null)
                return;

            var image = seat.GetComponent<Image>();
            if (image == null || image.sprite != null)
                return;

            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var source = Path.Combine(repoRoot, "desktop-overlay", "OverlayResources", "seats", "_default.png");
            if (!File.Exists(source))
                return;

            var destAsset = ArtFolder + "/seat-default.png";
            var destFull = Path.GetFullPath(Path.Combine(Application.dataPath, "Desktop", "OverlayArt", "seat-default.png"));
            try
            {
                File.Copy(source, destFull, true);
                AssetDatabase.ImportAsset(destAsset);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destAsset);
                if (sprite != null)
                {
                    image.sprite = sprite;
                    image.color = Color.white;
                    image.type = Image.Type.Simple;
                    var marker = seat.GetComponent<DesktopOverlayLayoutObject>();
                    if (marker != null && string.IsNullOrEmpty(marker.spriteFile))
                        marker.spriteFile = "seats/_default.png";
                }
            }
            catch
            {
            }
        }

        static void CopyHookRingPng()
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var source = Path.Combine(repoRoot, "desktop-overlay", "OverlayResources", "status", "hook-ring.png");
            if (!File.Exists(source))
                return;
            var dest = Path.GetFullPath(Path.Combine(Application.dataPath, "Desktop", "OverlayArt", "hook-ring.png"));
            try
            {
                File.Copy(source, dest, true);
                AssetDatabase.ImportAsset(HookRingAsset);
            }
            catch
            {
            }
        }
    }
}
#endif
