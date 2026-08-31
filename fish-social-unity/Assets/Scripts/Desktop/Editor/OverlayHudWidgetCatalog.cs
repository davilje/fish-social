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

            "btn_pan_left",

            "btn_pan_right",

            "dock_chat",

        };



        public static readonly string[] GroupIds =

        {

            "menu_rail",

            "dock_fishing",

        };



        public static readonly HudWidgetSpec[] All =

        {

            // ── 右侧竖条 ──

            new HudWidgetSpec("menu_rail", "group", 876, 217, 152, 328, true, null),

            new HudWidgetSpec("btn_menu_settings", "button", 0, 0, 70, 32, false, "menu_rail"),

            new HudWidgetSpec("btn_menu_map", "button", 0, 37, 70, 32, false, "menu_rail"),

            new HudWidgetSpec("btn_menu_shop", "button", 0, 74, 70, 32, false, "menu_rail"),

            new HudWidgetSpec("btn_menu_friends", "button", 0, 111, 70, 32, false, "menu_rail"),

            new HudWidgetSpec("btn_menu_catch", "button", 0, 148, 70, 32, false, "menu_rail"),

            new HudWidgetSpec("btn_menu_leaderboard", "button", 0, 185, 70, 32, false, "menu_rail"),

            new HudWidgetSpec("btn_debug_police", "button", -82, 151, 70, 32, false, "menu_rail"),

            new HudWidgetSpec("btn_debug_gameplay", "button", -82, 196, 70, 32, false, "menu_rail"),

            new HudWidgetSpec("btn_menu_toggle", "button", 0, 222, 70, 32, true, "menu_rail"),

            new HudWidgetSpec("btn_open_main", "button", 0, 259, 70, 32, true, "menu_rail"),

            new HudWidgetSpec("btn_exit_pond", "button", 0, 296, 70, 32, true, "menu_rail"),



            // ── 状态胶囊 ──

            new HudWidgetSpec("cap_status", "panel", 733, 458, 134, 88, true, null),

            new HudWidgetSpec("txt_status", "text", 6, 6, 78, 18, true, "cap_status"),

            new HudWidgetSpec("txt_pond", "text", 6, 26, 78, 16, true, "cap_status"),

            new HudWidgetSpec("txt_spot", "text", 6, 44, 78, 16, true, "cap_status"),

            new HudWidgetSpec("txt_error", "text", 371, 484, 218, 22, true, null),



            // ── 底部钓鱼条 ──

            new HudWidgetSpec("dock_fishing", "group", 371, 506, 218, 40, true, null),

            new HudWidgetSpec("btn_fishing_toggle", "button", 0, 0, 70, 32, true, "dock_fishing"),

            new HudWidgetSpec("btn_groundbait", "button", 74, 0, 70, 32, false, "dock_fishing"),

            new HudWidgetSpec("btn_catch_leave", "button", 148, 0, 70, 32, true, "dock_fishing"),



            // ── 打窝说明（独立控件，非 dock_fishing 子节点）──

            new HudWidgetSpec("txt_groundbait", "text", 611, 491, 104, 58, false, null),



            // ── 场景横移箭头（视口左右，默认隐藏；宽场景由 Overlay 显示）──

            new HudWidgetSpec("btn_pan_left", "button", 8, 248, 36, 64, false, null),

            new HudWidgetSpec("btn_pan_right", "button", 916, 248, 36, 64, false, null),



            // ── 聊天底栏：dock_chat.h = 折叠高度（九宫底板）；展开高 = 子控件 y+h 最大；底边固定向上长 ──

            new HudWidgetSpec("dock_chat", "panel", 23, 506, 272, 45, true, null),

            new HudWidgetSpec("chat_preview", "text", 22, 12, 212, 24, true, "dock_chat"),

            new HudWidgetSpec("chat_toggle", "button", 239, 12, 23, 24, true, "dock_chat"),

            new HudWidgetSpec("chat_log", "panel", 22, 17, 212, 115, false, "dock_chat"),

            new HudWidgetSpec("chat_scroll", "panel", 234, 17, 6, 115, false, "dock_chat"),

            new HudWidgetSpec("chat_input", "panel", 8, 125, 231, 37, false, "dock_chat"),

            new HudWidgetSpec("chat_send", "button", 239, 135, 23, 24, false, "dock_chat"),

            new HudWidgetSpec("chat_placeholder", "text", 34, 139, 184, 16, false, "dock_chat"),

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


