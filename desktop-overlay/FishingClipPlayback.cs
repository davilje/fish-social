using System;
using System.Collections.Generic;

namespace FishSocialOverlay
{
    sealed class FishingClipPlayback
    {
        public FishingClipPlayback(string playback, int loopStart, List<string[]> orders, int[] durationsMs, string nextClip)
        {
            Playback = playback;
            LoopStart = loopStart;
            Orders = orders;
            DurationsMs = durationsMs;
            NextClip = nextClip;
            SourceFrameCount = orders.Count;
        }

        public string Playback { get; }
        public int LoopStart { get; private set; }
        public List<string[]> Orders { get; private set; }
        public int FrameCount => Orders.Count;
        public int[] DurationsMs { get; private set; }
        public string NextClip { get; }
        public int DurationMs(int index) => DurationsMs[Math.Max(0, Math.Min(FrameCount - 1, index))];
        public int SourceFrameCount { get; }
        public FishingMotionSample[] Motion { get; private set; }
        public int SourceIndex(int index) => Motion == null ? index : Motion[Math.Max(0, Math.Min(FrameCount - 1, index))].Source;
        public double SourcePosition(int index) => Motion == null ? index : Motion[Math.Max(0, Math.Min(FrameCount - 1, index))].Position;
        public int IndexAtSource(int source)
        {
            if (Motion != null)
                for (var index = 0; index < Motion.Length; index++)
                    if (Motion[index].Position >= source) return index;
            return Math.Max(0, Math.Min(FrameCount - 1, source));
        }

        public void ApplyMotion(FishingMotionClip motion)
        {
            if (Motion != null || motion == null || !motion.Matches(DurationsMs)) return;
            var orders = new List<string[]>();
            var durations = new int[motion.Frames.Length];
            for (var index = 0; index < motion.Frames.Length; index++)
            {
                orders.Add(Orders[motion.Frames[index].Source]);
                durations[index] = motion.Frames[index].DurationMs;
            }
            Motion = motion.Frames;
            Orders = orders;
            DurationsMs = durations;
            LoopStart = IndexAtSource(LoopStart);
        }
    }
}
