using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    public abstract class JournalV2CommunityController : JournalV2Controller
    {
        protected int Epoch;
        protected bool Busy;
        protected bool Ready => App != null && App.Api != null && App.Api.CanUse;
        protected void Begin() { Epoch++; Busy=false; StopAllCoroutines(); }
        protected void Status(string value) { value = DesktopLocalization.Message(value); if(Page!=null && Page.Status!=null) DesktopLocalization.SetText(Page.Status, value??""); }
        protected static void Select(Button button,bool selected) { button?.GetComponent<JournalV2Button>()?.SetSelected(selected); }
        protected static void Action(Button button,UnityEngine.Events.UnityAction action)
        { if(button==null)return;button.onClick.RemoveAllListeners();button.onClick.AddListener(action); }
        protected static void Interactable(Button button,bool value)
        { if(button==null)return;button.interactable=value;button.GetComponent<JournalV2Button>()?.SetInteractable(value); }
        protected static void CardMotion(Button button,string id,int index,JournalV2ButtonKind kind,float? angle=null)
        {
            var motion=button.GetComponent<JournalV2Button>();
            if(motion==null)return;
            motion.Configure(JournalV2Pages.CommunityVisual(button) as RectTransform,kind,id);
            if(angle.HasValue)motion.SetCssAngle(angle.Value);
            if(Application.isPlaying)motion.PlayEntrance(index);else motion.Finish();
        }
        protected static void Height(GameObject root,float height)
        { var element=root.GetComponent<LayoutElement>()??root.AddComponent<LayoutElement>();element.minHeight=element.preferredHeight=height; }
        public override void Close() { Begin();base.Close(); }
    }
}
