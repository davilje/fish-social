using System;
using FishSocialOverlay;

static class OverlayWaterContactTests
{
    static int passed;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    public static void Main()
    {
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
                Check(origin + frame.FishTop * scale + pose.FishOffset > water, "bite fish is above water");
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
        Console.WriteLine("PASS: " + passed + "/" + passed + " water-contact groups");
    }
}
