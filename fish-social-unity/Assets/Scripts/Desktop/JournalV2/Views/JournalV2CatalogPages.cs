using System;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    public static partial class JournalV2Pages
    {
        public static JournalV2Page BuildCatalog(Transform parent,string id)
        {
            if(id!="shop" && id!="equip" && id!="bag" && id!="map" && id!="ponds" && id!="gallery")return null;
            var title=id=="shop"?"猫咪商店":id=="equip"?"我的装备":id=="bag"?"我的背包":id=="map"?"钓鱼世界":id=="ponds"?"选择鱼塘":"鱼类图鉴";
            var icon=id=="shop"?"shop":id=="equip"?"rod":id=="bag"?"bag":id=="gallery"?"book":"map";
            var page=Frame(parent,id,title,id=="map"?"跟着水流，去新的地方":id=="gallery"?"把每一次相遇，收藏进手账":"把喜欢的事，慢慢记下来",icon,JournalV2Theme.Coral);
            if(id=="map" || id=="ponds")page.gameObject.AddComponent<JournalV2PondController>().Build(page,id=="map");
            else if(id=="gallery")page.gameObject.AddComponent<JournalV2GalleryController>().Build(page);
            else page.gameObject.AddComponent<JournalV2CatalogController>().Build(page,id);
            return page;
        }
    }

    internal static class JournalV2CatalogUi
    {
        public static RectTransform Visual(Button button) => button.transform.Find("Visual") as RectTransform;
        public static void Clear(Transform parent)
        {
            for(var i=parent.childCount-1;i>=0;i--)
            { var child=parent.GetChild(i).gameObject;child.SetActive(false);child.transform.SetParent(null,false);if(Application.isPlaying)UnityEngine.Object.Destroy(child);else UnityEngine.Object.DestroyImmediate(child); }
        }
        public static void Label(Button button,string text)
        { var label=Visual(button)?.Find("Label")?.GetComponent<Text>();if(label!=null)DesktopLocalization.SetText(label, text??""); }
        public static void Enabled(Button button,bool enabled)
        { if(button==null)return;button.interactable=enabled;button.GetComponent<JournalV2Button>()?.SetInteractable(enabled); }
        public static Text Facts(Transform parent,string name,float x,float y,float w,float h)
        {
            var scroll=JournalV2Nodes.VerticalScroll(parent,name,x,y,w,h);
            var text=JournalV2Nodes.Text(scroll.content,"Text","",0,0,w-12,h,16,TextAnchor.UpperLeft);
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;
            var fit=text.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            return text;
        }
        public static Button Card(Transform parent,string id,string title,string meta,Sprite art,Vector2 size,int ordinal,bool observed=false)
        {
            var button=JournalV2Nodes.Button(parent,id,"",null,0,0,size.x,size.y,JournalV2ButtonKind.Sticker);
            var visual=Visual(button);
            var wellHeight=size.y==220?140:112;
            var well=JournalV2Nodes.Plate(visual,"ArtWell",10,10,size.x-20,wellHeight,JournalV2Theme.ArtWell,12,0);well.ShadowColor=Color.clear;
            JournalV2Nodes.Image(visual,"Artwork",art,10+(size.x-20)*.07f,20,(size.x-20)*.86f,wellHeight-20).preserveAspect=true;
            JournalV2Nodes.Text(visual,"Name",title,10,wellHeight+18,size.x-20,26,20,TextAnchor.MiddleCenter);
            var info=JournalV2Nodes.Text(visual,"Meta",meta,10,wellHeight+48,size.x-20,20,15,TextAnchor.MiddleCenter);
            var tape=JournalV2Nodes.Image(visual,"Tape",JournalV2Theme.Sprite("decor/tape-pink-stripe"),-18,-8,104,26);tape.rectTransform.pivot=new Vector2(.5f,.5f);tape.rectTransform.anchoredPosition=new Vector2(34,-5);tape.rectTransform.localEulerAngles=new Vector3(0,0,28);info.color=JournalV2Theme.Soft;
            var motion=button.GetComponent<JournalV2Button>();motion.Configure(visual,JournalV2ButtonKind.Sticker,id);
            if(Application.isPlaying){if(observed)motion.PlayObservedEntrance(ordinal);else if(ordinal<8)motion.PlayEntrance(ordinal);}
            return button;
        }
        public static Sprite Fish(string id)
        {
            id=DesktopGameData.ResolveSpeciesId(id);if(id=="largemouth_bass")id="black_bass";
            return JournalV2Theme.Sprite(DesktopLocalization.Concat("journal/illustrations/fish/", id));
        }
        public static Sprite Product(string kind,string id)
        {
            string art=null;
            if(kind=="rod")switch(id){case "rod-bamboo":art="rod-bamboo";break;case "rod-tai":case "rod-stream":art="rod-light";break;case "rod-iso":case "rod-surf":case "rod-giant":art="rod-long";break;case "rod-lure":case "rod-heavy":art="rod-wood";break;}
            else if(kind=="bait")switch(id){case "bait-basic":art="bait-basic";break;case "bait-veg":art="bait-wheat";break;case "bait-mix":art="bait-mixed";break;case "bait-meat":art="bait-shrimp";break;}
            else art=id=="vessel-raft"||(id??"").Contains("wood")?"boat-wood":"boat-blue";
            return art==null?null:JournalV2Theme.Sprite(DesktopLocalization.Concat("journal/illustrations/items/", art));
        }
        public static void Photo(RectTransform photo,Sprite art,string title,bool reveal)
        {
            var image=photo.Find("Visual/Art")?.GetComponent<Image>();if(image!=null){image.sprite=art;image.enabled=art!=null;image.preserveAspect=true;}
            var caption=photo.Find("Visual/Caption")?.GetComponent<Text>();if(caption!=null)DesktopLocalization.SetText(caption, title??"");
            if(reveal)photo.GetComponent<JournalV2Polaroid>()?.PlayReveal(new Vector2(24,0),.26f,.3f);
        }
        public static bool Has(string[] values,string id) => values!=null && Array.IndexOf(values,id)>=0;
        public static string Date(long milliseconds)
        { try{return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToLocalTime().ToString("yyyy.MM.dd");}catch{return "—";} }
    }
}
