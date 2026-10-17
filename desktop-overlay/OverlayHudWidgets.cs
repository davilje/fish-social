namespace FishSocialOverlay
{
    /// <summary>
    /// Overlay HUD widget ids (must match Unity OverlayHud prefab + export).
    /// JSON supports parentId for hierarchical layout groups.
    /// </summary>
    static class OverlayHudWidgets
    {
        public static readonly string[] Required =
        {
            "cap_status",
            "btn_exit_pond",
            "btn_fishing_toggle",
            "btn_groundbait",
            "btn_catch_leave",
            "dock_chat",
        };

        public static readonly string[] LayoutGroups =
        {
            "dock_fishing",
        };
    }
}
