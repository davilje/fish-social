using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Pet
{
    /// <summary>
    /// Sequence-frame placeholder renderer. Replace this component with a Spine
    /// renderer later; PetStateController and session code stay unchanged.
    /// </summary>
    public sealed class SpriteFramePetRenderer : MonoBehaviour, IPetRenderer
    {
        [SerializeField] Sprite[] _frames;
        [SerializeField] float _idleFps = 2f;
        [SerializeField] float _activeFps = 6f;

        Image _image;
        PetVisualState _state = PetVisualState.Offline;
        string _petId;
        float _elapsed;
        bool _usingArt;

        public void Bind(Image target)
        {
            _image = target;
            ReloadFrames();
            Apply(_state);
        }

        public void Apply(PetVisualState state)
        {
            _state = state;
            if (_image == null)
                return;

            ReloadFrames();
            _image.preserveAspect = true;
            _image.color = _usingArt ? Color.white : TintFor(state);
            if (_frames != null && _frames.Length > 0)
                _image.sprite = _frames[0];
        }

        void ReloadFrames()
        {
            var petId = PetArtLoader.CurrentOwnPetId();
            var art = PetArtLoader.GetFrames(petId, _state);
            if (art != null && art.Length > 0)
            {
                _frames = art;
                _petId = petId;
                _usingArt = true;
                return;
            }

            _usingArt = false;
            if (_frames == null || _frames.Length == 0 || _petId != null)
                _frames = PlaceholderPetFrames.GetFrames();
            _petId = petId;
        }

        void Update()
        {
            if (_image == null || _frames == null || _frames.Length < 2)
                return;
            if (_state == PetVisualState.Offline || _state == PetVisualState.Hooked)
                return;

            var fps = _state == PetVisualState.Dragging ? _activeFps : _idleFps;
            _elapsed += Time.unscaledDeltaTime * fps;
            var index = Mathf.FloorToInt(_elapsed) % _frames.Length;
            if (index < 0)
                index = 0;
            _image.sprite = _frames[index];
        }

        static Color TintFor(PetVisualState state)
        {
            switch (state)
            {
                case PetVisualState.Sit:
                    return new Color(0.82f, 0.92f, 0.78f, 1f);
                case PetVisualState.Cast:
                    return new Color(0.78f, 0.86f, 1f, 1f);
                case PetVisualState.Fishing:
                    return new Color(0.72f, 0.88f, 1f, 1f);
                case PetVisualState.Hooked:
                    return new Color(1f, 0.78f, 0.38f, 1f);
                case PetVisualState.Reel:
                case PetVisualState.Catching:
                    return new Color(0.5f, 0.82f, 1f, 1f);
                case PetVisualState.Dragging:
                    return new Color(1f, 0.92f, 0.45f, 1f);
                case PetVisualState.Offline:
                    return new Color(0.55f, 0.58f, 0.62f, 1f);
                default:
                    return Color.white;
            }
        }
    }
}
