using UnityEngine;
using UnityEngine.EventSystems;

namespace FishSocial.Desktop
{
    public sealed class CozyRoomBowlView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] CozyRoomProp source;
        public void Configure(CozyRoomProp value) { source = value; }
        public void OnPointerClick(PointerEventData value) { if (source != null) source.OnPointerClick(value); }
        public void OnBeginDrag(PointerEventData value) { if (source != null) source.OnBeginDrag(value); }
        public void OnDrag(PointerEventData value) { if (source != null) source.OnDrag(value); }
        public void OnEndDrag(PointerEventData value) { if (source != null) source.OnEndDrag(value); }
    }
}
