using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>Literal easing curves from prototype/cozy-journal/app.js, sampled as CSS cubic-bezier(x,y).</summary>
    public static class CozyWebMotion
    {
        public enum Ease { Linear, OutQuad, OutCubic, OutBack, InOutSine, InQuad, Fly, ItemFly, CssEase }
        public static float Evaluate(Ease ease, float progress)
        {
            if (progress <= 0) return 0;
            if (progress >= 1) return 1;
            switch (ease)
            {
                case Ease.CssEase: return Bezier(progress, .25f, .1f, .25f, 1);
                case Ease.OutQuad: return Bezier(progress, .5f, 1, .89f, 1);
                case Ease.OutCubic: return Bezier(progress, .33f, 1, .68f, 1);
                case Ease.OutBack: return Bezier(progress, .34f, 1.56f, .64f, 1);
                case Ease.InOutSine: return Bezier(progress, .37f, 0, .63f, 1);
                case Ease.InQuad: return Bezier(progress, .11f, 0, .5f, 0);
                case Ease.ItemFly: return Bezier(progress, .45f, 0, .25f, 1);
                case Ease.Fly: return Bezier(progress, .45f, 0, .3f, 1);
                default: return progress;
            }
        }
        static float Bezier(float x, float x1, float y1, float x2, float y2)
        {
            var low = 0f;
            var high = 1f;
            for (var i = 0; i < 18; i++)
            {
                var t = (low + high) * .5f;
                if (Point(t, x1, x2) < x) low = t; else high = t;
            }
            return Point((low + high) * .5f, y1, y2);
        }
        static float Point(float t, float a, float b)
        {
            var u = 1 - t;
            return 3 * u * u * t * a + 3 * u * t * t * b + t * t * t;
        }
        public static float Segment(float progress, float start, float end)
        {
            return Mathf.Clamp01((progress - start) / (end - start));
        }
        public static float Frames(float progress, params float[] values)
        {
            if (values == null || values.Length == 0) return 0;
            if (values.Length == 1) return values[0];
            var position = Mathf.Clamp01(progress) * (values.Length - 1);
            var index = Mathf.Min(Mathf.FloorToInt(position), values.Length - 2);
            return Mathf.LerpUnclamped(values[index], values[index + 1], position - index);
        }
    }
}
