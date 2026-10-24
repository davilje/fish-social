using System;

namespace FishSocialOverlay
{
    internal sealed class OverlayFishingRestMotion
    {
        public const double FootX = 264;
        public const double FootY = 450;
        public const double FishingTipX = 541.5;
        public const double FishingTipY = 149;
        public const double TransitionMs = 800;
        double _startedAt = double.NaN;
        double _changedAt;
        double _fromSleep;
        bool _sleeping;

        public RestPose Update(double now, bool active, bool sleeping)
        {
            if (!active)
            {
                _startedAt = double.NaN;
                _fromSleep = 0;
                _sleeping = false;
                return RestPose.Identity;
            }
            if (double.IsNaN(_startedAt))
            {
                _startedAt = now;
                _changedAt = now;
                _sleeping = sleeping;
            }
            if (_sleeping != sleeping)
            {
                _fromSleep = Blend(now);
                _changedAt = now;
                _sleeping = sleeping;
            }
            return Sample(Math.Max(0, now - _startedAt), Blend(now));
        }

        double Blend(double now)
        {
            var progress = Math.Max(0, Math.Min(1, (now - _changedAt) / TransitionMs));
            progress = progress * progress * (3 - 2 * progress);
            return _fromSleep + ((_sleeping ? 1 : 0) - _fromSleep) * progress;
        }

        public static RestPose Sample(double elapsedMs, double sleepBlend)
        {
            sleepBlend = Math.Max(0, Math.Min(1, sleepBlend));
            var awakeWave = Math.Sin(elapsedMs * Math.PI * 2 / 3200);
            var sleepWave = Math.Sin(elapsedMs * Math.PI * 2 / 4600);
            var growth = 1 + .006 * awakeWave * (1 - sleepBlend) + .009 * sleepWave * sleepBlend;
            var angle = (2.1 + .9 * Math.Sin(elapsedMs * Math.PI * 2 / 5800)) * sleepBlend * Math.PI / 180;
            var cosine = Math.Cos(angle);
            var sine = Math.Sin(angle);
            var pose = new RestPose
            {
                Matrix = new[] { cosine, sine, -sine * growth, cosine * growth, 0d, 0d },
                SleepBlend = sleepBlend,
                BubbleOpacity = sleepBlend * (.65 + .25 * sleepWave),
                BubbleRise = 3 * (1 + sleepWave)
            };
            pose.Matrix[4] = FootX - cosine * FootX + sine * growth * FootY;
            pose.Matrix[5] = FootY - sine * FootX - cosine * growth * FootY;
            return pose;
        }
    }

    internal struct RestPose
    {
        public double[] Matrix;
        public double SleepBlend, BubbleOpacity, BubbleRise;
        public static RestPose Identity => new RestPose { Matrix = new[] { 1d, 0, 0, 1, 0, 0 } };
        public double X(double sourceX, double sourceY) => Matrix[0] * sourceX + Matrix[2] * sourceY + Matrix[4];
        public double Y(double sourceX, double sourceY) => Matrix[1] * sourceX + Matrix[3] * sourceY + Matrix[5];

        public double[] Line(double tipX, double tipY, double bottom)
        {
            var length = bottom - tipY;
            if (length <= 1) return Identity.Matrix;
            var deltaX = X(tipX, tipY) - tipX;
            var deltaY = Y(tipX, tipY) - tipY;
            return new[] { 1d, 0, -deltaX / length, 1 - deltaY / length,
                deltaX * bottom / length, deltaY * bottom / length };
        }
    }
}
