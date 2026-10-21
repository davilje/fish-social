using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>Literal .tab CSS transitions and app.js press/release/entrance timelines.</summary>
    public sealed class CozyIndexMotion
    {
        public struct Pose
        {
            public float X, Alpha, Selection;
            public Vector2 Scale;
        }
        sealed class Transition
        {
            public float Value, From, To, Time, Duration, ReverseStart, Factor = 1;
            readonly float fullDuration;
            readonly bool color;
            public Transition(float initial,float duration,bool color = false)
            { Value=From=To=ReverseStart=initial; fullDuration=duration; Duration=duration; Time=duration; this.color=color; }
            float Ease(float t) => color ? CssEase(t) : CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutCubic,t);
            public void Set(float target)
            {
                if (To == target) return;
                var reversing = Time < Duration && target == ReverseStart;
                var factor = reversing ? Mathf.Clamp01(Mathf.Abs(Ease(Mathf.Clamp01(Time/Duration))*Factor + 1-Factor)) : 1;
                ReverseStart = reversing ? To : Value;
                From=Value; To=target; Time=0; Factor=factor; Duration=fullDuration*factor;
                if (Duration <= 0) Value=To;
            }
            public void Advance(float delta) { Time=Mathf.Min(Duration,Time+delta); Value=Duration<=0 ? To : Mathf.LerpUnclamped(From,To,Ease(Time/Duration)); }
            public void Settle() { Value=From=To; Time=Duration; Factor=1; }
        }
        readonly Transition position;
        readonly Transition color;
        bool selected;
        int press; // 0 idle, 1 held, 2 releasing
        float pressTime;
        float entranceTime, entranceDelay, entranceTo;
        bool entrance;
        public bool Selected => selected;
        public Pose Current { get; private set; }
        public CozyIndexMotion(bool initiallySelected = false)
        {
            selected=initiallySelected;
            position=new Transition(selected?-16:0,.2f);
            color=new Transition(selected?1:0,.16f,true);
            Refresh();
        }
        public void Select(bool value)
        {
            if(selected==value)return;
            selected=value; position.Set(value?-16:0); color.Set(value?1:0); Refresh();
        }
        public void PointerDown() { press=1; pressTime=0; Refresh(); }
        public void PointerUp() { if(press!=1)return; press=2; pressTime=0; Refresh(); }
        public void CancelPress() { press=0; pressTime=0; Refresh(); }
        public void BeginEntrance(int index)
        { entrance=true; entranceTime=0; entranceDelay=Mathf.Max(0,index)*.03f; entranceTo=selected?-16:0; Refresh(); }
        public void Settle()
        { position.Settle(); color.Settle(); entrance=false; press=0; Refresh(); }
        public void Advance(float seconds)
        {
            if(seconds<=0)return;
            position.Advance(seconds); color.Advance(seconds);
            if(press!=0) { pressTime+=seconds; if(press==2 && pressTime>=.22f)press=0; }
            if(entrance) { entranceTime+=seconds; if(entranceTime>=entranceDelay+.2f)entrance=false; }
            Refresh();
        }
        void Refresh()
        {
            var pose=new Pose { X=position.Value,Alpha=1,Selection=color.Value,Scale=Vector2.one };
            if(entrance)
            {
                var t=CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutCubic,Mathf.Clamp01((entranceTime-entranceDelay)/.2f));
                pose.X=Mathf.LerpUnclamped(-18,entranceTo,t); pose.Alpha=t;
            }
            if(press==1)
                pose.Scale=Vector2.LerpUnclamped(Vector2.one,new Vector2(1.06f,.92f),CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutQuad,Mathf.Clamp01(pressTime/.06f)));
            else if(press==2)
            {
                var t=CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutCubic,Mathf.Clamp01(pressTime/.22f));
                pose.Scale=t<=.5f ? Vector2.LerpUnclamped(new Vector2(1.06f,.92f),new Vector2(.98f,1.04f),t*2)
                    : Vector2.LerpUnclamped(new Vector2(.98f,1.04f),Vector2.one,(t-.5f)*2);
            }
            Current=pose;
        }
        // CSS transition's omitted easing is 'ease', not the movement's outCubic.
        static float CssEase(float x)
        {
            if(x<=0)return 0;if(x>=1)return 1;
            float low=0,high=1;
            for(var i=0;i<18;i++) { var t=(low+high)*.5f; var u=1-t; var p=3*u*u*t*.25f+3*u*t*t*.25f+t*t*t; if(p<x)low=t;else high=t; }
            var s=(low+high)*.5f;var v=1-s;return 3*v*v*s*.1f+3*v*s*s+s*s*s;
        }
    }
}
