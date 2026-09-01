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
        public static bool ContextMenuOpen { get; set; }

        public static event Action ContextMenuClosed;

        public static void NotifyContextMenuOpened()
        {
            ContextMenuOpen = true;
        }

        public static void NotifyContextMenuClosed()
        {
            ContextMenuOpen = false;
            if (Mouse.Captured != null)
                Mouse.Capture(null);
            ContextMenuClosed?.Invoke();
        }
    }
}
