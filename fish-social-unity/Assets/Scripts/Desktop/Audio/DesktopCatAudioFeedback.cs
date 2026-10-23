using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    [DisallowMultipleComponent]
    public sealed class DesktopCatAudioFeedback : MonoBehaviour, IPointerClickHandler
    {
        public static void Bind(Transform cat)
        {
            if (cat == null || !Application.isPlaying) return;
            var image = cat.GetComponent<Graphic>();
            if (image != null) image.raycastTarget = true;
            if (cat.GetComponent<DesktopCatAudioFeedback>() == null) cat.gameObject.AddComponent<DesktopCatAudioFeedback>();
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging) return;
            DesktopAudioService.PlayMeow();
            GetComponent<CozyCornerCharacter>()?.SetPose(CozyCornerCharacter.Pose.LookUp);
        }
    }
}
