using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace FishSocialOverlay
{
    internal static class OverlayAnimationClock
    {
        static readonly Stopwatch Watch = Stopwatch.StartNew();
        static readonly OverlayAnimationSchedule Schedule = new OverlayAnimationSchedule(() => Now);
        static readonly DispatcherTimer Driver = new DispatcherTimer(DispatcherPriority.Background)
        { Interval = TimeSpan.FromMilliseconds(16) };
        static bool _visible = true;
        static bool _workRequested;
        public static double Now => Watch.Elapsed.TotalMilliseconds;
        public static event Action AfterTick;
        static OverlayAnimationClock() { Driver.Tick += Tick; }
        public static OverlayAnimationSchedule.Subscription Create(double milliseconds)
            => Schedule.Create(milliseconds);
        public static void Wake()
        { _workRequested = true; if (_visible && !Driver.IsEnabled) Driver.Start(); }
        public static void SetVisible(bool visible)
        {
            _visible = visible;
            if (visible) Wake(); else Driver.Stop();
        }
        static void Tick(object sender, EventArgs e)
        {
            _workRequested = false;
            Schedule.Advance(Now);
            AfterTick?.Invoke();
            if (Schedule.Count == 0 && !_workRequested) Driver.Stop();
        }
    }
}
