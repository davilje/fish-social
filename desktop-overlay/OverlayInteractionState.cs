using System;
using System.Windows.Input;

namespace FishSocialOverlay
{
    /// <summary>
    /// Shared overlay input state so scene drag and context menus do not
    /// leave pet hover stuck off.
    /// </summary>
    static class OverlayInteractionState
    {
        public static bool SceneDragging { get; set; }

        /// <summary>
        /// Overlay-owned menu session flag. Never derived from ContextMenu.IsOpen,
        /// which can stay true after the popup is already gone.
        /// </summary>
        public static bool MenuSuppressesHover { get; private set; }

        public static event Action MenuHoverSuppressEnded;

        public static bool MenuBlocksHover()
        {
            return MenuSuppressesHover;
        }

        public static void BeginMenuHoverSuppress()
        {
            MenuSuppressesHover = true;
        }

        public static void EndMenuHoverSuppress()
        {
            var wasSuppressed = MenuSuppressesHover;
            MenuSuppressesHover = false;
            if (Mouse.Captured != null)
                Mouse.Capture(null);
            if (wasSuppressed)
                MenuHoverSuppressEnded?.Invoke();
        }
    }
}
