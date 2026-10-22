using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>The reference animates an empty .glint i while painting ::after. Keep disabled to match the visible approved preview.</summary>
    public sealed class CozyRareGlint : MonoBehaviour
    {
        [SerializeField] bool motionEnabled;
        int order;
        float time;
        RectTransform clip;
        RectTransform streak;
        public bool MotionEnabled => motionEnabled;
        public bool IsVisible => clip != null && clip.gameObject.activeSelf;
        public static CozyRareGlint Bind(GameObject card, int rareOrder, bool enableFixedPrototypeSelector = false)
        {
            var effect = card.GetComponent<CozyRareGlint>();
            if (effect == null) effect = card.AddComponent<CozyRareGlint>();
            effect.order = rareOrder; effect.motionEnabled = enableFixedPrototypeSelector; return effect;
        }
        public void SetMotionEnabled(bool value) { motionEnabled = value; time = 0; if (!value && clip != null) clip.gameObject.SetActive(false); }
        public void PreviewAt(float cycleSeconds)
        {
            var active = motionEnabled && order < 2 && !CozyMotion.Reduced && !CozyMotion.Paused;
            var elapsed = cycleSeconds - 6 - order * .18f;
            active &= elapsed >= 0 && elapsed <= .5f;
            if (!active) { if (clip != null) clip.gameObject.SetActive(false); return; }
            Build(); clip.gameObject.SetActive(true);
            var owner = (RectTransform)transform;
            clip.sizeDelta = owner.rect.size;
            clip.anchoredPosition = owner.rect.center;
            var paper = GetComponent<CozyStickerMotion>();
            clip.localRotation = Quaternion.Euler(0, 0, paper != null ? paper.AppliedRotation : 0);
            clip.anchoredPosition += paper != null ? paper.CurrentVisualOffset : Vector2.zero;
            streak.sizeDelta = new Vector2(owner.rect.width * .4f, owner.rect.height * 1.4f);
            var t = CozyWebMotion.Evaluate(CozyWebMotion.Ease.InOutSine, elapsed / .5f);
            streak.anchoredPosition = new Vector2(-owner.rect.width * .9f + 520 * t, 0);
            streak.localRotation = Quaternion.Euler(0, 0, -20);
        }
        void Build()
        {
            if (clip != null) return;
            var root = new GameObject("RareGlintClip", typeof(RectTransform), typeof(CozyCssGraphic), typeof(Mask));
            root.transform.SetParent(transform, false); clip = (RectTransform)root.transform;
            clip.anchorMin = clip.anchorMax = ((RectTransform)transform).pivot;
            var shape = root.GetComponent<CozyCssGraphic>(); shape.shape = CozyCssGraphic.Shape.Paper; shape.radius = 18; shape.border = 0; shape.raycastTarget = false;
            root.GetComponent<Mask>().showMaskGraphic = false;
            var go = new GameObject("Streak", typeof(RectTransform), typeof(CozyGlintGraphic));
            go.transform.SetParent(clip, false); streak = (RectTransform)go.transform; go.GetComponent<CozyGlintGraphic>().raycastTarget = false;
        }
        void Update() { if (!motionEnabled || CozyMotion.Paused) return; time += Time.unscaledDeltaTime; PreviewAt(time < 6 ? time : 6 + Mathf.Repeat(time - 6, 6)); }
        void OnEnable() { CozyMotion.ReducedChanged += Reduced; CozyMotion.PausedChanged += Paused; }
        void OnDisable() { CozyMotion.ReducedChanged -= Reduced; CozyMotion.PausedChanged -= Paused; if (clip != null) clip.gameObject.SetActive(false); }
        void Reduced(bool value) { if (value && clip != null) clip.gameObject.SetActive(false); }
        void Paused(bool value) { if (value && clip != null) clip.gameObject.SetActive(false); }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CozyGlintGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = rectTransform.rect;
            for (var i = 0; i < 3; i++)
            {
                var color = new Color(1, 245f / 255, 200f / 255, i == 1 ? .85f : 0);
                var x = Mathf.Lerp(r.xMin, r.xMax, i / 2f);
                mesh.AddVert(new Vector2(x, r.yMin), color, Vector2.zero); mesh.AddVert(new Vector2(x, r.yMax), color, Vector2.zero);
                if (i == 0) continue; var p = i * 2; mesh.AddTriangle(p - 2, p - 1, p); mesh.AddTriangle(p, p - 1, p + 1);
            }
        }
    }
}
