using UnityEngine;
using UnityEngine.UI;
namespace FishSocial.Desktop
{
    /// <summary>The original medal's radial gold fill, drawn as UI geometry rather than a square paper sprite.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CozyJournalMedalGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var rect=rectTransform.rect;var center=rect.center;var radius=Mathf.Min(rect.width,rect.height)/2;
            Ring(mesh,center+Vector2.down*4,radius,new Color32(217,178,90,255),new Color32(217,178,90,255));
            Ring(mesh,center,radius,new Color32(255,246,216,255),new Color32(243,213,138,255));
        }
        void Ring(VertexHelper mesh,Vector2 center,float radius,Color inner,Color outer)
        {
            var start=mesh.currentVertCount;mesh.AddVert(center,inner*color,Vector2.zero);
            const int count=64;
            for(var i=0;i<=count;i++){var a=i*2*Mathf.PI/count;mesh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,outer*color,Vector2.zero);if(i>0)mesh.AddTriangle(start,start+i,start+i+1);}
        }
    }
}
