using System;
using System.Threading;
using FishSocialOverlay;

public static class OverlayStaticArtTests
{
    static int passed;
    public static void Main()
    {
        Run("alpha keeps every 8-bit threshold value", AlphaValues);
        Run("fill mapping survives stretching and fractional coordinates", Mapping);
        Run("outside and invalid coordinates never capture clicks", Bounds);
        Run("alpha payload is one byte per original pixel", Size);
        Run("DPI masks match product fades within half an alpha byte", Masks);
        Run("obsolete mask builds honor cancellation", Cancellation);
        Console.WriteLine("PASS: " + passed + "/" + passed + " static-art math tests");
    }
    static void Run(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void AlphaValues()
    {
        var data = new byte[256]; for (var i = 0; i < 256; i++) data[i] = (byte)i;
        var map = new OverlayAlphaMap(256, 1, data);
        for (var i = 0; i < 256; i++) Check(map.Sample(i + .5, .5, 256, 1) == i, "alpha quantized");
        Check(map.Sample(7.9, .5, 256, 1) < 8 && map.Sample(8, .5, 256, 1) >= 8, "hit threshold shifted");
    }
    static void Mapping()
    {
        var data = new byte[17 * 13]; for (var i = 0; i < data.Length; i++) data[i] = (byte)(i % 256);
        var map = new OverlayAlphaMap(17, 13, data);
        for (var i = 0; i < 1000; i++)
        {
            var x = (i * 13.17) % 600; var y = (i * 7.23) % 300;
            var expected = data[(int)Math.Floor(y / 300 * 13) * 17 + (int)Math.Floor(x / 600 * 17)];
            Check(expected == map.Sample(x, y, 600, 300), "fill/viewport mapping changed");
        }
    }
    static void Bounds()
    {
        var map = new OverlayAlphaMap(1, 1, new byte[] { 255 });
        foreach (var p in new[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity, -1, 10 })
            Check(map.Sample(p, 1, 10, 10) == 0 && map.Sample(1, p, 10, 10) == 0, "out-of-image hit");
        Check(map.Sample(0, 0, 0, 10) == 0 && map.Sample(0, 0, double.NaN, 10) == 0, "invalid viewport hit");
    }
    static void Size()
    {
        var map = new OverlayAlphaMap(2170, 725, new byte[2170 * 725]);
        Check(map.Bytes == 1573250, "alpha stored as full BGRA");
    }
    static double Edge(double coordinate, double size)
    {
        var fade = Math.Min(40, size * .45);
        return Math.Max(0, Math.Min(1, Math.Min(coordinate / fade, (size - coordinate) / fade)));
    }
    static void Masks()
    {
        foreach (var size in new[] { (960,560), (800,400), (600,300), (32,24) })
        foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
        {
            var w = (int)Math.Ceiling(size.Item1 * scale); var h = (int)Math.Ceiling(size.Item2 * scale);
            var sx = w / (double)size.Item1; var sy = h / (double)size.Item2;
            var data = OverlayMaskRaster.Build(w,h,sx,sy,(x,y)=>Edge(x,size.Item1)*Edge(y,size.Item2));
            for (var y = 0; y < h; y += 7)
            for (var x = 0; x < w; x += 7)
            {
                var index = (y * w + x) * 4;
                var expected = Edge((x+.5)/sx,size.Item1) * Edge((y+.5)/sy,size.Item2) * 255;
                Check(Math.Abs(data[index+3]-expected) <= .50001, "fade changed at DPI");
                Check(data[index] == data[index+3] && data[index+1] == data[index+3], "mask not premultiplied");
            }
        }
    }
    static void Cancellation()
    {
        using (var cancel = new CancellationTokenSource())
        {
            cancel.Cancel();
            try { OverlayMaskRaster.Build(10,10,1,1,(x,y)=>1,cancel.Token); }
            catch (OperationCanceledException) { return; }
            throw new Exception("obsolete mask was still built");
        }
    }
}
