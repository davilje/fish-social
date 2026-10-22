using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Three literal CSS clip-path polygons from app.js sell(). The displayed card is copied as UI meshes, never a business object.</summary>
    public sealed class CozyCardFragments : MonoBehaviour
    {
        static readonly Vector2[][] Cuts = {
            new[] { new Vector2(0,0), new Vector2(.38f,0), new Vector2(.30f,.4f), new Vector2(.42f,.7f), new Vector2(.34f,1), new Vector2(0,1) },
            new[] { new Vector2(.38f,0), new Vector2(.70f,0), new Vector2(.62f,.35f), new Vector2(.72f,.68f), new Vector2(.64f,1), new Vector2(.34f,1), new Vector2(.42f,.7f), new Vector2(.30f,.4f) },
            new[] { new Vector2(.70f,0), new Vector2(1,0), new Vector2(1,1), new Vector2(.64f,1), new Vector2(.72f,.68f), new Vector2(.62f,.35f) }
        };
        readonly List<RectTransform> pieces = new List<RectTransform>();
        readonly List<float> offsets = new List<float>();
        Vector2 origin;
        float cssAngle;
        float elapsed;
        public const float Duration = .52f;
        public int PieceCount => pieces.Count;
        public static Vector2[] ClipPolygon(int index) { return (Vector2[])Cuts[index].Clone(); }
        public static CozyCardFragments Tear(RectTransform source, RectTransform layer = null)
        {
            if (source == null || CozyMotion.Reduced || CozyMotion.Paused) return null;
            if (layer == null)
            {
                var canvas = source.GetComponentInParent<Canvas>();
                if (canvas == null) return null;
                layer = canvas.transform as RectTransform;
            }
            var root = new GameObject("SoldCardFragments", typeof(RectTransform), typeof(CozyCardFragments));
            root.transform.SetParent(layer, false);
            var host = (RectTransform)root.transform;
            host.anchorMin = host.anchorMax = layer.pivot;
            host.anchoredPosition = Vector2.zero;
            var effect = root.GetComponent<CozyCardFragments>();
            var visual = source.GetComponent<CozyButtonVisual>(); var selected = visual != null && visual.Selected;
            var motion = source.GetComponent<CozyStickerMotion>(); effect.cssAngle = motion != null ? motion.CssAngle : -source.localEulerAngles.z;
            visual?.SetSelected(false); visual?.ApplyFinalPose(); motion?.ClearVisualPoseForSnapshot();
            effect.origin = host.InverseTransformPoint(source.TransformPoint(source.rect.center));
            var graphics = source.GetComponentsInChildren<Graphic>(false);
            var captures = new Dictionary<Graphic, CozyMeshCapture>();
            foreach (var graphic in graphics)
            {
                if (graphic.transform == source || !graphic.enabled) continue;
                var capture = graphic.gameObject.AddComponent<CozyMeshCapture>();
                captures[graphic] = capture; graphic.SetVerticesDirty();
            }
            Canvas.ForceUpdateCanvases();
            for (var i = 0; i < 3; i++)
            {
                var go = new GameObject("PaperShard" + i, typeof(RectTransform), typeof(CozyPolygonGraphic), typeof(Mask), typeof(CanvasGroup));
                go.transform.SetParent(host, false);
                var piece = (RectTransform)go.transform; piece.sizeDelta = source.rect.size;
                piece.anchoredPosition = effect.origin;
                var polygon = go.GetComponent<CozyPolygonGraphic>(); polygon.SetPolygon(Cuts[i]); polygon.raycastTarget = false;
                go.GetComponent<Mask>().showMaskGraphic = false;
                var alpha = go.GetComponent<CanvasGroup>(); alpha.blocksRaycasts = false; alpha.interactable = false;
                foreach (var graphic in graphics)
                {
                    if (graphic.transform == source || graphic.canvasRenderer.cull || !graphic.enabled) continue;
                    if (!captures.TryGetValue(graphic, out var capture)) continue;
                    var mesh = capture.Captured;
                    if (mesh != null && mesh.vertexCount > 0)
                    {
                        var copy = new GameObject(graphic.name, typeof(RectTransform), typeof(CozySnapshotGraphic));
                        copy.transform.SetParent(piece, false);
                        var local = (RectTransform)copy.transform; local.sizeDelta = source.rect.size;
                        var matrix = Matrix4x4.Translate(-(Vector3)source.rect.center) * source.worldToLocalMatrix * graphic.transform.localToWorldMatrix;
                        copy.GetComponent<CozySnapshotGraphic>().Capture(mesh, matrix, graphic.mainTexture, graphic.material);
                    }

                }
                effect.pieces.Add(piece); effect.offsets.Add((i - 1) * 40 + Random.Range(-10f, 10f));
            }
            foreach (var capture in captures.Values)
                if (Application.isPlaying) Destroy(capture); else DestroyImmediate(capture);
            visual?.SetSelected(selected); visual?.ApplyFinalPose(); motion?.ApplyFinalPose();
            effect.Render(0);
            return effect;
        }
        public void PreviewAt(float seconds) { Render(Mathf.Clamp01(seconds / Duration)); }
        public void Advance(float delta)
        {
            if (CozyMotion.Paused || delta <= 0) return;
            elapsed += delta;
            if (elapsed >= Duration || CozyMotion.Reduced) { Remove(); return; }
            Render(elapsed / Duration);
        }
        void Render(float progress)
        {
            var t = CozyWebMotion.Evaluate(CozyWebMotion.Ease.InQuad, progress);
            for (var i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i]; if (piece == null) continue;
                piece.anchoredPosition = origin + new Vector2(offsets[i], -90) * t;
                piece.localRotation = Quaternion.Euler(0, 0, -Mathf.Lerp(cssAngle, (i - 1) * 24 + 6, t));
                piece.GetComponent<CanvasGroup>().alpha = 1 - t;
            }
        }
        void Remove() { if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject); }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnEnable() { CozyMotion.ReducedChanged += Reduced; CozyMotion.PausedChanged += Paused; }
        void OnDisable() { CozyMotion.ReducedChanged -= Reduced; CozyMotion.PausedChanged -= Paused; }
        void Reduced(bool value) { if (value) Remove(); }
        void Paused(bool value) { if (value) Remove(); }
    }

    /// <summary>Capture the final Image/Text mesh after all existing UI mesh modifiers, using Unity 2021's supported pipeline.</summary>
    public sealed class CozyMeshCapture : BaseMeshEffect
    {
        public Mesh Captured { get; private set; }
        public override void ModifyMesh(VertexHelper vertices)
        {
            if (Captured == null) Captured = new Mesh();
            vertices.FillMesh(Captured);
        }
        protected override void OnDestroy()
        {
            if (Captured != null) { if (Application.isPlaying) Destroy(Captured); else DestroyImmediate(Captured); }
            base.OnDestroy();
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CozySnapshotGraphic : MaskableGraphic
    {
        readonly List<UIVertex> vertices = new List<UIVertex>();
        readonly List<int> indices = new List<int>();
        Texture texture;
        public override Texture mainTexture => texture != null ? texture : base.mainTexture;
        public void Capture(Mesh mesh, Matrix4x4 transform, Texture sourceTexture, Material sourceMaterial)
        {
            texture = sourceTexture; material = sourceMaterial; raycastTarget = false;
            vertices.Clear(); indices.Clear();
            var positions = mesh.vertices; var colors = mesh.colors32; var uv = mesh.uv;
            for (var i = 0; i < positions.Length; i++)
            {
                var vertex = UIVertex.simpleVert; vertex.position = transform.MultiplyPoint3x4(positions[i]);
                vertex.color = colors.Length > i ? colors[i] : (Color32)Color.white;
                if (uv.Length > i) vertex.uv0 = uv[i]; vertices.Add(vertex);
            }
            indices.AddRange(mesh.triangles); SetAllDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh) { mesh.Clear(); mesh.AddUIVertexStream(vertices, indices); }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CozyPolygonGraphic : MaskableGraphic
    {
        Vector2[] points;
        public void SetPolygon(Vector2[] value) { points = (Vector2[])value.Clone(); SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); if (points == null || points.Length < 3) return;
            var r = rectTransform.rect; var vertices = new List<Vector2>();
            foreach (var point in points) vertices.Add(new Vector2(r.xMin + point.x * r.width, r.yMax - point.y * r.height));
            if (SignedArea(vertices) < 0) vertices.Reverse();
            foreach (var point in vertices) mesh.AddVert(point, color, Vector2.zero);
            var indices = new List<int>(); for (var i = 0; i < vertices.Count; i++) indices.Add(i);
            var remaining = vertices.Count * vertices.Count;
            while (indices.Count > 2 && remaining-- > 0)
            {
                var clipped = false;
                for (var i = 0; i < indices.Count; i++)
                {
                    var a = indices[(i + indices.Count - 1) % indices.Count]; var b = indices[i]; var c = indices[(i + 1) % indices.Count];
                    if (Cross(vertices[b] - vertices[a], vertices[c] - vertices[b]) <= 0) continue;
                    var contains = false;
                    foreach (var test in indices)
                        if (test != a && test != b && test != c && Inside(vertices[test], vertices[a], vertices[b], vertices[c])) { contains = true; break; }
                    if (contains) continue;
                    mesh.AddTriangle(a, b, c); indices.RemoveAt(i); clipped = true; break;
                }
                if (!clipped) break;
            }
        }
        public static float SignedArea(IList<Vector2> polygon)
        { float sum = 0; for (var i = 0; i < polygon.Count; i++) sum += Cross(polygon[i], polygon[(i + 1) % polygon.Count]); return sum / 2; }
        static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
        static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        { return Cross(b - a, p - a) >= 0 && Cross(c - b, p - b) >= 0 && Cross(a - c, p - c) >= 0; }
    }
}
