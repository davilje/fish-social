#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FishSocial.Desktop.Editor
{
    /// <summary>Applies the approved foreground-water art and exports the saved pond layouts.</summary>
    public static class OverlayPondArtBaker
    {
        public const string ArtFolder = "Assets/Desktop/OverlayArt/Ponds";
        const string CatalogPath = ArtFolder + "/catalog.json";
        const string ModuleFolder = ArtFolder + "/Prefabs";
        const float Width = 1680f;
        const float Height = 560f;

        [Serializable] public sealed class Catalog
        {
            public int version;
            public Profile[] profiles;
            public Binding[] bindings;
            public Module[] modules;
        }
        [Serializable] public sealed class Profile { public string theme; public string image; public float shoreY; }
        [Serializable] public sealed class Binding { public string pondId; public string displayName; public string theme; public int variant; }
        [Serializable] public sealed class Module { public string name; public string label; }

        static Catalog ReadCatalog()
        {
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(CatalogPath));
            if (catalog == null || catalog.bindings == null || catalog.profiles == null || catalog.modules == null)
                throw new InvalidOperationException("Missing pond art catalog.");
            return catalog;
        }

        [MenuItem("Fish Social/Pond Art/Apply V8 And Export")]
        public static void Apply()
        {
            var catalog = ReadCatalog();
            ImportSprite(OverlayPondActorBaker.ArtFolder + "/seat-default.png");
            var actor = PrefabUtility.LoadPrefabContents(OverlayPondActorBaker.PrefabPath);
            try
            {
                foreach (var item in actor.GetComponentsInChildren<DesktopOverlayLayoutObject>(true))
                {
                    if (item.spriteFile == "null") item.spriteFile = string.Empty;
                    if (item.kind != "actor-seat") continue;
                    var image = item.GetComponent<Image>();
                    image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(OverlayPondActorBaker.ArtFolder + "/seat-default.png");
                    image.color = Color.white;
                }
                PrefabUtility.SaveAsPrefabAsset(actor, OverlayPondActorBaker.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(actor); }
            foreach (var file in Directory.GetFiles(ArtFolder, "*.png", SearchOption.AllDirectories))
                ImportSprite(file.Replace('\\', '/'));
            Directory.CreateDirectory(ModuleFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var module in catalog.modules)
                SaveModule(module);
            foreach (var binding in catalog.bindings)
                ApplyPond(binding, catalog.profiles.Single(p => p.theme == binding.theme));
            AssetDatabase.SaveAssets();
            var error = OverlayLayoutExporter.ExportAll(out var exported, false);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            // All reusable modules are shipped, including modules not placed in a particular pond.
            var resources = Path.GetFullPath(Path.Combine(OverlayLayoutExporter.LayoutOutputDir(), ".."));
            foreach (var file in Directory.GetFiles(ArtFolder + "/modules", "*.png"))
            {
                var target = Path.Combine(resources, "ponds", "modules", Path.GetFileName(file));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
            Verify();
            RenderPreviews(catalog);
            Debug.Log("[PondArt] Applied V8 to " + exported + " pond layouts, including the tutorial pond.");
        }

        static void ImportSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Cannot import " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = Vector4.zero;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        static void SaveModule(Module module)
        {
            var go = new GameObject(module.name, typeof(RectTransform), typeof(Image), typeof(DesktopOverlayLayoutObject));
            try
            {
                var image = go.GetComponent<Image>();
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/modules/" + module.name + ".png");
                image.color = Color.white;
                image.raycastTarget = false;
                image.preserveAspect = true;
                var size = image.sprite.rect.size;
                var scale = Mathf.Min(240 / size.x, 140 / size.y);
                OverlayPondLayoutBaker.PlaceTopLeft(go.GetComponent<RectTransform>(), 0, 0, size.x * scale, size.y * scale);
                var marker = go.GetComponent<DesktopOverlayLayoutObject>();
                marker.objectId = module.name;
                marker.kind = "sprite";
                marker.spriteFile = "ponds/modules/" + module.name + ".png";
                marker.anchor = "top-left";
                marker.zIndex = 4;
                PrefabUtility.SaveAsPrefabAsset(go, ModuleFolder + "/" + module.name + ".prefab");
            }
            finally { Object.DestroyImmediate(go); }
        }

        static void ApplyPond(Binding binding, Profile profile)
        {
            var path = OverlayPondLayoutBaker.PrefabPath(binding.pondId);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var canvas = root.GetComponent<RectTransform>();
                canvas.sizeDelta = new Vector2(Width, Height);
                var bg = root.transform.Find("pond-bg");
                var image = bg.GetComponent<Image>();
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + profile.image);
                image.color = Color.white;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.raycastTarget = false;
                OverlayPondLayoutBaker.PlaceTopLeft(bg.GetComponent<RectTransform>(), 0, 0, Width, Height);
                var marker = bg.GetComponent<DesktopOverlayLayoutObject>();
                marker.spriteFile = "ponds/" + profile.image;
                marker.anchor = "top-left";
                marker.zIndex = 0;
                bg.SetAsFirstSibling();
                var label = bg.Find("Label");
                if (label != null) Object.DestroyImmediate(label.gameObject);
                // Retire prior generated decorations before placing the selected variant.
                foreach (var item in root.GetComponentsInChildren<DesktopOverlayLayoutObject>(true))
                    if (item.kind == "sprite" && item != marker && item.transform.parent == root.transform)
                        Object.DestroyImmediate(item.gameObject);
                var spotIds = DesktopGameData.GetSpotIds(binding.pondId);
                for (var index = 0; index < spotIds.Length; index++)
                {
                    var spotId = spotIds[index];
                    var spot = root.GetComponentsInChildren<DesktopOverlayLayoutObject>(true)
                        .Single(item => item.kind == "spot" && item.spotId == spotId);
                    var rt = spot.GetComponent<RectTransform>();
                    var x = spotIds.Length == 1 ? Width / 2 : Mathf.Lerp(84, Width - 84, index / (float)(spotIds.Length - 1));
                    OverlayPondLayoutBaker.PlaceTopLeft(rt, x - rt.rect.width / 2, 250, rt.rect.width, rt.rect.height);
                    var seat = spot.GetComponentsInChildren<DesktopOverlayLayoutObject>(true).First(item => item.kind == "actor-seat");
                    var seatBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, seat.transform);
                    var seatBottom = Height - seatBounds.min.y;
                    rt.anchoredPosition += new Vector2(0, seatBottom - (profile.shoreY - 8));
                    var guide = spot.GetComponent<Image>();
                    if (guide != null) guide.color = new Color(1, 1, 1, 0);
                    var spotLabel = spot.transform.Find("Label");
                    if (spotLabel != null) spotLabel.gameObject.SetActive(false);
                }
                var waiting = root.transform.Find("waiting") as RectTransform;
                if (waiting != null) OverlayPondLayoutBaker.PlaceTopLeft(waiting, 80, 260, Width - 160, 64);
                foreach (var item in root.GetComponentsInChildren<DesktopOverlayLayoutObject>(true))
                {
                    if (item.spriteFile == "null") item.spriteFile = string.Empty;
                    if (item.kind == "actor-seat")
                    {
                        var seatImage = item.GetComponent<Image>();
                        seatImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(OverlayPondActorBaker.ArtFolder + "/seat-default.png");
                        seatImage.color = Color.white;
                    }
                }
                PlaceDecoration(root.transform, binding, profile);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void PlaceDecoration(Transform root, Binding binding, Profile profile)
        {
            var module = binding.theme == "reservoir" ? "water-gauge" : binding.theme == "lake" ? "reeds"
                : binding.theme == "pond" ? "lotus" : binding.theme == "coastal" ? "mooring-post"
                : binding.theme == "river" ? "river-stones" : null;
            if (module == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModuleFolder + "/" + module + ".prefab"), root);
            go.name = "art-" + module;
            var marker = go.GetComponent<DesktopOverlayLayoutObject>();
            marker.objectId = go.name;
            marker.zIndex = 4;
            var sprite = go.GetComponent<Image>().sprite;
            var size = sprite.rect.size;
            var height = module == "lotus" || module == "river-stones" ? 30f : 52f;
            var width = size.x / size.y * height;
            var x = binding.variant == 0 ? 12 : binding.variant == 1 ? Width - width - 12 : Width * 0.49f;
            var y = module == "lotus" ? profile.shoreY + 34 : profile.shoreY - height - 20;
            OverlayPondLayoutBaker.PlaceTopLeft(go.GetComponent<RectTransform>(), x, y, width, height);
            go.transform.SetSiblingIndex(1);
        }

        [MenuItem("Fish Social/Pond Art/Validate V8 References")]
        public static void Verify()
        {
            var catalog = ReadCatalog();
            var report = new List<string>();
            foreach (var binding in catalog.bindings)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPondLayoutBaker.PrefabPath(binding.pondId));
                if (asset == null) throw new InvalidOperationException("Missing pond " + binding.pondId);
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(asset) != 0)
                    throw new InvalidOperationException("Missing script " + binding.pondId);
                var bg = asset.transform.Find("pond-bg").GetComponent<Image>();
                var profile = catalog.profiles.Single(p => p.theme == binding.theme);
                if (bg.sprite == null || AssetDatabase.GetAssetPath(bg.sprite) != ArtFolder + "/" + profile.image)
                    throw new InvalidOperationException("Wrong background " + binding.pondId);
                var expected = new HashSet<string>(DesktopGameData.GetSpotIds(binding.pondId));
                var actual = asset.GetComponentsInChildren<DesktopOverlayLayoutObject>(true).Where(p => p.kind == "spot").Select(p => p.spotId).ToArray();
                if (actual.Length != expected.Count || !expected.SetEquals(actual))
                    throw new InvalidOperationException("Invalid spot references " + binding.pondId);
                foreach (var image in asset.GetComponentsInChildren<Image>(true))
                    if (image.sprite != null && !File.Exists(AssetDatabase.GetAssetPath(image.sprite)))
                        throw new InvalidOperationException("Missing image " + binding.pondId + "/" + image.name);
                var export = Path.Combine(OverlayLayoutExporter.LayoutOutputDir(), binding.pondId + ".json");
                if (!File.Exists(export) || !File.ReadAllText(export).Contains("ponds/" + profile.image))
                    throw new InvalidOperationException("Missing mapped export " + binding.pondId);
                report.Add(binding.pondId + " | " + binding.theme + " | spots=" + actual.Length + " | " + AssetDatabase.GetAssetPath(bg.sprite));
            }
            File.WriteAllText("Library/PondArtV8.validation.txt", "PASS " + report.Count + " ponds\n" + string.Join("\n", report));
        }

        static void RenderPreviews(Catalog catalog)
        {
            Directory.CreateDirectory("Library/PondArtPreviews");
            var catTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Desktop/OverlayArt/orange/fishing-0.png");
            var cat = Sprite.Create(catTexture, new Rect(0, 0, catTexture.width, catTexture.height), new Vector2(0.5f, 0.5f));
            foreach (var binding in catalog.bindings)
            {
                var scene = EditorSceneManager.NewPreviewScene();
                var target = new RenderTexture((int)Width, (int)Height, 24);
                var previous = RenderTexture.active;
                try
                {
                    var cameraGo = new GameObject("PondArtCamera", typeof(Camera));
                    SceneManager.MoveGameObjectToScene(cameraGo, scene);
                    var camera = cameraGo.GetComponent<Camera>();
                    camera.scene = scene;
                    camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.92f, 0.95f, 0.90f, 1);
                    camera.orthographic = true;
                    camera.orthographicSize = Height / 2;
                    camera.transform.position = new Vector3(Width / 2, Height / 2, -100);
                    camera.targetTexture = target;
                    var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPondLayoutBaker.PrefabPath(binding.pondId)));
                    SceneManager.MoveGameObjectToScene(root, scene);
                    root.transform.position = Vector3.zero;
                    foreach (var item in root.GetComponentsInChildren<DesktopOverlayLayoutObject>(true))
                    {
                        if (item.kind == "waiting" || item.kind == "pet-size") item.gameObject.SetActive(false);
                        if (item.kind.StartsWith("actor-", StringComparison.Ordinal) && item.kind != "actor-seat" && item.kind != "actor-pet")
                            item.gameObject.SetActive(false);
                        if (item.kind == "actor-pet")
                        {
                            var image = item.GetComponent<Image>();
                            image.sprite = cat;
                            image.color = Color.white;
                        }
                    }
                    foreach (var text in root.GetComponentsInChildren<Text>(true)) text.gameObject.SetActive(false);
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    RenderTexture.active = target;
                    var pixels = new Texture2D((int)Width, (int)Height, TextureFormat.RGBA32, false);
                    pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes("Library/PondArtPreviews/" + binding.pondId + ".png", pixels.EncodeToPNG());
                    Object.DestroyImmediate(pixels);
                    camera.targetTexture = null;
                }
                finally
                {
                    RenderTexture.active = previous;
                    target.Release();
                    Object.DestroyImmediate(target);
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
            Object.DestroyImmediate(cat);
        }
    }
}
#endif
