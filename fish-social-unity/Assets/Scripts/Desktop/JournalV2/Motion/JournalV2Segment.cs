using UnityEngine;

namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2Segment : MonoBehaviour
    {
        [SerializeField] RectTransform knob;
        [SerializeField] Vector2[] positions;
        [SerializeField] Vector2 from, to;
        float elapsed = .22f;
        [field: SerializeField] public int Selected { get; private set; }
        public void Configure(RectTransform knob, Vector2[] positions, int selected = 0)
        { this.knob = knob; this.positions = positions; Selected = -1; Select(selected); Finish(); }
        public void Select(int index)
        {
            if (positions == null || positions.Length == 0 || knob == null) return;
            index = Mathf.Clamp(index,0,positions.Length-1); if (index == Selected) return;
            Selected = index; from = knob.anchoredPosition; to = positions[index]; elapsed = 0;
            if (JournalV2MotionState.Immediate) Finish();
        }
        public void Advance(float delta)
        { if (JournalV2MotionState.Paused || elapsed >= .22f) return; Sample(elapsed+Mathf.Max(0,delta)); }
        public void Sample(float seconds)
        { elapsed = Mathf.Clamp(seconds,0,.22f); if (knob != null) knob.anchoredPosition = Vector2.LerpUnclamped(from,to,JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutBack,elapsed/.22f)); }
        public void Finish() { Sample(.22f); }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnEnable() { JournalV2MotionState.Changed += StateChanged; }
        void OnDisable() { JournalV2MotionState.Changed -= StateChanged; Finish(); }
        void StateChanged() { if (JournalV2MotionState.Immediate) Finish(); }
    }
}
