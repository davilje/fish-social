using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>The marker hit area stays fixed while its icon floats.</summary>
    public sealed class CozyPondMarkerView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IPointerClickHandler
    {
        Text _label;
        Image _icon;
        Image _color;
        Image _highlight;
        GameObject _pill;
        GameObject _paw;
        bool _selected;
        bool _locked;
        bool _floating;
        readonly CozyPondMarkerMotion motion = new CozyPondMarkerMotion();
        public CozyPondMarkerMotion.Pose CurrentPose => motion.Current;
        public void Advance(float seconds)
        { if(CozyMotion.Paused)return;if(CozyMotion.Reduced)motion.Settle();else motion.Advance(seconds);Paint(); }
        public void ApplyFinalPose(){motion.Settle();Paint();}
        public void SetEntering(bool value)
        { motion.Enter(value);if(CozyMotion.Reduced)motion.Settle();Paint(); }
        public void SetFloating(bool value,float phase)
        {
            if(_floating==value)return;_floating=value;
            if(value)CozyMotion.Float(FloatTarget,phase,30);else CozyMotion.StopFloat(FloatTarget);
        }
        void Update(){Advance(Time.unscaledDeltaTime);}
        void OnEnable(){CozyMotion.ReducedChanged+=OnReduced;}
        void OnReduced(bool value){if(value)ApplyFinalPose();}
        public RectTransform FloatTarget => _icon != null ? _icon.rectTransform : null;

        public void Bind(string name)
        {
            _label = DesktopModalUi.FindComponent<Text>(transform, "Label");
            _icon = DesktopModalUi.FindComponent<Image>(transform, "Icon");
            _paw = transform.Find("Paw")?.gameObject;
            _color = DesktopModalUi.FindComponent<Image>(transform, "Icon/Color");
            _highlight = DesktopModalUi.FindComponent<Image>(transform, "Icon/Highlight");
            _pill = transform.Find("LabelPill")?.gameObject;
            if (_label != null)
            {
                DesktopLocalization.SetText(_label, name); _label.raycastTarget = false;
                _label.horizontalOverflow = HorizontalWrapMode.Overflow;
                _label.verticalOverflow = VerticalWrapMode.Truncate;
                _label.resizeTextForBestFit = false;
                _label.transform.SetAsLastSibling();
                var width = _label.preferredWidth;
                if (width < 8f)
                    width = _label.fontSize * Mathf.Max(1, (name ?? string.Empty).Length);
                width = Mathf.Max(44f, width + 20f);
                var rect = _label.rectTransform;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                rect.anchoredPosition = new Vector2(32 - width / 2, -42);
                if (_pill != null)
                {
                    var pillRect = (RectTransform)_pill.transform;
                    pillRect.sizeDelta = rect.sizeDelta; pillRect.anchoredPosition = rect.anchoredPosition;
                }
            }
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
            _selected = selected; _locked = locked; motion.Select(selected && !locked);
            if (_icon != null)
            {
                _icon.sprite = CozyJournalTheme.Sprite("motion/bobber-base");
                _icon.color = new Color(1, 1, 1, locked ? .7f : 1);
                _icon.preserveAspect = false;
                if (_color != null) _color.color = locked ? (Color)new Color32(154, 160, 166, 179) : new Color(color.r, color.g, color.b, 1);
                if (_highlight != null) _highlight.color = new Color(1, 1, 1, locked ? .7f : 1);
                if (_label != null) _label.color = new Color(CatTheme.Ink.r, CatTheme.Ink.g, CatTheme.Ink.b, locked ? .7f : 1);
                var pill = _pill != null ? _pill.GetComponent<Image>() : null;
                if (pill != null) pill.color = new Color(1,1,1,locked ? .665f : .95f);
            }
            Refresh();
        }

        public static Color CategoryColor(string category)
        {
            switch (category)
            {
                case "wilderness": return new Color32(97,184,107,255);
                case "reservoir": return new Color32(71,158,199,255);
                case "advanced": return new Color32(242,191,71,255);
                case "veteran": return new Color32(158,107,219,255);
                case "giant": return new Color32(184,199,219,255);
                case "forbidden": return new Color32(219,97,82,255);
                default: return new Color32(97,102,107,255);
            }
        }

        void Refresh()
        {
            // The original .buoy .label is always visible; selection only adds the paw.
            if (_label != null) _label.gameObject.SetActive(true);
            if (_paw != null) _paw.SetActive(_selected && !_locked);
            if (_pill != null) _pill.SetActive(true);
            Paint();
        }

        void Paint()
        {
            var pose=motion.Current;
            if(_icon!=null)
            {
                // CSS body transform-origin is 50% 90%, 19.2 px below its center.
                var offset=new Vector2(pose.BodyX,pose.BodyY+19.2f*(pose.BodyScale-1));
                foreach(var graphic in _icon.GetComponentsInChildren<Graphic>(true))
                    Effect(graphic).SetVisual(_icon.transform,offset,pose.BodyAlpha,Vector2.one*pose.BodyScale);
            }
            var labelAlpha=pose.LabelAlpha/(_locked && pose.LabelAnimating?.7f:1);
            if(_label!=null)Effect(_label).SetVisual(_label.transform,new Vector2(0,pose.LabelY),labelAlpha,Vector2.one);
            var pill=_pill!=null?_pill.GetComponent<Graphic>():null;
            if(pill!=null)Effect(pill).SetVisual(pill.transform,new Vector2(0,pose.LabelY),labelAlpha,Vector2.one);
            var paw=_paw!=null?_paw.GetComponent<Graphic>():null;
            if(paw!=null)Effect(paw).SetVisual(paw.transform,new Vector2(0,pose.PawY),pose.PawAlpha,Vector2.one*pose.PawScale);
        }
        static CozyGraphicOffset Effect(Graphic graphic) => graphic.GetComponent<CozyGraphicOffset>() ?? graphic.gameObject.AddComponent<CozyGraphicOffset>();
        public void OnPointerEnter(PointerEventData _) { motion.Hover(true);if(CozyMotion.Reduced)motion.Settle();Paint(); }
        public void OnPointerExit(PointerEventData _) { motion.Hover(false);Paint(); }
        public void OnPointerClick(PointerEventData data)
        {
            if(data!=null && data.button!=PointerEventData.InputButton.Left)return;
            if(_locked)motion.Shake();else motion.Stamp();
            if(CozyMotion.Reduced)motion.Settle();Paint();
        }
        public void OnSelect(BaseEventData _) { Refresh(); }
        public void OnDeselect(BaseEventData _) { Refresh(); }
        void OnDisable() { CozyMotion.ReducedChanged-=OnReduced;CozyMotion.StopFloat(FloatTarget);_floating=false;motion.Enter(false);motion.Settle();Refresh(); }
    }
}
