using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Onboarding;

namespace FishSocial.Desktop
{
    public sealed class DesktopWorldMapPanel : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        float MapWidth => _content != null ? _content.rect.width : 1050f;
        float MapHeight => _content != null ? _content.rect.height : 700f;
        const float MinZoom = 0.65f;
        const float MaxZoom = 1.8f;

        RectTransform _viewport;
        RectTransform _content;
        RectTransform _markerLayer;
        Text _details;
        Text _status;
        Button _enter;
        Button _reset;
        SocialPondSessionController _pond;
        IAuthenticatedApiClient _api;
        WorldMapPondView[] _ponds = new WorldMapPondView[0];
        readonly Dictionary<string, WorldMapPondView> _byId =
            new Dictionary<string, WorldMapPondView>(StringComparer.Ordinal);
        readonly Dictionary<string, Button> _markersById =
            new Dictionary<string, Button>(StringComparer.Ordinal);
        public PondSelectionState SelectionState { get; } = new PondSelectionState();
        WorldMapPondView _selected => SelectionState.Selected;
        string _enteringPondId;
        bool _waitingEnter;
        Button _modeSell;
        Button _modeAuto;
        Vector2 _dragStart;
        Vector2 _contentStart;
        float _zoom = 1f;
        bool _uiReady;
        bool _dragging;

        public void Bind(SocialPondSessionController pond, IAuthenticatedApiClient api)
        {
            if (_pond != null)
            {
                _pond.ErrorReceived -= OnPondError;
                _pond.StateChanged -= OnPondStateChanged;
                _pond.SnapshotChanged -= OnPondSnapshot;
            }
            SelectionState.Changed -= RefreshSelection;
            SelectionState.Bind(api);
            SelectionState.Changed += RefreshSelection;
            _pond = pond;
            _api = api;
            EnsureUi();
            if (!_uiReady)
                return;
            _ponds = WorldMapPondCatalog.LoadVisible();
            _byId.Clear();
            for (var pondIndex = 0; pondIndex < _ponds.Length; pondIndex++)
                if (_ponds[pondIndex] != null && !string.IsNullOrEmpty(_ponds[pondIndex].pondId))
                    _byId[_ponds[pondIndex].pondId] = _ponds[pondIndex];
            if (_pond != null)
            {
                _pond.ErrorReceived += OnPondError;
                _pond.StateChanged += OnPondStateChanged;
                _pond.SnapshotChanged += OnPondSnapshot;
            }
            BuildMarkers();
            SelectById(_pond != null ? _pond.CurrentPondId : null);
            SetStatus(_pond == null ? DesktopLocalization.Text("ui.map.world_map.error.sign_in_required", "请先完成 Steam 登录。") : DesktopLocalization.Text("ui.map.world_map.hint.select_pond", "点选地图上的鱼塘。"));
            ClampContent();
        }

        public void OnOpened()
        {
            if (!_uiReady)
                return;
            StartCoroutine(SelectionState.LoadProgress(this));
            RefreshMarkerSelection();
            PondDetailView.RevealPhoto(transform.Find("Details"));
        }

        public void EnterPond(string pondId)
        {
            if (!_uiReady || string.IsNullOrEmpty(pondId))
                return;
            WorldMapPondView pond;
            if (!_byId.TryGetValue(pondId, out pond))
            {
                SetStatus(DesktopLocalization.Text("ui.map.world_map.error.pond_not_found", "未找到对应鱼塘，请重新选择。"));
                return;
            }
            if (SelectionState.Busy) return;
            Select(pond);
            EnterSelectedPond();
        }

        public void FocusPond(string pondId)
        {
            if (!_uiReady || SelectionState.Busy)
                return;
            WorldMapPondView selectedPond;
            if (string.IsNullOrEmpty(pondId) || !_byId.TryGetValue(pondId, out selectedPond))
            {
                SetStatus(DesktopLocalization.Text("ui.map.world_map.error.pond_not_found", "未找到对应鱼塘，请重新选择。"));
                return;
            }
            var sameSelection = _selected != null && _selected.pondId == selectedPond.pondId;
            Select(selectedPond);
            if(sameSelection)PondDetailView.RevealPhoto(transform.Find("Details"));
            CozyMotion.Cancel(_content);
            var start = _content.anchoredPosition;
            _zoom = 1.15f;
            _content.localScale = new Vector3(_zoom, _zoom, 1);
            _content.anchoredPosition = new Vector2(
                _viewport.rect.width * .5f - selectedPond.x * MapWidth * _zoom,
                selectedPond.y * MapHeight * _zoom - _viewport.rect.height * .5f);
            ClampContent();
            var destination = _content.anchoredPosition;
            _content.anchoredPosition = start;
            CozyMotion.MoveTo(_content, destination, .35f);
        }

        void EnsureUi()
        {
            _viewport = transform.Find("Viewport") as RectTransform;
            _content = transform.Find("Viewport/MapContent") as RectTransform;
            _markerLayer = transform.Find("Viewport/MapContent/MarkerLayer") as RectTransform;
            _details = Find<Text>("Details/FactsScroll/DetailsText") ?? Find<Text>("Details/DetailsText");
            _status = Find<Text>("Details/StatusText");
            _enter = Find<Button>("Details/EnterPond");
            _reset = Find<Button>("Details/ResetView");
            if (_viewport == null || _content == null || _markerLayer == null ||
                _details == null || _status == null || _enter == null || _reset == null ||
                Find<Image>("Viewport/MapContent/MapImage") == null ||
                Find<Button>("Details/ModeSell") == null || Find<Button>("Details/ModeAuto") == null)
            {
                _uiReady = false;
                Debug.LogError("[DesktopUI] PanelWorldMap requires Viewport/MapContent/{MapImage,MarkerLayer} and Details/{DetailsText,StatusText,EnterPond,ResetView,ModeSell,ModeAuto}.", this);
                return;
            }
            _uiReady = true;
            _enter.onClick.RemoveAllListeners();
            _enter.onClick.AddListener(EnterSelectedPond);
            _reset.onClick.RemoveAllListeners();
            _reset.onClick.AddListener(ResetView);
            DesktopModalUi.BindButton(transform, "Details/Retry", Retry);
            EnsureFeeModeButtons(_enter != null ? _enter.transform.parent : transform);
            ApplyCozyStyle();
        }

        void EnsureFeeModeButtons(Transform parent)
        {
            if (parent == null)
                return;
            _modeSell = DesktopModalUi.FindComponent<Button>(parent, "ModeSell");
            _modeAuto = DesktopModalUi.FindComponent<Button>(parent, "ModeAuto");
            DesktopModalUi.BindButton(parent, "ModeSell", () => SelectReturnFeeMode("sell_only"));
            DesktopModalUi.BindButton(parent, "ModeAuto", () => SelectReturnFeeMode("auto_return"));
            RefreshFeeModeButtons();
        }

        void SelectReturnFeeMode(string mode)
        {
            SelectionState.SelectFeeMode(mode);
        }

        void RefreshFeeModeButtons()
        {
            PondDetailView.Refresh(transform.Find("Details"), SelectionState, "DetailsText");
        }

        static float PondPhase(string pondId)
        {
            var phase = 0.15f;
            if (string.IsNullOrEmpty(pondId))
                return phase;
            for (var i = 0; i < pondId.Length; i++)
                phase += pondId[i] * 0.017f;
            return phase - Mathf.Floor(phase);
        }

        T Find<T>(string path) where T : Component
        {
            var node = transform.Find(path);
            return node == null ? null : node.GetComponent<T>();
        }

        void ApplyCozyStyle()
        {
            DesktopModalUi.StyleTree(transform);
            var backdrop = Find<Image>("Viewport/MapContent/MapImage");
            var sprite = CatTheme.LoadCozy("scenes/world-map.png", Vector4.zero);
            if (backdrop != null && sprite != null)
            {
                backdrop.overrideSprite = null;
                backdrop.sprite = sprite;
                // Keep the map and normalized marker layer in the artwork's native aspect ratio.
                _content.sizeDelta = new Vector2(700f * sprite.rect.width / sprite.rect.height, 700f);
            }
            DesktopModalUi.StyleButton(_enter, "pond.png", true);
            DesktopModalUi.StyleButton(_reset, "map.png");
            RefreshFeeModeButtons();
        }

        void BuildMarkers()
        {
            if (!_uiReady)
                return;
            _markersById.Clear();
            for (var markerIndex = _markerLayer.childCount - 1; markerIndex >= 0; markerIndex--)
            {
                var oldMarker = _markerLayer.GetChild(markerIndex).gameObject;
                oldMarker.SetActive(false);
                Destroy(oldMarker);
            }
            foreach (var item in _ponds)
            {
                if (item == null || string.IsNullOrEmpty(item.pondId))
                    continue;
                var locked = IsLocked(item);
                var instance = DesktopUiPrefabFactory.Instantiate("WorldMapPondMarker", _markerLayer);
                if (instance == null)
                    continue;
                instance.name = DesktopLocalization.Concat("Pond_", item.pondId);
                var marker = instance.GetComponent<RectTransform>();
                var button = instance.GetComponent<Button>();
                var label = DesktopModalUi.FindComponent<Text>(instance.transform, "Label");
                var icon = DesktopModalUi.FindComponent<Image>(instance.transform, "Icon");
                if (marker == null || button == null || label == null || icon == null)
                {
                    Debug.LogError("[DesktopUI] WorldMapPondMarker requires root RectTransform/Button, Label Text and Icon Image.", instance);
                    instance.SetActive(false);
                    Destroy(instance);
                    continue;
                }
                marker.anchorMin = new Vector2(item.x, 1f - item.y);
                marker.anchorMax = marker.anchorMin;
                marker.pivot = new Vector2(.5f, .5f);
                marker.anchoredPosition = Vector2.zero;
                var captured = item;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => FocusPond(captured.pondId));
                var markerView = instance.GetComponent<CozyPondMarkerView>() ?? instance.AddComponent<CozyPondMarkerView>();
                DesktopModalUi.StyleTree(instance.transform);
                markerView.Bind(item.displayName);
                markerView.SetState(false, locked, CozyPondMarkerView.CategoryColor(item.pondCategory));
                _markersById[item.pondId] = button;
            }
            RefreshMarkerSelection();
        }

        void RefreshMarkerSelection()
        {
            foreach (var marker in _markersById)
            {
                var selected = _selected != null && marker.Key == _selected.pondId;
                var view = marker.Value.GetComponent<CozyPondMarkerView>();
                if (view == null) continue;
                var pond = _byId[marker.Key];
                var locked = IsLocked(pond);
                view.SetState(selected, locked, CozyPondMarkerView.CategoryColor(pond.pondCategory));
                view.SetEntering(selected && SelectionState.Busy);
                view.SetFloating(selected && !locked && !SelectionState.Busy && isActiveAndEnabled, PondPhase(marker.Key));
            }
        }

        void SelectById(string pondId)
        {
            WorldMapPondView item;
            if (!string.IsNullOrEmpty(pondId) && _byId.TryGetValue(pondId, out item))
                Select(item);
            else
                Select(_ponds.Length > 0 ? _ponds[0] : null);
        }

        void Select(WorldMapPondView item)
        {
            if (SelectionState.Busy) return;
            var changed = _selected == null || item == null || _selected.pondId != item.pondId;
            SelectionState.Select(item);
            if (changed && item != null && _markersById.TryGetValue(item.pondId, out var marker) && marker != null)
                CozyMotion.Ripple(marker.transform as RectTransform);
            if (changed)
                PondDetailView.RevealPhoto(transform.Find("Details"));
        }

        void RefreshSelection()
        {
            if (!_uiReady) return;
            RefreshMarkerSelection();
            PondDetailView.Refresh(transform.Find("Details"), SelectionState, "DetailsText");
            if (_selected == null)
                SetEnterLabel(DesktopLocalization.Text("ui.map.world_map.hint.pond_required", "请选择鱼塘"), false);
            else if (SelectionState.Busy)
                SetEnterLabel(DesktopLocalization.Text("ui.map.pond_entry.button.entering", "正在进入…"), false);
            else
                SetEnterLabel(IsLocked(_selected) ? DesktopLocalization.Text("ui.map.pond_entry.button.unavailable", "暂不可进入") : DesktopLocalization.Text("ui.map.pond_entry.button.enter", "进入鱼塘"), !IsLocked(_selected));
        }

        void EnterSelectedPond()
        {
            if (SelectionState.Busy || _waitingEnter) return;
            if (DesktopOnboardingController.Instance != null &&
                DesktopOnboardingController.Instance.IsOnboardingActive)
            {
                SetStatus(DesktopLocalization.Text("ui.map.world_map.error.tutorial_required", "请先完成新手引导。"));
                return;
            }
            if (_selected == null || _pond == null)
            {
                SetStatus(DesktopLocalization.Text("ui.map.world_map.error.selection_not_ready", "请选择有效鱼塘，或等待会话初始化。"));
                return;
            }
            if (IsLocked(_selected))
            {
                SetStatus(AccessReason(_selected));
                return;
            }
            _pond.SetPendingReturnFeeMode(SelectionState.ReturnFeeMode);
            _enteringPondId = _selected.pondId;
            _waitingEnter = true;
            SelectionState.SetEntryStatus(true, DesktopLocalization.Format("ui.map.world_map.status.entering_named_pond", (object)(_selected.displayName)));
            if (_pond.State == SocialSocketState.Connected)
            {
                _pond.SwitchPond(_enteringPondId, (ok, message) =>
                {
                    if (this == null || !_waitingEnter) return;
                    _waitingEnter = false;
                    SelectionState.SetEntryStatus(false, message, !ok);
                    if (ok && string.Equals(_pond.CurrentPondId, _enteringPondId, StringComparison.Ordinal))
                        OpenOverlay();
                });
                return;
            }

            _pond.ConnectAndJoin(_enteringPondId);
        }

        void OnPondSnapshot(PondSnapshotDto snapshot)
        {
            if (snapshot?.pond == null || !string.Equals(snapshot.pond.id, _enteringPondId, StringComparison.Ordinal)) return;
            if (!_waitingEnter || string.IsNullOrEmpty(_enteringPondId) || _pond == null)
                return;
            if (!string.Equals(_pond.CurrentPondId, _enteringPondId, StringComparison.Ordinal))
                return;
            if (_pond.CurrentUser == null)
                return;
            _waitingEnter = false;
            SelectionState.SetEntryStatus(false, DesktopLocalization.Text("ui.map.world_map.status.entered", "已进入鱼塘。"));
            OpenOverlay();
        }

        static void OpenOverlay()
        {
            DesktopAppBootstrap.Instance?.StartNativeOverlay();
            WindowManager.Instance?.HideToTray();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }

        bool IsLocked(WorldMapPondView pond) => !SelectionState.CanEnter(pond);

        string AccessReason(WorldMapPondView pond) => SelectionState.AccessReason(pond);

        void SetEnterLabel(string label, bool enabled)
        {
            if (_enter == null)
                return;
            _enter.interactable = enabled && _pond != null;
            var colors = _enter.colors;
            colors.disabledColor = new Color(0.93f, 0.86f, 0.74f, 1f);
            _enter.colors = colors;
            var text = _enter.GetComponentInChildren<Text>();
            if (text != null)
            {
                DesktopLocalization.SetText(text, label);
                text.color = enabled ? CatTheme.Cream : CatTheme.Ink;
                _enter.GetComponent<CozyJournalInlineContent>()?.Reflow();
            }
        }

        void OnPondStateChanged(SocialSocketState state, string message)
        {
            if (state == SocialSocketState.Failed || state == SocialSocketState.Disconnected)
            {
                var failedEntry = _waitingEnter;
                _waitingEnter = false;
                SelectionState.SetEntryStatus(false, string.IsNullOrEmpty(message) ? DesktopLocalization.Text("ui.map.world_map.error.connection_failed", "鱼塘连接失败，可重试。") : message, failedEntry);
            }
            else if (_waitingEnter)
                SetStatus(string.IsNullOrEmpty(message) ? DesktopLocalization.Text("ui.map.world_map.status.awaiting_confirmation", "正在等待鱼塘确认…") : message);
            RefreshSelection();
        }

        void OnPondError(string message)
        {
            _waitingEnter = false;
            SelectionState.SetEntryStatus(false, string.IsNullOrEmpty(message) ? DesktopLocalization.Text("ui.map.world_map.error.entry_failed", "进入失败，可重试。") : message, true);
        }

        void SetStatus(string message)
        { message = DesktopLocalization.Message(message);
            SelectionState.SetEntryStatus(SelectionState.Busy, message);
        }

        void Retry()
        {
            if (SelectionState.Busy) return;
            if (SelectionState.EntryFailed && SelectionState.Progress != null) EnterSelectedPond();
            else StartCoroutine(SelectionState.LoadProgress(this));
        }

        public void ResetView()
        {
            if (!_uiReady)
                return;
            CozyMotion.Cancel(_content);
            _zoom = 1f;
            _content.localScale = Vector3.one;
            _content.anchoredPosition = new Vector2(-180, 140);
            ClampContent();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_uiReady)
                return;
            if (eventData.pointerPress != null && eventData.pointerPress.GetComponentInParent<CozyPondMarkerView>() != null) return;
            _dragging = RectTransformUtility.RectangleContainsScreenPoint(_viewport, eventData.position, eventData.pressEventCamera);
            if (!_dragging) return;
            CozyMotion.Cancel(_content);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position,
                eventData.pressEventCamera, out _dragStart);
            _contentStart = _content.anchoredPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_uiReady || !_dragging)
                return;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport, eventData.position,
                eventData.pressEventCamera, out local);
            _content.anchoredPosition = _contentStart + local - _dragStart;
            ClampContent();
        }

        public void OnEndDrag(PointerEventData eventData) { _dragging = false; }

        public void OnScroll(PointerEventData eventData)
        {
            if (!_uiReady || Mathf.Approximately(eventData.scrollDelta.y, 0f) ||
                !RectTransformUtility.RectangleContainsScreenPoint(_viewport, eventData.position, eventData.enterEventCamera))
                return;
            CozyMotion.Cancel(_content);
            _zoom = Mathf.Clamp(_zoom + (eventData.scrollDelta.y > 0f ? 0.1f : -0.1f),
                MinZoom, MaxZoom);
            _content.localScale = new Vector3(_zoom, _zoom, 1f);
            ClampContent();
        }

        void ClampContent()
        {
            if (_viewport == null || _content == null)
                return;
            var size = _viewport.rect.size;
            // Never zoom below cover: the old .65 minimum exposed 7.5 px at the right edge.
            var minimum = Mathf.Max(MinZoom, Mathf.Max(size.x / MapWidth, size.y / MapHeight));
            _zoom = Mathf.Clamp(_zoom, minimum, MaxZoom);
            _content.localScale = new Vector3(_zoom, _zoom, 1);
            var maxX = Mathf.Max(0f, MapWidth * _zoom - size.x);
            var maxY = Mathf.Max(0f, MapHeight * _zoom - size.y);
            var pos = _content.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, -maxX, 0);
            pos.y = Mathf.Clamp(pos.y, 0, maxY);
            _content.anchoredPosition = pos;
        }

        void OnDisable()
        {
            SelectionState.CancelProgress(this);
            foreach (var marker in _markersById.Values)
                if (marker != null) CozyMotion.StopFloat(marker.GetComponent<CozyPondMarkerView>()?.FloatTarget);
        }

        void OnDestroy()
        {
            SelectionState.Changed -= RefreshSelection;
            if (_pond == null)
                return;
            _pond.ErrorReceived -= OnPondError;
            _pond.StateChanged -= OnPondStateChanged;
            _pond.SnapshotChanged -= OnPondSnapshot;
        }
    }
}
