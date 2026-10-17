#if UNITY_EDITOR
namespace FishSocial.Desktop.Editor
{
    /// <summary>
    /// Overlay HUD widget tree (960×560). Child x/y are relative to parent when ParentId is set.
    /// Keep in sync with OverlayHud.prefab (export via Fish Social → Export Overlay HUD).
    /// </summary>
    static class OverlayHudWidgetCatalog
    {
        public static readonly string[] Required =
        {
            "cap_status",
            "btn_exit_pond",
            "btn_fishing_toggle",
            "btn_groundbait",
            "btn_catch_leave",
            "btn_pan_left",
            "btn_pan_right",
            "dock_chat",
        };

        public static readonly string[] GroupIds =
        {
            "dock_fishing",
        };

        /// <summary>菜单只走右键；这些节点在补齐 OverlayHud 时从 prefab 删除。</summary>
        public static readonly string[] Retired =
        {
            "menu_rail",
            "btn_menu_toggle",
            "btn_menu_map",
            "btn_menu_shop",
            "btn_menu_friends",
            "btn_menu_catch",
            "btn_menu_leaderboard",
            "btn_menu_settings",
            "btn_open_main",
        };

        public static readonly HudWidgetSpec[] All =
        {
            // ── 右下角：Debug（仅调宽）+ 状态胶囊 + 退出鱼塘 ──
            new HudWidgetSpec("btn_debug_police", "button", 906, 424, 38, 36, false, null),
            new HudWidgetSpec("btn_debug_gameplay", "button", 856, 468, 88, 36, false, null),
            new HudWidgetSpec("btn_exit_pond", "button", 856, 512, 88, 36, true, null),

            new HudWidgetSpec("cap_status", "panel", 684, 472, 164, 76, true, null),
            new HudWidgetSpec("txt_status", "text", 6, 6, 152, 18, true, "cap_status"),
            new HudWidgetSpec("txt_pond", "text", 6, 28, 152, 18, true, "cap_status"),
            new HudWidgetSpec("txt_spot", "text", 6, 50, 152, 18, true, "cap_status"),
            new HudWidgetSpec("txt_error", "text", 12, 12, 936, 28, true, null),

            // ── 底部居中钓鱼条 ──
            new HudWidgetSpec("dock_fishing", "group", 370, 512, 280, 36, true, null),
            new HudWidgetSpec("btn_fishing_toggle", "button", 0, 0, 88, 36, true, "dock_fishing"),
            new HudWidgetSpec("btn_groundbait", "button", 96, 0, 88, 36, false, "dock_fishing"),
            new HudWidgetSpec("btn_catch_leave", "button", 192, 0, 88, 36, true, "dock_fishing"),

            // ── 打窝说明（独立控件，非 dock_fishing 子节点）──
            new HudWidgetSpec("txt_groundbait", "text", 354, 442, 104, 58, false, null),

            // ── 场景横移箭头（视口左右，默认隐藏；宽场景由 Overlay 显示）──
            new HudWidgetSpec("btn_pan_left", "button", 12, 262, 64, 36, false, null),
            new HudWidgetSpec("btn_pan_right", "button", 884, 262, 64, 36, false, null),

            // ── 左下聊天底栏：dock_chat.h = 折叠高度；展开高 = 子控件 y+h 最大（10 行）；底边固定向上长 ──
            new HudWidgetSpec("dock_chat", "panel", 16, 506, 320, 42, true, null),
            new HudWidgetSpec("chat_preview", "text", 12, 9, 262, 24, true, "dock_chat"),
            new HudWidgetSpec("chat_toggle", "button", 282, 7, 28, 28, true, "dock_chat"),
            new HudWidgetSpec("chat_log", "panel", 12, 12, 288, 212, false, "dock_chat"),
            new HudWidgetSpec("chat_scroll", "panel", 304, 12, 6, 212, false, "dock_chat"),
            new HudWidgetSpec("chat_input", "panel", 12, 232, 230, 36, false, "dock_chat"),
            new HudWidgetSpec("chat_send", "button", 248, 236, 28, 28, false, "dock_chat"),
            new HudWidgetSpec("chat_placeholder", "text", 24, 242, 200, 16, false, "dock_chat"),
        };

        public struct HudWidgetSpec
        {
            public readonly string Id;
            public readonly string Kind;
            public readonly float X;
            public readonly float Y;
            public readonly float W;
            public readonly float H;
            public readonly bool VisibleDefault;
            public readonly string ParentId;

            public HudWidgetSpec(
                string id,
                string kind,
                float x,
                float y,
                float w,
                float h,
                bool visibleDefault,
                string parentId)
            {
                Id = id;
                Kind = kind;
                X = x;
                Y = y;
                W = w;
                H = h;
                VisibleDefault = visibleDefault;
                ParentId = parentId;
            }
        }
    }
}
#endif
