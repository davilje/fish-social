using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>app.js stickerHtml/stagger and pages.js jObservePonds. CSS transforms affect graphics, never hit layout.</summary>
    [DisallowMultipleComponent]
    public sealed class CozyStickerMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        static readonly HashSet<string> PondSeen = new HashSet<string>();
        readonly List<CozyGraphicOffset> graphics = new List<CozyGraphicOffset>();
        readonly Vector3[] corners = new Vector3[4];
        RectTransform rect;
        RectTransform viewport;
        CozyButtonVisual button;
        Image cardShadow;
        Image liftedShadow;
        float shadow;
        float shadowFrom;
        float shadowTo;
        float shadowTime = .18f;
        RectTransform tape;
        Vector3 tapeScale = Vector3.one;
        Vector2 tapePosition;
        float tapeTime = .16f;
        bool configured;
        bool selectionOrdered;
        int orderedChildCount = -1;
        public string SourceId { get; private set; }
        public bool EntranceComplete => complete;
        public float TapeReveal => tapeTime >= .16f ? 1 : CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutCubic, tapeTime / .16f);
        public float ShadowBlend => shadow;
        string key;
        float angle;
        public float CssAngle => angle;
        public Vector2 CurrentVisualOffset { get; private set; }
        public float AppliedRotation { get; private set; }
        public float AppliedScale { get; private set; } = 1;
        public void ClearVisualPoseForSnapshot()
        {
            foreach (var effect in graphics)
                if (effect != null) effect.SetJournalPose(transform, Vector2.zero, 0, 1, 1);
        }
        float delay;
        float baseDelay;
        int cardIndex;
        float time;
        float duration;
        bool pondObserver;
        bool animateAll;
        bool entering;
        bool complete;
        bool waitForLayout;
        bool hovered;
        bool lastSelected;
        bool? selectedOverride;
        public bool IsSelected => selectedOverride ?? (button != null && button.Selected);
        float lift;
        float liftFrom;
        float liftTo;
        float liftTime = .18f;
        Vector2 reflowFrom;
        float reflowTime = .26f;
        public void ApplyReflow(Vector3 previousWorldPosition)
        {
            ApplyFinalPose();
            reflowFrom = transform.InverseTransformVector(previousWorldPosition - transform.position);
            reflowTime = CozyMotion.Reduced ? .26f : 0;
            Apply(1);
        }

        public static CozyStickerMotion Bind(GameObject card, string stableKey, int index,
            RectTransform visibleArea = null, string sourceId = null, float? cssAngle = null, bool pondObserver = false,
            bool enterAll = false)
        {
            if (card == null) return null;
            var motion = card.GetComponent<CozyStickerMotion>();
            if (motion == null) motion = card.AddComponent<CozyStickerMotion>();
            motion.Configure(stableKey, index, visibleArea, sourceId, cssAngle, pondObserver, enterAll);
            return motion;
        }

        // JavaScript: let h=7; h=(h*31+c.charCodeAt(0))|0; (Math.abs(h)%300)/100-1.5.
        public static float StableAngle(string sourceId)
        {
            unchecked
            {
                var hash = 7;
                foreach (var c in sourceId ?? string.Empty) hash = hash * 31 + c;
                return (Math.Abs((long)hash) % 300) / 100f - 1.5f;
            }
        }

        void Configure(string stableKey, int index, RectTransform visibleArea, string sourceId, float? cssAngle, bool observe, bool enterAll)
        {
            rect = transform as RectTransform;
            var nextKey = stableKey ?? name;
            var preserveEntrance = configured && key == nextKey;
            key = nextKey;
            animateAll = enterAll;
            // Compatibility with the existing account-scoped controller keys: only the final id feeds tilt().
            sourceId = sourceId ?? key.Substring(key.LastIndexOf(':') + 1);
            SourceId = sourceId;
            angle = cssAngle ?? StableAngle(sourceId);
            viewport = visibleArea;
            pondObserver = observe;
            duration = observe ? CozyMotion.Snap : CozyMotion.Snap + .04f;
            cardIndex = index;
            delay = baseDelay = Mathf.Clamp(index, 0, 7) * CozyMotion.Stagger;
            button = GetComponent<CozyButtonVisual>();
            cardShadow = transform.Find("JournalCardShadow")?.GetComponent<Image>();
            EnsureShadowLayers();
            if (tape == null)
            {
                tape = transform.Find("JournalTape") as RectTransform;
                if (tape != null) { tapeScale = tape.localScale; tapePosition = tape.anchoredPosition; }
            }
            graphics.Clear();
            var hit = GetComponent<Button>();
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                // The card plate is often the root Image. Empty showcase slots have no child art,
                // so skipping the root leaves that slot without an entrance.
                var isRoot = graphic.transform == transform;
                if (!isRoot && hit != null && graphic == hit.targetGraphic) continue;
                var offset = graphic.GetComponent<CozyGraphicOffset>();
                if (offset == null) offset = graphic.gameObject.AddComponent<CozyGraphicOffset>();
                graphics.Add(offset);
            }
            if (!preserveEntrance)
            {
                selectedOverride = null;
                complete = observe ? PondSeen.Contains(key) : !animateAll && index >= 8;
                entering = false; waitForLayout = true; time = 0;
                lastSelected = IsSelected;
                lift = liftFrom = liftTo = lastSelected ? 6 : 0;
                shadow = shadowFrom = shadowTo = lastSelected ? 1 : 0;
                shadowTime = .18f; tapeTime = .16f;
            }
            configured = true; enabled = true;
            Apply(complete ? 1 : Mathf.Clamp01((time - (CozyMotion.Reduced ? 0 : delay)) / (CozyMotion.Reduced ? CozyMotion.Fade : duration)));
        }

        /// <summary>Selection for pointer-click cards that have no Button; button-backed cards keep their decoration state in sync.</summary>
        public void SetSelected(bool selected)
        {
            selectedOverride = selected;
            if (button != null) button.SetSelected(selected);
            RefreshExplicitSelection();
            if (CozyMotion.Reduced || CozyMotion.Paused) ApplyFinalPose();
            else Advance(0);
        }

        public void ClearSelectionOverride()
        {
            selectedOverride = null;
            GetComponent<CozyJournalCardVisual>()?.RefreshNow();
            if (CozyMotion.Reduced || CozyMotion.Paused) ApplyFinalPose();
            else Advance(0);
        }

        /// <summary>jSelect replays only the first matching card's tape, including repeated clicks on the current selection.</summary>
        public void ReplayTape()
        {
            if (!IsSelected) return;
            Advance(0);
            tapeTime = CozyMotion.Reduced || CozyMotion.Paused ? .16f : 0;
            ApplyTape();
        }

        void RefreshExplicitSelection()
        {
            var selection = transform.Find("JournalSelection");
            if (selectedOverride.HasValue)
            {
                if (selection != null) selection.gameObject.SetActive(IsSelected);
                if (tape != null) tape.gameObject.SetActive(IsSelected);
            }
            if (!isActiveAndEnabled) return;
            if (!IsSelected)
            {
                if (selectionOrdered)
                {
                    selectionOrdered = false;
                    orderedChildCount = -1;
                    var idleRibbon = transform.Find("JournalRibbon");
                    if (idleRibbon != null && idleRibbon.gameObject.activeSelf) idleRibbon.SetAsLastSibling();
                    var idleStamp = transform.Find("JournalStamp");
                    if (idleStamp != null && idleStamp.gameObject.activeSelf) idleStamp.SetAsLastSibling();
                }
                return;
            }
            // Reordering every frame dirties the shared canvas and hitches the rail tabs.
            if (selectionOrdered && orderedChildCount == transform.childCount) return;
            var ribbon = transform.Find("JournalRibbon");
            var stamp = transform.Find("JournalStamp");
            var place = transform.Find("Rank");
            if (place != null) place.SetAsLastSibling();
            if (selection != null && selection.gameObject.activeSelf) selection.SetAsLastSibling();
            if (ribbon != null && ribbon.gameObject.activeSelf) ribbon.SetAsLastSibling();
            if (stamp != null && stamp.gameObject.activeSelf) stamp.SetAsLastSibling();
            // The podium pill hangs across the selection stroke, so it paints after the ring.
            var rankBacking = transform.Find("JournalRankBacking");
            if (rankBacking != null) rankBacking.SetAsLastSibling();
            var rankLabel = transform.Find("JournalRankLabel");
            if (rankLabel != null) rankLabel.SetAsLastSibling();
            // The corner tape is z-index 2 in the preview, above the place mark.
            if (tape != null && tape.gameObject.activeSelf) tape.SetAsLastSibling();
            selectionOrdered = true;
            orderedChildCount = transform.childCount;
        }

        /// <summary>StylePostTape runs after the first capture; keep the styled corner pose.</summary>
        public void RememberTape()
        {
            if (tape == null)
                tape = transform.Find("JournalTape") as RectTransform;
            if (tape == null) return;
            tapeScale = tape.localScale;
            tapePosition = tape.anchoredPosition;
        }

        /// <summary>Start after the journal door/old-page exit has finished; preserve the prototype first-eight and pond-observer rules.</summary>
        public void RestartEntrance(float additionalDelay = 0)
        {
            if (!configured) return;
            delay = baseDelay + Mathf.Max(0, additionalDelay);
            complete = CozyMotion.Paused
                || (pondObserver ? PondSeen.Contains(key) : !animateAll && cardIndex >= 8);
            entering = false; time = 0; waitForLayout = true;
            Apply(complete ? 1 : 0);
        }

        /// <summary>Deterministic final CSS pose for Editor previews and restored pages; never changes the hit rectangle.</summary>
        public void ApplyFinalPose()
        {
            complete = true;
            reflowTime = .26f;
            entering = false;
            waitForLayout = false;
            lift = liftFrom = liftTo = IsSelected ? 6f : hovered ? 5f : 0f;
            liftTime = .18f;
            lastSelected = IsSelected;
            tapeTime = .16f;
            shadow = shadowFrom = shadowTo = lastSelected || hovered ? 1 : 0;
            shadowTime = .18f;
            Apply(1);
        }

        /// <summary>Sample a newly selected card at an absolute offset without replaying its entrance.</summary>
        public void SampleSelection(float seconds)
        {
            var selected = IsSelected;
            lastSelected = !selected;
            lift = liftFrom = 0;
            liftTo = 0;
            liftTime = 0;
            tapeTime = 0;
            shadow = shadowFrom = shadowTo = 0;
            shadowTime = 0;
            complete = true;
            Advance(Mathf.Max(0, seconds));
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
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            var overlap = Vector2.Max(Vector2.zero, Vector2.Min(max, viewport.rect.max) - Vector2.Max(min, viewport.rect.min));
            var area = Mathf.Max(1, (max.x - min.x) * (max.y - min.y));
            return overlap.x * overlap.y / area >= (pondObserver ? .12f : .0001f);
        }

        public void Advance(float delta)
        {
            if (CozyMotion.Paused || delta < 0) return;
            reflowTime = Mathf.Min(.26f, reflowTime + delta);
            var selected = IsSelected;
            if (selected && !lastSelected) tapeTime = 0;
            tapeTime = CozyMotion.Reduced ? .16f : Mathf.Min(.16f, tapeTime + delta);
            var targetShadow = selected || hovered ? 1f : 0f;
            if (targetShadow != shadowTo) { shadowFrom = shadow; shadowTo = targetShadow; shadowTime = 0; }
            shadowTime = Mathf.Min(.18f, shadowTime + delta);
            shadow = CozyMotion.Reduced ? shadowTo : Mathf.LerpUnclamped(shadowFrom, shadowTo, CssDefaultEase(shadowTime / .18f));
            var destination = selected ? 6f : hovered ? 5f : 0f;
            if (destination != liftTo)
            {
                liftFrom = lift; liftTo = destination; liftTime = 0;
            }
            lastSelected = selected;
            liftTime = Mathf.Min(.18f, liftTime + delta);
            lift = CozyMotion.Reduced ? liftTo : Mathf.LerpUnclamped(liftFrom, liftTo,
                CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutCubic, liftTime / .18f));
            if (!complete)
            {
                if (!entering)
                {
                    if (!InViewport()) { Apply(0); return; }
                    entering = true;
                    if (pondObserver) PondSeen.Add(key);
                }
                time += delta;
                if (time >= (CozyMotion.Reduced ? CozyMotion.Fade : delay + duration)) complete = true;
            }
            Apply(complete ? 1 : Mathf.Clamp01((time - (CozyMotion.Reduced ? 0 : delay)) / (CozyMotion.Reduced ? CozyMotion.Fade : duration)));
        }

        void Apply(float progress)
        {
            ApplyShadow();
            ApplyTape();
            var pose = GetComponent<CozyJournalStaticPose>();
            if (pose != null && pose.IsRevealing) return;
            var t = CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutBack, progress);
            // CSS rotate is clockwise in a downward-y coordinate system, opposite Unity's z axis.
            var scale = CozyMotion.Reduced ? 1f : Mathf.LerpUnclamped(1.12f, 1, t);
            var rotation = CozyMotion.Reduced ? -angle : -Mathf.LerpUnclamped(angle + 6, angle, t);
            CurrentVisualOffset = new Vector2(0, lift) + (CozyMotion.Reduced ? Vector2.zero : reflowFrom * (1 - CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutCubic, reflowTime / .26f)));
            AppliedRotation = rotation;
            AppliedScale = scale;
            foreach (var effect in graphics)
                if (effect != null) effect.SetJournalPose(transform, CurrentVisualOffset, rotation, scale, Mathf.Clamp01(t));
        }
        void EnsureShadowLayers()
        {
            if (cardShadow == null) return;
            CozyJournalTheme.ApplySurface(cardShadow, "surfaces/sticker-shadow", true);
            liftedShadow = transform.Find("JournalLiftedShadow")?.GetComponent<Image>();
            if (liftedShadow == null)
            {
                var go = new GameObject("JournalLiftedShadow", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                liftedShadow = go.GetComponent<Image>(); liftedShadow.raycastTarget = false;
                liftedShadow.transform.SetSiblingIndex(cardShadow.transform.GetSiblingIndex() + 1);
            }
            CozyJournalTheme.ApplySurface(liftedShadow, "surfaces/sticker-shadow-lifted", true);
        }
        void ApplyShadow()
        {
            // Crossfade the original CSS raster layers over the specified 180ms; each keeps its own transparent insets.
            if (cardShadow != null) cardShadow.color = new Color(1, 1, 1, 1 - shadow);
            if (liftedShadow != null) liftedShadow.color = new Color(1, 1, 1, shadow);
        }
        void ApplyTape()
        {
            RefreshExplicitSelection();
            if (tape == null) return;
            var scale = CozyMotion.Reduced ? 1 : Mathf.Max(.00001f, TapeReveal);
            // CSS defaults to transform-origin:center, even though the caption describes pulling from the left.
            var center = (Vector3)tape.rect.center;
            var shift = tape.localRotation * new Vector3(center.x * tapeScale.x * (1 - scale), 0, 0);
            tape.anchoredPosition = tapePosition + (Vector2)shift;
            tape.localScale = new Vector3(tapeScale.x * scale, tapeScale.y, tapeScale.z);
        }
        static float CssDefaultEase(float progress)
        {
            if (progress <= 0) return 0; if (progress >= 1) return 1;
            var low = 0f; var high = 1f;
            for (var i = 0; i < 18; i++)
            {
                var t = (low + high) * .5f; var u = 1 - t;
                var x = 3 * u * u * t * .25f + 3 * u * t * t * .25f + t * t * t;
                if (x < progress) low = t; else high = t;
            }
            var value = (low + high) * .5f; var inverse = 1 - value;
            return 3 * inverse * inverse * value * .1f + 3 * inverse * value * value + value * value * value;
        }
        void Update()
        {
            if (CozyMotion.EntranceHeld)
            {
                waitForLayout = false;
                return;
            }
            if (waitForLayout) { waitForLayout = false; return; }
            Advance(Time.unscaledDeltaTime);
        }
        public void OnPointerEnter(PointerEventData data) { hovered = true; }
        public void OnPointerExit(PointerEventData data) { hovered = false; }
        void OnEnable() { CozyMotion.ReducedChanged += OnReduced; CozyMotion.PausedChanged += OnPaused; }
        void OnDisable() { CozyMotion.ReducedChanged -= OnReduced; CozyMotion.PausedChanged -= OnPaused; hovered = false; ApplyFinalPose(); }
        void OnReduced(bool reduced) { if (reduced) ApplyFinalPose(); }
        void OnPaused(bool paused) { if (paused) ApplyFinalPose(); }
    }
}
