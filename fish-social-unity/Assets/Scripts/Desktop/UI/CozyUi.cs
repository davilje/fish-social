using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    public static class CozyUi
    {
        public static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max,
            Vector2 insetMin, Vector2 insetMax)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            var rect = node.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = insetMin;
            rect.offsetMax = insetMax;
            return rect;
        }

        public static Image Picture(Transform parent, string name, string path, Vector2 min,
            Vector2 max, Vector2 insetMin, Vector2 insetMax, bool preserveAspect = true)
        {
            var rect = Rect(parent, name, min, max, insetMin, insetMax);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = CatTheme.LoadCozy(path, Vector4.zero);
            image.color = Color.white;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }

        public static Text Label(Transform parent, string name, string value, int size,
            Vector2 min, Vector2 max, Vector2 insetMin, Vector2 insetMax,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = DesktopModalUi.Label(parent, name, value, size, alignment);
            var rect = text.rectTransform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = insetMin;
            rect.offsetMax = insetMax;
            return text;
        }

        public static Button Action(Transform parent, string name, string label, string icon,
            UnityEngine.Events.UnityAction action, Vector2 min, Vector2 max,
            Vector2 insetMin, Vector2 insetMax, bool primary = false)
        {
            var button = DesktopModalUi.MakeButton(parent, name, label, action);
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = insetMin;
            rect.offsetMax = insetMax;
            CatTheme.StyleButton(button, icon, primary);
            var text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.rectTransform.offsetMin = new Vector2(36f, 4f);
                text.rectTransform.offsetMax = new Vector2(-12f, -4f);
                text.color = CatTheme.Ink;
                text.fontSize = 18;
            }
            return button;
        }

        public static string PageTitle(ShellPanelId id)
        {
            switch (id)
            {
                case ShellPanelId.Pond: return "开始钓鱼";
                case ShellPanelId.WorldMap: return "世界地图";
                case ShellPanelId.PondSelection: return "选择鱼塘";
                case ShellPanelId.CatchBag: return "我的背包";
                case ShellPanelId.Equipment: return "装备搭配";
                case ShellPanelId.Shop: return "猫咪小铺";
                case ShellPanelId.Achievements: return "钓鱼成就";
                case ShellPanelId.Leaderboard: return "钓鱼排行榜";
                case ShellPanelId.Profile: return "我的钓鱼手账";
                case ShellPanelId.SocialFeed: return "钓友动态";
                case ShellPanelId.Friends: return "好友与聊天";
                case ShellPanelId.ProfileEdit: return "编辑个人资料";
                case ShellPanelId.Gallery: return "鱼类图鉴";
                case ShellPanelId.Settings: return "设置";
                default: return "猫咪钓鱼";
            }
        }
    }
}
