using UnityEngine;
using UnityEngine.UI;
using FishSocial.Motion;

namespace FishSocial.Desktop.Pet
{
    [DisallowMultipleComponent]
    public sealed class FishingRestLayerEffect : BaseMeshEffect
    {
        public FishingRestTransform Pose { get; private set; } = FishingRestTransform.Identity;
        public double Nod { get; private set; }

        public void SetPose(FishingRestTransform pose, double nod = 0)
        {
            if (pose.XX == Pose.XX && pose.XY == Pose.XY && pose.YX == Pose.YX && pose.YY == Pose.YY &&
                pose.OffsetX == Pose.OffsetX && pose.OffsetY == Pose.OffsetY && nod == Nod)
                return;
            Pose = pose;
            Nod = nod;
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            if (!IsActive() || graphic == null)
                return;
            var rect = graphic.rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0)
                return;
            if (Nod > .0001 && vertices.currentVertCount == 4)
            {
                var bl = UIVertex.simpleVert; var tl = bl; var tr = bl; var br = bl;
                vertices.PopulateUIVertex(ref bl, 0); vertices.PopulateUIVertex(ref tl, 1);
                vertices.PopulateUIVertex(ref tr, 2); vertices.PopulateUIVertex(ref br, 3);
                vertices.Clear();
                for (var row = 0; row <= 16; row++)
                    for (var col = 0; col <= 10; col++)
                    {
                        var u = col / 10f; var v = row / 16f;
                        var item = tl;
                        item.uv0 = Vector4.Lerp(Vector4.Lerp(tl.uv0,tr.uv0,u), Vector4.Lerp(bl.uv0,br.uv0,u),v);
                        var original = Vector3.Lerp(Vector3.Lerp(tl.position,tr.position,u),Vector3.Lerp(bl.position,br.position,u),v);
                        item.position = TransformPoint(original, rect, Pose, Nod);
                        vertices.AddVert(item);
                    }
                for (var row = 0; row < 16; row++)
                    for (var col = 0; col < 10; col++)
                    {
                        var i = row * 11 + col;
                        vertices.AddTriangle(i,i+1,i+12); vertices.AddTriangle(i,i+12,i+11);
                    }
                return;
            }
            var vertex = UIVertex.simpleVert;
            for (var index = 0; index < vertices.currentVertCount; index++)
            {
                vertices.PopulateUIVertex(ref vertex, index);
                vertex.position = TransformPoint(vertex.position, rect, Pose);
                vertices.SetUIVertex(vertex, index);
            }
        }

        public static Vector3 TransformPoint(Vector3 point, Rect rect, FishingRestTransform pose, double nod = 0)
        {
            if (rect.width <= 0 || rect.height <= 0)
                return point;
            var sourceX = (point.x - rect.xMin) * FishingLayerArt.CanvasWidth / rect.width;
            var sourceY = (rect.yMax - point.y) * FishingLayerArt.CanvasHeight / rect.height;
            FishingRestGeometry.Point(sourceX, sourceY, nod, out var x, out var y);
            return new Vector3(
                rect.xMin + (float)(pose.X(x, y) * rect.width / FishingLayerArt.CanvasWidth),
                rect.yMax - (float)(pose.Y(x, y) * rect.height / FishingLayerArt.CanvasHeight),
                point.z);
        }
    }
}
