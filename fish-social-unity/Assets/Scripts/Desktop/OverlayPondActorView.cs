using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Root of OverlayPondActor.prefab — seat art + pet/name/status/ring alignment.
    /// Pond layouts nest this prefab as an instance under each kind=spot (STEAM-DESKTOP-14A).
    /// Edit the prefab asset to change every pond; per-spot overrides only for spotId / position.
    /// </summary>
    public sealed class OverlayPondActorView : MonoBehaviour
    {
        public string spotId;
    }
}
