using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>One owner composes the permanent CSS tilt with the prototype's optional polaroid reveal.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CozyJournalStaticPose : MonoBehaviour
    {
        [SerializeField] Graphic[] graphics;
        [SerializeField] float cssAngle;
        Vector2 revealOffset;
        float revealDuration = .22f;
        float revealOpacity = .3f;
        float elapsed;
        bool revealing;
        public float CssAngle => cssAngle;
        public float AppliedScale { get; private set; } = 1;
        public float AppliedOpacity { get; private set; } = 1;
        public Vector2 AppliedOffset { get; private set; }
        public bool IsRevealing => revealing;
        public void Configure(float angle, params Graphic[] targets)
        {
            cssAngle = angle; graphics = targets; Refresh();
        }
        public void PlayReveal(Vector2 offset = default, float duration = .22f, float initialOpacity = .3f)
        {
            revealOffset = offset; revealDuration = Mathf.Max(.001f, duration);
            revealOpacity = Mathf.Clamp01(initialOpacity); elapsed = 0;
            revealing = !CozyMotion.Reduced && !CozyMotion.Paused;
            Refresh();
        }
        public void Advance(float delta)
        {
            if (!revealing || CozyMotion.Paused || delta <= 0) return;
            elapsed = Mathf.Min(revealDuration, elapsed + delta);
            if (elapsed >= revealDuration) revealing = false;
            Refresh();
        }
        public void SampleReveal(float seconds)
        {
            elapsed = Mathf.Clamp(seconds, 0, revealDuration);
            revealing = elapsed < revealDuration && !CozyMotion.Reduced && !CozyMotion.Paused;
            Refresh();
        }
        public void ApplyFinalPose() { revealing = false; elapsed = revealDuration; Refresh(); }
        void OnEnable()
        {
            CozyMotion.ReducedChanged -= OnReduced; CozyMotion.PausedChanged -= OnPaused;
            CozyMotion.ReducedChanged += OnReduced; CozyMotion.PausedChanged += OnPaused; Refresh();
        }
        void OnDisable()
        {
            CozyMotion.ReducedChanged -= OnReduced; CozyMotion.PausedChanged -= OnPaused; ApplyFinalPose();
        }
        void Update() { if (Application.isPlaying) Advance(Time.unscaledDeltaTime); }
        void LateUpdate() { Refresh(); }
        void OnReduced(bool value) { if (value) ApplyFinalPose(); }
        void OnPaused(bool value) { if (value) ApplyFinalPose(); }
        public void Refresh()
        {
            if (graphics == null) return;
            var t = revealing ? CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutBack, elapsed / revealDuration) : 1;
            AppliedScale = Mathf.LerpUnclamped(.9f, 1, t);
            AppliedOpacity = Mathf.Clamp01(Mathf.LerpUnclamped(revealOpacity, 1, t));
            AppliedOffset = revealOffset * (1 - t);
            foreach (var graphic in graphics)
            {
                if (graphic == null) continue;
                var effect = graphic.GetComponent<CozyGraphicOffset>();
                if (effect == null) effect = graphic.gameObject.AddComponent<CozyGraphicOffset>();
                effect.SetJournalPose(transform, AppliedOffset, -cssAngle, AppliedScale, AppliedOpacity);
            }
        }
    }
}
