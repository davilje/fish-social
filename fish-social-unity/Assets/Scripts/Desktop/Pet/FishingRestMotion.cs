using System;
using FishSocial.Motion;

namespace FishSocial.Desktop.Pet
{
    public sealed class FishingRestMotion
    {
        public const double FootX = 264;
        public const double FootY = 450;
        public const double RodTipX = 541.5;
        public const double RodTipY = 149;
        public const double LineBottom = 471;
        public const double TransitionMs = 800;
        double _startedAt = double.NaN;
        double _changedAt;
        double _fromSleep;
        bool _sleeping;

        public FishingRestPose Update(double now, bool active, bool sleeping)
        {
            if (!active)
            {
                _startedAt = double.NaN;
                _fromSleep = 0;
                _sleeping = false;
                return FishingRestPose.Identity;
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

        public static FishingRestPose Sample(double elapsedMs, double sleepBlend)
        {
            sleepBlend = Math.Max(0, Math.Min(1, sleepBlend));
            var awakeWave = Math.Sin(elapsedMs * Math.PI * 2 / 3200);
            var sleepWave = Math.Sin(elapsedMs * Math.PI * 2 / 4600);
            var growth = 1 + FishingRestGeometry.AwakeAmplitude * awakeWave * (1 - sleepBlend) + FishingRestGeometry.SleepAmplitude * sleepWave * sleepBlend;
            var angle = (2.1 + .9 * Math.Sin(elapsedMs * Math.PI * 2 / 5800)) * sleepBlend * Math.PI / 180;
            var cosine = Math.Cos(angle);
            var sine = Math.Sin(angle);
            return new FishingRestPose
            {
                Body = new FishingRestTransform
                {
                    XX = cosine, YX = sine, XY = -sine * growth, YY = cosine * growth,
                    OffsetX = FootX - cosine * FootX + sine * growth * FootY,
                    OffsetY = FootY - sine * FootX - cosine * growth * FootY,
                },
                SleepBlend = sleepBlend,
                Nod = FishingRestGeometry.Nod(elapsedMs) * sleepBlend,
                BubbleOpacity = sleepBlend * (.65 + .25 * sleepWave),
                BubbleRise = 3 * (1 + sleepWave),
            };
        }
    }

    public struct FishingRestPose
    {
        public FishingRestTransform Body;
        public double SleepBlend, BubbleOpacity, BubbleRise, Nod;
        public FishingRestTransform Rod => FishingRestTransform.FromArray(FishingRestGeometry.Multiply(Body.ToArray(), FishingRestGeometry.Rod(Nod)));
        public static FishingRestPose Identity => new FishingRestPose { Body = FishingRestTransform.Identity };

        public FishingRestTransform Line(double tipX, double tipY, double bottom)
        {
            var length = bottom - tipY;
            if (length <= 1)
                return FishingRestTransform.Identity;
            var rod = Rod;
            var deltaX = rod.X(tipX, tipY) - tipX;
            var deltaY = rod.Y(tipX, tipY) - tipY;
            return new FishingRestTransform
            {
                XX = 1, XY = -deltaX / length, YY = 1 - deltaY / length,
                OffsetX = deltaX * bottom / length, OffsetY = deltaY * bottom / length,
            };
        }
    }

    public struct FishingRestTransform
    {
        public double XX, YX, XY, YY, OffsetX, OffsetY;
        public static FishingRestTransform Identity => new FishingRestTransform { XX = 1, YY = 1 };
        public double[] ToArray() => new[] { XX, YX, XY, YY, OffsetX, OffsetY };
        public static FishingRestTransform FromArray(double[] m) => new FishingRestTransform
        { XX=m[0], YX=m[1], XY=m[2], YY=m[3], OffsetX=m[4], OffsetY=m[5] };
        public double X(double sourceX, double sourceY) => XX * sourceX + XY * sourceY + OffsetX;
        public double Y(double sourceX, double sourceY) => YX * sourceX + YY * sourceY + OffsetY;
    }
}
