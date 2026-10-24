#if OVERLAY_MOTION_TESTS || OVERLAY_PERFORMANCE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FishSocialOverlay;

internal static class OverlayFishingMotionTestData
{
    public static readonly string[] Pets = { "orange", "calico", "white", "silver", "brown", "cream", "blue", "black", "cow", "dafeiyu" };
    public static readonly string[] Clips = { "cast", "reel", "reel_empty", "groundbait" };
    public static readonly int[] ExpandedCounts = { 13, 16, 16, 18 };
    public static string Root => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OverlayResources", "fishing");
    public static string MotionJson => File.ReadAllText(Path.Combine(Root, "motion-samples.json"));
    public static Dictionary<string, FishingMotionClip> Motion() => OverlayFishingMotion.Parse(MotionJson);
    public static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static FishingClipPlayback Original(string pet, string name)
    {
        using (var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, pet, "manifest.json"))))
        {
            var clip = document.RootElement.GetProperty("clips").GetProperty(name);
            var durations = new List<int>();
            foreach (var value in clip.GetProperty("durationsMs").EnumerateArray()) durations.Add(value.GetInt32());
            var orders = new List<string[]>();
            foreach (var frame in clip.GetProperty("frames").EnumerateArray())
            {
                var order = new List<string>();
                foreach (var layer in frame.EnumerateArray()) order.Add(layer.GetString());
                orders.Add(order.ToArray());
            }
            return new FishingClipPlayback(clip.GetProperty("playback").GetString(),
                clip.TryGetProperty("loopStart", out var start) ? start.GetInt32() : 0,
                orders, durations.ToArray(), clip.TryGetProperty("nextClip", out var next) ? next.GetString() : string.Empty);
        }
    }

    public static void CheckExpansion(FishingClipPlayback original, FishingClipPlayback expanded, int expected)
    {
        Check(expanded.FrameCount == expected && expanded.FrameCount > expanded.SourceFrameCount, "missing expanded samples");
        Check(expanded.SourceFrameCount == original.FrameCount, "source frame count changed");
        Check(expanded.Playback == original.Playback && expanded.NextClip == original.NextClip, "motion changed playback behavior");
        var sums = new int[original.FrameCount];
        for (var index = 0; index < expanded.FrameCount; index++)
        {
            var source = expanded.SourceIndex(index);
            Check(source >= 0 && source < original.FrameCount && expanded.DurationMs(index) > 0, "invalid sample mapping");
            Check(string.Join(",", expanded.Orders[index]) == string.Join(",", original.Orders[source]), "layer order changed");
            Check(expanded.SourcePosition(index) >= source && expanded.SourcePosition(index) < source + 1, "sample crossed source boundary");
            sums[source] += expanded.DurationMs(index);
        }
        for (var source = 0; source < sums.Length; source++)
        {
            Check(sums[source] == original.DurationMs(source), "source duration drifted");
            Check(expanded.SourcePosition(expanded.IndexAtSource(source)) == source, "authored pose not retained exactly");
        }
        Check(expanded.LoopStart == expanded.IndexAtSource(original.LoopStart), "loop index not remapped");
    }

    public static void CheckRig(string name, FishingClipPlayback playback)
    {
        using (var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "source-rig.json"))))
        {
            var rig = document.RootElement.GetProperty("clips").GetProperty(name);
            foreach (var sample in playback.Motion)
            {
                if (sample.Position == sample.Source)
                    foreach (var transform in sample.Transforms)
                        Check(Identity(transform), "authored key pose has a non-identity transform");
                foreach (var layer in new[] { 0, 1, 2, 6 })
                    Check(Identity(sample.Transforms[layer]), "body, head or hands were transformed");
                var rod = sample.Transforms[3];
                var grip = rig[sample.Source].GetProperty("grip");
                if (grip.ValueKind == JsonValueKind.Null)
                    Check(Identity(rod), "rod moved without an authored grip");
                else
                {
                    var horizontal = grip[0].GetDouble(); var vertical = grip[1].GetDouble();
                    Check(Math.Abs(rod[0] * horizontal + rod[2] * vertical + rod[4] - horizontal) < .000001 &&
                        Math.Abs(rod[1] * horizontal + rod[3] * vertical + rod[5] - vertical) < .000001, "rod grip drifted");
                    Check(Math.Abs(rod[0] * rod[3] - rod[1] * rod[2] - 1) < .000001, "rod was scaled or skewed");
                }
                if (sample.Line != null)
                {
                    var tip = rig[sample.Source].GetProperty("tip");
                    var horizontal = tip[0].GetDouble(); var vertical = tip[1].GetDouble();
                    Check(Math.Abs(rod[0] * horizontal + rod[2] * vertical + rod[4] - sample.Line[0]) < .000001 &&
                        Math.Abs(rod[1] * horizontal + rod[3] * vertical + rod[5] - sample.Line[1]) < .000001, "line disconnected from rod tip");
                }
            }
        }
    }

    public static bool Identity(double[] values)
    {
        var expected = new double[] { 1, 0, 0, 1, 0, 0 };
        for (var index = 0; index < expected.Length; index++) if (Math.Abs(values[index] - expected[index]) > .000001) return false;
        return true;
    }
}
#endif
