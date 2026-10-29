using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Corrects baked coordinates to the cozy-journal stage (1672×941, page at 160,150).</summary>
    public static class CozyWebStage
    {
        public static void AlignHeader(Transform header)
        {
            if (header == null)
                return;
            var name = header.Find("ProfileCard/Name")?.GetComponent<Text>();
            var level = header.Find("ProfileCard/Level")?.GetComponent<Text>();
            var badge = header.Find("ProfileCard/LevelBadge") as RectTransform;
            if (name == null)
                return;
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            var nameRect = name.rectTransform;
            var width = Mathf.Clamp(name.preferredWidth, 40f, 190f);
            nameRect.sizeDelta = new Vector2(width, nameRect.sizeDelta.y);
            var x = nameRect.anchoredPosition.x + width + 14f;
            if (badge != null)
                badge.anchoredPosition = new Vector2(x, badge.anchoredPosition.y);
            if (level != null)
                level.rectTransform.anchoredPosition = new Vector2(x, level.rectTransform.anchoredPosition.y);
        }

        public static void AlignPage(Transform page)
        {
            if (page == null)
                return;
            var title = page.Find("DesignHeader/Title") as RectTransform;
            if (title == null)
                return;
            if (Mathf.Abs(title.anchoredPosition.x - 100f) < 0.6f)
                title.anchoredPosition = new Vector2(92f, title.anchoredPosition.y);
            var label = title.GetComponent<Text>();
            var paw = page.Find("DesignHeader/JournalPaw") as RectTransform;
            if (label == null || paw == null)
                return;
            if (label.preferredWidth > 1f)
                paw.anchoredPosition = new Vector2(title.anchoredPosition.x + label.preferredWidth + 6f, paw.anchoredPosition.y);
        }

        public static void AlignBag(Transform page)
        {
            if (page == null || CozySurplus.Visible)
                return;
            var sell = Find(page, "Sell") as RectTransform;
            if (sell == null)
                return;
            CozySurplus.Park(Find(page, "Return")?.gameObject);
            CozySurplus.Park(Find(page, "Share")?.gameObject);
        }

        static Transform Find(Transform root, string name)
        {
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
