using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Permanent CSS paper rotation deforms artwork without moving button hit rectangles.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CozyPondArtworkPose : MonoBehaviour
    {
        [SerializeField] float unityAngle;
        [SerializeField] Graphic[] artwork = new Graphic[0];
        [SerializeField] Transform additionalArtwork;
        readonly List<Graphic> descendants = new List<Graphic>();
        readonly Dictionary<Graphic, Matrix4x4> lastMatrices = new Dictionary<Graphic, Matrix4x4>();
        Vector2 ownerSize;

        public void Configure(float angle, params Graphic[] graphics) => ConfigureGroup(angle, null, graphics);

        public void ConfigureGroup(float angle, Transform dynamicArtwork, params Graphic[] graphics)
        {
            unityAngle = angle;
            artwork = graphics;
            additionalArtwork = dynamicArtwork;
            lastMatrices.Clear();
            Apply();
        }

        void OnEnable() { lastMatrices.Clear(); Canvas.willRenderCanvases += Apply; Apply(); }
        void OnDisable() { Canvas.willRenderCanvases -= Apply; lastMatrices.Clear(); }

        void Apply()
        {
            var size = transform is RectTransform rect ? rect.rect.size : Vector2.zero;
            if (ownerSize != size) { ownerSize = size; lastMatrices.Clear(); }
            foreach (var graphic in artwork) ApplyGraphic(graphic);
            // Runtime pond markers are created after the prefab's OnEnable. Collect into a reusable
            // list so they inherit the same pose and stencil without rebuilding their hit geometry.
            if (additionalArtwork == null) return;
            descendants.Clear();
            additionalArtwork.GetComponentsInChildren(true, descendants);
            if (lastMatrices.Count > descendants.Count + artwork.Length + 8) lastMatrices.Clear();
            foreach (var graphic in descendants) ApplyGraphic(graphic);
        }

        void ApplyGraphic(Graphic graphic)
        {
            if (graphic == null) return;
            var relative = graphic.transform.worldToLocalMatrix * transform.localToWorldMatrix;
            if (lastMatrices.TryGetValue(graphic, out var previous) && previous == relative) return;
            var effect = graphic.GetComponent<CozyGraphicOffset>() ?? graphic.gameObject.AddComponent<CozyGraphicOffset>();
            effect.SetJournalPose(transform, Vector2.zero, unityAngle, 1, 1);
            lastMatrices[graphic] = relative;
        }
    }
}
