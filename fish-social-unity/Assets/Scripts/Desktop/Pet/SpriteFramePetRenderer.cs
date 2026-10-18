using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Pet
{
    /// <summary>
    /// Authoritative presentation requests with independent, duration-driven layered playback.
    /// </summary>
    public sealed class SpriteFramePetRenderer : MonoBehaviour, IPetRenderer
    {
        [SerializeField] Sprite[] _frames;
        [SerializeField] float _idleFps = 2f;
        [SerializeField] float _activeFps = 6f;

        Image _image;
        RectTransform _layerHost;
        Image[] _layerImages;
        FishingLayerArt.Session _layered;
        PetVisualState _state = PetVisualState.Offline;
        PetVisualState _deferred;
        bool _hasDeferred;
        bool _outcomeFlow;
        bool _successCompleted;
        bool _holdingCatch;
        bool _hasSeatPose;
        bool _hasApplied;
        bool _manualPlayback;
        bool _layerFinished;
        string _activeClip;
        string _petId;
        float _elapsed;
        int _layerIndex;
        bool _usingArt;
        bool _layoutInProgress;
        Vector2 _layerSlotSize;
        int _layerDrawWidth;
        int _layerDrawHeight;

        public string ActiveClip => _activeClip;
        public string ActiveSkinId => _petId;
        public int FrameIndex => _layerIndex;
        public bool ClipFinished => _layerFinished;
        public bool IsHoldingCatch => _holdingCatch;
        public PetVisualState RequestedState => _state;
        public FishingLayerArt.Session ActiveSession => _layered;
        public bool IsPlaying { get; set; } = true;

        public void Bind(Image target)
        {
            if (_image != target && _layerHost != null)
            {
                Destroy(_layerHost.gameObject);
                _layerHost = null;
                _layerImages = null;
            }
            ReleaseLayers();
            _image = target;
            _hasApplied = false;
            Apply(_state);
        }

        public void Apply(PetVisualState state)
        {
            var petId = PetArtLoader.CurrentOwnPetId();
            if (_hasApplied && !_manualPlayback && _state == state && _petId == petId)
                return;

            var previous = _state;
            _state = state;
            _hasApplied = true;
            if (!_manualPlayback && _successCompleted && _petId == petId && state == PetVisualState.Catching &&
                HoldCatch(petId))
                return;
            if (!_manualPlayback && _holdingCatch && _petId == petId && ShouldDefer(state))
            {
                _deferred = state;
                _hasDeferred = true;
                _holdingCatch = false;
                if (StartLayers(petId, "reset_catch"))
                    return;
                _hasDeferred = false;
                _outcomeFlow = false;
            }
            if (!_manualPlayback && _outcomeFlow && _petId == petId && ShouldDefer(state))
            {
                _deferred = state;
                _hasDeferred = true;
                return;
            }

            if (!_manualPlayback && _outcomeFlow && _petId == petId &&
                ((_activeClip == "reel_empty" || _activeClip == "reset_empty")
                    ? state == PetVisualState.EmptyReel
                    : state == PetVisualState.Reel || state == PetVisualState.Catching))
            {
                _hasDeferred = false;
                return;
            }

            _hasDeferred = false;
            _manualPlayback = false;
            _outcomeFlow = false;
            _holdingCatch = false;
            if (state != PetVisualState.Sit && state != PetVisualState.Catching)
                _successCompleted = false;
            if (_image == null)
                return;

            var clip = ClipFor(state, previous);
            if (StartLayers(petId, clip))
            {
                _outcomeFlow = IsOutcomeClip(clip);
                return;
            }

            ReleaseLayers();
            _elapsed = 0f;
            _layerIndex = 0;
            _hasSeatPose = state != PetVisualState.Idle && state != PetVisualState.Offline &&
                state != PetVisualState.Dragging;
            if (_layerHost != null)
                _layerHost.gameObject.SetActive(false);
            ReloadFrames();
            _image.preserveAspect = true;
            _image.color = _usingArt ? Color.white : TintFor(state);
            if (_frames != null && _frames.Length > 0)
                _image.sprite = _frames[0];
        }

        public bool PlayClip(string skinId, string clip)
        {
            if (_image == null || !StartLayers(skinId, clip))
                return false;
            _manualPlayback = true;
            _successCompleted = false;
            _holdingCatch = false;
            _hasDeferred = false;
            _outcomeFlow = IsOutcomeClip(clip);
            return true;
        }

        string ClipFor(PetVisualState state, PetVisualState previous)
        {
            switch (state)
            {
                case PetVisualState.Idle:
                    return _hasSeatPose ? "leave" : "idle";
                case PetVisualState.Sit:
                    return !_hasSeatPose && (previous == PetVisualState.Idle || previous == PetVisualState.Offline)
                        ? "sit" : "ready";
                case PetVisualState.Offline:
                case PetVisualState.Dragging:
                    return "idle";
                default:
                    return PetStateController.ToWire(state);
            }
        }

        static bool IsOutcomeClip(string clip)
        {
            return clip == "reel" || clip == "catch" || clip == "reset_catch" ||
                clip == "reel_empty" || clip == "reset_empty";
        }

        void OnDestroy()
        {
            ReleaseLayers();
        }

        Vector2 CurrentSlotSize()
        {
            var rect = _image.rectTransform.rect;
            return new Vector2(
                rect.width > 1f ? rect.width : _image.rectTransform.sizeDelta.x,
                rect.height > 1f ? rect.height : _image.rectTransform.sizeDelta.y);
        }

        void OnRectTransformDimensionsChange()
        {
            if (_layoutInProgress || _image == null || _layered == null ||
                _layerHost == null || _layerImages == null)
                return;
            var slotSize = CurrentSlotSize();
            if (slotSize.x < 1f || slotSize.y < 1f || float.IsNaN(slotSize.x) || float.IsNaN(slotSize.y) ||
                float.IsInfinity(slotSize.x) || float.IsInfinity(slotSize.y) || slotSize == _layerSlotSize)
                return;

            _layoutInProgress = true;
            try
            {
                FishingLayerArt.Place(_petId, slotSize.x, slotSize.y,
                    out var drawWidth, out var drawHeight, out var originX, out var originY);
                if (drawWidth != _layerDrawWidth || drawHeight != _layerDrawHeight)
                {
                    var resized = FishingLayerArt.Open(_petId, _activeClip, drawWidth, drawHeight);
                    if (resized != null)
                    {
                        var previous = _layered;
                        _layered = resized;
                        _layerDrawWidth = drawWidth;
                        _layerDrawHeight = drawHeight;
                        FishingLayerArt.Release(previous);
                    }
                }
                _layerSlotSize = slotSize;
                LayoutLayers(drawWidth, drawHeight, originX, originY);
                ShowLayer();
            }
            finally
            {
                _layoutInProgress = false;
            }
        }

        bool StartLayers(string petId, string clip)
        {
            var slotSize = CurrentSlotSize();
            int drawW;
            int drawH;
            float originX;
            float originY;
            FishingLayerArt.Place(petId, slotSize.x, slotSize.y, out drawW, out drawH, out originX, out originY);
            var session = FishingLayerArt.Open(petId, clip, drawW, drawH);
            if (session == null)
                return false;

            ReleaseLayers();
            _layered = session;
            _activeClip = clip;
            _layerFinished = false;
            _petId = petId;
            _usingArt = true;
            _elapsed = 0f;
            _layerIndex = 0;
            _hasSeatPose = clip != "idle";
            _layoutInProgress = true;
            try
            {
                _layerSlotSize = slotSize;
                _layerDrawWidth = drawW;
                _layerDrawHeight = drawH;
                EnsureLayerHost();
                LayoutLayers(drawW, drawH, originX, originY);
                _layerHost.gameObject.SetActive(true);
                _image.sprite = null;
                _image.color = new Color(1f, 1f, 1f, 0f);
                _image.preserveAspect = true;
                ShowLayer();
            }
            finally
            {
                _layoutInProgress = false;
            }
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
            var order = _layered.Order(index);
            for (var depth = 0; depth < order.Length; depth++)
            {
                var layer = IndexOf(FishingLayerArt.LayerNames, order[depth]);
                if (layer < 0)
                    continue;
                var image = _layerImages[layer];
                image.sprite = _layered.Frame(index, layer);
                image.enabled = image.sprite != null;
                image.color = Color.white;
                image.transform.SetSiblingIndex(depth);
            }
        }

        void ReleaseLayers()
        {
            if (_layered == null)
                return;
            FishingLayerArt.Release(_layered);
            _layered = null;
            _activeClip = null;
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
            if (IsPlaying)
                Tick(Time.unscaledDeltaTime);
        }

        public void Tick(float deltaSeconds)
        {
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0f)
                return;
            if (_layered != null)
            {
                AdvanceLayers(deltaSeconds);
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
            _elapsed += deltaSeconds * fps;
            int index;
            if (IsOneShot(_state))
                index = Mathf.Min(Mathf.FloorToInt(_elapsed), _frames.Length - 1);
            else
                index = Mathf.FloorToInt(_elapsed) % _frames.Length;
            if (index < 0)
                index = 0;
            _image.sprite = _frames[index];
        }

        void AdvanceLayers(float deltaSeconds)
        {
            if (_layerFinished || _layered.Playback == "hold")
                return;

            _elapsed += deltaSeconds;
            while (_layered != null && !_layerFinished && _layered.Playback != "hold" &&
                _elapsed >= _layered.DurationMs(_layerIndex) / 1000f)
            {
                _elapsed -= _layered.DurationMs(_layerIndex) / 1000f;
                MoveNextLayer();
            }
        }

        public void StepFrame()
        {
            if (_layered == null || _layerFinished || _layered.Playback == "hold")
                return;
            _elapsed = 0f;
            MoveNextLayer();
        }

        void MoveNextLayer()
        {
            if (_layerIndex + 1 < _layered.FrameCount)
            {
                _layerIndex++;
                ShowLayer();
                return;
            }

            if (_layered.Playback == "intro-loop")
            {
                _layerIndex = _layered.LoopStart;
                ShowLayer();
                return;
            }

            if (!_manualPlayback && _state == PetVisualState.Catching &&
                (_activeClip == "catch" || _activeClip == "reset_catch") && HoldCatch(_petId))
                return;

            var nextClip = _layered.NextClip;
            if (nextClip != null)
            {
                var remaining = _elapsed;
                var completedSuccess = _activeClip == "reset_catch";
                if (StartLayers(_petId, nextClip))
                {
                    _elapsed = remaining;
                    if (_outcomeFlow && !IsOutcomeClip(nextClip))
                    {
                        _successCompleted = completedSuccess;
                        _outcomeFlow = false;
                        FlushDeferredVisual();
                    }
                    return;
                }
            }

            _layerFinished = true;
            _outcomeFlow = false;
            FlushDeferredVisual();
        }

        bool HoldCatch(string skinId)
        {
            if (_activeClip != "catch" && !StartLayers(skinId, "catch"))
                return false;
            _layerIndex = _layered.FrameCount - 1;
            _elapsed = 0f;
            _layerFinished = true;
            _holdingCatch = true;
            _outcomeFlow = true;
            _hasDeferred = false;
            ShowLayer();
            return true;
        }

        void FlushDeferredVisual()
        {
            if (!_hasDeferred)
                return;
            var next = _deferred;
            _hasDeferred = false;
            _hasApplied = false;
            Apply(next);
        }

        static bool ShouldDefer(PetVisualState state)
        {
            return state == PetVisualState.Sit || state == PetVisualState.Idle ||
                state == PetVisualState.Bait || state == PetVisualState.Cast ||
                state == PetVisualState.Fishing || state == PetVisualState.Groundbait;
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
                   state == PetVisualState.EmptyReel ||
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
                case PetVisualState.EmptyReel:
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
