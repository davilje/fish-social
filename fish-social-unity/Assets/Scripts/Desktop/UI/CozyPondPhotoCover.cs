using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>CSS object-fit: cover using UV cropping; the authored photo rectangle never moves.</summary>
    [DisallowMultipleComponent]
    public sealed class CozyPondPhotoCover : BaseMeshEffect
    {
        public static Vector2 CropScale(Vector2 source, Vector2 destination)
        {
            if (source.x <= 0 || source.y <= 0 || destination.x <= 0 || destination.y <= 0) return Vector2.one;
            var ratio = (destination.x / destination.y) / (source.x / source.y);
            return ratio < 1 ? new Vector2(ratio, 1) : new Vector2(1, 1 / ratio);
        }
        public override void ModifyMesh(VertexHelper vertices)
        {
            var image = graphic as Image;
            if (!IsActive() || image == null || image.sprite == null) return;
            var scale = CropScale(image.sprite.rect.size, image.rectTransform.rect.size);
            var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(image.sprite);
            var center = new Vector2((uv.x + uv.z) * .5f, (uv.y + uv.w) * .5f);
            var vertex = new UIVertex();
            for (var i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                var value = center + Vector2.Scale((Vector2)vertex.uv0 - center, scale);
                vertex.uv0 = new Vector4(value.x, value.y, vertex.uv0.z, vertex.uv0.w);
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
