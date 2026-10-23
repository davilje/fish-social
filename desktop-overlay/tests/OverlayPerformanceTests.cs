#if OVERLAY_PERFORMANCE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using FishSocialOverlay;

public static class OverlayPerformanceTests
{
    static int passed;
    [STAThread]
    public static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri("/FishSocialOverlay;component/CozyTheme.xaml", UriKind.Relative) });
        Run("existing PNG decoding and manifest contracts", FishingLayerTextureTests.Main);
        Run("mixed cats preserve shared and body bitmaps", MixedCats);
        Run("same cat has independently balanced users", SameCat);
        Run("warmup uses final size and survives new actors", Warmup);
        Run("disposal stops timers and allows collection", Disposal);
        Run("30 pond transitions dispose departed actors", PondTransitions);
        Console.WriteLine("PASS: " + passed + "/" + passed + " Windows WPF regression groups");
        app.Shutdown();
    }

    static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static T Field<T>(object value, string name) => (T)value.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    static ImageSource Frame(string pet, string clip, string layer) => OverlayFishingLayers.BitmapFor(
        OverlayFishingLayers.LayerFile(pet, clip, 0, layer), 174, 139);

    static void MixedCats()
    {
        Require(OverlayFishingLayers.TryOpen("orange", "hooked") != null, "orange assets must be deployed");
        Require(OverlayFishingLayers.TryOpen("calico", "hooked") != null, "calico assets must be deployed");
        OverlayFishingLayers.Acquire("orange", "hooked");
        OverlayFishingLayers.Acquire("calico", "hooked");
        var body = Frame("calico", "hooked", "body");
        var prop = Frame("calico", "hooked", "rod");
        Require(body != null && prop != null, "fixture images are missing");
        Require(ReferenceEquals(prop, Frame("orange", "hooked", "rod")), "props must share decoded instances");
        OverlayFishingLayers.Release("orange", "hooked");
        var decoded = OverlayFishingLayers.DecodeCount;
        Require(ReferenceEquals(body, Frame("calico", "hooked", "body")), "other cat body was dropped");
        Require(ReferenceEquals(prop, Frame("calico", "hooked", "rod")), "live shared prop was dropped");
        Require(decoded == OverlayFishingLayers.DecodeCount, "surviving cat decoded again");
        OverlayFishingLayers.Release("calico", "hooked");
        OverlayFishingLayers.Acquire("calico", "hooked");
        Require(ReferenceEquals(body, Frame("calico", "hooked", "body")), "next cycle lost warm frame");
        OverlayFishingLayers.Release("calico", "hooked");
    }

    static void SameCat()
    {
        OverlayFishingLayers.TryOpen("orange", "catch");
        OverlayFishingLayers.Acquire("orange", "catch");
        OverlayFishingLayers.Acquire("orange", "catch");
        var body = Frame("orange", "catch", "body");
        OverlayFishingLayers.Release("orange", "catch");
        Require(ReferenceEquals(body, Frame("orange", "catch", "body")), "remaining user lost its image");
        Require(OverlayFishingLayers.ActiveClipCount == 1, "clip reference count unbalanced");
        OverlayFishingLayers.Release("orange", "catch");
        Require(OverlayFishingLayers.ActiveClipCount == 0, "last user retained active clip");
    }

    static OverlayActorChrome Chrome(double size) => new OverlayActorChrome
    { Pet = new OverlayLayoutObjectDto { w = size, h = size } };

    static void Warmup()
    {
        using (var actor = new OverlayPetActor("warmup", null))
        {
            actor.ApplyChrome(Chrome(128));
            actor.WarmupActionClips("orange");
            var decoded = OverlayFishingLayers.DecodeCount;
            actor.Apply("cat", "cast", "casting", 0, 0, petId: "orange");
            Require(decoded == OverlayFishingLayers.DecodeCount, "first action decoded despite warmup");
            var before = Frame("orange", "cast", "body");
            using (var second = new OverlayPetActor("new cat", null))
            {
                second.ApplyChrome(Chrome(96));
                second.Apply("cat", "sit", "seated", 0, 0, petId: "calico");
                Require(ReferenceEquals(before, Frame("orange", "cast", "body")), "new size cleared global cache");
            }
            actor.ApplyChrome(Chrome(96));
            actor.WarmupActionClips("orange");
            decoded = OverlayFishingLayers.DecodeCount;
            actor.Apply("cat", "bait", "baiting", 0, 0, petId: "orange");
            Require(decoded == OverlayFishingLayers.DecodeCount, "resized warmup used stale size");
        }
    }

    static void AssertStopped(OverlayPetActor actor)
    {
        foreach (var name in new[] { "_frameTimer", "_refreshTimer", "_tooltipTimer" })
            Require(!Field<DispatcherTimer>(actor, name).IsEnabled, "timer left running: " + name);
        Require(Field<DispatcherTimer>(actor, "_speechFadeTimer") == null, "speech timer left attached");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference RemovedActor()
    {
        var actor = new OverlayPetActor("removed", null);
        actor.ApplyChrome(Chrome(128));
        actor.Apply("cat", "cast", "casting", 0, 0, petId: "orange");
        actor.ShowSpeech("临时气泡", true);
        actor.Dispose(); actor.Dispose();
        actor.ShowSpeech("must not restart", true);
        AssertStopped(actor);
        Require(OverlayFishingLayers.ActiveClipCount == 0, "disposed actor retained clip");
        return new WeakReference(actor);
    }

    static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    static void Disposal()
    {
        var active = OverlayPetActor.ActiveActorCount;
        var reference = RemovedActor();
        Require(OverlayPetActor.ActiveActorCount == active, "disposal left an active actor");
        for (var i = 0; i < 3 && reference.IsAlive; i++)
        { Pump(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Require(!reference.IsAlive, "removed actor is still rooted after dispatcher cleanup");
    }

    static void PondTransitions()
    {
        var actorLayer = new Canvas();
        var scene = new PondScenePresenter(new Canvas(), actorLayer, new Canvas(), new Canvas(),
            new Image(), new Rectangle(), new Rectangle(), new Rectangle(), null);
        try
        {
            for (var i = 0; i < 30; i++)
            {
                var previous = new List<OverlayPetActor>();
                foreach (OverlayPetActor actor in actorLayer.Children) previous.Add(actor);
                scene.Apply(new IpcMessage { PondId = "test-pond-" + i, OwnPetId = "orange",
                    PetVisualState = "sit", FishingPhase = "seated", Users = new[] {
                        new OverlayUserDto { PlayerId = "other", PetId = "calico", PetVisualState = "sit", FishingPhase = "seated" } } });
                foreach (var actor in previous) AssertStopped(actor);
                Require(actorLayer.Children.Count == 2, "scene accumulated actors");
                Require(OverlayPetActor.ActiveActorCount == 2, "departed actor remains active");
                var removed = (OverlayPetActor)actorLayer.Children[1];
                scene.Apply(new IpcMessage { PondId = "test-pond-" + i, OwnPetId = "orange",
                    PetVisualState = "sit", FishingPhase = "seated", Users = Array.Empty<OverlayUserDto>() });
                AssertStopped(removed);
            }
        }
        finally
        {
            var last = (OverlayPetActor)actorLayer.Children[0];
            scene.Dispose(); scene.Dispose();
            AssertStopped(last);
        }
        Require(actorLayer.Children.Count == 0 && OverlayFishingLayers.ActiveClipCount == 0, "scene disposal retained actors/clips");
        Require(OverlayPetActor.ActiveActorCount == 0, "scene disposal left active actors");
        Require(OverlayFishingLayers.CachedPixelBytes <= 64L * 1024 * 1024, "released cache exceeded budget");
    }
}
#endif
