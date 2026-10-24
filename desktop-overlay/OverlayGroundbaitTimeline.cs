using System;

namespace FishSocialOverlay
{
    internal sealed class OverlayGroundbaitTimeline
    {
        readonly double[] _boundaries;
        readonly int _loopStart;
        readonly int _loopEnd;
        readonly int _outroStart;
        readonly double _startedAt;
        double? _endedAt;

        public OverlayGroundbaitTimeline(int[] durationsMs, int loopStart, int loopEnd, int outroStart, double startedAt)
        {
            if (durationsMs == null || loopStart < 1 || loopEnd <= loopStart || loopEnd >= durationsMs.Length
                || outroStart < loopStart || outroStart >= durationsMs.Length)
                throw new ArgumentException("Groundbait requires intro, loop and outro frames.");
            _boundaries = new double[durationsMs.Length + 1];
            for (var index = 0; index < durationsMs.Length; index++)
            {
                if (durationsMs[index] <= 0) throw new ArgumentException("Frame durations must be positive.");
                _boundaries[index + 1] = _boundaries[index] + durationsMs[index];
            }
            _loopStart = loopStart;
            _loopEnd = loopEnd;
            _outroStart = outroStart;
            _startedAt = startedAt;
        }

        public bool Ending => _endedAt.HasValue;

        public void End(double now)
        {
            if (!_endedAt.HasValue) _endedAt = Math.Max(_startedAt, now);
        }

        public Frame Sample(double now)
        {
            double position;
            long occurrence = 0;
            if (_endedAt.HasValue)
            {
                position = _boundaries[_outroStart] + Math.Max(0, now - _endedAt.Value);
                if (position >= _boundaries[_boundaries.Length - 1])
                    return new Frame(_boundaries.Length - 2, 0, true);
            }
            else
            {
                position = Math.Max(0, now - _startedAt);
                if (position >= _boundaries[_loopStart])
                {
                    occurrence = (long)((position - _boundaries[_loopStart]) / (_boundaries[_loopEnd] - _boundaries[_loopStart]));
                    position = _boundaries[_loopStart] +
                        (position - _boundaries[_loopStart]) % (_boundaries[_loopEnd] - _boundaries[_loopStart]);
                }
            }
            var index = Array.BinarySearch(_boundaries, position);
            if (index < 0) index = ~index - 1;
            return new Frame(index, _boundaries[index + 1] - position, false, occurrence);
        }

        internal readonly struct Frame
        {
            public Frame(int index, double remainingMs, bool complete, long occurrence = 0)
            { Index = index; RemainingMs = remainingMs; Complete = complete; Occurrence = occurrence; }
            public long Occurrence { get; }
            public int Index { get; }
            public double RemainingMs { get; }
            public bool Complete { get; }
        }
    }
}
