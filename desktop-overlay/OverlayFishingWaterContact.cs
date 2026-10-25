using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FishSocialOverlay
{
    // Source alpha bounds are exported once; no pixel scans or image edits on the UI thread.
    internal sealed class FishingContactFrame
    {
        public double LineTop { get; set; }
        public double LineBottom { get; set; }
        public double BobberY { get; set; } = -1;
        public double FishTop { get; set; } = -1;
    }

    internal struct FishingContactPose
    {
        public double LineScale, LinePivot, BobberOffset, FishOffset, BaitOffset;
    }

    internal static class OverlayFishingWaterContact
    {
        public const double ReferenceSurfaceY = 463.5;
        public const double FishDepth = 64;
        public const double GroundbaitDrop = 90;
        static readonly Lazy<Dictionary<string, FishingContactFrame[]>> Frames =
            new Lazy<Dictionary<string, FishingContactFrame[]>>(Load);

        static Dictionary<string, FishingContactFrame[]> Load()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OverlayResources", "fishing", "water-contact.json");
            try { return JsonSerializer.Deserialize<Dictionary<string, FishingContactFrame[]>>(File.ReadAllText(path)); }
            catch (IOException) { return new Dictionary<string, FishingContactFrame[]>(); }
            catch (JsonException) { return new Dictionary<string, FishingContactFrame[]>(); }
        }

        public static FishingContactFrame Frame(string clip, int index)
        {
            return Frames.Value != null && Frames.Value.TryGetValue(clip, out var frames) && index >= 0 && index < frames.Length
                ? frames[index] : null;
        }

        // Scene and actor coordinates are both logical units, before viewport/DPI scaling.
        public static double LocalSurface(double? sceneWaterlineY, double actorOriginY, double fallback)
        {
            if (!sceneWaterlineY.HasValue || double.IsNaN(sceneWaterlineY.Value) ||
                double.IsInfinity(sceneWaterlineY.Value) || double.IsNaN(actorOriginY) || double.IsInfinity(actorOriginY))
                return fallback;
            return sceneWaterlineY.Value - actorOriginY;
        }

        public static bool IsSubmergedClip(string clip)
            => clip == "hooked" || clip == "bite" || clip == "effort";

        // Reuse the approved waiting float; bite source art has an empty bobber layer.
        public const double BobberPivotX = 569.5, BobberPivotY = 463.5;

        public static void HookedBobberMotion(double position, int sourceCount, out double dip, out double angle, int loopStart = -1)
        {
            // Intro and looping section each end at their initial pose. A hooked
            // clip loops from source 4, while effort loops from 0 and bite is once.
            var period = loopStart < 0 ? sourceCount - 1 : position < loopStart ? loopStart : sourceCount - loopStart;
            var offset = loopStart >= 0 && position >= loopStart ? loopStart : 0;
            var phase = (position - offset) * Math.PI * 2 / Math.Max(1, period);
            dip = 2 + 4 * (.5 - .5 * Math.Cos(phase));
            angle = 8 * Math.Sin(phase);
        }

        // The published interpolation line is a quadratic, not the original PNG.
        // Coordinates already include the layer/root/22B transforms and actor origin.
        public static bool QuadraticWaterContact(double x0, double y0, double x1, double y1,
            double x2, double y2, double surface, out double x)
        {
            x = 0;
            var a = y0 - 2 * y1 + y2;
            var b = 2 * (y1 - y0);
            var c = y0 - surface;
            double t;
            if (Math.Abs(a) < 1e-9)
            {
                if (Math.Abs(b) < 1e-9) return false;
                t = -c / b;
            }
            else
            {
                var discriminant = b * b - 4 * a * c;
                if (discriminant < 0) return false;
                var root = Math.Sqrt(discriminant);
                var first = (-b - root) / (2 * a);
                var second = (-b + root) / (2 * a);
                t = first >= 0 && first <= 1 ? first : second;
                if (second >= 0 && second <= 1 && second < t) t = second;
            }
            if (double.IsNaN(t) || double.IsInfinity(t) || t < 0 || t > 1) return false;
            var u = 1 - t;
            x = u * u * x0 + 2 * u * t * x1 + t * t * x2;
            return !double.IsNaN(x) && !double.IsInfinity(x);
        }

        // Underwater struggle projects onto the surface; an emerging fish still
        // needs an actual intersection before it can emit fish.surface/slap.
        public static bool ProjectsDisturbance(string cue, string clip)
            => IsSubmergedClip(clip) && (cue == "bite.onset" || cue == "fish.struggle");

        public static double Weight(string clip, double index, int count)
        {
            if (clip == "fishing" || clip == "hooked" || clip == "bite" || clip == "effort") return 1;
            var t = count > 1 ? Math.Max(0, Math.Min(1, index / (double)(count - 1))) : 1;
            if (clip == "cast") return t * t;
            if (clip == "reel" || clip == "reel_empty") return Math.Pow(1 - t, 2);
            return 0;
        }

        public static FishingContactPose Solve(FishingContactFrame frame, double scale, double originY, double surfaceY, double weight)
        {
            var pose = new FishingContactPose { LineScale = 1 };
            if (frame == null || scale <= 0 || weight <= 0) return pose;
            weight = Math.Max(0, Math.Min(1, weight));
            var delta = Math.Max(0, surfaceY - (originY + ReferenceSurfaceY * scale));
            pose.BobberOffset = Math.Max(0, surfaceY - (originY + (frame.BobberY >= 0 ? frame.BobberY : ReferenceSurfaceY) * scale)) * weight;
            pose.BaitOffset = delta * weight;
            // Bite starts beneath the surface, with the line connected to the moving fish.
            pose.FishOffset = frame.FishTop >= 0
                ? Math.Max(delta, surfaceY + FishDepth * scale - (originY + frame.FishTop * scale)) * weight
                : delta * weight;
            var extension = frame.FishTop >= 0 ? pose.FishOffset : pose.BobberOffset;
            var length = (frame.LineBottom - frame.LineTop) * scale;
            if (length > 1)
            {
                pose.LinePivot = frame.LineTop * scale;
                pose.LineScale = 1 + extension / length;
            }
            return pose;
        }

        public static double GroundbaitOffset(double position, double scale, double originY, double surfaceY)
        {
            if (position < 3 || position >= 6 || scale <= 0) return 0;
            var progress = (position - 3) / 3;
            var drop = Math.Max(0, surfaceY - (originY + ReferenceSurfaceY * scale)) + GroundbaitDrop * scale;
            return drop * progress * progress;
        }
    }
}
