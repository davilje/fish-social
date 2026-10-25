using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FishSocialOverlay
{
    internal static class OverlayFishingBobber
    {
        // Anchor is the visible line's intersection with the current scene water.
        // Dip/tilt affect the float only; the surface emitter stays on that intersection.
        public static void Apply(Image image, Point? anchor, double position, int sourceCount,
            double scaleX, double scaleY, int loopStart = -1)
        {
            image.Visibility = anchor.HasValue ? Visibility.Visible : Visibility.Hidden;
            image.Clip = null;
            if (!anchor.HasValue) return;
            OverlayFishingWaterContact.HookedBobberMotion(position, sourceCount, out var dip, out var angle, loopStart);
            var pivotX = OverlayFishingWaterContact.BobberPivotX * scaleX;
            var pivotY = OverlayFishingWaterContact.BobberPivotY * scaleY;
            var matrix = Matrix.Identity;
            matrix.RotateAt(angle, pivotX, pivotY);
            matrix.Translate(anchor.Value.X - image.Margin.Left - pivotX,
                anchor.Value.Y + dip * scaleY - image.Margin.Top - pivotY);
            var transform = new MatrixTransform(matrix);
            transform.Freeze();
            image.RenderTransform = transform;
        }
    }
}
