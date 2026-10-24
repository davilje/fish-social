using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace FishSocialOverlay
{
    internal static class OverlayFishingMotionDrawing
    {
        static readonly ConditionalWeakTable<FishingMotionSample, DrawingImage> Lines = new ConditionalWeakTable<FishingMotionSample, DrawingImage>();

        public static ImageSource Line(FishingMotionSample sample)
        {
            return sample?.Line == null ? null : Lines.GetValue(sample, DrawLine);
        }

        static DrawingImage DrawLine(FishingMotionSample sample)
        {
            var points = sample.Line;
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(points[0], points[1]), false, false);
                context.QuadraticBezierTo(new Point(points[2], points[3]), new Point(points[4], points[5]), true, false);
            }
            var group = new DrawingGroup { ClipGeometry = new RectangleGeometry(new Rect(0, 0, 640, 512)) };
            group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 640, 512))));
            group.Children.Add(new GeometryDrawing(null, new Pen(new SolidColorBrush(Color.FromRgb(62, 54, 40)), 1.4), geometry));
            group.Freeze();
            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }
    }
}
