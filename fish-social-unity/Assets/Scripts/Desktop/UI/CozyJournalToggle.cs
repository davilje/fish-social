using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    [DisallowMultipleComponent]
    public sealed class CozyJournalToggle : MonoBehaviour
    {
        [SerializeField] Toggle toggle;
        [SerializeField] Image track;
        [SerializeField] RectTransform knob;
        [SerializeField] Image paw;
        bool on;
        float elapsed = 1;
        float trackFrom;
        public CozyToggleMotion.Pose CurrentPose { get; private set; }

        public void Configure(Toggle owner,Image background,RectTransform handle,Image pushingPaw = null)
        {
            if(toggle!=null)toggle.onValueChanged.RemoveListener(Apply);
            toggle=owner;track=background;knob=handle;paw=pushingPaw;
            if(toggle!=null && isActiveAndEnabled)toggle.onValueChanged.AddListener(Apply);
            ApplyFinalPose();
        }
        void OnEnable()
        {
            CozyMotion.ReducedChanged+=OnReduced;
            if(toggle!=null){toggle.onValueChanged.RemoveListener(Apply);toggle.onValueChanged.AddListener(Apply);}
            ApplyFinalPose();
        }
        void OnDisable()
        {
            CozyMotion.ReducedChanged-=OnReduced;
            if(toggle!=null)toggle.onValueChanged.RemoveListener(Apply);
            ApplyFinalPose();
        }
        void OnReduced(bool reduced){if(reduced)ApplyFinalPose();}
        void Apply(bool value)
        {
            if(on==value)return;
            trackFrom=CurrentPose.TrackOn;on=value;elapsed=0;
            if(CozyMotion.Reduced)elapsed=1;
            Paint();
        }
        public void ApplyFinalPose()
        { on=toggle!=null && toggle.isOn;elapsed=1;trackFrom=on?1:0;Paint(); }
        public void Advance(float seconds)
        {
            if(CozyMotion.Paused || seconds<0)return;
            if(toggle!=null && toggle.isOn!=on)Apply(toggle.isOn);
            elapsed=CozyMotion.Reduced?1:Mathf.Min(1,elapsed+seconds);Paint();
        }
        void Update(){Advance(Time.unscaledDeltaTime);}
        void Paint()
        {
            var pose=CozyToggleMotion.Sample(on,elapsed);
            pose.TrackOn=Mathf.Lerp(trackFrom,on?1:0,CozyToggleMotion.CssEase(elapsed/.22f));
            CurrentPose=pose;
            if(knob!=null)knob.anchoredPosition=new Vector2(-6+pose.KnobX,6); // 10 px transparent source shadow inset
            if(track!=null)track.color=Color.Lerp(new Color32(234,220,196,255),new Color32(107,184,107,255),pose.TrackOn);
            if(paw!=null)
            {
                paw.rectTransform.anchoredPosition=new Vector2(-30+pose.PawX,-2);
                paw.color=new Color(1,243/255f,223/255f,pose.PawAlpha);
                paw.enabled=pose.PawAlpha>0;
            }
        }
    }
}
