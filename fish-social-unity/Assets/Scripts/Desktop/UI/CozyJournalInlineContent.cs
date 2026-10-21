using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Measured CSS-style inline content. Only foreground children move; hit roots stay fixed.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CozyJournalInlineContent : MonoBehaviour
    {
        enum Kind { Centered, NumericPrice, Profile }
        [SerializeField] Kind kind;
        [SerializeField] RectTransform row;
        [SerializeField] Text label;
        [SerializeField] Image icon;
        [SerializeField] Image prefix;
        [SerializeField] Text badgeLabel;
        [SerializeField] Sprite iconOverride;
        [SerializeField] Vector2 iconSize = new Vector2(28,28);
        [SerializeField] Vector2 prefixSize = new Vector2(30,22);
        [SerializeField] float gap = 10;
        [SerializeField] float padding = 12;
        [SerializeField] int fontSize = 23;
        [SerializeField] int minimumFontSize = 12;
        [SerializeField] float maxNameWidth = 201;
        [SerializeField] Vector2 profileOrigin = new Vector2(110,18);
        bool reflowing;

        public void ConfigureCentered(RectTransform bounds, Text text, Image inlineIcon, Vector2 size,
            float spacing = 10, float inset = 12, Image leading = null, Sprite forcedIcon = null, bool numericPriceOnly = false)
        {
            kind = numericPriceOnly ? Kind.NumericPrice : Kind.Centered;
            row = bounds; label = text; icon = inlineIcon; iconSize = size;
            gap = spacing; padding = inset; prefix = leading; iconOverride = forcedIcon;
            fontSize = text != null ? text.fontSize : 23;
            minimumFontSize = Mathf.Min(12,fontSize);
            Reflow();
        }

        public void ConfigureProfile(RectTransform card, Text nickname, Image badge, Text level)
        {
            kind = Kind.Profile; row = card; label = nickname; icon = badge; badgeLabel = level;
            fontSize = 26; minimumFontSize = 18; maxNameWidth = 201; gap = 14;
            Reflow();
        }

        public static void ReflowTree(Transform root)
        {
            if (root == null) return;
            foreach (var inline in root.GetComponentsInChildren<CozyJournalInlineContent>(true)) inline.Reflow();
        }

        void OnEnable() { Canvas.willRenderCanvases += Reflow; Reflow(); }
        void OnDisable() { Canvas.willRenderCanvases -= Reflow; }

        public static bool IsNumericPrice(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            value = value.Trim().Replace("\u00a0", "").Replace("\u202f", "");
            var digit = false;
            foreach (var c in value)
            {
                if (c >= '0' && c <= '9') { digit = true; continue; }
                if (c != ',' && c != '.') return false;
            }
            return digit && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) && price >= 0;
        }

        public void Reflow()
        {
            if (reflowing || row == null || label == null || label.font == null) return;
            reflowing = true;
            try
            {
                if (kind == Kind.Profile) Profile();
                else Centered();
            }
            finally { reflowing = false; }
        }

        void Centered()
        {
            if (kind == Kind.NumericPrice && icon != null)
            {
                var visible = IsNumericPrice(label.text);
                if (icon.gameObject.activeSelf != visible) icon.gameObject.SetActive(visible);
            }
            if (icon != null && iconOverride != null && icon.sprite != iconOverride)
                icon.sprite = iconOverride;
            var hasIcon = Visible(icon);
            var hasPrefix = Visible(prefix);
            var count = (hasIcon ? 1 : 0) + (hasPrefix ? 1 : 0) + (string.IsNullOrEmpty(label.text) ? 0 : 1);
            var occupied = (hasIcon ? iconSize.x : 0) + (hasPrefix ? prefixSize.x : 0) + Mathf.Max(0,count-1) * gap;
            var available = Mathf.Max(0, row.rect.width - 2 * padding - occupied);
            var measured = Measure(label, fontSize);
            var width = Mathf.Min(available, measured);
            FitText(label, width < measured - .01f, fontSize, minimumFontSize);
            var x = (row.rect.width - occupied - width) * .5f;
            if (hasPrefix)
            {
                Position(prefix.rectTransform,x,(row.rect.height-prefixSize.y)*.5f,prefixSize.x,prefixSize.y);
                x += prefixSize.x + gap;
            }
            if (hasIcon)
            {
                Position(icon.rectTransform,x,(row.rect.height-iconSize.y)*.5f,iconSize.x,iconSize.y);
                x += iconSize.x + gap;
            }
            Position(label.rectTransform,x,0,width,row.rect.height);
            label.alignment = TextAnchor.MiddleCenter;
        }

        void Profile()
        {
            if (icon == null || badgeLabel == null) return;
            // Match the browser's 16 px horizontal badge padding, allowing multi-digit levels.
            var badgeWidth = Mathf.Clamp(Measure(badgeLabel,20) + 32,70,128);
            var available = Mathf.Min(maxNameWidth, Mathf.Max(0,row.rect.width - profileOrigin.x - 20 - gap - badgeWidth));
            var measured = Measure(label,fontSize);
            var width = Mathf.Min(available,measured);
            FitText(label,width < measured - .01f,fontSize,minimumFontSize);
            label.alignment = TextAnchor.MiddleLeft;
            Position(label.rectTransform,profileOrigin.x,profileOrigin.y,width,34);
            var badgeX = profileOrigin.x + width + gap;
            Position(icon.rectTransform,badgeX,24,badgeWidth,30);
            Position(badgeLabel.rectTransform,badgeX,24,badgeWidth,30);
            FitText(badgeLabel,false,20,20); badgeLabel.alignment = TextAnchor.MiddleCenter;
        }

        static bool Visible(Image image) => image != null && image.enabled && image.gameObject.activeSelf && image.sprite != null;

        static float Measure(Text text, int size)
        {
            var settings = text.GetGenerationSettings(Vector2.zero);
            settings.resizeTextForBestFit = false;
            settings.fontSize = size;
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            return text.cachedTextGeneratorForLayout.GetPreferredWidth(text.text,settings) / text.pixelsPerUnit;
        }

        static void FitText(Text text, bool shrink, int size, int minimum)
        {
            if (text.fontSize != size) text.fontSize = size;
            text.resizeTextMinSize = minimum; text.resizeTextMaxSize = size;
            text.resizeTextForBestFit = shrink;
            text.horizontalOverflow = shrink ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void Position(RectTransform target, float x, float y, float width, float height)
        {
            // A row may be an invisible sibling of Text, preserving every legacy binding path.
            var world = row.TransformPoint(new Vector3(row.rect.xMin+x,row.rect.yMax-y,0));
            var parent = target.parent as RectTransform;
            var point = (Vector2)parent.InverseTransformPoint(world);
            var anchored = point - new Vector2(parent.rect.xMin,parent.rect.yMax);
            target.anchorMin = target.anchorMax = target.pivot = Vector2.up;
            if (target.anchoredPosition != anchored) target.anchoredPosition = anchored;
            var size = new Vector2(width,height);
            if (target.sizeDelta != size) target.sizeDelta = size;
        }
    }
}
