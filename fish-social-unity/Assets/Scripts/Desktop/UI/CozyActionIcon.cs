using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Small outline icons that stay sharp on compact actions.</summary>
    public sealed class CozyActionIcon : MaskableGraphic
    {
        public bool destructive;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = rectTransform.rect;
            var center = r.center;
            var scale = Mathf.Min(r.width, r.height) / 16f;
            if (destructive)
            {
                Line(mesh, center, scale, new Vector2(-5, 3), new Vector2(5, 3));
                Line(mesh, center, scale, new Vector2(-2, 5.5f), new Vector2(2, 5.5f));
                Line(mesh, center, scale, new Vector2(-2, 5.5f), new Vector2(-2, 3));
                Line(mesh, center, scale, new Vector2(2, 5.5f), new Vector2(2, 3));
                Line(mesh, center, scale, new Vector2(-4, 1), new Vector2(-3.5f, -5.5f));
                Line(mesh, center, scale, new Vector2(4, 1), new Vector2(3.5f, -5.5f));
                Line(mesh, center, scale, new Vector2(-3.5f, -5.5f), new Vector2(3.5f, -5.5f));
                Line(mesh, center, scale, new Vector2(-1.4f, 0), new Vector2(-1.4f, -3.5f));
                Line(mesh, center, scale, new Vector2(1.4f, 0), new Vector2(1.4f, -3.5f));
            }
            else
            {
                const int steps = 40;
                for (var i = 0; i < steps; i++)
                {
                    var a = i * Mathf.PI * 2 / steps;
                    var b = (i + 1) * Mathf.PI * 2 / steps;
                    Line(mesh, center, scale, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6,
                        new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 6);
                }
                Line(mesh, center, scale, new Vector2(0, -3), new Vector2(0, .5f));
                Line(mesh, center, scale, new Vector2(0, 3), new Vector2(0, 3.3f));
            }
        }

        void Line(VertexHelper mesh, Vector2 center, float scale, Vector2 from, Vector2 to)
        {
            var width = (to - from).normalized;
            var normal = new Vector2(-width.y, width.x) * .65f;
            var start = mesh.currentVertCount;
            mesh.AddVert(center + (from - normal) * scale, color, Vector2.zero);
            mesh.AddVert(center + (from + normal) * scale, color, Vector2.zero);
            mesh.AddVert(center + (to + normal) * scale, color, Vector2.zero);
            mesh.AddVert(center + (to - normal) * scale, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
