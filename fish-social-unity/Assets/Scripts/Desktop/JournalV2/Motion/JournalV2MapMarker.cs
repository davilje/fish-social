using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2MapMarker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform visual, body, label, labelPill, paw;
        [SerializeField] CanvasGroup bodyAlpha, labelAlpha, pillAlpha, pawAlpha;
        [SerializeField] Graphic colorLayer;
        [SerializeField] Vector2 bodyRest, labelRest, pillRest, pawRest;
        [SerializeField] bool selected, locked;
        [SerializeField] Color markerColor = Color.white;
        bool hovering, stamping, shaking, entering, rippleSent, sunken;
        float hoverTime, stampTime, shakeTime, enterTime, loopTime, pressTime;
        int pressPhase;
        Action entered;
        public bool Selected => selected;
        public void Configure(RectTransform visual)
        {
            this.visual = visual; if (visual == null) return;
            body = visual.Find("Body") as RectTransform; label = visual.Find("Label") as RectTransform;
            labelPill = visual.Find("LabelPill") as RectTransform; paw = visual.Find("Paw") as RectTransform;
            bodyRest = Position(body); labelRest = Position(label); pillRest = Position(labelPill); pawRest = Position(paw);
            bodyAlpha = Group(body); labelAlpha = Group(label); pillAlpha = Group(labelPill); pawAlpha = Group(paw);
            var layer = body != null ? body.Find("Color") : null; colorLayer = layer != null ? layer.GetComponent<Graphic>() : null;
            Finish();
        }
        static Vector2 Position(RectTransform node) => node != null ? node.anchoredPosition : Vector2.zero;
        static CanvasGroup Group(RectTransform node)
        { if (node == null) return null; var g = node.GetComponent<CanvasGroup>(); return g != null ? g : node.gameObject.AddComponent<CanvasGroup>(); }
        static void Alpha(CanvasGroup group,float value) { if (group != null) group.alpha = value; }
        public void SetState(bool selected, bool locked, Color color)
        {
            var change = !this.selected && selected;
            this.selected = selected; this.locked = locked; markerColor = color;
            if (selected && !locked && isActiveAndEnabled) JournalV2LoopBudget.Claim(this,30); else JournalV2LoopBudget.Release(this);
            if (change && !locked) Stamp(); else Render();
        }
        public void Hover(bool value) { hovering = value; hoverTime = 0; Render(); }
        public void Stamp()
        { stampTime = 0; rippleSent = false; stamping = !JournalV2MotionState.Immediate; Render(); }
        public void Shake() { shakeTime = 0; shaking = !JournalV2MotionState.Immediate; Render(); }
        public void Enter(Action done = null)
        {
            entered = done; enterTime = 0; sunken = false; entering = !JournalV2MotionState.Immediate;
            JournalV2LoopBudget.Release(this);
            if (visual != null) JournalV2Particle.Ripple(visual,Vector2.zero,120,44,3);
            if (!entering) { var callback = entered; entered = null; Finish(); callback?.Invoke(); } else Render();
        }
        public void Advance(float delta)
        {
            if (JournalV2MotionState.Paused || delta < 0) return;
            hoverTime += delta;
            if (stamping)
            {
                stampTime += delta;
                if (!rippleSent && stampTime >= .36f)
                { rippleSent = true; if (visual != null) JournalV2Particle.Ripple(visual,Vector2.zero,110,40,2); }
                if (stampTime >= .38f) stamping = false;
            }
            if (shaking) { shakeTime += delta; if (shakeTime >= .2f) shaking = false; }
            if (entering)
            {
                enterTime += delta;
                if (enterTime >= .36f) { entering = false; sunken = true; var callback = entered; entered = null; Render(); callback?.Invoke(); return; }
            }
            if (JournalV2LoopBudget.Owner == this) loopTime += delta;
            if (pressPhase != 0) { pressTime += delta; if (pressPhase == 2 && pressTime >= .22f) pressPhase = 0; }
            Render();
        }
        public void Sample(float seconds)
        { hoverTime = stampTime = shakeTime = enterTime = Mathf.Max(0,seconds); Render(); }
        public void Finish()
        {
            hovering = stamping = shaking = entering = sunken = false; pressPhase = 0; entered = null;
            hoverTime = stampTime = shakeTime = enterTime = 1;
            if (selected && !locked && isActiveAndEnabled) JournalV2LoopBudget.Claim(this,30); else JournalV2LoopBudget.Release(this);
            Render();
        }
        void Render()
        {
            if (visual == null) return;
            var reduced = JournalV2MotionState.Immediate;
            var wave = !reduced && JournalV2LoopBudget.Owner == this && !entering
                ? Mathf.Lerp(-1,1,JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.InOutSine,Mathf.PingPong(loopTime/1.2f,1))) : 0;
            var hover = hovering && !reduced ? JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutBack,hoverTime/.16f) : 0;
            var shake = shaking && !reduced ? JournalV2WebEase.Frames(shakeTime/.2f,0,-4,4,-3,0) : 0;
            var dip = sunken ? 1 : entering && !reduced ? JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.InQuad,enterTime/.36f) : 0;
            if (body != null)
            {
                var scale = 1+.15f*hover; var rotation = Quaternion.Euler(0,0,-4*wave);
                var origin = body.rect.min+Vector2.Scale(body.rect.size,new Vector2(.5f,.1f));
                body.anchoredPosition = bodyRest+new Vector2(shake,-3*wave-16*dip)+origin-(Vector2)(rotation*((Vector3)origin*scale));
                body.localRotation = rotation; body.localScale = Vector3.one*scale;
            }
            Alpha(bodyAlpha,(locked ? .7f : 1)*(1-dip));
            if (colorLayer != null)
            {
                var gray = markerColor.grayscale;
                colorLayer.color = locked ? Color.Lerp(markerColor,new Color(gray,gray,gray,markerColor.a),.9f) : markerColor;
            }
            var labelOffset = hovering && !reduced ? Vector2.down*6*(1-hover) : Vector2.zero;
            if (label != null) label.anchoredPosition = labelRest+labelOffset;
            if (labelPill != null) labelPill.anchoredPosition = pillRest+labelOffset;
            var labelOpacity = (locked ? .7f : 1)*(hovering && !reduced ? Mathf.Clamp01(Mathf.LerpUnclamped(.6f,1,hover)) : 1);
            Alpha(labelAlpha,labelOpacity); Alpha(pillAlpha,labelOpacity);
            var stamp = stamping && !reduced ? JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutBack,(stampTime-.12f)/.26f) : 1;
            if (paw != null) { paw.anchoredPosition = pawRest+Vector2.up*30*(1-stamp); paw.localScale = Vector3.one*Mathf.LerpUnclamped(1.6f,1,stamp); }
            Alpha(pawAlpha,selected && !locked ? .85f*Mathf.Clamp01(stamp) : 0);
            var press = Vector2.one;
            if (!reduced && pressPhase == 1) press = Vector2.LerpUnclamped(Vector2.one,new Vector2(1.02f,.96f),JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutQuad,pressTime/.06f));
            else if (!reduced && pressPhase == 2)
            {
                var t = JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutCubic,pressTime/.22f);
                press = t < .5f ? Vector2.LerpUnclamped(new Vector2(1.02f,.96f),new Vector2(.99f,1.02f),t*2)
                    : Vector2.LerpUnclamped(new Vector2(.99f,1.02f),Vector2.one,(t-.5f)*2);
            }
            visual.localScale = new Vector3(press.x,press.y,1);
        }
        public void OnPointerEnter(PointerEventData data) { Hover(true); }
        public void OnPointerExit(PointerEventData data) { Hover(false); }
        public void OnPointerDown(PointerEventData data) { if (JournalV2MotionState.Immediate) return; pressPhase = 1; pressTime = 0; }
        public void OnPointerUp(PointerEventData data) { if (pressPhase == 0) return; pressPhase = 2; pressTime = 0; }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnEnable() { JournalV2MotionState.Changed += StateChanged; if (selected && !locked) JournalV2LoopBudget.Claim(this,30); }
        void OnDisable() { JournalV2MotionState.Changed -= StateChanged; Finish(); JournalV2LoopBudget.Release(this); }
        void StateChanged() { if (!JournalV2MotionState.Immediate) return; var callback = entered; Finish(); callback?.Invoke(); }
    }
}
