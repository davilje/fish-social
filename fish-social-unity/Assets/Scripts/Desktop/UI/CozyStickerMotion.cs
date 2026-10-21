using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Graphic-only paper pose. Layout and button hit rectangles stay stable.</summary>
    [DisallowMultipleComponent]
    public sealed class CozyStickerMotion : MonoBehaviour
    {
        static readonly HashSet<string> Entered = new HashSet<string>();
        static readonly Queue<string> EntryOrder = new Queue<string>();
        readonly List<CozyGraphicOffset> graphics = new List<CozyGraphicOffset>();
        readonly Vector3[] corners = new Vector3[4];
        RectTransform rect;
        RectTransform viewport;
        string key;
        float angle;
        float delay;
        float time;
        bool entering;
        bool complete;
        bool waitForLayout;

        public static CozyStickerMotion Bind(GameObject card, string stableKey, int index, RectTransform visibleArea = null)
        {
            if (card == null) return null;
            var motion = card.GetComponent<CozyStickerMotion>() ?? card.AddComponent<CozyStickerMotion>();
            motion.Configure(stableKey, index, visibleArea);
            return motion;
        }

        public static float StableAngle(string stableKey)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (var c in stableKey ?? string.Empty) hash = (hash ^ c) * 16777619;
                return ((hash % 301) - 150f) / 100f;
            }
        }

        void Configure(string stableKey, int index, RectTransform visibleArea)
        {
            rect = transform as RectTransform;
            key = stableKey ?? name;
            angle = StableAngle(key);
            viewport = visibleArea;
            delay = Mathf.Clamp(index, 0, CozyMotion.StaggerLimit - 1) * CozyMotion.Stagger;
            graphics.Clear();
            var button = GetComponent<Button>();
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.transform == transform || (button != null && graphic == button.targetGraphic)) continue;
                var effect = graphic.GetComponent<CozyGraphicOffset>() ?? graphic.gameObject.AddComponent<CozyGraphicOffset>();
                graphics.Add(effect);
            }
            complete = Entered.Contains(key);
            entering = false;
            waitForLayout = true;
            time = 0;
            enabled = true;
            Apply(1f);
        }

        bool InViewport()
        {
            if (rect == null || !gameObject.activeInHierarchy) return false;
            if (viewport == null) return true;
            rect.GetWorldCorners(corners);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var corner in corners)
            {
                var point = (Vector2)viewport.InverseTransformPoint(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return viewport.rect.Overlaps(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
        }

        public void Advance(float delta)
        {
            if (complete || CozyMotion.Paused || delta <= 0) return;
            if (!entering)
            {
                if (!InViewport()) return;
                entering = true;
                Entered.Add(key);
                EntryOrder.Enqueue(key);
                while (EntryOrder.Count > 4096) Entered.Remove(EntryOrder.Dequeue());
            }
            time += delta;
            var progress = CozyMotion.Reduced ? 1f : Mathf.Clamp01((time - delay) / CozyMotion.Snap);
            Apply(progress);
            if (progress >= 1)
            {
                complete = true;
            }
        }

        void Apply(float progress)
        {
            var t = 1f - Mathf.Pow(1f - progress, 3);
            var scale = CozyMotion.Reduced ? 1f : Mathf.Lerp(1.08f, 1f, t);
            var offset = CozyMotion.Reduced ? Vector2.zero : new Vector2(0, (1f - t) * 13f);
            var rotation = CozyMotion.Reduced ? 0f : angle * t;
            foreach (var effect in graphics)
                if (effect != null) effect.SetJournalPose(transform, offset, rotation, scale, Mathf.Lerp(.4f, 1f, t));
        }
        void Update()
        {
            // Grid/ScrollRect layout is rebuilt at the end of the frame that instantiated cards.
            if (waitForLayout) { waitForLayout = false; return; }
            Advance(Time.unscaledDeltaTime);
        }
        void OnEnable() { CozyMotion.ReducedChanged += OnReduced; }
        void OnDisable() { CozyMotion.ReducedChanged -= OnReduced; Apply(1); }
        void OnReduced(bool reduced) { Apply(1); if (reduced) complete = true; }
    }
}
