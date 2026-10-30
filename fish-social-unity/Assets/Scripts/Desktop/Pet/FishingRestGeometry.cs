using System;

namespace FishSocial.Motion
{
    // Shared by Unity and native Overlay; coordinates match the approved web study.
    public static class FishingRestGeometry
    {
        public const double AwakeAmplitude = .022, SleepAmplitude = .028;
        public const double Duration = 6400;
        static readonly double[] Times = { 0, 900, 1900, 2700, 3300, 3900, 4400, 5200, 6400 };
        static readonly double[] Values = { 0, .08, .48, 1, 1, .18, .32, 0, 0 };
        public static double Nod(double time)
        {
            var phase = (time % Duration + Duration) % Duration;
            for (var i = 1; i < Times.Length; i++)
                if (phase < Times[i])
                {
                    var t = (phase - Times[i - 1]) / (Times[i] - Times[i - 1]);
                    return Values[i - 1] + (Values[i] - Values[i - 1]) * t * t * (3 - 2 * t);
                }
            return 0;
        }
        public static void Point(double x, double y, double amount, out double px, out double py)
        {
            var w = Math.Max(0, Math.Min(1, (410 - y) / 180));
            w = w * w * (3 - 2 * w);
            var angle = amount * 8 * Math.PI / 180;
            var c = Math.Cos(angle); var s = Math.Sin(angle);
            var dx = x - 282; var dy = y - 337;
            px = x + w * (c * dx - s * dy - dx + 3 * amount);
            py = y + w * (s * dx + c * dy - dy + 11 * amount);
        }
        public static double[] Rod(double amount)
        {
            const double x = 346, y = 348.5;
            Point(x, y, amount, out var px, out var py);
            var angle = amount * 1.5 * Math.PI / 180;
            var c = Math.Cos(angle); var s = Math.Sin(angle);
            return new[] { c, s, -s, c, px - c * x + s * y, py - s * x - c * y };
        }
        public static double[] Multiply(double[] a, double[] b) => new[] {
            a[0]*b[0]+a[2]*b[1], a[1]*b[0]+a[3]*b[1], a[0]*b[2]+a[2]*b[3], a[1]*b[2]+a[3]*b[3],
            a[0]*b[4]+a[2]*b[5]+a[4], a[1]*b[4]+a[3]*b[5]+a[5] };
    }
}
