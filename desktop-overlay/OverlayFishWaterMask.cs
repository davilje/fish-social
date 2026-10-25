using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FishSocialOverlay
{
    internal static class OverlayFishWaterMask
    {
        // WPF applies Clip before RenderTransform. Invert the published pose so
        // the boundary stays horizontal in the scene, including rotated fish.
        public static void Apply(Image image, string clip, double surfaceY)
        {
            var hidden = OverlayFishingWaterContact.IsSubmergedClip(clip);
            image.Visibility = hidden ? Visibility.Hidden : Visibility.Visible;
            if (hidden) { image.Clip = null; return; }
            var matrix = image.RenderTransform.Value;
            if (!matrix.HasInverse || double.IsNaN(surfaceY) || double.IsInfinity(surfaceY))
            { image.Visibility = Visibility.Hidden; image.Clip = null; return; }
            var bounds = image.RenderTransform.TransformBounds(new Rect(0, 0, image.Width, image.Height));
            var bottom = Math.Min(bounds.Bottom, surfaceY - image.Margin.Top);
            if (bottom <= bounds.Top)
            { image.Visibility = Visibility.Hidden; image.Clip = null; return; }
            matrix.Invert();
            var mask = new RectangleGeometry(new Rect(bounds.Left, bounds.Top, bounds.Width, bottom - bounds.Top))
            { Transform = new MatrixTransform(matrix) };
            mask.Freeze();
            image.Clip = mask;
        }
    }
}
