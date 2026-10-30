using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using FishSocialOverlay;

static class OverlayFishingRestMotionTests
{
    static int passed;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Near(double actual, double expected, string message) { Check(Math.Abs(actual - expected) < 1e-8, message); }
    static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    public static void Main()
    {
        Run("approved nod has a low hold, lift, recovery and identical Unity timing", () =>
        {
            Near(FishSocial.Motion.FishingRestGeometry.Nod(2700),1,"missing lowered head");
            Near(FishSocial.Motion.FishingRestGeometry.Nod(3200),1,"missing low hold");
            Near(FishSocial.Motion.FishingRestGeometry.Nod(3900),.18,"missing lift");
            Near(FishSocial.Motion.FishingRestGeometry.Nod(6400),0,"loop seam");
            for(var t=0;t<12800;t+=41)
            {
                var overlay=OverlayFishingRestMotion.Sample(t,1);
                var unity=FishSocial.Desktop.Pet.FishingRestMotion.Sample(t,1);
                Near(overlay.Nod,unity.Nod,"different nod timing");
                Near(overlay.RodY(541.5,149),unity.Rod.Y(541.5,149),"different rod tip");
                FishSocial.Motion.FishingRestGeometry.Point(346,348.5,overlay.Nod,out var x,out var y);
                var rod=overlay.RodMatrix;
                Near(overlay.X(x,y),rod[0]*346+rod[2]*348.5+rod[4],"grip x detached");
                Near(overlay.Y(x,y),rod[1]*346+rod[3]*348.5+rod[5],"grip y detached");
            }
        });
        Run("long sessions cannot accumulate scale and feet never move", () =>
        {
            var motion = new OverlayFishingRestMotion();
            for (var time = 0; time <= 86400000; time += 1577)
            {
                var pose = motion.Update(time, true, time >= 1800000);
                Near(pose.X(264, 450), 264, "foot x drift"); Near(pose.Y(264, 450), 450, "foot y drift");
                var determinant = pose.Matrix[0] * pose.Matrix[3] - pose.Matrix[1] * pose.Matrix[2];
                Check(determinant >= .972 - 1e-10 && determinant <= 1.028 + 1e-10, "unbounded body scale");
            }
        });
        Run("sleep and wake are continuous and snapshots do not restart blending", () =>
        {
            var motion = new OverlayFishingRestMotion(); motion.Update(0, true, false);
            Near(motion.Update(1800000, true, true).SleepBlend, 0, "sleep popped");
            Near(motion.Update(1800400, true, true).SleepBlend, .5, "sleep blend restarted");
            Near(motion.Update(1800800, true, true).SleepBlend, 1, "sleep did not settle");
            Near(motion.Update(1800900, true, false).SleepBlend, 1, "wake popped");
            Near(motion.Update(1801300, true, false).SleepBlend, .5, "wake blend restarted");
            Near(motion.Update(1801700, true, false).SleepBlend, 0, "wake failed");
        });
        Run("leaving fishing resets pose and sleeping badge", () =>
        {
            var motion = new OverlayFishingRestMotion(); motion.Update(0, true, true); motion.Update(2000, true, true);
            var pose = motion.Update(2001, false, true);
            Near(pose.X(230, 200), 230, "stale x transform"); Near(pose.Y(230, 200), 200, "stale y transform");
            Check(pose.SleepBlend == 0 && pose.BubbleOpacity == 0, "stale sleeping marker");
            Near(motion.Update(4000, true, false).Y(230, 200), 200, "new session inherited old phase");
        });
        Run("breathing line follows rod tip while float endpoint stays fixed", () =>
        {
            for (var time = 0; time < 12000; time += 37)
            {
                var pose = OverlayFishingRestMotion.Sample(time, time / 12000d);
                var line = pose.Line(430, 170, 463);
                var rod = pose.RodMatrix;
                Near(430 + line[2] * 170 + line[4], rod[0]*430+rod[2]*170+rod[4], "detached rod tip x");
                Near(line[3] * 170 + line[5], pose.RodY(430, 170), "detached rod tip y");
                Near(540 + line[2] * 463 + line[4], 540, "float x drift");
                Near(line[3] * 463 + line[5], 463, "float y drift");
                var water = OverlayFishingWaterContact.Solve(new FishingContactFrame
                { LineTop = pose.RodY(430, 170), LineBottom = 463, BobberY = 463 }, 1, 0, 572, 1);
                Near(water.LinePivot + (463 - water.LinePivot) * water.LineScale, 572, "water endpoint disconnected");
            }
        });
        Run("non-authoritative time alone never puts a cat to sleep", () =>
        {
            var motion = new OverlayFishingRestMotion(); motion.Update(0, true, false);
            Near(motion.Update(86400000, true, false).SleepBlend, 0, "local timer overrode server");
        });
        Run("IPC accepts missing null and real UTC sleep deadlines", () =>
        {
            foreach (var value in new[] { "{}", "{\"fishingSleepAt\":null}", "{\"fishingAsleep\":true,\"fishingSleepAt\":1792828800000}" })
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(value)))
                {
                    var message = (IpcMessage)new DataContractJsonSerializer(typeof(IpcMessage)).ReadObject(stream);
                    Check(message != null, "IPC deserialize failed");
                    if (message.FishingAsleep) Check(message.FishingSleepAt == 1792828800000L, "UTC truncated");
                }
            }
        });
        Console.WriteLine("PASS " + passed + "/" + passed + " rest motion groups");
    }
}
