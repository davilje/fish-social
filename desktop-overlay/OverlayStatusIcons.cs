using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FishSocialOverlay
{
    /// <summary>
    /// 16–20px phase icons: OverlayResources/status/fishing.png and hooked.png.
    /// Missing files fall back to a frozen vector DrawingImage (no crash).
    /// </summary>
    static class OverlayStatusIcons
    {
        public const double Size = 18;

        static readonly Dictionary<string, ImageSource> Cache =
            new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public static ImageSource Get(string kind)
        {
            var key = string.IsNullOrWhiteSpace(kind) ? "fishing" : kind.Trim();
            if (Cache.TryGetValue(key, out var cached))
                return cached;

            var loaded = TryLoadPng(key) ?? CreatePlaceholder(key);
            Cache[key] = loaded;
            return loaded;
        }

        static ImageSource TryLoadPng(string kind)
        {
            if (!IsSafeKind(kind))
                return null;
            var path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "OverlayResources",
                "status",
                kind + ".png");
            if (!File.Exists(path))
                return null;
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        static bool IsSafeKind(string kind)
        {
            for (var i = 0; i < kind.Length; i++)
            {
                var c = kind[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_'))
                    return false;
            }

            return kind.Length > 0;
        }

        static ImageSource CreatePlaceholder(string kind)
        {
            var hooked = string.Equals(kind, "hooked", StringComparison.OrdinalIgnoreCase);
            var fill = hooked
                ? Color.FromRgb(232, 156, 64)
                : Color.FromRgb(90, 168, 214);
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(
                new SolidColorBrush(Color.FromArgb(0xE6, fill.R, fill.G, fill.B)),
                new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 230)), 0.8),
                new EllipseGeometry(new Point(9, 9), 8, 8)));
            if (hooked)
            {
                var hook = new PathGeometry();
                hook.Figures.Add(new PathFigure(
                    new Point(9, 4),
                    new PathSegment[]
                    {
                        new LineSegment(new Point(9, 12), true),
                        new ArcSegment(
                            new Point(5, 12),
                            new Size(4, 3.5),
                            0,
                            false,
                            SweepDirection.Clockwise,
                            true),
                    },
                    false));
                group.Children.Add(new GeometryDrawing(
                    null,
                    new Pen(Brushes.White, 1.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
                    hook));
            }
            else
            {
                group.Children.Add(new GeometryDrawing(
                    null,
                    new Pen(Brushes.White, 1.3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round },
                    new LineGeometry(new Point(6, 12), new Point(13, 5))));
                group.Children.Add(new GeometryDrawing(
                    Brushes.White,
                    null,
                    new EllipseGeometry(new Point(13, 5), 1.6, 1.6)));
            }

            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }
    }
}
