using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Finite, nonblocking acknowledgement. Call only from the actual API result callback.</summary>
    [DisallowMultipleComponent]
    public sealed class CozyActionFeedback : MonoBehaviour
    {
        RectTransform stamp;
        CanvasGroup group;
        Image ring;
        Text label;
        float time;
        bool success;
        public static void Play(Transform target, bool succeeded, string caption)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return;
            var feedback = target.GetComponent<CozyActionFeedback>() ?? target.gameObject.AddComponent<CozyActionFeedback>();
            feedback.Show(succeeded, caption);
        }
        void Show(bool succeeded, string caption)
        {
            if (stamp == null)
            {
                var go = new GameObject("JournalActionStamp", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                go.transform.SetParent(transform, false);
                stamp = go.GetComponent<RectTransform>();
                stamp.anchorMin = stamp.anchorMax = new Vector2(1, 1);
                stamp.pivot = new Vector2(1, 1);
                stamp.sizeDelta = new Vector2(80, 80);
                ring = go.GetComponent<Image>();
                ring.sprite = CozyJournalTheme.Sprite("decor/stamp-ring");
                ring.raycastTarget = false;
                group = go.GetComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                var textGo = new GameObject("Caption", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(stamp, false);
                label = textGo.GetComponent<Text>();
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                label.font = CatTheme.Font;
                label.fontSize = 16;
                label.alignment = TextAnchor.MiddleCenter;
                label.raycastTarget = false;
                label.supportRichText = false;
            }
            success = succeeded;
            time = 0;
            label.text = caption;
            ring.color = label.color = succeeded ? new Color(.35f, .58f, .35f) : new Color(.78f, .28f, .22f);
            stamp.gameObject.SetActive(!CozyMotion.Paused);
            stamp.SetAsLastSibling();
            Render();
        }
        public void Advance(float delta)
        {
            if (stamp == null || !stamp.gameObject.activeSelf || CozyMotion.Paused) return;
            time += Mathf.Max(0, delta);
            if (time >= CozyMotion.Celebrate) { stamp.gameObject.SetActive(false); return; }
            Render();
        }
        void Render()
        {
            if (stamp == null) return;
            var enter = Mathf.Clamp01(time / CozyMotion.Fade);
            group.alpha = Mathf.Min(enter, Mathf.Clamp01((CozyMotion.Celebrate - time) / CozyMotion.Fade));
            stamp.localScale = Vector3.one;
            stamp.localRotation = Quaternion.identity;
            stamp.anchoredPosition = new Vector2(-12, -12);
            if (CozyMotion.Reduced) return;
            if (success)
            {
                stamp.localScale = Vector3.one * Mathf.Lerp(1.35f, 1, 1 - Mathf.Pow(1 - enter, 3));
                stamp.localRotation = Quaternion.Euler(0, 0, -8);
            }
            else
                stamp.anchoredPosition += Vector2.right * Mathf.Sin(Mathf.Clamp01(time / .24f) * Mathf.PI * 4) * 4f;
        }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnEnable() { CozyMotion.ReducedChanged += OnReduced; CozyMotion.PausedChanged += OnPaused; }
        void OnDisable()
        {
            CozyMotion.ReducedChanged -= OnReduced;
            CozyMotion.PausedChanged -= OnPaused;
            if (stamp != null) stamp.gameObject.SetActive(false);
        }
        void OnReduced(bool reduced) { Render(); }
        void OnPaused(bool paused) { if (paused && stamp != null) stamp.gameObject.SetActive(false); }
    }
}
