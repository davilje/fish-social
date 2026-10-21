using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Native .tab paint: only fill interpolates; border, shadow and index notch switch immediately.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CozyIndexGraphic : MaskableGraphic
    {
        bool selected;
        float blend;
        public void SetState(bool selection,float amount)
        { if(selected==selection && Mathf.Approximately(blend,amount))return;selected=selection;blend=amount;SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;
            var coral=new Color32(252,146,60,255);
            var shadow=r;shadow.y-=4;
            Rounded(mesh,shadow,18,selected?(Color)new Color32(212,111,34,255):(Color)new Color32(230,210,176,255));
            Rounded(mesh,r,18,selected?(Color)coral:(Color)new Color32(232,214,182,255));
            var inner=new Rect(r.x+2,r.y+2,r.width-4,r.height-4);
            Rounded(mesh,inner,16,Color.Lerp(new Color32(255,253,247,255),coral,blend));
            var notch=new Rect(r.x,r.y+20,8,r.height-40);
            Rounded(mesh,notch,6,selected?new Color(1,1,1,.45f):new Color(117/255f,71/255f,46/255f,.08f),true);
        }
        static void Rounded(VertexHelper mesh,Rect r,float radius,Color color,bool flatLeft=false)
        {
            var start=mesh.currentVertCount;mesh.AddVert(r.center,color,Vector2.zero);
            const int steps=12;
            for(var corner=0;corner<4;corner++)
            {
                var rad=flatLeft && (corner==1 || corner==2)?0:radius;
                var center=new Vector2(corner==0 || corner==3?r.xMax-rad:r.xMin+rad,corner<2?r.yMax-rad:r.yMin+rad);
                for(var i=0;i<=steps;i++)
                {
                    var angle=(corner*90+i*90f/steps)*Mathf.Deg2Rad;
                    mesh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*rad,color,Vector2.zero);
                    var index=mesh.currentVertCount-1;if(index>start+1)mesh.AddTriangle(start,index-1,index);
                }
            }
            mesh.AddTriangle(start,mesh.currentVertCount-1,start+1);
        }
    }
}
