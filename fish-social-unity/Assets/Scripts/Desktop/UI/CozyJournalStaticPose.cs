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
        Vector2 armedOffset;
        float armedDuration = .26f;
        float armedOpacity = .3f;
        public float CssAngle => cssAngle;
        public float AppliedScale { get; private set; } = 1;
        public float AppliedOpacity { get; private set; } = 1;
        public Vector2 AppliedOffset { get; private set; }
        public bool IsRevealing => revealing;
        public void Configure(float angle, params Graphic[] targets)
        {
            cssAngle = angle; graphics = targets; Refresh();
        }
        public static bool IsDetailPolaroid(string node)
        {
            return node == "JournalPolaroid" || node == "JournalBagPhoto" ||
                node == "JournalCollectionPhoto" || node == "JournalPhoto" ||
                node == "PhotoFrame";
        }

        public static void RevealAside(Transform page, bool stationary = false)
        {
            if (page == null) return;
            foreach (var pose in page.GetComponentsInChildren<CozyJournalStaticPose>(true))
            {
                if (!IsDetailPolaroid(pose.gameObject.name))
                    continue;
                pose.PlayReveal(stationary ? Vector2.zero : new Vector2(24f, 0f), stationary ? CozyMotion.Snap : .26f, .3f);
            }
        }

        public void PlayReveal(Vector2 offset = default, float duration = .22f, float initialOpacity = .3f)
        {
            armedOffset = offset;
            armedDuration = duration;
            armedOpacity = initialOpacity;
            BeginReveal();
        }

        void BeginReveal()
        {
            if (graphics == null || graphics.Length == 0)
                graphics = GetComponentsInChildren<Graphic>(true);
            revealOffset = armedOffset;
            revealDuration = Mathf.Max(.001f, CozyMotion.Reduced ? Mathf.Min(CozyMotion.Fade, armedDuration) : armedDuration);
            revealOpacity = Mathf.Clamp01(armedOpacity);
            elapsed = 0;
            revealing = !CozyMotion.Paused;
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
            revealing = elapsed < revealDuration && !CozyMotion.Paused;
            Refresh();
        }
        public void ApplyFinalPose() { revealing = false; elapsed = revealDuration; Refresh(); }
        bool StickerOwnsPose()
        {
            return Application.isPlaying && GetComponent<CozyStickerMotion>() != null;
        }

        void OnEnable()
        {
            CozyMotion.ReducedChanged -= OnReduced; CozyMotion.PausedChanged -= OnPaused;
            CozyMotion.ReducedChanged += OnReduced; CozyMotion.PausedChanged += OnPaused;
            if (!StickerOwnsPose()) Refresh();
        }
        void OnDisable()
        {
            CozyMotion.ReducedChanged -= OnReduced; CozyMotion.PausedChanged -= OnPaused;
            revealing = false;
            if (!Application.isPlaying) ApplyFinalPose();
        }
        void Update()
        {
            if (!Application.isPlaying || !revealing) return;
            // Selection and parent opacity animate concurrently, as in Web Animations.
            // Only the explicit paper cover holds content; ancestor alpha is not a clock.
            if (CozyMotion.EntranceHeld) return;
            Advance(Time.unscaledDeltaTime);
        }
        void LateUpdate() { if (revealing || !StickerOwnsPose()) Refresh(); }
        void OnReduced(bool value) { if (value) ApplyFinalPose(); }
        void OnPaused(bool value) { if (value) ApplyFinalPose(); }
        public void Refresh()
        {
            if (graphics == null) return;
            var t = revealing ? CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutBack, elapsed / revealDuration) : 1;
            AppliedScale = CozyMotion.Reduced ? 1 : Mathf.LerpUnclamped(.9f, 1, t);
            AppliedOpacity = Mathf.Clamp01(Mathf.LerpUnclamped(revealOpacity, 1, t));
            AppliedOffset = CozyMotion.Reduced ? Vector2.zero : revealOffset * (1 - t);
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
