#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        /// <summary>The accepted journal-workspace layout, in notebook design coordinates.</summary>
        public static void ApplyWorkspaceProfile(GameObject root)
        {
            if (root.name == "PanelProfileHub") WorkspaceProfile(root.transform);
            if (root.name == "PanelProfileEdit") WorkspaceEdit(root.transform);
        }

        static void WorkspaceProfile(Transform root)
        {
            var tab = root.Find("Shell/Content/Tab_资料");
            if (tab == null) return;
            root.Find("Shell/Sidebar/Tab_成就")?.gameObject.SetActive(false);
            Hide(tab, "JournalShowcaseLink", "JournalAlbumLink", "DesignStats", "DesignPrivacy", "DesignId");
            DpText(tab, "JournalHeading", 0, 2, 500, 40, 24, value:"我的钓鱼时光");
            WorkspaceAction(root, "Shell/Header/Edit", 854, 112.5f, 102, 43, 16, "编辑资料");
            var identity = DpSurface(tab, "JournalIdentity", "surfaces/input-default", 9, 54, 692, 140);
            identity.SetAsFirstSibling(); Hide(identity,"Landscape","LandscapeBase");
            Picture(tab,"Avatar",null,27,79,90,90);
            Place(tab,"Avatar",27,79,90,90).localEulerAngles = new Vector3(0,0,5);
            DpText(tab,"WorkspaceKicker",139,69,530,20,13,true,value:"AN ANGLER’S JOURNAL · 湖畔手账");
            DpText(tab,"DesignName",139,94,410,33,27);
            DpText(tab,"WorkspaceLevel",566,95,110,28,15,false,TextAnchor.MiddleRight);
            DpText(tab,"DesignBio",139,132,530,24,15);
            DpText(tab,"WorkspaceSince",139,163,530,22,14,true);
            Place(tab,"DesignStatCards",9,204,692,78);
            for(var i=0;i<3;i++)
            {
                var card=Place(tab,"DesignStatCards/Card"+i,i*234,0,224,78);
                DpSurface(card,"Backing","surfaces/input-default",0,0,224,78).SetAsFirstSibling(); Hide(card,"Icon");
                DpText(card,"Label",16,10,192,21,14,true);
                DpText(card,"Value",16,33,192,34,26);
            }
            var progress=DpSurface(tab,"JournalProgress","surfaces/input-default",9,292,692,182);progress.SetAsFirstSibling();
            DpText(progress,"Heading",18,14,380,24,17,value:"下一段钓鱼旅程");
            DpText(progress,"Level",450,14,224,24,17,false,TextAnchor.MiddleRight);
            Place(progress,"Track",18,48,656,12); Place(progress,"Track/Fill",0,0,656,12);
            DpText(progress,"Caption",18,65,656,23,15,true);
            Picture(progress,"RecentArt",null,18,110,76,42);
            DpText(progress,"RecentTitle",108,106,548,25,16);
            DpText(progress,"RecentDetail",108,136,548,23,14,true);
            var aside=tab.Find("DesignShowcase");
            Hide(aside,"Open","Size");
            DpText(aside,"Heading",24,16,314,30,22,value:"我的珍藏");
            // Reconfigure the shared photo pose with the shorter Web photo dimensions.
            Place(aside,"JournalPhoto",30,60,302,208); DpCenter((RectTransform)aside.Find("JournalPhoto"));
            Place(aside,"JournalPhotoInset",42,72,278,142);
            Place(aside,"Art",59,78,244,130);
            DpText(aside,"Species",42,221,278,30,22,false,TextAnchor.MiddleCenter);
            Place(aside,"JournalPhotoTape",134,46,104,26);
            aside.Find("JournalPhoto")?.GetComponent<CozyJournalStaticPose>()?.Configure(-2,
                aside.Find("JournalPhoto/JournalPaper").GetComponent<Image>(),aside.Find("JournalPhotoInset").GetComponent<Image>(),
                aside.Find("Art").GetComponent<Image>(),aside.Find("Species").GetComponent<Text>(),aside.Find("JournalPhotoTape").GetComponent<Image>());
            DpText(aside,"WorkspaceCaption",24,280,314,22,13,true);
            var labels=new[]{"体长","估算体重","记录时间","相遇水域"};
            for(var i=0;i<labels.Length;i++)
            {
                var fact=Place(aside,"WorkspaceFact"+i,24,314+i*28,314,22);
                DpText(fact,"Label",0,0,100,22,15,true,value:labels[i]);
                DpText(fact,"Value",100,0,214,22,15,false,TextAnchor.MiddleRight);
            }
            DpText(aside,"WorkspaceNote",24,436,314,44,14,true,TextAnchor.UpperLeft,"那些耐心等待的时刻，\n都在手账里留下了印记。");
        }

        static void WorkspaceEdit(Transform root)
        {
            var rail=DpSurface(root,"JournalEditRail","surfaces/paper-rail",24,112,210,496);
            rail.SetAsFirstSibling();Hide(rail,"Heading","Note");
            var labels=new[]{"资料","展示柜","图鉴","相册","成就"};
            var icons=new[]{"user","fish","map","heart","medal"};
            for(var i=0;i<labels.Length;i++)
            {
                var path="Tab_"+labels[i];
                DpButton(rail,path,10,12+i*88,190,76,22,labels[i]);
                var button=rail.Find(path).GetComponent<Button>();
                var icon=Picture(button.transform,"Icon",CatTheme.LoadCozy("icons/"+icons[i]+".png",Vector4.zero),30,23,30,30);icon.enabled=true;icon.gameObject.SetActive(true);
                Hide(button.transform,"CozyIcon");
                CatTheme.StyleButton(button,icons[i]);
                var visual=button.GetComponent<CozyButtonVisual>();
                visual.ConfigureSurface("surfaces/tab-index-default","surfaces/tab-index-hover",null,"surfaces/tab-index-selected");
                visual.SetSelected(i==0);
                DpText(button.transform,"Label",74,0,116,76,22,false,TextAnchor.MiddleLeft,labels[i]);
            }
            DpText(root,"JournalEditHeading",254,114,560,40,24,value:"编辑我的手账");
            DpText(root,"WorkspaceSubtitle",814,114,532,40,15,true,TextAnchor.MiddleRight,"名字、模样，还有最喜欢的鱼获");
            Hide(root,"AvatarsTitle","ShowcaseTitle","DesignFormPaper","DesignAvatarPaper","DesignShowcasePaper");
            root.Find("DesignHeader").gameObject.SetActive(true);
            CozyItemArt.Text(root,"DesignHeader/Title","个人中心");
            CozyItemArt.Text(root,"DesignHeader/Subtitle","记录属于你的钓鱼时光");
            Picture(root,"DesignHeader/Icon",CatTheme.LoadCozy("icons/user.png",Vector4.zero),28,25,46,46);
            var left=DpSurface(root,"JournalPreview","surfaces/input-default",254,160,342,440);left.SetAsFirstSibling(); WorkspaceWhiteCard(left);
            Hide(left,"Heading","JournalPhoto","JournalPhotoInset","JournalPhotoTape","Bio");
            Picture(left,"Avatar",null,20,20,70,70);
            DpText(left,"Kicker",105,20,217,20,13,true,value:"你的钓友名片");
            DpText(left,"Name",105,45,217,29,22);
            DpText(left,"Hint",105,76,217,20,14,true,value:"选择喜欢的模样，记下今天的自己");
            var right=DpSurface(root,"WorkspaceEditRight","surfaces/input-default",616,160,730,440);right.SetAsFirstSibling();WorkspaceWhiteCard(right);
            Place(root,"Form",274,274,302,238);
            DpText(root,"Form/NicknameLabel",0,0,200,24,16,value:"游戏昵称");
            DpText(root,"Form/NicknameLimit",200,0,102,24,13,true,TextAnchor.MiddleRight,"最多 12 字");
            Place(root,"Form/Nickname",0,32,302,46);
            DpText(root,"Form/BioLabel",0,88,180,24,16,value:"个人简介");
            DpText(root,"Form/BioCount",180,88,122,24,13,true,TextAnchor.MiddleRight,"0 / 120");
            Place(root,"Form/Bio",0,120,302,105);
            foreach(var field in root.Find("Form").GetComponentsInChildren<InputField>(true))
            {
                CozyJournalTheme.ApplyInput(field);
                foreach(var text in field.GetComponentsInChildren<Text>(true)){text.fontSize=16;text.fontStyle=FontStyle.Normal;Inset(text.rectTransform,12,10,12,10);}
            }
            Place(root,"Actions",274,534,302,46);
            WorkspaceAction(root,"Actions/Save",146,0,156,46,17,"保存资料",true);
            WorkspaceAction(root,"Header/Back",274,534,134,46,17,"取消修改");
            WorkspaceAction(root,"Actions/Retry",-230,6,170,32,13,"重试");root.Find("Actions/Retry").gameObject.SetActive(false);
            DpText(root,"Status",274,512,302,20,12,true);
            WorkspaceAction(root,"WorkspaceAvatarTab",636,178,130,44,18,"选择头像");
            WorkspaceAction(root,"WorkspaceShowcaseTab",778,178,130,44,18,"展示鱼获");
            DpText(root,"WorkspaceTabHint",918,178,408,44,13,true,TextAnchor.MiddleRight);
            WorkspaceScroll(root,"Avatars",636,238,690,344,3,222,104);
            WorkspaceScroll(root,"Showcase",636,238,690,344,4,163.5f,164);
            WorkspaceChoice(root,"WorkspaceAvatarTemplate",222,104,true,false);
            WorkspaceChoice(root,"WorkspaceSlotTemplate",163.5f,164,false,false);
            WorkspaceChoice(root,"WorkspacePickerTemplate",244,188,false,true);
            var picker=DpSurface(root,"Picker","surfaces/modal-paper",116,-39.5f,1120,720);picker.SetAsLastSibling();
            var canvas=picker.GetComponent<Canvas>();if(canvas==null)canvas=picker.gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=100;
            if(picker.GetComponent<GraphicRaycaster>()==null)picker.gameObject.AddComponent<GraphicRaycaster>();
            var dim=Place(picker,"WorkspaceDim",-276,-110.5f,1672,941);dim.SetAsFirstSibling();
            var dimImage=dim.GetComponent<Image>()??dim.gameObject.AddComponent<Image>();dimImage.color=new Color(.33f,.21f,.12f,.4f);dimImage.raycastTarget=true;

            WorkspaceAction(picker,"Dismiss",1036,24,48,42,22,"×");
            var closeRect=(RectTransform)picker.Find("Dismiss");closeRect.anchorMin=closeRect.anchorMax=closeRect.pivot=Vector2.one;closeRect.anchoredPosition=new Vector2(-36,-24);
            DpText(picker,"Title",32,24,950,40,28,value:"整理展示位");
            DpText(picker,"Subtitle",32,84,1056,25,16,true,value:"挑一条喜欢的鱼获。清空展示位不会移除背包里的鱼。");
            if(picker.Find("Search")==null)DesktopModalUi.MakeInput(picker,"Search","搜索鱼种…",40);
            Place(picker,"Search",32,130,1056,44);CozyJournalTheme.ApplyInput(picker.Find("Search").GetComponent<InputField>());
            Place(picker,"Viewport",32,190,1056,400);
            var grid=picker.Find("Viewport/Content").GetComponent<GridLayoutGroup>();grid.enabled=true;grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=4;grid.cellSize=new Vector2(244,188);grid.spacing=new Vector2(14,14);grid.padding=new RectOffset(6,6,6,6);
            var fit=grid.GetComponent<ContentSizeFitter>();if(fit!=null){fit.enabled=true;fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;}
            WorkspaceAction(picker,"Clear",32,636,174,46,17,"清空此展示位");
            DpText(picker,"Selection",224,636,420,46,15,true);
            WorkspaceAction(picker,"Close",750,636,132,46,17,"取消");
            WorkspaceAction(picker,"Confirm",894,636,194,46,17,"放进展示位",true);
            picker.gameObject.SetActive(false);
            root.Find("Showcase").gameObject.SetActive(false);
            foreach(var layer in new[]{"JournalStitch","JournalGrain","JournalDots","JournalEdge","JournalShadow"})root.Find(layer)?.SetAsFirstSibling();
        }

        static void WorkspaceWhiteCard(Transform host)
        {
            Hide(host,"JournalPaper");
            var wash=CozyCssGraphic.EnsureWash(host,"WorkspaceFill",new Color32(255,253,247,255),18,1.5f,new Color32(234,216,186,255));
            wash.transform.SetAsFirstSibling();
        }

        static void WorkspaceAction(Transform root,string path,float x,float y,float w,float h,int size,string caption,bool primary=false)
        {
            DpButton(root,path,x,y,w,h,size,caption,primary);
            var node=root.Find(path);Hide(node,"Icon","CozyIcon");
            var visual=node.GetComponent<CozyButtonVisual>();
            if(visual!=null)
            {
                var serialized=new SerializedObject(visual);serialized.FindProperty("primarySurface").boolValue=primary;serialized.ApplyModifiedPropertiesWithoutUndo();
                var family=primary?"surfaces/button-primary-":"surfaces/button-quiet-";
                visual.ConfigureSurface(family+"default",family+"hover",family+"pressed",family+"hover",family+"disabled");
            }
            if(path=="WorkspaceAvatarTab"||path=="WorkspaceShowcaseTab")
            {
                var wash=CozyCssGraphic.EnsureWash(node,"WorkspaceSelected",new Color32(251,232,207,255),12,1,new Color32(233,162,93,255));wash.transform.SetSiblingIndex(1);
                var state=node.GetComponent<CozyJournalCardVisual>()??node.gameObject.AddComponent<CozyJournalCardVisual>();state.Configure(visual,wash.gameObject,null);
            }
            var label=DpText(node,"Label",0,0,w,h,size,false,TextAnchor.MiddleCenter,caption);if(primary)label.color=Color.white;
            var inline=node.GetComponent<CozyJournalInlineContent>()??node.gameObject.AddComponent<CozyJournalInlineContent>();
            inline.ConfigureCentered((RectTransform)node,label,null,Vector2.zero,0,8);
        }

        static void WorkspaceScroll(Transform root,string path,float x,float y,float w,float h,int columns,float cw,float ch)
        {
            DpScroll(root,path,x,y,w,h,columns,cw,ch,12,12);
            var scroll=root.Find(path).GetComponent<ScrollRect>();
            Place(root,path+"/Viewport",0,0,w,h);
            var grid=scroll.content.GetComponent<GridLayoutGroup>();grid.padding=new RectOffset();
        }

        static void WorkspaceChoice(Transform root,string name,float w,float h,bool avatar,bool picker)
        {
            var rect=Place(root,name,0,0,w,h);
            var button=rect.GetComponent<Button>();if(button==null)button=rect.gameObject.AddComponent<Button>();
            var hit=rect.GetComponent<Image>()??rect.gameObject.AddComponent<Image>();
            hit.color=new Color(1f,1f,1f,0f);hit.raycastTarget=true;
            if(button.targetGraphic==null)button.targetGraphic=hit;
            CatTheme.StyleButton(button);
            JournalCard(rect.gameObject); Hide(rect,"JournalTape");
            Hide(rect,"JournalSelection");
            var visual=rect.GetComponent<CozyButtonVisual>();visual.ConfigureSurface("surfaces/input-default","surfaces/input-default","surfaces/input-default","surfaces/input-default","surfaces/input-default");
            var baseWash=CozyCssGraphic.EnsureWash(rect,"WorkspaceBase",avatar?new Color32(251,245,233,255):new Color32(255,250,240,255),16,0,Color.clear);baseWash.transform.SetSiblingIndex(1);
            var selectedWash=CozyCssGraphic.EnsureWash(rect,"WorkspaceSelected",new Color32(255,240,216,255),16,2,new Color32(252,146,60,255));selectedWash.transform.SetSiblingIndex(2);
            var check=DpText(selectedWash.transform,"Check",w-26,4,20,22,17,false,TextAnchor.MiddleCenter,"✓");check.color=new Color32(214,126,49,255);check.gameObject.SetActive(avatar);
            rect.GetComponent<CozyJournalCardVisual>()?.Configure(visual,selectedWash.gameObject,null);
            Picture(rect,"DesignItemArt",null,avatar?12:12,avatar?14:18,avatar?76:w-24,avatar?76:picker?96:78);
            DpText(rect,"Label",avatar?96:10,avatar?36:picker?116:103,avatar?116:w-20,avatar?32:27,avatar?17:picker?19:16,false,avatar?TextAnchor.MiddleLeft:TextAnchor.MiddleCenter);
            DpText(rect,"WorkspaceMeta",10,picker?145:132,w-20,22,12,true,TextAnchor.MiddleCenter);
            DpText(rect,"WorkspaceRecord",10,168,w-20,18,11,true,TextAnchor.MiddleCenter).gameObject.SetActive(picker);
            DpText(rect,"WorkspaceSlot",9,6,50,18,12,true);
            DpText(rect,"WorkspaceEmpty",10,18,w-20,78,40,true,TextAnchor.MiddleCenter,"＋").gameObject.SetActive(false);
            rect.gameObject.SetActive(false);
        }
    }
}
#endif
