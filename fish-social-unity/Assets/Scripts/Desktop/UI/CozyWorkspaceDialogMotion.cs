using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>pages.js jDialog: 16px upward / 200ms OutCubic; reduced motion is a 120ms fade.</summary>
    [DisallowMultipleComponent]
    public sealed class CozyWorkspaceDialogMotion : MonoBehaviour
    {
        RectTransform paper, dim;
        Vector2 rest, dimRest;
        CanvasGroup alpha;
        public float Opacity { get; private set; } = 1;
        public static void Play(RectTransform paper, RectTransform stationaryDim = null)
        {
            if (paper == null) return;
            var motion = paper.GetComponent<CozyWorkspaceDialogMotion>() ?? paper.gameObject.AddComponent<CozyWorkspaceDialogMotion>();
            motion.Begin(stationaryDim);
        }
        void Begin(RectTransform stationaryDim)
        {
            if (paper == null)
            {
                paper = transform as RectTransform; rest = paper.anchoredPosition;
                alpha = GetComponent<CanvasGroup>();
                if (alpha == null) alpha = gameObject.AddComponent<CanvasGroup>();
            }
            Finish();
            dim = stationaryDim;
            if (dim != null)
            {
                dimRest = dim.anchoredPosition;
            }
            Sample(0);
            CozyMotion.Animate(paper, CozyMotion.Slide, Sample, Restore,
                CozyWebMotion.Ease.OutCubic, cleanup: Restore, fadeWhenReduced: true);
        }
        void Sample(float t)
        {
            if (paper == null) return;
            var offset = CozyMotion.Reduced ? Vector2.zero : Vector2.down * (16 * (1 - t));
            paper.anchoredPosition = rest + offset;
            SetOpacity(Mathf.Clamp01(t));
            if (dim != null) dim.anchoredPosition = dimRest - offset;
        }
        void Restore()
        {
            if (paper != null) paper.anchoredPosition = rest;
            SetOpacity(1);
            if (dim != null) dim.anchoredPosition = dimRest;
        }
        public void Finish() { if (paper != null) CozyMotion.Cancel(paper); Restore(); }
        void OnDisable() { Finish(); }
        void SetOpacity(float value)
        {
            Opacity = value;
            if (alpha == null) return;
            // A zero-alpha CanvasGroup also culls an embedded dim at frame zero,
            // even with ignoreParentGroups. Fade the paper graphics independently.
            alpha.alpha = dim == null ? value : 1;
            if (dim == null || paper == null) return;
            foreach (var graphic in paper.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.transform == dim || graphic.transform.IsChildOf(dim)) continue;
                var fade = graphic.GetComponent<CozyDialogFadeGraphic>() ?? graphic.gameObject.AddComponent<CozyDialogFadeGraphic>();
                fade.SetOpacity(value);
            }
        }
    }

    public sealed class CozyDialogFadeGraphic : BaseMeshEffect
    {
        float opacity = 1;
        public void SetOpacity(float value) { opacity = value; if (graphic != null) graphic.SetVerticesDirty(); }
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            var vertex = new UIVertex();
            for (var i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                vertex.color.a = (byte)Mathf.RoundToInt(vertex.color.a * opacity);
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
