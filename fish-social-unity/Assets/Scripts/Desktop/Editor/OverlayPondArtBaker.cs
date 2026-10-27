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
using FishSocial.Desktop.Pet;
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
            public Module[] seats;
            public string defaultSeat;
        }
        [Serializable] public sealed class Profile { public string theme; public string image; public float shoreY; public string layerLayout; }
        [Serializable] public sealed class LayerLayout { public float width; public float height; public float groundY; public SceneLayer[] layers; }
        [Serializable] public sealed class SceneLayer
        {
            public string id;
            public string image;
            public float x, y, w, h;
            public int z;
        }
        [Serializable] public sealed class Binding
        {
            public string pondId, displayName, theme, seat;
            public int variant;
            // Optional per-level scenery; seats and gameplay still use the shared theme.
            public string image, layerLayout;
            public float shoreY;
        }
        [Serializable] public sealed class Module
        {
            public string name;
            public string label;
            public float width;
            // Normalized coordinates in the PNG, measured from its top-left corner.
            public float supportX;
            public float supportY;
            public float groundY;
        }

        static Catalog ReadCatalog()
        {
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(CatalogPath));
            if (catalog == null || catalog.bindings == null || catalog.profiles == null || catalog.modules == null)
                throw new InvalidOperationException("Missing pond art catalog.");
            return catalog;
        }

        static Profile ResolveProfile(Catalog catalog, Binding binding)
        {
            var shared = catalog.profiles.Single(p => p.theme == binding.theme);
            var custom = !string.IsNullOrEmpty(binding.image);
            return new Profile { theme = shared.theme,
                image = custom ? binding.image : shared.image,
                layerLayout = custom ? binding.layerLayout : shared.layerLayout,
                shoreY = binding.shoreY > 0 ? binding.shoreY : shared.shoreY };
        }

        [MenuItem("Fish Social/Overlay/应用场景资源并导出")]
        public static void ApplySceneryAndExport()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Close the Prefab Stage before applying scenery.");
            var catalog = ReadCatalog();
            // Validate every scene before modifying saved layouts.
            var profiles = catalog.bindings.Select(b => ResolveProfile(catalog, b)).ToArray();
            foreach (var profile in profiles)
                foreach (var layer in ReadLayerLayout(profile).layers)
                    if (!File.Exists(ArtFolder + "/" + layer.image))
                        throw new FileNotFoundException("Missing scene image", layer.image);
            foreach (var image in profiles.SelectMany(p => ReadLayerLayout(p).layers).Select(l => l.image).Distinct())
                ImportSprite(ArtFolder + "/" + image);
            foreach (var binding in catalog.bindings)
            {
                var profile = ResolveProfile(catalog, binding);
                var spec = catalog.seats.Single(s => s.name == binding.seat);
                var path = OverlayPondLayoutBaker.PrefabPath(binding.pondId);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var canvas = root.GetComponent<RectTransform>();
                    foreach (var item in root.GetComponentsInChildren<DesktopOverlayLayoutObject>(true))
                        if (item.kind == "sprite" && item.objectId != "pond-bg" && item.transform.parent == root.transform)
                            Object.DestroyImmediate(item.gameObject);
                    PlaceSceneLayers(root.transform, profile);
                    foreach (var spot in root.GetComponentsInChildren<DesktopOverlayLayoutObject>(true).Where(s => s.kind == "spot"))
                    {
                        var seat = spot.GetComponentsInChildren<DesktopOverlayLayoutObject>(true).Single(s => s.kind == "actor-seat").GetComponent<RectTransform>();
                        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, seat);
                        var ground = canvas.rect.height - bounds.min.y - seat.rect.height * (1 - spec.groundY);
                        // Move the whole actor cluster only; preserve its saved contact geometry and X position.
                        spot.GetComponent<RectTransform>().anchoredPosition += new Vector2(0, ground - SeatGroundY(profile));
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            var error = OverlayLayoutExporter.ExportAll(out var count, false);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            var resources = Path.GetFullPath(Path.Combine(OverlayLayoutExporter.LayoutOutputDir(), "..", "ponds"));
            foreach (var binding in catalog.bindings)
            {
                var profile = ResolveProfile(catalog, binding);
                var target = Path.Combine(resources, profile.layerLayout);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(ArtFolder + "/" + profile.layerLayout, target, true);
                // Keep the legacy per-pond fallback consistent with the selected complete panorama.
                if (ReadLayerLayout(profile).layers.Length == 1)
                    File.Copy(ArtFolder + "/" + profile.image, Path.Combine(resources, binding.pondId + ".png"), true);
            }
            Verify();
            RenderPreviews(catalog);
            CozyJournalResourcePreparation.RefreshPondPreviews();
            OverlayResourceSync.SyncExportedResources();
            Debug.Log("[PondArt] Applied per-level scenery to " + count + " saved layouts; actor contacts preserved.");
        }

        // Batch/authoring API retained; historical operation is not a daily menu command.
        public static void Apply()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var reopen = stage == null ? null : stage.assetPath;
            if (stage != null && stage.scene.isDirty)
                throw new InvalidOperationException("Unsaved Prefab Stage: " + reopen + ". Save or close it before applying pond art.");
            if (stage != null) StageUtility.GoToMainStage();
            try { ApplySavedAssets(); }
            finally { if (!string.IsNullOrEmpty(reopen)) PrefabStageUtility.OpenPrefab(reopen); }
        }

        static void ApplySavedAssets()
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
                AlignActorSeat(actor, catalog.seats.Single(s => s.name == catalog.defaultSeat));
                PrefabUtility.SaveAsPrefabAsset(actor, OverlayPondActorBaker.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(actor); }
            foreach (var file in Directory.GetFiles(ArtFolder, "*.png", SearchOption.AllDirectories))
                ImportSprite(file.Replace('\\', '/'));
            Directory.CreateDirectory(ModuleFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var module in catalog.modules)
                SaveModule(module);
            foreach (var seat in catalog.seats)
                SaveModule(seat, "seats");
            foreach (var binding in catalog.bindings)
                ApplyPond(binding, ResolveProfile(catalog, binding), catalog.seats.Single(s => s.name == binding.seat));
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
            foreach (var file in Directory.GetFiles(ArtFolder + "/seats", "*.png"))
            {
                var destination = Path.Combine(resources, "seats", Path.GetFileName(file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
            }
            File.Copy(ArtFolder + "/seats/" + catalog.defaultSeat + ".png", Path.Combine(resources, "seats", "_default.png"), true);
            Verify();
            RenderPreviews(catalog);
            Debug.Log("[PondArt] Applied V8 to " + exported + " pond layouts, including the tutorial pond.");
        }

        // Seat art can be approved independently from scenery. Preserve backgrounds,
        // decorations, horizontal spot positions and their existing references.
        [MenuItem("Fish Social/Overlay/应用座椅资源并导出")]
        public static void ApplySeatsAndExport()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                throw new InvalidOperationException("Close the Prefab Stage before applying seats.");
            var catalog = ReadCatalog();
            foreach (var spec in catalog.seats)
            {
                ImportSprite(ArtFolder + "/seats/" + spec.name + ".png");
                SaveModule(spec, "seats");
            }
            var defaultSpec = catalog.seats.Single(s => s.name == catalog.defaultSeat);
            File.Copy(ArtFolder + "/seats/" + defaultSpec.name + ".png",
                OverlayPondActorBaker.ArtFolder + "/seat-default.png", true);
            ImportSprite(OverlayPondActorBaker.ArtFolder + "/seat-default.png");
            var actor = PrefabUtility.LoadPrefabContents(OverlayPondActorBaker.PrefabPath);
            try
            {
                ApplySeat(actor, defaultSpec, OverlayPondActorBaker.ArtFolder + "/seat-default.png");
                PrefabUtility.SaveAsPrefabAsset(actor, OverlayPondActorBaker.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(actor); }
            foreach (var binding in catalog.bindings)
            {
                var path = OverlayPondLayoutBaker.PrefabPath(binding.pondId);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var spec = catalog.seats.Single(s => s.name == binding.seat);
                    var profile = ResolveProfile(catalog, binding);
                    var canvas = root.GetComponent<RectTransform>();
                    foreach (var spot in root.GetComponentsInChildren<DesktopOverlayLayoutObject>(true).Where(p => p.kind == "spot"))
                    {
                        spot.spriteFile = "seats/" + spec.name + ".png";
                        ApplySeat(spot.gameObject, spec, ArtFolder + "/seats/" + spec.name + ".png");
                        var seat = spot.GetComponentsInChildren<DesktopOverlayLayoutObject>(true).First(p => p.kind == "actor-seat").GetComponent<RectTransform>();
                        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, seat);
                        var ground = canvas.rect.height - bounds.min.y - seat.rect.height * (1 - spec.groundY);
                        spot.GetComponent<RectTransform>().anchoredPosition += new Vector2(0, ground - SeatGroundY(profile));
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            var error = OverlayLayoutExporter.ExportAll(out var exported, false);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            var resources = Path.GetFullPath(Path.Combine(OverlayLayoutExporter.LayoutOutputDir(), "..", "seats"));
            Directory.CreateDirectory(resources);
            foreach (var spec in catalog.seats)
                File.Copy(ArtFolder + "/seats/" + spec.name + ".png", Path.Combine(resources, spec.name + ".png"), true);
            File.Copy(ArtFolder + "/seats/" + defaultSpec.name + ".png", Path.Combine(resources, "_default.png"), true);
            Verify();
            RenderPreviews(catalog);
            Debug.Log("[PondArt] Seats only: updated and verified " + exported + " layouts.");
        }

        static void ApplySeat(GameObject actor, Module spec, string spritePath)
        {
            var marker = actor.GetComponentsInChildren<DesktopOverlayLayoutObject>(true).First(p => p.kind == "actor-seat");
            marker.spriteFile = "seats/" + spec.name + ".png";
            var image = marker.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (image.sprite == null) throw new InvalidOperationException("Missing seat " + spritePath);
            image.color = Color.white;
            image.preserveAspect = true;
            AlignActorSeat(actor, spec);
        }

        // A type shares a single layout file. Never place its cutouts by hand per level.
        public static void ApplyLayeredPondPilot()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Close the Prefab Stage before applying pond scenery.");
            var catalog = ReadCatalog();
            var profile = ResolveProfile(catalog, catalog.bindings.First(b => b.theme == "pond"));
            var layout = ReadLayerLayout(profile);
            foreach (var layer in layout.layers) ImportSprite(ArtFolder + "/" + layer.image);
            var selected = catalog.bindings.Where(b => b.theme == profile.theme).ToArray();
            if (selected.Length == 0) throw new InvalidOperationException("No pond bindings.");
            foreach (var binding in selected)
            {
                ApplyPond(binding, profile, catalog.seats.Single(s => s.name == binding.seat));
                var error = OverlayLayoutExporter.ExportPond(binding.pondId);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            AssetDatabase.SaveAssets();
            var resources = Path.GetFullPath(Path.Combine(OverlayLayoutExporter.LayoutOutputDir(), "..", "ponds"));
            var target = Path.Combine(resources, profile.layerLayout);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(ArtFolder + "/" + profile.layerLayout, target, true);
            Verify();
            catalog.bindings = selected;
            RenderPreviews(catalog);
            Debug.Log("[PondArt] Layered pond pilot: " + selected.Length + " level(s), " + layout.layers.Length + " shared layers.");
        }

        static float SeatGroundY(Profile profile) => string.IsNullOrEmpty(profile.layerLayout)
            ? profile.shoreY - 8 : ReadLayerLayout(profile).groundY;

        // Targeted scene update: no seat re-alignment and no changes to other ponds.
        public static void ApplyQiandaoAnimationAndExport()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Close the Prefab Stage before applying pond scenery.");
            var catalog = ReadCatalog();
            var binding = catalog.bindings.Single(b => b.pondId == "pond-calm");
            var profile = ResolveProfile(catalog, binding);
            foreach (var layer in ReadLayerLayout(profile).layers) ImportSprite(ArtFolder + "/" + layer.image);
            var path = OverlayPondLayoutBaker.PrefabPath(binding.pondId);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var item in root.GetComponentsInChildren<DesktopOverlayLayoutObject>(true))
                    if (item.kind == "sprite" && item.objectId != "pond-bg" && item.transform.parent == root.transform)
                        Object.DestroyImmediate(item.gameObject);
                PlaceSceneLayers(root.transform, profile);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            var error = OverlayLayoutExporter.ExportPond(binding.pondId);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            var resources = Path.GetFullPath(Path.Combine(OverlayLayoutExporter.LayoutOutputDir(), "..", "ponds"));
            var target = Path.Combine(resources, profile.layerLayout);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(ArtFolder + "/" + profile.layerLayout, target, true);
            var folder = Path.GetDirectoryName(ArtFolder + "/" + profile.layerLayout);
            File.Copy(Path.Combine(folder, "preview.png"), Path.Combine(resources, binding.pondId + ".png"), true);
            Verify();
            catalog.bindings = new[] { binding };
            RenderPreviews(catalog);
            OverlayResourceSync.SyncExportedResources();
            Debug.Log("[PondArt] Installed Qiandao animation layers; saved actor geometry preserved.");
        }

        // Applies only scenery sprites for the remaining ponds; saved seats and cat positions stay intact.
        public static void ApplyRepairedPastelScenesAndExport()
        {
            ApplyRemainingLayeredScenesAndExport();
            ImportUnassignedPastelSceneSprites();
            SyncPastelSceneBundles();
        }

        public static void RefreshPastelCloudMotionAndSync()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            SyncPastelSceneBundles();
        }

        static void SyncPastelSceneBundles()
        {
            // Candidate scenes already shipped in the resource pack must be refreshed too.
            var resources = Path.GetFullPath(Path.Combine(OverlayLayoutExporter.LayoutOutputDir(), "..", "ponds"));
            var sceneDirs = Directory.GetDirectories(ArtFolder + "/layered/pastel-scenes-v1");
            if (sceneDirs.Length != 30) throw new InvalidOperationException("Expected all 30 pastel scene bundles.");
            foreach (var sceneDir in sceneDirs)
            {
                var layout = JsonUtility.FromJson<LayerLayout>(File.ReadAllText(Path.Combine(sceneDir, "layout.json")));
                ValidateRepairedPastelLayers(layout, sceneDir);
                foreach (var layer in layout.layers)
                {
                    var destination = Path.Combine(resources, layer.image);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(ArtFolder + "/" + layer.image, destination, true);
                }
                var sky = layout.layers.Single(layer => layer.id == "pond-bg").image;
                var error = OverlayLayoutExporter.CopySceneMotion(ArtFolder + "/" + sky, Path.Combine(resources, sky));
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            OverlayResourceSync.SyncExportedResources();
            Debug.Log("[PondArt] Refreshed all 30 repaired scene bundles, including 14 unassigned candidates.");
        }

        public static void ApplyRemainingLayeredScenesAndExport()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Close the Prefab Stage before applying pond scenery.");
            var catalog = ReadCatalog();
            var selected = catalog.bindings.Where(b => b.pondId != "pond-calm").ToArray();
            var profiles = selected.Select(b => ResolveProfile(catalog, b)).ToArray();
            if (selected.Length != 20)
                throw new InvalidOperationException("Expected 20 pastel pond layouts.");
            foreach (var profile in profiles)
                ValidateRepairedPastelLayers(ReadLayerLayout(profile), profile.layerLayout);
            foreach (var image in profiles.SelectMany(p => ReadLayerLayout(p).layers).Select(l => l.image).Distinct())
                ImportSprite(ArtFolder + "/" + image);
            foreach (var binding in selected)
            {
                var profile = ResolveProfile(catalog, binding);
                var path = OverlayPondLayoutBaker.PrefabPath(binding.pondId);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var item in root.GetComponentsInChildren<DesktopOverlayLayoutObject>(true))
                        if (item.kind == "sprite" && item.objectId != "pond-bg" && item.transform.parent == root.transform)
                            Object.DestroyImmediate(item.gameObject);
                    PlaceSceneLayers(root.transform, profile);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            foreach (var binding in selected)
            {
                var error = OverlayLayoutExporter.ExportPond(binding.pondId);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            var resources = Path.GetFullPath(Path.Combine(OverlayLayoutExporter.LayoutOutputDir(), "..", "ponds"));
            foreach (var layout in profiles.Select(p => p.layerLayout).Distinct())
            {
                var target = Path.Combine(resources, layout);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(ArtFolder + "/" + layout, target, true);
            }
            Verify();
            catalog.bindings = selected;
            RenderPreviews(catalog);
            OverlayResourceSync.SyncExportedResources();
            Debug.Log("[PondArt] Installed animated scenery for " + selected.Length + " ponds; saved actor geometry preserved.");
        }

        public static void ImportUnassignedPastelSceneSprites()
        {
            var folder = ArtFolder + "/layered/pastel-scenes-v1";
            var used = new HashSet<string>(ReadCatalog().bindings
                .Where(b => b.pondId != "pond-calm")
                .Select(b => Path.GetFileName(Path.GetDirectoryName(b.image))));
            var candidates = Directory.GetDirectories(folder)
                .Where(dir => !used.Contains(Path.GetFileName(dir))).ToArray();
            if (candidates.Length != 14)
                throw new InvalidOperationException("Expected 14 unassigned pastel scenes.");
            foreach (var sceneDir in candidates)
            {
                var layoutFile = Path.Combine(sceneDir, "layout.json");
                var layout = JsonUtility.FromJson<LayerLayout>(File.ReadAllText(layoutFile));
                ValidateRepairedPastelLayers(layout, layoutFile);
                foreach (var layer in layout.layers)
                    ImportSprite(ArtFolder + "/" + layer.image);
            }
            AssetDatabase.SaveAssets();
            Verify();
            Debug.Log("[PondArt] Imported sprites for 14 unassigned pastel scenes.");
        }

        static void ValidateRepairedPastelLayers(LayerLayout layout, string source)
        {
            var expected = new[] { "pond-bg", "scene-celestial", "scene-cloud-far", "scene-cloud-middle",
                "scene-cloud-near", "scene-far", "scene-middle", "scene-foliage-mid", "scene-foliage-near",
                "scene-shore", "scene-water" };
            if (layout == null || layout.layers == null || layout.layers.Any(layer => layer == null)
                || !layout.layers.OrderBy(layer => layer.z).Select(layer => layer.id).SequenceEqual(expected))
                throw new InvalidOperationException("Expected repaired sky, fixed celestial, three clouds and separate foliage layers: " + source);
            foreach (var layer in layout.layers)
                if (!File.Exists(ArtFolder + "/" + layer.image))
                    throw new FileNotFoundException("Missing repaired scene layer", layer.image);
        }

        static LayerLayout ReadLayerLayout(Profile profile)
        {
            if (string.IsNullOrEmpty(profile.layerLayout)) throw new InvalidOperationException("Missing layer layout.");
            var layout = JsonUtility.FromJson<LayerLayout>(File.ReadAllText(ArtFolder + "/" + profile.layerLayout));
            if (layout == null || layout.layers == null || layout.width != Width || layout.height != Height
                || layout.groundY <= 0 || layout.groundY >= profile.shoreY)
                throw new InvalidOperationException("Invalid shared scene coordinates.");
            if (layout.layers.Count(l => l.id == "pond-bg" && l.image == profile.image) != 1
                || layout.layers.Select(l => l.id).Distinct().Count() != layout.layers.Length)
                throw new InvalidOperationException("Invalid layer IDs or background.");
            return layout;
        }

        static void PlaceSceneLayers(Transform root, Profile profile)
        {
            var layout = ReadLayerLayout(profile);
            // Water contact is independent of the seat landing line on dry ground.
            root.GetComponent<DesktopOverlayPondLayoutView>().waterlineY = profile.shoreY + 20;
            PlaceArrivalLane(root, layout.groundY);
            var sibling = 0;
            foreach (var layer in layout.layers.OrderBy(l => l.z))
            {
                var go = layer.id == "pond-bg" ? root.Find("pond-bg").gameObject
                    : new GameObject(layer.id, typeof(RectTransform), typeof(Image), typeof(DesktopOverlayLayoutObject));
                go.transform.SetParent(root, false);
                go.transform.SetSiblingIndex(sibling++);
                var image = go.GetComponent<Image>();
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + layer.image);
                if (image.sprite == null) throw new InvalidOperationException("Missing scene layer " + layer.image);
                image.color = Color.white;
                image.preserveAspect = false;
                image.raycastTarget = false;
                var marker = go.GetComponent<DesktopOverlayLayoutObject>();
                marker.objectId = layer.id;
                marker.kind = "sprite";
                marker.spriteFile = "ponds/" + layer.image;
                marker.anchor = "top-left";
                marker.zIndex = layer.z;
                OverlayPondLayoutBaker.PlaceTopLeft(go.GetComponent<RectTransform>(), layer.x, layer.y, layer.w, layer.h);
            }
        }

        static void PlaceArrivalLane(Transform root, float groundY)
        {
            var waiting = root.Find("waiting") as RectTransform;
            if (waiting == null) return;
            // WaitingLane uses the centre of this strip as the standing foot line.
            OverlayPondLayoutBaker.PlaceTopLeft(waiting, 80, groundY - 52, Width - 160, 64);
            waiting.GetComponent<DesktopOverlayLayoutObject>().anchor = "top-left";
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

        static void SaveModule(Module module, string sourceFolder = "modules")
        {
            var go = new GameObject(module.name, typeof(RectTransform), typeof(Image), typeof(DesktopOverlayLayoutObject));
            try
            {
                var image = go.GetComponent<Image>();
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + sourceFolder + "/" + module.name + ".png");
                image.color = Color.white;
                image.raycastTarget = false;
                image.preserveAspect = true;
                var size = image.sprite.rect.size;
                var scale = sourceFolder == "seats" && module.width > 0
                    ? module.width / size.x : Mathf.Min(240 / size.x, 140 / size.y);
                OverlayPondLayoutBaker.PlaceTopLeft(go.GetComponent<RectTransform>(), 0, 0, size.x * scale, size.y * scale);
                var marker = go.GetComponent<DesktopOverlayLayoutObject>();
                marker.objectId = module.name;
                marker.kind = "sprite";
                marker.spriteFile = (sourceFolder == "seats" ? "seats/" : "ponds/modules/") + module.name + ".png";
                marker.anchor = "top-left";
                marker.zIndex = 4;
                PrefabUtility.SaveAsPrefabAsset(go, ModuleFolder + "/" + module.name + ".prefab");
            }
            finally { Object.DestroyImmediate(go); }
        }

        static void ApplyPond(Binding binding, Profile profile, Module seatSpec)
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
                var selectedSeat = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/seats/" + binding.seat + ".png");
                if (selectedSeat == null) throw new InvalidOperationException("Missing seat " + binding.seat);
                var spotIds = DesktopGameData.GetSpotIds(binding.pondId);
                for (var index = 0; index < spotIds.Length; index++)
                {
                    var spotId = spotIds[index];
                    var spot = root.GetComponentsInChildren<DesktopOverlayLayoutObject>(true)
                        .Single(item => item.kind == "spot" && item.spotId == spotId);
                    spot.spriteFile = "seats/" + binding.seat + ".png";
                    var rt = spot.GetComponent<RectTransform>();
                    var x = spotIds.Length == 1 ? Width / 2 : Mathf.Lerp(84, Width - 84, index / (float)(spotIds.Length - 1));
                    OverlayPondLayoutBaker.PlaceTopLeft(rt, x - rt.rect.width / 2, 250, rt.rect.width, rt.rect.height);
                    var seat = spot.GetComponentsInChildren<DesktopOverlayLayoutObject>(true).First(item => item.kind == "actor-seat");
                    seat.spriteFile = "seats/" + binding.seat + ".png";
                    var seatImage = seat.GetComponent<Image>();
                    seatImage.sprite = selectedSeat;
                    seatImage.color = Color.white;
                    seatImage.preserveAspect = true;
                    var seatRect = seat.GetComponent<RectTransform>();
                    AlignActorSeat(spot.gameObject, seatSpec);
                    var seatHeight = seatRect.rect.height;
                    var seatBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, seat.transform);
                    var groundY = seatSpec.groundY > 0 ? seatSpec.groundY : 1;
                    var seatBottom = Height - seatBounds.min.y - seatHeight * (1 - groundY);
                    rt.anchoredPosition += new Vector2(0, seatBottom - SeatGroundY(profile));
                    var guide = spot.GetComponent<Image>();
                    if (guide != null) guide.color = new Color(1, 1, 1, 0);
                    var spotLabel = spot.transform.Find("Label");
                    if (spotLabel != null) spotLabel.gameObject.SetActive(false);
                }
                var waiting = root.transform.Find("waiting") as RectTransform;
                if (waiting != null) PlaceArrivalLane(root.transform, SeatGroundY(profile));
                foreach (var item in root.GetComponentsInChildren<DesktopOverlayLayoutObject>(true))
                {
                    if (item.spriteFile == "null") item.spriteFile = string.Empty;
                    if (item.kind == "actor-seat")
                    {
                        var seatImage = item.GetComponent<Image>();
                        seatImage.sprite = selectedSeat;
                        seatImage.color = Color.white;
                    }
                }
                if (!string.IsNullOrEmpty(profile.layerLayout)) PlaceSceneLayers(root.transform, profile);
                else PlaceDecoration(root.transform, binding, profile);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void AlignActorSeat(GameObject actor, Module spec)
        {
            var parts = actor.GetComponentsInChildren<DesktopOverlayLayoutObject>(true);
            var seat = parts.First(p => p.kind == "actor-seat").GetComponent<RectTransform>();
            var pet = parts.First(p => p.kind == "actor-pet").GetComponent<RectTransform>();
            // Keep the saved pet sprite; its canvas and the animation reference contain transparent padding.
            pet.GetComponent<Image>().color = Color.white;
            var sprite = seat.GetComponent<Image>().sprite;
            var width = spec.width > 0 ? spec.width : 85;
            var height = width * sprite.rect.height / sprite.rect.width;
            seat.sizeDelta = new Vector2(width, height);
            if (spec.supportY <= 0) return;
            var foot = FishingLayerArt.SeatContact("orange", pet.rect.width, pet.rect.height);
            var contact = seat.parent.InverseTransformPoint(pet.TransformPoint(new Vector3(
                pet.rect.xMin + foot.x, pet.rect.yMax - foot.y)));
            var parent = ((RectTransform)seat.parent).rect;
            OverlayPondLayoutBaker.PlaceTopLeft(seat,
                contact.x - parent.xMin - width * spec.supportX,
                parent.yMax - contact.y - height * spec.supportY, width, height);
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

        [MenuItem("Fish Social/校验/钓鱼场景与座位引用")]
        public static void Verify()
        {
            var catalog = ReadCatalog();
            var report = new List<string>();
            foreach (var binding in catalog.bindings)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPondLayoutBaker.PrefabPath(binding.pondId));
                if (asset == null) throw new InvalidOperationException("Missing pond " + binding.pondId);
                foreach (var node in asset.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) != 0)
                        throw new InvalidOperationException("Missing script " + binding.pondId + "/" + node.name);
                var bg = asset.transform.Find("pond-bg").GetComponent<Image>();
                var profile = ResolveProfile(catalog, binding);
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
                foreach (var seat in asset.GetComponentsInChildren<DesktopOverlayLayoutObject>(true).Where(p => p.kind == "actor-seat"))
                    if (seat.spriteFile != "seats/" + binding.seat + ".png" || seat.GetComponent<Image>().sprite == null)
                        throw new InvalidOperationException("Wrong seat " + binding.pondId);
                var spec = catalog.seats.Single(s => s.name == binding.seat);
                foreach (var spot in asset.GetComponentsInChildren<DesktopOverlayLayoutObject>(true).Where(p => p.kind == "spot"))
                    VerifySeatContact(spot.gameObject, spec);
                report.Add(binding.pondId + " | " + binding.theme + " | seat=" + binding.seat + " | spots=" + actual.Length + " | " + AssetDatabase.GetAssetPath(bg.sprite));
            }
            VerifySeatContact(AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPondActorBaker.PrefabPath),
                catalog.seats.Single(s => s.name == catalog.defaultSeat));
            File.WriteAllText("Library/PondArtV8.validation.txt", "PASS " + report.Count + " ponds\n" + string.Join("\n", report));
        }

        static void VerifySeatContact(GameObject actor, Module spec)
        {
            if (spec.supportY <= 0) return;
            var parts = actor.GetComponentsInChildren<DesktopOverlayLayoutObject>(true);
            var seat = parts.First(p => p.kind == "actor-seat").GetComponent<RectTransform>();
            var pet = parts.First(p => p.kind == "actor-pet").GetComponent<RectTransform>();
            if (pet.GetComponent<Image>().sprite == null || pet.GetComponent<Image>().color != Color.white)
                throw new InvalidOperationException("Invalid saved pet preview: " + actor.name);
            var actual = seat.TransformPoint(new Vector3(seat.rect.xMin + seat.rect.width * spec.supportX,
                seat.rect.yMax - seat.rect.height * spec.supportY));
            foreach (var skin in FishingLayerArt.SkinIds)
            {
                var foot = FishingLayerArt.SeatContact(skin, pet.rect.width, pet.rect.height);
                var expected = pet.TransformPoint(new Vector3(pet.rect.xMin + foot.x, pet.rect.yMax - foot.y));
                if (Vector3.Distance(actual, expected) > 0.1f)
                    throw new InvalidOperationException("Seat contact mismatch: " + actor.name + "/" + skin);
            }
        }

        static void RenderPreviews(Catalog catalog)
        {
            Directory.CreateDirectory("Library/PondArtPreviews");
            var previewedSeats = new HashSet<string>();
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
                    if (previewedSeats.Add(binding.seat))
                    {
                        var spots = root.GetComponentsInChildren<DesktopOverlayLayoutObject>(true).Where(p => p.kind == "spot").ToArray();
                        var selected = spots[spots.Length / 2];
                        foreach (var spot in spots) spot.gameObject.SetActive(spot == selected);
                        var seat = selected.GetComponentsInChildren<DesktopOverlayLayoutObject>(true).First(p => p.kind == "actor-seat");
                        var pet = selected.GetComponentsInChildren<DesktopOverlayLayoutObject>(true).First(p => p.kind == "actor-pet");
                        var bounds = new Bounds(seat.transform.position, Vector3.zero);
                        var corners = new Vector3[4];
                        foreach (var part in new[] { seat, pet })
                        {
                            part.GetComponent<RectTransform>().GetWorldCorners(corners);
                            foreach (var corner in corners) bounds.Encapsulate(corner);
                        }
                        camera.aspect = 1.5f;
                        camera.orthographicSize = Mathf.Max(bounds.size.y / 2 + 12, (bounds.size.x / 2 + 12) / camera.aspect);
                        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -100);
                        var closeup = new RenderTexture(600, 400, 24);
                        try
                        {
                            camera.targetTexture = closeup;
                            Canvas.ForceUpdateCanvases();
                            camera.Render();
                            RenderTexture.active = closeup;
                            var detail = new Texture2D(600, 400, TextureFormat.RGBA32, false);
                            detail.ReadPixels(new Rect(0, 0, 600, 400), 0, 0);
                            detail.Apply();
                            File.WriteAllBytes("Library/PondArtPreviews/" + binding.seat + ".png", detail.EncodeToPNG());
                            Object.DestroyImmediate(detail);
                            var renderer = pet.gameObject.AddComponent<SpriteFramePetRenderer>();
                            renderer.Bind(pet.GetComponent<Image>());
                            renderer.IsPlaying = false;
                            foreach (var clip in new[] { "ready", "fishing" })
                            {
                                if (!renderer.PlayClip("orange", clip))
                                    throw new InvalidOperationException("Cannot preview live fishing art: " + clip);
                                Canvas.ForceUpdateCanvases();
                                camera.Render();
                                RenderTexture.active = closeup;
                                var live = new Texture2D(600, 400, TextureFormat.RGBA32, false);
                                live.ReadPixels(new Rect(0, 0, 600, 400), 0, 0);
                                live.Apply();
                                File.WriteAllBytes("Library/PondArtPreviews/" + binding.seat + "-runtime-" + clip + ".png", live.EncodeToPNG());
                                Object.DestroyImmediate(live);
                            }
                        }
                        finally
                        {
                            camera.targetTexture = target;
                            RenderTexture.active = target;
                            closeup.Release();
                            Object.DestroyImmediate(closeup);
                        }
                    }
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
        }
    }
}
#endif
