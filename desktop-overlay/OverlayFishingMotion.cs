using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FishSocialOverlay
{
    internal sealed class FishingMotionSample
    {
        public int Source { get; set; } = -1;
        public double Position { get; set; } = double.NaN;
        public int DurationMs { get; set; }
        public double[][] Transforms { get; set; }
        public double[] Line { get; set; }
        public FishingContactFrame Contact { get; set; }
    }

    internal sealed class FishingMotionClip
    {
        public int[] DurationsMs { get; set; }
        public FishingMotionSample[] Frames { get; set; }

        public bool Matches(int[] durations)
        {
            if (durations == null || DurationsMs == null || durations.Length != DurationsMs.Length || Frames == null || Frames.Length < durations.Length) return false;
            var sums = new int[durations.Length];
            var previous = -1.0;
            var previousSource = -1;
            foreach (var frame in Frames)
            {
                if (frame == null || frame.Source < 0 || frame.Source >= durations.Length || frame.DurationMs < 1 || frame.DurationMs > 60000 ||
                    !Finite(frame.Position) || frame.Position < frame.Source || frame.Position >= frame.Source + 1 || frame.Position <= previous ||
                    frame.Transforms == null || frame.Transforms.Length != 9 || frame.Contact == null
                    || frame.Source < previousSource || frame.Source > previousSource + 1) return false;
                if (sums[frame.Source] == 0 && frame.Position != frame.Source) return false;
                for (var layer = 0; layer < frame.Transforms.Length; layer++)
                {
                    var matrix = frame.Transforms[layer];
                    if (matrix == null || matrix.Length != 6) return false;
                    foreach (var value in matrix) if (!Finite(value) || Math.Abs(value) > 100000) return false;
                    if (!Rigid(matrix)) return false;
                    var body = layer == 0 || layer == 1 || layer == 2 || layer == 6;
                    var fixedLayer = body || layer == 4 || frame.Position == frame.Source;
                    if ((fixedLayer || layer == 7 || layer == 8) && !TranslationOnly(matrix)) return false;
                    if (fixedLayer && (!Near(matrix[4], 0) || !Near(matrix[5], 0))) return false;
                }
                if (frame.Line != null)
                {
                    if (frame.Line.Length != 6) return false;
                    foreach (var value in frame.Line) if (!Finite(value) || Math.Abs(value) > 4096) return false;
                }
                if (!Finite(frame.Contact.LineTop) || !Finite(frame.Contact.LineBottom) || !Finite(frame.Contact.BobberY) || !Finite(frame.Contact.FishTop)) return false;
                previous = frame.Position;
                previousSource = frame.Source;
                sums[frame.Source] += frame.DurationMs;
            }
            for (var index = 0; index < durations.Length; index++)
                if (durations[index] < 1 || durations[index] > 60000
                    || durations[index] != DurationsMs[index] || sums[index] != durations[index]) return false;
            return true;
        }

        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static bool Near(double value, double expected) => Math.Abs(value - expected) <= .000001;
        static bool TranslationOnly(double[] matrix)
            => Near(matrix[0], 1) && Near(matrix[1], 0) && Near(matrix[2], 0) && Near(matrix[3], 1);
        static bool Rigid(double[] matrix)
            => Near(matrix[0] * matrix[0] + matrix[1] * matrix[1], 1)
                && Near(matrix[2] * matrix[2] + matrix[3] * matrix[3], 1)
                && Near(matrix[0] * matrix[2] + matrix[1] * matrix[3], 0)
                && Near(matrix[0] * matrix[3] - matrix[1] * matrix[2], 1);
    }

    internal static class OverlayFishingMotion
    {
        sealed class Document
        {
            public int Version { get; set; }
            public Dictionary<string, FishingMotionClip> Clips { get; set; }
        }

        static readonly Lazy<Dictionary<string, FishingMotionClip>> Clips = new Lazy<Dictionary<string, FishingMotionClip>>(Load);

        static Dictionary<string, FishingMotionClip> Load()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OverlayResources", "fishing", "motion-samples.json");
            try { return Parse(File.ReadAllText(path)); }
            catch (IOException) { return new Dictionary<string, FishingMotionClip>(); }
            catch (UnauthorizedAccessException) { return new Dictionary<string, FishingMotionClip>(); }
        }

        internal static Dictionary<string, FishingMotionClip> Parse(string json)
        {
            try
            {
                var document = JsonSerializer.Deserialize<Document>(json);
                if (document?.Version == 1 && document.Clips != null) return document.Clips;
            }
            catch (JsonException) { }
            return new Dictionary<string, FishingMotionClip>();
        }

        public static FishingMotionClip Find(string name, int[] durations)
        {
            return Clips.Value.TryGetValue(name, out var clip) && clip != null && clip.Matches(durations) ? clip : null;
        }
    }
}
