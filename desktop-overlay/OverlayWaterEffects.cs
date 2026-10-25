using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FishSocialOverlay
{
    internal sealed class WaterEffectTier
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int DurationMs { get; set; }
        public int DropCount { get; set; }
        public int ArcCount { get; set; }
        public int CrownCount { get; set; }
        public int SpawnCount { get; set; }
    }
    internal sealed class WaterEffectCue
    {
        public string Id { get; set; }
        public string Cue { get; set; }
        public string[] Markers { get; set; }
        public string SizeTier { get; set; }
        public int DurationMs { get; set; }
        public int SpawnCount { get; set; }
        public int Priority { get; set; }
        public string ReducedVariant { get; set; }
        public string[] AllowedPhases { get; set; }
    }
    internal sealed class WaterEffectManifest
    {
        public int SchemaVersion { get; set; }
        public Dictionary<string, WaterEffectTier> Tiers { get; set; }
        public WaterEffectCue[] Cues { get; set; }
        public WaterEffectAsset[] Assets { get; set; }
    }
    internal sealed class WaterEffectAsset
    {
        public string Id { get; set; }
        public string File { get; set; }
    }
    internal sealed class SourceWaterLayer
    {
        public double[] Bounds { get; set; }
        public double[][] RowCenters { get; set; }
    }
    internal sealed class SourceWaterMarker
    {
        public string Clip { get; set; }
        public int Source { get; set; }
        public string Cue { get; set; }
        public string Layer { get; set; }
    }
    internal sealed class SourceWaterManifest
    {
        public Dictionary<string, Dictionary<string, SourceWaterLayer>> Frames { get; set; }
        public SourceWaterMarker[] Markers { get; set; }
    }
    internal static class WaterEffectResources
    {
        internal sealed class Pack
        {
            public WaterEffectManifest Manifest;
            public SourceWaterManifest Source;
            public readonly Dictionary<string, BitmapSource> Textures = new Dictionary<string, BitmapSource>();
            public long DecodedBytes;
            public readonly Dictionary<string, int> OpaqueTop = new Dictionary<string, int>();
        }
        static Task<Pack> _loading;
        // Contact geometry also serves the real bobber, independently of effect quality
        // or a missing effect texture. Decode once off the dispatcher.
        static readonly Lazy<Task<SourceWaterManifest>> SourceGeometry = new Lazy<Task<SourceWaterManifest>>(
            () => Task.Run(() =>
            {
                try
                {
                    var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OverlayResources", "fishing", "source-water-markers.json");
                    return JsonSerializer.Deserialize<SourceWaterManifest>(File.ReadAllText(path),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
                { return null; }
            }));
        internal static Task<SourceWaterManifest> SourceGeometryAsync() => SourceGeometry.Value;
        public static Pack Current { get; private set; }
        public static async void Warmup()
        {
            if (Current != null) return;
            if (_loading == null) _loading = Task.Run(Load);
            Current = await _loading;
        }
        static Pack Load()
        {
            try
            {
                var root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OverlayResources");
                var directory = Path.Combine(root, "effects", "fishing", "v4");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var pack = new Pack
                {
                    Manifest = JsonSerializer.Deserialize<WaterEffectManifest>(File.ReadAllText(Path.Combine(directory, "effects.json")), options),
                    Source = SourceGeometryAsync().GetAwaiter().GetResult()
                };
                if (pack.Manifest?.SchemaVersion != 2 || pack.Manifest.Tiers == null || pack.Manifest.Assets == null || pack.Source == null) return null;
                // H and I read one canonical one-shot source definition. This
                // bounds export only adds low-frequency continuous visual cues.
                var shared = JsonSerializer.Deserialize<AudioSourceMarkerDocument>(
                    File.ReadAllText(Path.Combine(root, "fishing", "presentation-markers.json")), options);
                if (shared?.Version != 2 || shared.Markers == null) return null;
                var markers = new List<SourceWaterMarker>();
                foreach (var marker in pack.Source.Markers ?? Array.Empty<SourceWaterMarker>())
                    if (marker.Cue == "waiting.ripple" || marker.Cue == "fish.struggle" || marker.Cue == "retrieve.contact" || marker.Cue == "fish.surface") markers.Add(marker);
                foreach (var marker in shared.Markers)
                    if (marker.Marker == "float.contact" || marker.Marker == "groundbait.contact" || marker.Marker == "fish.surface" || marker.Marker == "fish.slap")
                        markers.Add(new SourceWaterMarker { Clip = marker.Clip, Source = marker.SourceIndex, Cue = marker.Marker, Layer = marker.ContactLayer });
                pack.Source.Markers = markers.ToArray();
                foreach (var asset in pack.Manifest.Assets)
                {
                    if (string.IsNullOrEmpty(asset.Id) || string.IsNullOrEmpty(asset.File) || Path.GetFileName(asset.File) != asset.File) return null;
                    using (var stream = File.OpenRead(Path.Combine(directory, asset.File)))
                    {
                        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream; image.EndInit(); image.Freeze();
                        pack.DecodedBytes += (long)image.PixelWidth * image.PixelHeight * 4;
                        if (pack.DecodedBytes > 4 * 1024 * 1024) return null;
                        pack.Textures[asset.Id] = image;
                        // Remap the painted shading once at prewarm: deep blue edge,
                        // pale cyan body, cream foam against the saturated blue pond.
                        BitmapSource rgba = image;
                        if (image.Format != PixelFormats.Bgra32)
                        { rgba = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0); rgba.Freeze(); }
                        var pixels = new byte[rgba.PixelWidth * rgba.PixelHeight * 4];
                        rgba.CopyPixels(pixels, rgba.PixelWidth * 4, 0);
                        if (asset.Id.StartsWith("ripple-", StringComparison.Ordinal))
                        {
                            var top = rgba.PixelHeight;
                            for (var y = 0; y < rgba.PixelHeight && top == rgba.PixelHeight; y++)
                                for (var x = 0; x < rgba.PixelWidth; x++)
                                    if (pixels[(y * rgba.PixelWidth + x) * 4 + 3] > 8) { top = y; break; }
                            pack.OpaqueTop[asset.Id] = top;
                        }
                        for (var pixel = 0; pixel < pixels.Length; pixel += 4)
                        {
                            var tone = Math.Max(0, Math.Min(1, (pixels[pixel + 1] - 125) / 85.0));
                            var upper = tone >= .32;
                            var blend = upper ? (tone - .32) / .68 : tone / .32;
                            pixels[pixel + 2] = (byte)Math.Round(upper ? 181 + 74 * blend : 27 + 154 * blend);
                            pixels[pixel + 1] = (byte)Math.Round(upper ? 240 + 7 * blend : 82 + 158 * blend);
                            pixels[pixel] = (byte)Math.Round(upper ? 245 - 21 * blend : 114 + 131 * blend);
                        }
                        var tinted = BitmapSource.Create(rgba.PixelWidth, rgba.PixelHeight, 96, 96,
                            PixelFormats.Bgra32, null, pixels, rgba.PixelWidth * 4);
                        tinted.Freeze(); pack.Textures[asset.Id] = tinted;
                    }
                }
                return pack;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is FormatException || e is System.Runtime.InteropServices.COMException || e is JsonException || e is ArgumentException || e is InvalidOperationException || e is NotSupportedException)
            { LatencyTrace.Write("waterFx resource-unavailable=" + e.GetType().Name); return null; }
        }
    }

    // One global pool of 24 Image draw instances, shared by every actor. Only live
    // groups subscribe to the existing clock; ticks mutate transforms/opacity only.
    internal sealed class OverlayWaterEffects : IDisposable
    {
        internal sealed class Particle
        {
            public readonly Image Image = new Image { IsHitTestVisible = false, Stretch = Stretch.Fill, Visibility = Visibility.Collapsed };
            public readonly ScaleTransform Scale = new ScaleTransform();
            public readonly TranslateTransform Move = new TranslateTransform();
            public bool Used;
            public string Kind;
            public double Direction, Delay, Height;
            public readonly RectangleGeometry ComponentClip = new RectangleGeometry();
            public Particle()
            {
                var transforms = new TransformGroup(); transforms.Children.Add(Scale); transforms.Children.Add(Move);
                Image.RenderTransform = transforms;
            }
        }
        sealed class Active
        {
            public WaterEffectBudget.Group Budget;
            public readonly List<Particle> Particles = new List<Particle>(10);
            public double Started, Duration, EndAt = double.PositiveInfinity;
            public string Cue;
            public int Tier;
        }
        static readonly Particle[] Pool = MakePool();
        static readonly WaterEffectBudget Budget = new WaterEffectBudget();
        static Particle[] MakePool() { var p = new Particle[24]; for (var i = 0; i < p.Length; i++) p[i] = new Particle(); return p; }
        public static int ActiveInstances => Budget.Instances;
        public static int ActiveGroups => Budget.Groups;
        readonly List<Active> _active = new List<Active>(2);
        readonly OverlayAnimationSchedule.Subscription _clock;
        readonly Canvas _back, _front;
        readonly RectangleGeometry _clipGeometry = new RectangleGeometry();
        public readonly OverlayWaterEffectsPolicy Policy = new OverlayWaterEffectsPolicy();
        string _quality = "standard";
        bool _self, _visible = true, _eligible = true, _disposed;
        string _phase;
        double _surfaceY;
        Rect _clip = Rect.Empty;
        public event Action<string, long, string> MarkerPublished;
        public OverlayWaterEffects(Canvas back, Canvas front)
        {
            _back = back; _front = front;
            _back.Clip = _clipGeometry; _front.Clip = _clipGeometry;
            _clock = OverlayAnimationClock.Create(16); _clock.Tick += Tick;
            WaterEffectResources.Warmup();
        }
        public void Configure(FishingPresentationEnvelope envelope, bool self, string quality)
        {
            _self = self;
            if (Policy.SetEnvelope(envelope)) Stop(true);
            if (!_visible) Policy.SuppressOneShots = true;
            var next = quality == "off" || quality == "reduced" ? quality : "standard";
            if (next != _quality) { _quality = next; Stop(false); }
        }
        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (!visible) { Policy.SuppressOneShots = true; Stop(true); }
        }
        public void SetEligible(bool eligible) { _eligible = eligible; if (!eligible && !_self) Stop(true); }
        public void SetClip(Rect clip)
        {
            if (_clip == clip) return;
            _clip = clip;
            _clipGeometry.Rect = clip;
        }
        public void SetSurface(double surfaceY) { _surfaceY = surfaceY; }
        public void Phase(string phase)
        {
            if (_phase == phase) return;
            _phase = phase;
            if (OverlayWaterEffectsPolicy.Ending(phase)) Stop(false);
        }
        public bool Publish(string cue, string phase, long occurrence, Point anchor, double slotScale, bool contact)
        {
            if (_disposed || !_visible || !_eligible) return false;
            if (!Policy.Accept(cue, phase, occurrence, OverlayAnimationClock.Now, contact, out var key)) return false;
            // Audio is a separate consumption bit owned by Unity; visual quality and
            // resource admission never suppress this shared semantic callback.
            MarkerPublished?.Invoke(cue, occurrence, key);
            if (_quality == "off" || _clip.IsEmpty || !_clip.Contains(anchor)) return false;
            var reduced = _quality == "reduced";
            if (reduced && !_self) return false;
            var pack = WaterEffectResources.Current;
            if (pack?.Manifest.Cues == null) return false; // no catch-up queue
            WaterEffectCue config = null;
            foreach (var item in pack.Manifest.Cues) if (item.Cue == cue || Array.IndexOf(item.Markers ?? Array.Empty<string>(), cue) >= 0) { config = item; break; }
            if (config == null || Array.IndexOf(config.AllowedPhases ?? Array.Empty<string>(), phase) < 0 || reduced && config.ReducedVariant == "none") return false;
            var tierName = config.SizeTier == "shared" ? OverlayWaterEffectsPolicy.Tier(Policy.Envelope.WaterSizeTier) : "S";
            if (!pack.Manifest.Tiers.TryGetValue(tierName, out var tier)) return false;
            var tierIndex = tierName == "XL" ? 3 : tierName == "L" ? 2 : tierName == "M" ? 1 : 0;
            var sharedTier = config.SizeTier == "shared";
            var arcCount = reduced ? (tierIndex > 0 ? 2 : 1) : sharedTier ? tier.ArcCount : cue == "groundbait.contact" ? 2 : 1;
            var shape = OverlayWaterEffectsPolicy.Components(cue, arcCount, sharedTier ? tier.CrownCount : 1,
                sharedTier ? tier.DropCount : 1, _self, reduced);
            var arcs = shape.Arcs; var crowns = shape.Crowns;
            var count = Math.Min(10, shape.Count);
            _surfaceY = anchor.Y;
            if (count < 1) return false;
            if (cue == "bite.onset") RemoveCue("waiting.ripple");
            if (cue == "fish.surface" || cue == "fish.slap") RemoveCue("fish.struggle");
            var effect = new Active { Started = OverlayAnimationClock.Now, Duration = reduced ? 150 + tierIndex * 20 :
                !_self ? Math.Min(320, tier.DurationMs) : config.DurationMs > 0 ? config.DurationMs : tier.DurationMs, Cue = cue, Tier = tierIndex };
            effect.Duration = OverlayWaterEffectsPolicy.Duration(cue, effect.Duration);
            var priority = _self ? cue == "bite.onset" ? 100 : cue == "fish.surface" || cue == "fish.slap" ? 90 : cue == "waiting.ripple" ? 10 : 70 : 30;
            effect.Budget = Budget.Admit(this, _self, count, priority, () => Recycle(effect));
            if (effect.Budget == null) return false;
            _active.Add(effect);
            var variant = OverlayWaterEffectsPolicy.Variant(key) == 0 ? "a" : "b";
            for (var index = 0; index < count; index++)
            {
                Particle particle = null;
                foreach (var free in Pool) if (!free.Used) { particle = free; break; }
                if (particle == null) break;
                particle.Used = true;
                particle.Kind = index < arcs ? "ripple" : index < arcs + crowns ? "splash" : "drop";
                var assetTier = !_self && tierIndex > 1 ? "m" : tierName.ToLowerInvariant();
                var asset = particle.Kind == "drop" ? index % 2 == 0 ? "drop-round" : "drop-short" : particle.Kind + "-" + assetTier + "-" + variant;
                if (!pack.Textures.TryGetValue(asset, out var texture)) { particle.Used = false; continue; }
                particle.Image.Source = texture;
                var scale = slotScale * (cue == "groundbait.contact" ? .6 : 1);
                if (particle.Kind == "ripple") scale *= cue == "waiting.ripple" ? 1.7 : 1.2;
                // Remote L/XL retain range differences while using only a single M arc.
                if (!_self && tierIndex > 1) scale *= tierIndex == 2 ? 1.25 : 1.5;
                var canvasWidth = particle.Kind == "drop" ? 64 : 128;
                var canvasAnchorX = canvasWidth / 2;
                particle.Image.Width = canvasWidth * scale; particle.Image.Height = 64 * scale;
                Canvas.SetLeft(particle.Image, anchor.X - canvasAnchorX * scale);
                var top = particle.Kind == "ripple" && pack.OpaqueTop.TryGetValue(asset, out var opaqueTop) ? opaqueTop : 44;
                // Keep the full visible ellipse below the water plane rather
                // than discarding its upper semicircle. The dynamic clip below
                // remains the final guard as transforms and slot size change.
                Canvas.SetTop(particle.Image, anchor.Y - top * scale);
                particle.Scale.CenterX = canvasAnchorX * scale; particle.Scale.CenterY = 44 * scale;
                particle.Image.Clip = null;
                if (particle.Kind == "ripple")
                {
                    particle.ComponentClip.Rect = new Rect(0, 44 * scale, 128 * scale, 20 * scale);
                    particle.Image.Clip = particle.ComponentClip;
                }
                else if (particle.Kind == "splash" && crowns == 2)
                {
                    particle.ComponentClip.Rect = new Rect((index - arcs) * 64 * scale, 0, 64 * scale, 64 * scale);
                    particle.Image.Clip = particle.ComponentClip;
                }
                particle.Direction = (index % 2 == 0 ? -1 : 1) * (4 + index * 1.7) * slotScale;
                particle.Height = (4 + tierIndex * 7) * slotScale;
                if (cue == "groundbait.contact") Canvas.SetLeft(particle.Image, anchor.X - canvasAnchorX * scale + (index < arcs ? index - (arcs - 1) * .5 : 0) * 7 * slotScale);
                particle.Delay = particle.Kind == "ripple" ? index * .08 : particle.Kind == "drop" ? .08 + (index % 3) * .035 : .02;
                (particle.Kind == "drop" ? _front : _back).Children.Add(particle.Image);
                particle.Image.Visibility = Visibility.Visible;
                effect.Particles.Add(particle);
            }
            _clock.Start(); OverlayAnimationClock.Wake(); Tick(null, EventArgs.Empty); return true;
        }
        void RemoveCue(string cue)
        { for (var i = _active.Count - 1; i >= 0; i--) if (_active[i].Cue == cue) Recycle(_active[i]); }
        void Tick(object sender, EventArgs e)
        {
            var now = OverlayAnimationClock.Now;
            for (var index = _active.Count - 1; index >= 0; index--)
            {
                var effect = _active[index];
                var elapsed = now - effect.Started;
                if (elapsed >= effect.Duration || now >= effect.EndAt) { Recycle(effect); continue; }
                var progress = elapsed / effect.Duration;
                var ending = Math.Min(1, (effect.EndAt - now) / 100);
                foreach (var p in effect.Particles)
                {
                    var t = Math.Max(0, Math.Min(1, (progress - p.Delay) / (1 - p.Delay)));
                    // The PNG already carries translucent paint. Hold its colour before
                    // fading, instead of multiplying it by a second 55% ceiling.
                    var fade = Math.Max(0, Math.Min(1, (t - .4) / .6));
                    var opacity = .95 * Math.Min(1, t / .06) * (1 - fade * fade * (3 - 2 * fade));
                    p.Image.Opacity = progress < p.Delay ? 0 : opacity * ending;
                    if (p.Kind == "ripple")
                    {
                        p.Scale.ScaleX = .62 + t * .48; p.Scale.ScaleY = .7 + t * .3;
                        p.Move.X = p.Direction * .15 * t; p.Move.Y = t * 2;
                        // The image clip is before RenderTransform. Invert the
                        // current transform so the rendered arc cannot cross the
                        // actual water line, including resize and actor motion.
                        var y = (_surfaceY - Canvas.GetTop(p.Image) - p.Move.Y - p.Scale.CenterY * (1 - p.Scale.ScaleY)) / p.Scale.ScaleY;
                        y = Math.Max(0, Math.Min(p.Image.Height, y));
                        p.ComponentClip.Rect = new Rect(0, y, p.Image.Width, p.Image.Height - y);
                    }
                    else if (p.Kind == "splash")
                    { p.Scale.ScaleX = .7 + Math.Sin(t * Math.PI / 2) * .3; p.Scale.ScaleY = .35 + Math.Sin(t * Math.PI) * .65; p.Move.X = p.Direction * .2 * t; p.Move.Y = 0; }
                    else
                    { p.Scale.ScaleX = p.Scale.ScaleY = 1; p.Move.X = p.Direction * Math.Sqrt(t); p.Move.Y = -Math.Sin(t * Math.PI) * p.Height; }
                }
            }
            if (_active.Count == 0) _clock.Stop();
        }
        void Recycle(Active effect)
        {
            Budget.Release(effect.Budget); _active.Remove(effect);
            foreach (var p in effect.Particles)
            {
                p.Image.Visibility = Visibility.Collapsed; p.Image.Source = null;
                if (p.Image.Parent is Panel parent) parent.Children.Remove(p.Image);
                p.Used = false;
            }
            effect.Particles.Clear();
            if (_active.Count == 0) _clock.Stop();
        }
        public void Stop(bool immediately)
        {
            if (immediately) { while (_active.Count > 0) Recycle(_active[_active.Count - 1]); }
            else foreach (var effect in _active) effect.EndAt = Math.Min(effect.EndAt, OverlayAnimationClock.Now + 100);
        }
        public void Dispose() { _disposed = true; Stop(true); _clock.Tick -= Tick; _clock.Stop(); }
    }
}
