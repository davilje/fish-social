using System;
using System.Collections.Generic;

namespace FishSocialOverlay
{
    // Pure scheduling core: one monotonic timestamp drives every actor. Start during
    // a callback inherits that boundary, so once->next transitions retain elapsed time.
    internal sealed class OverlayAnimationSchedule
    {
        readonly List<Subscription> _active = new List<Subscription>();
        readonly List<Subscription> _snapshot = new List<Subscription>();
        readonly Func<double> _now;
        public OverlayAnimationSchedule(Func<double> now) { _now = now; }
        public int Count
        {
            get { var count = 0; foreach (var timer in _active) if (!timer.Suspended) count++; return count; }
        }
        public Subscription Create(double milliseconds) => new Subscription(this, milliseconds);
        public void Advance(double now)
        {
            _snapshot.Clear();
            _snapshot.AddRange(_active);
            try
            {
                foreach (var timer in _snapshot)
                {
                    // Bound a single dispatch. Debt is retained, never silently discard
                    // a terminal boundary. Loop periods are skipped by the actor itself.
                    var remaining = 128;
                    while (!timer.Suspended && timer.IsEnabled && timer.Due <= now && remaining-- > 0)
                    {
                        timer.Firing = true;
                        timer.Restarted = false;
                        try { timer.Fire(); }
                        finally { timer.Firing = false; }
                        if (timer.IsEnabled && !timer.Restarted)
                            timer.Due += timer.Interval.TotalMilliseconds;
                    }
                }
            }
            finally { _snapshot.Clear(); }
        }
        internal sealed class Subscription
        {
            readonly OverlayAnimationSchedule _owner;
            TimeSpan _interval;
            internal double Due;
            internal bool Firing, Restarted;
            public event EventHandler Tick;
            public bool IsEnabled { get; private set; }
            public bool Suspended { get; set; }
            public TimeSpan Interval
            {
                get => _interval;
                set => _interval = TimeSpan.FromMilliseconds(Math.Max(1, value.TotalMilliseconds));
            }
            public Subscription(OverlayAnimationSchedule owner, double milliseconds)
            { _owner = owner; Interval = TimeSpan.FromMilliseconds(milliseconds); }
            public void Start()
            {
                if (IsEnabled) return;
                IsEnabled = true;
                Due = (Firing ? Due : _owner._now()) + Interval.TotalMilliseconds;
                Restarted = true;
                _owner._active.Add(this);
            }
            public void Stop()
            { if (!IsEnabled) return; IsEnabled = false; _owner._active.Remove(this); }
            public void SkipPeriods(double now, double period)
            {
                if (period > 0 && now > Due + period)
                    Due += Math.Floor((now - Due) / period) * period;
            }
            internal void Fire() => Tick?.Invoke(this, EventArgs.Empty);
        }
    }
}
