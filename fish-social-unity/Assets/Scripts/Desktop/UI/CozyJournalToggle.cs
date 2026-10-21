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

        public void Configure(Toggle owner, Image background, RectTransform handle)
        {
            toggle = owner; track = background; knob = handle;
            Apply(toggle != null && toggle.isOn);
        }

        void OnEnable()
        {
            if (toggle == null) return;
            toggle.onValueChanged.RemoveListener(Apply);
            toggle.onValueChanged.AddListener(Apply);
            Apply(toggle.isOn);
        }

        void OnDisable()
        {
            if (toggle != null) toggle.onValueChanged.RemoveListener(Apply);
        }

        void Apply(bool on)
        {
            if (knob != null) knob.anchoredPosition = new Vector2(on ? 40 : 4, -4);
            if (track != null) track.color = on ? new Color(.78f, .94f, .72f) : Color.white;
        }
    }
}
