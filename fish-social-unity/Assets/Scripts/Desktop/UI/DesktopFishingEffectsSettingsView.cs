using FishSocial.Desktop.JournalV2;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Uses the existing settings page; the overlay remains free of controls.</summary>
    public sealed class DesktopFishingEffectsSettingsView : MonoBehaviour
    {
        public const string Preference = "FishSocial.FishingEffectsQuality";
        readonly Button[] choices = new Button[3];
        static readonly string[] Values = { "standard", "reduced", "off" };
        static readonly string[] Labels = { "标准", "简化", "关闭" };
        public static string Quality
        {
            get
            {
                var value = PlayerPrefs.GetString(Preference, "standard");
                if (value == "off") return "off";
                return value == "reduced" || CozyMotion.Reduced ? "reduced" : "standard";
            }
        }

        public static void Ensure(Transform parent, float y = 572, float width = 674)
        {
            if (parent == null || parent.Find("FishingEffectsSettings") != null) return;
            var root = JournalV2Nodes.Rect(parent, "FishingEffectsSettings", 18, y, width, 112);
            var view = root.gameObject.AddComponent<DesktopFishingEffectsSettingsView>();
            JournalV2Nodes.Text(root, "Label", "钓鱼水面特效", 4, 0, width, 30, 21);
            JournalV2Nodes.Text(root, "Hint", "简化保留入水、咬钩和真实拍水的水纹", 4, 76, width, 28, 16);
            for (var i = 0; i < Values.Length; i++)
            {
                var value = Values[i];
                var button = JournalV2Nodes.Button(root, value, Labels[i], null,
                    4 + i * 134, 34, 124, 36);
                view.choices[i] = button;
                button.onClick.AddListener(() =>
                {
                    if (Quality == value) return;
                    // Save even when the bootstrap has not been constructed (e.g. settings preview).
                    if (DesktopAppBootstrap.Instance != null)
                        DesktopAppBootstrap.Instance.SetFishingEffectsQuality(value);
                    else
                    {
                        PlayerPrefs.SetString(Preference, value);
                        PlayerPrefs.Save();
                    }
                    view.Refresh();
                });
            }
            view.Refresh();
        }

        void Refresh()
        {
            var quality = Quality;
            for (var i = 0; i < choices.Length; i++)
            {
                if (choices[i] == null) continue;
                var label = choices[i].GetComponentInChildren<Text>();
                if (label != null) label.text = (quality == Values[i] ? "✓ " : "") + Labels[i];
            }
        }
        void OnReducedChanged(bool reduced) { Refresh(); }
        void OnEnable() { CozyMotion.ReducedChanged += OnReducedChanged; Refresh(); }
        void OnDisable() { CozyMotion.ReducedChanged -= OnReducedChanged; }
    }
}
