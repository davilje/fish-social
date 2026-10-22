#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        public static void ApplyProfileRefinement(GameObject root)
        {
            if(root.name=="AchievementRow"){RefineAchievement(root.transform);return;}
            if(root.name=="GallerySpeciesSlot")
            {
                var node=root.transform;
                RefineStamp(node,"JournalStamp",167,-12,54,"已收录",13);
                var stamp=node.Find("JournalStamp");stamp.gameObject.SetActive(false);
                var name=DpText(node,"Label",12,128,186,27,18,false,TextAnchor.MiddleCenter);
                var meta=DpText(node,"Rarity",12,158,186,23,13,true,TextAnchor.MiddleCenter);
                var view=root.GetComponent<CozyProfileCollectionCard>()??root.AddComponent<CozyProfileCollectionCard>();
                view.Configure(name,meta,null,null,node.Find("FishArt")?.GetComponent<Image>(),stamp.gameObject);
                return;
            }
            if(root.name!="PanelProfileHub"&&root.name!="PanelAchievements")return;
            var profile=root.transform.Find("Shell/Content/Tab_资料");
            var fill=profile?.Find("JournalProgress/Track/Fill")?.GetComponent<Image>();
            if(fill!=null)
            {
                var track=fill.transform.parent.GetComponent<Image>();track.type=Image.Type.Sliced;track.preserveAspect=false;
                var progress=fill.GetComponent<CozyJournalProgressFill>()??fill.gameObject.AddComponent<CozyJournalProgressFill>();progress.Refresh();
            }
            RefineCollection(root.transform,"展示柜",215,250);
            RefineCollection(root.transform,"图鉴",156,188);
        }
        static void RefineCollection(Transform root,string kind,float width,float height)
        {
            var tab=root.Find("Shell/Content/Tab_"+kind);var card=tab?.Find("JournalCardTemplate");if(card==null)return;
            var showcase=kind=="展示柜";
            JournalCard(card.gameObject);
            card.GetComponent<CozyButtonVisual>()?.ConfigureSurface("surfaces/sticker-card","surfaces/sticker-card","surfaces/sticker-card","surfaces/sticker-card","surfaces/sticker-card");
            Hide(card,"JournalCardPaper");
            var tape=JournalSurface(card,"JournalTape",showcase?"decor/tape-gold-dots":"decor/tape-pink-stripe",-18,-8,104,26);
            DpCenter(tape.rectTransform);
            tape.rectTransform.localEulerAngles=new Vector3(0,0,28);tape.gameObject.SetActive(false);
            var content=tab.Find((showcase?"ShowcaseGrid":"CodexGrid")+"/Viewport/Content");
            var grid=content?.GetComponent<GridLayoutGroup>();
            if(grid!=null){grid.padding=new RectOffset(14,14,showcase?34:24,22);grid.spacing=new Vector2(showcase?22:18,24);}
            var paper=Place(card,"JournalArtWell",10,10,width-20,showcase?170:110);
            Hide(paper,"JournalPaper");
            var old=paper.GetComponent<Image>();if(old!=null)UnityEngine.Object.DestroyImmediate(old);
            if(paper.GetComponent<CanvasRenderer>()==null)paper.gameObject.AddComponent<CanvasRenderer>();
            var well=paper.GetComponent<CozyProfileArtWell>()??paper.gameObject.AddComponent<CozyProfileArtWell>();
            well.Top=showcase?(Color)new Color32(244,230,203,255):(Color)new Color32(251,243,228,255);
            well.Bottom=showcase?(Color)new Color32(255,248,235,255):(Color)new Color32(251,243,228,255);well.raycastTarget=false;
            card.Find("CozyVisual")?.SetAsFirstSibling();
            paper.SetSiblingIndex(1);
            var artWidth=(width-20)*.86f;
            var art=Picture(card,"DesignItemArt",null,(width-artWidth)/2,showcase?30:19,artWidth,showcase?130:92);
            var name=DpText(card,"Label",10,showcase?188:128,width-20,27,18,false,TextAnchor.MiddleCenter);
            var meta=DpText(card,"JournalMetadata",10,showcase?218:158,width-20,23,13,true,TextAnchor.MiddleCenter);
            var ribbon=JournalSurface(card,"JournalRibbon","surfaces/category-chip",width-72,10,80,24);
            ribbon.color=new Color32(247,182,200,255);
            var label=DpText(ribbon.transform,"Label",0,0,80,24,12,false,TextAnchor.MiddleCenter);label.color=Color.white;
            var empty=DpText(card,"JournalEmpty",12,showcase?45:25,width-24,80,55,true,TextAnchor.MiddleCenter,"＋");empty.gameObject.SetActive(false);
            var stamp=card.Find("JournalStamp");
            RefineStamp(card,"JournalStamp",width-43,-12,54,"已收录",13);
            stamp.gameObject.SetActive(false);ribbon.gameObject.SetActive(showcase);
            var view=card.GetComponent<CozyProfileCollectionCard>()??card.gameObject.AddComponent<CozyProfileCollectionCard>();view.Configure(name,meta,label,empty,art,stamp.gameObject);
            if(showcase)
            {
                Hide(tab,"ShowcaseDetailRows");
                DpText(tab,"Detail",754,316,314,88,17,true,TextAnchor.UpperLeft);
            }
            else
            {
                DpText(tab,"Detail",754,306,314,30,15,true,TextAnchor.UpperLeft);
                for(var i=0;i<4;i++)
                {
                    var record=Place(tab,"JournalRecord"+i,754,350+i*28,314,23);
                    DpText(record,"Label",0,0,94,23,15,true);
                    DpText(record,"Value",94,0,220,23,15,false,TextAnchor.MiddleRight);
                }
            }
        }
        static void RefineStamp(Transform root,string path,float x,float y,float size,string label,int fontSize)
        {
            var stamp=Place(root,path,x,y,size,size);stamp.localRotation=Quaternion.identity;DpCenter(stamp);stamp.localEulerAngles=new Vector3(0,0,14);
            var image=stamp.GetComponent<Image>();if(image!=null){image.color=new Color(205f/255,64f/255,46f/255,.9f);image.raycastTarget=false;}
            var text=DpText(stamp,"Label",0,0,size,size,fontSize,false,TextAnchor.MiddleCenter,label);text.color=new Color(205f/255,64f/255,46f/255,.92f);
            stamp.SetAsLastSibling();
        }
        static void RefineAchievement(Transform root)
        {
            var medal=Place(root,"JournalMedal",61.5f,28,92,92);Hide(medal,"JournalPaper");
            var image=medal.GetComponent<Image>();if(image!=null)UnityEngine.Object.DestroyImmediate(image);
            if(medal.GetComponent<CanvasRenderer>()==null) medal.gameObject.AddComponent<CanvasRenderer>();
            var graphic=medal.GetComponent<CozyJournalMedalGraphic>()??medal.gameObject.AddComponent<CozyJournalMedalGraphic>();graphic.raycastTarget=false;
            Place(root,"Reflow_AchievementSeal",78.5f,45,58,58);
            DpText(root,"Label",10,148,195,28,20,false,TextAnchor.MiddleCenter);
            DpText(root,"JournalDescription",10,182,195,48,15,true,TextAnchor.UpperCenter);
            RefineStamp(root,"DesignStateBadge",153,-16,74,"已解锁",18);
            Hide(root,"DesignState","JournalTape");
        }
    }
}
#endif
