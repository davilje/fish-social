using UnityEngine;
using UnityEngine.UI;
namespace FishSocial.Desktop
{
    /// <summary>Native .sticker .art rounded fill, including the showcase's vertical paper gradient.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CozyProfileArtWell : MaskableGraphic
    {
        public Color Top = new Color32(251,243,228,255);
        public Color Bottom = new Color32(251,243,228,255);
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;const float radius=12;
            mesh.AddVert(r.center,ColorAt(r.center.y,r),Vector2.zero);
            var count=0;
            for(var corner=0;corner<4;corner++)
            {
                var c=new Vector2(corner==0||corner==3?r.xMax-radius:r.xMin+radius,corner<2?r.yMax-radius:r.yMin+radius);
                for(var i=0;i<=8;i++)
                {
                    var a=(corner*90+i*90f/8)*Mathf.Deg2Rad;
                    var point=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                    mesh.AddVert(point,ColorAt(point.y,r),Vector2.zero);count++;
                    if(count>1)mesh.AddTriangle(0,count-1,count);
                }
            }
            mesh.AddTriangle(0,count,1);
        }
        Color ColorAt(float y,Rect r){return Color.Lerp(Bottom,Top,Mathf.InverseLerp(r.yMin,r.yMax,y))*color;}
    }
}
