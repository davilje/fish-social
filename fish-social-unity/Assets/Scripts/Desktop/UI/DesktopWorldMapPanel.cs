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
            for (var i = 0; i < _ponds.Length; i++)
                if (_ponds[i] != null && !string.IsNullOrEmpty(_ponds[i].pondId))
                    _byId[_ponds[i].pondId] = _ponds[i];
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
                sellRect.anchorMin = new Vector2(0.02f, 0.18f);
                sellRect.anchorMax = new Vector2(0.49f, 0.26f);
                sellRect.offsetMin = Vector2.zero;
                sellRect.offsetMax = Vector2.zero;
            }
            if (autoRect != null)
            {
                autoRect.anchorMin = new Vector2(0.51f, 0.18f);
                autoRect.anchorMax = new Vector2(0.98f, 0.26f);
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
            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected
                    ? new Color(0.18f, 0.45f, 0.32f, 1f)
                    : new Color(0.2f, 0.24f, 0.28f, 1f);
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
            var root = GetComponent<RectTransform>();
            if (_viewport == null)
            {
                _viewport = NewRect("Viewport", root);
                _viewport.anchorMin = new Vector2(0f, 0f);
                _viewport.anchorMax = new Vector2(0.68f, 1f);
                _viewport.offsetMin = Vector2.zero;
                _viewport.offsetMax = Vector2.zero;
                _viewport.gameObject.AddComponent<RectMask2D>();
            }
            if (_content == null)
            {
                _content = NewRect("MapContent", _viewport);
                _content.sizeDelta = new Vector2(MapWidth, MapHeight);
                _content.anchorMin = new Vector2(0.5f, 0.5f);
                _content.anchorMax = new Vector2(0.5f, 0.5f);
                _content.pivot = new Vector2(0.5f, 0.5f);
                var map = NewRect("MapImage", _content);
                map.anchorMin = Vector2.zero;
                map.anchorMax = Vector2.one;
                map.offsetMin = Vector2.zero;
                map.offsetMax = Vector2.zero;
                map.gameObject.AddComponent<Image>().color =
                    new Color(0.16f, 0.3f, 0.24f, 1f);
            }
            if (_markerLayer == null)
            {
                _markerLayer = NewRect("MarkerLayer", _content);
                _markerLayer.anchorMin = Vector2.zero;
                _markerLayer.anchorMax = Vector2.one;
                _markerLayer.offsetMin = Vector2.zero;
                _markerLayer.offsetMax = Vector2.zero;
            }
            var details = transform.Find("Details") as RectTransform;
            if (details == null)
            {
                details = NewRect("Details", root);
                details.anchorMin = new Vector2(0.68f, 0f);
                details.anchorMax = Vector2.one;
                details.offsetMin = Vector2.zero;
                details.offsetMax = Vector2.zero;
                details.gameObject.AddComponent<Image>().color =
                    new Color(0.08f, 0.12f, 0.16f, 0.96f);
            }
            _details = EnsureText(details, "DetailsText", "请选择地图上的鱼塘。");
            _status = EnsureText(details, "StatusText", string.Empty);
            _enter = EnsureButton(details, "EnterPond", "进入鱼塘");
            _reset = EnsureButton(details, "ResetView", "重置地图");
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
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        static Button EnsureButton(Transform parent, string name, string label)
        {
            var node = parent.Find(name) ?? NewRect(name, parent);
            var button = node.GetComponent<Button>() ?? node.gameObject.AddComponent<Button>();
            var text = EnsureText(node, "Text", label);
            text.text = label;
            return button;
        }

        void BuildMarkers()
        {
            for (var i = _markerLayer.childCount - 1; i >= 0; i--)
                Destroy(_markerLayer.GetChild(i).gameObject);
            foreach (var item in _ponds)
            {
                if (item == null || string.IsNullOrEmpty(item.pondId))
                    continue;
                var locked = IsLocked(item);
                var marker = NewRect("Pond_" + item.pondId, _markerLayer);
                marker.anchorMin = new Vector2(item.x, 1f - item.y);
                marker.anchorMax = marker.anchorMin;
                marker.sizeDelta = new Vector2(64f, 64f);
                marker.gameObject.AddComponent<Image>().color =
                    DesktopGameData.CategoryColor(item.pondCategory, locked);
                var button = marker.gameObject.AddComponent<Button>();
                var captured = item;
                button.onClick.AddListener(() => Select(captured));
                var caption = item.displayName + "\n" +
                              DesktopGameData.CategoryLabel(item.pondCategory);
                if (locked)
                    caption += "\n锁";
                var label = EnsureText(marker, "Label", caption);
                label.alignment = TextAnchor.MiddleCenter;
                label.fontSize = 10;
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = Vector2.zero;
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
