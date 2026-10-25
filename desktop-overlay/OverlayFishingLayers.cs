using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FishSocialOverlay
{
    /// <summary>
    /// Approved fishing clips: nine full-canvas layers and per-frame timings.
    /// Missing or invalid manifests use the current portrait.
    /// </summary>
    static class OverlayFishingLayers
    {
        public const int CanvasWidth = 640;
        public const int CanvasHeight = 512;

        public static readonly string[] LayerNames =
        {
            "body", "head", "far_hand", "rod", "line", "fish", "hands", "bobber", "bait",
        };

        static readonly Dictionary<string, PetPack> Packs =
            new Dictionary<string, PetPack>(StringComparer.Ordinal);

        // Budget applies to decoded pixel payload, not PNG file size or total process memory.
        // Active resources are pinned; a temporary overrun is trimmed when their users leave.
        static readonly OverlayResourceCache<BitmapImage> Bitmaps =
            new OverlayResourceCache<BitmapImage>(64L * 1024 * 1024);

        static readonly Dictionary<string, int> TextureScales =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        static readonly HashSet<string> Missing =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static readonly Dictionary<string, int> ClipUsers =
            new Dictionary<string, int>(StringComparer.Ordinal);

        static readonly Dictionary<string, string[]> ClipResources =
            new Dictionary<string, string[]>(StringComparer.Ordinal);
        static readonly Dictionary<string, string> SleepHeads = new Dictionary<string, string>(StringComparer.Ordinal);

        internal static long CachedPixelBytes => Bitmaps.ResidentBytes;
        internal static int CachedBitmapCount => Bitmaps.Count;
        internal static int ActiveClipCount => ClipUsers.Count;
        internal static long DecodeCount { get; private set; }

        // These dictionaries and the LRU belong exclusively to the dispatcher.
        // Workers only read files and return immutable packs / frozen bitmaps.
        static readonly OverlayDecodeQueue DecodeQueue = new OverlayDecodeQueue();
        static readonly Dictionary<string, Task<PetPack>> PendingPacks = new Dictionary<string, Task<PetPack>>();
        sealed class PendingImage
        {
            public Task<BitmapImage> Task;
            public readonly CancellationTokenSource Cancel = new CancellationTokenSource();
            public int Users;
        }
        static readonly Dictionary<string, PendingImage> PendingImages = new Dictionary<string, PendingImage>(StringComparer.OrdinalIgnoreCase);
        internal static int PendingDecodes => DecodeQueue.Pending;
        internal static long RejectedDecodes { get; private set; }

        internal static bool PackIsLoaded(string petId) => Packs.ContainsKey(petId);

        public static async Task<bool> EnsurePackAsync(string petId)
        {
            System.Windows.Application.Current?.Dispatcher.VerifyAccess();
            if (!IsSafePetId(petId)) return false;
            if (Packs.TryGetValue(petId, out var pack)) return pack != null;
            if (!PendingPacks.TryGetValue(petId, out var pending))
            {
                pending = DecodeQueue.TryRun(() => ReadPack(petId));
                if (pending == null) { RejectedDecodes++; return false; }
                PendingPacks[petId] = pending;
            }
            pack = await pending;
            PendingPacks.Remove(petId);
            if (!Packs.ContainsKey(petId)) PublishPack(petId, pack);
            return pack != null;
        }

        public static FishingClipPlayback TryOpen(string petId, string clip)
        {
            if (!IsSafePetId(petId) || !IsSafePetId(clip))
                return null;

            var pack = LoadPack(petId);
            if (pack == null || !pack.Clips.TryGetValue(clip, out var playback))
                return null;
            return playback;
        }

        public static string LayerFile(string petId, string clip, int index, string layer)
        {
            string[] body = null;
            if (IsSafePetId(petId) && Packs.TryGetValue(petId, out var pack) && pack != null)
                body = pack.BodyLayers;
            if (body == null)
                body = new[] { "body", "head", "far_hand", "hands" };
            return LayerPath(petId, clip, index, layer, body);
        }

        public static string SleepHead(string petId)
        {
            return petId != null && SleepHeads.TryGetValue(petId, out var path) ? path : null;
        }

        public static string LayerPath(
            string petId, string clip, int index, string layer, IReadOnlyList<string> bodyLayers)
        {
            if (Packs.TryGetValue(petId, out var pack) && pack != null && pack.Clips.TryGetValue(clip, out var playback))
                index = playback.SourceIndex(index);
            return SourceLayerPath(petId, clip, index, layer, bodyLayers);
        }

        static string SourceLayerPath(string petId, string clip, int index, string layer, IReadOnlyList<string> bodyLayers)
        {
            // Borrow through the normal cache/retain path, even if water effects are off.
            if (layer == "bobber" && OverlayFishingWaterContact.IsSubmergedClip(clip))
            { clip = "fishing"; index = 0; }
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
            if (count == 0)
            {
                var pack = LoadPack(petId);
                if (pack == null || !pack.Clips.TryGetValue(clip, out var playback)) return;
                if (!ClipResources.TryGetValue(key, out var resources))
                {
                    var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    for (var frame = 0; frame < playback.FrameCount; frame++)
                        foreach (var layer in LayerNames)
                            paths.Add(LayerPath(petId, clip, frame, layer, pack.BodyLayers));
                    if (clip == "fishing" && SleepHead(petId) != null) paths.Add(SleepHead(petId));
                    resources = new string[paths.Count];
                    paths.CopyTo(resources);
                    ClipResources[key] = resources;
                }
                // Includes the shared prop paths, protected by all species using this clip.
                Bitmaps.Retain(resources);
            }
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
            if (ClipResources.TryGetValue(key, out var resources))
                Bitmaps.Release(resources);
        }

        static bool DecodeKey(string path, ref int width, ref int height, out int scale, out string key)
        {
            scale = 1;
            key = null;
            if (width < 1 || height < 1 || string.IsNullOrEmpty(path) ||
                !TextureScales.TryGetValue(path, out scale)) return false;
            if (scale > 1)
            {
                width = (int)Math.Min(CanvasWidth * scale, (long)width * 2);
                height = (int)Math.Min(CanvasHeight * scale, (long)height * 2);
            }
            key = width + "x" + height + "|" + path;
            return true;
        }

        // Synchronous reference path for the native resource tests. Actor rendering
        // exclusively uses BitmapAsync; never call this from a frame/state handler.
        internal static ImageSource BitmapFor(string path, int decodeW, int decodeH)
        {
            if (!DecodeKey(path, ref decodeW, ref decodeH, out var scale, out var key) || Missing.Contains(key)) return null;
            if (Bitmaps.TryGet(key, out var cached)) return cached;
            return PublishBitmap(key, path, DecodeBitmap(path, decodeW, decodeH, scale));
        }

        public static async Task<BitmapImage> BitmapAsync(string path, int decodeW, int decodeH, CancellationToken token = default)
        {
            System.Windows.Application.Current?.Dispatcher.VerifyAccess();
            if (token.IsCancellationRequested || !DecodeKey(path, ref decodeW, ref decodeH, out var scale, out var key) || Missing.Contains(key)) return null;
            if (Bitmaps.TryGet(key, out var cached)) return cached;
            if (!PendingImages.TryGetValue(key, out var pending))
            {
                pending = new PendingImage();
                pending.Task = DecodeQueue.TryRun(() => DecodeBitmap(path, decodeW, decodeH, scale), pending.Cancel.Token);
                if (pending.Task == null) { pending.Cancel.Dispose(); RejectedDecodes++; return null; }
                PendingImages[key] = pending;
            }
            pending.Users++;
            try
            {
                var image = await OverlayTaskWait.WithCancellation(pending.Task, token);
                // One publication, even if several actors await the same resource.
                if (PendingImages.TryGetValue(key, out var current) && ReferenceEquals(current, pending))
                {
                    PendingImages.Remove(key);
                    PublishBitmap(key, path, image);
                }
                return image;
            }
            catch (OperationCanceledException) { return null; }
            finally
            {
                if (--pending.Users == 0)
                {
                    if (!pending.Task.IsCompleted) pending.Cancel.Cancel();
                    if (PendingImages.TryGetValue(key, out var current) && ReferenceEquals(current, pending))
                        PendingImages.Remove(key);
                    pending.Cancel.Dispose();
                }
            }
        }

        static BitmapImage PublishBitmap(string key, string path, BitmapImage image)
        {
            System.Windows.Application.Current?.Dispatcher.VerifyAccess();
            if (image == null) { Missing.Add(key); return null; }
            DecodeCount++;
            var stride = ((long)image.PixelWidth * image.Format.BitsPerPixel + 7) / 8;
            Bitmaps.Add(key, path, image, stride * image.PixelHeight);
            return image;
        }

        static BitmapImage DecodeBitmap(string path, int width, int height, int scale)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    if (decoder.Frames[0].PixelWidth != CanvasWidth * scale ||
                        decoder.Frames[0].PixelHeight != CanvasHeight * scale) return null;
                    stream.Position = 0;
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = width;
                    image.DecodePixelHeight = height;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (ArgumentException) { return null; }
            catch (FormatException) { return null; }
            catch (InvalidOperationException) { return null; }
            catch (System.Runtime.InteropServices.COMException) { return null; }
        }

        public static Point FootInSlot(string petId, double width, double height)
        {
            var place = DefaultPlace;
            if (petId != null && Packs.TryGetValue(petId, out var pack) && pack != null)
                place = pack.Place;
            var unit = Math.Min(width, height) / place.Reference;
            return new Point((width - unit * place.Reference) * .5 + unit * place.ReferenceFootX,
                (height - unit * place.Reference) * .5 + unit * place.ReferenceFootY);
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
                if (Packs.TryGetValue(petId, out var pack) && pack != null)
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

        static PetPack LoadPack(string petId)
        {
            if (Packs.TryGetValue(petId, out var cached))
                return cached;

            var pack = ReadPack(petId);
            PublishPack(petId, pack);
            return pack;
        }

        static PetPack ReadPack(string petId)
        {
            PetPack pack = null;
            try
            {
                var path = Path.Combine(FishingRoot(), petId, "manifest.json");
                if (File.Exists(path))
                    pack = Parse(File.ReadAllText(path));
                if (pack != null)
                {
                    foreach (var pair in pack.Clips)
                    {
                        for (var index = 0; index < pair.Value.FrameCount; index++)
                        {
                            foreach (var layer in LayerNames)
                            {
                                if (!File.Exists(SourceLayerPath(petId, pair.Key, pair.Value.SourceIndex(index), layer, pack.BodyLayers)))
                                    return null;
                            }
                        }
                    }
                }
            }
            catch (IOException)
            {
                pack = null;
            }
            catch (UnauthorizedAccessException)
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
            catch (FormatException)
            {
                pack = null;
            }
            catch (OverflowException)
            {
                pack = null;
            }

            return pack;
        }

        static void PublishPack(string petId, PetPack pack)
        {
            System.Windows.Application.Current?.Dispatcher.VerifyAccess();
            if (pack != null)
            {
                var sleepHead = Path.Combine(FishingRoot(), petId, "body", "fishing", "0", "head-sleep.png");
                if (File.Exists(sleepHead))
                {
                    SleepHeads[petId] = sleepHead;
                    TextureScales[sleepHead] = pack.BodyTextureScale;
                }
                foreach (var pair in pack.Clips)
                {
                    for (var index = 0; index < pair.Value.FrameCount; index++)
                    {
                        foreach (var layer in LayerNames)
                            TextureScales[SourceLayerPath(petId, pair.Key, pair.Value.SourceIndex(index), layer, pack.BodyLayers)] =
                                IndexOf(pack.BodyLayers, layer) >= 0 ? pack.BodyTextureScale : 1;
                    }
                }
            }
            Packs[petId] = pack;
        }

        static PetPack Parse(string json)
        {
            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                if (!root.TryGetProperty("version", out var version) || version.GetInt32() != 2)
                    return null;
                if (!root.TryGetProperty("sourceVersion", out var sourceVersion) || sourceVersion.GetInt32() != 19)
                    return null;
                if (!root.TryGetProperty("canvas", out var canvas) || canvas.ValueKind != JsonValueKind.Array
                    || canvas.GetArrayLength() != 2 || canvas[0].GetInt32() != 640 || canvas[1].GetInt32() != 512)
                    return null;
                if (!ReadNames(root, "layers", out var layers) || !SameNames(layers, LayerNames))
                    return null;
                if (!ReadNames(root, "bodyLayers", out var body)
                    || !SameNames(body, new[] { "body", "head", "far_hand", "hands" }))
                    return null;
                var bodyTextureScale = 1;
                if (root.TryGetProperty("bodyTextureScale", out var textureScale) &&
                    (textureScale.ValueKind != JsonValueKind.Number || !textureScale.TryGetInt32(out bodyTextureScale) ||
                    (bodyTextureScale != 1 && bodyTextureScale != 2 && bodyTextureScale != 4)))
                    return null;

                if (!root.TryGetProperty("clips", out var clipsElement) ||
                    clipsElement.ValueKind != JsonValueKind.Object)
                    return null;

                var clips = new Dictionary<string, FishingClipPlayback>(StringComparer.Ordinal);
                foreach (var property in clipsElement.EnumerateObject())
                {
                    var playback = ParseClip(property.Value);
                    if (playback == null || !IsSafePetId(property.Name) || clips.ContainsKey(property.Name))
                        return null;
                    playback.ApplyMotion(OverlayFishingMotion.Find(property.Name, playback.DurationsMs));
                    clips[property.Name] = playback;
                }
                foreach (var clip in clips.Values)
                {
                    if (!string.IsNullOrEmpty(clip.NextClip) && !clips.ContainsKey(clip.NextClip))
                        return null;
                }

                var place = ReadPlace(root);
                return new PetPack(body, clips, place, bodyTextureScale);
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

            if (!element.TryGetProperty("durationsMs", out var durationsElement)
                || durationsElement.ValueKind != JsonValueKind.Array
                || durationsElement.GetArrayLength() != orders.Count)
                return null;
            var durations = new int[orders.Count];
            var durationIndex = 0;
            foreach (var duration in durationsElement.EnumerateArray())
            {
                if (duration.ValueKind != JsonValueKind.Number || !duration.TryGetInt32(out var milliseconds)
                    || milliseconds < 1 || milliseconds > 60000)
                    return null;
                durations[durationIndex++] = milliseconds;
            }
            var nextClip = element.TryGetProperty("nextClip", out var nextElement)
                && nextElement.ValueKind == JsonValueKind.String ? nextElement.GetString() : string.Empty;
            if (!string.IsNullOrEmpty(nextClip) && !IsSafePetId(nextClip))
                return null;
            return new FishingClipPlayback(playback, loopStart, orders, durations, nextClip);
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
            ReferenceFootX = 139.5,
            ReferenceFootY = 399,
            ReferenceBody = 279,
        };

        sealed class PetPack
        {
            public PetPack(
                string[] bodyLayers,
                Dictionary<string, FishingClipPlayback> clips,
                LayerPlace place,
                int bodyTextureScale)
            {
                BodyLayers = bodyLayers;
                Clips = clips;
                Place = place;
                BodyTextureScale = bodyTextureScale;
            }

            public string[] BodyLayers { get; }
            public int BodyTextureScale { get; }
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

}
