using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Centers the visible icon/caption group as translated captions change width.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CozyActionContentLayout : MonoBehaviour
    {
        Text caption;
        RectTransform icon;
        string previousText;
        Vector2 previousSize;
        Font previousFont;
        int previousFontSize;

        void OnEnable()
        {
            previousText = null;
            Canvas.willRenderCanvases += Reflow;
            Reflow();
        }

        void OnDisable() { Canvas.willRenderCanvases -= Reflow; }

        public void Reflow()
        {
            if (caption == null) caption = transform.Find("Label")?.GetComponent<Text>();
            if (icon == null) icon = transform.Find("ActionIcon") as RectTransform;
            var root = transform as RectTransform;
            if (caption == null || icon == null || root == null) return;
            var size = root.rect.size;
            if (previousText == caption.text && previousSize == size && previousFont == caption.font &&
                previousFontSize == caption.fontSize) return;
            previousText = caption.text;
            previousSize = size;
            previousFont = caption.font;
            previousFontSize = caption.fontSize;
            const float iconSize = 16f, gap = 6f;
            // Keep a generous text box for font bearings while centering by measured content width.
            var boxWidth = Mathf.Max(0, size.x - 37f);
            var textWidth = Mathf.Min(boxWidth, Mathf.Ceil(caption.preferredWidth));
            var left = (size.x - iconSize - gap - textWidth) * .5f;
            Place(icon, left, (size.y - iconSize) * .5f, iconSize, iconSize);
            Place(caption.rectTransform, left + iconSize + gap + (textWidth - boxWidth) * .5f,
                0, boxWidth, size.y);
            caption.alignment = TextAnchor.MiddleCenter;
        }

        static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
