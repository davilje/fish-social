#if OVERLAY_PERFORMANCE_TESTS
using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Threading;
using FishSocialOverlay;

internal static class OverlayGroundbaitNativeTests
{
    static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, PrivateInstance).GetValue(owner);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Tick(OverlayPetActor actor)
        => typeof(OverlayPetActor).GetMethod("OnFrameTick", PrivateInstance).Invoke(actor, new object[] { null, EventArgs.Empty });
    static void Wait(Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done())
        {
            Check(watch.ElapsedMilliseconds < 15000, "groundbait native test timed out");
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(1);
        }
    }
    static void Load(string pet = "orange")
    {
        var load = OverlayFishingLayers.EnsurePackAsync(pet);
        Wait(() => load.IsCompleted);
        Check(load.Result, "missing groundbait pack: " + pet);
    }
    static OverlayGroundbaitTimeline Timeline(OverlayPetActor actor) => Field<OverlayGroundbaitTimeline>(actor, "_groundbaitTimeline");
    static FishingClipPlayback Clip(OverlayPetActor actor) => Field<FishingClipPlayback>(actor, "_layered");
    static int SourceFrame(OverlayPetActor actor) => Clip(actor).SourceIndex(Field<int>(actor, "_frameIndex"));
    static void Age(OverlayPetActor actor, bool ending)
    {
        var clip = Clip(actor);
        var timeline = new OverlayGroundbaitTimeline(clip.DurationsMs, clip.IndexAtSource(1), clip.IndexAtSource(7), clip.IndexAtSource(6),
            OverlayAnimationClock.Now - 60000);
        if (ending) timeline.End(OverlayAnimationClock.Now - 1000);
        typeof(OverlayPetActor).GetField("_groundbaitTimeline", PrivateInstance).SetValue(actor, timeline);
    }

    public static void PhaseLifecycle()
    {
        Load();
        var activeClips = OverlayFishingLayers.ActiveClipCount;
        using (var actor = new OverlayPetActor("groundbait-phase", null))
        {
            var deadline = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 30000;
            actor.Apply("cat", "sit", "groundbaiting", 2000, deadline, petId: "orange");
            Check(Timeline(actor) != null && Field<string>(actor, "_layeredClip") == "groundbait", "phase did not override stale visual");
            var timeline = Timeline(actor);
            actor.Apply("cat", "groundbait", "groundbaiting", 2100, deadline + 1000, petId: "orange", groundbaitActive: true);
            Check(ReferenceEquals(timeline, Timeline(actor)), "buff or deadline refresh restarted the episode");
            Check(Field<long>(actor, "_hookDeadlineMs") == deadline + 1000, "real deadline was rewritten by presentation");
            Age(actor, false); Tick(actor);
            Check(SourceFrame(actor) >= 1 && SourceFrame(actor) < 7, "long phase stopped before real completion");
            timeline = Timeline(actor);
            actor.Apply("cat", "groundbait", "groundbaiting", 0, 1, petId: "orange");
            Check(ReferenceEquals(timeline, Timeline(actor)) && !timeline.Ending, "expired deadline ended the phase loop");
            actor.Apply("cat", "groundbait", "seated", 0, 0, petId: "orange", groundbaitActive: true);
            Check(Timeline(actor).Ending && SourceFrame(actor) == 6, "actual phase exit did not enter hand-return outro");
            actor.Apply("cat", "cast", "casting", 0, 0, petId: "orange");
            actor.Apply("cat", "fishing", "waiting", 0, 0, petId: "orange");
            Check(Field<string>(actor, "_deferredState") == "fishing", "outro lost latest network destination");
            Age(actor, true); Tick(actor);
            Check(Timeline(actor) == null && Field<string>(actor, "_layeredClip") == "fishing", "outro failed to flush latest destination");
        }
        Check(OverlayFishingLayers.ActiveClipCount == activeClips, "groundbait transition leaked acquired clips");
    }

    public static void RepeatAndInterrupt()
    {
        Load();
        var actor = new OverlayPetActor("groundbait-repeat", null);
        try
        {
            actor.Apply("cat", "groundbait", "groundbaiting", 0, 0, petId: "orange");
            actor.Apply("cat", "sit", "seated", 0, 0, petId: "orange");
            var ending = Timeline(actor);
            Check(ending.Ending && SourceFrame(actor) == 6, "short phase skipped its hand-return outro");
            actor.Apply("cat", "groundbait", "groundbaiting", 0, 0, petId: "orange", groundbaitActive: true);
            Check(Timeline(actor) != ending && !Timeline(actor).Ending && SourceFrame(actor) == 0, "repeat did not restart intro");
            actor.Apply("cat", "offline", "idle", 0, 0, petId: "orange", groundbaitActive: true);
            Check(Timeline(actor) == null && Field<string>(actor, "_layeredClip") == "idle", "offline did not cancel groundbait");
            actor.Apply("cat", "groundbait", "groundbaiting", 0, 0, petId: "orange");
            actor.Apply("cat", "dragging", "groundbaiting", 0, 0, petId: "orange");
            Check(Timeline(actor) == null && Field<string>(actor, "_layeredClip") == "idle", "dragging did not cancel groundbait");
            actor.Apply("cat", "groundbait", "groundbaiting", 0, 0, petId: "orange");
        }
        finally { actor.Dispose(); }
        Check(Timeline(actor) == null && !Field<OverlayAnimationSchedule.Subscription>(actor, "_frameTimer").IsEnabled,
            "disposed groundbait left a timeline or running callback");
    }

    public static void HiddenAndPetChange()
    {
        Load(); Load("calico");
        using (var actor = new OverlayPetActor("groundbait-hidden", null))
        {
            actor.Apply("cat", "groundbait", "groundbaiting", 0, 0, petId: "orange");
            Wait(() => !actor.FramePending);
            actor.SetPresentationVisible(false);
            var timer = Field<OverlayAnimationSchedule.Subscription>(actor, "_frameTimer");
            var decoded = OverlayFishingLayers.DecodeCount;
            var source = Field<Image[]>(actor, "_layerImages")[0].Source;
            actor.Apply("cat", "groundbait", "groundbaiting", 0, 0, petId: "calico");
            Check(timer.Suspended && Field<string>(actor, "_petId") == "calico", "hidden skin change lost suspension");
            Check(OverlayFishingLayers.DecodeCount == decoded && ReferenceEquals(source, Field<Image[]>(actor, "_layerImages")[0].Source),
                "hidden skin change decoded or cleared the complete composite");
            actor.Apply("cat", "sit", "seated", 0, 0, petId: "calico", groundbaitActive: true);
            Age(actor, true);
            actor.SetPresentationVisible(true);
            Tick(actor);
            Wait(() => !actor.FramePending);
            Check(Timeline(actor) == null && Field<string>(actor, "_layeredClip") == "ready", "hidden outro failed to resume to ready");
            var layers = Field<Image[]>(actor, "_layerImages");
            var body = Array.IndexOf(OverlayFishingLayers.LayerNames, "body");
            var expected = OverlayFishingLayers.BitmapFor(OverlayFishingLayers.LayerFile("calico", "ready", 0, "body"),
                Field<int>(actor, "_layerDrawW"), Field<int>(actor, "_layerDrawH"));
            Check(expected != null && ReferenceEquals(expected, layers[body].Source), "stale groundbait decode replaced resumed destination");
        }
    }
}
#endif
