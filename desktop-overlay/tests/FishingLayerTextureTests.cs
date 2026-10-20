#if FISHING_LAYER_TESTS
using System;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FishSocialOverlay;

public static class FishingLayerTextureTests
{
    [STAThread]
    public static void Main()
    {
        foreach (var value in new[] { "0", "-1", "3", "8", "2.0", "2e0", "2.5", "2147483648", "\"2\"", "null", "true", "[]" })
            Require(Parse(Manifest("test", value)) == null, "Rejected scale " + value);
        foreach (var value in new[] { null, "1", "2", "4" })
        {
            var scale = value == null ? 1 : int.Parse(value);
            var pack = Parse(Manifest("test", value));
            Require(pack != null, "Accepted scale " + value);
            Require((int)pack.GetType().GetProperty("BodyTextureScale").GetValue(pack) == scale, "Stored scale");
            CheckLayers(value, scale);
        }
        Console.WriteLine("PASS: fishing manifest compatibility, body/prop sizes, decode caps and missing-body fallback");
    }

    static void CheckLayers(string value, int scale)
    {
        var pet = "texture-test-" + Guid.NewGuid().ToString("N");
        var clip = pet;
        var root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OverlayResources", "fishing");
        var petFolder = Path.Combine(root, pet);
        var bodyFolder = Path.Combine(petFolder, "body", clip, "0");
        var sharedFolder = Path.Combine(root, "shared", clip, "0");
        Directory.CreateDirectory(bodyFolder);
        Directory.CreateDirectory(sharedFolder);
        try
        {
            File.WriteAllText(Path.Combine(petFolder, "manifest.json"), Manifest(clip, value));
            var bodyBytes = Png(640 * scale, 512 * scale);
            var propBytes = scale == 1 ? bodyBytes : Png(640, 512);
            foreach (var layer in OverlayFishingLayers.LayerNames)
            {
                var body = IsBody(layer);
                File.WriteAllBytes(Path.Combine(body ? bodyFolder : sharedFolder, layer + ".png"), body ? bodyBytes : propBytes);
            }
            Require(OverlayFishingLayers.TryOpen(pet, clip) != null, "Open complete pack");
            foreach (var layer in OverlayFishingLayers.LayerNames)
            {
                var path = OverlayFishingLayers.LayerFile(pet, clip, 0, layer);
                var bitmap = (BitmapSource)OverlayFishingLayers.BitmapFor(path, 20, 16);
                var multiplier = IsBody(layer) && scale > 1 ? 2 : 1;
                Require(bitmap != null && bitmap.PixelWidth == 20 * multiplier && bitmap.PixelHeight == 16 * multiplier,
                    "Decoded dimensions: " + layer);
            }
            var bodyPath = OverlayFishingLayers.LayerFile(pet, clip, 0, "body");
            if (scale == 2)
            {
                var capped = (BitmapSource)OverlayFishingLayers.BitmapFor(bodyPath, 1280, 1024);
                Require(capped.PixelWidth == 1280 && capped.PixelHeight == 1024, "Native cap");
            }
            OverlayFishingLayers.Place(pet, 512, 512, out var width, out var height, out var x, out var y);
            OverlayFishingLayers.Place("missing-texture-test", 512, 512,
                out var defaultWidth, out var defaultHeight, out var defaultX, out var defaultY);
            Require(width == defaultWidth && height == defaultHeight && x == defaultX && y == defaultY, "Logical placement");
            File.WriteAllBytes(bodyPath, scale > 1 ? propBytes : Png(1280, 1024));
            Require(OverlayFishingLayers.BitmapFor(bodyPath, 25, 20) == null, "Wrong body dimensions rejected");
            var propPath = OverlayFishingLayers.LayerFile(pet, clip, 0, "rod");
            File.WriteAllBytes(propPath, scale > 1 ? bodyBytes : Png(1280, 1024));
            Require(OverlayFishingLayers.BitmapFor(propPath, 25, 20) == null, "High-resolution prop rejected");
            File.Delete(bodyPath);
            Require(OverlayFishingLayers.TryOpen(pet, clip) == null, "Missing cached body rejected");
            var packs = (System.Collections.IDictionary)typeof(OverlayFishingLayers).GetField(
                "Packs", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            packs.Remove(pet);
            Require(OverlayFishingLayers.TryOpen(pet, clip) == null, "Missing body rejected on initial load");
        }
        finally
        {
            OverlayFishingLayers.SetDecodeSize(0, scale);
            Directory.Delete(petFolder, true);
            Directory.Delete(Path.Combine(root, "shared", clip), true);
        }
    }

    static bool IsBody(string layer) => layer == "body" || layer == "head" || layer == "far_hand" || layer == "hands";

    static object Parse(string json) => typeof(OverlayFishingLayers).GetMethod(
        "Parse", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { json });

    static string Manifest(string clip, string value)
    {
        var layers = "[\"" + string.Join("\",\"", OverlayFishingLayers.LayerNames) + "\"]";
        return "{\"version\":2,\"sourceVersion\":19,\"canvas\":[640,512],\"layers\":" + layers +
            ",\"bodyLayers\":[\"body\",\"head\",\"far_hand\",\"hands\"]" +
            (value == null ? "" : ",\"bodyTextureScale\":" + value) +
            ",\"clips\":{\"" + clip + "\":{\"playback\":\"hold\",\"durationsMs\":[100],\"frames\":[" + layers + "]}}}";
    }

    static byte[] Png(int width, int height)
    {
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null,
            new byte[width * height * 4], width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using (var stream = new MemoryStream())
        {
            encoder.Save(stream);
            return stream.ToArray();
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
#endif
