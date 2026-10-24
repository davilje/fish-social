#if OVERLAY_PERFORMANCE_TESTS
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FishSocialOverlay;

static class OverlayWaterEffectsNativeTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static FishingPresentationEnvelope Identity(string action, string tier, string eventId = "bite")
        => new FishingPresentationEnvelope { PresentationSessionId = "native-test", ActorId = "self", ActionId = action,
            EventId = eventId, Owner = "overlay", OwnerEpoch = 100, ActorGeneration = 1, Source = "authoritative", WaterSizeTier = tier };
    static void PumpUntil(Func<bool> done, int timeoutMs = 5000)
    {
        var watch = Stopwatch.StartNew();
        while (!done())
        {
            if (watch.ElapsedMilliseconds > timeoutMs) throw new Exception("WPF water effect operation timed out");
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame); Thread.Sleep(2);
        }
    }
    public static void PoolAndHitTesting()
    {
        WaterEffectResources.Warmup(); PumpUntil(() => WaterEffectResources.Current != null);
        Check(WaterEffectResources.Current.DecodedBytes <= 4 * 1024 * 1024, "water texture decode budget exceeded");
        var back = new Canvas { IsHitTestVisible = false, Width = 400, Height = 200 };
        var front = new Canvas { IsHitTestVisible = false, Width = 400, Height = 200 };
        using (var effects = new OverlayWaterEffects(back, front))
        {
            effects.SetClip(new Rect(0, 0, 400, 200));
            var tiers = new[] { "S", "M", "L", "XL" }; var counts = new[] { 4, 6, 8, 10 };
            for (var index = 0; index < tiers.Length; index++)
            {
                effects.Configure(Identity("tier-" + index, tiers[index]), true, "standard");
                Check(effects.Publish("bite.onset", "hooked", 0, new Point(180, 120), 1, true), "shipped tier cue did not render " + tiers[index]);
                Check(back.Children.Count + front.Children.Count == counts[index], "Image count differs from real tier budget " + tiers[index]);
                Check(OverlayWaterEffects.ActiveInstances == counts[index], "global instance counter hides particles");
                Check(!effects.Publish("bite.onset", "hooked", 0, new Point(180,120), 1, true), "duplicate native marker redrew particles");
                foreach (Image image in back.Children) Check(!image.IsHitTestVisible, "back particle intercepts click");
                foreach (Image image in front.Children)
                {
                    Check(!image.IsHitTestVisible, "drop intercepts click");
                    Check(image.Width == 64, "drop 64px asset stretched to 128px");
                }
                Check(back.InputHitTest(new Point(180,120)) == null && front.InputHitTest(new Point(180,120)) == null,
                    "decorative effect canvas contributed alpha to input hit test");
                if (tiers[index] == "XL")
                {
                    var halfCount = 0;
                    foreach (Image image in back.Children) if (image.Clip is RectangleGeometry half && half.Rect.Width == 64) halfCount++;
                    Check(halfCount == 2, "XL crown repeats whole art instead of two Image halves");
                }
                effects.Stop(true);
                Check(OverlayWaterEffects.ActiveGroups == 0 && OverlayWaterEffects.ActiveInstances == 0, "pool did not return to baseline");
            }
            effects.Configure(Identity("ending", "XL"), true, "standard");
            effects.Publish("bite.onset", "hooked", 0, new Point(180,120), 1, true);
            var stopped = Stopwatch.StartNew(); effects.Phase("resolving");
            PumpUntil(() => OverlayWaterEffects.ActiveInstances == 0, 500);
            // Timer dispatch can be delayed by the test host, while the renderer
            // clamps EndAt to 100ms and never queues another resolving emitter.
            Check(stopped.ElapsedMilliseconds < 300, "ended group did not converge at next dispatcher opportunity");
            Check(!effects.Publish("fish.surface", "resolving", 1, new Point(180,120), 1, true), "result phase sprayed");
        }
        Check(OverlayWaterEffects.ActiveGroups == 0, "dispose retained subscription/group");
    }
    public static void QualityAndHiddenLifecycle()
    {
        var back = new Canvas { IsHitTestVisible = false };
        var front = new Canvas { IsHitTestVisible = false };
        using (var effects = new OverlayWaterEffects(back, front))
        {
            effects.SetClip(new Rect(0,0,400,200));
            effects.Configure(Identity("reduced", "XL"), true, "reduced");
            Check(effects.Publish("bite.onset", "hooked", 0, new Point(180,120), 1, true), "reduced bite missing");
            Check(back.Children.Count == 2 && front.Children.Count == 0, "reduced creates splash/drop particles");
            effects.Stop(true);
            effects.Configure(Identity("off", "XL"), true, "off");
            Check(!effects.Publish("bite.onset", "hooked", 0, new Point(180,120), 1, true), "off mode rendered");
            effects.Configure(Identity("off", "XL"), true, "standard");
            Check(!effects.Publish("bite.onset", "hooked", 0, new Point(180,120), 1, true), "quality on replayed past event");
            effects.Configure(Identity("hidden", "M"), true, "standard");
            effects.SetVisible(false);
            effects.Configure(Identity("hidden", "M", "hidden-bite"), true, "standard");
            effects.SetVisible(true);
            Check(!effects.Publish("bite.onset", "hooked", 0, new Point(180,120), 1, true), "resume replayed hidden bite");
            effects.Configure(Identity("hidden", "M", "fresh-visible-bite"), true, "standard");
            Check(effects.Publish("bite.onset", "hooked", 0, new Point(180,120), 1, true), "fresh visible authority bite remained suppressed");
            effects.SetVisible(false);
            Check(OverlayWaterEffects.ActiveInstances == 0, "hidden visuals accumulated particles");
        }
    }
    public static void RealCueConfiguration()
    {
        var back = new Canvas { IsHitTestVisible = false }; var front = new Canvas { IsHitTestVisible = false };
        using (var effects = new OverlayWaterEffects(back, front))
        {
            effects.SetClip(new Rect(0,0,400,200));
            foreach (var cue in new[] { "fish.struggle", "fish.surface", "fish.slap", "float.contact", "waiting.ripple", "groundbait.contact" })
            {
                var phase = cue == "groundbait.contact" ? "groundbaiting" : cue == "waiting.ripple" ? "waiting" : cue == "float.contact" ? "casting" : "effort";
                effects.Configure(Identity(cue, "XL"), true, "standard");
                Check(effects.Publish(cue, phase, 0, new Point(180,120), 1, true), "real effects.json cue mapping failed: " + cue);
                Check(back.Children.Count + front.Children.Count <= 10, "real cue breaks max draw count");
                effects.Stop(true);
            }
        }
    }
}
#endif
