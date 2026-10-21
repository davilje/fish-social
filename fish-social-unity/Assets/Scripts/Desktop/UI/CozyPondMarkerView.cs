using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>The marker hit area stays fixed while its icon floats.</summary>
    public sealed class CozyPondMarkerView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        Text _label;
        Image _icon;
        GameObject _paw;
        bool _selected;
        bool _hovered;
        bool _focused;
        public RectTransform FloatTarget => _icon != null ? _icon.rectTransform : null;

        public void Bind(string name)
        {
            _label = DesktopModalUi.FindComponent<Text>(transform, "Label");
            _icon = DesktopModalUi.FindComponent<Image>(transform, "Icon");
            _paw = transform.Find("Paw")?.gameObject;
            if (_label != null) { _label.text = name; _label.raycastTarget = false; }
            if (_icon != null) _icon.raycastTarget = false;
            var visual = GetComponent<CozyButtonVisual>();
            if (visual != null) visual.enabled = false;
            var backing = transform.Find("CozyVisual");
            if (backing != null) backing.gameObject.SetActive(false);
            var hit = GetComponent<Image>();
            if (hit != null) { hit.sprite = null; hit.color = Color.clear; hit.raycastTarget = true; }
            var button = GetComponent<Button>();
            if (button != null) button.transition = Selectable.Transition.None;
            Refresh();
        }

        public void SetState(bool selected, bool locked, Color color)
        {
            _selected = selected;
            if (_icon != null)
            {
                _icon.sprite = CatTheme.LoadCozy(locked ? "icons/lock.png" : "journal/motion/bobber.png", Vector4.zero);
                _icon.color = locked ? new Color(.65f, .59f, .49f, 1f) : color;
                _icon.preserveAspect = true;
            }
            Refresh();
        }

        void Refresh()
        {
            if (_label != null) _label.gameObject.SetActive(_selected || _hovered || _focused);
            if (_paw != null) _paw.SetActive(_selected);
        }

        public void OnPointerEnter(PointerEventData _) { _hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData _) { _hovered = false; Refresh(); }
        public void OnSelect(BaseEventData _) { _focused = true; Refresh(); }
        public void OnDeselect(BaseEventData _) { _focused = false; Refresh(); }
        void OnDisable() { _hovered = _focused = false; CozyMotion.StopFloat(FloatTarget); Refresh(); }
    }
}
