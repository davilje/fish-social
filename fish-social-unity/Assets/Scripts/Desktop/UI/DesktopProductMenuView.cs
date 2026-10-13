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
        static readonly MenuEntry[] Entries =
        {
            new MenuEntry(DesktopProductMenuAction.CurrentPond, "当前鱼塘", false),
            new MenuEntry(DesktopProductMenuAction.WorldMap, "世界地图", false),
            new MenuEntry(DesktopProductMenuAction.Shop, "商店与装备", false),
            new MenuEntry(DesktopProductMenuAction.Friends, "好友与聊天", false),
            new MenuEntry(DesktopProductMenuAction.CatchBag, "鱼获/背包", false),
            new MenuEntry(DesktopProductMenuAction.Profile, "个人中心", false),
            new MenuEntry(DesktopProductMenuAction.Settings, "设置", false),
            new MenuEntry(DesktopProductMenuAction.SocialFeed, "动态墙", false),
            new MenuEntry(DesktopProductMenuAction.Leaderboard, "排行榜", false),
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
            if (_root != null || canvas == null)
                return;
            var existing = canvas.Find("PopupProductMenu") ?? canvas.Find("ProductContextMenu");
            var instance = existing != null ? existing.gameObject : null;
            var created = instance == null;
            if (created)
            {
                var prefab = Resources.Load<GameObject>("Desktop/Prefabs/ProductContextMenu");
                if (prefab == null)
                {
                    Debug.LogError("[DesktopUI] Required prefab is missing: Desktop/Prefabs/ProductContextMenu", this);
                    return;
                }
                instance = Object.Instantiate(prefab, canvas, false);
                instance.name = "ProductContextMenu";
            }
            var rootRect = instance.GetComponent<RectTransform>();
            var panel = instance.transform.Find("MenuPanel") as RectTransform;
            var dismiss = instance.GetComponent<Button>();
            var valid = rootRect != null && panel != null && dismiss != null;
            foreach (var entry in Entries)
            {
                var button = DesktopModalUi.FindComponent<Button>(panel, entry.Action.ToString());
                var label = button != null ? DesktopModalUi.FindComponent<Text>(button.transform, "Label") : null;
                if (button == null || label == null)
                {
                    Debug.LogError("[DesktopUI] ProductContextMenu requires MenuPanel/" + entry.Action + " Button/Label Text.", instance);
                    valid = false;
                    continue;
                }
                label.text = entry.Label;
                button.onClick.RemoveAllListeners();
                var action = entry.Action;
                button.onClick.AddListener(() => OnItemClicked(action));
            }
            if (!valid)
            {
                Debug.LogError("[DesktopUI] ProductContextMenu requires a root RectTransform/Button and complete MenuPanel.", instance);
                instance.SetActive(false);
                if (created)
                    Object.Destroy(instance);
                return;
            }
            _root = instance;
            _rootRt = rootRect;
            _panel = panel;
            dismiss.onClick.RemoveAllListeners();
            dismiss.onClick.AddListener(OnDismiss);
            DesktopModalUi.StyleTree(_root.transform);
        }
    }
}
