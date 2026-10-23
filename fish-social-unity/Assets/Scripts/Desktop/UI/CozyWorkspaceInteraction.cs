using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>workspace.css feedback; the original journal's press squash is not used by these controls.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class CozyWorkspaceInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IDeselectHandler
    {
        public enum Kind { Button, Avatar, Choice }
        [SerializeField] Kind kind;
        [SerializeField] Graphic avatar;
        Button button;
        CozyButtonVisual visual;
        CozyGraphicOffset avatarPose;
        bool hovered, pressed;
        float elapsed, from, target, current;
        public float Progress => current;
        public static void Bind(Button button, Kind kind = Kind.Button, Graphic avatar = null)
        {
            if (button == null) return;
            var motion = button.GetComponent<CozyWorkspaceInteraction>() ?? button.gameObject.AddComponent<CozyWorkspaceInteraction>();
            motion.kind = kind; motion.avatar = avatar; motion.button = button;
            motion.visual = button.GetComponent<CozyButtonVisual>();
            if (avatar != null) motion.avatarPose = avatar.GetComponent<CozyGraphicOffset>() ?? avatar.gameObject.AddComponent<CozyGraphicOffset>();
            motion.Paint();
        }
        void Paint()
        {
            if (kind == Kind.Button) visual?.SetMotionOffset(new Vector2(0, CozyMotion.Reduced ? 0 : current));
            if (kind == Kind.Avatar && avatarPose != null)
                avatarPose.SetJournalPose(avatar.transform, Vector2.zero, CozyMotion.Reduced ? 0 : 5 * current,
                    CozyMotion.Reduced ? 1 : 1 + .1f * current, 1);
        }
        void Retarget()
        {
            var active = button != null && button.IsInteractable() && hovered;
            var next = kind == Kind.Button ? active ? pressed ? -2 : 2 : 0 : kind == Kind.Avatar && active ? 1 : 0;
            if (target == next) return;
            from = current; target = next; elapsed = 0;
        }
        public void Advance(float seconds)
        {
            Retarget();
            if (CozyMotion.Paused || CozyMotion.Reduced) { if (current != 0) { current = 0; Paint(); } return; }
            if (current == target) return;
            elapsed += Mathf.Max(0, seconds);
            current = Mathf.LerpUnclamped(from, target, CozyWebMotion.Evaluate(CozyWebMotion.Ease.CssEase,
                Mathf.Clamp01(elapsed / (kind == Kind.Avatar ? .2f : .16f))));
            Paint();
        }
        void Update() { Advance(Time.unscaledDeltaTime); }
        public void OnPointerEnter(PointerEventData data) { hovered = true; Retarget(); }
        public void OnPointerExit(PointerEventData data) { hovered = pressed = false; Retarget(); }
        public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) { pressed = true; Retarget(); } }
        public void OnPointerUp(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) { pressed = false; Retarget(); } }
        public void OnDeselect(BaseEventData data) { pressed = false; Retarget(); }
        void OnDisable() { hovered = pressed = false; from = target = current = 0; Paint(); }
    }
}
