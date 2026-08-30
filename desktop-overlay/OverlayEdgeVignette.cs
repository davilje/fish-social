using System;
using System.Windows;
using System.Windows.Media;

namespace FishSocialOverlay
{
    /// <summary>
    /// Scene-only edge fade. Absolute mapping is required: RelativeToBoundingBox follows
    /// overflowing descendants (pets/labels), which pushes the bottom fade below ClipToBounds.
    /// </summary>
    public sealed class OverlayEdgeVignette
    {
        public const double EdgeTop = 40;
        public const double EdgeBottom = 40;
        public const double EdgeLeft = 40;
        public const double EdgeRight = 40;

        readonly FrameworkElement _verticalHost;
        readonly FrameworkElement _horizontalHost;

        public OverlayEdgeVignette(FrameworkElement verticalHost, FrameworkElement horizontalHost)
        {
            _verticalHost = verticalHost ?? throw new ArgumentNullException(nameof(verticalHost));
            _horizontalHost = horizontalHost ?? throw new ArgumentNullException(nameof(horizontalHost));
        }

        public void ApplySize(double width, double height)
        {
            var w = Math.Max(1.0, width);
            var h = Math.Max(1.0, height);
            _verticalHost.OpacityMask = CreateVerticalMask(h);
            _horizontalHost.OpacityMask = CreateHorizontalMask(w);
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
