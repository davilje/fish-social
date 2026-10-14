using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Stable widget id for Overlay HUD export. Must match overlay-hud.json ids.
    /// Sprite file fields are HUD-relative PNG paths (for example cozy/icons-light/map.png).
    /// Button metrics use the shared Cozy defaults: 3px downward press, 16px icon.
    /// </summary>
    public sealed class DesktopOverlayHudWidget : MonoBehaviour
    {
        public string widgetId;
        public string kind = "button";
        public string spriteFile;
        [Tooltip("HUD-relative PNG path; keep cozy/ subdirectories, never an absolute path.")]
        public string spriteHoverFile;
        public string spritePressedFile;
        public string iconSpriteFile;
        public float pressedOffsetY = 3f;
        public float iconSize = 16f;
        public int zIndex = 100;
        public bool visibleDefault = true;
    }
}
