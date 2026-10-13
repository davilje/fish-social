using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    [DisallowMultipleComponent]
    public sealed class CozyGraphicOffset : BaseMeshEffect
    {
        Vector3 localOffset;
        float opacity = 1f;
        bool stackedLabel;
        Vector2 savedAnchorMin;
        Vector2 savedAnchorMax;
        Vector2 savedPivot;
        Vector2 savedSizeDelta;
        Vector3 savedPosition;
        TextAnchor savedAlignment;
        LayoutElement labelLayout;
        bool savedIgnoreLayout;

        internal void SetStackedLabel(bool stacked)
        {
            var label = graphic as Text;
            if (label == null)
                return;
            var rect = label.rectTransform;
            if (stacked)
            {
                if (!stackedLabel)
                {
                    savedAnchorMin = rect.anchorMin;
                    savedAnchorMax = rect.anchorMax;
                    savedPivot = rect.pivot;
                    savedSizeDelta = rect.sizeDelta;
                    savedPosition = rect.anchoredPosition3D;
                    savedAlignment = label.alignment;
                    labelLayout = label.GetComponent<LayoutElement>();
                    savedIgnoreLayout = labelLayout != null && labelLayout.ignoreLayout;
                    if (labelLayout == null && rect.parent != null && rect.parent.GetComponent<LayoutGroup>() != null)
                        labelLayout = label.gameObject.AddComponent<LayoutElement>();
                    stackedLabel = true;
                }
                if (labelLayout != null)
                    labelLayout.ignoreLayout = true;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(0f, 24f);
                rect.anchoredPosition3D = new Vector3(0f, 12f, savedPosition.z);
                label.alignment = TextAnchor.MiddleCenter;
            }
            else if (stackedLabel)
            {
                if (labelLayout != null)
                    labelLayout.ignoreLayout = savedIgnoreLayout;
                rect.anchorMin = savedAnchorMin;
                rect.anchorMax = savedAnchorMax;
                rect.pivot = savedPivot;
                rect.sizeDelta = savedSizeDelta;
                rect.anchoredPosition3D = savedPosition;
                label.alignment = savedAlignment;
                stackedLabel = false;
            }
        }

        internal void SetVisual(Transform owner, float verticalOffset, float alpha)
        {
            var offset = transform.InverseTransformVector(owner.TransformVector(new Vector3(0f, verticalOffset, 0f)));
            if (localOffset == offset && Mathf.Approximately(opacity, alpha))
                return;
            localOffset = offset;
            opacity = alpha;
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || (localOffset == Vector3.zero && Mathf.Approximately(opacity, 1f)))
                return;
            var vertex = new UIVertex();
            for (var vertexIndex = 0; vertexIndex < vertices.currentVertCount; vertexIndex++)
            {
                vertices.PopulateUIVertex(ref vertex, vertexIndex);
                vertex.position += localOffset;
                vertex.color.a = (byte)Mathf.RoundToInt(vertex.color.a * opacity);
                vertices.SetUIVertex(vertex, vertexIndex);
            }
        }
    }
}
