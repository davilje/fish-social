using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Direct art hit targets; dragging never invokes the prop's click action.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class CozyRoomProp : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] CozyHomeRoom room;
        [SerializeField] string propId;
        Vector2 grabOffset, origin;
        int pointer = int.MinValue;
        bool dragging;
        public string PropId => propId;
        public bool CanDrag => false; // Furniture is painted into the approved room background.
        public Vector2 BottomCenter
        {
            get { var r = (RectTransform)transform; return new Vector2(r.anchoredPosition.x + r.rect.width / 2, -r.anchoredPosition.y + r.rect.height); }
        }
        public void Configure(CozyHomeRoom owner, string id)
        { room = owner; propId = id; GetComponent<Image>().raycastTarget = true; }
        public void SetBottomCenter(Vector2 value)
        {
            var r = (RectTransform)transform;
            r.anchoredPosition = new Vector2(value.x - r.rect.width / 2, -value.y + r.rect.height);
        }
        public void OnPointerClick(PointerEventData e)
        {
            if (room == null || !room.CanInteract || e.button != PointerEventData.InputButton.Left || e.dragging || dragging) return;
            room.ActivateProp(propId);
        }
        public void OnBeginDrag(PointerEventData e)
        {
            if (!CanDrag || room == null || !room.CanInteract || e.button != PointerEventData.InputButton.Left) return;
            origin = BottomCenter; grabOffset = origin - room.PointerPosition(e); pointer = e.pointerId; dragging = true;
            e.eligibleForClick = false;
            room.BeginFurnitureDrag();
        }
        public void OnDrag(PointerEventData e)
        {
            if (!dragging || e.pointerId != pointer || !room.CanInteract) return;
            SetBottomCenter(CozyHomeRoom.ClampFurniture(propId, room.PointerPosition(e) + grabOffset));
            room.FurnitureMoved(propId);
        }
        public void OnEndDrag(PointerEventData e)
        {
            if (!dragging || e.pointerId != pointer) return;
            dragging = false; pointer = int.MinValue; room.EndFurnitureDrag(); room.SaveLayout();
        }
        public void CancelDrag()
        {
            if (!dragging) return;
            SetBottomCenter(origin); dragging = false; pointer = int.MinValue;
            room.FurnitureMoved(propId); room.EndFurnitureDrag();
        }
        void OnDisable() { CancelDrag(); }
    }
}
