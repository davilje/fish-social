using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Visual-only receipt of an already settled catch. It never awards currency or changes inventory.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class CozyCatchCelebration : MonoBehaviour
    {
        public const float NewCatchDuration = 1.6f;
        public const float OrdinaryCatchDuration = 1f;
        public const int QueueLimit = 8;
        sealed class Receipt
        {
            public string Id;
            public string Name;
            public string Detail;
            public Sprite Fish;
            public bool IsNew;
        }
        readonly Queue<Receipt> queue = new Queue<Receipt>();
        readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        readonly Queue<string> seenOrder = new Queue<string>();
        readonly List<RectTransform> particles = new List<RectTransform>();
        GameObject overlay;
        RectTransform panel;
        RectTransform fish;
        RectTransform rays;
        CanvasGroup group;
        Image fishImage;
        Text title;
        Text detail;
        Receipt current;
        float elapsed;
        public bool IsPlaying => current != null;
        public int QueuedCount => queue.Count;
        public string CurrentCatchId => current?.Id;
        public float CurrentDuration => current != null && current.IsNew ? NewCatchDuration : OrdinaryCatchDuration;
        public event Action<string> Completed;

        public bool Enqueue(string catchId, string fishName, Sprite fishSprite, bool isCodexNew, string detailText = null)
        {
            if (!string.IsNullOrEmpty(catchId) && !seen.Add(catchId)) return false;
            if (!string.IsNullOrEmpty(catchId))
            {
                seenOrder.Enqueue(catchId);
                while (seenOrder.Count > 64) seen.Remove(seenOrder.Dequeue());
            }
            // Notification queue only: old hidden receipts may be coalesced; business state is already settled.
            while (queue.Count >= QueueLimit) queue.Dequeue();
            queue.Enqueue(new Receipt { Id = catchId, Name = fishName ?? "鱼获", Fish = fishSprite,
                IsNew = isCodexNew, Detail = detailText ?? string.Empty });
            StartNext();
            return true;
        }

        void StartNext()
        {
            if (current != null || queue.Count == 0 || CozyMotion.Paused || !isActiveAndEnabled) return;
            Build();
            current = queue.Dequeue();
            elapsed = 0;
            title.text = current.IsNew ? "新图鉴 · " + current.Name : "钓到了 · " + current.Name;
            detail.text = current.Detail;
            fishImage.sprite = current.Fish;
            fishImage.enabled = current.Fish != null;
            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();
            Render(0);
        }

        public void Skip()
        {
            if (current == null) return;
            var id = current.Id;
            current = null;
            if (overlay != null) overlay.SetActive(false);
            Completed?.Invoke(id);
            StartNext();
        }

        public void Clear()
        {
            current = null;
            queue.Clear();
            seen.Clear();
            seenOrder.Clear();
            if (overlay != null) overlay.SetActive(false);
        }

        public void Advance(float delta)
        {
            if (CozyMotion.Paused || !isActiveAndEnabled || delta <= 0) return;
            StartNext();
            if (current == null) return;
            elapsed += delta;
            if (elapsed >= CurrentDuration) { Skip(); return; }
            Render(elapsed);
        }

        void Render(float time)
        {
            if (current == null || overlay == null) return;
            var duration = CurrentDuration;
            var enter = Mathf.Clamp01(time / (CozyMotion.Reduced ? CozyMotion.Fade : .24f));
            var exit = Mathf.Clamp01((duration - time) / CozyMotion.Fade);
            group.alpha = Mathf.Min(enter, exit);
            panel.localScale = Vector3.one;
            panel.anchoredPosition = new Vector2(0, 10);
            panel.localRotation = Quaternion.identity;
            fish.anchoredPosition = new Vector2(0, 12);
            fish.localRotation = Quaternion.identity;
            rays.localRotation = Quaternion.identity;
            rays.gameObject.SetActive(current.IsNew && !CozyMotion.Reduced);
            if (!CozyMotion.Reduced)
            {
                var spring = 1f - Mathf.Pow(1f - enter, 3f);
                panel.localScale = Vector3.one * Mathf.Lerp(.92f, 1f, spring);
                panel.anchoredPosition += Vector2.up * ((1f - spring) * -28f);
                var reveal = Mathf.Clamp01(time / .6f);
                fish.anchoredPosition += Vector2.up * (Mathf.Sin(reveal * Mathf.PI) * 24f);
                fish.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(reveal * Mathf.PI * 2f) * 8f);
                rays.localRotation = Quaternion.Euler(0, 0, time * 18f);
            }
            for (var i = 0; i < particles.Count; i++)
            {
                var particle = particles[i];
                particle.gameObject.SetActive(current.IsNew && !CozyMotion.Reduced);
                if (!particle.gameObject.activeSelf) continue;
                var t = Mathf.Clamp01((time - .18f) / .9f);
                var angle = i * (360f / particles.Count) * Mathf.Deg2Rad;
                particle.anchoredPosition = new Vector2(Mathf.Cos(angle) * 180f * t,
                    Mathf.Sin(angle) * 125f * t - 70f * t * t + 20f);
                particle.localRotation = Quaternion.Euler(0, 0, i * 31f + t * 180f);
                var image = particle.GetComponent<Image>();
                image.color = new Color(1, 1, 1, Mathf.Clamp01(1f - t));
            }
        }

        void Build()
        {
            if (overlay != null) return;
            overlay = new GameObject("CatchReceiptOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(CanvasGroup));
            overlay.transform.SetParent(transform, false);
            var root = (RectTransform)overlay.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            var hit = overlay.GetComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;
            var skip = overlay.GetComponent<Button>();
            skip.targetGraphic = hit;
            skip.transition = Selectable.Transition.None;
            skip.onClick.AddListener(Skip);
            group = overlay.GetComponent<CanvasGroup>();
            panel = Image("Paper", root, "surfaces/sticker-card", new Vector2(400, 304));
            CozyJournalTheme.ApplySurface(panel.GetComponent<Image>(), "surfaces/sticker-card");
            rays = Image("AwardRays", panel, "motion/award-rays", new Vector2(260, 240));
            rays.anchoredPosition = new Vector2(0, 12);
            fish = Image("Fish", panel, null, new Vector2(160, 112));
            fishImage = fish.GetComponent<Image>();
            fishImage.preserveAspect = true;
            title = Label("Title", panel, new Vector2(0, 112), new Vector2(360, 42), 24);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 16;
            title.resizeTextMaxSize = 24;
            detail = Label("Receipt", panel, new Vector2(0, -70), new Vector2(352, 66), 17);
            var hint = Label("Skip", panel, new Vector2(0, -126), new Vector2(320, 24), 13);
            hint.text = "点击跳过";
            hint.color = new Color(.55f, .42f, .31f);
            for (var i = 0; i < 9; i++)
                particles.Add(Image("Confetti" + i, panel, "motion/paper-confetti-" + (i % 3 + 1), new Vector2(16, 22)));
            title.transform.SetAsLastSibling();
            detail.transform.SetAsLastSibling();
            hint.transform.SetAsLastSibling();
        }

        static RectTransform Image(string name, Transform parent, string path, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.sprite = !string.IsNullOrEmpty(path) ? CozyJournalTheme.Sprite(path) : null;
            image.color = Color.white;
            return rect;
        }

        static Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
            text.rectTransform.sizeDelta = size;
            text.rectTransform.anchoredPosition = position;
            text.font = CatTheme.Font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = CatTheme.Ink;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        void OnEnable()
        {
            CozyMotion.ReducedChanged -= OnReduced;
            CozyMotion.PausedChanged -= OnPaused;
            CozyMotion.ReducedChanged += OnReduced;
            CozyMotion.PausedChanged += OnPaused;
            if (current != null && overlay != null) overlay.SetActive(!CozyMotion.Paused);
        }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnDisable()
        {
            CozyMotion.ReducedChanged -= OnReduced;
            CozyMotion.PausedChanged -= OnPaused;
            if (overlay != null) overlay.SetActive(false);
        }
        void OnReduced(bool reduced) { Render(elapsed); }
        void OnPaused(bool paused)
        {
            if (overlay != null) overlay.SetActive(!paused && current != null);
            if (!paused) StartNext();
        }
    }
}
