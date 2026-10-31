using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    public sealed partial class CozyHomeRoom
    {
        void EnsureHomeActions()
        {
            equipment = HomeAction(equipment, "Equipment", "rod", 748, 547, "ui.home.room.button.equipment", "我的钓具");
            aquarium = HomeAction(aquarium, "Aquarium", "tank", 1401, 550, "ui.home.room.button.showcase", "鱼缸 · 展示柜");
            sleep = HomeAction(sleep, "Sleep", "sleep", 327, 637, "ui.home.room.button.sleep", "睡觉");
            food = HomeAction(food, "Food", "food", 1558, 646, "ui.home.room.button.feed", "吃饭");
            goFishing = HomeAction(goFishing, "GoFishing", "door", 1591, 470, "ui.home.room.button.go_fishing", "出门钓鱼");
            sleep.onClick.RemoveAllListeners(); sleep.onClick.AddListener(SleepInBed);
            food.onClick.RemoveAllListeners(); food.onClick.AddListener(() => Feed());
        }

        Button HomeAction(Button button, string name, string icon, float x, float y, string key, string fallback)
        {
            if (button == null) button = transform.Find(name)?.GetComponent<Button>();
            if (button == null) button = Rect(transform, name, 0, 0, 48, 48).gameObject.AddComponent<Button>();
            EnsureQuietButton(button);
            var r = (RectTransform)button.transform;
            r.anchorMin = r.anchorMax = r.pivot = Vector2.up;
            r.anchoredPosition = new Vector2(x - 24, -y); r.sizeDelta = new Vector2(48, 48);
            var label = r.Find("Label"); if (label != null) label.gameObject.SetActive(false);
            var glyph = r.Find("HomeIcon")?.GetComponent<RawImage>();
            if (glyph == null) glyph = Rect(r, "HomeIcon", 12, 12, 24, 24).gameObject.AddComponent<RawImage>();
            glyph.texture = Resources.Load<Texture2D>("Desktop/Theme/Cozy/interaction-icons/" + icon);
            glyph.raycastTarget = false;
            var tag = button.GetComponent<CozyHomeActionTag>() ?? button.gameObject.AddComponent<CozyHomeActionTag>();
            tag.Configure(this, key, fallback);
            button.GetComponent<CozyButtonVisual>().SetHomeIconTag(name == "GoFishing");
            return button;
        }
    }
}
