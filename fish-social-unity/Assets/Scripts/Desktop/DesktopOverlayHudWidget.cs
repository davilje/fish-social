using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Stable widget id for Overlay HUD export. Must match overlay-hud.json ids.
    /// </summary>
    public sealed class DesktopOverlayHudWidget : MonoBehaviour
    {
        public string widgetId;
        public string kind = "button";
        public string spriteFile;
        public int zIndex = 100;
        public bool visibleDefault = true;
    }
}
