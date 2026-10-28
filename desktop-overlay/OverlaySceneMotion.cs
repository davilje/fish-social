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
            [DataMember] public CloudSettings[] clouds;
        }
        [DataContract] internal sealed class CloudSettings
        { [DataMember] public string id; [DataMember] public double speed, phase, verticalSlope; [DataMember] public CloudSpriteSettings[] sprites; }
        [DataContract] internal sealed class CloudSpriteSettings
        {
            [DataMember] public string image;
            [DataMember] public int width, height;
            [DataMember] public double left, top, velocityX, velocityY;
        }
        sealed class CloudSprite
        {
            public CloudSpriteSettings Settings;
            public double Scale, VerticalScale;
            public TranslateTransform Transform = new TranslateTransform();
        }
        [DataContract] internal sealed class LeafSettings
        { [DataMember] public string id, weightTexture; [DataMember] public int left, top, width, height; [DataMember] public double phase, primaryPeriod; [DataMember] public double? amplitude; }
        sealed class Cloud
        {
            public CloudSettings Settings;
            public double Scale, VerticalScale, Width;
            public TranslateTransform First = new TranslateTransform(), Second = new TranslateTransform();
        }
        sealed class Leaf
        {
            public OverlayLeafWarp Warp;
            public byte[] Output;
            public WriteableBitmap Bitmap;
        }
        readonly Settings _settings;
        readonly List<Leaf> _leaves = new List<Leaf>();
        readonly List<Cloud> _clouds = new List<Cloud>();
        readonly List<CloudSprite> _cloudSprites = new List<CloudSprite>();
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
            if ((settings.version != 1 && settings.version != 2 && settings.version != 3) || settings.width < 1 || settings.height < 1 ||
                settings.leafAmplitude < 0 || settings.leafAmplitude > 24 ||
                settings.leaves == null || settings.leaves.Length > 16)
                throw new InvalidDataException("Invalid scene motion settings");
            if (settings.version == 1 && (settings.cloudPeriod <= 0 || settings.cloudAmplitude < 0 ||
                settings.cloudAmplitude > 64 || string.IsNullOrEmpty(settings.cloudId) || !images.ContainsKey(settings.cloudId)))
                throw new InvalidDataException("Invalid legacy cloud settings");
            var cloudArt = new Dictionary<string, BitmapSource>();
            if (settings.version >= 2)
            {
                if (settings.clouds == null || (settings.clouds.Length != 0 && settings.clouds.Length != 3))
                    throw new InvalidDataException("Expected no cloud layers or three cloud layers");
                var cloudIds = new HashSet<string>();
                foreach (var cloud in settings.clouds)
                {
                    if (cloud == null || string.IsNullOrEmpty(cloud.id) || !cloudIds.Add(cloud.id) || !images.ContainsKey(cloud.id) ||
                        (settings.version == 2 && (double.IsNaN(cloud.speed) || cloud.speed <= 0 || cloud.speed > 100)) || double.IsNaN(cloud.phase) || double.IsInfinity(cloud.phase)
                        || double.IsNaN(cloud.verticalSlope) || double.IsInfinity(cloud.verticalSlope) || Math.Abs(cloud.verticalSlope) > .15)
                        throw new InvalidDataException("Invalid repeating cloud layer");
                    if (settings.version != 3) continue;
                    if (cloud.sprites == null || cloud.sprites.Length < 1 || cloud.sprites.Length > 256)
                        throw new InvalidDataException("Invalid independent cloud count");
                    foreach (var sprite in cloud.sprites)
                    {
                        if (sprite == null || string.IsNullOrWhiteSpace(sprite.image) || cloudArt.ContainsKey(sprite.image)
                            || sprite.width < 1 || sprite.height < 1 || sprite.width > settings.width || sprite.height > settings.height
                            || !Finite(sprite.left) || !Finite(sprite.top) || !Finite(sprite.velocityX) || !Finite(sprite.velocityY)
                            || Math.Abs(sprite.velocityX) > 5 || Math.Abs(sprite.velocityY) > 5
                            || sprite.left < -sprite.width || sprite.left > settings.width || sprite.top < 0 || sprite.top + sprite.height > settings.height)
                            throw new InvalidDataException("Invalid independent cloud sprite");
                        var art = await OverlayStaticArt.LoadAsync(Inside(directory, sprite.image), false, token);
                        token.ThrowIfCancellationRequested();
                        if (art?.Bitmap == null || art.Bitmap.PixelWidth != sprite.width || art.Bitmap.PixelHeight != sprite.height)
                            throw new InvalidDataException("Cloud sprite size mismatch");
                        cloudArt.Add(sprite.image, art.Bitmap);
                    }
                }
            }
            var weights = new Dictionary<string, byte[]>();
            var prepared = new List<Tuple<LeafSettings, OverlayLeafWarp>>();
            foreach (var spec in settings.leaves)
            {
                if (spec == null || string.IsNullOrWhiteSpace(spec.id)) throw new InvalidDataException("Invalid foliage layer");
                if (spec.amplitude.HasValue && (!Finite(spec.amplitude.Value) || spec.amplitude.Value < 0 || spec.amplitude.Value > 24))
                    throw new InvalidDataException("Invalid foliage amplitude");
                var weightFile = string.IsNullOrEmpty(spec.weightTexture) ? settings.weightTexture : spec.weightTexture;
                if (string.IsNullOrWhiteSpace(weightFile)) throw new InvalidDataException("Missing foliage weight");
                if (!weights.TryGetValue(weightFile, out var weight))
                {
                    var art = await OverlayStaticArt.LoadAsync(Inside(directory, weightFile), false, token);
                    token.ThrowIfCancellationRequested();
                    if (art?.Bitmap == null || art.Bitmap.PixelWidth != settings.width || art.Bitmap.PixelHeight != settings.height)
                        throw new InvalidDataException("Scene wind weight size mismatch");
                    var gray = new FormatConvertedBitmap(art.Bitmap, PixelFormats.Gray8, null, 0); gray.Freeze();
                    weight = new byte[settings.width * settings.height]; gray.CopyPixels(weight, settings.width, 0);
                    weights[weightFile] = weight;
                }
                if (!images.TryGetValue(spec.id, out var image) || !(image.Source is BitmapSource bitmap) ||
                    bitmap.PixelWidth != settings.width || bitmap.PixelHeight != settings.height)
                    throw new InvalidDataException("Missing/mismatched moving tree " + spec.id);
                var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Pbgra32, null, 0); converted.Freeze();
                var pixels = new byte[settings.width * settings.height * 4]; converted.CopyPixels(pixels, settings.width * 4, 0);
                var warp = new OverlayLeafWarp(pixels, weight, settings.width, settings.height, spec.left, spec.width, spec.height,
                    spec.phase, spec.amplitude ?? settings.leafAmplitude, settings.verticalRatio, spec.primaryPeriod > 0 ? spec.primaryPeriod : settings.primaryPeriod,
                    settings.secondaryPeriod, settings.verticalPeriod, spec.top);
                prepared.Add(Tuple.Create(spec, warp));
            }
            token.ThrowIfCancellationRequested();
            return new OverlaySceneMotion(settings, images, prepared, cloudArt);
        }
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        OverlaySceneMotion(Settings settings, Dictionary<string, Image> images, List<Tuple<LeafSettings, OverlayLeafWarp>> prepared,
            Dictionary<string, BitmapSource> cloudArt)
        {
            _settings = settings;
            if (settings.version == 1)
            {
                var cloud = images[settings.cloudId]; _cloudScale = cloud.Width / settings.width;
                cloud.RenderTransform = _cloudTransform;
            }
            else foreach (var spec in settings.clouds)
            {
                var source = images[spec.id];
                var group = new Canvas { Width = source.Width, Height = source.Height, ClipToBounds = true, IsHitTestVisible = false };
                Canvas.SetLeft(group, Canvas.GetLeft(source)); Canvas.SetTop(group, Canvas.GetTop(source)); Panel.SetZIndex(group, Panel.GetZIndex(source));
                if (settings.version == 3)
                {
                    foreach (var sprite in spec.sprites)
                    {
                        var item = new CloudSprite { Settings = sprite, Scale = source.Width / settings.width, VerticalScale = source.Height / settings.height };
                        group.Children.Add(new Image { Source = cloudArt[sprite.image], Width = sprite.width * item.Scale,
                            Height = sprite.height * item.VerticalScale, Stretch = Stretch.Fill, IsHitTestVisible = false, RenderTransform = item.Transform });
                        if (sprite.velocityX != 0 || sprite.velocityY != 0) _cloudSprites.Add(item);
                        PlaceCloudSprite(item, 0);
                    }
                    _elements[spec.id] = group; continue;
                }
                var cloud = new Cloud { Settings = spec, Width = source.Width, Scale = source.Width / settings.width,
                    VerticalScale = source.Height / settings.height };
                foreach (var transform in new[] { cloud.First, cloud.Second })
                    group.Children.Add(new Image { Source = source.Source, Width = source.Width, Height = source.Height,
                        Stretch = Stretch.Fill, IsHitTestVisible = false, RenderTransform = transform });
                _clouds.Add(cloud); _elements[spec.id] = group;
                PlaceCloud(cloud, 0);
            }
            foreach (var pair in prepared)
            {
                var spec = pair.Item1; var warp = pair.Item2; var source = images[spec.id];
                var sx = source.Width / settings.width; var sy = source.Height / settings.height;
                var group = new Canvas { Width = source.Width, Height = source.Height, IsHitTestVisible = false, ClipToBounds = true };
                Canvas.SetLeft(group, Canvas.GetLeft(source)); Canvas.SetTop(group, Canvas.GetTop(source)); Panel.SetZIndex(group, Panel.GetZIndex(source));
                // Exclude exactly the moving rectangle; all surrounding roots remain fixed.
                var root = new Image { Width = source.Width, Height = source.Height, Source = source.Source, Stretch = Stretch.Fill, IsHitTestVisible = false,
                    Clip = new CombinedGeometry(GeometryCombineMode.Exclude,
                        new RectangleGeometry(new Rect(0, 0, source.Width, source.Height)),
                        new RectangleGeometry(new Rect(spec.left * sx, spec.top * sy, spec.width * sx, spec.height * sy))) };
                group.Children.Add(root);
                var leaf = new Leaf { Warp = warp, Output = new byte[warp.Width * warp.Height * 4],
                    Bitmap = new WriteableBitmap(warp.Width, warp.Height, 96, 96, PixelFormats.Pbgra32, null) };
                warp.Render(0, leaf.Output); Publish(leaf);
                var crown = new Image { Width = warp.Width * sx, Height = warp.Height * sy, Source = leaf.Bitmap, Stretch = Stretch.Fill, IsHitTestVisible = false };
                Canvas.SetLeft(crown, spec.left * sx); Canvas.SetTop(crown, spec.top * sy); group.Children.Add(crown);
                _leaves.Add(leaf); _elements[spec.id] = group;
            }
            _clock = OverlayAnimationClock.Create(1000.0 / 24); _clock.Tick += Tick;
        }
        static void Publish(Leaf leaf) => leaf.Bitmap.WritePixels(new Int32Rect(0, 0, leaf.Warp.Width, leaf.Warp.Height), leaf.Output, leaf.Warp.Width * 4, 0);
        void PlaceCloudSprite(CloudSprite sprite, double seconds)
        {
            var spec = sprite.Settings;
            OverlaySceneMotionMath.CloudSpritePlacement(seconds, spec.velocityX, spec.velocityY, _settings.width, _settings.height,
                spec.width, spec.height, spec.left, spec.top, out var x, out var y);
            sprite.Transform.X = x * sprite.Scale; sprite.Transform.Y = y * sprite.VerticalScale;
        }
        void PlaceCloud(Cloud cloud, double seconds)
        {
            OverlaySceneMotionMath.CloudPlacement(seconds, cloud.Settings.speed, _settings.width,
                cloud.Settings.phase, cloud.Settings.verticalSlope, 0, out var x, out var y);
            cloud.First.X = x * cloud.Scale; cloud.First.Y = y * cloud.VerticalScale;
            OverlaySceneMotionMath.CloudPlacement(seconds, cloud.Settings.speed, _settings.width,
                cloud.Settings.phase, cloud.Settings.verticalSlope, -1, out x, out y);
            cloud.Second.X = x * cloud.Scale; cloud.Second.Y = y * cloud.VerticalScale;
        }
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
            if (_settings.version == 1) _cloudTransform.X = OverlaySceneMotionMath.CloudOffset(seconds, _settings.cloudAmplitude, _settings.cloudPeriod, _cloudScale);
            else foreach (var cloud in _clouds) PlaceCloud(cloud, seconds);
            foreach (var sprite in _cloudSprites) PlaceCloudSprite(sprite, seconds);
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
