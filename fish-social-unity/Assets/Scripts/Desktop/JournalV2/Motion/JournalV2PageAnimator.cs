using System;
using UnityEngine;

namespace FishSocial.Desktop.JournalV2
{
    /// <summary>Explicit body/header children keep page paper and hit layout outside the animation hierarchy.</summary>
    [DisallowMultipleComponent]
    public sealed class JournalV2PageAnimator : MonoBehaviour
    {
        [SerializeField] RectTransform body, title, page;
        [SerializeField] Transform rail, middle, aside;
        [SerializeField] CanvasGroup bodyAlpha, titleAlpha, middleAlpha, asideAlpha, pageAlpha;
        [SerializeField] Vector2 bodyRest, titleRest, pageRest;
        float elapsed;
        int phase, stickerCount, generation;
        bool firstOpen, childrenStarted;
        Action completion;
        JournalV2Button[] buttons = new JournalV2Button[0];
        public float BodyOpacity => bodyAlpha != null ? bodyAlpha.alpha : 1;
        public Vector2 BodyOffset => body != null ? body.anchoredPosition-bodyRest : Vector2.zero;
        public float Elapsed => elapsed;
        public bool IsPlaying => phase != 0;
        public void Configure(RectTransform body, RectTransform headerTitle, Transform rail, Transform middle, Transform aside)
        {
            this.body = body; page = body != null ? body.parent as RectTransform : null;
            pageRest = page != null ? page.anchoredPosition : Vector2.zero; pageAlpha = Group(page);
            title = headerTitle; this.rail = rail; this.middle = middle; this.aside = aside;
            bodyRest = body != null ? body.anchoredPosition : Vector2.zero;
            titleRest = title != null ? title.anchoredPosition : Vector2.zero;
            bodyAlpha = Group(body); titleAlpha = Group(title); middleAlpha = Group(middle); asideAlpha = Group(aside);
            Finish();
        }
        static CanvasGroup Group(Transform node)
        {
            if (node == null) return null;
            var group = node.GetComponent<CanvasGroup>(); return group != null ? group : node.gameObject.AddComponent<CanvasGroup>();
        }
        static void Alpha(CanvasGroup group, float value)
        { if (group == null) return; group.alpha = Mathf.Clamp01(value); }
        public void Enter(bool firstOpen) { Enter(firstOpen,null); }
        public void Enter(bool firstOpen, Action done)
        {
            Cancel(); this.firstOpen = firstOpen; completion = done; phase = 1; elapsed = 0; childrenStarted = false;
            buttons = body != null ? body.GetComponentsInChildren<JournalV2Button>() : new JournalV2Button[0];
            stickerCount = 0; foreach (var button in buttons) if (IsCard(button)) stickerCount++;
            if (JournalV2MotionState.Immediate) { Finish(); done?.Invoke(); return; }
            if (!firstOpen) StartChildren();
            RenderEntrance();
        }
        public void Exit(Action done = null)
        {
            Cancel(); phase = 2; elapsed = 0; completion = done;
            if (JournalV2MotionState.Immediate) { Finish(); done?.Invoke(); return; }
            RenderExit();
        }
        public void Flip(Action replaceContent, Action done = null)
        {
            Exit(() => { replaceContent?.Invoke(); Enter(false,done); });
        }
        public void Close(Action done = null)
        {
            Cancel(); phase = 3; elapsed = 0; completion = done;
            if (JournalV2MotionState.Immediate) { Finish(); done?.Invoke(); return; }
            RenderClose();
        }
        static bool IsCard(JournalV2Button button) => button.Kind == JournalV2ButtonKind.Sticker
            || button.Kind == JournalV2ButtonKind.RankRow || button.Kind == JournalV2ButtonKind.Podium;
        void StartChildren()
        {
            if (childrenStarted) return;
            childrenStarted = true;
            var cardOrdinal = 0; var railOrdinal = 0;
            foreach (var button in buttons)
            {
                if (IsCard(button)) button.PlayEntrance(cardOrdinal++);
                else if (firstOpen && button.Kind == JournalV2ButtonKind.Index) button.PlayEntrance(railOrdinal++);
            }
        }
        public void Advance(float delta)
        {
            if (phase == 0 || JournalV2MotionState.Paused || delta < 0) return;
            Sample(elapsed+delta);
        }
        public void Sample(float seconds)
        {
            if (phase == 0) return;
            elapsed = Mathf.Max(0,seconds);
            if (phase == 1)
            {
                if (firstOpen && elapsed >= .2f) StartChildren();
                RenderEntrance();
                if (elapsed >= .2f && completion != null)
                {
                    var done = completion; completion = null; var version = generation;
                    done(); if (version != generation) return;
                }
                if (elapsed >= (firstOpen ? .6f : .4f)) { phase = 0; RestoreGroups(); }
            }
            else if (phase == 2)
            { RenderExit(); if (elapsed >= .12f) CompletePhase(); }
            else
            { RenderClose(); if (elapsed >= .14f) CompletePhase(); }
        }
        void RenderEntrance()
        {
            var t = JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutCubic,elapsed/.2f);
            if (body != null) body.anchoredPosition = bodyRest+(!firstOpen ? Vector2.right*24*(1-t) : Vector2.zero);
            if (firstOpen && page != null) page.anchoredPosition = pageRest+Vector2.down*16*(1-t);
            Alpha(bodyAlpha,firstOpen ? 1 : t); Alpha(pageAlpha,firstOpen ? t : 1);
            if (title != null) title.anchoredPosition = titleRest+(!firstOpen ? Vector2.right*10*(1-t) : Vector2.zero);
            Alpha(titleAlpha,firstOpen ? 1 : t);
            var local = elapsed-(firstOpen ? .2f : 0);
            // app.js entrance fades .mid-h, not the card grid; cards own their independent stagger.
            var heading = middle != null ? middle.Find("Heading") : null;
            if (heading != null) Alpha(Group(heading),local/.16f);
            Alpha(middleAlpha,1);
            Alpha(asideAlpha,(local-.08f-Mathf.Min(8,stickerCount)*.02f)/.16f);
        }
        void RenderExit()
        {
            var t = JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.InQuad,elapsed/.12f);
            if (body != null) body.anchoredPosition = bodyRest+Vector2.left*(24*t);
            Alpha(bodyAlpha,1-t); Alpha(titleAlpha,1);
        }
        void RenderClose()
        {
            var t = JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.InQuad,elapsed/.14f);
            if (page != null) page.anchoredPosition = pageRest+Vector2.down*(12*t);
            Alpha(pageAlpha,1-t);
        }
        void CompletePhase()
        { var done = completion; completion = null; phase = 0; RestoreGroups(); done?.Invoke(); }
        public void Cancel() { generation++; completion = null; phase = 0; RestoreGroups(); }
        public void Finish()
        {
            Cancel();
            foreach (var button in buttons) if (button != null) button.Finish();
        }
        void RestoreGroups()
        {
            if (body != null) body.anchoredPosition = bodyRest;
            if (page != null) page.anchoredPosition = pageRest; Alpha(pageAlpha,1);
            if (title != null) title.anchoredPosition = titleRest;
            Alpha(bodyAlpha,1); Alpha(titleAlpha,1); Alpha(middleAlpha,1); Alpha(asideAlpha,1);
            if (middle != null && middle.Find("Heading") != null) Alpha(Group(middle.Find("Heading")),1);
        }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnEnable() { JournalV2MotionState.Changed += StateChanged; }
        void OnDisable() { JournalV2MotionState.Changed -= StateChanged; Finish(); }
        void StateChanged()
        { if (!JournalV2MotionState.Immediate) return; var done = completion; Finish(); done?.Invoke(); }
    }
}
