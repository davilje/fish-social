using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FishSocialOverlay
{
    internal sealed class AudioSourceMarker
    {
        public string Clip { get; set; }
        public int SourceIndex { get; set; }
        public int SourceMs { get; set; }
        public string Marker { get; set; }
        public string ContactLayer { get; set; }
    }

    internal sealed class AudioSourceMarkerDocument
    {
        public int Version { get; set; }
        public AudioSourceMarker[] Markers { get; set; }
        public Dictionary<string, Dictionary<string, double[][]>> Bounds { get; set; }
    }

    // Source definitions and exported alpha bounds are shared with the Unity
    // fallback. Runtime never scans textures or assumes expanded frame numbers.
    internal static class OverlayAudioSourceMarkers
    {
        static readonly Lazy<AudioSourceMarkerDocument> Document = new Lazy<AudioSourceMarkerDocument>(Load);
        internal static AudioSourceMarkerDocument Metadata => Document.Value;
        static AudioSourceMarkerDocument Load()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OverlayResources", "fishing", "presentation-markers.json");
                var value = JsonSerializer.Deserialize<AudioSourceMarkerDocument>(File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return value != null && value.Version == 2 && value.Markers != null ? value : null;
            }
            catch (IOException) { return null; }
            catch (JsonException) { return null; }
        }

        public static bool InWindow(AudioSourceMarker marker, string clip, double sourcePosition)
            => marker.Clip == clip && sourcePosition >= marker.SourceIndex && sourcePosition < marker.SourceIndex + 1;

        public static bool Contact(string layer, string clip, int sourceIndex, FishingMotionSample sample,
            FishingContactFrame contact, FishingContactPose pose, double scale, double originY, double surfaceY)
        {
            if (string.IsNullOrEmpty(layer)) return true;
            if (scale <= 0 || contact == null) return false;
            if (layer == "line")
            {
                var top = originY + contact.LineTop * scale;
                var bottom = originY + (contact.LineBottom * scale - pose.LinePivot) * pose.LineScale + pose.LinePivot;
                return contact.LineBottom > contact.LineTop && top <= surfaceY && bottom >= surfaceY;
            }
            if (Document.Value?.Bounds == null || !Document.Value.Bounds.TryGetValue(clip, out var layers) ||
                !layers.TryGetValue(layer, out var frames) || sourceIndex < 0 || sourceIndex >= frames.Length) return false;
            var bounds = frames[sourceIndex];
            if (bounds == null || bounds.Length != 4) return false;
            var layerIndex = layer == "fish" ? 5 : layer == "bobber" ? 7 : layer == "bait" ? 8 : -1;
            if (layerIndex < 0) return false;
            var matrix = sample?.Transforms?[layerIndex];
            var offset = layer == "fish" ? pose.FishOffset : layer == "bobber" ? pose.BobberOffset : pose.BaitOffset;
            var low = double.PositiveInfinity; var high = double.NegativeInfinity;
            for (var corner = 0; corner < 4; corner++)
            {
                var x = bounds[corner % 2 == 0 ? 0 : 2];
                var y = bounds[corner < 2 ? 1 : 3];
                Transform(matrix, ref x, ref y);
                Transform(sample?.Root, ref x, ref y);
                var rendered = originY + y * scale + offset;
                low = Math.Min(low, rendered); high = Math.Max(high, rendered);
            }
            // Contact tolerance is in source pixels, independent of slot size/DPI.
            return low <= surfaceY + scale && high >= surfaceY - scale;
        }

        static void Transform(double[] values, ref double x, ref double y)
        {
            if (values == null || values.Length != 6) return;
            var nextX = values[0] * x + values[2] * y + values[4];
            y = values[1] * x + values[3] * y + values[5]; x = nextX;
        }

        public static long GroundbaitOccurrence(FishingClipPlayback clip, double elapsedMs)
        {
            if (clip == null || clip.SourceFrameCount != 8) return 0;
            double intro = 0, period = 0;
            var start = clip.IndexAtSource(1); var end = clip.IndexAtSource(7);
            for (var index = 0; index < start; index++) intro += clip.DurationMs(index);
            for (var index = start; index < end; index++) period += clip.DurationMs(index);
            return period <= 0 || elapsedMs <= intro ? 0 : (long)Math.Floor((elapsedMs - intro) / period);
        }
    }
}
