using System;
using System.Threading;

namespace FishSocialOverlay
{
    internal static class OverlayMaskRaster
    {
        public static byte[] Build(int width, int height, double scaleX, double scaleY,
            Func<double, double, double> sample, CancellationToken token = default)
        {
            if (width < 1 || height < 1 || !(scaleX > 0) || !(scaleY > 0)) throw new ArgumentException("Invalid mask size");
            var pixels = new byte[checked(width * height * 4)];
            for (var y = 0; y < height; y++)
            {
                token.ThrowIfCancellationRequested();
                for (var x = 0; x < width; x++)
                {
                    var alpha = (byte)Math.Round(Math.Max(0, Math.Min(1, sample((x + .5) / scaleX, (y + .5) / scaleY))) * 255);
                    var i = (y * width + x) * 4;
                    pixels[i] = pixels[i + 1] = pixels[i + 2] = pixels[i + 3] = alpha;
                }
            }
            return pixels;
        }
    }
}
