using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FishSocialOverlay;

public static class OverlaySchedulingTests
{
    static int passed;
    public static void Main()
    {
        Run("unequal frame durations catch up without drift", Durations);
        Run("once transitions retain debt and fire exactly once", Transitions);
        Run("stopped callbacks are removed during dispatch", Removed);
        Run("suspended actors resume against elapsed time", Visibility);
        Run("loop periods skip overdue redundant frames", Loop);
        Run("long catch-up is bounded without losing terminals", BoundedCatchup);
        Run("decode queue bounds concurrency and admission", QueueBounds);
        Run("decode failure releases worker and capacity", QueueFailure);
        Run("canceled queued decode never reads its file", QueueCancellation);
        Run("canceling one waiter preserves other shared users", SharedCancellation);
        Console.WriteLine("PASS: " + passed + "/" + passed + " scheduling/worker tests");
    }
    static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Durations()
    {
        double now = 0;
        var clock = new OverlayAnimationSchedule(() => now);
        var timer = clock.Create(100);
        var durations = new[] { 100, 360, 100, 200 };
        var frame = 0;
        timer.Tick += (_, __) => { frame++; if (frame == durations.Length) timer.Stop(); else timer.Interval = TimeSpan.FromMilliseconds(durations[frame]); };
        timer.Start();
        now = 459; clock.Advance(now); Check(frame == 1, "advanced before 460ms boundary");
        now = 650; clock.Advance(now); Check(frame == 3, "lost elapsed frame durations");
        now = 760; clock.Advance(now); Check(frame == 4 && clock.Count == 0, "terminal duration wrong");
        clock.Advance(5000); Check(frame == 4, "terminal fired twice");
    }
    static void Transitions()
    {
        double now = 0;
        var clock = new OverlayAnimationSchedule(() => now);
        var timer = clock.Create(100);
        var events = new List<string>();
        timer.Tick += (_, __) =>
        {
            timer.Stop();
            if (events.Count == 0) { events.Add("reel"); timer.Interval = TimeSpan.FromMilliseconds(360); timer.Start(); }
            else if (events.Count == 1) { events.Add("catch"); timer.Interval = TimeSpan.FromMilliseconds(200); timer.Start(); }
            else events.Add("reset");
        };
        timer.Start(); now = 1000; clock.Advance(now); clock.Advance(now + 1000);
        Check(string.Join(",", events) == "reel,catch,reset", "transition restarted from wall time or repeated");
    }
    static void Removed()
    {
        double now = 0;
        var clock = new OverlayAnimationSchedule(() => now);
        var a = clock.Create(100); var b = clock.Create(100); var count = 0;
        a.Tick += (_, __) => { a.Stop(); b.Stop(); }; b.Tick += (_, __) => count++;
        a.Start(); b.Start(); now = 200; clock.Advance(now);
        Check(count == 0 && clock.Count == 0, "departed actor callback ran");
    }
    static void Visibility()
    {
        double now = 0;
        var clock = new OverlayAnimationSchedule(() => now); var timer = clock.Create(100); var count = 0;
        timer.Tick += (_, __) => count++;
        timer.Start(); timer.Suspended = true; now = 500; clock.Advance(now);
        Check(count == 0 && clock.Count == 0, "hidden actor continued work");
        timer.Suspended = false; clock.Advance(now); Check(count == 5, "resume lost timeline"); timer.Stop();
    }
    static void Loop()
    {
        double now = 0;
        var clock = new OverlayAnimationSchedule(() => now); var timer = clock.Create(100); var count = 0;
        timer.Tick += (_, __) => { timer.SkipPeriods(now, 400); count++; };
        timer.Start(); now = 3600050; clock.Advance(now);
        Check(count == 4, "loop replayed an hour of hidden frames"); timer.Stop();
    }
    static void BoundedCatchup()
    {
        double now = 0;
        var clock = new OverlayAnimationSchedule(() => now); var timer = clock.Create(1); var count = 0;
        timer.Tick += (_, __) => { if (++count == 200) timer.Stop(); };
        timer.Start(); now = 1000; clock.Advance(now); Check(count == 128, "dispatch not bounded");
        clock.Advance(now); Check(count == 200 && clock.Count == 0, "terminal debt was discarded");
    }
    static void QueueBounds()
    {
        var queue = new OverlayDecodeQueue(2, 4);
        using (var release = new ManualResetEventSlim())
        using (var entered = new CountdownEvent(2))
        {
            var running = 0; var peak = 0;
            Func<object> decode = () =>
            {
                var active = Interlocked.Increment(ref running);
                int old;
                do { old = Volatile.Read(ref peak); } while (active > old && Interlocked.CompareExchange(ref peak, active, old) != old);
                if (entered.CurrentCount > 0) { lock (entered) { if (entered.CurrentCount > 0) entered.Signal(); } }
                if (!release.Wait(5000)) throw new Exception("worker blocked");
                Interlocked.Decrement(ref running); return new object();
            };
            var jobs = new[] { queue.TryRun(decode), queue.TryRun(decode), queue.TryRun(decode), queue.TryRun(decode) };
            try
            {
                Check(entered.Wait(5000), "workers never entered");
                Check(queue.Pending == 4 && queue.TryRun(decode) == null, "queue admitted unbounded work");
            }
            finally { release.Set(); }
            Task.WaitAll(jobs);
            Check(peak == 2 && queue.Pending == 0, "concurrency exceeded limit or work leaked");
        }
    }
    static void QueueCancellation()
    {
        var queue = new OverlayDecodeQueue(1, 2);
        using (var release = new ManualResetEventSlim())
        using (var entered = new ManualResetEventSlim())
        using (var cancel = new CancellationTokenSource())
        {
            var active = queue.TryRun(() => { entered.Set(); release.Wait(5000); return new object(); });
            Check(entered.Wait(5000), "worker not started");
            var touched = false;
            var queued = queue.TryRun(() => { touched = true; return new object(); }, cancel.Token);
            try
            {
                cancel.Cancel();
                try { queued.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
                Check(!touched && queue.Pending == 1, "canceled work still decoded/occupied capacity");
            }
            finally { release.Set(); }
            active.GetAwaiter().GetResult();
        }
    }
    static void SharedCancellation()
    {
        var shared = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var cancel = new CancellationTokenSource())
        {
            var leaving = OverlayTaskWait.WithCancellation(shared.Task, cancel.Token);
            var staying = OverlayTaskWait.WithCancellation(shared.Task, CancellationToken.None);
            cancel.Cancel();
            try { leaving.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
            Check(leaving.IsCanceled && !shared.Task.IsCompleted, "one waiter canceled the shared producer");
            var image = new object(); shared.SetResult(image);
            Check(ReferenceEquals(image, staying.GetAwaiter().GetResult()), "remaining waiter lost its result");
        }
    }

    static void QueueFailure()
    {
        var queue = new OverlayDecodeQueue(1, 1);
        try { queue.TryRun<object>(() => throw new InvalidOperationException("fixture")).GetAwaiter().GetResult(); }
        catch (InvalidOperationException) { }
        Check(queue.Pending == 0, "failed task retained capacity");
        Check(queue.TryRun(() => new object()).GetAwaiter().GetResult() != null, "worker not reusable");
    }
}
