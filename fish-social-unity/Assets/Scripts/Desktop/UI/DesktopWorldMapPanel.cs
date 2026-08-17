using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

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
        WorldMapPondDefinition[] _ponds = new WorldMapPondDefinition[0];
        readonly Dictionary<string, WorldMapPondDefinition> _byId =
            new Dictionary<string, WorldMapPondDefinition>(StringComparer.Ordinal);
        WorldMapPondDefinition _selected;
        Vector2 _dragStart;
        Vector2 _contentStart;
        float _zoom = 1f;

        public void Bind(SocialPondSessionController pond)
        {
            _pond = pond;
            EnsureUi();
            _ponds = WorldMapPondCatalog.Load();
            _byId.Clear();
            for (var i = 0; i < _ponds.Length; i++)
                if (_ponds[i] != null && !string.IsNullOrEmpty(_ponds[i].pondId))
                    _byId[_ponds[i].pondId] = _ponds[i];
            if (_pond != null)
            {
                _pond.ErrorReceived += OnPondError;
                _pond.StateChanged += OnPondStateChanged;
            }
            BuildMarkers();
            SelectById(_pond != null ? _pond.CurrentPondId : null);
            SetStatus(_pond == null ? "请先完成 Steam 登录。" :
                "地图坐标由配置驱动，鱼塘状态由服务端返回。");
            ClampContent();
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
                var marker = NewRect("Pond_" + item.pondId, _markerLayer);
                marker.anchorMin = new Vector2(item.x, 1f - item.y);
                marker.anchorMax = marker.anchorMin;
                marker.sizeDelta = new Vector2(58f, 58f);
                marker.gameObject.AddComponent<Image>().color =
                    new Color(0.95f, 0.75f, 0.28f, 0.95f);
                var button = marker.gameObject.AddComponent<Button>();
                var captured = item;
                button.onClick.AddListener(() => Select(captured));
                var label = EnsureText(marker, "Label", item.displayName);
                label.alignment = TextAnchor.MiddleCenter;
                label.fontSize = 11;
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = Vector2.zero;
            }
        }

        void SelectById(string pondId)
        {
            WorldMapPondDefinition item;
            if (!string.IsNullOrEmpty(pondId) && _byId.TryGetValue(pondId, out item))
                Select(item);
            else
                Select(_ponds.Length > 0 ? _ponds[0] : null);
        }

        void Select(WorldMapPondDefinition item)
        {
            _selected = item;
            if (_selected == null)
            {
                _details.text = "暂无可用鱼塘坐标配置。";
                _enter.interactable = false;
                return;
            }
            var online = _pond != null && _pond.CurrentPondId == _selected.pondId &&
                         _pond.LatestSnapshot != null
                ? (_pond.LatestSnapshot.users == null ? 0 : _pond.LatestSnapshot.users.Length).ToString()
                : "进入后由服务端同步";
            _details.text = _selected.displayName + "\n\n主题：" + _selected.theme +
                            "\npondId：" + _selected.pondId +
                            "\n在线人数：" + online +
                            "\n容量：" + _selected.capacity;
            _enter.interactable = _pond != null;
        }

        void EnterSelectedPond()
        {
            if (_selected == null || _pond == null)
            {
                SetStatus("请选择有效鱼塘，或等待会话初始化。");
                return;
            }
            SetStatus("正在进入 " + _selected.displayName + "…");
            _pond.ConnectAndJoin(_selected.pondId, "Steam玩家");
            DesktopAppBootstrap.Instance?.StartNativeOverlay();
            WindowManager.Instance?.HideToTray();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
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
        }
    }
}
