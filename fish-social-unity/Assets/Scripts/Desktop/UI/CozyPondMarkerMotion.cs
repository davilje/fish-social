using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>Literal bindMap/selectPond WAAPI channels; the marker hit rectangle never animates.</summary>
    public sealed class CozyPondMarkerMotion
    {
        public struct Pose { public bool LabelAnimating; public float BodyScale,BodyX,BodyY,BodyAlpha,LabelY,LabelAlpha,PawY,PawScale,PawAlpha; }
        bool hovered,selected,entering;
        float hoverTime=1,labelTime=1,pawTime=1,shakeTime=1,entryTime=1;
        public Pose Current { get; private set; }
        public CozyPondMarkerMotion(){Paint();}
        public void Hover(bool value) { hovered=value;if(value){hoverTime=0;labelTime=0;}Paint(); }
        public void Select(bool value) { if(selected==value)return;selected=value;pawTime=value?0:1;Paint(); }
        public void Stamp() { if(selected)pawTime=0;Paint(); }
        public void Shake() { shakeTime=0;Paint(); }
        public void Enter(bool value) { if(entering==value)return;entering=value;entryTime=value?0:1;Paint(); }
        public void Settle() { hovered=false;hoverTime=labelTime=pawTime=shakeTime=entryTime=1;Paint(); }
        public void Advance(float delta)
        { if(delta<0)return;hoverTime+=delta;labelTime+=delta;pawTime+=delta;shakeTime+=delta;entryTime+=delta;Paint(); }
        void Paint()
        {
            var p=new Pose { BodyScale=1,BodyAlpha=1,LabelAlpha=1,PawScale=1,PawAlpha=selected?1:0 };
            if(hovered)p.BodyScale=Mathf.LerpUnclamped(1,1.15f,CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutBack,Mathf.Clamp01(hoverTime/.16f)));
            if(labelTime<.16f)
            { p.LabelAnimating=true;var t=CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutBack,labelTime/.16f);p.LabelY=-6*(1-t);p.LabelAlpha=Mathf.Clamp01(Mathf.LerpUnclamped(.6f,1,t)); }
            if(selected && pawTime>=.12f && pawTime<.38f)
            { var t=CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutBack,(pawTime-.12f)/.26f);p.PawY=30*(1-t);p.PawScale=Mathf.LerpUnclamped(1.6f,1,t);p.PawAlpha=Mathf.Clamp01(.85f*t); }
            // Source has no fill mode: CSS opacity 1 applies during the 120 ms delay and after completion.
            if(shakeTime<.2f)p.BodyX=CozyWebMotion.Frames(shakeTime/.2f,0,-4,4,-3,0);
            if(entering)
            { var t=CozyWebMotion.Evaluate(CozyWebMotion.Ease.InQuad,Mathf.Clamp01(entryTime/.36f));p.BodyY=-16*t;p.BodyAlpha=1-t; }
            Current=p;
        }
    }
}
