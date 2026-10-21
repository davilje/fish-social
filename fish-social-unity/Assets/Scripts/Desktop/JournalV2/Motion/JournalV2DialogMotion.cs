using UnityEngine;

namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2DialogMotion : MonoBehaviour
    {
        [SerializeField] RectTransform paper;
        [SerializeField] CanvasGroup group;
        [SerializeField] Vector2 rest;
        float elapsed;
        bool playing;
        public void Configure(RectTransform paper)
        {
            this.paper = paper; if (paper == null) return; rest = paper.anchoredPosition;
            group = paper.GetComponent<CanvasGroup>(); if (group == null) group = paper.gameObject.AddComponent<CanvasGroup>();
            Finish();
        }
        public void Open() { elapsed = 0; playing = true; Sample(0); }
        public void Advance(float delta) { if (playing && !JournalV2MotionState.Paused) Sample(elapsed+Mathf.Max(0,delta)); }
        public void Sample(float seconds)
        {
            elapsed = Mathf.Max(0,seconds);
            var duration = JournalV2MotionState.Reduced ? .12f : .2f;
            var t = JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutCubic,elapsed/duration);
            playing = elapsed < duration;
            if (paper != null) paper.anchoredPosition = rest+(JournalV2MotionState.Immediate ? Vector2.zero : Vector2.down*16*(1-t));
            if (group != null) group.alpha = t;
        }
        public void Finish() { playing = false; if (paper != null) paper.anchoredPosition = rest; if (group != null) group.alpha = 1; }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnEnable() { JournalV2MotionState.Changed += StateChanged; }
        void OnDisable() { JournalV2MotionState.Changed -= StateChanged; Finish(); }
        void StateChanged() { if (JournalV2MotionState.Immediate) Finish(); }
    }
}
