using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Hover/focus labels live above the scene and never intercept room input.</summary>
    public sealed class CozyHomeActionTag : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] CozyHomeRoom room;
        [SerializeField] string textKey, fallback;
        RectTransform hint;
        Text caption;
        CozyCssGraphic stateDot;
        bool hovered, focused;
        public void Configure(CozyHomeRoom owner, string key, string label)
        {
            room = owner; textKey = key; fallback = label;
            if ((name == "Sleep" || name == "Food") && stateDot == null)
            {
                var existing = transform.Find("StateDot");
                var node = existing != null ? existing.gameObject : new GameObject("StateDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(CozyCssGraphic));
                node.transform.SetParent(transform, false);
                var rect = (RectTransform)node.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(1,1); rect.pivot = new Vector2(.5f,.5f);
                rect.anchoredPosition = new Vector2(-6,-6); rect.sizeDelta = new Vector2(8,8);
                stateDot = node.GetComponent<CozyCssGraphic>(); stateDot.shape = CozyCssGraphic.Shape.Circle; stateDot.raycastTarget = false;
                stateDot.color = name == "Sleep" ? (Color)new Color32(120,134,106,255) : new Color32(203,156,75,255);
                node.SetActive(false);
            }
        }
        void Update()
        {
            if (stateDot == null || room == null) return;
            var active = name == "Sleep" ? room.IsSleeping || room.CurrentActivity == CozyHomeRoom.Activity.GoingToBed : room.IsFeeding;
            if (stateDot.gameObject.activeSelf != active) stateDot.gameObject.SetActive(active);
        }
        void Show()
        {
            if (room == null || !room.CanInteract) return;
            if (hint == null)
            {
                hint = new GameObject("HomeActionHint", typeof(RectTransform)).GetComponent<RectTransform>();
                hint.SetParent(room.transform, false);
                hint.anchorMin = hint.anchorMax = hint.pivot = Vector2.up;
                hint.sizeDelta = new Vector2(210, 38);
                CozyCssGraphic.EnsureWash(hint, "Paper", new Color32(255,250,240,255), 10, 1, new Color32(220,204,175,255));
                foreach (var g in hint.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
                caption = new GameObject("Caption", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                caption.transform.SetParent(hint, false); DesktopModalUi.Stretch(caption.gameObject);
                caption.font = CatTheme.Font; caption.fontSize = 17; caption.color = CatTheme.Ink;
                caption.alignment = TextAnchor.MiddleCenter; caption.raycastTarget = false;
            }
            var p = ((RectTransform)transform).anchoredPosition;
            hint.anchoredPosition = new Vector2(Mathf.Clamp(p.x - 81, 8, 1454), p.y + 46);
            hint.SetAsLastSibling();
            DesktopLocalization.SetText(caption, DesktopLocalization.Text(textKey, fallback));
            hint.gameObject.SetActive(true);
        }
        void Hide() { if (hint != null) hint.gameObject.SetActive(false); }
        public void OnPointerEnter(PointerEventData e) { hovered = true; Show(); }
        public void OnPointerExit(PointerEventData e) { hovered = false; if (!focused) Hide(); }
        public void OnSelect(BaseEventData e) { focused = true; Show(); }
        public void OnDeselect(BaseEventData e) { focused = false; if (!hovered) Hide(); }
        void OnDisable() { hovered = focused = false; Hide(); }
        void OnDestroy() { if (hint != null) Destroy(hint.gameObject); }
    }
}
