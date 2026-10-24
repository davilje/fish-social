using System;

namespace FishSocialOverlay
{
    internal static class OverlayUiAudioPolicy
    {
        public static bool Allowed(string cue)
        {
            switch (cue)
            {
                case "ui_click": case "ui_open": case "ui_close": case "ui_toggle":
                case "ui_tick": case "ui_success": case "ui_error": case "manual_meow": return true;
                default: return false;
            }
        }

        // Navigation is sounded by Unity after the route is accepted. Native
        // class handlers must not emit a second generic click for that gesture.
        public static bool IsNavigation(string action)
        {
            switch (action)
            {
                case "OpenMain_OnClick": case "MenuPond_OnClick": case "MenuMap_OnClick":
                case "MenuShop_OnClick": case "MenuFriends_OnClick": case "MenuCatch_OnClick":
                case "MenuGallery_OnClick": case "MenuProfile_OnClick": case "MenuFeed_OnClick":
                case "MenuLeaderboard_OnClick": case "MenuSettings_OnClick": return true;
                default: return false;
            }
        }

        public static string LocalCue(string name, string content, bool expanded)
        {
            if (name == "MenuToggleButton" || name == "ChatDockToggle" || name == "GameplayDebugButton")
                return expanded ? "ui_close" : "ui_open";
            if (content == "关闭" || content == "取消" || content == "返回") return "ui_close";
            return "ui_click";
        }
    }

    internal sealed class OverlayPetClickGesture
    {
        double _x, _y, _started;
        bool _active, _moved, _cancelled;
        public void Begin(double x, double y, double nowMs)
        { _x = x; _y = y; _started = nowMs; _active = true; _moved = _cancelled = false; }
        public void WindowMoved() { if (_active) _moved = true; }
        public void Cancel() { if (_active) _cancelled = true; }
        public bool Complete(double x, double y, double nowMs, double thresholdX, double thresholdY, bool released)
        {
            var click = _active && !_moved && !_cancelled && released && nowMs - _started <= 650 &&
                Math.Abs(x - _x) <= thresholdX && Math.Abs(y - _y) <= thresholdY;
            _active = false; return click;
        }
    }
}
