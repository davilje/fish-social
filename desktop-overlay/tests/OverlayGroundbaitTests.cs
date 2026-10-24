#if OVERLAY_GROUNDBAIT_TESTS
using System;
using FishSocialOverlay;

public static class OverlayGroundbaitTests
{
    static readonly int[] Durations = { 100, 120, 120, 120, 120, 120, 120, 160 };
    static int _passed;

    public static void Main()
    {
        Run("authored intro and loop boundaries", Boundaries);
        Run("long phase never enters outro or stops", LongPhase);
        Run("short phase ends directly in timed outro", ShortPhase);
        Run("duplicate completion does not extend outro", DuplicateEnd);
        Run("new episode starts a fresh intro", Repeat);
        Run("network gap keeps looping until actual phase exit", NetworkGap);
        Run("expanded samples preserve source timing", ExpandedFrames);
        Run("hidden and low cadence scheduling catch up without replay", PausedScheduling);
        Run("cancel removes the scheduled callback", Cancel);
        Run("invalid segments and durations fail early", InvalidMetadata);
        Console.WriteLine("PASS: " + _passed + "/" + _passed + " groundbait timeline tests");
    }

    static OverlayGroundbaitTimeline New(double startedAt = 0)
        => new OverlayGroundbaitTimeline(Durations, 1, 7, 6, startedAt);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Run(string name, Action test) { test(); _passed++; Console.WriteLine("PASS " + name); }
    static void Frame(OverlayGroundbaitTimeline timeline, double now, int index, double remaining)
    {
        var frame = timeline.Sample(now);
        Check(!frame.Complete && frame.Index == index && Math.Abs(frame.RemainingMs - remaining) < .0001,
            "unexpected frame at " + now + ": " + frame.Index + "/" + frame.RemainingMs);
    }

    static void Boundaries()
    {
        var timeline = New();
        Frame(timeline, 0, 0, 100);
        Frame(timeline, 99.5, 0, .5);
        for (var index = 1; index < 7; index++) Frame(timeline, 100 + (index - 1) * 120, index, 120);
        Frame(timeline, 819, 6, 1);
        Frame(timeline, 820, 1, 120);
        Frame(timeline, 940, 2, 120);
    }

    static void LongPhase()
    {
        var timeline = New(1000);
        foreach (var elapsed in new[] { 980d, 5000, 60000, 3600000, 86400000 })
        {
            var frame = timeline.Sample(1000 + elapsed);
            Check(!frame.Complete && frame.Index >= 1 && frame.Index < 7, "long phase stopped or entered outro");
        }
    }

    static void ShortPhase()
    {
        var timeline = New();
        timeline.End(25);
        Frame(timeline, 25, 6, 120);
        Frame(timeline, 145, 7, 160);
        Frame(timeline, 304, 7, 1);
        Check(timeline.Sample(305).Complete, "short phase stretched or lost outro duration");
    }

    static void DuplicateEnd()
    {
        var timeline = New();
        timeline.End(5000);
        timeline.End(5100);
        Check(timeline.Sample(5280).Complete, "network refresh restarted outro");
        Check(timeline.Sample(100000).Complete, "completed timeline restarted");
    }

    static void Repeat()
    {
        var previous = New(); previous.End(20);
        var next = New(50);
        Frame(next, 50, 0, 100);
        Frame(next, 150, 1, 120);
        Check(!next.Ending && previous.Ending, "repeat reused ending state");
    }

    static void NetworkGap()
    {
        var timeline = New();
        Frame(timeline, 60000, 2, 100);
        timeline.End(60000);
        Frame(timeline, 60000, 6, 120);
        Frame(timeline, 60120, 7, 160);
        Check(timeline.Sample(60280).Complete, "late phase exit was backdated to a predicted deadline");
    }

    static void ExpandedFrames()
    {
        var expanded = new int[16];
        for (var index = 0; index < Durations.Length; index++)
        {
            expanded[index * 2] = Durations[index] / 2;
            expanded[index * 2 + 1] = Durations[index] - expanded[index * 2];
        }
        var source = New();
        var timeline = new OverlayGroundbaitTimeline(expanded, 2, 14, 12, 0);
        for (var now = 0; now < 10000; now += 13)
            Check(timeline.Sample(now).Index / 2 == source.Sample(now).Index, "expanded source frame timing changed");
        source.End(10000); timeline.End(10000);
        Frame(timeline, 10000, 12, 60);
        Frame(timeline, 10060, 13, 60);
        Frame(timeline, 10120, 14, 80);
        Frame(timeline, 10200, 15, 80);
        Check(timeline.Sample(10280).Complete && source.Sample(10280).Complete, "expanded outro drifted");
    }

    static void PausedScheduling()
    {
        double now = 0;
        var schedule = new OverlayAnimationSchedule(() => now);
        var timer = schedule.Create(100);
        var timeline = New();
        var callbacks = 0;
        var completions = 0;
        timer.Tick += (_, __) =>
        {
            callbacks++;
            var frame = timeline.Sample(now);
            timer.Stop();
            if (frame.Complete) { completions++; return; }
            timer.Interval = TimeSpan.FromMilliseconds(frame.RemainingMs);
            timer.Start();
            timer.Due = now + timer.Interval.TotalMilliseconds;
        };
        timer.Start(); timer.Suspended = true;
        now = 3600000; schedule.Advance(now);
        Check(callbacks == 0, "hidden actor advanced");
        timer.Suspended = false; schedule.Advance(now);
        Check(callbacks == 1 && timer.IsEnabled, "resume replayed obsolete frames or stopped looping");
        for (var update = 0; update < 10; update++) { now += 500; schedule.Advance(now); }
        Check(callbacks == 11, "low cadence accumulated frame debt");
        timer.Suspended = true; timeline.End(now);
        now += 10000; schedule.Advance(now);
        Check(completions == 0, "hidden actor completed presentation work");
        timer.Suspended = false; schedule.Advance(now); schedule.Advance(now + 10000);
        Check(completions == 1 && schedule.Count == 0, "resume did not finish exactly once");
    }

    static void Cancel()
    {
        double now = 0;
        var schedule = new OverlayAnimationSchedule(() => now);
        var timer = schedule.Create(100);
        var callbacks = 0;
        timer.Tick += (_, __) => callbacks++;
        timer.Start(); timer.Stop(); now = 100000; schedule.Advance(now);
        Check(callbacks == 0 && schedule.Count == 0, "canceled actor left active callbacks");
    }

    static void InvalidMetadata()
    {
        foreach (var bounds in new[] { new[] { 0, 7 }, new[] { 3, 3 }, new[] { 1, 8 } })
        {
            var threw = false;
            try { new OverlayGroundbaitTimeline(Durations, bounds[0], bounds[1], 6, 0); }
            catch (ArgumentException) { threw = true; }
            Check(threw, "invalid segment accepted");
        }
        var invalid = (int[])Durations.Clone(); invalid[4] = 0;
        var rejected = false;
        try { new OverlayGroundbaitTimeline(invalid, 1, 7, 6, 0); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "zero frame duration accepted");
        foreach (var outro in new[] { -1, Durations.Length })
        {
            var threw = false;
            try { new OverlayGroundbaitTimeline(Durations, 1, 7, outro, 0); }
            catch (ArgumentException) { threw = true; }
            Check(threw, "invalid independent outro boundary accepted");
        }
    }
}
#endif
