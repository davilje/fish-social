namespace FishSocialOverlay
{
    /// <summary>
    /// Shared overlay input state so scene drag does not open pet tooltips.
    /// </summary>
    static class OverlayInteractionState
    {
        public static bool SceneDragging { get; set; }
    }
}
