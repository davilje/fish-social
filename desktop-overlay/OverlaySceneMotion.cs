using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FishSocialOverlay
{
    // Decoration-only motion: cannot change actor ordering or gameplay coordinates.
    internal sealed class OverlaySceneMotion : IDisposable
    {
        [DataContract] internal sealed class Settings
        {
            [DataMember] public int version, width, height;
            [DataMember] public string weightTexture, cloudId;
            [DataMember] public double cloudAmplitude, cloudPeriod, leafAmplitude, verticalRatio, primaryPeriod, secondaryPeriod, verticalPeriod;
            [DataMember] public LeafSettings[] leaves;
        }
        [DataContract] internal sealed class LeafSettings
        { [DataMember] public string id; [DataMember] public int left, width, height; [DataMember] public double phase; }
        sealed class Leaf
        {
            public OverlayLeafWarp Warp;
            public byte[] Output;
            public WriteableBitmap Bitmap;
        }
        readonly Settings _settings;
        readonly List<Leaf> _leaves = new List<Leaf>();
        readonly OverlayAnimationSchedule.Subscription _clock;
        readonly TranslateTransform _cloudTransform = new TranslateTransform();
        readonly double _cloudScale;
        readonly Dictionary<string, FrameworkElement> _elements = new Dictionary<string, FrameworkElement>();
        bool _disposed, _visible, _rendering;
        double _elapsed, _resumedAt;
        internal bool IsRunning => _clock.IsEnabled;
        internal bool IsDisposed => _disposed;
        public FrameworkElement Element(string id, Image fallback) => _elements.TryGetValue(id, out var value) ? value : fallback;

        static string Inside(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative)) throw new InvalidDataException("Missing scene motion resource");
            var prefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
            var result = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Scene motion resource outside root");
            return result;
        }
        public static async Task<OverlaySceneMotion> TryCreateAsync(string resources, string background,
            Dictionary<string, Image> images, CancellationToken token)
        {
            if (string.IsNullOrEmpty(background)) return null;
            var directory = Path.GetDirectoryName(Inside(resources, background));
            var manifest = Path.Combine(directory, "motion.json");
            var settings = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                if (!File.Exists(manifest)) return null;
                using (var stream = File.OpenRead(manifest))
                    return (Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(stream);
            }, token);
            if (settings == null) return null;
            if (settings.version != 1 || settings.width < 1 || settings.height < 1 || settings.cloudPeriod <= 0 ||
                settings.cloudAmplitude < 0 || settings.cloudAmplitude > 64 || settings.leafAmplitude < 0 || settings.leafAmplitude > 8 ||
                settings.leaves == null || settings.leaves.Length > 4 || !images.ContainsKey(settings.cloudId))
                throw new InvalidDataException("Invalid scene motion settings");
            var art = await OverlayStaticArt.LoadAsync(Inside(directory, settings.weightTexture), false, token);
            token.ThrowIfCancellationRequested();
            if (art?.Bitmap == null || art.Bitmap.PixelWidth != settings.width || art.Bitmap.PixelHeight != settings.height)
                throw new InvalidDataException("Scene wind weight size mismatch");
            var gray = new FormatConvertedBitmap(art.Bitmap, PixelFormats.Gray8, null, 0); gray.Freeze();
            var weight = new byte[settings.width * settings.height]; gray.CopyPixels(weight, settings.width, 0);
            var prepared = new List<Tuple<LeafSettings, OverlayLeafWarp>>();
            foreach (var spec in settings.leaves)
            {
                if (!images.TryGetValue(spec.id, out var image) || !(image.Source is BitmapSource bitmap) ||
                    bitmap.PixelWidth != settings.width || bitmap.PixelHeight != settings.height)
                    throw new InvalidDataException("Missing/mismatched moving tree " + spec.id);
                var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Pbgra32, null, 0); converted.Freeze();
                var pixels = new byte[settings.width * settings.height * 4]; converted.CopyPixels(pixels, settings.width * 4, 0);
                var warp = new OverlayLeafWarp(pixels, weight, settings.width, settings.height, spec.left, spec.width, spec.height,
                    spec.phase, settings.leafAmplitude, settings.verticalRatio, settings.primaryPeriod, settings.secondaryPeriod, settings.verticalPeriod);
                prepared.Add(Tuple.Create(spec, warp));
            }
            token.ThrowIfCancellationRequested();
            return new OverlaySceneMotion(settings, images, prepared);
        }
        OverlaySceneMotion(Settings settings, Dictionary<string, Image> images, List<Tuple<LeafSettings, OverlayLeafWarp>> prepared)
        {
            _settings = settings;
            var cloud = images[settings.cloudId]; _cloudScale = cloud.Width / settings.width;
            cloud.RenderTransform = _cloudTransform;
            foreach (var pair in prepared)
            {
                var spec = pair.Item1; var warp = pair.Item2; var source = images[spec.id];
                var sx = source.Width / settings.width; var sy = source.Height / settings.height;
                var group = new Canvas { Width = source.Width, Height = source.Height, IsHitTestVisible = false, ClipToBounds = true };
                Canvas.SetLeft(group, Canvas.GetLeft(source)); Canvas.SetTop(group, Canvas.GetTop(source)); Panel.SetZIndex(group, Panel.GetZIndex(source));
                // Static trunk/root below the moving patch. Identical scale on both pieces.
                var root = new Image { Width = source.Width, Height = source.Height, Source = source.Source, Stretch = Stretch.Fill, IsHitTestVisible = false,
                    Clip = new RectangleGeometry(new Rect(0, spec.height * sy, source.Width, source.Height - spec.height * sy)) };
                group.Children.Add(root);
                var leaf = new Leaf { Warp = warp, Output = new byte[warp.Width * warp.Height * 4],
                    Bitmap = new WriteableBitmap(warp.Width, warp.Height, 96, 96, PixelFormats.Pbgra32, null) };
                warp.Render(0, leaf.Output); Publish(leaf);
                var crown = new Image { Width = warp.Width * sx, Height = warp.Height * sy, Source = leaf.Bitmap, Stretch = Stretch.Fill, IsHitTestVisible = false };
                Canvas.SetLeft(crown, spec.left * sx); Canvas.SetTop(crown, 0); group.Children.Add(crown);
                _leaves.Add(leaf); _elements[spec.id] = group;
            }
            _clock = OverlayAnimationClock.Create(1000.0 / 24); _clock.Tick += Tick;
        }
        static void Publish(Leaf leaf) => leaf.Bitmap.WritePixels(new Int32Rect(0, 0, leaf.Warp.Width, leaf.Warp.Height), leaf.Output, leaf.Warp.Width * 4, 0);
        public void SetVisible(bool visible)
        {
            if (_disposed || visible == _visible) return;
            _visible = visible;
            if (visible) { _resumedAt = OverlayAnimationClock.Now; _clock.Start(); OverlayAnimationClock.Wake(); }
            else { _elapsed += Math.Max(0, OverlayAnimationClock.Now - _resumedAt); _clock.Stop(); }
        }
        async void Tick(object sender, EventArgs args)
        {
            if (_disposed || !_visible) return;
            _clock.SkipPeriods(OverlayAnimationClock.Now, _clock.Interval.TotalMilliseconds);
            var seconds = (_elapsed + OverlayAnimationClock.Now - _resumedAt) / 1000;
            _cloudTransform.X = OverlaySceneMotionMath.CloudOffset(seconds, _settings.cloudAmplitude, _settings.cloudPeriod, _cloudScale);
            if (_rendering) return; // At most one worker, no frame queue and no per-frame pixel allocations.
            _rendering = true;
            try
            {
                await Task.Run(() => { foreach (var leaf in _leaves) leaf.Warp.Render(seconds, leaf.Output); });
                if (_disposed || !_visible) return;
                foreach (var leaf in _leaves) Publish(leaf);
            }
            catch (Exception ex)
            { System.Diagnostics.Debug.WriteLine("[SceneMotion] " + ex.Message); SetVisible(false); }
            finally { _rendering = false; }
        }
        public void Dispose()
        { if (_disposed) return; _disposed = true; _clock.Stop(); _clock.Tick -= Tick; }
    }
}
