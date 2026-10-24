using System;

namespace FishSocialOverlay
{
    // Scene-space feet, never sprite centres. The server owns seat occupancy;
    // this presentation-only trajectory may lag it while the cat stands up.
    internal sealed class OverlaySeatMotion
    {
        public double X { get; private set; }
        public double Y { get; private set; }
        public bool WaitingForStand { get; private set; }
        public bool Moving { get; private set; }
        double _fromX, _fromY, _toX, _toY, _started, _duration, _sampledAt;

        public void Snap(double x, double y)
        {
            X = _toX = x; Y = _toY = y;
            WaitingForStand = Moving = false;
        }

        public void WaitForStand(double x, double y)
        {
            _toX = x; _toY = y;
            WaitingForStand = true; Moving = false;
        }

        public void Retarget(double x, double y)
        {
            var changed = _toX != x || _toY != y;
            _toX = x; _toY = y;
            // Waiting-lane indices can change when another player leaves.
            // Rebase at the rendered position instead of bending the old path.
            if (Moving && changed) Begin(_sampledAt);
        }

        public void Begin(double now)
        {
            WaitingForStand = false;
            _fromX = X; _fromY = Y; _started = _sampledAt = now;
            var distance = Math.Sqrt(Math.Pow(_toX - X, 2) + Math.Pow(_toY - Y, 2));
            _duration = Math.Max(180, Math.Min(650, distance * 2));
            Moving = distance > .01;
            if (!Moving) Snap(_toX, _toY);
        }

        // Returns true once, at arrival. Repeated network snapshots do not reset time.
        public bool Advance(double now)
        {
            if (!Moving) return false;
            _sampledAt = now;
            var t = Math.Max(0, Math.Min(1, (now - _started) / _duration));
            var eased = t * t * (3 - 2 * t);
            X = _fromX + (_toX - _fromX) * eased;
            Y = _fromY + (_toY - _fromY) * eased;
            if (t < 1) return false;
            Snap(_toX, _toY);
            return true;
        }
    }
}
