using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2Particle : MonoBehaviour
    {
        RectTransform rect;
        Graphic graphic;
        float elapsed, delay, duration;
        bool ripple;
        public static void Ripple(Transform parent, Vector2 localPosition, float width = 70, float height = 22, int count = 1)
        {
            if (parent == null || JournalV2MotionState.Immediate) return;
            for (var i = 0; i < count; i++) Create(parent,localPosition,new Vector2(width,height),true,i*.12f);
        }
        public static void Trail(Transform parent, Vector2 localPosition)
        { if (parent != null && !JournalV2MotionState.Immediate) Create(parent,localPosition,new Vector2(8,8),false,0); }
        static void Create(Transform parent, Vector2 position, Vector2 size, bool ripple, float delay)
        {
            var go = new GameObject(ripple ? "Ripple" : "Trail",typeof(RectTransform)); go.transform.SetParent(parent,false);
            var rect = go.GetComponent<RectTransform>();
            var parentRect = parent as RectTransform; rect.anchorMin = rect.anchorMax = parentRect != null ? parentRect.pivot : Vector2.one*.5f;
            rect.sizeDelta = size; rect.anchoredPosition = position;
            Graphic image;
            if (ripple) { var sprite = go.AddComponent<Image>(); sprite.sprite = Resources.Load<Sprite>("Desktop/Theme/Cozy/journal/motion/ripple-ring"); image = sprite; }
            else { var circle = go.AddComponent<JournalV2Plate>(); circle.Radius = 4; circle.BorderWidth = 0; circle.ShadowColor = Color.clear; circle.color = new Color32(255,233,181,255); image = circle; }
            image.raycastTarget = false;
            var effect = go.AddComponent<JournalV2Particle>(); effect.rect = rect; effect.graphic = image;
            effect.ripple = ripple; effect.delay = delay; effect.duration = ripple ? .36f : .32f; effect.Advance(0);
        }
        public void Advance(float delta)
        {
            if (JournalV2MotionState.Immediate) { Remove(); return; }
            elapsed += Mathf.Max(0,delta);
            var t = Mathf.Clamp01((elapsed-delay)/duration);
            var eased = ripple ? JournalV2WebEase.Evaluate(JournalV2WebEase.Curve.OutCubic,t) : t;
            if (rect != null) rect.localScale = Vector3.one*(ripple ? Mathf.LerpUnclamped(.4f,1.5f,eased) : Mathf.Lerp(1,.4f,eased));
            if (graphic != null) { var c = graphic.color; c.a = elapsed < delay ? 0 : .9f*(1-eased); graphic.color = c; }
            if (t >= 1) Remove();
        }
        void Remove() { if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject); }
        void Update() { Advance(Time.unscaledDeltaTime); }
    }
}
