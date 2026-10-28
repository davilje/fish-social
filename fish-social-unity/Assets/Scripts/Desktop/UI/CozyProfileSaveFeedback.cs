using System;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Keep the successful save visible for 240ms before the same-sheet return.</summary>
    public sealed class CozyProfileSaveFeedback : MonoBehaviour
    {
        Text label;
        string original;
        CanvasGroup alpha;
        bool playing;
        public static void Play(Button button, Action completed)
        {
            if (button == null) { completed?.Invoke(); return; }
            var motion = button.GetComponent<CozyProfileSaveFeedback>() ?? button.gameObject.AddComponent<CozyProfileSaveFeedback>();
            motion.Begin(completed);
        }
        void Begin(Action completed)
        {
            if (playing) return;
            label = GetComponentInChildren<Text>();
            if (label == null) { completed?.Invoke(); return; }
            original = label.text;
            label.text = DesktopLocalization.Text("ui.profile.save_feedback.status.saved", "✓ 已保存");
            alpha = label.GetComponent<CanvasGroup>();
            if (alpha == null) alpha = label.gameObject.AddComponent<CanvasGroup>();
            playing = true;
            Sample(0);
            CozyMotion.Animate((RectTransform)transform, .24f, Sample, () =>
            {
                playing = false;
                Sample(1);
                completed?.Invoke();
            }, cleanup: Restore, fadeWhenReduced: true);
        }
        void Sample(float t) { if (alpha != null) alpha.alpha = .35f + .65f*t; }
        void Restore()
        {
            playing = false;
            if (label != null) label.text = original;
            Sample(1);
        }
        public void CancelFeedback() { CozyMotion.Cancel((RectTransform)transform); Restore(); }
        void OnDisable() { CancelFeedback(); }
    }
}
