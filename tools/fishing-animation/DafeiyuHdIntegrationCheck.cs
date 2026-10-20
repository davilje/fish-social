using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Pet;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DafeiyuHdIntegrationCheck
{
    const string Project = "/private/tmp/fish-dafeiyu-integration";
    static readonly int[] Counts = { 1, 4, 1, 4, 4, 1, 4, 6, 10, 6, 7, 4, 6, 4, 8, 4 };

    [Serializable]
    sealed class ManifestInfo
    {
        public int bodyTextureScale;
        public int[] canvas;
    }

    [Serializable]
    sealed class Report
    {
        public string result;
        public string error;
        public string unityVersion;
        public string manifestSha256;
        public int clips;
        public int slots;
        public int layerChecks;
        public int warmupSlots;
        public int legacyCats;
        public bool nativeCap;
        public bool logicalPlacement;
        public bool invalidSourcesRejected;
        public bool portrait512;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static bool IsBody(string layer)
    {
        return layer == "body" || layer == "head" || layer == "far_hand" || layer == "hands";
    }

    static int CheckSession(FishingLayerArt.Session session, int count, int width, int height, int scale)
    {
        Require(session != null && session.FrameCount == count, "Session frame count");
        for (var frame = 0; frame < count; frame++)
        {
            Require(session.Order(frame).Distinct().Count() == 9, "Nine unique ordered layers");
            Require(session.DurationMs(frame) > 0, "Positive frame duration");
            foreach (var layer in FishingLayerArt.LayerNames)
            {
                var sprite = session.Frame(frame, layer);
                Require(sprite != null, "Missing sprite: " + session.Key + "/" + frame + "/" + layer);
                var hd = IsBody(layer) && scale > 1;
                var expectedWidth = hd ? Math.Min(640 * scale, width * 2) : width;
                var expectedHeight = hd ? Math.Min(512 * scale, height * 2) : height;
                Require(sprite.texture.width == expectedWidth && sprite.texture.height == expectedHeight,
                    "Texture size: " + session.Key + "/" + frame + "/" + layer);
                Require(sprite.rect.size == new Vector2(expectedWidth, expectedHeight), "Full texture sprite rect");
            }
        }
        return count * 9;
    }

    static void CheckPlacement()
    {
        var previous = DesktopProfileCache.Latest;
        var root = new GameObject("DafeiyuHdLayoutCheck", typeof(RectTransform), typeof(Image));
        try
        {
            DesktopProfileCache.Latest = new PlayerProfileDto { avatarUrl = "/image/profile/cat_avatar_dafeiyu.png" };
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(256, 256);
            var renderer = root.AddComponent<SpriteFramePetRenderer>();
            renderer.IsPlaying = false;
            renderer.Bind(root.GetComponent<Image>());
            Require(renderer.PlayClip("dafeiyu", "hooked"), "Actual renderer accepts HD clip");
            FishingLayerArt.Place("dafeiyu", 256, 256, out var width, out var height, out var x, out var y);
            var images = (Image[])typeof(SpriteFramePetRenderer).GetField(
                "_layerImages", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(renderer);
            for (var frame = 0; frame < 10; frame++)
            {
                foreach (var image in images)
                {
                    Require(image.rectTransform.sizeDelta == new Vector2(width, height), "Logical Image dimensions");
                    Require(image.rectTransform.anchoredPosition == new Vector2(x, -y), "Stable logical placement");
                }
                renderer.StepFrame();
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            DesktopProfileCache.Latest = previous;
        }
    }

    public static void Run()
    {
        Require(Path.GetFullPath(Application.dataPath) == Project + "/Assets", "Only the isolated project is allowed");
        var report = new Report { result = "failed", unityVersion = Application.unityVersion };
        try
        {
            var fishing = Path.Combine(Application.streamingAssetsPath, "Fishing");
            var manifestPath = Path.Combine(fishing, "dafeiyu/manifest.json");
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonUtility.FromJson<ManifestInfo>(json);
            Require(manifest.bodyTextureScale == 2 && manifest.canvas.SequenceEqual(new[] { 640, 512 }), "Installed HD manifest required");
            Require(FishingLayerArt.ValidateManifest(json, out var manifestError), manifestError);
            using (var sha = SHA256.Create())
                report.manifestSha256 = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(manifestPath))).Replace("-", "").ToLowerInvariant();
            foreach (var value in new[] { "0", "3", "8", "2.5", "\"2\"", "null" })
                Require(!FishingLayerArt.ValidateManifest(Regex.Replace(json, "(\"bodyTextureScale\"\\s*:\\s*)2", "${1}" + value), out _), "Reject manifest scale " + value);
            Require(FishingLayerArt.ClipNames.Length == 16, "Sixteen clips");
            for (var clipIndex = 0; clipIndex < FishingLayerArt.ClipNames.Length; clipIndex++)
            {
                var clip = FishingLayerArt.ClipNames[clipIndex];
                var session = FishingLayerArt.Open("dafeiyu", clip, 173, 139);
                try { report.layerChecks += CheckSession(session, Counts[clipIndex], 173, 139, 2); }
                finally { FishingLayerArt.Release(session); }
                report.clips++;
                report.slots += Counts[clipIndex];
                for (var step = 0; step < Counts[clipIndex] * 9; step++)
                    Require(FishingLayerArt.WarmupSlice("dafeiyu", clip, 174, 140) == (step == Counts[clipIndex] * 9 - 1), "Warmup completes after all layers");
                session = FishingLayerArt.TryGetCached("dafeiyu", clip, 174, 140);
                try { report.layerChecks += CheckSession(session, Counts[clipIndex], 174, 140, 2); }
                finally { while (session != null && session.Users > 0) FishingLayerArt.Release(session); }
                report.warmupSlots += Counts[clipIndex];
            }
            Require(report.slots == 74 && report.warmupSlots == 74, "All 74 slots load and warm");
            var capped = FishingLayerArt.Open("dafeiyu", "idle", 900, 720);
            try { CheckSession(capped, 1, 900, 720, 2); report.nativeCap = true; }
            finally { FishingLayerArt.Release(capped); }
            foreach (var skin in FishingLayerArt.SkinIds.Where(skin => skin != "dafeiyu"))
            {
                var session = FishingLayerArt.Open(skin, "idle", 173, 139);
                try { CheckSession(session, 1, 173, 139, 1); report.legacyCats++; }
                finally { FishingLayerArt.Release(session); }
            }
            Require(report.legacyCats == 9, "Nine legacy cats");
            var load = typeof(FishingLayerArt).GetMethod("LoadSprite", BindingFlags.NonPublic | BindingFlags.Static);
            Require(load.Invoke(null, new object[] { Path.Combine(fishing, "shared/idle/0/rod.png"), 173, 139, 2 }) == null, "Reject low-resolution HD body");
            Require(load.Invoke(null, new object[] { Path.Combine(fishing, "dafeiyu/body/idle/0/body.png"), 173, 139, 1 }) == null, "Reject HD prop dimensions");
            Require(load.Invoke(null, new object[] { Path.Combine(fishing, "missing-body.png"), 173, 139, 2 }) == null, "Reject missing HD body");
            report.invalidSourcesRejected = true;
            CheckPlacement();
            report.logicalPlacement = true;
            var portrait = DesktopDefaultAvatars.Portrait("/image/profile/cat_avatar_dafeiyu.png");
            Require(portrait != null && AssetDatabase.GetAssetPath(portrait).EndsWith("/dafeiyu.png"), "Independent Dafeiyu portrait");
            Require(portrait.texture.width == 512 && portrait.texture.height == 512, "512-pixel portrait");
            report.portrait512 = true;
            report.result = "passed";
        }
        catch (Exception error)
        {
            report.error = error.ToString();
            Debug.LogException(error);
        }
        File.WriteAllText(Project + "/hd-result.json", JsonUtility.ToJson(report, true));
        EditorApplication.Exit(report.result == "passed" ? 0 : 1);
    }
}
