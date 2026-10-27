#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static class WorldMapSceneAuthoring
    {
        const string MapPath = "Assets/Resources/Desktop/Theme/Cozy/scenes/world-map.png";
        const string PrefabPath = "Assets/Resources/Desktop/Prefabs/PanelWorldMap.prefab";

        [MenuItem("Fish Social/World Map/Apply Artwork Aspect And Verify")]
        public static void ApplyAndVerify()
        {
            AssetDatabase.ImportAsset(MapPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Resources/Desktop/WorldMapPonds.json", ImportAssetOptions.ForceUpdate);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MapPath);
            if (sprite == null) throw new InvalidOperationException("World map sprite failed to import.");
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var content = root.transform.Find("Viewport/MapContent") as RectTransform;
                var image = content.Find("MapImage").GetComponent<Image>();
                content.sizeDelta = new Vector2(700f * sprite.rect.width / sprite.rect.height, 700f);
                image.sprite = sprite;
                image.overrideSprite = null;
                image.color = Color.white;
                image.type = Image.Type.Simple;
                image.preserveAspect = false; // Parent has the same aspect; marker layer shares its full rect.
                foreach (var name in new[] { "MapImage", "MarkerLayer" })
                {
                    var rect = content.Find(name) as RectTransform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var ponds = WorldMapPondCatalog.LoadVisible();
            if (ponds.Length != 20) throw new InvalidOperationException("Expected the existing 20 world-map destinations.");
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (var pond in ponds)
            {
                if (!ids.Add(pond.pondId) || pond.x <= 0 || pond.x >= 1 || pond.y <= 0 || pond.y >= 1)
                    throw new InvalidOperationException("Invalid world-map destination: " + pond.pondId);
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Library/CozyRedesignPreviews");
            // Use the existing full UI renderer, including saved clipping, marker prefabs and details panel.
            var render = typeof(CozyRedesignPreview).GetMethod("Render", BindingFlags.Static | BindingFlags.NonPublic);
            if (render == null) throw new MissingMethodException("CozyRedesignPreview.Render");
            render.Invoke(null, new object[] { "PanelWorldMap", null, null, 0f });
            File.WriteAllText("Library/CozyRedesignPreviews/world-map-verification.json",
                "{\"status\":\"PASS\",\"destinations\":20,\"width\":" + sprite.rect.width +
                ",\"height\":" + sprite.rect.height + ",\"aspectPreserved\":true}");
            Debug.Log("[WorldMapScene] PASS: 20 destinations, native image aspect, saved prefab and full UI render.");
        }
    }
}
#endif
