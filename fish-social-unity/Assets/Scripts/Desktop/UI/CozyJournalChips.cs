using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Web .chip row. The pill sprite already exists; this only places it.</summary>
    public static class CozyJournalChips
    {
        public static void Show(Text anchor, params string[] labels)
        {
            if (anchor == null)
                return;
            var parent = anchor.transform.parent;
            if (parent == null)
                return;
            var row = parent.Find("JournalChips") as RectTransform;
            if (row == null)
            {
                var go = new GameObject("JournalChips", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                go.transform.SetParent(parent, false);
                row = (RectTransform)go.transform;
                var layout = go.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 8f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }
            var anchorRect = anchor.rectTransform;
            row.anchorMin = anchorRect.anchorMin;
            row.anchorMax = anchorRect.anchorMax;
            row.pivot = anchorRect.pivot;
            var used = anchor.preferredHeight > 1f ? anchor.preferredHeight : anchorRect.sizeDelta.y;
            used = Mathf.Clamp(used, 18f, Mathf.Max(18f, anchorRect.sizeDelta.y));
            row.anchoredPosition = anchorRect.anchoredPosition + new Vector2(0f, -used - 8f);
            row.sizeDelta = new Vector2(Mathf.Max(80f, anchorRect.sizeDelta.x), 28f);
            for (var i = row.childCount - 1; i >= 0; i--)
            {
                var child = row.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Object.Destroy(child);
                else
                    Object.DestroyImmediate(child);
            }
            var sprite = CozyJournalTheme.Sprite("surfaces/category-chip");
            var count = 0;
            if (labels != null)
            {
                foreach (var label in labels)
                {
                    if (string.IsNullOrEmpty(label))
                        continue;
                    count++;
                    var chip = new GameObject("Chip", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    chip.transform.SetParent(row, false);
                    var image = chip.GetComponent<Image>();
                    image.raycastTarget = false;
                    if (sprite != null)
                    {
                        image.sprite = sprite;
                        image.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                        image.color = Color.white;
                    }
                    else
                        image.color = new Color(0.965f, 0.918f, 0.831f, 1f);
                    var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                    textGo.transform.SetParent(chip.transform, false);
                    var text = textGo.GetComponent<Text>();
                    text.font = CatTheme.Font;
                    text.fontSize = 15;
                    text.color = CatTheme.Ink;
                    text.alignment = TextAnchor.MiddleCenter;
                    text.supportRichText = false;
                    text.raycastTarget = false;
                    DesktopLocalization.SetText(text, label);
                    var width = Mathf.Clamp(text.preferredWidth + 24f, 64f, 200f);
                    chip.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 26f);
                    var element = chip.GetComponent<LayoutElement>();
                    element.preferredWidth = width;
                    element.preferredHeight = 26f;
                    var textRect = text.rectTransform;
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = new Vector2(8f, 0f);
                    textRect.offsetMax = new Vector2(-8f, 0f);
                }
            }
            row.gameObject.SetActive(count > 0);
        }
    }
}
