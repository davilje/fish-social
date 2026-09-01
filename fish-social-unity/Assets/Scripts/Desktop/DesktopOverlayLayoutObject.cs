using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Layout object exported to OverlayResources/layouts/&lt;pondId&gt;.json.
        /// kind: sprite | spot | waiting | pet-size |
        /// actor-seat | actor-pet | actor-hit | actor-name | actor-status | actor-ring | actor-ring-bg
        /// Spot hosts nest OverlayPondActor.prefab instances (STEAM-DESKTOP-14A).
    /// </summary>
    public sealed class DesktopOverlayLayoutObject : MonoBehaviour
    {
        public string objectId;
        public string kind = "spot";
        public string spotId;
        public string spriteFile;
        public int zIndex;
        public string anchor = "bottom-center";
    }
}
