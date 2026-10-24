#if OVERLAY_PERFORMANCE_TESTS
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
            var tiers = new[] { "S", "M", "L", "XL" }; var counts = new[] { 4, 5, 6, 6 };
            for (var index = 0; index < tiers.Length; index++)
            {
                effects.Configure(Identity("tier-" + index, tiers[index]), true, "standard");
                Check(effects.Publish("fish.surface", "hooked", 0, new Point(180, 120), 1, true), "shipped tier cue did not render " + tiers[index]);
                Check(back.Children.Count + front.Children.Count == counts[index], "Image count differs from real tier budget " + tiers[index]);
                Check(OverlayWaterEffects.ActiveInstances == counts[index], "global instance counter hides particles");
                Check(!effects.Publish("fish.surface", "hooked", 0, new Point(180,120), 1, true), "duplicate native marker redrew particles");
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
            Check(!effects.Publish("fish.slap", "resolving", 1, new Point(180,120), 1, true), "slap reused retrieve surface exception");
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
            foreach (var cue in new[] { "bite.onset", "fish.struggle", "fish.surface", "fish.slap", "float.contact", "waiting.ripple", "groundbait.contact", "retrieve.contact" })
            {
                var phase = cue == "retrieve.contact" ? "resolving" : cue == "groundbait.contact" ? "groundbaiting" : cue == "waiting.ripple" ? "waiting" : cue == "float.contact" ? "casting" : "effort";
                effects.Configure(Identity(cue, "XL"), true, "standard");
                Check(effects.Publish(cue, phase, 0, new Point(180,120), 1, true), "real effects.json cue mapping failed: " + cue);
                Check(back.Children.Count + front.Children.Count <= 10, "real cue breaks max draw count");
                if (cue == "bite.onset" || cue == "fish.struggle")
                    Check(back.Children.Count == 2 && front.Children.Count == 0, "bite/struggle emitted splash/drop");
                if (cue == "groundbait.contact") Check(back.Children.Count == 3 && front.Children.Count == 1, "GB lost small contact crown/drop");
                effects.Stop(true);
            }
            effects.Configure(Identity("retrieve-no-contact", "S"), true, "standard");
            effects.Phase("stopping");
            Check(!effects.Publish("retrieve.contact", "stopping", 0, new Point(180,120), 1, false), "noncontact reel sprayed");
            Check(effects.Publish("retrieve.contact", "stopping", 0, new Point(180,120), 1, true), "real stop contact was suppressed by ending cleanup");
            Check(back.Children.Count == 2 && front.Children.Count == 1, "retrieve small contact component count");
            effects.Stop(true);
            var retrieveFish = Identity("retrieve-fish", "L"); retrieveFish.Source = "prediction";
            effects.Configure(retrieveFish, true, "standard");
            Check(!effects.Publish("fish.surface", "stopping",0,new Point(180,120),1,false), "submerged retrieve fish sprayed");
            Check(effects.Publish("fish.surface", "stopping",0,new Point(180,120),1,true), "real retrieve fish exit did not render");
            Check(back.Children.Count == 4 && front.Children.Count == 2, "retrieve fish lost visible L tier splash shape");
            Check(!effects.Publish("fish.surface", "stopping",0,new Point(180,120),1,true), "retrieve fish contact repeated");
            Check(!effects.Publish("fish.slap", "stopping",0,new Point(180,120),1,true), "retrieve exception leaked to slap");
            effects.Stop(true);
            effects.Configure(Identity("ripple-raster", "XL"), true, "standard");
            Check(effects.Publish("bite.onset", "hooked", 0, new Point(180,120), 1, true), "raster bite setup");
            var watch = Stopwatch.StartNew(); PumpUntil(() => watch.ElapsedMilliseconds > 65);
            Check(!InkAbove(back, 120), "actual transformed ripple alpha crossed above waterline");
            Check(InkAbove(back, 190), "ripple clipping removed the entire ellipse");
            effects.Stop(true);
            effects.Configure(Identity("splash-raster", "S"), true, "standard");
            Check(effects.Publish("float.contact", "casting", 0, new Point(180,120), 1, true), "raster splash setup");
            watch.Restart(); PumpUntil(() => watch.ElapsedMilliseconds > 65);
            Check(InkAbove(back, 120) || InkAbove(front,120), "waterline ripple clip incorrectly clipped flying splash/drop");
        }
    }
    static bool InkAbove(Canvas canvas, int limit)
    {
        canvas.Width = 400; canvas.Height = 200;
        canvas.Measure(new Size(400,200)); canvas.Arrange(new Rect(0,0,400,200)); canvas.UpdateLayout();
        var bitmap = new RenderTargetBitmap(400,200,96,96,PixelFormats.Pbgra32); bitmap.Render(canvas);
        var pixels = new byte[400*200*4]; bitmap.CopyPixels(pixels,400*4,0);
        for (var y=0; y<limit; y++) for(var x=0;x<400;x++) if(pixels[(y*400+x)*4+3]>8)return true;
        return false;
    }
}
#endif
