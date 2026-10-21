#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        static void ApplyToggleParity(GameObject root)
        {
            foreach(var toggle in root.GetComponentsInChildren<Toggle>(true))
            {
                var track=toggle.transform.Find("JournalTrack") as RectTransform;
                if(track==null)continue;
                track.sizeDelta=new Vector2(74,38);
                var knob=Place(track,"JournalKnob",-6,-6,50,50);
                var image=knob.GetComponent<Image>();
                CozyJournalTheme.ApplySurface(image,"decor/toggle-knob");image.raycastTarget=false;
                var paw=Picture(track,"JournalPaw",CozyJournalTheme.Sprite("decor/paw-push"),-30,2,34,34);
                paw.transform.SetSiblingIndex(knob.GetSiblingIndex());paw.raycastTarget=false;
                toggle.transition=Selectable.Transition.None;
                toggle.graphic=null;
                var component=toggle.GetComponent<CozyJournalToggle>() ?? toggle.gameObject.AddComponent<CozyJournalToggle>();
                component.Configure(toggle,track.GetComponent<Image>(),knob,paw);
            }
        }
    }
}
#endif
