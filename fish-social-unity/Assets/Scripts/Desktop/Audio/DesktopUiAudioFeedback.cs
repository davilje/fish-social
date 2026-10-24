using FishSocial.Desktop.JournalV2;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Event handlers survive rebinding onClick and never add duplicate listeners.</summary>
    [DisallowMultipleComponent]
    public sealed class DesktopUiAudioFeedback : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, ISubmitHandler, IUpdateSelectedHandler
    {
        bool pointerAllowed;
        bool submitAllowed;
        bool toggleBefore, hadToggle;
        public static void Ensure(GameObject target)
        {
            if (Application.isPlaying && target.GetComponent<DesktopUiAudioFeedback>() == null)
                target.AddComponent<DesktopUiAudioFeedback>();
        }
        public static string CueFor(string name, bool toggle, bool navigation)
        {
            if (toggle) return "ui_toggle";
            var lower = (name ?? string.Empty).ToLowerInvariant();
            if (lower.Contains("close") || lower.Contains("cancel") || lower.Contains("dismiss") || lower == "backdrop" || lower == "shade" || lower.Contains("关闭") || lower.Contains("取消")) return "ui_close";
            return navigation ? "ui_open" : "ui_click";
        }
        bool Allowed()
        {
            var selectable = GetComponent<Selectable>();
            return selectable != null && selectable.interactable && selectable.IsInteractable();
        }
        public void OnPointerDown(PointerEventData eventData)
        {
            pointerAllowed = eventData.button == PointerEventData.InputButton.Left && Allowed();
            CaptureToggle();
        }
        void CaptureToggle()
        {
            var toggle=GetComponent<Toggle>(); var journal=GetComponent<JournalV2Toggle>();
            hadToggle=toggle!=null||journal!=null; toggleBefore=toggle!=null?toggle.isOn:journal!=null&&journal.Value;
        }
        void Feedback()
        {
            var selectable = GetComponent<Selectable>();
            if (selectable == null) return;
            var visual = GetComponent<JournalV2Button>();
            if(hadToggle)
            {
                var toggle=GetComponent<Toggle>();var journal=GetComponent<JournalV2Toggle>();
                var now=toggle!=null?toggle.isOn:journal!=null&&journal.Value;
                if(now==toggleBefore)return;
            }
            var navigation = visual != null && visual.Kind == JournalV2ButtonKind.Nav;
            navigation |= transform.parent != null && (transform.parent.name == "Header" || transform.parent.name == "Nav");
            // Actual router transitions own navigation audio, including redirects/failures.
            if(navigation)return;
            DesktopAudioService.Play(CueFor(name, selectable is Toggle || GetComponent<JournalV2Toggle>() != null, navigation));
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && pointerAllowed && !eventData.dragging) Feedback();
            pointerAllowed = false;
        }
        public void OnUpdateSelected(BaseEventData eventData) { submitAllowed = Allowed(); CaptureToggle(); }
        public void OnSubmit(BaseEventData eventData) { if (submitAllowed || Allowed()) Feedback(); }
    }
}
