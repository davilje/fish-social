using FishSocial.Desktop.JournalV2;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    public sealed class DesktopAudioSettingsView : MonoBehaviour
    {
        Slider music, effects;
        Text musicLabel, effectsLabel;
        Toggle backgroundBite;
        float previewAt;

        public static void Ensure(Transform parent, float y = 260, float width = 674)
        {
            if (parent == null || parent.Find("AudioSettings") != null) return;
            var root = JournalV2Nodes.Rect(parent, "AudioSettings", 18, y, width, 180);
            var view = root.gameObject.AddComponent<DesktopAudioSettingsView>();
            view.music = view.Row(root, "MusicVolume", DesktopLocalization.Text("ui.settings.audio.label.music_volume", "背景音乐"), 0, width, out view.musicLabel);
            view.effects = view.Row(root, "EffectsVolume", DesktopLocalization.Text("ui.settings.audio.label.sound_volume", "音效与猫叫"), 64, width, out view.effectsLabel);
            var toggleRoot = JournalV2Nodes.Rect(root, "BackgroundBite", 4, 132, width - 8, 40);
            var hit = toggleRoot.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            view.backgroundBite = toggleRoot.gameObject.AddComponent<Toggle>();
            var box = JournalV2Nodes.Plate(toggleRoot, "Box", 0, 6, 28, 28, JournalV2Theme.Paper, 7, 2);
            box.ShadowColor = Color.clear;
            var check = JournalV2Nodes.Text(toggleRoot, "Check", "✓", 0, 4, 28, 32, 23, TextAnchor.MiddleCenter);
            check.color = JournalV2Theme.Green;
            view.backgroundBite.targetGraphic = box; view.backgroundBite.graphic = check;
            JournalV2Nodes.Text(toggleRoot, "Label", DesktopLocalization.Text("ui.settings.audio.toggle.background_bite", "后台咬钩提示"), 42, 0, width - 50, 40, 20);
            view.backgroundBite.onValueChanged.AddListener(DesktopAudioService.SetBackgroundBite);
            view.Refresh();
            view.music.onValueChanged.AddListener(value => {
                DesktopAudioService.SetMusicVolume(value); view.Label(view.musicLabel, value);
            });
            view.effects.onValueChanged.AddListener(value => {
                DesktopAudioService.SetEffectsVolume(value); view.Label(view.effectsLabel, value);
                if (Time.realtimeSinceStartup < view.previewAt) return;
                view.previewAt = Time.realtimeSinceStartup + .18f; DesktopAudioService.Play("ui_tick");
            });
        }
        Slider Row(Transform parent, string name, string label, float y, float width, out Text percent)
        {
            var row = JournalV2Nodes.Rect(parent, name, 0, y, width, 62);
            JournalV2Nodes.Text(row, "Label", label, 4, 0, width - 80, 30, 21);
            percent = JournalV2Nodes.Text(row, "Percent", "", width - 80, 0, 76, 30, 18, TextAnchor.MiddleRight);
            percent.color = JournalV2Theme.Soft;
            var hit = JournalV2Nodes.Rect(row, "Slider", 8, 28, width - 16, 32);
            var hitImage = hit.gameObject.AddComponent<Image>(); hitImage.color = Color.clear;
            var slider = hit.gameObject.AddComponent<Slider>(); slider.minValue = 0; slider.maxValue = 1;
            var track = JournalV2Nodes.Plate(hit, "Track", 0, 10, width - 16, 12, new Color32(234, 220, 196, 255), 6, 0);
            track.ShadowColor = Color.clear;
            var fillArea = JournalV2Nodes.Rect(hit, "FillArea", 0, 10, width - 16, 12);
            var fill = JournalV2Nodes.Plate(fillArea, "Fill", 0, 0, width - 16, 12, JournalV2Theme.Green, 6, 0);
            JournalV2Nodes.Stretch(fill.rectTransform);
            fill.ShadowColor = Color.clear;
            slider.fillRect = fill.rectTransform;
            var handleArea = JournalV2Nodes.Rect(hit, "HandleArea", 0, 0, width - 16, 32);
            var handle = JournalV2Nodes.Plate(handleArea, "Handle", 0, 2, 28, 28, JournalV2Theme.Paper, 14, 2);
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0, .5f);
            handle.rectTransform.pivot = new Vector2(.5f, .5f); handle.rectTransform.anchoredPosition = Vector2.zero;
            handle.rectTransform.sizeDelta = new Vector2(28, -4);
            handle.BorderColor = JournalV2Theme.Green; handle.ShadowColor = Color.clear;
            slider.handleRect = handle.rectTransform; slider.targetGraphic = handle; slider.transition = Selectable.Transition.None;
            return slider;
        }
        void Label(Text label, float value) { if (label != null) DesktopLocalization.SetText(label, (Mathf.RoundToInt(value * 100) + "%")); }
        void Refresh()
        {
            if (music == null || effects == null) return;
            music.SetValueWithoutNotify(DesktopAudioService.MusicVolume);
            effects.SetValueWithoutNotify(DesktopAudioService.EffectsVolume);
            Label(musicLabel, music.value); Label(effectsLabel, effects.value);
            backgroundBite?.SetIsOnWithoutNotify(DesktopAudioService.BackgroundBiteEnabled);
        }
        void OnEnable() { Refresh(); }
    }
}
