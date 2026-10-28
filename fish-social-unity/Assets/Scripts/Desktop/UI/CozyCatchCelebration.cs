using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Literal app.js celebrate timeline, fed only by a settled server receipt. Never grants currency/inventory.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class CozyCatchCelebration : MonoBehaviour
    {
        // The prototype caption says 1.6/1.0, but its executable sequence includes a 1.2s hold and .2s fade.
        public const float NewCatchDuration = 3.06f;
        public const float OrdinaryCatchDuration = 2.82f;
        public const int QueueLimit = 8;
        sealed class Receipt { public string Id, Name, Detail; public Sprite Fish; public bool IsNew; public float? Size; }
        readonly Queue<Receipt> queue = new Queue<Receipt>();
        readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        readonly Queue<string> seenOrder = new Queue<string>();
        readonly List<RectTransform> drops = new List<RectTransform>();
        readonly List<RectTransform> coins = new List<RectTransform>();
        readonly List<RectTransform> ripples = new List<RectTransform>();
        readonly List<Vector2> splashOffsets = new List<Vector2>();
        GameObject overlay;
        RectTransform stage, panel, fish, water, bobber, stamp;
        CanvasGroup group, dim;
        Image fishImage;
        Text title, detail;
        Receipt current;
        float elapsed;
        bool jumped;
        public RectTransform CoinsTarget { get; set; }
        public CozyCornerCharacter Character { get; set; }
        public bool IsPlaying => current != null;
        public int QueuedCount => queue.Count;
        public string CurrentCatchId => current?.Id;
        public float CurrentDuration => current != null && current.IsNew ? NewCatchDuration : OrdinaryCatchDuration;
        public event Action<string> Completed;

        public bool Enqueue(string catchId, string fishName, Sprite fishSprite, bool isCodexNew,
            string detailText = null, float? sizeM = null)
        {
            if (!string.IsNullOrEmpty(catchId) && !seen.Add(catchId)) return false;
            if (!string.IsNullOrEmpty(catchId))
            {
                seenOrder.Enqueue(catchId);
                while (seenOrder.Count > 64) seen.Remove(seenOrder.Dequeue());
            }
            while (queue.Count >= QueueLimit) queue.Dequeue();
            queue.Enqueue(new Receipt { Id = catchId, Name = fishName ?? DesktopLocalization.Text("ui.catch.celebration.fallback.fish_name", "鱼获"), Fish = fishSprite,
                IsNew = isCodexNew, Detail = detailText ?? string.Empty, Size = sizeM });
            StartNext(); return true;
        }
        void StartNext()
        {
            if (current != null || queue.Count == 0 || CozyMotion.Paused || !isActiveAndEnabled) return;
            Build(); current = queue.Dequeue(); elapsed = 0; jumped = false;
            DesktopLocalization.SetText(title, current.Name); DesktopLocalization.SetText(detail, current.Detail);
            fishImage.sprite = current.Fish; fishImage.enabled = current.Fish != null;
            var fishHeight = current.Fish != null ? 300 * current.Fish.rect.height / current.Fish.rect.width : 200;
            Place(fish, 686, 520, 300, fishHeight);
            splashOffsets.Clear();
            // The original intentionally randomizes these trajectories; do not replace them with a symmetric fan.
            for (var i = 0; i < 14; i++)
            {
                var angle = UnityEngine.Random.Range(-150f, -30f) * Mathf.Deg2Rad;
                var distance = UnityEngine.Random.Range(90f, 220f);
                splashOffsets.Add(new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * distance);
            }
            overlay.SetActive(true); overlay.transform.SetAsLastSibling();
            Character?.NotifyActivity(); Render(0);
        }
        public void Skip()
        {
            if (current == null) return;
            var id = current.Id; current = null;
            if (overlay != null) overlay.SetActive(false);
            Completed?.Invoke(id); StartNext();
        }
        public void Clear()
        {
            current = null; queue.Clear(); seen.Clear(); seenOrder.Clear();
            if (overlay != null) overlay.SetActive(false);
        }
        /// <summary>Deterministic rendering state for Editor reference captures; requires an already enqueued receipt.</summary>
        public void PreviewAt(float seconds)
        {
            if (current == null) return;
            elapsed = Mathf.Clamp(seconds, 0, CurrentDuration);
            Render(elapsed);
        }
        public void Advance(float delta)
        {
            if (CozyMotion.Paused || !isActiveAndEnabled || delta <= 0) return;
            StartNext(); if (current == null) return;
            elapsed += delta;
            if (elapsed >= CurrentDuration) { Skip(); return; }
            Render(elapsed);
            if (current.IsNew && elapsed >= 1 && !jumped)
            { jumped = true; Character?.SetPose(CozyCornerCharacter.Pose.Jump); }
        }
        static float Ease(float time, float start, float duration, CozyWebMotion.Ease easing = CozyWebMotion.Ease.OutCubic)
        { return CozyWebMotion.Evaluate(easing, (time - start) / duration); }
        static CanvasGroup Alpha(RectTransform rect)
        {
            var group = rect.GetComponent<CanvasGroup>();
            if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
            return group;
        }
        void Render(float time)
        {
            if (current == null || overlay == null) return;
            var host = (RectTransform)overlay.transform;
            var ratio = Mathf.Min(host.rect.width / 1672, host.rect.height / 941);
            stage.localScale = Vector3.one * (ratio > 0 ? ratio : 1);
            group.alpha = 1 - Ease(time, CurrentDuration - .2f, .2f);
            dim.alpha = Ease(time, 0, .16f);
            var reduced = CozyMotion.Reduced;
            var waterT = Ease(time, 0, .2f);
            Alpha(water).alpha = reduced ? 0 : waterT;
            water.localScale = Vector3.one * (reduced ? 1 : Mathf.Lerp(.8f, 1, waterT));
            bobber.anchoredPosition = Center(816, 584, 40, 58);
            bobber.localScale = Vector3.one; bobber.localRotation = Quaternion.identity;
            Alpha(bobber).alpha = reduced ? 0 : 1 - Ease(time, .18f, .12f);
            if (!reduced) bobber.anchoredPosition += Vector2.down * CozyWebMotion.Frames(Ease(time, 0, .18f, CozyWebMotion.Ease.InOutSine), 0, 12, 6);
            var cardT = Ease(time, .6f, .26f, CozyWebMotion.Ease.OutBack);
            panel.anchoredPosition = Center(586, 210, 500, 380);
            panel.localRotation = reduced ? Quaternion.identity : Quaternion.Euler(0, 0, Mathf.LerpUnclamped(-4.5f, 1.5f, cardT));
            panel.localScale = Vector3.one * (reduced ? 1 : Mathf.LerpUnclamped(1.12f, 1, cardT));
            Alpha(panel).alpha = reduced ? Ease(time, 0, CozyMotion.Fade) : Mathf.Clamp01(cardT);
            if (!reduced && current.IsNew && time >= 1.18f && time < 1.32f)
            {
                var nudge = Ease(time, 1.18f, .14f);
                panel.anchoredPosition += new Vector2(CozyWebMotion.Frames(nudge, 0, 3, -3, 0), CozyWebMotion.Frames(nudge, 0, -2, 1, 0));
            }
            var fishBase = Center(686, 520, 300, fish.sizeDelta.y);
            fish.anchoredPosition = fishBase; fish.localRotation = Quaternion.identity; fish.localScale = Vector3.one;
            if (reduced)
            {
                fish.anchoredPosition += Vector2.up * 200;
                Alpha(fish).alpha = Ease(time, 0, CozyMotion.Fade);
            }
            else if (time < .6f)
            {
                var t = Ease(time, .18f, .42f);
                var a = Mathf.Clamp01(t / .43f); var b = Mathf.Clamp01((t - .43f) / .57f);
                fish.anchoredPosition += Vector2.up * (t < .43f ? Mathf.Lerp(-100, 40, a) : Mathf.Lerp(40, 240, b));
                fish.localScale = Vector3.one * Mathf.Lerp(.5f, 1, a);
                fish.localRotation = Quaternion.Euler(0, 0, t < .43f ? Mathf.Lerp(-25, 30, a) : Mathf.Lerp(30, 390, b));
                Alpha(fish).alpha = time < .18f ? 0 : a;
            }
            else
            {
                var t = Ease(time, .6f, .4f, CozyWebMotion.Ease.OutBack);
                fish.anchoredPosition += Vector2.up * Mathf.LerpUnclamped(240, 200, t);
                fish.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(390, 364, t));
                fish.localScale = Vector3.one * Mathf.LerpUnclamped(1, 1.05f, t); Alpha(fish).alpha = 1;
            }
            stamp.gameObject.SetActive(current.IsNew);
            stamp.localScale = Vector3.one; stamp.localRotation = Quaternion.identity;
            var stampT = Ease(time, 1, .2f, CozyWebMotion.Ease.OutBack);
            Alpha(stamp).alpha = reduced ? 1 : Mathf.Clamp01(stampT);
            if (!reduced) { stamp.localScale = Vector3.one * Mathf.LerpUnclamped(1.7f, 1, stampT); stamp.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(40, 14, stampT)); }
            if (current.Size.HasValue)
            {
                var t = reduced ? 1 : Mathf.Clamp01((time - .9f) / .36f);
                DesktopLocalization.SetText(detail, DesktopLocalization.Concat((current.Size.Value * (1 - Mathf.Pow(1 - t, 3))).ToString("0.00"), " m"));
            }
            for (var i = 0; i < drops.Count; i++)
            {
                var drop = drops[i]; drop.gameObject.SetActive(!reduced && time >= .18f && time < .82f);
                if (!drop.gameObject.activeSelf) continue;
                var t = Ease(time, .18f, .64f); var endpoint = splashOffsets[i];
                var offset = t < .5f ? Vector2.Lerp(Vector2.zero, new Vector2(endpoint.x * .7f, endpoint.y), t * 2)
                    : Vector2.Lerp(new Vector2(endpoint.x * .7f, endpoint.y), new Vector2(endpoint.x, endpoint.y * .2f - 60),
                        CozyWebMotion.Evaluate(CozyWebMotion.Ease.InQuad, (t - .5f) * 2));
                drop.anchoredPosition = Center(829, 612, 14, 14) + offset;
                drop.localScale = Vector3.one * (t < .5f ? Mathf.Lerp(.6f, 1, t * 2) : Mathf.Lerp(1, .8f, (t - .5f) * 2));
                Alpha(drop).alpha = t < .5f ? 1 : 2 * (1 - t);
            }
            for (var i = 0; i < ripples.Count; i++)
            {
                var ripple = ripples[i];
                var t = Ease(time, i * .12f, .36f);
                ripple.gameObject.SetActive(!reduced && time >= i * .12f && time < i * .12f + .36f);
                ripple.localScale = Vector3.one * (reduced ? 1 : Mathf.Lerp(.4f, 1.5f, t));
                Alpha(ripple).alpha = .9f * (1 - t);
            }
            var flyStart = current.IsNew ? 1.24f : 1f;
            for (var i = 0; i < coins.Count; i++)
            {
                var coin = coins[i]; coin.gameObject.SetActive(!reduced && time >= .36f);
                if (!coin.gameObject.activeSelf) continue;
                var angle = i * Mathf.PI * 2 / 8;
                var endpoint = new Vector2(836 + Mathf.Cos(angle) * 150, -380 - Mathf.Sin(angle) * 110);
                var t = Ease(time, .36f, .36f, CozyWebMotion.Ease.OutBack);
                coin.anchoredPosition = Vector2.LerpUnclamped(new Vector2(836, -380), endpoint, t);
                coin.localScale = Vector3.one * Mathf.LerpUnclamped(.3f, 1, t); Alpha(coin).alpha = Mathf.Clamp01(t);
                if (time >= flyStart)
                {
                    var destination = CoinsTarget != null ? (Vector2)stage.InverseTransformPoint(CoinsTarget.TransformPoint(CoinsTarget.rect.center))
                        + new Vector2(836, -470.5f) : new Vector2(1236, -66);
                    var fly = Ease(time, flyStart + i * .04f, .42f, CozyWebMotion.Ease.Fly);
                    coin.anchoredPosition = Vector2.LerpUnclamped(endpoint, destination, fly);
                    coin.localScale = Vector3.one * Mathf.Lerp(1, .7f, fly);
                }
            }
        }
        void Build()
        {
            if (overlay != null) return;
            overlay = new GameObject("CatchReceiptOverlay", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
            overlay.transform.SetParent(transform, false);
            var root = (RectTransform)overlay.transform; root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            var hit = overlay.GetComponent<Image>(); hit.color = Color.clear;
            var skip = overlay.GetComponent<Button>(); skip.targetGraphic = hit; skip.transition = Selectable.Transition.None; skip.onClick.AddListener(Skip);
            group = overlay.GetComponent<CanvasGroup>();
            var shade = Node("Dim", root); shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero;
            var background = shade.gameObject.AddComponent<Image>(); background.color = new Color(60f / 255, 38f / 255, 18f / 255, .42f); background.raycastTarget = false; dim = Alpha(shade);
            stage = Node("Stage", root); stage.sizeDelta = new Vector2(1672, 941);
            water = Shape("Water", stage, 536, 600, 600, 120, CozyCssGraphic.Shape.Water, Color.white);
            for (var i = 0; i < 2; i++)
            {
                var ripple = Shape(DesktopLocalization.Concat("Ripple", i), stage, 756, 617, 160, 46, CozyCssGraphic.Shape.Ring, new Color(1, 1, 1, .85f));
                ripple.GetComponent<CozyCssGraphic>().border = 2;
                ripples.Add(ripple);
            }
            bobber = Picture("Bobber", stage, 816, 584, 40, 58, CozyMotion.BobberSprite());
            panel = Node("Paper", stage); Place(panel, 586, 210, 500, 380);
            Picture("OriginalPaperAndShadow", panel, -80, -80, 660, 540, CozyJournalTheme.Sprite("motion/celebration-paper"));
            title = Label("Title", panel, 0, 268, 500, 54, 36); detail = Label("Size", panel, 0, 318, 500, 40, 24); detail.color = new Color(117f / 255, 71f / 255, 46f / 255, .68f);
            stamp = Shape("NewStamp", panel, 418, -30, 108, 108, CozyCssGraphic.Shape.Stamp, new Color(205f / 255, 64f / 255, 46f / 255, .9f)); stamp.GetComponent<CozyCssGraphic>().border = 5;
            stamp.GetComponent<CozyCssGraphic>().material = Resources.Load<Material>(DesktopLocalization.Concat(CozyJournalTheme.ResourceRoot, "materials/PaperMultiply"));
            var stampText = Label("Caption", stamp, 0, 0, 108, 108, 34); DesktopLocalization.SetText(stampText, DesktopLocalization.Text("ui.catch.celebration.badge.new", "新")); stampText.material = Resources.Load<Material>(DesktopLocalization.Concat(CozyJournalTheme.ResourceRoot, "materials/PaperMultiply")); stampText.color = new Color(205f / 255, 64f / 255, 46f / 255, .92f);
            fish = Picture("Fish", stage, 686, 520, 300, 200, null); fishImage = fish.GetComponent<Image>();
            var hint = Label("Skip", stage, 586, 879, 500, 28, 18); DesktopLocalization.SetText(hint, DesktopLocalization.Text("ui.catch.celebration.hint.skip", "点击任意位置跳过")); hint.color = new Color(1, 246f / 255, 230f / 255, .8f);
            for (var i = 0; i < 14; i++) drops.Add(Shape(DesktopLocalization.Concat("Drop", i), stage, 829, 612, 14, 14, CozyCssGraphic.Shape.Circle, new Color(191f / 255, 230f / 255, 251f / 255)));
            var coinSprite = Resources.Load<Sprite>("Desktop/Theme/Cozy/icons/coin");
            for (var i = 0; i < 8; i++) coins.Add(Picture(DesktopLocalization.Concat("Coin", i), stage, 819, 363, 34, 34, coinSprite));
        }
        static Vector2 Center(float x, float y, float width, float height) { return new Vector2(x + width / 2, -y - height / 2); }
        static RectTransform Node(string name, Transform parent)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform; }
        static void Place(RectTransform rect, float x, float y, float width, float height)
        { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = Center(x, y, width, height); }
        static RectTransform Shape(string name, Transform parent, float x, float y, float width, float height, CozyCssGraphic.Shape kind, Color color)
        {
            var rect = Node(name, parent); Place(rect, x, y, width, height); var shape = rect.gameObject.AddComponent<CozyCssGraphic>();
            shape.shape = kind; shape.color = color; shape.raycastTarget = false; return rect;
        }
        static RectTransform Picture(string name, Transform parent, float x, float y, float width, float height, Sprite sprite)
        {
            var rect = Node(name, parent); Place(rect, x, y, width, height); var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false; return rect;
        }
        static Text Label(string name, Transform parent, float x, float y, float width, float height, int fontSize)
        {
            var rect = Node(name, parent); Place(rect, x, y, width, height); var text = rect.gameObject.AddComponent<Text>();
            text.font = CatTheme.Font; text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter; text.color = CatTheme.Ink;
            text.supportRichText = false; text.raycastTarget = false; return text;
        }
        void OnEnable()
        {
            CozyMotion.ReducedChanged -= OnReduced; CozyMotion.PausedChanged -= OnPaused;
            CozyMotion.ReducedChanged += OnReduced; CozyMotion.PausedChanged += OnPaused;
            if (current != null && overlay != null) overlay.SetActive(!CozyMotion.Paused);
        }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnDisable()
        { CozyMotion.ReducedChanged -= OnReduced; CozyMotion.PausedChanged -= OnPaused; if (overlay != null) overlay.SetActive(false); }
        void OnReduced(bool reduced) { Render(elapsed); }
        void OnPaused(bool paused) { if (overlay != null) overlay.SetActive(!paused && current != null); if (!paused) StartNext(); }
    }
}
