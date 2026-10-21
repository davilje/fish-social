using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>app.js moveBobber: 60ms crouch, 16-segment 360ms parabola, 200ms landing.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class CozyBobberNavigation : MonoBehaviour
    {
        enum State { Rest, Hop, Enter, Exit }
        RectTransform rect;
        CanvasGroup alpha;
        RectTransform destinationIcon;
        Vector2 from;
        Vector2 destination;
        State state;
        float time;
        bool initialized;
        bool hidden;
        bool landing;
        int trails;
        bool Initialize()
        {
            if (this == null) return false;
            if (rect == null) rect = transform as RectTransform;
            if (rect == null) return false;
            // Unity's missing/destroyed native component wrappers are not CLR-null. Never use ?? here.
            if (alpha == null) alpha = GetComponent<CanvasGroup>();
            if (alpha == null) alpha = gameObject.AddComponent<CanvasGroup>();
            return alpha != null;
        }
        public void Move(Vector2 position, RectTransform icon)
        {
            if (!Initialize()) return;
            if (initialized && !hidden && state == State.Rest && destination == position) return;
            CozyMotion.StopFloat(rect);
            CozyMotion.Cancel(rect);
            from = rect.anchoredPosition;
            destination = position;
            destinationIcon = icon;
            time = 0; landing = false; trails = 0;
            state = hidden ? State.Enter : State.Hop;
            hidden = false;
            if (!initialized || CozyMotion.Reduced || CozyMotion.Paused)
            {
                initialized = true;
                Finish();
                return;
            }
            Render();
        }
        public void Hide()
        {
            if (!Initialize()) return;
            if (hidden) return;
            CozyMotion.StopFloat(rect);
            from = rect.anchoredPosition;
            destination = from;
            time = 0;
            state = State.Exit;
            hidden = true;
            CozyMotion.Ripple(rect, 2, 70, 22);
            if (CozyMotion.Reduced || CozyMotion.Paused) Finish();
        }
        public void Advance(float delta)
        {
            if (state == State.Rest || CozyMotion.Paused || delta <= 0 || !isActiveAndEnabled || !Initialize()) return;
            time += delta;
            if (CozyMotion.Reduced || time >= (state == State.Hop ? .62f : state == State.Enter ? .32f : .26f))
            { Finish(); return; }
            Render();
        }
        void Render()
        {
            if (rect == null || alpha == null) return;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            alpha.alpha = 1;
            if (state == State.Enter)
            {
                var t = CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutCubic, time / .32f);
                rect.anchoredPosition = destination + Vector2.up * (t < .7f
                    ? Mathf.Lerp(-18, 8, t / .7f) : Mathf.Lerp(8, 0, (t - .7f) / .3f));
                alpha.alpha = Mathf.Min(1, t / .7f);
                return;
            }
            if (state == State.Exit)
            {
                var t = CozyWebMotion.Evaluate(CozyWebMotion.Ease.InQuad, time / .26f);
                rect.anchoredPosition = from + Vector2.down * (18 * t); alpha.alpha = 1 - t; return;
            }
            if (time < .06f)
            {
                var t = CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutQuad, time / .06f);
                rect.localScale = Vector3.LerpUnclamped(Vector3.one, new Vector3(1.14f, .84f, 1), t);
                return;
            }
            if (time < .42f)
            {
                var t = (time - .06f) / .36f;
                // WAAPI interpolates the sixteen explicit keyframes linearly.
                var lower = Mathf.Floor(t * 16) / 16;
                var upper = Mathf.Min(1, lower + 1f / 16);
                var blend = (t - lower) * 16;
                var x = Mathf.Lerp(Horizontal(lower), Horizontal(upper), blend);
                var y = Mathf.Lerp(46 * 4 * lower * (1 - lower), 46 * 4 * upper * (1 - upper), blend);
                rect.anchoredPosition = Vector2.LerpUnclamped(from, destination, x) + Vector2.up * y;
                var dir = Mathf.Sign(destination.x - from.x);
                if (dir == 0) dir = 1;
                rect.localRotation = Quaternion.Euler(0, 0, -dir * 16 * (1 - 2 * t));
                rect.localScale = Vector3.Lerp(FlightScale(lower), FlightScale(upper), blend);
                while (trails < 3 && t >= (trails + 1) * .25f)
                {
                    trails++;
                    Trail();
                }
                return;
            }
            rect.anchoredPosition = destination;
            var land = CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutCubic, (time - .42f) / .2f);
            rect.localScale = land < .5f
                ? Vector3.LerpUnclamped(new Vector3(1.16f, .84f, 1), new Vector3(.95f, 1.07f, 1), land * 2)
                : Vector3.LerpUnclamped(new Vector3(.95f, 1.07f, 1), Vector3.one, (land - .5f) * 2);
            if (!landing)
            {
                landing = true;
                CozyMotion.Ripple(rect, 1, 70, 22);
                if (destinationIcon != null)
                {
                    var icon = destinationIcon;
                    var rest = icon.anchoredPosition;
                    CozyMotion.Animate(icon, .3f, t =>
                    {
                        if (icon != null) icon.anchoredPosition = rest + Vector2.up * (t < .4f ? 8 * t / .4f : 8 * (1 - (t - .4f) / .6f));
                    });
                }
            }
        }
        static float Horizontal(float t) { return .5f - Mathf.Cos(Mathf.PI * t) / 2; }
        static Vector3 FlightScale(float t) { return t < .2f ? new Vector3(.9f, 1.14f, 1) : t > .85f ? new Vector3(.94f, 1.08f, 1) : Vector3.one; }
        void Trail()
        {
            var go = new GameObject("BobberTrail", typeof(RectTransform), typeof(CozyCssGraphic), typeof(CanvasGroup));
            go.transform.SetParent(rect.parent, false);
            var point = (RectTransform)go.transform;
            point.anchorMin = rect.anchorMin; point.anchorMax = rect.anchorMax;
            point.anchoredPosition = rect.anchoredPosition;
            point.sizeDelta = new Vector2(8, 8);
            var image = go.GetComponent<CozyCssGraphic>();
            image.shape = CozyCssGraphic.Shape.Circle;
            image.color = new Color(.886f, .325f, .247f, .55f); image.raycastTarget = false;
            var group = go.GetComponent<CanvasGroup>();
            CozyMotion.Animate(point, .32f, t =>
            {
                if (point == null) return;
                point.localScale = Vector3.one * Mathf.Lerp(1, .4f, t); group.alpha = .9f * (1 - t);
            }, () => { if (go != null) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); } });
        }
        void Finish(bool resumeLoop = true)
        {
            state = State.Rest;
            if (rect == null) return;
            rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
            rect.anchoredPosition = hidden ? destination + Vector2.down * 18 : destination;
            if (alpha != null) alpha.alpha = hidden ? 0 : 1;
            if (resumeLoop && !hidden && isActiveAndEnabled) CozyMotion.Float(rect, 0, CozyMotion.NavigationPriority);
        }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnEnable() { CozyMotion.ReducedChanged += OnReduced; CozyMotion.PausedChanged += OnPaused; }
        void OnDisable()
        {
            CozyMotion.ReducedChanged -= OnReduced;
            CozyMotion.PausedChanged -= OnPaused;
            // Teardown may already have destroyed CanvasGroup. Settle existing transforms only: no component creation or loop registration.
            Finish(false);
            if (rect != null) { CozyMotion.Cancel(rect); CozyMotion.StopFloat(rect); }
        }
        void OnDestroy() { if (rect != null) CozyMotion.StopFloat(rect); }
        void OnReduced(bool reduced) { if (reduced && rect != null) Finish(); }
        void OnPaused(bool paused) { if (paused && rect != null) Finish(); }
    }
}
