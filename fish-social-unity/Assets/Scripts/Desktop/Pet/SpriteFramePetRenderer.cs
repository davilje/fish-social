using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Pet
{
    /// <summary>
    /// Flat pose clips, or the orange seven-layer fishing pack when a manifest exists.
    /// </summary>
    public sealed class SpriteFramePetRenderer : MonoBehaviour, IPetRenderer
    {
        const float LayerFrameSeconds = 0.125f;

        [SerializeField] Sprite[] _frames;
        [SerializeField] float _idleFps = 2f;
        [SerializeField] float _activeFps = 6f;

        Image _image;
        RectTransform _layerHost;
        Image[] _layerImages;
        FishingLayerArt.Session _layered;
        PetVisualState _state = PetVisualState.Offline;
        PetVisualState _deferred;
        bool _deferReel;
        string _petId;
        float _elapsed;
        int _layerIndex;
        bool _usingArt;

        public void Bind(Image target)
        {
            _image = target;
            ReloadFrames();
            Apply(_state);
        }

        public void Apply(PetVisualState state)
        {
            if (ShouldFinishReel(state))
            {
                _deferred = state;
                _deferReel = true;
                return;
            }

            _deferReel = false;
            _state = state;
            if (_image == null)
                return;

            ReleaseLayers();
            _elapsed = 0f;
            _layerIndex = 0;
            var petId = PetArtLoader.CurrentOwnPetId();
            if (TryBindLayers(petId, state))
            {
                _petId = petId;
                _usingArt = true;
                _image.sprite = null;
                _image.color = new Color(1f, 1f, 1f, 0f);
                _image.preserveAspect = true;
                ShowLayer();
                return;
            }

            if (_layerHost != null)
                _layerHost.gameObject.SetActive(false);
            ReloadFrames();
            _image.preserveAspect = true;
            _image.color = _usingArt ? Color.white : TintFor(state);
            if (_frames != null && _frames.Length > 0)
                _image.sprite = _frames[0];
        }

        void OnDestroy()
        {
            ReleaseLayers();
        }

        bool TryBindLayers(string petId, PetVisualState state)
        {
            var wire = PetStateController.ToWire(state);
            var rect = _image.rectTransform.rect;
            var slotW = rect.width > 1f ? rect.width : _image.rectTransform.sizeDelta.x;
            var slotH = rect.height > 1f ? rect.height : _image.rectTransform.sizeDelta.y;
            int drawW;
            int drawH;
            float originX;
            float originY;
            FishingLayerArt.Place(petId, slotW, slotH, out drawW, out drawH, out originX, out originY);
            _layered = FishingLayerArt.Open(petId, wire, drawW, drawH);
            if (_layered == null)
                return false;

            EnsureLayerHost();
            LayoutLayers(drawW, drawH, originX, originY);
            _layerHost.gameObject.SetActive(true);
            return true;
        }

        void EnsureLayerHost()
        {
            if (_layerHost != null)
                return;

            var host = new GameObject("FishingLayers", typeof(RectTransform));
            host.transform.SetParent(_image.transform, false);
            _layerHost = host.GetComponent<RectTransform>();
            _layerHost.anchorMin = Vector2.zero;
            _layerHost.anchorMax = Vector2.one;
            _layerHost.offsetMin = Vector2.zero;
            _layerHost.offsetMax = Vector2.zero;
            _layerImages = new Image[FishingLayerArt.LayerNames.Length];
            for (var i = 0; i < _layerImages.Length; i++)
            {
                var child = new GameObject(FishingLayerArt.LayerNames[i], typeof(RectTransform), typeof(Image));
                child.transform.SetParent(_layerHost, false);
                var image = child.GetComponent<Image>();
                image.raycastTarget = false;
                image.maskable = false;
                image.preserveAspect = false;
                var rt = image.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = Vector2.zero;
                _layerImages[i] = image;
            }
        }

        void LayoutLayers(int drawW, int drawH, float originX, float originY)
        {
            var position = new Vector2(originX, -originY);
            for (var i = 0; i < _layerImages.Length; i++)
            {
                var rect = _layerImages[i].rectTransform;
                rect.sizeDelta = new Vector2(drawW, drawH);
                rect.anchoredPosition = position;
            }
        }

        void ShowLayer()
        {
            var count = _layered.FrameCount;
            if (count <= 0)
                return;
            var index = _layerIndex;
            if (index < 0 || index >= count)
                index = 0;
            var order = _layered.Orders[index];
            for (var z = 0; z < order.Length; z++)
            {
                var layer = IndexOf(FishingLayerArt.LayerNames, order[z]);
                if (layer < 0)
                    continue;
                var image = _layerImages[layer];
                image.sprite = _layered.Frames[index, layer];
                image.enabled = image.sprite != null;
                image.color = Color.white;
                image.transform.SetSiblingIndex(z);
            }
        }

        void ReleaseLayers()
        {
            if (_layered == null)
                return;
            FishingLayerArt.Release(_layered);
            _layered = null;
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
            if (_layered != null)
            {
                AdvanceLayers();
                return;
            }

            if (_image == null || _frames == null || _frames.Length < 2)
                return;
            if (_state == PetVisualState.Offline)
                return;

            var fps = IsOneShot(_state) ||
                      _state == PetVisualState.Hooked ||
                      _state == PetVisualState.Dragging
                ? _activeFps
                : _idleFps;
            _elapsed += Time.unscaledDeltaTime * fps;
            int index;
            if (IsOneShot(_state))
                index = Mathf.Min(Mathf.FloorToInt(_elapsed), _frames.Length - 1);
            else
                index = Mathf.FloorToInt(_elapsed) % _frames.Length;
            if (index < 0)
                index = 0;
            _image.sprite = _frames[index];
        }

        void AdvanceLayers()
        {
            var count = _layered.FrameCount;
            if (count <= 1 || _layered.Playback == "hold")
                return;

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < LayerFrameSeconds)
                return;
            _elapsed -= LayerFrameSeconds;

            if (_layered.Playback == "once")
            {
                if (_layerIndex >= count - 1)
                {
                    FlushDeferredVisual();
                    return;
                }

                _layerIndex++;
                ShowLayer();
                return;
            }

            if (_layered.Playback == "intro-loop" && count >= 10 &&
                _layered.LoopStart > 0 && _layered.LoopStart < count)
            {
                _layerIndex++;
                if (_layerIndex >= count)
                    _layerIndex = _layered.LoopStart;
                ShowLayer();
                return;
            }

            _layerIndex = (_layerIndex + 1) % count;
            ShowLayer();
        }

        bool ShouldFinishReel(PetVisualState state)
        {
            if (_layered == null || _layered.Playback != "once" || _state != PetVisualState.Reel)
                return false;
            if (_layerIndex >= _layered.FrameCount - 1)
                return false;
            return state == PetVisualState.Sit || state == PetVisualState.Idle;
        }

        void FlushDeferredVisual()
        {
            if (!_deferReel)
                return;
            var next = _deferred;
            _deferReel = false;
            Apply(next);
        }

        static int IndexOf(string[] names, string name)
        {
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i] == name)
                    return i;
            }

            return -1;
        }

        static bool IsOneShot(PetVisualState state)
        {
            return state == PetVisualState.Bait ||
                   state == PetVisualState.Cast ||
                   state == PetVisualState.Reel ||
                   state == PetVisualState.Catching;
        }

        static Color TintFor(PetVisualState state)
        {
            switch (state)
            {
                case PetVisualState.Sit:
                    return new Color(0.82f, 0.92f, 0.78f, 1f);
                case PetVisualState.Bait:
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
