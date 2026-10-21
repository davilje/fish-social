using UnityEngine;

namespace FishSocial.Desktop.JournalV2
{
    /// <summary>Literal 60+360+200ms navigation sequence. Only the visual child moves; the nav buttons never move.</summary>
    public sealed class JournalV2Bobber : MonoBehaviour
    {
        [SerializeField] RectTransform visual, icon;
        [SerializeField] CanvasGroup group;
        [SerializeField] Vector2 rest, from, target, position, iconRest;
        float elapsed, loopTime, iconTime = .3f;
        int phase, trailMask;
        [SerializeField] bool initialized, hidden;
        bool landed;
        public Vector2 Position => position;
        public bool Hidden => hidden;
        public void Configure(RectTransform visual)
        {
            this.visual = visual; if (visual == null) return;
            rest = visual.anchoredPosition;
            group = visual.GetComponent<CanvasGroup>(); if (group == null) group = visual.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Finish();
        }
        public void MoveTo(Vector2 target, RectTransform destinationIcon = null)
        {
            RestoreIcon();
            if (icon != destinationIcon) { icon = destinationIcon; if (icon != null) iconRest = icon.anchoredPosition; }
            if (initialized && !hidden && target == this.target) return;
            from = position; this.target = target; trailMask = 0; elapsed = 0; landed = false;
            JournalV2LoopBudget.Release(this);
            if (!initialized || JournalV2MotionState.Immediate) { initialized = true; hidden = false; Finish(); return; }
            phase = hidden ? 2 : 1; hidden = false; Render();
        }
        public void Hide()
        {
            if (hidden) return;
            from = position; hidden = true; elapsed = 0; phase = JournalV2MotionState.Immediate ? 0 : 3;
            JournalV2LoopBudget.Release(this); RestoreIcon();
            if (visual != null) JournalV2Particle.Ripple(visual.parent,rest+position+Vector2.down*28,70,22,2);
            if (phase == 0) Finish(); else Render();
        }
        public void Advance(float delta)
        {
            if (JournalV2MotionState.Paused || delta < 0) return;
            if (phase != 0)
            {
                elapsed += delta;
                if (phase == 1 && elapsed >= .42f && !landed)
                {
                    landed = true; iconTime = 0;
                    if (visual != null) JournalV2Particle.Ripple(visual.parent,rest+target+Vector2.down*28);
                }
                if (phase == 1 && elapsed >= .06f)
                    for (var i = 0; i < 3; i++)
                    {
                        var t = (i+1)*.25f;
                        if (elapsed >= .06f+.36f*t && (trailMask & (1<<i)) == 0)
                        {
                            trailMask |= 1<<i;
                            var ex = .5f-Mathf.Cos(Mathf.PI*t)*.5f;
                            var p = Vector2.Lerp(from,target,ex)+Vector2.up*(46*4*t*(1-t));
                            if (visual != null) JournalV2Particle.Trail(visual.parent,rest+p);
                        }
                    }
                if (elapsed >= (phase == 1 ? .62f : phase == 2 ? .32f : .26f)) Finish();
            }
            if (phase == 0 && !hidden && JournalV2LoopBudget.Owner == this) loopTime += delta;
            if (iconTime < .3f) iconTime = Mathf.Min(.3f,iconTime+delta);
            Render();
        }
        public void Sample(float seconds) { elapsed = Mathf.Max(0,seconds); Render(); }
        public void Finish()
        {
            phase = 0; position = target;
            if (!hidden && initialized && isActiveAndEnabled) JournalV2LoopBudget.Claim(this,10); else JournalV2LoopBudget.Release(this);
            Render();
        }
        void Render()
        {
            if (visual == null || group == null) return;
            var scale = Vector2.one; float rotation = 0, opacity = hidden ? 0 : 1;
            position = target;
            if (!JournalV2MotionState.Immediate)
            {
                if (phase == 1)
                {
                    if (elapsed < .06f)
                    { position = from; scale = Vector2.LerpUnclamped(Vector2.one,new Vector2(1.14f,.84f),JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutQuad,elapsed/.06f)); }
                    else if (elapsed < .42f)
                    {
                        var frame = Mathf.Clamp01((elapsed-.06f)/.36f)*16;
                        var a = Mathf.Min(15,Mathf.FloorToInt(frame)); var mix = frame-a;
                        var ta = a/16f; var tb = (a+1)/16f;
                        var x = Mathf.Lerp(.5f-Mathf.Cos(Mathf.PI*ta)*.5f,.5f-Mathf.Cos(Mathf.PI*tb)*.5f,mix);
                        var y = Mathf.Lerp(46*4*ta*(1-ta),46*4*tb*(1-tb),mix);
                        position = Vector2.LerpUnclamped(from,target,x)+Vector2.up*y;
                        rotation = -(Mathf.Sign(target.x-from.x)==0 ? 1 : Mathf.Sign(target.x-from.x))*16*(1-2*(frame/16));
                        scale = Vector2.Lerp(FlightScale(ta),FlightScale(tb),mix);
                    }
                    else
                    {
                        var t = JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutCubic,(elapsed-.42f)/.2f);
                        scale = t < .5f ? Vector2.LerpUnclamped(new Vector2(1.16f,.84f),new Vector2(.95f,1.07f),t*2)
                            : Vector2.LerpUnclamped(new Vector2(.95f,1.07f),Vector2.one,(t-.5f)*2);
                    }
                }
                else if (phase == 2)
                {
                    var t = JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutCubic,elapsed/.32f);
                    position.y += t < .7f ? Mathf.LerpUnclamped(-18,8,t/.7f) : Mathf.LerpUnclamped(8,0,(t-.7f)/.3f); opacity = t;
                }
                else if (phase == 3)
                {
                    var t = JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.InQuad,elapsed/.26f);
                    position = from+Vector2.down*18*t; opacity = 1-t;
                }
                else if (!hidden && JournalV2LoopBudget.Owner == this)
                {
                    var wave = Mathf.Lerp(-1,1,JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.InOutSine,Mathf.PingPong(loopTime/1.2f,1)));
                    position += Vector2.down*3*wave; rotation = -4*wave;
                }
            }
            visual.anchoredPosition = rest+position; visual.localScale = new Vector3(scale.x,scale.y,1); visual.localRotation = Quaternion.Euler(0,0,rotation);
            group.alpha = opacity;
            if (icon != null)
            {
                var t = JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutCubic,iconTime/.3f);
                var y = t < .4f ? 8*t/.4f : 8*(1-(t-.4f)/.6f);
                icon.anchoredPosition = iconRest+Vector2.up*(JournalV2MotionState.Immediate ? 0 : y);
            }
        }
        static Vector2 FlightScale(float t) => t < .2f ? new Vector2(.9f,1.14f) : t > .85f ? new Vector2(.94f,1.08f) : Vector2.one;
        void RestoreIcon() { iconTime = .3f; if (icon != null) icon.anchoredPosition = iconRest; }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnEnable() { JournalV2MotionState.Changed += StateChanged; if (initialized && !hidden) JournalV2LoopBudget.Claim(this,10); }
        void OnDisable() { JournalV2MotionState.Changed -= StateChanged; JournalV2LoopBudget.Release(this); RestoreIcon(); phase = 0; Render(); }
        void StateChanged() { if (JournalV2MotionState.Immediate) { RestoreIcon(); Finish(); } }
    }
}
