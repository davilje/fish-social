using UnityEngine;

namespace FishSocial.Desktop
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CozyJournalCardVisual : MonoBehaviour
    {
        [SerializeField] CozyButtonVisual button;
        [SerializeField] GameObject selection;
        [SerializeField] GameObject tape;
        bool last;
        public void Configure(CozyButtonVisual visual, GameObject outline, GameObject tapeObject)
        {
            button = visual; selection = outline; tape = tapeObject;
            RefreshNow();
        }
        void OnEnable() { RefreshNow(); }
        void LateUpdate()
        {
            if (button != null && last != button.Selected) RefreshNow();
        }
        public void RefreshNow()
        {
            last = button != null && button.Selected;
            if (selection != null) selection.SetActive(last);
            if (tape != null) tape.SetActive(last);
        }
    }
}
