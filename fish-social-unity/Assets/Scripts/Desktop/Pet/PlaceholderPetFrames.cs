using UnityEngine;

namespace FishSocial.Desktop.Pet
{
    /// <summary>
    /// Square 256×256 placeholder sequence. Prefers Resources/Pet/PlaceholderCat
    /// so art can replace the generated frames without touching layout or state.
    /// </summary>
    public static class PlaceholderPetFrames
    {
        public const int Size = 256;

        static Sprite[] _frames;

        public static Sprite[] GetFrames()
        {
            if (_frames != null)
                return _frames;

            var loaded = Resources.Load<Sprite>("Pet/PlaceholderCat");
            if (loaded != null)
            {
                _frames = new[] { loaded };
                return _frames;
            }

            _frames = new[]
            {
                CreateFrame(0),
                CreateFrame(5),
            };
            return _frames;
        }

        static Sprite CreateFrame(int yOffset)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "PetPlaceholder",
            };

            var pixels = new Color32[Size * Size];
            var clear = new Color32(0, 0, 0, 0);
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            var fill = new Color32(77, 137, 168, 255);
            var stroke = new Color32(184, 225, 239, 255);
            var eye = new Color32(22, 33, 43, 255);
            var nose = new Color32(243, 201, 105, 255);
            var box = new Color32(90, 120, 140, 90);

            FillRect(pixels, 12, 12, Size - 24, Size - 24, box);
            DrawRectBorder(pixels, 8, 8, Size - 16, Size - 16, stroke);

            FillEllipse(pixels, 128, 110 + yOffset, 72, 68, fill);
            FillTriangle(pixels, 68, 148 + yOffset, 88, 208 + yOffset, 112, 152 + yOffset, fill);
            FillTriangle(pixels, 144, 152 + yOffset, 168, 208 + yOffset, 188, 148 + yOffset, fill);
            FillEllipse(pixels, 104, 122 + yOffset, 8, 11, eye);
            FillEllipse(pixels, 152, 122 + yOffset, 8, 11, eye);
            FillEllipse(pixels, 128, 102 + yOffset, 6, 4, nose);

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }

        static void FillRect(Color32[] pixels, int x, int y, int w, int h, Color32 color)
        {
            var x1 = Mathf.Clamp(x + w, 0, Size);
            var y1 = Mathf.Clamp(y + h, 0, Size);
            x = Mathf.Clamp(x, 0, Size);
            y = Mathf.Clamp(y, 0, Size);
            for (var py = y; py < y1; py++)
            {
                var row = py * Size;
                for (var px = x; px < x1; px++)
                    pixels[row + px] = color;
            }
        }

        static void DrawRectBorder(Color32[] pixels, int x, int y, int w, int h, Color32 color)
        {
            FillRect(pixels, x, y, w, 3, color);
            FillRect(pixels, x, y + h - 3, w, 3, color);
            FillRect(pixels, x, y, 3, h, color);
            FillRect(pixels, x + w - 3, y, 3, h, color);
        }

        static void FillEllipse(Color32[] pixels, int cx, int cy, int rx, int ry, Color32 color)
        {
            var rx2 = rx * rx;
            var ry2 = ry * ry;
            var minX = Mathf.Max(0, cx - rx);
            var maxX = Mathf.Min(Size - 1, cx + rx);
            var minY = Mathf.Max(0, cy - ry);
            var maxY = Mathf.Min(Size - 1, cy + ry);
            for (var py = minY; py <= maxY; py++)
            {
                var dy = py - cy;
                var row = py * Size;
                for (var px = minX; px <= maxX; px++)
                {
                    var dx = px - cx;
                    if (dx * dx * ry2 + dy * dy * rx2 <= rx2 * ry2)
                        pixels[row + px] = color;
                }
            }
        }

        static void FillTriangle(Color32[] pixels, int x1, int y1, int x2, int y2, int x3, int y3, Color32 color)
        {
            var minX = Mathf.Max(0, Mathf.Min(x1, Mathf.Min(x2, x3)));
            var maxX = Mathf.Min(Size - 1, Mathf.Max(x1, Mathf.Max(x2, x3)));
            var minY = Mathf.Max(0, Mathf.Min(y1, Mathf.Min(y2, y3)));
            var maxY = Mathf.Min(Size - 1, Mathf.Max(y1, Mathf.Max(y2, y3)));
            for (var py = minY; py <= maxY; py++)
            {
                var row = py * Size;
                for (var px = minX; px <= maxX; px++)
                {
                    if (PointInTriangle(px, py, x1, y1, x2, y2, x3, y3))
                        pixels[row + px] = color;
                }
            }
        }

        static bool PointInTriangle(int px, int py, int x1, int y1, int x2, int y2, int x3, int y3)
        {
            var d1 = Sign(px, py, x1, y1, x2, y2);
            var d2 = Sign(px, py, x2, y2, x3, y3);
            var d3 = Sign(px, py, x3, y3, x1, y1);
            var hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            var hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);
        }

        static int Sign(int px, int py, int x1, int y1, int x2, int y2)
        {
            return (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
        }
    }
}
