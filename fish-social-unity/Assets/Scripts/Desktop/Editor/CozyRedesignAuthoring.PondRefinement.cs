#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        public static void ApplyPondRefinement(GameObject root)
        {
            if(root==null)return;
            var t=root.transform;
            if(root.name=="PondSelectionCard")
            {
                Place(t,"",0,0,329.5f,248);
                var size=t.GetComponent<LayoutElement>();if(size!=null){size.minHeight=248;size.preferredHeight=248;}
                var title=Label(t,"Name","",10,152,309.5f,31,22);SinglePondLine(title,18);
                var dot=Label(t,"CategoryDot","●",10,187,16,24,14);dot.alignment=TextAnchor.MiddleCenter;
                var category=Label(t,"Description","",30,187,172,24,14);SinglePondLine(category,12);
                var fee=Label(t,"Fee","",208,187,111.5f,24,14);fee.alignment=TextAnchor.MiddleRight;SinglePondLine(fee,11);
                var species=Label(t,"Species","",10,216,309.5f,22,13);species.color=new Color32(161,130,100,255);SinglePondLine(species,11);
                var access=t.Find("Access")?.GetComponent<Text>();
                if(access!=null)
                {
                    access.fontSize=12;access.lineSpacing=1;SinglePondLine(access,10);
                    var badge=t.Find("AccessBadge");
                    if(badge!=null)access.transform.SetSiblingIndex(badge.GetSiblingIndex()+1);
                }
                foreach(var path in new[]{"Name","CategoryDot","Description","Fee","Species","Access","AccessBadge"})t.Find(path)?.gameObject.SetActive(true);
                if(root.GetComponent<CozyPondCardContent>()==null)root.AddComponent<CozyPondCardContent>();
            }
            else if(root.name=="PanelPondSelection")
            {
                var grid=t.Find("PondCardScroll/Viewport/Cards")?.GetComponent<GridLayoutGroup>();if(grid!=null)grid.cellSize=new Vector2(329.5f,248);
                var note=Label(t,"DesignPondRail/Note","选好一片水域，\n再决定回鱼方式。",18,344,174,48,16);
                note.lineSpacing=1.2f;note.alignment=TextAnchor.UpperLeft;
                RefinePondEntry(t,"DesignPondAside/Enter");
            }
            else if(root.name=="PanelWorldMap")
            {
                Hide(t,"DesignMapRail/Heading","DesignMapRail/Body","DesignMapRail/Tip");
                var note=Label(t,"DesignMapRail/Note","拖动地图浏览\n点浮标查看鱼塘",18,318,174,48,16);note.lineSpacing=1.2f;note.alignment=TextAnchor.UpperLeft;
                Place(t,"ListPonds",42,492,174,43);
                Place(t,"Details/ResetView",-942,438,174,40);
                RefinePondEntry(t,"Details/EnterPond");
                RefinePondInline(t,"ListPonds",18,false);
                RefinePondInline(t,"Details/ResetView",18,false);
            }
        }
        static void SinglePondLine(Text text,int minimum)
        {
            text.enabled=true;text.resizeTextForBestFit=true;text.resizeTextMinSize=minimum;text.resizeTextMaxSize=text.fontSize;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;text.lineSpacing=1;text.raycastTarget=false;
        }
        static void RefinePondEntry(Transform root,string path)
        {RefinePondInline(root,path,22,true);}
        static void RefinePondInline(Transform root,string path,int fontSize,bool miniFish)
        {
            var rect=root.Find(path) as RectTransform;var label=rect?.Find("Label")?.GetComponent<Text>();if(rect==null||label==null)return;
            label.fontSize=fontSize;label.lineSpacing=1;label.fontStyle=FontStyle.Normal;
            foreach(var group in rect.GetComponents<LayoutGroup>())Object.DestroyImmediate(group);
            foreach(var fitter in rect.GetComponents<ContentSizeFitter>())Object.DestroyImmediate(fitter);
            var icon=rect.Find("Icon")?.GetComponent<Image>()??rect.Find("CozyIcon")?.GetComponent<Image>();
            var sprite=miniFish?CozyJournalTheme.Sprite("motion/inline-mini-fish"):null;
            if(miniFish&&icon==null)icon=Picture(rect,"Icon",sprite,0,0,30,22);
            if(miniFish){icon.gameObject.SetActive(true);icon.enabled=true;icon.sprite=sprite;icon.color=Color.white;}
            var inline=rect.GetComponent<CozyJournalInlineContent>()??rect.gameObject.AddComponent<CozyJournalInlineContent>();
            inline.ConfigureCentered(rect,label,icon,miniFish?new Vector2(30,22):new Vector2(24,24),10,12,forcedIcon:sprite);
        }
    }
}
#endif
