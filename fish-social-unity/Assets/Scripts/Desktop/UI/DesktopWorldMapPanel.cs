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
        string _feeConfirmPondId;
        string _returnFeeMode;
        string _lastSelectedPondId;
        bool _waitingEnter;
        Button _modeSell;
        Button _modeAuto;
        Vector2 _dragStart;
        Vector2 _contentStart;
        float _zoom = 1f;

        public void Bind(SocialPondSessionController pond, IAuthenticatedApiClient api)
        {
            _pond = pond;
            _api = api;
            EnsureUi();
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
            SetStatus(_pond == null ? "请先完成 Steam 登录。" :
                "颜色表示鱼塘类型；灰色为未开放或未解锁。");
            ClampContent();
        }

        public void OnOpened()
        {
            if (_api == null || !_api.CanUse)
                return;
            StartCoroutine(LoadProgress());
        }

        public void FocusPond(string pondId)
        {
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
                _details == null || _status == null || _enter == null || _reset == null)
                BuildFallbackUi();
            _enter.onClick.RemoveAllListeners();
            _enter.onClick.AddListener(EnterSelectedPond);
            _reset.onClick.RemoveAllListeners();
            _reset.onClick.AddListener(ResetView);
            EnsureFeeModeButtons(_enter != null ? _enter.transform.parent : transform);
            ApplyCozyStyle();
        }

        void EnsureFeeModeButtons(Transform parent)
        {
            if (parent == null || _modeSell != null)
                return;
            _modeSell = EnsureButton(parent, "ModeSell", "出售档（仅卖）");
            _modeAuto = EnsureButton(parent, "ModeAuto", "回鱼档（自动）");
            var sellRect = _modeSell.GetComponent<RectTransform>();
            var autoRect = _modeAuto.GetComponent<RectTransform>();
            if (sellRect != null)
            {
                sellRect.anchorMin = new Vector2(0.02f, 0.21f);
                sellRect.anchorMax = new Vector2(0.49f, 0.28f);
                sellRect.offsetMin = Vector2.zero;
                sellRect.offsetMax = Vector2.zero;
            }
            if (autoRect != null)
            {
                autoRect.anchorMin = new Vector2(0.51f, 0.21f);
                autoRect.anchorMax = new Vector2(0.98f, 0.28f);
                autoRect.offsetMin = Vector2.zero;
                autoRect.offsetMax = Vector2.zero;
            }
            _modeSell.onClick.AddListener(() => SelectReturnFeeMode("sell_only"));
            _modeAuto.onClick.AddListener(() => SelectReturnFeeMode("auto_return"));
            RefreshFeeModeButtons();
        }

        void SelectReturnFeeMode(string mode)
        {
            _returnFeeMode = mode;
            _feeConfirmPondId = null;
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
            CatTheme.StyleButton(button);
            var visual = button.GetComponent<CozyButtonVisual>();
            if (visual != null)
                visual.SetSelected(selected);
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

        void BuildFallbackUi()
        {
            _viewport = EnsureRect(transform, "Viewport");
            Layout(_viewport, Vector2.zero, new Vector2(0.68f, 1f));
            var viewportImage = _viewport.GetComponent<Image>() ?? _viewport.gameObject.AddComponent<Image>();
            viewportImage.color = CatTheme.Cream;
            if (_viewport.GetComponent<RectMask2D>() == null && _viewport.GetComponent<Mask>() == null)
                _viewport.gameObject.AddComponent<RectMask2D>();

            _content = EnsureRect(_viewport, "MapContent");
            _content.anchorMin = new Vector2(0.5f, 0.5f);
            _content.anchorMax = _content.anchorMin;
            _content.pivot = new Vector2(0.5f, 0.5f);
            _content.sizeDelta = new Vector2(MapWidth, MapHeight);
            var mapImage = EnsureRect(_content, "MapImage");
            Layout(mapImage, Vector2.zero, Vector2.one);
            if (mapImage.GetComponent<Image>() == null)
                mapImage.gameObject.AddComponent<Image>();
            _markerLayer = EnsureRect(_content, "MarkerLayer");
            Layout(_markerLayer, Vector2.zero, Vector2.one);

            var detailsPanel = EnsureRect(transform, "Details");
            Layout(detailsPanel, new Vector2(0.68f, 0f), Vector2.one);
            if (detailsPanel.GetComponent<Image>() == null)
                detailsPanel.gameObject.AddComponent<Image>();
            _details = EnsureText(detailsPanel, "DetailsText", "请选择地图上的鱼塘。");
            _status = EnsureText(detailsPanel, "StatusText", string.Empty);
            _enter = EnsureButton(detailsPanel, "EnterPond", "进入鱼塘");
            _reset = EnsureButton(detailsPanel, "ResetView", "重置地图");
        }

        void ApplyCozyStyle()
        {
            var background = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            CatTheme.ApplyPaper(background);
            var viewportImage = _viewport.GetComponent<Image>();
            if (viewportImage != null)
                viewportImage.color = CatTheme.Cream;
            var backdropNode = _content.Find("MapImage");
            var backdrop = backdropNode != null ? backdropNode.GetComponent<Image>() : _content.GetComponent<Image>();
            if (backdrop == null)
            {
                var backdropRect = EnsureRect(_content, "MapImage");
                Layout(backdropRect, Vector2.zero, Vector2.one);
                backdrop = backdropRect.gameObject.AddComponent<Image>();
            }
            backdrop.transform.SetAsFirstSibling();
            backdrop.overrideSprite = null;
            backdrop.sprite = CatTheme.LoadCozy("scenes/world-map.png", Vector4.zero);
            backdrop.type = Image.Type.Simple;
            backdrop.preserveAspect = false;
            backdrop.color = backdrop.sprite != null ? Color.white : CatTheme.Cream;
            backdrop.raycastTarget = true;
            _markerLayer.SetAsLastSibling();

            var detailsPanel = _details.transform.parent;
            var detailsImage = detailsPanel.GetComponent<Image>() ?? detailsPanel.gameObject.AddComponent<Image>();
            detailsImage.color = CatTheme.Cream;
            CatTheme.ApplyPaper(detailsImage);
            foreach (var label in detailsPanel.GetComponentsInChildren<Text>(true))
            {
                label.color = CatTheme.Ink;
                if (CatTheme.Font != null)
                    label.font = CatTheme.Font;
                label.raycastTarget = false;
            }
            Layout(_details.rectTransform, new Vector2(0.06f, 0.41f), new Vector2(0.94f, 0.96f));
            _details.alignment = TextAnchor.UpperLeft;
            _details.horizontalOverflow = HorizontalWrapMode.Wrap;
            _details.verticalOverflow = VerticalWrapMode.Truncate;
            _details.resizeTextForBestFit = true;
            _details.resizeTextMinSize = 14;
            _details.resizeTextMaxSize = 18;
            Layout(_status.rectTransform, new Vector2(0.06f, 0.29f), new Vector2(0.94f, 0.4f));
            _status.fontSize = 16;
            _status.resizeTextForBestFit = true;
            _status.resizeTextMinSize = 14;
            _status.resizeTextMaxSize = 16;
            _status.alignment = TextAnchor.UpperLeft;
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            Layout(_enter.GetComponent<RectTransform>(), new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.2f));
            Layout(_reset.GetComponent<RectTransform>(), new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.1f));
            CatTheme.StyleButton(_enter, "pond.png", true);
            CatTheme.StyleButton(_reset, "map.png");
            CatTheme.StyleButton(_modeSell);
            CatTheme.StyleButton(_modeAuto);
            RefreshFeeModeButtons();
        }

        static RectTransform EnsureRect(Transform parent, string name)
        {
            return parent.Find(name) as RectTransform ?? NewRect(name, parent);
        }

        static void Layout(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static Text EnsureText(Transform parent, string name, string value)
        {
            var node = parent.Find(name) ?? NewRect(name, parent);
            var text = node.GetComponent<Text>() ?? node.gameObject.AddComponent<Text>();
            text.text = value;
            text.color = CatTheme.Ink;
            text.font = CatTheme.Font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        static Button EnsureButton(Transform parent, string name, string label)
        {
            var node = parent.Find(name) ?? NewRect(name, parent);
            var image = node.GetComponent<Image>() ?? node.gameObject.AddComponent<Image>();
            image.color = CatTheme.Cream;
            var button = node.GetComponent<Button>() ?? node.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = EnsureText(node, "Text", label);
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 12;
            text.resizeTextMaxSize = 18;
            Layout(text.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
            return button;
        }

        void BuildMarkers()
        {
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
                var marker = NewRect("Pond_" + item.pondId, _markerLayer);
                marker.anchorMin = new Vector2(item.x, 1f - item.y);
                marker.anchorMax = marker.anchorMin;
                marker.sizeDelta = new Vector2(172f, 96f);
                var markerImage = marker.gameObject.AddComponent<Image>();
                markerImage.color = CatTheme.Cream;
                var button = marker.gameObject.AddComponent<Button>();
                button.targetGraphic = markerImage;
                var captured = item;
                button.onClick.AddListener(() => Select(captured));
                var caption = item.displayName + "\n" +
                              DesktopGameData.CategoryLabel(item.pondCategory);
                if (locked)
                    caption += " · 未开放/需解锁";
                var label = EnsureText(marker, "Label", caption);
                label.alignment = TextAnchor.MiddleCenter;
                label.fontSize = 16;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 14;
                label.resizeTextMaxSize = 16;
                label.color = locked ? new Color(0.38f, 0.34f, 0.3f, 1f) : CatTheme.Ink;
                Layout(label.rectTransform, new Vector2(0.23f, 0.1f), new Vector2(0.94f, 0.9f));
                CatTheme.StyleButton(button, locked ? "lock.png" : "pond.png");
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
            _feeConfirmPondId = null;
            if (_selected == null)
            {
                _returnFeeMode = "sell_only";
                _lastSelectedPondId = null;
            }
            else if (!string.Equals(_lastSelectedPondId, _selected.pondId, StringComparison.Ordinal))
            {
                _lastSelectedPondId = _selected.pondId;
                _returnFeeMode = PondHasDualFee(_selected) ? null : "sell_only";
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
                            "\n状态：" + reason +
                            "\n" + levelLine +
                            "\n" + feeLine +
                            "\n主题：" + _selected.theme +
                            "\n容量：" + _selected.capacity;

            if (locked)
                SetEnterLabel("暂不可进入", false);
            else if (PondHasDualFee(_selected) && string.IsNullOrEmpty(_returnFeeMode))
                SetEnterLabel("请先选收费模式", false);
            else if (activeFee > 0)
                SetEnterLabel("确认进入", true);
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
            var activeFee = _returnFeeMode == "auto_return" && PondHasDualFee(_selected)
                ? _selected.feePer2hAutoReturn
                : (_selected.feePer2hSellOnly > 0 ? _selected.feePer2hSellOnly : _selected.feePer2h);
            if (activeFee > 0 && _feeConfirmPondId != _selected.pondId)
            {
                _feeConfirmPondId = _selected.pondId;
                var today = _progress != null ? _progress.todayFeeCharges : 0;
                var maxFee = _selected.maxFeeChargesPerDay > 0 ? _selected.maxFeeChargesPerDay : 4;
                var modeLabel = _returnFeeMode == "auto_return" ? "回鱼档" : "出售档";
                SetStatus(modeLabel + "：每满 2 小时扣 " + activeFee +
                          " 金币，今日已扣 " + today + "/" + maxFee +
                          "。再点一次确认进入。");
                SetEnterLabel("再次确认进入", true);
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
            var text = _enter.GetComponentInChildren<Text>();
            if (text != null)
                text.text = label;
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
            _zoom = 1f;
            _content.localScale = Vector3.one;
            _content.anchoredPosition = Vector2.zero;
            ClampContent();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragStart = eventData.position;
            _contentStart = _content.anchoredPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            _content.anchoredPosition = _contentStart + eventData.position - _dragStart;
            ClampContent();
        }

        public void OnEndDrag(PointerEventData eventData) { }

        public void OnScroll(PointerEventData eventData)
        {
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
