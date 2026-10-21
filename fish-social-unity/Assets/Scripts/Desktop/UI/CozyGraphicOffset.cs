using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    [DisallowMultipleComponent]
    public sealed class CozyGraphicOffset : BaseMeshEffect
    {
        Vector3 localOffset;
        float opacity = 1f;
        Vector2 visualScale = Vector2.one;
        Matrix4x4 deformation = Matrix4x4.identity;
        Matrix4x4 journalDeformation = Matrix4x4.identity;
        float journalAlpha = 1f;
        bool journalPose;
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
            SetVisual(owner, verticalOffset, alpha, Vector2.one);
        }

        internal void SetVisual(Transform owner, float verticalOffset, float alpha, Vector2 scale)
        {
            SetVisual(owner, new Vector2(0, verticalOffset), alpha, scale);
        }

        internal void SetVisual(Transform owner, Vector2 offsetInOwner, float alpha, Vector2 scale)
        {
            var offset = transform.InverseTransformVector(owner.TransformVector(offsetInOwner));
            if (localOffset == offset && Mathf.Approximately(opacity, alpha) && visualScale == scale)
                return;
            localOffset = offset;
            opacity = alpha;
            visualScale = scale;
            var center = owner is RectTransform ownerRect ? (Vector3)ownerRect.rect.center : Vector3.zero;
            deformation = transform.worldToLocalMatrix * owner.localToWorldMatrix
                * Matrix4x4.Translate(center) * Matrix4x4.Scale(new Vector3(scale.x, scale.y, 1f)) * Matrix4x4.Translate(-center)
                * owner.worldToLocalMatrix * transform.localToWorldMatrix;
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        internal void SetJournalPose(Transform owner, Vector2 offset, float rotation, float scale, float alpha)
        {
            journalPose = offset != Vector2.zero || rotation != 0 || scale != 1 || alpha != 1;
            journalAlpha = alpha;
            var center = owner is RectTransform ownerRect ? (Vector3)ownerRect.rect.center : Vector3.zero;
            journalDeformation = transform.worldToLocalMatrix * owner.localToWorldMatrix
                * Matrix4x4.Translate(center) * Matrix4x4.TRS(offset, Quaternion.Euler(0, 0, rotation), Vector3.one * scale) * Matrix4x4.Translate(-center)
                * owner.worldToLocalMatrix * transform.localToWorldMatrix;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || (localOffset == Vector3.zero && Mathf.Approximately(opacity, 1f) && visualScale == Vector2.one && !journalPose))
                return;
            var vertex = new UIVertex();
            for (var vertexIndex = 0; vertexIndex < vertices.currentVertCount; vertexIndex++)
            {
                vertices.PopulateUIVertex(ref vertex, vertexIndex);
                vertex.position = journalDeformation.MultiplyPoint3x4(deformation.MultiplyPoint3x4(vertex.position) + localOffset);
                vertex.color.a = (byte)Mathf.RoundToInt(vertex.color.a * opacity * journalAlpha);
                vertices.SetUIVertex(vertex, vertexIndex);
            }
        }
    }
}
