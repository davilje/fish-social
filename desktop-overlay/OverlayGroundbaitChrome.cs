namespace FishSocialOverlay
{
    /// <summary>Marker visibility follows the live action and HUD, never the persistent bait buff.</summary>
    public static class OverlayGroundbaitChrome
    {
        public const double Size = 36;
        public static bool Show(bool seated, string phase, bool chromeShown, bool windowShown)
            => seated && phase == "groundbaiting" && chromeShown && windowShown;
        public static double Left(double petHitLeft) => petHitLeft - Size - 6;
        public static double Top(double petHitTop) => petHitTop;
    }
}
