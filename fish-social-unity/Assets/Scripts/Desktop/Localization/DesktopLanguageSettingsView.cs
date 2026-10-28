using FishSocial.Desktop.JournalV2;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    public sealed class DesktopLanguageSettingsView : MonoBehaviour
    {
        readonly Button[] choices = new Button[2];
        static readonly string[] Languages = { "zh-CN", "en-US" };

        public static void Ensure(Transform parent, float y = 700, float width = 674)
        {
            if (parent == null || parent.Find("LanguageSettings") != null) return;
            var root = JournalV2Nodes.Rect(parent, "LanguageSettings", 18, y, width, 132);
            var view = root.gameObject.AddComponent<DesktopLanguageSettingsView>();
            var title = JournalV2Nodes.Text(root, "Label", "", 4, 0, width, 30, 21);
            DesktopLocalization.SetText(title, DesktopLocalization.Text("ui.settings.language.title", "界面语言"));
            var hint = JournalV2Nodes.Text(root, "Hint", "", 4, 80, width, 48, 16);
            DesktopLocalization.SetText(hint, DesktopLocalization.Text("ui.settings.language.description", "切换后立即生效，语言选择会自动保存"));
            hint.color = JournalV2Theme.Soft;
            for (var i = 0; i < Languages.Length; i++)
            {
                var language = Languages[i];
                var button = JournalV2Nodes.Button(root, language, "", null, 4 + i * 192, 36, 182, 38);
                view.choices[i] = button;
                button.onClick.AddListener(() => DesktopLocalization.SetLanguage(language));
            }
            view.Refresh();
        }

        void Refresh()
        {
            var language = DesktopLocalization.Language;
            for (var i = 0; i < choices.Length; i++)
            {
                var button = choices[i];
                if (button == null) continue;
                var label = button.GetComponentInChildren<Text>();
                DesktopLocalization.SetText(label, i == 0
                    ? DesktopLocalization.Text("ui.settings.language.option.chinese", "简体中文")
                    : DesktopLocalization.Text("ui.settings.language.option.english", "English"));
                button.interactable = language != Languages[i];
            }
        }

        void OnEnable() { DesktopLocalization.Changed += Refresh; Refresh(); }
        void OnDisable() { DesktopLocalization.Changed -= Refresh; }
    }
}
