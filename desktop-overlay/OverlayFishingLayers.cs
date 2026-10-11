using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FishSocialOverlay
{
    /// <summary>
    /// Orange fishing clips: seven full-canvas layers, one frame index, shared decode cache.
    /// Missing or invalid manifests fall back to the flat ART-03 sequence.
    /// </summary>
    static class OverlayFishingLayers
    {
        public const int CanvasWidth = 640;
        public const int CanvasHeight = 512;

        public static readonly string[] LayerNames =
        {
            "cat", "rod", "hands", "line", "bobber", "fish", "bait",
        };

        static readonly Dictionary<string, PetPack> Packs =
            new Dictionary<string, PetPack>(StringComparer.Ordinal);

        static readonly Dictionary<string, BitmapImage> Bitmaps =
            new Dictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);

        static readonly HashSet<string> Missing =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static readonly Dictionary<string, int> ClipUsers =
            new Dictionary<string, int>(StringComparer.Ordinal);

        static int _decodeW;
        static int _decodeH;

        public static FishingClipPlayback TryOpen(string petId, string clip)
        {
            if (!IsSafePetId(petId) || string.IsNullOrEmpty(clip))
                return null;

            var pack = LoadPack(petId);
            if (pack == null || !pack.Clips.TryGetValue(clip, out var playback))
                return null;
            if (!File.Exists(LayerPath(petId, clip, 0, "cat", pack.BodyLayers)))
                return null;
            return playback;
        }

        public static string LayerFile(string petId, string clip, int index, string layer)
        {
            string[] body = null;
            if (IsSafePetId(petId) && Packs.TryGetValue(petId, out var pack) && pack != null)
                body = pack.BodyLayers;
            if (body == null)
                body = new[] { "cat", "hands" };
            return LayerPath(petId, clip, index, layer, body);
        }

        public static string LayerPath(
            string petId, string clip, int index, string layer, IReadOnlyList<string> bodyLayers)
        {
            var body = false;
            if (bodyLayers != null)
            {
                for (var i = 0; i < bodyLayers.Count; i++)
                {
                    if (string.Equals(bodyLayers[i], layer, StringComparison.Ordinal))
                    {
                        body = true;
                        break;
                    }
                }
            }

            var root = FishingRoot();
            var folder = body
                ? Path.Combine(root, petId, "body", clip, index.ToString())
                : Path.Combine(root, "shared", clip, index.ToString());
            return Path.Combine(folder, layer + ".png");
        }

        public static void Acquire(string petId, string clip)
        {
            var key = UserKey(petId, clip);
            ClipUsers.TryGetValue(key, out var count);
            ClipUsers[key] = count + 1;
        }

        public static void Release(string petId, string clip)
        {
            var key = UserKey(petId, clip);
            if (!ClipUsers.TryGetValue(key, out var count))
                return;
            count -= 1;
            if (count > 0)
            {
                ClipUsers[key] = count;
                return;
            }

            ClipUsers.Remove(key);
            DropClip(clip);
        }

        public static void SetDecodeSize(int width, int height)
        {
            if (width == _decodeW && height == _decodeH)
                return;
            _decodeW = width;
            _decodeH = height;
            Bitmaps.Clear();
            Missing.Clear();
        }

        public static ImageSource BitmapFor(string path, int decodeW, int decodeH)
        {
            if (decodeW < 1 || decodeH < 1 || string.IsNullOrEmpty(path))
                return null;

            var key = decodeW + "x" + decodeH + "|" + path;
            if (Missing.Contains(key))
                return null;
            if (Bitmaps.TryGetValue(key, out var cached))
                return cached;
            if (!File.Exists(path))
            {
                Missing.Add(key);
                return null;
            }

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = decodeW;
                image.DecodePixelHeight = decodeH;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                Bitmaps[key] = image;
                return image;
            }
            catch (IOException)
            {
                Missing.Add(key);
                return null;
            }
            catch (NotSupportedException)
            {
                Missing.Add(key);
                return null;
            }
        }

        public static void Place(
            string petId,
            double slotW,
            double slotH,
            out int drawW,
            out int drawH,
            out double originX,
            out double originY)
        {
            var place = DefaultPlace;
            if (IsSafePetId(petId))
            {
                var pack = LoadPack(petId);
                if (pack != null)
                    place = pack.Place;
            }

            Place(place, slotW, slotH, out drawW, out drawH, out originX, out originY);
        }

        public static void Place(
            LayerPlace place,
            double slotW,
            double slotH,
            out int drawW,
            out int drawH,
            out double originX,
            out double originY)
        {
            if (slotW < 1 || slotH < 1 || place.BodyHeight < 1 || place.Reference < 1)
            {
                drawW = 1;
                drawH = 1;
                originX = 0;
                originY = 0;
                return;
            }

            var uniform = Math.Min(slotW, slotH) / place.Reference;
            var scale = uniform * place.ReferenceBody / place.BodyHeight;
            drawW = Math.Max(1, (int)Math.Round(CanvasWidth * scale));
            drawH = Math.Max(1, (int)Math.Round(CanvasHeight * scale));
            var scaleX = drawW / (double)CanvasWidth;
            var scaleY = drawH / (double)CanvasHeight;
            var padX = (slotW - uniform * place.Reference) * 0.5;
            var padY = (slotH - uniform * place.Reference) * 0.5;
            var footX = padX + uniform * place.ReferenceFootX;
            var footY = padY + uniform * place.ReferenceFootY;
            originX = footX - place.FootX * scaleX;
            originY = footY - place.FootY * scaleY;
        }

        static void DropClip(string clip)
        {
            var marker = Path.DirectorySeparatorChar + clip + Path.DirectorySeparatorChar;
            var stale = new List<string>();
            foreach (var key in Bitmaps.Keys)
            {
                if (key.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                    stale.Add(key);
            }

            foreach (var key in Missing)
            {
                if (key.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                    stale.Add(key);
            }

            for (var i = 0; i < stale.Count; i++)
            {
                Bitmaps.Remove(stale[i]);
                Missing.Remove(stale[i]);
            }
        }

        static PetPack LoadPack(string petId)
        {
            if (Packs.TryGetValue(petId, out var cached))
                return cached;

            PetPack pack = null;
            try
            {
                var path = Path.Combine(FishingRoot(), petId, "manifest.json");
                if (File.Exists(path))
                    pack = Parse(File.ReadAllText(path));
            }
            catch (IOException)
            {
                pack = null;
            }
            catch (JsonException)
            {
                pack = null;
            }
            catch (InvalidOperationException)
            {
                pack = null;
            }

            Packs[petId] = pack;
            return pack;
        }

        static PetPack Parse(string json)
        {
            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                if (!root.TryGetProperty("version", out var version) || version.GetInt32() != 1)
                    return null;
                if (!ReadNames(root, "layers", out var layers) || !SameNames(layers, LayerNames))
                    return null;
                if (!ReadNames(root, "bodyLayers", out var body) || body.Length == 0)
                    body = new[] { "cat", "hands" };
                for (var i = 0; i < body.Length; i++)
                {
                    if (IndexOf(LayerNames, body[i]) < 0)
                        return null;
                }

                if (!root.TryGetProperty("clips", out var clipsElement) ||
                    clipsElement.ValueKind != JsonValueKind.Object)
                    return null;

                var clips = new Dictionary<string, FishingClipPlayback>(StringComparer.Ordinal);
                foreach (var property in clipsElement.EnumerateObject())
                {
                    var playback = ParseClip(property.Value);
                    if (playback != null)
                        clips[property.Name] = playback;
                }

                var place = ReadPlace(root);
                return new PetPack(body, clips, place);
            }
        }

        static FishingClipPlayback ParseClip(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return null;
            if (!element.TryGetProperty("playback", out var playbackElement))
                return null;
            var playback = playbackElement.GetString();
            if (playback != "once" && playback != "hold" && playback != "intro-loop")
                return null;
            if (!element.TryGetProperty("frames", out var framesElement) ||
                framesElement.ValueKind != JsonValueKind.Array)
                return null;

            var orders = new List<string[]>();
            foreach (var frame in framesElement.EnumerateArray())
            {
                if (frame.ValueKind != JsonValueKind.Array)
                    return null;
                var order = new string[LayerNames.Length];
                var count = 0;
                foreach (var nameElement in frame.EnumerateArray())
                {
                    if (count >= order.Length)
                        return null;
                    var name = nameElement.GetString();
                    if (IndexOf(LayerNames, name) < 0 || IndexOf(order, name) >= 0)
                        return null;
                    order[count++] = name;
                }

                if (count != order.Length)
                    return null;
                orders.Add(order);
            }

            if (orders.Count == 0)
                return null;

            var loopStart = 0;
            if (element.TryGetProperty("loopStart", out var loopElement) &&
                loopElement.ValueKind == JsonValueKind.Number)
                loopStart = loopElement.GetInt32();
            if (playback == "intro-loop" && (loopStart < 0 || loopStart >= orders.Count))
                return null;

            return new FishingClipPlayback(playback, loopStart, orders);
        }

        static LayerPlace ReadPlace(JsonElement root)
        {
            var place = DefaultPlace;
            if (!root.TryGetProperty("place", out var element) ||
                element.ValueKind != JsonValueKind.Object)
                return place;

            if (TryPair(element, "foot", out var footX, out var footY))
            {
                place.FootX = footX;
                place.FootY = footY;
            }

            if (element.TryGetProperty("bodyHeight", out var body) &&
                body.ValueKind == JsonValueKind.Number &&
                body.GetDouble() > 1)
                place.BodyHeight = body.GetDouble();

            if (element.TryGetProperty("reference", out var reference) &&
                reference.ValueKind == JsonValueKind.Number &&
                reference.GetDouble() > 1)
                place.Reference = reference.GetDouble();

            if (TryPair(element, "referenceFoot", out var refX, out var refY))
            {
                place.ReferenceFootX = refX;
                place.ReferenceFootY = refY;
            }

            if (element.TryGetProperty("referenceBody", out var refBody) &&
                refBody.ValueKind == JsonValueKind.Number &&
                refBody.GetDouble() > 1)
                place.ReferenceBody = refBody.GetDouble();

            return place;
        }

        static bool TryPair(JsonElement parent, string name, out double x, out double y)
        {
            x = 0;
            y = 0;
            if (!parent.TryGetProperty(name, out var element) ||
                element.ValueKind != JsonValueKind.Array)
                return false;
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number)
                    return false;
                if (index == 0)
                    x = item.GetDouble();
                else if (index == 1)
                    y = item.GetDouble();
                index++;
            }

            return index >= 2;
        }

        static bool ReadNames(JsonElement root, string property, out string[] names)
        {
            names = null;
            if (!root.TryGetProperty(property, out var element) ||
                element.ValueKind != JsonValueKind.Array)
                return false;
            var list = new List<string>();
            foreach (var item in element.EnumerateArray())
            {
                var name = item.GetString();
                if (string.IsNullOrEmpty(name))
                    return false;
                list.Add(name);
            }

            names = list.ToArray();
            return names.Length > 0;
        }

        static bool SameNames(string[] left, string[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (var i = 0; i < left.Length; i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        static int IndexOf(IReadOnlyList<string> names, string name)
        {
            if (names == null || name == null)
                return -1;
            for (var i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], name, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        static string FishingRoot()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OverlayResources", "fishing");
        }

        static string UserKey(string petId, string clip)
        {
            return petId + "|" + clip;
        }

        static bool IsSafePetId(string petId)
        {
            if (string.IsNullOrEmpty(petId))
                return false;
            for (var i = 0; i < petId.Length; i++)
            {
                var c = petId[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_'))
                    return false;
            }

            return true;
        }

        static readonly LayerPlace DefaultPlace = new LayerPlace
        {
            FootX = 264,
            FootY = 450,
            BodyHeight = 257,
            Reference = 512,
            ReferenceFootX = 138.5,
            ReferenceFootY = 404,
            ReferenceBody = 279,
        };

        sealed class PetPack
        {
            public PetPack(
                string[] bodyLayers,
                Dictionary<string, FishingClipPlayback> clips,
                LayerPlace place)
            {
                BodyLayers = bodyLayers;
                Clips = clips;
                Place = place;
            }

            public string[] BodyLayers { get; }
            public Dictionary<string, FishingClipPlayback> Clips { get; }
            public LayerPlace Place { get; }
        }
    }

    struct LayerPlace
    {
        public double FootX;
        public double FootY;
        public double BodyHeight;
        public double Reference;
        public double ReferenceFootX;
        public double ReferenceFootY;
        public double ReferenceBody;
    }

    sealed class FishingClipPlayback
    {
        public FishingClipPlayback(string playback, int loopStart, List<string[]> orders)
        {
            Playback = playback;
            LoopStart = loopStart;
            Orders = orders;
        }

        public string Playback { get; }
        public int LoopStart { get; }
        public List<string[]> Orders { get; }
        public int FrameCount => Orders.Count;
    }
}
