using System.Collections;
using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>app.js switchTab/entrance: paper and index rail stay fixed; content fades independently.</summary>
    [DisallowMultipleComponent]
    public sealed class CozyJournalPageMotion : MonoBehaviour
    {
        [SerializeField] CanvasGroup heading;
        [SerializeField] CanvasGroup aside;
        [SerializeField] CanvasGroup status;
        [SerializeField] CanvasGroup[] asideParts;
        [SerializeField] CanvasGroup[] middle;
        float elapsed;
        int count;
        int phase;
        public float HeadingAlpha => heading != null ? heading.alpha : 1;
        public float AsideAlpha => aside != null ? aside.alpha : 1;

        public void Configure(Transform title, Transform detail, Transform footer, params Transform[] content)
        {
            heading = Group(title); aside = Group(detail); status = Group(footer); asideParts = null;
            middle = new CanvasGroup[content != null ? content.Length : 0];
            for (var i = 0; i < middle.Length; i++) middle[i] = Group(content[i]);
        }
        public void ConfigureAside(params Transform[] parts)
        {
            aside = null;
            asideParts = new CanvasGroup[parts != null ? parts.Length : 0];
            for (var i = 0; i < asideParts.Length; i++) asideParts[i] = Group(parts[i]);
        }
        void AsideAlphaTo(float alpha)
        {
            Alpha(aside, alpha);
            foreach (var group in asideParts ?? new CanvasGroup[0]) Alpha(group, alpha);
        }
        static CanvasGroup Group(Transform target)
        {
            if (target == null) return null;
            var group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.gameObject.AddComponent<CanvasGroup>();
        }
        static void Alpha(CanvasGroup group, float alpha)
        {
            if (group == null) return;
            group.alpha = Mathf.Clamp01(alpha);
            group.blocksRaycasts = alpha > .001f;
        }
        public void SampleExit(float seconds)
        {
            var alpha = 1 - Mathf.Clamp01(seconds / .1f);
            Alpha(heading, alpha); AsideAlphaTo(alpha); Alpha(status, alpha);
            foreach (var group in middle ?? new CanvasGroup[0]) Alpha(group, alpha);
        }
        public void SampleEntrance(float seconds, int stickerCount)
        {
            var fade = CozyMotion.Reduced ? .12f : .16f;
            var delay = CozyMotion.Reduced ? 0 : .08f + Mathf.Min(8, stickerCount) * .02f;
            Alpha(heading, seconds / fade); AsideAlphaTo((seconds - delay) / fade); Alpha(status, 1);
            foreach (var group in middle ?? new CanvasGroup[0]) Alpha(group, 1);
        }
        public IEnumerator Exit()
        {
            phase = 0;
            var time = 0f;
            SampleExit(0);
            while (time < .1f)
            {
                yield return null;
                if (!CozyMotion.Paused) time += Time.unscaledDeltaTime;
                SampleExit(time);
            }
        }
        public void PlayEntrance(int stickerCount)
        {
            elapsed = 0; count = stickerCount; phase = 1; SampleEntrance(0, count);
        }
        public void Advance(float seconds)
        {
            if (phase == 0 || CozyMotion.Paused || CozyMotion.EntranceHeld) return;
            elapsed += Mathf.Max(0, seconds); SampleEntrance(elapsed, count);
            if (elapsed >= .4f) phase = 0;
        }
        public void Finish()
        {
            phase = 0; SampleEntrance(1, 0);
        }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnEnable()
        {
            CozyMotion.ReducedChanged+=OnReduced;
            CozyMotion.PausedChanged+=OnPaused;
        }
        void OnReduced(bool value) { if(value) Finish(); }
        void OnPaused(bool value) { if(value) Finish(); }
        void OnDisable()
        {
            CozyMotion.ReducedChanged-=OnReduced;
            CozyMotion.PausedChanged-=OnPaused;
            Finish();
        }
    }
}
