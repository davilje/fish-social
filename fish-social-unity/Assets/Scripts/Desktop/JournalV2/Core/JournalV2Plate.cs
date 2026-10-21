using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    /// <summary>CSS box paint only. Text, icons, hit geometry and animation remain separate native nodes.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class JournalV2Plate : Image
    {
        public float Radius=16;
        public float BorderWidth=2;
        public Color BorderColor=JournalV2Theme.Line;
        public Color ShadowColor=new Color(117/255f,71/255f,46/255f,.16f);
        public Vector2 ShadowOffset=new Vector2(0,-4);
        public float ShadowBlur;
        public bool VerticalGradient;
        public Color BottomColor=Color.white;
        public void Refresh() { SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            if(sprite!=null) {base.OnPopulateMesh(mesh);return;}
            mesh.Clear();var box=rectTransform.rect;
            if(ShadowColor.a>0)
            {
                var passes=ShadowBlur>0?8:1;
                for(var i=passes-1;i>=0;i--)
                {
                    var spread=ShadowBlur*i/passes;
                    var r=new Rect(box.x+ShadowOffset.x-spread,box.y+ShadowOffset.y-spread,box.width+spread*2,box.height+spread*2);
                    var c=ShadowColor;c.a*=passes>1?1f/passes:1;
                    Rounded(mesh,r,Radius+spread,c);
                }
            }
            if(BorderWidth>0) Rounded(mesh,box,Radius,BorderColor);
            var inset=Mathf.Max(0,BorderWidth);
            Rounded(mesh,new Rect(box.x+inset,box.y+inset,Mathf.Max(0,box.width-inset*2),Mathf.Max(0,box.height-inset*2)),Mathf.Max(0,Radius-inset),color,VerticalGradient?(Color?)BottomColor:null);
        }
        static void Rounded(VertexHelper mesh,Rect r,float radius,Color color,Color? bottom=null)
        {
            if(r.width<=0||r.height<=0||color.a<=0)return;
            radius=Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f);
            var center=mesh.currentVertCount;
            mesh.AddVert(r.center,bottom.HasValue?Color.Lerp(bottom.Value,color,.5f):color,Vector2.zero);
            const int steps=10;
            var corners=new[]{new Vector2(r.xMax-radius,r.yMax-radius),new Vector2(r.xMin+radius,r.yMax-radius),new Vector2(r.xMin+radius,r.yMin+radius),new Vector2(r.xMax-radius,r.yMin+radius)};
            for(var q=0;q<4;q++) for(var i=0;i<=steps;i++)
            {
                var angle=(q*90+i*90f/steps)*Mathf.Deg2Rad;
                var point=corners[q]+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                mesh.AddVert(point,bottom.HasValue?Color.Lerp(bottom.Value,color,(point.y-r.yMin)/r.height):color,Vector2.zero);
            }
            var count=4*(steps+1);
            for(var i=0;i<count;i++)mesh.AddTriangle(center,center+1+i,center+1+(i+1)%count);
        }
    }
}
