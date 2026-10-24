using System.Windows;
using System.Windows.Media;

namespace FishSocialOverlay
{
    // Native vector rendering follows the journal paper and primary-button palette at every DPI.
    internal static class OverlayHookProgressStyle
    {
        public const double StrokeWidth = 3;
        public static readonly Brush Progress = CreateProgress();
        public static readonly ImageSource Background = CreateBackground();
        static Brush CreateProgress()
        {
            var brush = new LinearGradientBrush(Color.FromRgb(255, 170, 94), Color.FromRgb(252, 146, 60), 90);
            brush.Freeze(); return brush;
        }
        static ImageSource CreateBackground()
        {
            var drawing = new DrawingGroup();
            using (var context = drawing.Open())
            {
                context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 48, 48));
                var paper = new SolidColorBrush(Color.FromRgb(255, 253, 247));
                var track = new Pen(new SolidColorBrush(Color.FromRgb(239, 223, 196)), StrokeWidth);
                context.DrawEllipse(paper, track, new Point(24, 24), 22.5, 22.5);
            }
            drawing.Freeze();
            var image = new DrawingImage(drawing); image.Freeze(); return image;
        }
    }
}
