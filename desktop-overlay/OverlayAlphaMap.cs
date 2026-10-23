using System;

namespace FishSocialOverlay
{
    // Exact source-resolution alpha, one byte per pixel. No approximation at holes
    // or thin shoreline edges; hit testing retains the existing nearest-pixel rule.
    internal sealed class OverlayAlphaMap
    {
        readonly byte[] _alpha;
        public int Width { get; }
        public int Height { get; }
        public long Bytes => _alpha.LongLength;
        public OverlayAlphaMap(int width, int height, byte[] alpha)
        {
            if (width < 1 || height < 1 || alpha == null || (long)width * height != alpha.LongLength)
                throw new ArgumentException("Invalid alpha plane");
            Width = width; Height = height; _alpha = alpha;
        }
        public byte Sample(double x, double y, double viewWidth, double viewHeight)
        {
            if (!(viewWidth > 0) || !(viewHeight > 0) || !(x >= 0) || !(y >= 0) ||
                x >= viewWidth || y >= viewHeight) return 0;
            var px = Math.Min(Width - 1, (int)Math.Floor(x / viewWidth * Width));
            var py = Math.Min(Height - 1, (int)Math.Floor(y / viewHeight * Height));
            return _alpha[py * Width + px];
        }
    }
}
