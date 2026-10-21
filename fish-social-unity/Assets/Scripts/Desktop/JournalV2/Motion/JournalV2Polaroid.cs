using UnityEngine;

namespace FishSocial.Desktop.JournalV2
{
    [DisallowMultipleComponent]
    public sealed class JournalV2Polaroid : MonoBehaviour
    {
        [SerializeField] RectTransform visual;
        [SerializeField] CanvasGroup group;
        [SerializeField] Vector2 rest, offset;
        [SerializeField] float angle, elapsed, duration = .22f, initialAlpha = .3f;
        bool playing;
        public float CssAngle => angle;
        public Vector2 VisualOffset { get; private set; }
        public float VisualScale { get; private set; } = 1;
        public float Opacity { get; private set; } = 1;
        public bool IsPlaying => playing;
        public void Configure(RectTransform visual, float cssAngle = -2)
        {
            this.visual = visual; angle = cssAngle;
            if (visual == null) return;
            rest = visual.anchoredPosition;
            group = visual.GetComponent<CanvasGroup>(); if (group == null) group = visual.gameObject.AddComponent<CanvasGroup>();
            Finish();
        }
        public void PlayReveal(Vector2 offset = default(Vector2), float duration = .22f, float alpha = .3f)
        {
            this.offset = offset; this.duration = Mathf.Max(.001f,duration); initialAlpha = Mathf.Clamp01(alpha);
            elapsed = 0; playing = !JournalV2MotionState.Immediate;
            if (!playing) Finish(); else Render(0);
        }
        public void Advance(float delta)
        {
            if (!playing || JournalV2MotionState.Paused) return;
            Sample(elapsed + Mathf.Max(0,delta));
        }
        public void Sample(float seconds)
        {
            elapsed = Mathf.Max(0,seconds); playing = elapsed < duration && !JournalV2MotionState.Immediate;
            Render(playing ? JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutBack,elapsed/duration) : 1);
        }
        public void Finish() { playing = false; elapsed = duration; Render(1); }
        void Render(float t)
        {
            if (visual == null) return;
            VisualScale = Mathf.LerpUnclamped(.9f,1,t); Opacity = Mathf.Clamp01(Mathf.LerpUnclamped(initialAlpha,1,t));
            VisualOffset = offset*(1-t);
            var center = visual.rect.center;
            var rotated = Quaternion.Euler(0,0,-angle)*((Vector3)center*VisualScale);
            visual.anchoredPosition = rest+VisualOffset+center-(Vector2)rotated;
            visual.localScale = Vector3.one*VisualScale; visual.localRotation = Quaternion.Euler(0,0,-angle);
            if (group != null) group.alpha = Opacity;
        }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnEnable() { JournalV2MotionState.Changed += StateChanged; }
        void OnDisable() { JournalV2MotionState.Changed -= StateChanged; Finish(); }
        void StateChanged() { if (JournalV2MotionState.Immediate) Finish(); }
    }
}
