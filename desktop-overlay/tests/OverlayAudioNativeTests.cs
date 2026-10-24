#if OVERLAY_PERFORMANCE_TESTS
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FishSocialOverlay;

// Windows-only integration: use the real async decoder, dispatcher and nine
// Image sources. These are compiled on other hosts, never reported as run there.
static class OverlayAudioNativeTests
{
    static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Fields).GetValue(value);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void PumpUntil(Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done())
        {
            Check(watch.ElapsedMilliseconds < 10000, "native audio publication timed out");
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame); Thread.Sleep(1);
        }
    }
    static OverlayAudioPresentation Identity(string id, string source = "authoritative")
        => new OverlayAudioPresentation { ProtocolVersion = 2, PresentationSessionId = "audio-native", ActorId = "self",
            PondId = "pond", SpotId = "spot", OwnerEpoch = 1, Owner = "overlay", Enabled = true,
            ActionId = id, EventId = id, EventKind = "bite", Source = source,
            EventDeadlineMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 30000 };

    public static void ActualCompositeAndColdCancellation()
    {
        var pack = OverlayFishingLayers.EnsurePackAsync("orange"); PumpUntil(() => pack.IsCompleted);
        Check(pack.Result, "native audio source pack missing");
        var queue = (OverlayDecodeQueue)typeof(OverlayFishingLayers).GetField("DecodeQueue",
            BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var serial = 0;
        foreach (var delay in new[] { 0, 150, 400 })
        foreach (var cancelled in new[] { false, true })
        using (var actor = new OverlayPetActor("audio-native-" + serial, null))
        using (var gate = new ManualResetEventSlim(false))
        using (var entered = new CountdownEvent(2))
        {
            var one = queue.TryRun(() => { entered.Signal(); gate.Wait(); return new object(); });
            var two = queue.TryRun(() => { entered.Signal(); gate.Wait(); return new object(); });
            Check(entered.Wait(1000), "failed to occupy real decoder workers");
            var count = 0;
            actor.AudioMarkerPublished += marker => {
                Check(marker.Marker == "bite.onset", "submerged native fish invented another marker");
                foreach (var image in Field<Image[]>(actor, "_layerImages"))
                    Check(image.Source != null, "audio preceded the complete nine-layer composite");
                Check(actor.IsVisible, "hidden native actor emitted a marker");
                count++;
            };
            var size = 121 + serial++;
            actor.ApplyChrome(new OverlayActorChrome { Pet = new OverlayLayoutObjectDto { w = size, h = size } });
            actor.ConfigureAudioPresentation(Identity("action-" + serial), "hooked", "orange");
            actor.Apply("cat", "hooked", "hooked", 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 30000, petId: "orange");
            var window = new Window { Content = actor, Width = 300, Height = 300, ShowInTaskbar = false };
            try
            {
                window.Show();
                typeof(OverlayPetActor).GetMethod("RenderLatestFrame", Fields).Invoke(actor, null);
                Check(count == 0, "cold decode sounded before actual publication");
                if (cancelled) actor.CancelAudioPresentation();
                var release = Task.Run(async () => { await Task.Delay(delay); gate.Set(); });
                PumpUntil(() => !actor.FramePending && one.IsCompleted && two.IsCompleted && count == (cancelled ? 0 : 1));
                actor.ConfigureAudioPresentation(Identity("action-" + serial), "hooked", "orange");
                Check(count == (cancelled ? 0 : 1), "refresh replayed a native bite");
            }
            finally { gate.Set(); window.Content = null; window.Close(); }
        }
    }
}
#endif
