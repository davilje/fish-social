using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Pet
{
    [DisallowMultipleComponent]
    public sealed class FishingRestLayerEffect : BaseMeshEffect
    {
        public FishingRestTransform Pose { get; private set; } = FishingRestTransform.Identity;

        public void SetPose(FishingRestTransform pose)
        {
            if (pose.XX == Pose.XX && pose.XY == Pose.XY && pose.YX == Pose.YX && pose.YY == Pose.YY &&
                pose.OffsetX == Pose.OffsetX && pose.OffsetY == Pose.OffsetY)
                return;
            Pose = pose;
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
            var vertex = UIVertex.simpleVert;
            for (var index = 0; index < vertices.currentVertCount; index++)
            {
                vertices.PopulateUIVertex(ref vertex, index);
                vertex.position = TransformPoint(vertex.position, rect, Pose);
                vertices.SetUIVertex(vertex, index);
            }
        }

        public static Vector3 TransformPoint(Vector3 point, Rect rect, FishingRestTransform pose)
        {
            if (rect.width <= 0 || rect.height <= 0)
                return point;
            var sourceX = (point.x - rect.xMin) * FishingLayerArt.CanvasWidth / rect.width;
            var sourceY = (rect.yMax - point.y) * FishingLayerArt.CanvasHeight / rect.height;
            return new Vector3(
                rect.xMin + (float)(pose.X(sourceX, sourceY) * rect.width / FishingLayerArt.CanvasWidth),
                rect.yMax - (float)(pose.Y(sourceX, sourceY) * rect.height / FishingLayerArt.CanvasHeight),
                point.z);
        }
    }
}
