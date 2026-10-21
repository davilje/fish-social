using System;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    /// <summary>toggleSwitch's actual 80ms paw entry, 20ms knob delay and 220ms push; paw cancels at 320ms.</summary>
    public sealed class JournalV2Toggle : MonoBehaviour
    {
        [SerializeField] RectTransform knob, paw;
        [SerializeField] Graphic track;
        [SerializeField] CanvasGroup pawAlpha;
        [SerializeField] Vector2 knobRest, pawRest;
        [SerializeField] Color offColor, onColor = new Color32(107,184,107,255);
        [SerializeField] bool value;
        bool playing;
        float elapsed;
        public bool Value => value;
        public float KnobX { get; private set; }
        public float PawX { get; private set; }
        public float PawOpacity { get; private set; }
        public event Action<bool> Changed;
        public void Configure(RectTransform knob, Graphic track, RectTransform paw, bool initialValue = false)
        {
            this.knob = knob; this.track = track; this.paw = paw;
            knobRest = knob != null ? knob.anchoredPosition : Vector2.zero;
            pawRest = paw != null ? paw.anchoredPosition : Vector2.zero;
            offColor = track != null ? track.color : (Color)new Color32(221,215,201,255);
            if (paw != null) { pawAlpha = paw.GetComponent<CanvasGroup>(); if (pawAlpha == null) pawAlpha = paw.gameObject.AddComponent<CanvasGroup>(); }
            value = initialValue; Finish();
        }
        public void SetValue(bool on, bool animate = true, bool notify = true)
        {
            if (value == on) return;
            value = on; elapsed = 0; playing = animate && !JournalV2MotionState.Immediate;
            if (playing) Sample(0); else Finish();
            if (notify) Changed?.Invoke(value);
        }
        public void Toggle() { SetValue(!value); }
        public void Advance(float delta)
        { if (playing && !JournalV2MotionState.Paused) Sample(elapsed+Mathf.Max(0,delta)); }
        public void Sample(float seconds)
        {
            elapsed = Mathf.Max(0,seconds);
            playing = elapsed < (value ? .32f : .22f) && !JournalV2MotionState.Immediate;
            var t = playing ? elapsed : 1;
            var color = JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.Css,t/.22f);
            if (track != null) track.color = Color.LerpUnclamped(offColor,onColor,value ? color : 1-color);
            KnobX = value ? 36*JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutBack,(t-.1f)/.22f)
                : 36*(1-JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutCubic,t/.22f));
            PawOpacity = 0; PawX = -12;
            if (value && playing && t < .08f) { PawX = Mathf.Lerp(-12,0,t/.08f); PawOpacity = t/.08f; }
            else if (value && playing)
            {
                var p = JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutCubic,(t-.08f)/.32f);
                PawX = p < .7f ? Mathf.LerpUnclamped(0,34,p/.7f) : Mathf.LerpUnclamped(34,20,(p-.7f)/.3f);
                PawOpacity = p < .7f ? 1 : 1-(p-.7f)/.3f;
            }
            if (knob != null) knob.anchoredPosition = knobRest+Vector2.right*KnobX;
            if (paw != null) paw.anchoredPosition = pawRest+Vector2.right*PawX;
            if (pawAlpha != null) pawAlpha.alpha = PawOpacity;
        }
        public void Finish() { playing = false; Sample(1); }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnEnable() { JournalV2MotionState.Changed += StateChanged; }
        void OnDisable() { JournalV2MotionState.Changed -= StateChanged; Finish(); }
        void StateChanged() { if (JournalV2MotionState.Immediate) Finish(); }
    }
}
