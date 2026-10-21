using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>Source toggleSwitch timeline. The paw is cancelled when the delayed knob completes at 320 ms.</summary>
    public static class CozyToggleMotion
    {
        public struct Pose { public float KnobX, PawX, PawAlpha, TrackOn; }
        public static Pose Sample(bool on,float seconds)
        {
            seconds=Mathf.Max(0,seconds);
            var pose=new Pose { KnobX=on?0:36,PawX=-12,PawAlpha=0,TrackOn=on?CssEase(seconds/.22f):1-CssEase(seconds/.22f) };
            if(!on)
            {
                pose.KnobX=36*(1-CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutCubic,Mathf.Clamp01(seconds/.22f)));
                return pose;
            }
            if(seconds<.08f)
            { pose.PawX=Mathf.Lerp(-12,0,seconds/.08f);pose.PawAlpha=seconds/.08f; }
            else if(seconds<.32f)
            {
                var t=CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutCubic,(seconds-.08f)/.32f);
                pose.PawX=t<=.7f?Mathf.LerpUnclamped(0,34,t/.7f):Mathf.LerpUnclamped(34,20,(t-.7f)/.3f);
                pose.PawAlpha=t<=.7f?1:Mathf.LerpUnclamped(1,0,(t-.7f)/.3f);
            }
            pose.KnobX=36*CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutBack,Mathf.Clamp01((seconds-.1f)/.22f));
            return pose;
        }
        public static float CssEase(float x)
        {
            x=Mathf.Clamp01(x);if(x==0 || x==1)return x;
            float low=0,high=1;
            for(var i=0;i<18;i++){var t=(low+high)*.5f;var u=1-t;var p=3*u*u*t*.25f+3*u*t*t*.25f+t*t*t;if(p<x)low=t;else high=t;}
            var s=(low+high)*.5f;var v=1-s;return 3*v*v*s*.1f+3*v*s*s+s*s*s;
        }
    }
}
