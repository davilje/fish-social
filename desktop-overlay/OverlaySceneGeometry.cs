using System;

namespace FishSocialOverlay
{
    internal static class OverlaySceneGeometry
    {
        // Small window only. 72 = the earlier 24px rise plus another 48px,
        // so the seated cat sits lower in the 600×300 frame.
        const double SmallWindowLift = 72;

        public static double CropTop(double sceneHeight, double viewportHeight)
        {
            var crop = Math.Max(0, sceneHeight - viewportHeight);
            return Math.Max(0, crop - (viewportHeight <= 300.5 ? SmallWindowLift : 0));
        }

        // Quarter-pixel depth bands keep fractional export noise from changing
        // same-row ordering. Right-facing neighbours consistently cover rear rods.
        public static int ActorOrder(double groundY, double x)
        {
            if (double.IsNaN(groundY) || double.IsInfinity(groundY) || double.IsNaN(x) || double.IsInfinity(x)) return 0;
            var depth = (int)Math.Round(Math.Max(0, Math.Min(16383, groundY)) * 4);
            return depth * 4096 + (int)Math.Round(Math.Max(0, Math.Min(4095, x)));
        }
    }
}
