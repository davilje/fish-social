#if OVERLAY_PERFORMANCE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        { Source = new Uri("/FishSocialOverlay;component/CozyTheme.xaml", UriKind.Relative) });
        Run("existing PNG decoding and manifest contracts", FishingLayerTextureTests.Main);
        Run("mixed cats preserve shared and body bitmaps", MixedCats);
        Run("same cat has independently balanced users", SameCat);
        Run("warmup uses final size and survives new actors", Warmup);
        Run("disposal stops timers and allows collection", Disposal);
        Run("30 pond transitions dispose departed actors", PondTransitions);
        Run("unchanged snapshots do not measure or move actors", IncrementalLayout);
        Run("chat appends preserve controls and defer collapsed logs", IncrementalChat);
        Run("async decoding shares frozen results and rejects stale actors", AsyncFrames);
        Run("hidden actors resume terminal animation chains", HiddenAnimation);
        Run("viewport changes preserve pan limits", ViewportPan);
        Run("static alpha extraction is exact and prepared before display", OverlayStaticPerformanceTests.AlphaExtraction);
        Run("static decoding shares warm results and balances cancellation", OverlayStaticPerformanceTests.SharedStaticLoads);
        Run("static image cache evicts under real decoded pixel pressure", OverlayStaticPerformanceTests.StaticCacheBudget);
        Run("pond switch and disposal reject stale static art", OverlayStaticPerformanceTests.SceneCancellation);
        Run("async decoration preserves geometry and z-order", OverlayStaticPerformanceTests.DecorationGeometry);
        Run("nine-slice eviction bounds cache and preserves live brushes", OverlayStaticPerformanceTests.NineSliceBudget);
        Run("combined and dual masks preserve rendered alpha", OverlayStaticPerformanceTests.MaskParity);
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
            WaitUntil(() => !actor.WarmupPending);
            var decoded = OverlayFishingLayers.DecodeCount;
            actor.Apply("cat", "cast", "casting", 0, 0, petId: "orange");
            WaitUntil(() => !actor.FramePending);
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
            WaitUntil(() => !actor.WarmupPending);
            decoded = OverlayFishingLayers.DecodeCount;
            actor.Apply("cat", "bait", "baiting", 0, 0, petId: "orange");
            WaitUntil(() => !actor.FramePending);
            Require(decoded == OverlayFishingLayers.DecodeCount, "resized warmup used stale size");
        }
    }

    static void AssertStopped(OverlayPetActor actor)
    {
        foreach (var name in new[] { "_frameTimer", "_refreshTimer", "_tooltipTimer" })
            Require(!(bool)Field<object>(actor, name).GetType().GetProperty("IsEnabled").GetValue(Field<object>(actor, name)), "timer left running: " + name);
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
        WaitUntil(() => OverlayFishingLayers.PendingDecodes == 0);
        for (var i = 0; i < 3 && reference.IsAlive; i++)
        { Pump(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Require(!reference.IsAlive, "removed actor is still rooted after dispatcher cleanup");
    }

    static void WaitUntil(Func<bool> ready)
    {
        var watch = Stopwatch.StartNew();
        while (!ready())
        {
            if (watch.ElapsedMilliseconds > 15000) throw new Exception("Dispatcher condition timed out");
            Pump(); Thread.Sleep(5);
        }
        Pump();
    }

    static void IncrementalLayout()
    {
        using (var actor = new OverlayPetActor("layout", null))
        {
            actor.ApplyChrome(Chrome(128));
            actor.Apply("同一昵称", "sit", "seated", 0, 0, petId: "orange");
            actor.Place(500, 300);
            var passes = actor.LayoutPassCount;
            var x = Canvas.GetLeft(actor); var y = Canvas.GetTop(actor);
            for (var i = 0; i < 100; i++)
            {
                actor.ApplyChrome(Chrome(128));
                actor.Apply("同一昵称", "sit", "seated", i * 100, 0, petId: "orange");
                actor.Place(500, 300);
            }
            Require(actor.LayoutPassCount == passes, "unchanged snapshot forced another layout");
            Require(Canvas.GetLeft(actor) == x && Canvas.GetTop(actor) == y, "snapshot changed body origin");
            actor.Apply("更长的昵称需要重新排版", "sit", "seated", 0, 0, petId: "orange");
            Require(actor.LayoutPassCount == passes + 1, "name edit did not remeasure");
            actor.ApplyChrome(Chrome(96)); actor.Measure(new Size(1000, 1000)); actor.Arrange(new Rect(0, 0, actor.Width, actor.Height));
            var body = Field<Grid>(actor, "_body");
            var origin = body.TranslatePoint(new Point(48, 48), actor);
            Require(Math.Abs(Canvas.GetLeft(actor) + origin.X - 500) < .01 &&
                Math.Abs(Canvas.GetTop(actor) + origin.Y - 300) < .01, "computed center differs from WPF arrangement");
        }
    }

    static OverlayChatDto Chat(string id, string text) => new OverlayChatDto { MessageId = id, Nickname = "猫", Text = text };
    static void IncrementalChat()
    {
        var list = new StackPanel(); var preview = new TextBlock();
        var presenter = new OverlayChatPresenter(preview, new ScrollViewer { Content = list }, list);
        presenter.UpdateMessages(new[] { Chat("1", "甲"), Chat("2", "乙") });
        var first = list.Children[0]; var second = list.Children[1];
        presenter.UpdateMessages(new[] { Chat("1", "甲"), Chat("2", "乙"), Chat("3", "丙") });
        Require(ReferenceEquals(first, list.Children[0]), "append recreated first row");
        presenter.UpdateMessages(new[] { Chat("2", "乙"), Chat("3", "丙"), Chat("4", "丁") });
        Require(ReferenceEquals(second, list.Children[0]), "sliding window recreated surviving row");
        presenter.UpdateMessages(new[] { Chat("2", "乙"), Chat("3", "已编辑"), Chat("4", "丁") });
        Require(((TextBlock)list.Children[1]).Text.Contains("已编辑"), "middle edit was missed");
        presenter.SetExpanded(false);
        presenter.UpdateMessages(new[] { Chat("5", "新消息") });
        Require(list.Children.Count == 3 && preview.Text.Contains("新消息"), "collapsed list rebuilt or preview stale");
        presenter.SetExpanded(true);
        Require(list.Children.Count == 1 && ((TextBlock)list.Children[0]).Text.Contains("新消息"), "expanded list did not catch up");
    }

    static void AsyncFrames()
    {
        var ready = OverlayFishingLayers.EnsurePackAsync("orange"); WaitUntil(() => ready.IsCompleted);
        Require(ready.Result, "missing manifest");
        var path = OverlayFishingLayers.LayerFile("orange", "hooked", 0, "body");
        var before = OverlayFishingLayers.DecodeCount;
        var a = OverlayFishingLayers.BitmapAsync(path, 173, 138);
        var b = OverlayFishingLayers.BitmapAsync(path, 173, 138);
        WaitUntil(() => a.IsCompleted && b.IsCompleted);
        Require(a.Result != null && a.Result.IsFrozen && ReferenceEquals(a.Result, b.Result), "async image was not shared/frozen");
        Require(OverlayFishingLayers.DecodeCount == before + 1, "duplicate request decoded twice");
        using (var cancel = new CancellationTokenSource())
        {
            var leaving = OverlayFishingLayers.BitmapAsync(path, 171, 136, cancel.Token);
            var staying = OverlayFishingLayers.BitmapAsync(path, 171, 136);
            cancel.Cancel();
            WaitUntil(() => leaving.IsCompleted && staying.IsCompleted);
            Require(staying.Result != null && staying.Result.IsFrozen, "canceling one real actor canceled shared image");
        }
        var actor = new OverlayPetActor("stale", null);
        actor.ApplyChrome(Chrome(127));
        actor.Apply("cat", "cast", "casting", 0, 0, petId: "orange");
        // Start a frame request, then dispose before the asynchronous result is published.
        typeof(OverlayPetActor).GetMethod("RenderLatestFrame", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(actor, null);
        actor.Dispose();
        WaitUntil(() => OverlayFishingLayers.PendingDecodes == 0); Pump();
        foreach (var layer in Field<Image[]>(actor, "_layerImages")) Require(layer.Source == null, "late decode resurrected actor");
        using (var visible = new OverlayPetActor("swap", null))
        {
            visible.ApplyChrome(Chrome(128));
            visible.Apply("cat", "fishing", "waiting", 0, 0, petId: "orange");
            WaitUntil(() => !visible.FramePending && Field<FishingClipPlayback>(visible, "_layered") != null);
            var previous = Field<Image[]>(visible, "_layerImages")[0].Source;
            visible.Apply("cat", "fishing", "waiting", 0, 0, petId: "calico");
            Require(previous != null && ReferenceEquals(previous, Field<Image[]>(visible, "_layerImages")[0].Source),
                "state change cleared the last complete composite before replacement was ready");
            WaitUntil(() => !visible.FramePending && Field<string>(visible, "_petId") == "calico");
            var expected = OverlayFishingLayers.BitmapFor(OverlayFishingLayers.LayerFile("calico", "fishing", 0, "body"),
                Field<int>(visible, "_layerDrawW"), Field<int>(visible, "_layerDrawH"));
            Require(ReferenceEquals(expected, Field<Image[]>(visible, "_layerImages")[0].Source), "old species overwrote new frame");
        }
    }

    static void HiddenAnimation()
    {
        using (var actor = new OverlayPetActor("hidden", null))
        {
            actor.ApplyChrome(Chrome(128));
            actor.Apply("cat", "reel_empty", "resolving", 0, 0, petId: "orange");
            actor.Apply("cat", "sit", "seated", 0, 0, petId: "orange");
            actor.SetPresentationVisible(false);
            var timer = Field<OverlayAnimationSchedule.Subscription>(actor, "_frameTimer");
            var before = Field<int>(actor, "_frameIndex");
            timer.Due = OverlayAnimationClock.Now - 60000;
            Pump(); Require(before == Field<int>(actor, "_frameIndex"), "offscreen actor advanced presentation");
            actor.SetPresentationVisible(true);
            WaitUntil(() => !timer.IsEnabled);
            Require(Field<string>(actor, "_deferredState") == string.Empty, "reset did not flush deferred seated state");
            Require(Field<string>(actor, "_layeredClip") == "ready", "reel_empty/reset_empty did not finish at ready");
        }
    }

    static void ViewportPan()
    {
        var pan = new OverlayScenePan(new Canvas()); var changed = 0;
        pan.ViewportChanged += () => changed++;
        pan.SetViewportSize(960, 560); pan.SetSceneSize(2170, 725, true);
        pan.TryCenterOnWorldX(10000, true);
        Require(pan.OffsetX == -1210 && changed > 0, "right clamp or viewport notification lost");
        pan.TryCenterOnWorldX(-10000, true); Require(pan.OffsetX == 0, "left clamp lost");
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
