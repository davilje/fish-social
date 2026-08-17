using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Product context menu for the Fish Social window. Dispatches actions only;
    /// does not own pond session, overlay, or panel business logic.
    /// </summary>
    public sealed class DesktopProductMenuView : MonoBehaviour
    {
        const float ItemHeight = 36f;
        const float MenuWidth = 208f;
        const float SeparatorHeight = 8f;

        static readonly MenuEntry[] Entries =
        {
            new MenuEntry(DesktopProductMenuAction.CurrentPond, "当前鱼塘", false),
            new MenuEntry(DesktopProductMenuAction.WorldMap, "世界地图", false),
            new MenuEntry(DesktopProductMenuAction.Shop, "商店与装备", false),
            new MenuEntry(DesktopProductMenuAction.Friends, "好友与聊天", false),
            new MenuEntry(DesktopProductMenuAction.CatchBag, "鱼获/背包", false),
            new MenuEntry(DesktopProductMenuAction.Gallery, "图鉴", false),
            new MenuEntry(DesktopProductMenuAction.Settings, "设置", false),
            new MenuEntry(DesktopProductMenuAction.HideToTray, "隐藏到托盘", true),
            new MenuEntry(DesktopProductMenuAction.Quit, "退出", true),
        };

        RectTransform _host;
        IDesktopProductMenuHandler _handler;
        GameObject _root;
        RectTransform _rootRt;
        RectTransform _panel;
        int _openedFrame = -1;

        struct MenuEntry
        {
            public readonly DesktopProductMenuAction Action;
            public readonly string Label;
            public readonly bool SeparatorBefore;

            public MenuEntry(DesktopProductMenuAction action, string label, bool separatorBefore)
            {
                Action = action;
                Label = label;
                SeparatorBefore = separatorBefore;
            }
        }

        public void Bind(Transform canvas, RectTransform host, IDesktopProductMenuHandler handler)
        {
            _host = host;
            _handler = handler;
            Build(canvas);
            Hide();
        }

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
        }

        void Update()
        {
            if (_host == null || !_host.gameObject.activeInHierarchy)
            {
                Hide();
                return;
            }

            if (!Input.GetMouseButtonDown(1))
                return;

            if (!RectTransformUtility.RectangleContainsScreenPoint(_host, Input.mousePosition, null))
                return;

            ShowAt(Input.mousePosition);
        }

        void ShowAt(Vector2 screenPosition)
        {
            if (_root == null)
                return;

            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
            _openedFrame = Time.frameCount;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootRt, screenPosition, null, out local);
            var canvasSize = _rootRt.rect.size;
            var menuSize = _panel.rect.size;
            if (menuSize.y < 1f)
                menuSize = _panel.sizeDelta;

            var minX = -canvasSize.x * 0.5f + 8f;
            var maxX = canvasSize.x * 0.5f - menuSize.x - 8f;
            var minY = -canvasSize.y * 0.5f + menuSize.y + 8f;
            var maxY = canvasSize.y * 0.5f - 8f;
            if (maxX < minX)
                maxX = minX;
            if (maxY < minY)
                maxY = minY;

            _panel.anchoredPosition = new Vector2(
                Mathf.Clamp(local.x, minX, maxX),
                Mathf.Clamp(local.y, minY, maxY));
        }

        void OnDismiss()
        {
            if (Time.frameCount <= _openedFrame)
                return;
            Hide();
        }

        void OnItemClicked(DesktopProductMenuAction action)
        {
            Hide();
            _handler?.HandleProductMenu(action);
        }

        void Build(Transform canvas)
        {
            _root = new GameObject("ProductContextMenu", typeof(RectTransform), typeof(Image));
            _root.transform.SetParent(canvas, false);
            _rootRt = _root.GetComponent<RectTransform>();
            _rootRt.anchorMin = Vector2.zero;
            _rootRt.anchorMax = Vector2.one;
            _rootRt.offsetMin = Vector2.zero;
            _rootRt.offsetMax = Vector2.zero;
            var rootImage = _root.GetComponent<Image>();
            rootImage.color = new Color(0f, 0f, 0f, 0f);
            rootImage.raycastTarget = true;

            var dismiss = _root.AddComponent<Button>();
            dismiss.transition = Selectable.Transition.None;
            dismiss.onClick.AddListener(OnDismiss);

            var panelGo = new GameObject("MenuPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(_root.transform, false);
            _panel = panelGo.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0f, 1f);
            _panel.sizeDelta = new Vector2(MenuWidth, 8f);
            panelGo.GetComponent<Image>().color = new Color(0.12f, 0.17f, 0.22f, 0.98f);
            panelGo.GetComponent<Image>().raycastTarget = true;

            var layout = panelGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 2;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = panelGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var entry in Entries)
            {
                if (entry.SeparatorBefore)
                    CreateSeparator(panelGo.transform);
                CreateItem(panelGo.transform, entry.Label, entry.Action);
            }
        }

        void CreateSeparator(Transform parent)
        {
            var go = new GameObject("Separator", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = SeparatorHeight;
            go.GetComponent<LayoutElement>().minHeight = SeparatorHeight;
            var image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.12f);
            image.raycastTarget = false;
        }

        void CreateItem(Transform parent, string label, DesktopProductMenuAction action)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = ItemHeight;
            go.GetComponent<LayoutElement>().minHeight = ItemHeight;
            var image = go.GetComponent<Image>();
            image.color = new Color(0.18f, 0.26f, 0.32f, 1f);

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(12f, 0f);
            textRt.offsetMax = new Vector2(-8f, 0f);
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = label;
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;

            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.28f, 0.42f, 0.5f, 1f);
            colors.pressedColor = new Color(0.16f, 0.32f, 0.4f, 1f);
            button.colors = colors;
            var captured = action;
            button.onClick.AddListener(() => OnItemClicked(captured));
        }
    }
}
