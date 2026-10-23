using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FishSocialOverlay
{
    internal sealed class OverlayStaticImage
    {
        public BitmapSource Bitmap;
        public OverlayAlphaMap Alpha;
        public long Bytes => Bitmap == null ? 256 :
            ((long)Bitmap.PixelWidth * Bitmap.Format.BitsPerPixel + 7) / 8 * Bitmap.PixelHeight + (Alpha?.Bytes ?? 0) + 256;
    }

    internal static class OverlayStaticArt
    {
        // Separate static-scene budget; active Image.Source references remain valid
        // after eviction. Budget describes cache-owned payload, not process memory.
        static readonly OverlayResourceCache<OverlayStaticImage> Cache = new OverlayResourceCache<OverlayStaticImage>(48L * 1024 * 1024);
        static readonly OverlayDecodeQueue Queue = new OverlayDecodeQueue(2, 64);
        static readonly ConditionalWeakTable<BitmapSource, OverlayAlphaMap> AlphaMaps = new ConditionalWeakTable<BitmapSource, OverlayAlphaMap>();
        sealed class Request
        {
            public Task<OverlayStaticImage> Task;
            public readonly CancellationTokenSource Cancel = new CancellationTokenSource();
            public int Users;
        }
        static readonly Dictionary<string, Request> Pending = new Dictionary<string, Request>(StringComparer.OrdinalIgnoreCase);
        public static long CachedBytes => Cache.ResidentBytes;
        public static int CachedCount => Cache.Count;
        public static int PendingCount => Queue.Pending;
        public static long DecodeCount { get; private set; }

        public static bool TryGetAlpha(BitmapSource source, out OverlayAlphaMap alpha) => AlphaMaps.TryGetValue(source, out alpha);

        public static async Task<string> ResolveAsync(Func<string> resolve, CancellationToken token)
        {
            Task<string> job;
            while ((job = Queue.TryRun(resolve, token)) == null)
                await Task.Delay(16, token);
            return await job;
        }

        public static async Task<OverlayStaticImage> LoadAsync(string path, bool prepareAlpha, CancellationToken token)
        {
            Application.Current?.Dispatcher.VerifyAccess();
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(path)) return null;
            var key = (prepareAlpha ? "alpha|" : "image|") + Path.GetFullPath(path);
            Request request;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (Cache.TryGet(key, out var cached)) return cached;
                if (Pending.TryGetValue(key, out request)) break;
                request = new Request();
                request.Task = Queue.TryRun(() => Decode(path, prepareAlpha), request.Cancel.Token);
                if (request.Task != null) { Pending[key] = request; break; }
                request.Cancel.Dispose();
                // Retry admission without growing an async recursion chain.
                await Task.Delay(16, token);
            }
            request.Users++;
            try
            {
                var result = await OverlayTaskWait.WithCancellation(request.Task, token);
                Application.Current?.Dispatcher.VerifyAccess();
                if (Pending.TryGetValue(key, out var current) && ReferenceEquals(current, request))
                {
                    Pending.Remove(key);
                    if (result.Bitmap != null)
                    {
                        DecodeCount++;
                        if (result.Alpha != null) AlphaMaps.Add(result.Bitmap, result.Alpha);
                    }
                    Cache.Add(key, key, result, result.Bytes);
                }
                return result;
            }
            finally
            {
                if (--request.Users == 0)
                {
                    if (!request.Task.IsCompleted) request.Cancel.Cancel();
                    if (Pending.TryGetValue(key, out var current) && ReferenceEquals(current, request)) Pending.Remove(key);
                    request.Cancel.Dispose();
                }
            }
        }

        static OverlayStaticImage Decode(string path, bool prepareAlpha)
        {
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                    return new OverlayStaticImage { Bitmap = bitmap, Alpha = prepareAlpha ? BuildAlpha(bitmap) : null };
                }
            }
            catch (IOException) { return new OverlayStaticImage(); }
            catch (UnauthorizedAccessException) { return new OverlayStaticImage(); }
            catch (NotSupportedException) { return new OverlayStaticImage(); }
            catch (ArgumentException) { return new OverlayStaticImage(); }
            catch (FormatException) { return new OverlayStaticImage(); }
            catch (InvalidOperationException) { return new OverlayStaticImage(); }
            catch (System.Runtime.InteropServices.COMException) { return new OverlayStaticImage(); }
        }

        internal static OverlayAlphaMap BuildAlpha(BitmapSource source)
        {
            BitmapSource bgra = source;
            if (source.Format != PixelFormats.Bgra32 && source.Format != PixelFormats.Pbgra32)
            {
                bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
                bgra.Freeze();
            }
            var width = bgra.PixelWidth; var height = bgra.PixelHeight;
            var stride = checked(width * 4);
            var plane = new byte[checked(width * height)];
            // A 32-row scratch buffer replaces a second full-sized BGRA copy.
            var scratch = new byte[checked(stride * Math.Min(32, height))];
            for (var y = 0; y < height; y += 32)
            {
                var rows = Math.Min(32, height - y);
                bgra.CopyPixels(new Int32Rect(0, y, width, rows), scratch, stride, 0);
                for (var i = 0; i < width * rows; i++) plane[y * width + i] = scratch[i * 4 + 3];
            }
            return new OverlayAlphaMap(width, height, plane);
        }
    }
}
