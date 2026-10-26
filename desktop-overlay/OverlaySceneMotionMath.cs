using System;

namespace FishSocialOverlay
{
    // Pure sampling kernel shared by runtime and cross-platform validation.
    internal sealed class OverlayLeafWarp
    {
        readonly byte[] _source, _weight;
        readonly int _width, _height, _left, _top, _cropWidth, _cropHeight;
        readonly double[] _detail, _vertical;
        readonly double _amplitude, _verticalRatio, _primary, _secondary, _verticalPeriod;
        public int Width => _cropWidth;
        public int Height => _cropHeight;
        public int Left => _left;
        public int Top => _top;
        public OverlayLeafWarp(byte[] source, byte[] weight, int width, int height,
            int left, int cropWidth, int cropHeight, double phase, double amplitude,
            double verticalRatio, double primary, double secondary, double verticalPeriod, int top = 0)
        {
            if (source == null || weight == null || width < 1 || height < 1 ||
                source.Length != width * height * 4 || weight.Length != width * height ||
                left < 0 || top < 0 || cropWidth < 1 || left + cropWidth > width || cropHeight < 1 || top + cropHeight > height ||
                primary <= 0 || secondary <= 0 || verticalPeriod <= 0) throw new ArgumentException("Invalid leaf warp data");
            _source = source; _weight = weight; _width = width; _height = height;
            _left = left; _top = top; _cropWidth = cropWidth; _cropHeight = cropHeight;
            _amplitude = amplitude; _verticalRatio = verticalRatio;
            _primary = primary; _secondary = secondary; _verticalPeriod = verticalPeriod;
            _detail = new double[cropWidth]; _vertical = new double[cropWidth];
            for (var x = 0; x < cropWidth; x++)
            { _detail[x] = Math.Cos((left + x + .5) * .025 + phase); _vertical[x] = Math.Sin((left + x + .5) * .018 + phase); }
        }
        public void Render(double seconds, byte[] output)
        {
            if (output == null || output.Length != _cropWidth * _cropHeight * 4) throw new ArgumentException("Wrong leaf buffer");
            var slow = .78 * Math.Sin(seconds * Math.PI * 2 / _primary);
            var fast = .22 * Math.Sin(seconds * Math.PI * 2 / _secondary);
            var vertical = _verticalRatio * Math.Sin(seconds * Math.PI * 2 / _verticalPeriod);
            for (var y = 0; y < _cropHeight; y++) for (var x = 0; x < _cropWidth; x++)
            {
                var gx = x + _left; var gy = y + _top; var index = gy * _width + gx; var dst = (y * _cropWidth + x) * 4;
                var w = _weight[index] / 255.0 * _amplitude;
                var sx = gx - w * (slow + fast * _detail[x]);
                var sy = gy - w * vertical * _vertical[x];
                if (w == 0 || seconds == 0)
                { Buffer.BlockCopy(_source, index * 4, output, dst, 4); continue; }
                if (sx < -.5 || sy < -.5 || sx > _width - .5 || sy > _height - .5)
                { output[dst] = output[dst + 1] = output[dst + 2] = output[dst + 3] = 0; continue; }
                sx = Math.Max(0, Math.Min(_width - 1, sx)); sy = Math.Max(0, Math.Min(_height - 1, sy));
                var x0 = (int)sx; var y0 = (int)sy; var fx = sx - x0; var fy = sy - y0;
                var x1 = Math.Min(x0 + 1, _width - 1); var y1 = Math.Min(y0 + 1, _height - 1);
                var a = (y0 * _width + x0) * 4; var b = (y0 * _width + x1) * 4;
                var c = (y1 * _width + x0) * 4; var d = (y1 * _width + x1) * 4;
                for (var channel = 0; channel < 4; channel++)
                    output[dst + channel] = (byte)Math.Round(
                        (_source[a + channel] * (1 - fx) + _source[b + channel] * fx) * (1 - fy) +
                        (_source[c + channel] * (1 - fx) + _source[d + channel] * fx) * fy);
            }
        }
    }

    internal static class OverlaySceneMotionMath
    {
        public static double CloudOffset(double seconds, double amplitude, double period, double scale)
            => amplitude * Math.Sin(seconds * Math.PI * 2 / period) * scale;
        public static double CloudTravel(double seconds, double speed, double width, double phase = 0)
        {
            if (width <= 0 || speed <= 0) throw new ArgumentException("Invalid cloud cycle");
            var distance = seconds * speed + phase;
            return distance - Math.Floor(distance / width) * width;
        }
    }
}
