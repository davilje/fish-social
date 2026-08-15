using UnityEngine.UI;

namespace FishSocial.Desktop.Pet
{
    /// <summary>
    /// Presentation-only. Swap SpriteFramePetRenderer for Spine later without
    /// changing PetStateController, window, or pond session code.
    /// </summary>
    public interface IPetRenderer
    {
        void Bind(Image target);
        void Apply(PetVisualState state);
    }
}
