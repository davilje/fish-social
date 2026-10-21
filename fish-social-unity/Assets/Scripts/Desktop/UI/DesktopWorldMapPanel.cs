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
        const float MapWidth = 2200f;
        const float MapHeight = 1300f;
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
        WorldMapPondView _selected;
        FishingProgressDto _progress;
        string _returnFeeMode;
        string _lastSelectedPondId;
        bool _waitingEnter;
        Button _modeSell;
        Button _modeAuto;
        Vector2 _dragStart;
        Vector2 _contentStart;
        float _zoom = 1f;
        bool _uiReady;

        public void Bind(SocialPondSessionController pond, IAuthenticatedApiClient api)
        {
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
            SetStatus(_pond == null ? "请先完成 Steam 登录。" : "点选地图上的鱼塘。");
            ClampContent();
        }

        public void OnOpened()
        {
            if (!_uiReady)
                return;
            if (_api == null || !_api.CanUse)
                return;
            StartCoroutine(LoadProgress());
        }

        public void EnterPond(string pondId)
        {
            if (!_uiReady || string.IsNullOrEmpty(pondId))
                return;
            WorldMapPondView pond;
            if (!_byId.TryGetValue(pondId, out pond))
            {
                SetStatus("未找到对应鱼塘，请重新选择。");
                return;
            }
            _lastSelectedPondId = null;
            Select(pond);
            EnterSelectedPond();
        }

        public void FocusPond(string pondId)
        {
            if (!_uiReady)
                return;
            WorldMapPondView selectedPond;
            if (string.IsNullOrEmpty(pondId) || !_byId.TryGetValue(pondId, out selectedPond))
            {
                SetStatus("未找到对应鱼塘，请重新选择。");
                return;
            }
            Select(selectedPond);
            _content.anchoredPosition = new Vector2(
                (0.5f - selectedPond.x) * MapWidth * _zoom,
                (selectedPond.y - 0.5f) * MapHeight * _zoom);
            ClampContent();
        }

        System.Collections.IEnumerator LoadProgress()
        {
            yield return _api.GetFishingProgress((ok, dto, _) =>
            {
                if (ok)
                    _progress = dto;
            });
            BuildMarkers();
            Select(_selected);
        }

        void EnsureUi()
        {
            _viewport = transform.Find("Viewport") as RectTransform;
            _content = transform.Find("Viewport/MapContent") as RectTransform;
            _markerLayer = transform.Find("Viewport/MapContent/MarkerLayer") as RectTransform;
            _details = Find<Text>("Details/DetailsText");
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
            _returnFeeMode = mode;
            RefreshFeeModeButtons();
            if (_selected != null)
            {
                var fee = mode == "auto_return" ? _selected.feePer2hAutoReturn : _selected.feePer2hSellOnly;
                SetStatus((mode == "auto_return" ? "已选回鱼档" : "已选出售档") +
                          "：每 2h " + fee + " 金币。再点确认进入。");
            }
            Select(_selected);
        }

        void RefreshFeeModeButtons()
        {
            var show = PondHasDualFee(_selected);
            if (_modeSell != null)
                _modeSell.gameObject.SetActive(show);
            if (_modeAuto != null)
                _modeAuto.gameObject.SetActive(show);
            if (!show)
                return;
            TintModeButton(_modeSell, _returnFeeMode == "sell_only");
            TintModeButton(_modeAuto, _returnFeeMode == "auto_return");
        }

        static void TintModeButton(Button button, bool selected)
        {
            if (button == null)
                return;
            DesktopModalUi.StyleButton(button);
            var visual = button.GetComponent<CozyButtonVisual>();
            if (visual != null)
                visual.SetSelected(selected);
        }

        static string PlaceLine(string pondId)
        {
            var ponds = DesktopGameData.Ponds;
            for (var i = 0; i < ponds.Length; i++)
            {
                var pond = ponds[i];
                if (pond == null || pond.pondId != pondId)
                    continue;
                var place = DesktopGameData.RegionLabel(pond.bioRegion);
                var water = DesktopGameData.WaterLabel(pond.waterType);
                if (!string.IsNullOrEmpty(water))
                    place = string.IsNullOrEmpty(place) ? water : place + " · " + water;
                if (!string.IsNullOrEmpty(pond.realWorldRef))
                    place = string.IsNullOrEmpty(place) ? pond.realWorldRef : place + " · " + pond.realWorldRef;
                return string.IsNullOrEmpty(place) ? "" : "\n" + place;
            }
            return "";
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

        static bool PondHasDualFee(WorldMapPondView pond)
        {
            return pond != null && pond.allowsAutoReturn && pond.feePer2hSellOnly > 0;
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
                instance.name = "Pond_" + item.pondId;
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
                marker.pivot = new Vector2(item.x > 0.82f ? 1f : item.x < 0.18f ? 0f : 0.5f, 0.5f);
                marker.sizeDelta = new Vector2(128f, 44f);
                marker.anchoredPosition = Vector2.zero;
                var captured = item;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Select(captured));
                label.text = item.displayName;
                label.alignment = TextAnchor.MiddleLeft;
                var labelRect = label.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 0.5f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.anchoredPosition = new Vector2(40f, 0f);
                labelRect.sizeDelta = new Vector2(82f, 36f);
                DesktopModalUi.StyleTree(instance.transform);
                DesktopModalUi.StyleButton(button, locked ? "lock.png" : "pond.png");
                button.GetComponent<CozyButtonVisual>()?.SetTint(DesktopGameData.CategoryColor(item.pondCategory, locked));
                if (!locked)
                    CozyMotion.Float(marker, PondPhase(item.pondId));
                _markersById[item.pondId] = button;
            }
            RefreshMarkerSelection();
        }

        void RefreshMarkerSelection()
        {
            foreach (var marker in _markersById)
            {
                var visual = marker.Value.GetComponent<CozyButtonVisual>();
                if (visual != null)
                    visual.SetSelected(_selected != null && marker.Key == _selected.pondId);
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
            _selected = item;
            RefreshMarkerSelection();
            if (item != null && _markersById.TryGetValue(item.pondId, out var selectedMarker) && selectedMarker != null)
                CozyMotion.Ripple(selectedMarker.transform as RectTransform);
            if (_selected == null)
            {
                _returnFeeMode = "sell_only";
                _lastSelectedPondId = null;
            }
            else if (!string.Equals(_lastSelectedPondId, _selected.pondId, StringComparison.Ordinal))
            {
                _lastSelectedPondId = _selected.pondId;
                _returnFeeMode = "sell_only";
            }
            RefreshFeeModeButtons();
            if (_selected == null)
            {
                _details.text = "暂无可用鱼塘坐标配置。";
                SetEnterLabel("进入鱼塘", false);
                return;
            }

            var locked = IsLocked(_selected);
            var reason = AccessReason(_selected);
            var feeLine = PondHasDualFee(_selected)
                ? "出售档：每 2h " + _selected.feePer2hSellOnly + " 金币（仅可卖）" +
                  "\n回鱼档：每 2h " + _selected.feePer2hAutoReturn + " 金币（达标自动回塘）"
                : _selected.feePer2h > 0
                    ? "每 2 小时扣费：" + _selected.feePer2h + " 金币"
                    : "入场费：免费";
            var today = _progress != null ? _progress.todayFeeCharges : 0;
            var maxFee = _selected.maxFeeChargesPerDay > 0 ? _selected.maxFeeChargesPerDay : 4;
            var activeFee = _returnFeeMode == "auto_return" && PondHasDualFee(_selected)
                ? _selected.feePer2hAutoReturn
                : (_selected.feePer2hSellOnly > 0 ? _selected.feePer2hSellOnly : _selected.feePer2h);
            if (activeFee > 0)
                feeLine += "\n今日已扣：" + today + " / " + maxFee + " 次";
            var levelLine = _selected.minPlayerLevel > 0
                ? "需要钓鱼等级 " + _selected.minPlayerLevel +
                  "（当前 " + (_progress != null ? _progress.level : 0) + "）"
                : "等级要求：无";

            _details.text = _selected.displayName +
                            "\n类型：" + DesktopGameData.CategoryLabel(_selected.pondCategory) +
                            PlaceLine(_selected.pondId) +
                            "\n状态：" + reason +
                            "\n" + levelLine +
                            "\n" + feeLine +
                            "\n鱼种：" + DesktopGameData.PondSpeciesSummary(_selected.pondId, 4);

            if (locked)
                SetEnterLabel("暂不可进入", false);
            else if (PondHasDualFee(_selected) && string.IsNullOrEmpty(_returnFeeMode))
                SetEnterLabel("请先选收费模式", false);
            else
                SetEnterLabel("进入鱼塘", true);
        }

        void EnterSelectedPond()
        {
            if (DesktopOnboardingController.Instance != null &&
                DesktopOnboardingController.Instance.IsOnboardingActive)
            {
                SetStatus("请先完成新手引导。");
                return;
            }
            if (_selected == null || _pond == null)
            {
                SetStatus("请选择有效鱼塘，或等待会话初始化。");
                return;
            }
            if (IsLocked(_selected))
            {
                SetStatus(AccessReason(_selected));
                return;
            }
            if (PondHasDualFee(_selected) && string.IsNullOrEmpty(_returnFeeMode))
            {
                SetStatus("请先选择「出售档」或「回鱼档」。");
                return;
            }
            _pond.SetPendingReturnFeeMode(string.IsNullOrEmpty(_returnFeeMode) ? "sell_only" : _returnFeeMode);
            SetStatus("正在进入 " + _selected.displayName + "…");
            _waitingEnter = true;
            if (_pond.State == SocialSocketState.Connected)
            {
                _pond.SwitchPond(_selected.pondId, (ok, message) =>
                {
                    SetStatus(message);
                    if (!ok)
                    {
                        _waitingEnter = false;
                        return;
                    }
                    OpenOverlay();
                    _waitingEnter = false;
                });
                return;
            }

            _pond.ConnectAndJoin(_selected.pondId);
        }

        void OnPondSnapshot(PondSnapshotDto _)
        {
            if (!_waitingEnter || _selected == null || _pond == null)
                return;
            if (!string.Equals(_pond.CurrentPondId, _selected.pondId, StringComparison.Ordinal))
                return;
            if (_pond.CurrentUser == null)
                return;
            _waitingEnter = false;
            OpenOverlay();
        }

        static void OpenOverlay()
        {
            DesktopAppBootstrap.Instance?.StartNativeOverlay();
            WindowManager.Instance?.HideToTray();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }

        bool IsLocked(WorldMapPondView pond)
        {
            if (pond == null)
                return true;
            if (!pond.isOpen || pond.pondCategory == "giant")
                return true;
            if (_progress != null && !_progress.onboardingCompleted)
                return true;
            if (pond.minPlayerLevel > 0 &&
                (_progress == null || _progress.level < pond.minPlayerLevel))
                return true;
            return false;
        }

        string AccessReason(WorldMapPondView pond)
        {
            if (pond.pondCategory == "giant" || !pond.isOpen)
                return "暂未开放";
            if (_progress != null && !_progress.onboardingCompleted)
                return "请先完成新手引导";
            if (pond.minPlayerLevel > 0 &&
                (_progress == null || _progress.level < pond.minPlayerLevel))
            {
                var current = _progress != null ? _progress.level : 0;
                return "需要钓鱼等级 " + pond.minPlayerLevel + "（当前 " + current + "）";
            }
            return "可进入";
        }

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
                text.text = label;
                text.color = enabled ? CatTheme.Cream : CatTheme.Ink;
            }
        }

        void OnPondStateChanged(SocialSocketState state, string message)
        {
            if (state == SocialSocketState.Connected)
                SetStatus("已连接，Overlay 正在显示鱼塘。");
            else if (state == SocialSocketState.Failed || state == SocialSocketState.Disconnected)
                SetStatus(string.IsNullOrEmpty(message) ? "鱼塘连接失败，可重试。" : message);
            else
                SetStatus(string.IsNullOrEmpty(message) ? "正在连接鱼塘…" : message);
            Select(_selected);
        }

        void OnPondError(string message)
        {
            _waitingEnter = false;
            SetStatus(string.IsNullOrEmpty(message) ? "进入失败，可重试。" : message);
        }

        void SetStatus(string message)
        {
            if (_status != null)
                _status.text = message ?? string.Empty;
        }

        public void ResetView()
        {
            if (!_uiReady)
                return;
            _zoom = 1f;
            _content.localScale = Vector3.one;
            _content.anchoredPosition = Vector2.zero;
            ClampContent();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!_uiReady)
                return;
            _dragStart = eventData.position;
            _contentStart = _content.anchoredPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_uiReady)
                return;
            _content.anchoredPosition = _contentStart + eventData.position - _dragStart;
            ClampContent();
        }

        public void OnEndDrag(PointerEventData eventData) { }

        public void OnScroll(PointerEventData eventData)
        {
            if (!_uiReady)
                return;
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
            var maxX = Mathf.Max(0f, (MapWidth * _zoom - size.x) * 0.5f);
            var maxY = Mathf.Max(0f, (MapHeight * _zoom - size.y) * 0.5f);
            var pos = _content.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, -maxX, maxX);
            pos.y = Mathf.Clamp(pos.y, -maxY, maxY);
            _content.anchoredPosition = pos;
        }

        void OnDestroy()
        {
            if (_pond == null)
                return;
            _pond.ErrorReceived -= OnPondError;
            _pond.StateChanged -= OnPondStateChanged;
            _pond.SnapshotChanged -= OnPondSnapshot;
        }
    }
}
