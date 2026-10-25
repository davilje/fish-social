using System;
using FishSocialOverlay;

static class OverlayWaterContactTests
{
    static int passed;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    public static void Main()
    {
        Run("hidden bites project disturbances without inventing a fish exit", () =>
        {
            foreach (var clip in new[] { "hooked", "bite", "effort" })
            {
                Check(OverlayFishingWaterContact.IsSubmergedClip(clip), "bite fish is not hidden");
                Check(OverlayFishingWaterContact.ProjectsDisturbance("bite.onset", clip), "bite cue was lost underwater");
                Check(OverlayFishingWaterContact.ProjectsDisturbance("fish.struggle", clip), "struggle cue was lost underwater");
                Check(!OverlayFishingWaterContact.ProjectsDisturbance("fish.surface", clip), "submerged fish emits an exit");
            }
            foreach (var clip in new[] { "reel", "catch", "reset_catch", "reel_empty", "fishing" })
                Check(!OverlayFishingWaterContact.IsSubmergedClip(clip) &&
                    !OverlayFishingWaterContact.ProjectsDisturbance("fish.struggle", clip), "submerged permission leaked to " + clip);
        });
        Run("moving a seat up the bank keeps bobber and fish on the scene waterline", () =>
        {
            const double worldWater = 446.431;
            foreach (var actorY in new[] { 310d, 278d, 246d })
            {
                var surface = OverlayFishingWaterContact.LocalSurface(worldWater, actorY, 80);
                var frame = OverlayFishingWaterContact.Frame("fishing", 0);
                var pose = OverlayFishingWaterContact.Solve(frame, .125, -12, surface, 1);
                Check(Math.Abs(actorY - 12 + frame.BobberY * .125 + pose.BobberOffset - worldWater) < .001,
                    "bobber moved onto land with the seat");
                frame = OverlayFishingWaterContact.Frame("hooked", 0);
                pose = OverlayFishingWaterContact.Solve(frame, .125, -12, surface, 1);
                Check(actorY - 12 + frame.FishTop * .125 + pose.FishOffset >= worldWater + 8 - .001,
                    "fish depth follows the seat instead of water");
            }
            Check(OverlayFishingWaterContact.LocalSurface(null, 300, 86) == 86, "legacy layout fallback changed");
            Check(OverlayFishingWaterContact.LocalSurface(double.NaN, 300, 86) == 86, "invalid waterline accepted");
        });
        foreach (var size in new[] { 64, 96, 128, 192 })
        {
            var s = size;
            Run("water contact at pet size " + s, () =>
            {
                var scale = s / 512.0 * 279 / 257;
                var origin = s * 404 / 512.0 - 450 * scale;
                var water = s + 6;
                var frame = OverlayFishingWaterContact.Frame("fishing", 0);
                Check(frame != null, "exported geometry absent");
                var pose = OverlayFishingWaterContact.Solve(frame, scale, origin, water, 1);
                var bobber = origin + frame.BobberY * scale + pose.BobberOffset;
                Check(Math.Abs(bobber - water) < .001, "bobber floats above water");
                var tip = frame.LineTop * scale;
                Check(Math.Abs((tip - pose.LinePivot) * pose.LineScale + pose.LinePivot - tip) < .001, "rod tip detached");
                var endpoint = (frame.LineBottom * scale - pose.LinePivot) * pose.LineScale + pose.LinePivot + origin;
                Check(endpoint >= water, "line stops above water");
                frame = OverlayFishingWaterContact.Frame("hooked", 0);
                pose = OverlayFishingWaterContact.Solve(frame, scale, origin, water, 1);
                Check(origin + frame.FishTop * scale + pose.FishOffset >= water + 64 * scale - .001,
                    "bite fish does not reach the deeper 64px reference depth");
            });
        }
        Run("cast connects at water and retrieve releases extension continuously", () =>
        {
            Check(OverlayFishingWaterContact.Weight("cast",0,4)==0, "cast starts extended");
            Check(OverlayFishingWaterContact.Weight("cast",3,4)==1, "cast never reaches water");
            double prior=2;
            for(int i=0;i<6;i++) { var w=OverlayFishingWaterContact.Weight("reel",i,6); Check(w<=prior,"reel extension grows");prior=w; }
            Check(prior==0,"reel never restores authored pose");
            Check(OverlayFishingWaterContact.Weight("catch",0,7)==0,"catch held fish was moved");
        });
        Run("missing bounds and empty line preserve safe transforms", () =>
        {
            Check(OverlayFishingWaterContact.Frame("missing",0)==null,"missing data fabricated");
            var pose=OverlayFishingWaterContact.Solve(null,1,0,600,1);Check(pose.LineScale==1,"missing geometry distorts art");
            pose=OverlayFishingWaterContact.Solve(new FishingContactFrame(),1,0,600,1);Check(pose.LineScale==1,"empty line has invalid scale");
        });
        Run("fish depth scales with art and preserves the rod tip and line extension", () =>
        {
            var frame = new FishingContactFrame { LineTop = 100, LineBottom = 400, FishTop = 400 };
            foreach (var scale in new[] { .125, .25, .5, 1d })
            foreach (var origin in new[] { -60d, 0, 40 })
            {
                var surface = origin + 500 * scale;
                var pose = OverlayFishingWaterContact.Solve(frame, scale, origin, surface, 1);
                Check(Math.Abs(origin + 400 * scale + pose.FishOffset - surface - 64 * scale) < .000001,
                    "fish depth is not 64 scaled pixels");
                Check(Math.Abs(pose.LinePivot - 100 * scale) < .000001, "depth change moved the rod tip pivot");
                Check(Math.Abs(300 * scale * (pose.LineScale - 1) - pose.FishOffset) < .000001,
                    "line no longer extends to the deeper fish");
                var halfway = OverlayFishingWaterContact.Solve(frame, scale, origin, surface, .5);
                Check(Math.Abs(halfway.FishOffset * 2 - pose.FishOffset) < .000001, "retrieve depth does not taper continuously");
            }
        });
        Run("held fish and final retrieve poses receive no water drop", () =>
        {
            var frame = new FishingContactFrame { LineTop = 100, LineBottom = 400, FishTop = 400 };
            foreach (var clip in new[] { "catch", "reset_catch", "reset_empty", "sit", "leave", "bait" })
            {
                var weight = OverlayFishingWaterContact.Weight(clip, 1.5, 4);
                var pose = OverlayFishingWaterContact.Solve(frame, .5, -10, 400, weight);
                Check(weight == 0 && pose.FishOffset == 0 && pose.LineScale == 1, "held fish was pulled out of the hands: " + clip);
            }
            foreach (var clip in new[] { "reel", "reel_empty" })
                Check(OverlayFishingWaterContact.Solve(frame, 1, 0, 500,
                    OverlayFishingWaterContact.Weight(clip, 5, 6)).FishOffset == 0, "retrieve ends with a submerged held fish");
        });
        Run("groundbait drops quadratically after release and clears on hand return", () =>
        {
            foreach (var scale in new[] { .125, .25, .5, 1d })
            foreach (var origin in new[] { -30d, 0, 60 })
            {
                var surface = origin + OverlayFishingWaterContact.ReferenceSurfaceY * scale;
                var middle = OverlayFishingWaterContact.GroundbaitOffset(4.5, scale, origin, surface);
                Check(Math.Abs(middle - 90 * scale * .25) < .000001, "mid-flight drop does not use quadratic progress and 90px depth");
                var lowerSurface = OverlayFishingWaterContact.GroundbaitOffset(4.5, scale, origin, surface + 40);
                Check(Math.Abs(lowerSurface - middle - 10) < .000001, "drop ignores actual surface height");
                Check(Math.Abs(OverlayFishingWaterContact.GroundbaitOffset(4.5, scale, origin + 40, surface) - middle) < .000001,
                    "surface above the art creates an upward groundbait correction");
                var previous = 0d;
                for (var position = 3d; position < 6; position += .125)
                {
                    var offset = OverlayFishingWaterContact.GroundbaitOffset(position, scale, origin, surface);
                    Check(offset >= previous && offset < 90 * scale, "groundbait drop is non-monotonic or overshoots");
                    previous = offset;
                }
                foreach (var position in new[] { -1d, 0, 2.999, 3, 6, 6.001, 7, 8 })
                    Check(OverlayFishingWaterContact.GroundbaitOffset(position, scale, origin, surface) == 0,
                        "groundbait drop remains before release or after hand return");
            }
            Check(OverlayFishingWaterContact.GroundbaitOffset(4.5, 0, 0, 500) == 0
                && OverlayFishingWaterContact.GroundbaitOffset(4.5, -1, 0, 500) == 0, "invalid art scale produces a drop");
        });
        Console.WriteLine("PASS: " + passed + "/" + passed + " water-contact groups");
    }
}
