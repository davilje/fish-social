using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Original stampOn/flyTo acknowledgements, invoked only after an actual successful API result.</summary>
    [DisallowMultipleComponent]
    public sealed class CozyActionFeedback : MonoBehaviour
    {
        RectTransform stamp;
        CanvasGroup group;
        Text label;
        float time;
        public static void Play(Transform target, bool succeeded, string caption, bool purchaseAnimation = false)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return;
            DesktopAudioService.Play(succeeded ? "ui_success" : "ui_error");
            if (!succeeded)
            {
                CozyMotion.Shake(target as RectTransform);
                return;
            }
            var canvas = target.GetComponentInParent<Canvas>();
            var host = canvas != null ? canvas.transform.Find("MainRoot") : null;
            if (purchaseAnimation && host != null)
            {
                var source = target.Find("DesignArt")?.GetComponent<Image>();
                var destination = host.Find("Nav/Equipment/Icon") as RectTransform;
                if (source != null && destination != null)
                {
                    FlyTo(source, destination, host as RectTransform);
                    host.GetComponentInChildren<CozyCornerCharacter>(true)?.SetPose(CozyCornerCharacter.Pose.Nod);
                    return;
                }
            }
            var feedback = target.GetComponent<CozyActionFeedback>();
            if (feedback == null) feedback = target.gameObject.AddComponent<CozyActionFeedback>();
            feedback.Show(caption);
        }
        public static void FlyTo(Image source, RectTransform destination, RectTransform layer)
        {
            if (source == null || source.sprite == null || destination == null || layer == null || CozyMotion.Reduced || CozyMotion.Paused) return;
            var go = new GameObject("PurchasedItemFlight", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(layer, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = layer.pivot;
            rect.sizeDelta = new Vector2(110, 110);
            var image = go.GetComponent<Image>(); image.sprite = source.sprite; image.preserveAspect = true; image.raycastTarget = false;
            var alpha = go.GetComponent<CanvasGroup>();
            var from = (Vector2)layer.InverseTransformPoint(source.rectTransform.TransformPoint(source.rectTransform.rect.center));
            var to = (Vector2)layer.InverseTransformPoint(destination.TransformPoint(destination.rect.center));
            var control = new Vector2((from.x + to.x) / 2, Mathf.Max(from.y, to.y) + 180);
            System.Action cleanup = () => { if (go != null) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); } };
            CozyMotion.Animate(rect, .62f, t =>
            {
                if (rect == null) return;
                // WAAPI interpolates the fourteen precomputed keyframes.
                var lower = Mathf.Floor(t * 14) / 14; var upper = Mathf.Min(1, lower + 1f / 14); var blend = (t - lower) * 14;
                rect.anchoredPosition = Vector2.LerpUnclamped(Bezier(from, control, to, lower), Bezier(from, control, to, upper), blend);
                rect.localScale = Vector3.one * (1 - .65f * t); rect.localRotation = Quaternion.Euler(0, 0, 25 * t);
                alpha.alpha = Mathf.Lerp(lower > .92f ? .6f : 1, upper > .92f ? .6f : 1, blend);
            }, () =>
            {
                cleanup();
                if (destination == null) return;
                var rest = destination.anchoredPosition;
                CozyMotion.Animate(destination, .3f, t => { if (destination != null) destination.anchoredPosition = rest + Vector2.up
                    * (t < .4f ? 8 * t / .4f : 8 * (1 - t) / .6f); });
                CozyMotion.Ripple(destination, 1, 70, 22);
            }, CozyWebMotion.Ease.ItemFly, cleanup: cleanup);
        }
        public static void CoinsFly(RectTransform source, RectTransform destination, RectTransform layer, int count = 5)
        {
            if (source == null || destination == null || layer == null || CozyMotion.Reduced || CozyMotion.Paused) return;
            var origin = (Vector2)layer.InverseTransformPoint(source.TransformPoint(source.rect.center));
            var target = (Vector2)layer.InverseTransformPoint(destination.TransformPoint(destination.rect.center));
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject("SoldFishCoin", typeof(RectTransform), typeof(Image)); go.transform.SetParent(layer, false);
                var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = layer.pivot; rect.sizeDelta = new Vector2(34, 34);
                var image = go.GetComponent<Image>(); image.sprite = Resources.Load<Sprite>("Desktop/Theme/Cozy/icons/coin"); image.raycastTarget = false;
                var start = origin + new Vector2(Random.Range(-40f, 40f), Random.Range(-20f, 20f));
                var control = new Vector2((start.x + target.x) / 2 + Random.Range(-60f, 60f), Mathf.Max(start.y, target.y) + 120);
                System.Action cleanup = () => { if (go != null) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); } };
                rect.anchoredPosition = start; rect.localScale = Vector3.one * .4f;
                CozyMotion.Animate(rect, .52f, t =>
                {
                    if (rect == null) return;
                    var low = Mathf.Floor(t * 10) / 10; var high = Mathf.Min(1, low + .1f); var blend = (t - low) * 10;
                    rect.anchoredPosition = Vector2.LerpUnclamped(Bezier(start, control, target, low), Bezier(start, control, target, high), blend);
                    var lowScale = low < .15f ? .4f + low * 4 : 1 - low * .3f;
                    var highScale = high < .15f ? .4f + high * 4 : 1 - high * .3f;
                    rect.localScale = Vector3.one * Mathf.Lerp(lowScale, highScale, blend);
                }, cleanup, CozyWebMotion.Ease.Fly, i * .045f, cleanup);
            }
        }

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) { return (1 - t) * (1 - t) * a + 2 * (1 - t) * t * b + t * t * c; }
        void Show(string caption)
        {
            if (stamp == null)
            {
                var go = new GameObject("JournalActionStamp", typeof(RectTransform), typeof(CozyCssGraphic), typeof(CanvasGroup));
                go.transform.SetParent(transform, false); stamp = (RectTransform)go.transform;
                stamp.anchorMin = stamp.anchorMax = new Vector2(1, 1); stamp.pivot = new Vector2(.5f, .5f);
                stamp.sizeDelta = new Vector2(74, 74); stamp.anchoredPosition = new Vector2(-23, -19);
                var ring = go.GetComponent<CozyCssGraphic>(); ring.shape = CozyCssGraphic.Shape.Stamp; ring.border = 4;
                ring.material = Resources.Load<Material>(DesktopLocalization.Concat(CozyJournalTheme.ResourceRoot, "materials/PaperMultiply"));
                ring.color = new Color(205f / 255, 64f / 255, 46f / 255, .9f); ring.raycastTarget = false;
                group = go.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
                var textGo = new GameObject("Caption", typeof(RectTransform), typeof(Text)); textGo.transform.SetParent(stamp, false);
                label = textGo.GetComponent<Text>(); label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                label.material = Resources.Load<Material>(DesktopLocalization.Concat(CozyJournalTheme.ResourceRoot, "materials/PaperMultiply"));
                label.font = CatTheme.Font; label.fontSize = 18; label.alignment = TextAnchor.MiddleCenter;
                label.color = new Color(205f / 255, 64f / 255, 46f / 255, .92f); label.raycastTarget = false; label.supportRichText = false;
            }
            time = 0; DesktopLocalization.SetText(label, caption); stamp.gameObject.SetActive(!CozyMotion.Paused); stamp.SetAsLastSibling(); Render();
        }
        public void Advance(float delta)
        {
            if (stamp == null || !stamp.gameObject.activeSelf || CozyMotion.Paused || time >= .33f) return;
            time += Mathf.Max(0, delta); Render();
        }
        void Render()
        {
            if (stamp == null) return;
            var t = CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutBack, time / .2f);
            group.alpha = CozyMotion.Reduced ? 1 : Mathf.Clamp01(t);
            stamp.localScale = Vector3.one * (CozyMotion.Reduced ? 1 : Mathf.LerpUnclamped(1.7f, 1, t));
            stamp.localRotation = CozyMotion.Reduced ? Quaternion.identity : Quaternion.Euler(0, 0, Mathf.LerpUnclamped(40, 14, t));
        }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnEnable() { CozyMotion.ReducedChanged += OnReduced; CozyMotion.PausedChanged += OnPaused; }
        void OnDisable() { CozyMotion.ReducedChanged -= OnReduced; CozyMotion.PausedChanged -= OnPaused; if (stamp != null) stamp.gameObject.SetActive(false); }
        void OnReduced(bool reduced) { Render(); }
        void OnPaused(bool paused) { if (stamp != null) stamp.gameObject.SetActive(!paused); }
    }
}
