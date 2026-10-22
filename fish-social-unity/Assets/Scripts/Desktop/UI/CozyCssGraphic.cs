using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Native geometry for the prototype's CSS circles, elliptical gradient and rounded paper; no generated textures.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CozyCssGraphic : MaskableGraphic
    {
        public enum Shape { Circle, Water, Paper, Stamp, Ring }
        public Shape shape;
        public float radius = 22;
        public float border = 8;
        public Color borderColor = Color.white;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = rectTransform.rect;
            if (shape == Shape.Water)
            {
                Water(mesh, r);
            }
            else if (shape == Shape.Ring)
                Ellipse(mesh, r.center, r.size * .5f, 1 - border / Mathf.Min(r.width, r.height) * 2, 1, color, color);
            else if (shape == Shape.Stamp)
            {
                var size = r.size * .5f;
                Ellipse(mesh, r.center, size, 0, 1, new Color(1, .941f, .922f, .25f), new Color(1, .941f, .922f, .25f));
                var thickness = border / Mathf.Min(size.x, size.y) / 3;
                Ellipse(mesh, r.center, size, 1 - thickness, 1, color, color);
                Ellipse(mesh, r.center, size, 1 - thickness * 3, 1 - thickness * 2, color, color);
            }
            else if (shape == Shape.Circle) Ellipse(mesh, r.center, r.size * .5f, 0, 1, color, color);
            else
            {
                // CSS white outline is external; preserve the declared 500x380 content box.
                var outline = new Rect(r.x - border, r.y - border, r.width + border * 2, r.height + border * 2);
                Rounded(mesh, outline, radius + border, borderColor);
                Rounded(mesh, r, radius, color);
            }
        }
        static void Water(VertexHelper mesh, Rect r)
        {
            var gradientCenter = new Vector2(r.center.x, r.yMax - r.height * .4f);
            var gradientRadius = new Vector2(r.width * .5f, r.height * .6f) * Mathf.Sqrt(2);
            const int count = 80;
            for (var ring = 0; ring < 20; ring++)
            {
                var start = mesh.currentVertCount;
                for (var i = 0; i <= count; i++)
                {
                    var angle = i * Mathf.PI * 2 / count;
                    var direction = new Vector2(Mathf.Cos(angle) * r.width * .5f, Mathf.Sin(angle) * r.height * .5f);
                    for (var side = 0; side < 2; side++)
                    {
                        var point = r.center + direction * ((ring + side) / 20f);
                        var delta = point - gradientCenter;
                        var distance = new Vector2(delta.x / gradientRadius.x, delta.y / gradientRadius.y).magnitude;
                        var centerColor = new Color(159f / 255, 212f / 255, 242f / 255);
                        var edgeColor = new Color(93f / 255, 173f / 255, 226f / 255);
                        var color = Color.Lerp(centerColor, edgeColor, distance / .6f);
                        if (distance > .6f) color.a = Mathf.Clamp01(1 - (distance - .6f) / .12f);
                        mesh.AddVert(point, color, Vector2.zero);
                    }
                    if (i == 0) continue;
                    var p = start + i * 2;
                    mesh.AddTriangle(p - 2, p - 1, p); mesh.AddTriangle(p, p - 1, p + 1);
                }
            }
        }

        static void Ellipse(VertexHelper mesh, Vector2 center, Vector2 size, float inner, float outer, Color a, Color b)
        {
            const int count = 80;
            var start = mesh.currentVertCount;
            for (var i = 0; i <= count; i++)
            {
                var angle = i * Mathf.PI * 2 / count;
                var direction = new Vector2(Mathf.Cos(angle) * size.x, Mathf.Sin(angle) * size.y);
                mesh.AddVert(center + direction * inner, a, Vector2.zero);
                mesh.AddVert(center + direction * outer, b, Vector2.zero);
                if (i == 0) continue;
                var p = start + i * 2;
                mesh.AddTriangle(p - 2, p - 1, p); mesh.AddTriangle(p, p - 1, p + 1);
            }
        }
        /// <summary>Flat CSS fill. Clears a sliced sprite on the host so the wash is the only plate.</summary>
        public static CozyCssGraphic EnsureWash(Transform host, string washName, Color fill, float radius, float border, Color borderColor)
        {
            if (host == null) return null;
            var image = host.GetComponent<Image>();
            if (image != null)
            {
                image.overrideSprite = null;
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.color = new Color(1f, 1f, 1f, 0f);
            }
            var wash = host.Find(washName);
            if (wash == null)
            {
                var go = new GameObject(washName, typeof(RectTransform), typeof(CozyCssGraphic));
                go.transform.SetParent(host, false);
                wash = go.transform;
            }
            wash.SetAsFirstSibling();
            var rect = (RectTransform)wash;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            var graphic = wash.GetComponent<CozyCssGraphic>();
            graphic.shape = Shape.Paper;
            graphic.radius = radius;
            graphic.border = border;
            graphic.borderColor = borderColor;
            graphic.color = fill;
            graphic.raycastTarget = false;
            graphic.enabled = true;
            wash.gameObject.SetActive(true);
            return graphic;
        }

        static void Rounded(VertexHelper mesh, Rect r, float radius, Color color)
        {
            var start = mesh.currentVertCount;
            radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) / 2);
            mesh.AddVert(r.center, color, Vector2.zero);
            const int steps = 10;
            for (var corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner == 0 || corner == 3 ? r.xMax - radius : r.xMin + radius,
                    corner < 2 ? r.yMax - radius : r.yMin + radius);
                for (var i = 0; i <= steps; i++)
                {
                    var angle = (corner * 90 + i * 90f / steps) * Mathf.Deg2Rad;
                    mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
                    var index = mesh.currentVertCount - 1;
                    if (index > start + 1) mesh.AddTriangle(start, index - 1, index);
                }
            }
            mesh.AddTriangle(start, mesh.currentVertCount - 1, start + 1);
        }
    }
}
