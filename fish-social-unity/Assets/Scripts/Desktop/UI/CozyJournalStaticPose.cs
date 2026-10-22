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
        bool armed;
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

        public static void RevealAside(Transform page)
        {
            if (page == null) return;
            foreach (var pose in page.GetComponentsInChildren<CozyJournalStaticPose>(true))
            {
                if (!IsDetailPolaroid(pose.gameObject.name))
                    continue;
                pose.PlayReveal(new Vector2(24f, 0f), .26f, .3f);
            }
        }

        public void PlayReveal(Vector2 offset = default, float duration = .22f, float initialOpacity = .3f)
        {
            armed = true;
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
            revealDuration = Mathf.Max(.001f, armedDuration);
            revealOpacity = Mathf.Clamp01(armedOpacity);
            elapsed = 0;
            revealing = !CozyMotion.Reduced && !CozyMotion.Paused;
            Refresh();
        }

        bool HierarchyVisible()
        {
            if (!isActiveAndEnabled) return false;
            var node = transform;
            while (node != null)
            {
                var group = node.GetComponent<CanvasGroup>();
                // Only a fully concealed sheet holds the reveal. A door that is already
                // fading in has to carry the polaroid with the cards; waiting until the
                // sheet was opaque played the photo again after the page had arrived.
                if (group != null && group.alpha <= .001f) return false;
                node = node.parent;
            }
            return true;
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
        bool StickerOwnsPose()
        {
            return Application.isPlaying && GetComponent<CozyStickerMotion>() != null;
        }

        void OnEnable()
        {
            CozyMotion.ReducedChanged -= OnReduced; CozyMotion.PausedChanged -= OnPaused;
            CozyMotion.ReducedChanged += OnReduced; CozyMotion.PausedChanged += OnPaused;
            if (Application.isPlaying && armed) BeginReveal();
            else if (!StickerOwnsPose()) Refresh();
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
            // Page flips fade the aside while this 0.26s snap would otherwise finish unseen.
            if (!HierarchyVisible())
            {
                elapsed = 0;
                Refresh();
                return;
            }
            Advance(Time.unscaledDeltaTime);
        }
        void LateUpdate() { if (revealing || !StickerOwnsPose()) Refresh(); }
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
