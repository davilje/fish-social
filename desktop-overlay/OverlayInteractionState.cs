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

        /// <summary>
        /// Right-click captures the pond until button-up so the menu still opens
        /// when the previous popup closes. That capture must survive menu Closed.
        /// </summary>
        public static bool PreserveMouseCapture { get; set; }

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
            if (!PreserveMouseCapture && Mouse.Captured != null)
                Mouse.Capture(null);
            if (wasSuppressed)
                MenuHoverSuppressEnded?.Invoke();
        }
    }
}
