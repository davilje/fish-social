using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Media;

namespace FishSocialOverlay
{
    /// <summary>
    /// Scene-only edge fade. Absolute mapping is required: RelativeToBoundingBox follows
    /// overflowing descendants (pets/labels), which pushes the bottom fade below ClipToBounds.
    /// </summary>
    public sealed class OverlayEdgeVignette : IDisposable
    {
        public const double EdgeTop = 40;
        public const double EdgeBottom = 40;
        public const double EdgeLeft = 40;
        public const double EdgeRight = 40;

        readonly bool _combined;
        double _width, _height, _scaleX, _scaleY;
        int _version;
        bool _disposed;
        CancellationTokenSource _build;
        public string Mode { get; private set; } = "dual";
        readonly FrameworkElement _verticalHost;
        readonly FrameworkElement _horizontalHost;

        public OverlayEdgeVignette(FrameworkElement verticalHost, FrameworkElement horizontalHost, bool? combined = null)
        {
            _combined = combined ?? string.Equals(Environment.GetEnvironmentVariable("FISH_SOCIAL_OVERLAY_COMBINED_MASK"), "1", StringComparison.Ordinal);
            _verticalHost = verticalHost ?? throw new ArgumentNullException(nameof(verticalHost));
            _horizontalHost = horizontalHost ?? throw new ArgumentNullException(nameof(horizontalHost));
        }

        public async void ApplySize(double width, double height)
        {
            if (_disposed) return;
            var w = Math.Max(1.0, width);
            var h = Math.Max(1.0, height);
            var dpi = VisualTreeHelper.GetDpi(_horizontalHost);
            if (w == _width && h == _height && dpi.DpiScaleX == _scaleX && dpi.DpiScaleY == _scaleY) return;
            _width = w; _height = h; _scaleX = dpi.DpiScaleX; _scaleY = dpi.DpiScaleY;
            var version = ++_version;
            _build?.Cancel(); _build?.Dispose(); _build = null;
            _verticalHost.OpacityMask = CreateVerticalMask(h);
            _horizontalHost.OpacityMask = CreateHorizontalMask(w);
            Mode = "dual";
            if (!_combined) return;
            _build = new CancellationTokenSource();
            var token = _build.Token;
            // Diagnostic A/B path. Default remains the original continuous gradients
            // until Windows GPU/frame-time and edge-pixel comparisons are accepted.
            try
            {
                var source = await Task.Run(() =>
                {
                    var px = Math.Max(1, (int)Math.Ceiling(w * dpi.DpiScaleX));
                    var py = Math.Max(1, (int)Math.Ceiling(h * dpi.DpiScaleY));
                    var pixels = OverlayMaskRaster.Build(px, py, px / w, py / h, (x, y) => SampleMask(x, y, w, h), token);
                    var bitmap = BitmapSource.Create(px, py, 96, 96, PixelFormats.Pbgra32, null, pixels, px * 4);
                    bitmap.Freeze(); return bitmap;
                }, token);
                await _horizontalHost.Dispatcher.InvokeAsync(() =>
                {
                    if (_disposed || version != _version) return;
                    var brush = new ImageBrush(source)
                    {
                        ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, w, h),
                        Stretch = Stretch.Fill, TileMode = TileMode.None,
                    };
                    brush.Freeze();
                    _verticalHost.OpacityMask = null;
                    _horizontalHost.OpacityMask = brush;
                    Mode = "combined";
                });
            }
            catch (OperationCanceledException) { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _version++;
            _build?.Cancel(); _build?.Dispose(); _build = null;
        }

        /// <summary>
        /// Composed mask alpha at layout pixel (0 = fully faded). Matches the two Absolute
        /// OpacityMasks (vertical then horizontal). Used by click-through; do not delete the masks.
        /// </summary>
        public static double SampleMask(double x, double y, double width, double height)
        {
            var w = Math.Max(1.0, width);
            var h = Math.Max(1.0, height);
            var top = Math.Min(Math.Max(1.0, EdgeTop), h * 0.45);
            var bottom = Math.Min(Math.Max(1.0, EdgeBottom), h * 0.45);
            var left = Math.Min(Math.Max(1.0, EdgeLeft), w * 0.45);
            var right = Math.Min(Math.Max(1.0, EdgeRight), w * 0.45);
            return EdgeAlpha(y, h, top, bottom) * EdgeAlpha(x, w, left, right);
        }

        static double EdgeAlpha(double coord, double size, double startFade, double endFade)
        {
            if (coord <= 0 || coord >= size)
                return 0;
            if (coord < startFade)
                return coord / startFade;
            if (coord > size - endFade)
                return (size - coord) / endFade;
            return 1;
        }

        static Brush CreateVerticalMask(double height)
        {
            var top = Math.Min(Math.Max(1.0, EdgeTop), height * 0.45);
            var bottom = Math.Min(Math.Max(1.0, EdgeBottom), height * 0.45);
            var brush = new LinearGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, height),
                SpreadMethod = GradientSpreadMethod.Pad,
            };
            brush.GradientStops.Add(new GradientStop(Hidden, 0));
            brush.GradientStops.Add(new GradientStop(Visible, top / height));
            brush.GradientStops.Add(new GradientStop(Visible, 1.0 - bottom / height));
            brush.GradientStops.Add(new GradientStop(Hidden, 1));
            brush.Freeze();
            return brush;
        }

        static Brush CreateHorizontalMask(double width)
        {
            var left = Math.Min(Math.Max(1.0, EdgeLeft), width * 0.45);
            var right = Math.Min(Math.Max(1.0, EdgeRight), width * 0.45);
            var brush = new LinearGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                StartPoint = new Point(0, 0),
                EndPoint = new Point(width, 0),
                SpreadMethod = GradientSpreadMethod.Pad,
            };
            brush.GradientStops.Add(new GradientStop(Hidden, 0));
            brush.GradientStops.Add(new GradientStop(Visible, left / width));
            brush.GradientStops.Add(new GradientStop(Visible, 1.0 - right / width));
            brush.GradientStops.Add(new GradientStop(Hidden, 1));
            brush.Freeze();
            return brush;
        }

        // Both alpha and luminance fade (WPF OpacityMask may use either).
        static readonly Color Hidden = Color.FromArgb(0, 0, 0, 0);
        static readonly Color Visible = Color.FromArgb(255, 255, 255, 255);
    }
}
