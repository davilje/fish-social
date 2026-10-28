using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2Toast : MonoBehaviour
    {
        struct Notice { public string Text; public bool Error; }
        readonly Queue<Notice> queue = new Queue<Notice>();
        [SerializeField] RectTransform visual, fish, cat;
        [SerializeField] Text label;
        [SerializeField] CanvasGroup group;
        [SerializeField] Vector2 rest;
        bool active, error;
        float elapsed;
        public int PendingCount => queue.Count;
        public bool IsShowing => active;
        public Vector2 VisualOffset { get; private set; }
        public void Configure(RectTransform visual, Text label, RectTransform fish, RectTransform cat)
        {
            this.visual = visual; this.label = label; this.fish = fish; this.cat = cat;
            rest = visual != null ? visual.anchoredPosition : Vector2.zero;
            if (visual != null) { group = visual.GetComponent<CanvasGroup>(); if (group == null) group = visual.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; }
            Clear();
        }
        public void Show(string text, bool error = false)
        {
            queue.Enqueue(new Notice { Text = DesktopLocalization.Message(text), Error = error });
            if (!active && !JournalV2MotionState.Paused) Next();
        }
        void Next()
        {
            if (queue.Count == 0) { active = false; if (group != null) group.alpha = 0; return; }
            var next = queue.Dequeue(); active = true; error = next.Error; elapsed = 0;
            if (label != null) DesktopLocalization.SetText(label, next.Text);
            if (fish != null) fish.gameObject.SetActive(!error);
            if (cat != null) cat.gameObject.SetActive(error);
            Render();
        }
        public void Advance(float delta)
        {
            if (JournalV2MotionState.Paused || delta < 0) return;
            if (!active) { if (queue.Count > 0) Next(); return; }
            elapsed += delta;
            if (elapsed >= 3.24f) Next(); else Render();
        }
        public void Sample(float seconds) { elapsed = Mathf.Max(0,seconds); Render(); }
        void Render()
        {
            if (visual == null || group == null) return;
            if (!active || JournalV2MotionState.Paused) { group.alpha = 0; return; }
            var enter = JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutCubic,elapsed/.42f);
            var exit = JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.InQuad,(elapsed-2.92f)/.32f);
            VisualOffset = JournalV2MotionState.Reduced ? Vector2.zero : new Vector2(460*(1-enter)-140*exit,8*exit);
            visual.anchoredPosition = rest+VisualOffset;
            group.alpha = enter*(1-exit);
            if (fish != null)
            {
                var cycle = Mathf.Min(elapsed,.24f*12)/.24f;
                var phase = Mathf.PingPong(cycle,1);
                var rotation = Mathf.Lerp(-5,5,JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.InOutSine,phase));
                fish.localRotation = Quaternion.Euler(0,0,JournalV2MotionState.Reduced ? 0 : -rotation);
            }
            if (cat != null) cat.localRotation = Quaternion.Euler(0,0,JournalV2MotionState.Reduced ? 0 : -14*JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutBack,(elapsed-.3f)/.3f));
        }
        public void Clear()
        {
            queue.Clear(); active = false; elapsed = 0; VisualOffset = Vector2.zero;
            if (visual != null) visual.anchoredPosition = rest;
            if (group != null) group.alpha = 0;
            if (fish != null) fish.localRotation = Quaternion.identity;
            if (cat != null) cat.localRotation = Quaternion.identity;
        }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnEnable() { JournalV2MotionState.Changed += StateChanged; }
        void OnDisable() { JournalV2MotionState.Changed -= StateChanged; Clear(); }
        void StateChanged() { Render(); }
    }
}
