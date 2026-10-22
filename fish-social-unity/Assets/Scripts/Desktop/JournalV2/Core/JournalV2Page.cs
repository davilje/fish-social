using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2Page : MonoBehaviour
    {
        public string PageId;
        public bool Modal;
        public RectTransform Root => (RectTransform)transform;
        public RectTransform Body,Rail,Middle,Aside;
        public Text Heading,Status,HeaderTitle;
        public Button CloseButton;
    }

    public abstract class JournalV2Controller : MonoBehaviour
    {
        [System.NonSerialized] public JournalV2App App;
        public JournalV2Page Page;
        protected int Generation;
        public virtual bool CanClose => true;
        public virtual void Bind(JournalV2App app,JournalV2Page page) {App=app;Page=page;}
        public virtual void Open(int tab=0,string targetId=null) {++Generation;}
        public virtual void Close() {++Generation;StopAllCoroutines();}
        protected void Error(string message) {if(Page!=null && Page.Status!=null)Page.Status.text=message??"暂时无法加载，请重试。";}
    }

    public static partial class JournalV2Pages
    {
        public static JournalV2Page Frame(Transform parent,string id,string title,string subtitle,string icon,Color accent)
        {
            var root=JournalV2Nodes.Rect(parent,id,160,150,1370,650);
            var page=root.gameObject.AddComponent<JournalV2Page>();page.PageId=id;
            var paper=JournalV2Nodes.Plate(root,"Paper",0,0,1370,650,JournalV2Theme.Paper,22,2);
            paper.ShadowOffset=new Vector2(0,-8);paper.ShadowBlur=16;paper.BorderColor=new Color32(239,223,196,255);
            var dots=JournalV2Nodes.Image(root,"Dots",JournalV2Theme.Sprite("textures/paper-dots"),24,24,1322,602);dots.type=Image.Type.Tiled;dots.preserveAspect=false;
            JournalV2Nodes.Image(root,"Stitch",JournalV2Theme.Sprite("decor/page-stitch"),10,10,1350,630);
            var header=JournalV2Nodes.Rect(root,"Header",0,0,1370,96);
            JournalV2Nodes.Image(header,"Icon",JournalV2Theme.Sprite("icons/"+icon),34,26,44,44);
            var underline=JournalV2Nodes.Plate(header,"Underline",92,53,Mathf.Max(68,title.Length*34)+16,15,new Color(accent.r,accent.g,accent.b,.45f),3,0);underline.ShadowColor=Color.clear;
            page.HeaderTitle=JournalV2Nodes.Text(header,"Title",title,92,22,310,50,34);
            JournalV2Nodes.Text(header,"Subtitle",subtitle,380,36,850,26,18).color=JournalV2Theme.Soft;
            var rule=JournalV2Nodes.Plate(header,"Rule",30,94,1310,2,new Color(117/255f,71/255f,46/255f,.1f),0,0);rule.ShadowColor=Color.clear;
            page.CloseButton=JournalV2Nodes.Button(header,"Close","×",null,1294,24,52,52);
            page.Body=JournalV2Nodes.Rect(root,"Body",0,0,1370,650);
            page.Rail=JournalV2Nodes.Rect(page.Body,"Rail",24,112,210,496);
            var rail=JournalV2Nodes.Plate(page.Rail,"Paper",0,0,210,496,new Color(243/255f,230/255f,207/255f,.85f),16,0);rail.ShadowColor=Color.clear;
            page.Middle=JournalV2Nodes.Rect(page.Body,"Middle",254,160,710,440);
            page.Aside=JournalV2Nodes.Rect(page.Body,"Aside",984,112,362,496);
            var aside=JournalV2Nodes.Plate(page.Aside,"Paper",0,0,362,496,new Color(255/255f,253/255f,247/255f,.92f),16,2);aside.BorderColor=new Color32(239,223,196,255);aside.ShadowColor=Color.clear;
            page.Heading=JournalV2Nodes.Text(page.Body,"Heading",title,254,114,710,40,24);
            page.Status=JournalV2Nodes.Text(page.Body,"Status","",254,610,1068,26,16);page.Status.color=JournalV2Theme.Soft;
            for(var i=0;i<3;i++)JournalV2Nodes.Image(root,"Ring"+i,JournalV2Theme.Sprite("decor/binder-ring"),-34,140+160*i,74,44);
            header.SetAsLastSibling();
            var animator=root.gameObject.AddComponent<JournalV2PageAnimator>();animator.Configure(page.Body,page.HeaderTitle.rectTransform,page.Rail,page.Middle,page.Aside);
            return page;
        }
    }
}
