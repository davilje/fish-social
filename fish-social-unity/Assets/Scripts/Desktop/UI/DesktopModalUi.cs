using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    public enum DesktopModalId
    {
        None = 0,
        Social = 1,
        CatchBag = 2,
        Gallery = 3,
        Settings = 4,
    }

    public static class DesktopModalUi
    {
        public static Font Font => CatTheme.Font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        public static readonly Color PanelColor = CatTheme.Cream;
        public static readonly Color Header = new Color(1f, 0.93f, 0.82f, 1f);
        public static readonly Color Button = CatTheme.Cream;
        public static readonly Color Tab = new Color(1f, 0.94f, 0.85f, 1f);
        public static readonly Color Slot = new Color(1f, 0.96f, 0.90f, 1f);
        public static readonly Color SlotOn = new Color(1f, 0.85f, 0.64f, 1f);

        public static RectTransform Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Transform FindChild(Transform root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path))
                return null;
            return root.Find(path);
        }

        public static T FindComponent<T>(Transform root, string path)
            where T : Component
        {
            var child = FindChild(root, path);
            return child != null ? child.GetComponent<T>() : null;
        }

        public static Transform FindDescendant(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var match = FindDescendant(root.GetChild(i), name);
                if (match != null)
                    return match;
            }
            return null;
        }

        public static T FindDescendantComponent<T>(Transform root, string name)
            where T : Component
        {
            var child = FindDescendant(root, name);
            return child != null ? child.GetComponent<T>() : null;
        }

        public static bool BindButton(
            Transform root, string path, UnityEngine.Events.UnityAction onClick)
        {
            var button = FindComponent<Button>(root, path);
            if (button == null)
                return false;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            StyleButton(button);
            return true;
        }

        public static bool BindToggle(
            Transform root, string path, bool value, System.Action<bool> onChanged)
        {
            var toggle = FindComponent<Toggle>(root, path);
            if (toggle == null)
                return false;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.SetIsOnWithoutNotify(value);
            toggle.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            return true;
        }

        public static bool BindDescendantButton(
            Transform root, string name, UnityEngine.Events.UnityAction onClick)
        {
            var button = FindDescendantComponent<Button>(root, name);
            if (button == null)
                return false;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            StyleButton(button);
            return true;
        }

        public static bool BindDescendantToggle(
            Transform root, string name, bool value, System.Action<bool> onChanged)
        {
            var toggle = FindDescendantComponent<Toggle>(root, name);
            if (toggle == null)
                return false;
            toggle.onValueChanged.RemoveAllListeners();
            toggle.SetIsOnWithoutNotify(value);
            toggle.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            return true;
        }

        public static GameObject Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        public static void StyleButton(Button button, string iconName = null, bool primary = false)
        {
            if (button == null)
                return;
            var visual = button.GetComponent<CozyButtonVisual>();
            if (visual == null)
                return;
            if (FindComponent<Image>(button.transform, "CozyVisual") == null)
            {
                Debug.LogError("[DesktopUI] Button requires its serialized CozyVisual Image: " + button.name, button);
                return;
            }
            visual.Configure(button, null, primary);
            if (!string.IsNullOrEmpty(iconName))
            {
                visual.SetIconName(iconName);
                visual.RefreshContent();
            }
        }

        public static void StyleTree(Transform root)
        {
            if (root == null)
                return;
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.name == "FeatureModalRoot" || button.name == "ProductContextMenu" ||
                    button.name == "PopupProductMenu" || button.name == "ModalPanel" ||
                    button.name == "Panel" || button.name == "Dim" || button.name == "MenuPanel")
                    continue;
                StyleButton(button);
            }
            CatTheme.SkinTree(root);
        }

        public static bool RequireNodes(Transform root, string prefab, params string[] paths)
        {
            var valid = true;
            foreach (var path in paths)
            {
                if (FindChild(root, path) != null)
                    continue;
                Debug.LogError("[DesktopUI] Required prefab node is missing: Desktop/Prefabs/" +
                               prefab + "/" + path, root);
                valid = false;
            }
            return valid;
        }

        public static Text Label(Transform parent, string name, string text, int size, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = CatTheme.Ink;
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Button MakeButton(Transform parent, string name, string text, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Button;
            var label = Label(go.transform, "Label", text, 15, TextAnchor.MiddleCenter);
            Stretch(label.gameObject);
            var button = go.GetComponent<Button>();
            if (onClick != null)
                button.onClick.AddListener(onClick);
            CatTheme.StyleButton(button);
            return button;
        }

        public static InputField MakeInput(Transform parent, string name, string placeholder, int charLimit)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Slot;
            var ph = Label(go.transform, "Placeholder", placeholder, 15, TextAnchor.MiddleLeft);
            var value = Label(go.transform, "Text", string.Empty, 15, TextAnchor.MiddleLeft);
            Stretch(ph.gameObject).offsetMin = new Vector2(10, 0);
            Stretch(value.gameObject).offsetMin = new Vector2(10, 0);
            ph.color = new Color(CatTheme.Ink.r, CatTheme.Ink.g, CatTheme.Ink.b, 0.55f);
            var input = go.GetComponent<InputField>();
            input.textComponent = value;
            input.placeholder = ph;
            input.characterLimit = charLimit;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        public static ScrollRect MakeScroll(Transform parent, string name, out Transform content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = Slot;
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(go.transform, false);
            var contentRt = Stretch(contentGo);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.offsetMin = new Vector2(0f, 0f);
            contentRt.offsetMax = Vector2.zero;
            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = go.GetComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            content = contentGo.transform;
            return scroll;
        }

        public static void Clear(Transform parent)
        {
            if (parent == null)
                return;
            for (var i = parent.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.GetChild(i).gameObject);
        }

        public static Text Row(Transform parent, string text, int height = 28)
        {
            var label = Label(parent, "Row", text, 15, TextAnchor.MiddleLeft);
            var layout = label.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            return label;
        }
    }

}
