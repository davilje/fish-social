using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Authored collection slots keep species, metadata and stamp in separate bounded areas.</summary>
    public sealed class CozyProfileCollectionCard : MonoBehaviour
    {
        [SerializeField] Text title,metadata,ribbon,empty;
        [SerializeField] Image art;
        [SerializeField] GameObject stamp;
        public void Configure(Text title,Text metadata,Text ribbon,Text empty,Image art,GameObject stamp)
        {this.title=title;this.metadata=metadata;this.ribbon=ribbon;this.empty=empty;this.art=art;this.stamp=stamp;}
        public void Bind(string name,string detail,Sprite sprite,bool locked=false,bool collected=false,int slot=0)
        {
            if(title!=null){title.text=name??"";title.supportRichText=false;}
            if(metadata!=null)metadata.text=detail??"";
            if(ribbon!=null){ribbon.text=slot>0?"展示 "+slot:"";ribbon.transform.parent.gameObject.SetActive(slot>0);}
            if(empty!=null)empty.gameObject.SetActive(sprite==null&&slot>0);
            if(art!=null){art.sprite=sprite;art.enabled=sprite!=null;art.color=locked?new Color(.3f,.25f,.2f,.45f):Color.white;}
            if(stamp!=null)stamp.SetActive(collected);
        }
    }
}
