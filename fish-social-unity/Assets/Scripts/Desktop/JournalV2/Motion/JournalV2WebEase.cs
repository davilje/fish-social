using UnityEngine;

namespace FishSocial.Desktop.JournalV2
{
    /// <summary>CSS cubic-bezier inversion, preserving OutBack overshoot. Coordinates copied from app.js EASE.</summary>
    public static class JournalV2WebEase
    {
        public enum Curve { Linear, Css, OutQuad, OutCubic, OutBack, InQuad, InOutSine, CoinFly, ItemFly }
        public static float Evaluate(Curve curve, float x)
        {
            switch (curve)
            {
                case Curve.Css: return Bezier(x, .25f, .1f, .25f, 1);
                case Curve.OutQuad: return Bezier(x, .5f, 1, .89f, 1);
                case Curve.OutCubic: return Bezier(x, .33f, 1, .68f, 1);
                case Curve.OutBack: return Bezier(x, .34f, 1.56f, .64f, 1);
                case Curve.InQuad: return Bezier(x, .11f, 0, .5f, 0);
                case Curve.InOutSine: return Bezier(x, .37f, 0, .63f, 1);
                case Curve.CoinFly: return Bezier(x, .45f, 0, .3f, 1);
                case Curve.ItemFly: return Bezier(x, .45f, 0, .25f, 1);
                default: return Mathf.Clamp01(x);
            }
        }
        public static float Bezier(float x, float x1, float y1, float x2, float y2)
        {
            if (x <= 0) return 0; if (x >= 1) return 1;
            double lo = 0, hi = 1;
            for (var i = 0; i < 30; i++)
            {
                var t = (lo + hi) * .5;
                if (Point(t, x1, x2) < x) lo = t; else hi = t;
            }
            return (float)Point((lo + hi) * .5, y1, y2);
        }
        static double Point(double t, double a, double b)
        { var u = 1 - t; return 3 * u * u * t * a + 3 * u * t * t * b + t * t * t; }
        public static float Frames(float progress, params float[] values)
        {
            if (values == null || values.Length == 0) return 0;
            if (values.Length == 1) return values[0];
            var at = Mathf.Clamp01(progress) * (values.Length - 1);
            var i = Mathf.Min(values.Length - 2, Mathf.FloorToInt(at));
            return Mathf.LerpUnclamped(values[i], values[i + 1], at - i);
        }
        public static float StableAngle(string id)
        {
            unchecked
            {
                var hash = 7;
                foreach (var character in id ?? string.Empty) hash = hash * 31 + character;
                return (System.Math.Abs((long)hash) % 300) / 100f - 1.5f;
            }
        }
    }
}
