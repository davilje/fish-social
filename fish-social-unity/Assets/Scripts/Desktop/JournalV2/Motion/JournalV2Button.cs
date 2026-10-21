using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    public enum JournalV2ButtonKind { Plain, Primary, Index, Nav, Sticker, Segment, RankRow, Podium }

    /// <summary>Fixed root hit rectangle; exactly one writer owns the visual child's CSS translate/rotate/scale/opacity.</summary>
    [DisallowMultipleComponent]
    public sealed class JournalV2Button : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISubmitHandler, ICancelHandler
    {
        static readonly Color Ink = new Color32(117,71,46,255);
        static readonly Color Coral = new Color32(252,146,60,255);
        static readonly Color CoralDeep = new Color32(212,111,34,255);
        [SerializeField] RectTransform visual, tape;
        [SerializeField] CanvasGroup alpha;
        [SerializeField] Button button;
        [SerializeField] Graphic plate, label, icon, lightIcon, selection, shadow, raisedShadow, notch;
        [SerializeField] JournalV2Plate nativePlate;
        [SerializeField] Color borderRest, shadowRest;
        [SerializeField] Vector2 shadowOffsetRest;
        [SerializeField] float shadowBlurRest;
        [SerializeField] Vector2 rest, tapeRest;
        [SerializeField] Vector3 tapeScale = Vector3.one;
        [SerializeField] Color plateRest = Color.white, textRest = Ink;
        [SerializeField] JournalV2ButtonKind kind;
        [SerializeField] bool configured, selected, interactable = true;
        bool hovered;
        float stateTime = 1, colorTime = 1, stateFrom, stateTo, state, shadowFrom, shadowTo, shadowValue;
        Color colorFrom, colorTo, textFrom, textTo;
        float pressTime, entranceTime, entranceDelay, entranceDuration = .26f, tapeTime = .16f, shakeTime = .18f;
        int pressPhase;
        bool entering, railEntrance;
        Vector2 pressScale = Vector2.one;
        [SerializeField] RectTransform fishIcon;
        [SerializeField] Vector2 fishIconRest;
        float fishWagTime = .42f;
        public event Action<bool> PrimaryHoverChanged;
        [SerializeField] string sourceId;
        public string SourceId => sourceId;
        public bool Selected => selected;
        public bool Interactable => interactable;
        public JournalV2ButtonKind Kind => kind;
        [field: SerializeField] public float CssAngle { get; private set; }
        public Vector2 VisualOffset { get; private set; }
        public Vector2 VisualScale { get; private set; } = Vector2.one;
        public float Opacity { get; private set; } = 1;
        public float TapeReveal => JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutCubic, tapeTime / .16f);
        public bool EntranceComplete => !entering;
        public event Action<bool> SelectionChanged;
        bool Card => kind == JournalV2ButtonKind.Sticker || kind == JournalV2ButtonKind.RankRow || kind == JournalV2ButtonKind.Podium;

        public void Configure(RectTransform visual, JournalV2ButtonKind kind, string sourceId = null)
        {
            if (visual == null || visual == transform) throw new ArgumentException("A separate Visual child is required.", nameof(visual));
            if (!configured || this.visual != visual) rest = visual.anchoredPosition;
            this.visual = visual; this.kind = kind; this.sourceId = sourceId;
            alpha = visual.GetComponent<CanvasGroup>(); if (alpha == null) alpha = visual.gameObject.AddComponent<CanvasGroup>();
            alpha.blocksRaycasts = false; alpha.interactable = false;
            button = GetComponent<Button>();
            if (button != null) { button.transition = Selectable.Transition.None; button.interactable = interactable; }
            plate = Child("Plate"); label = Child("Label"); icon = Child("Icon"); lightIcon = Child("LightIcon");
            notch = Child("Notch");
            nativePlate = plate as JournalV2Plate;
            if (nativePlate != null) { borderRest = nativePlate.BorderColor; shadowRest = nativePlate.ShadowColor; shadowOffsetRest = nativePlate.ShadowOffset; shadowBlurRest = nativePlate.ShadowBlur; }
            selection = Child("Selection"); shadow = Child("Shadow"); raisedShadow = Child("RaisedShadow");
            tape = visual.Find("Tape") as RectTransform;
            if (tape != null) { tapeRest = tape.anchoredPosition; tapeScale = tape.localScale; }
            var iconImage = icon as Image;
            if (kind == JournalV2ButtonKind.Primary && iconImage != null && iconImage.sprite != null && iconImage.sprite.name == "fish")
            { fishIcon = iconImage.rectTransform; fishIconRest = fishIcon.anchoredPosition; }
            plateRest = plate != null ? plate.color : Color.white;
            textRest = label != null ? label.color : Ink;
            CssAngle = Card && kind != JournalV2ButtonKind.RankRow ? JournalV2WebEase.StableAngle(sourceId) : 0;
            foreach (var graphic in visual.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            configured = true; Finish();
        }
        Graphic Child(string name) { var child = visual.Find(name); return child != null ? child.GetComponent<Graphic>() : null; }
        public void SetCssAngle(float cssAngle) { CssAngle = cssAngle; Render(); }
        public void SetPalette(Color normalPlate, Color normalText)
        { plateRest = normalPlate; textRest = normalText; Finish(); }
        public void SetSelected(bool value)
        {
            if (selected == value) return;
            selected = value; ChangeState();
            if (value && Card) ReplayTape();
            SelectionChanged?.Invoke(value);
        }
        public void SetInteractable(bool value)
        {
            interactable = value; if (button != null) button.interactable = value;
            if (!value) { hovered = false; pressPhase = 0; pressScale = Vector2.one; }
            ChangeState();
        }
        float Destination => kind == JournalV2ButtonKind.Index ? (selected ? -16 : 0) : Card ? (selected ? 6 : hovered && interactable ? 5 : 0) : 0;
        Color PlateColor => kind == JournalV2ButtonKind.Index && selected ? Coral : plateRest;
        Color TextColor => kind == JournalV2ButtonKind.Primary || (selected && (kind == JournalV2ButtonKind.Index || kind == JournalV2ButtonKind.Segment))
            ? Color.white : kind == JournalV2ButtonKind.Nav && selected ? CoralDeep : textRest;
        void ChangeState()
        {
            stateFrom = state; stateTo = Destination;
            shadowFrom = shadowValue; shadowTo = Card && (selected || hovered && interactable) ? 1 : 0;
            stateTime = 0; colorTime = 0;
            colorFrom = plate != null ? plate.color : PlateColor; colorTo = PlateColor;
            textFrom = label != null ? label.color : TextColor; textTo = TextColor;
            if (JournalV2MotionState.Immediate) Finish(); else Render();
        }
        public void PlayEntrance(int ordinal)
        {
            if (!configured) return;
            railEntrance = kind == JournalV2ButtonKind.Index;
            if (JournalV2MotionState.Immediate || (!Card && !railEntrance) || (Card && ordinal >= 8)) { entering = false; Render(); return; }
            entranceDuration = railEntrance ? .2f : .26f;
            entranceDelay = Mathf.Max(0, ordinal) * (railEntrance ? .03f : .04f);
            entranceTime = 0; entering = true; Render();
        }
        public void PlayObservedEntrance(int ordinal)
        {
            PlayEntrance(Mathf.Min(7, ordinal));
            entranceDuration = .22f;
        }
        public void ReplayTape()
        {
            if (!selected || !Card) return;
            tapeTime = JournalV2MotionState.Immediate ? .16f : 0; Render();
        }
        public void PulseLike()
        {
            if (JournalV2MotionState.Immediate) return;
            pressPhase = 4; pressTime = 0; pressScale = Vector2.one*1.1f; Render();
        }
        public void Finish()
        {
            entering = false; pressPhase = 0; pressScale = Vector2.one; shakeTime = .18f; tapeTime = .16f;
            fishWagTime = .42f; state = stateFrom = stateTo = Destination; stateTime = 1;
            shadowValue = shadowFrom = shadowTo = Card && (selected || hovered && interactable) ? 1 : 0;
            colorFrom = colorTo = PlateColor; textFrom = textTo = TextColor; colorTime = 1;
            Render();
        }
        public void Advance(float delta)
        {
            if (!configured || JournalV2MotionState.Paused || delta < 0) return;
            if (JournalV2MotionState.Reduced) { Finish(); return; }
            fishWagTime = Mathf.Min(.42f,fishWagTime+delta); stateTime += delta; colorTime += delta; tapeTime = Mathf.Min(.16f, tapeTime + delta); shakeTime = Mathf.Min(.18f, shakeTime + delta);
            state = Mathf.LerpUnclamped(stateFrom, stateTo, JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutCubic, stateTime / (kind == JournalV2ButtonKind.Index ? .2f : .18f)));
            shadowValue = Mathf.LerpUnclamped(shadowFrom, shadowTo, JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.Css, stateTime / .18f));
            if (entering) { entranceTime += delta; if (entranceTime >= entranceDelay + entranceDuration) entering = false; }
            if (pressPhase != 0)
            {
                pressTime += delta;
                var down = Card ? new Vector2(1.02f,.96f) : new Vector2(1.06f,.92f);
                if (pressPhase == 1 || pressPhase == 3)
                {
                    pressScale = Vector2.LerpUnclamped(Vector2.one, down, JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutQuad, pressTime / .06f));
                    if (pressPhase == 3 && pressTime >= .06f) { pressPhase = 2; pressTime -= .06f; }
                }
                if (pressPhase == 4)
                {
                    pressScale = Vector2.one*Mathf.LerpUnclamped(1.1f,1,JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutBack,pressTime/.22f));
                    if (pressTime >= .22f) { pressPhase = 0; pressScale = Vector2.one; }
                }
                if (pressPhase == 2)
                {
                    var t = JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutCubic, pressTime / .22f);
                    var rebound = Card ? new Vector2(.99f,1.02f) : new Vector2(.98f,1.04f);
                    pressScale = t < .5f ? Vector2.LerpUnclamped(down,rebound,t*2) : Vector2.LerpUnclamped(rebound,Vector2.one,(t-.5f)*2);
                    if (pressTime >= .22f) { pressPhase = 0; pressScale = Vector2.one; }
                }
            }
            Render();
        }
        void Render()
        {
            if (!configured || visual == null) return;
            var reduced = JournalV2MotionState.Immediate;
            var entrance = entering ? JournalV2WebEase.Evaluate(railEntrance ? JournalV2WebEase.Curve.OutCubic : JournalV2WebEase.Curve.OutBack,
                (entranceTime - entranceDelay) / entranceDuration) : 1;
            var offset = kind == JournalV2ButtonKind.Index ? new Vector2(state,0) : new Vector2(0,state);
            if (railEntrance && entering) offset.x = Mathf.LerpUnclamped(-18,stateTo,entrance);
            if (shakeTime < .18f && !reduced) offset.x += JournalV2WebEase.Frames(shakeTime/.18f,0,-4,4,-3,3,0);
            var rotation = -CssAngle - (Card && entering && !reduced ? 6*(1-entrance) : 0);
            var scale = Card && entering && !reduced ? Mathf.LerpUnclamped(1.12f,1,entrance) : 1;
            VisualOffset = offset;
            VisualScale = reduced ? Vector2.one : pressScale * scale;
            Opacity = Mathf.Clamp01(entrance) * (interactable ? 1 : .75f);
            var center = visual.rect.center;
            var rotatedCenter = Quaternion.Euler(0,0,rotation) * new Vector3(center.x*VisualScale.x,center.y*VisualScale.y,0);
            visual.anchoredPosition = rest + offset + center - (Vector2)rotatedCenter;
            visual.localRotation = Quaternion.Euler(0,0,rotation);
            visual.localScale = new Vector3(VisualScale.x,VisualScale.y,1);
            alpha.alpha = Opacity;
            var colorT = JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.Css,colorTime/.16f);
            if (plate != null) plate.color = kind == JournalV2ButtonKind.Nav || kind == JournalV2ButtonKind.Segment
                ? new Color(plateRest.r,plateRest.g,plateRest.b,0) : Color.LerpUnclamped(colorFrom,colorTo,colorT);
            if (plate != null && (kind == JournalV2ButtonKind.Nav || kind == JournalV2ButtonKind.Segment)) plate.enabled = false;
            if (nativePlate != null)
            {
                if (kind == JournalV2ButtonKind.Index)
                {
                    nativePlate.BorderColor = selected ? Coral : borderRest;
                    nativePlate.ShadowColor = selected ? CoralDeep : shadowRest;
                }
                else if (Card)
                {
                    nativePlate.ShadowOffset = Vector2.LerpUnclamped(shadowOffsetRest,new Vector2(0,-16),shadowValue);
                    nativePlate.ShadowBlur = Mathf.LerpUnclamped(shadowBlurRest,selected ? 26 : 24,shadowValue);
                    var liftedColor = new Color(Ink.r,Ink.g,Ink.b,.22f);
                    nativePlate.ShadowColor = Color.LerpUnclamped(shadowRest,liftedColor,shadowValue);
                }
                nativePlate.Refresh();
            }
            if (notch != null) notch.color = selected ? new Color(1,1,1,.45f) : new Color(Ink.r,Ink.g,Ink.b,.08f);
            if (label != null) label.color = Color.LerpUnclamped(textFrom,textTo,colorT);
            if (lightIcon != null)
            {
                var light = kind == JournalV2ButtonKind.Primary || kind == JournalV2ButtonKind.Index && selected;
                lightIcon.color = Color.white; lightIcon.gameObject.SetActive(light); if (icon != null) icon.gameObject.SetActive(!light);
            }
            if (fishIcon != null)
            {
                var wag = !reduced && fishWagTime < .42f ? JournalV2WebEase.Frames(JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.InOutSine,fishWagTime/.42f),0,-14,10,0) : 0;
                var pivot = fishIcon.rect.min+Vector2.Scale(fishIcon.rect.size,new Vector2(.7f,.5f));
                var q = Quaternion.Euler(0,0,-wag);
                fishIcon.anchoredPosition = fishIconRest+pivot-(Vector2)(q*(Vector3)pivot); fishIcon.localRotation = q;
            }
            if (selection != null) selection.gameObject.SetActive(selected);
            if (tape != null)
            {
                tape.gameObject.SetActive(selected);
                var reveal = Mathf.Max(.00001f,TapeReveal);
                var tapeCenter = tape.rect.center;
                var shift = tape.localRotation * new Vector3(tapeCenter.x*tapeScale.x*(1-reveal),0,0);
                tape.anchoredPosition = tapeRest+(Vector2)shift;
                tape.localScale = new Vector3(tapeScale.x*reveal,tapeScale.y,tapeScale.z);
            }
            if (shadow != null && raisedShadow != null)
            {
                var normalColor = shadow.color; normalColor.a = 1-shadowValue; shadow.color = normalColor;
                var raisedColor = raisedShadow.color; raisedColor.a = shadowValue; raisedShadow.color = raisedColor;
            }
        }
        public void OnPointerEnter(PointerEventData data) { hovered = true; if (kind == JournalV2ButtonKind.Primary && interactable) { fishWagTime = 0; PrimaryHoverChanged?.Invoke(true); } ChangeState(); }
        public void OnPointerExit(PointerEventData data) { hovered = false; if (kind == JournalV2ButtonKind.Primary) PrimaryHoverChanged?.Invoke(false); ChangeState(); }
        public void OnPointerDown(PointerEventData data)
        {
            if (data != null && data.button != PointerEventData.InputButton.Left) return;
            if (JournalV2MotionState.Immediate) return;
            if (!interactable) { shakeTime = 0; Render(); return; }
            pressPhase = 1; pressTime = 0; pressScale = Vector2.one; Render();
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (data != null && data.button != PointerEventData.InputButton.Left) return;
            if (pressPhase == 0) return;
            pressPhase = 2; pressTime = 0;
            pressScale = Card ? new Vector2(1.02f,.96f) : new Vector2(1.06f,.92f); Render();
        }
        public void OnSubmit(BaseEventData data)
        { if (!interactable || JournalV2MotionState.Immediate) return; pressPhase = 3; pressTime = 0; Render(); }
        public void OnCancel(BaseEventData data) { OnPointerUp(null); }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnEnable() { JournalV2MotionState.Changed += OnMotionState; if (configured) Finish(); }
        void OnDisable() { JournalV2MotionState.Changed -= OnMotionState; hovered = false; Finish(); }
        void OnMotionState() { if (JournalV2MotionState.Immediate) Finish(); }
    }
}
