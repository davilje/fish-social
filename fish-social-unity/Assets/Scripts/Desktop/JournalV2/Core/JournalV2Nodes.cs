using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    public static class JournalV2Nodes
    {
        public static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.up;
            r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        public static void Stretch(RectTransform rect)
        { rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero; }
        public static JournalV2Plate Plate(Transform parent,string name,float x,float y,float w,float h,Color fill,float radius=16,float border=2)
        {
            var r=Rect(parent,name,x,y,w,h);r.gameObject.AddComponent<CanvasRenderer>();
            var plate=r.gameObject.AddComponent<JournalV2Plate>();plate.color=fill;plate.Radius=radius;plate.BorderWidth=border;plate.raycastTarget=false;return plate;
        }
        public static Image Image(Transform parent,string name,Sprite sprite,float x,float y,float w,float h)
        {
            var r=Rect(parent,name,x,y,w,h);var image=r.gameObject.AddComponent<Image>();
            image.sprite=sprite;image.color=sprite!=null?Color.white:Color.clear;image.preserveAspect=true;image.raycastTarget=false;return image;
        }
        public static Text Text(Transform parent,string name,string value,float x,float y,float w,float h,int size,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var r=Rect(parent,name,x,y,w,h);var t=r.gameObject.AddComponent<Text>();
            t.font=JournalV2Theme.Font;DesktopLocalization.SetText(t, value??"");t.fontSize=size;t.fontStyle=FontStyle.Normal;t.color=JournalV2Theme.Ink;
            t.alignment=align;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.raycastTarget=false;return t;
        }
        public static Button Button(Transform parent,string name,string label,string iconKey,float x,float y,float w,float h,JournalV2ButtonKind kind=JournalV2ButtonKind.Plain)
        {
            var r=Rect(parent,name,x,y,w,h);var hit=r.gameObject.AddComponent<Image>();hit.color=Color.clear;
            var button=r.gameObject.AddComponent<Button>();button.targetGraphic=hit;button.transition=Selectable.Transition.None;
            var visual=Rect(r,"Visual",0,0,w,h);visual.pivot=new Vector2(.5f,.5f);visual.anchoredPosition=new Vector2(w/2,-h/2);
            var primary=kind==JournalV2ButtonKind.Primary;
            if(kind!=JournalV2ButtonKind.Nav)
            {
                var plate=Plate(visual,"Plate",0,0,w,h,primary?JournalV2Theme.Coral:(Color)new Color32(255,253,247,255),kind==JournalV2ButtonKind.Index?18:14);
                plate.BorderColor=primary?JournalV2Theme.Coral:JournalV2Theme.Line;
                plate.ShadowColor=primary?new Color32(212,111,34,255):new Color32(230,210,176,255);
            }
            if(kind==JournalV2ButtonKind.Index)
            {
                Plate(visual,"Notch",0,18,8,h-36,new Color(117/255f,71/255f,46/255f,.08f),6,0).ShadowColor=Color.clear;
            }
            var nav=kind==JournalV2ButtonKind.Nav;
            var iconSize=nav?44:kind==JournalV2ButtonKind.Index?30:28;
            var icon=Image(visual,"Icon",JournalV2Theme.Sprite((primary?"icons-light/":"icons/")+iconKey),nav?(w-iconSize)/2:kind==JournalV2ButtonKind.Index?30:18,nav?18:(h-iconSize)/2,iconSize,iconSize);
            icon.gameObject.SetActive(!string.IsNullOrEmpty(iconKey));
            if(kind==JournalV2ButtonKind.Index)
            {
                var light=Image(visual,"LightIcon",JournalV2Theme.Sprite(DesktopLocalization.Concat("icons-light/", iconKey)),30,(h-iconSize)/2,iconSize,iconSize);
                light.color=new Color(1,1,1,0);
            }
            var left=kind==JournalV2ButtonKind.Index?74:!string.IsNullOrEmpty(iconKey)?54:8;
            var text=Text(visual,"Label",label,nav?0:left,nav?66:0,nav?w:w-left-8,nav?26:h,nav?19:kind==JournalV2ButtonKind.Index?22:23,kind==JournalV2ButtonKind.Index?TextAnchor.MiddleLeft:TextAnchor.MiddleCenter);
            if(primary)text.color=Color.white;
            r.gameObject.AddComponent<JournalV2Button>().Configure(visual,kind,name);
            return button;
        }
        public static ScrollRect GridScroll(Transform parent,string name,float x,float y,float w,float h,int columns,Vector2 cell,Vector2 gap)
        {
            var scroll=Scroll(parent,name,x,y,w,h);var grid=scroll.content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=columns;grid.cellSize=cell;grid.spacing=gap;grid.padding=new RectOffset(14,14,14,22);
            var fitter=scroll.content.gameObject.AddComponent<ContentSizeFitter>();fitter.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;return scroll;
        }
        public static ScrollRect VerticalScroll(Transform parent,string name,float x,float y,float w,float h,float gap=16)
        {
            var scroll=Scroll(parent,name,x,y,w,h);var layout=scroll.content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth=true;layout.childControlHeight=false;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;layout.spacing=gap;layout.padding=new RectOffset(14,14,14,22);
            var fitter=scroll.content.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;return scroll;
        }
        static ScrollRect Scroll(Transform parent,string name,float x,float y,float w,float h)
        {
            var r=Rect(parent,name,x,y,w,h);var scroll=r.gameObject.AddComponent<ScrollRect>();
            var viewport=Rect(r,"Viewport",0,0,w,h);Stretch(viewport);viewport.gameObject.AddComponent<RectMask2D>();
            var hit=viewport.gameObject.AddComponent<Image>();hit.color=Color.clear;
            var content=Rect(viewport,"Content",0,0,w,h);content.anchorMax=new Vector2(1,1);content.sizeDelta=new Vector2(0,h);
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;return scroll;
        }
        public static InputField Input(Transform parent,string name,string value,float x,float y,float w,float h,int fontSize=18,bool multiline=false)
        {
            var r=Rect(parent,name,x,y,w,h);var bg=Plate(r,"Plate",0,0,w,h,new Color32(255,249,237,255),12,2);bg.ShadowColor=Color.clear;bg.raycastTarget=true;
            var field=r.gameObject.AddComponent<InputField>();field.targetGraphic=bg;field.transition=Selectable.Transition.None;
            field.textComponent=Text(r,"Text",value,12,4,w-24,h-8,fontSize,multiline?TextAnchor.UpperLeft:TextAnchor.MiddleLeft);
            var placeholder=Text(r,"Placeholder","",12,4,w-24,h-8,fontSize,multiline?TextAnchor.UpperLeft:TextAnchor.MiddleLeft);placeholder.color=JournalV2Theme.Soft;field.placeholder=placeholder;
            field.lineType=multiline?InputField.LineType.MultiLineNewline:InputField.LineType.SingleLine;DesktopLocalization.SetText(field, value??"");return field;
        }
        public static RectTransform Polaroid(Transform parent,string name,Sprite sprite,string caption,float x,float y,float w=302,float h=236,float cssAngle=-2)
        {
            var root=Rect(parent,name,x,y,w,h);var visual=Rect(root,"Visual",0,0,w,h);
            visual.pivot=new Vector2(.5f,.5f);visual.anchoredPosition=new Vector2(w/2,-h/2);
            var frame=Plate(visual,"Frame",0,0,w,h,Color.white,2,0);frame.ShadowOffset=new Vector2(0,-6);frame.ShadowBlur=8;
            var well=Plate(visual,"Well",12,12,w-24,h-68,JournalV2Theme.ArtWell,12,0);well.ShadowColor=Color.clear;
            Image(visual,"Art",sprite,24,22,w-48,h-88);
            Text(visual,"Caption",caption,12,h-46,w-24,34,24,TextAnchor.MiddleCenter);
            var tape=Image(visual,"Tape",JournalV2Theme.Sprite("decor/tape-gold-dots"),(w-104)/2,-14,104,26);
            tape.rectTransform.pivot=new Vector2(.5f,.5f);tape.rectTransform.anchoredPosition=new Vector2(w/2,1);tape.rectTransform.localEulerAngles=new Vector3(0,0,4);
            root.gameObject.AddComponent<JournalV2Polaroid>().Configure(visual,cssAngle);
            return root;
        }
        public static void Clear(Transform parent)
        {
            if(parent==null)return;
            for(var i=parent.childCount-1;i>=0;i--)
            {
                var child=parent.GetChild(i).gameObject;child.SetActive(false);
                if(Application.isPlaying) Object.Destroy(child);else Object.DestroyImmediate(child);
            }
        }
    }
}
