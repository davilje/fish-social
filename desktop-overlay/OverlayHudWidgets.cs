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
            "btn_menu_toggle",
            "btn_menu_map",
            "btn_menu_shop",
            "btn_menu_friends",
            "btn_menu_catch",
            "btn_menu_leaderboard",
            "btn_menu_settings",
            "cap_status",
            "btn_open_main",
            "btn_exit_pond",
            "btn_fishing_toggle",
            "btn_groundbait",
            "btn_catch_leave",
            "dock_chat",
        };

        public static readonly string[] LayoutGroups =
        {
            "menu_rail",
            "dock_fishing",
        };
    }
}
