#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        static readonly Color WebSoft = new Color(117/255f,71/255f,46/255f,.68f);
        static void ApplyVisualParity(GameObject root)
        {
            var t = root.transform;
            if (t.Find("DesignHeader") != null) WebPage(t);
            if (root.name == "DesktopShell") WebShell(t);
            if (root.name == "PanelShop" || root.name == "PanelEquipment") WebShop(t, root.name == "PanelEquipment");
            if (root.name == "PanelCatch") WebBag(t);
            if (root.name == "ShopItemCard" || root.name == "CatchSlot") WebCard(root);
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (!IsIndex(button.transform)) continue;
                if (button.transform.Find("Icon") != null) Place(button.transform,"Icon",30,23,30,30);
                WebText(button.transform,"Label",74,0,116,76,22);
            }
        }

        static void WebText(Transform t, string path, float x, float y, float w, float h, int size, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var node = t.Find(path);
            if (node == null) return;
            Place(t,path,x,y,w,h);
            var label = node.GetComponent<Text>();
            if (label == null) return;
            label.fontSize = size;
            label.fontStyle = FontStyle.Normal;
            label.resizeTextForBestFit = false;
            label.alignment = alignment;
            label.lineSpacing = 1;
        }

        static void WebPage(Transform t)
        {
            WebText(t,"DesignHeader/Title",92,22,310,50,34);
            WebText(t,"DesignHeader/Subtitle",380,36,850,26,18);
            Place(t,"DesignHeader/Icon",34,26,44,44);
            var subtitle = t.Find("DesignHeader/Subtitle").GetComponent<Text>();
            switch(t.name)
            {
                case "PanelCatch": subtitle.text="今天的收获都在这里"; break;
                case "PanelEquipment": subtitle.text="挑一根顺手的竿"; break;
                case "PanelAchievements": subtitle.text="每一枚奖章都是一个故事"; break;
                case "PanelLeaderboard": subtitle.text="看看谁是本周的钓鱼王"; break;
                case "PanelProfileHub": subtitle.text="记录属于你的钓鱼时光"; break;
                case "PanelSocialFeed": subtitle.text="钓友们最近在忙什么"; break;
                case "PanelSettings": subtitle.text="按你的习惯调整，改动会自动保存"; break;
            }
            subtitle.color = WebSoft;
            DesktopModalUi.PlaceTopRight(t.Find("DesignHeader/Close").GetComponent<Button>(),24,24,52);
            var rule = JournalSurface(t,"DesignHeader/Rule","surfaces/paper-page",30,94,1310,2);
            rule.sprite = null; rule.color = new Color(117/255f,71/255f,46/255f,.1f);
            var title = t.Find("DesignHeader/Title").GetComponent<Text>();
            var width = Mathf.Max(68, title.preferredWidth);
            var accent = t.name == "PanelCatch" || t.name == "PanelProfileHub" ? new Color(.988f,.573f,.235f) : t.name == "PanelLeaderboard" ? new Color(.969f,.714f,.784f) : t.name == "PanelSettings" ? new Color(.78f,.698f,.6f) : t.name == "PanelEquipment" || t.name == "PanelWorldMap" ? new Color(.36f,.68f,.89f)
                : t.name == "PanelSocialFeed" ? new Color(.42f,.72f,.42f) : new Color(.95f,.75f,.28f);
            var underline = JournalSurface(t,"DesignHeader/JournalTitleUnderline","surfaces/paper-page",92,53,width+16,15);
            underline.sprite = null; underline.color = new Color(accent.r,accent.g,accent.b,.45f);
            underline.transform.SetAsFirstSibling();
            var paw = JournalSurface(t,"DesignHeader/JournalPaw","decor/paw-mark",112+width,24,30,30);
            paw.rectTransform.localEulerAngles = new Vector3(0,0,-14);
            paw.color = new Color(accent.r,accent.g,accent.b,.55f);
            JournalSurface(t,"JournalStitch","decor/page-stitch",10,10,1350,630).transform.SetSiblingIndex(4);
            for (var i=0;i<3;i++)
            {
                // Sprite includes a 10px transparent shadow margin on each edge.
                JournalSurface(t,"JournalRing"+i,"decor/binder-ring",-34,140+160*i,74,44).transform.SetAsLastSibling();
            }
            var status = t.Find("Status")?.GetComponent<Text>();
            if (status != null) { WebText(t,"Status",254,610,1068,26,17); status.color=WebSoft; }
        }

        static void WebShell(Transform t)
        {
            var h=t.Find("MainRoot/Header");
            Place(h,"WorldMapEntry",562,29,320,76);
            Place(h,"WorldMapEntry/Icon",22,18,40,40);
            WebText(h,"WorldMapEntry/Label",78,9,190,34,25);
            WebText(h,"WorldMapEntry/Hint",78,46,210,18,13);
            var arrow=Label(h,"WorldMapEntry/Arrow","↗",270,18,28,40,28);
            arrow.gameObject.SetActive(true);
            arrow.font=Resources.GetBuiltinResource<Font>("Arial.ttf"); arrow.color=WebSoft;
            Place(h,"ProfileCard/Avatar",14,10.5f,82,82);
            WebText(h,"ProfileCard/Name",110,18,66,34,26);
            Place(h,"ProfileCard/LevelBadge",176,24,70,30);
            WebText(h,"ProfileCard/Level",176,24,70,30,20,TextAnchor.MiddleCenter);
            Place(h,"ProfileCard/XpBackground",110,64,285,18);
            WebText(h,"ProfileCard/XpLabel",110,64,285,18,13,TextAnchor.MiddleCenter);
            foreach(var n in new[]{"FishCoins","CatchCount"})
            {
                Place(h,n+"/Icon",16,12,40,40);
                WebText(h,n+"/Amount",66,10,112,44,28);
            }
            Place(h,"FishCoins/Plus",185,13,38,38);
            WebText(h,"FishCoins/Plus/Label",0,0,38,38,26,TextAnchor.MiddleCenter);
            WebText(h,"CatchCount/Hint",166,20,36,24,16);
            var coin=h.Find("FishCoins/Icon")?.GetComponent<Image>();
            if(coin!=null) coin.sprite=CatTheme.LoadCozy("icons/coin.png",Vector4.zero);
            foreach(var n in new[]{"Mail","Settings"}) Place(h,n+"/Icon",16,12,40,40);
            var nav=t.Find("MainRoot/Nav");
            Place(t,"MainRoot/Nav",320,807,1032,108);
            var bobber=nav.Find("CozyBobber") as RectTransform;
            if(bobber!=null) {bobber.sizeDelta=new Vector2(40,58);bobber.pivot=new Vector2(.5f,.2f);bobber.anchorMin=bobber.anchorMax=((RectTransform)nav).pivot;bobber.GetComponent<Image>().sprite=CozyMotion.BobberSprite();}
            JournalLayer(nav,"JournalDockEdge","surfaces/dock-edge").transform.SetAsFirstSibling();
            JournalSurface(nav,"JournalDockStitch","decor/dock-stitch",18,96,996,2);
            var tabs=new[]{"CatchBag","Equipment","Shop","Achievements","Leaderboard","Profile","SocialFeed"};
            for(var i=0;i<tabs.Length;i++)
            {
                if(nav.Find(tabs[i])==null) continue;
                Place(nav,tabs[i],2+i*146.857f,2,146.857f,104);
                Place(nav,tabs[i]+"/Icon",51.4285f,18,44,44);
                WebText(nav,tabs[i]+"/Label",0,66,146.857f,26,19,TextAnchor.MiddleCenter);
                if(i>0)
                {
                    var divider=Place(nav,"Divider"+i,i*146.857f+1,24,2,60).GetComponent<Image>();
                    if(divider!=null) divider.color=new Color(117/255f,71/255f,46/255f,.08f);
                }
            }
            var cat=t.Find("MainRoot/CozyCornerCat") as RectTransform;
            if(cat!=null)
            {
                var sprite=CozyJournalTheme.Sprite("illustrations/cat-fishing") ?? cat.GetComponent<Image>().sprite;
                cat.GetComponent<Image>().sprite=sprite;
                cat.anchorMin=cat.anchorMax=cat.pivot=new Vector2(1,0);
                cat.anchoredPosition=new Vector2(12,-4);
                cat.sizeDelta=new Vector2(250,250*sprite.rect.height/sprite.rect.width);
            }
        }

        static void WebGrid(Transform t,string scroll,string content,int columns)
        {
            Place(t,scroll,254,160,710,440);
            var viewport=t.Find(scroll+"/Viewport") as RectTransform;
            if(viewport!=null) Stretch(viewport,0);
            Grid(t,content,columns,(682-(columns-1)*18f)/columns,192);
            var grid=t.Find(content).GetComponent<GridLayoutGroup>();
            grid.spacing=new Vector2(18,18);
            grid.padding=new RectOffset(14,14,14,22);
        }

        static void WebShop(Transform t,bool equipment)
        {
            WebGrid(t,"Items","Items/Viewport/Content",3);
            if(equipment)
            {
                Place(t,"Tabs/Tackle",0,0,190,76); Place(t,"Tabs/Bait",0,88,190,76); Hide(t,"Tabs/Vessel");
            }
            WebText(t,"DesignItemsHeading",254,114,710,40,24);
            WebText(t,"Details/DesignHeading",22,16,318,32,22);
            if(equipment) ButtonAt(t,"Details/BrowseShop","去商店看看","shop",72,428,218,42,true).gameObject.SetActive(false);
            WebPhoto(t.Find("Details"),"DesignArtBacking","DesignArt","DesignName",equipment?"blue-stripe":"gold-dots");
            Place(t,"Details/DescriptionScroll",24,316,314,90);
            var description=t.Find("Details/DescriptionScroll/Description")?.GetComponent<Text>();
            if(description!=null)
            {
                Place(t,"Details/DescriptionScroll/Description",0,0,314,90);
                description.fontSize=16; description.color=WebSoft; description.lineSpacing=1.4f;
                description.resizeTextForBestFit=false;
                description.horizontalOverflow=HorizontalWrapMode.Overflow;
                description.verticalOverflow=VerticalWrapMode.Truncate;
            }
            var summaryScroll=t.Find("Details/DescriptionScroll").GetComponent<ScrollRect>();
            if(summaryScroll!=null) summaryScroll.enabled=false;
            foreach(var name in new[]{"Buy","Equip","BrowseShop"})
            {
                if(t.Find("Details/"+name)==null)continue;
                Place(t,"Details/"+name,72,428,218,42);
                Place(t,"Details/"+name+"/Icon",16,9,24,24);
                WebText(t,"Details/"+name+"/Label",44,0,164,42,18,TextAnchor.MiddleCenter);
                t.Find("Details/"+name).GetComponent<CozyButtonVisual>().SetTint(Color.white);
            }
            ApplyItemDetails(t.gameObject);
        }

        static void WebPhoto(Transform t,string backingPath,string artPath,string namePath,string tape)
        {
            var frame=JournalSurface(t,"JournalPolaroid","surfaces/polaroid-frame",32,60,302,236);
            frame.rectTransform.pivot=new Vector2(.5f,.5f); frame.rectTransform.anchoredPosition=new Vector2(183,-178);
            var shadow=JournalLayer(frame.transform,"Shadow","surfaces/polaroid-shadow");
            shadow.transform.SetAsFirstSibling();
            // External children keep business paths while drawing around this shared photo pivot.
            var back=JournalSurface(t,backingPath,"surfaces/art-well",44,72,278,168);
            Place(t,artPath,60.68f,81,244.64f,150);
            WebText(t,namePath,44,250,278,32,24,TextAnchor.MiddleCenter);
            var strip=JournalSurface(t,"JournalPhotoTape","decor/tape-"+tape,136,46,104,26);
            strip.rectTransform.localEulerAngles=new Vector3(0,0,4);
            strip.color=new Color(1,1,1,.95f);
            var pose=frame.GetComponent<CozyJournalStaticPose>()??frame.gameObject.AddComponent<CozyJournalStaticPose>();
            pose.Configure(-2,frame,shadow,back,t.Find(artPath)?.GetComponent<Graphic>(),t.Find(namePath)?.GetComponent<Graphic>(),strip);
            // Paint the frame before the image/name, but above the aside background.
            frame.transform.SetSiblingIndex(1);
            back.transform.SetSiblingIndex(2);
            t.Find(artPath)?.SetAsLastSibling(); t.Find(namePath)?.SetAsLastSibling(); strip.transform.SetAsLastSibling();
        }

        // Batch/authoring API retained; historical operation is not a daily menu command.
        public static void RepairInventoryDetails()
        {
            var path = Folder + "PanelCatch.prefab";
            var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
            try
            {
                JournalInventory(root);
                WebBag(root.transform);
                ApplyInlineParity(root);
                ApplyPageMotionParity(root);
                Record(root);
                SaveJournalPrefab(root, path);
            }
            finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
            UnityEditor.AssetDatabase.SaveAssets();
        }

        static void WebBag(Transform t)
        {
            WebGrid(t,"Grid","Grid/Viewport/Slots",4);
            WebText(t,"Reflow_BagHeading",254,114,710,40,24);
            WebText(t,"DesignDetailHeading",1006,128,318,32,22);
            // The bag retains its real return/share actions; their controls remain below the photo.
            var frame=JournalSurface(t,"JournalBagPhoto","surfaces/polaroid-frame",1014,170,302,240);
            frame.rectTransform.pivot=new Vector2(.5f,.5f);frame.rectTransform.anchoredPosition=new Vector2(1165,-290);
            var shadow=JournalLayer(frame.transform,"Shadow","surfaces/polaroid-shadow");
            var back=JournalSurface(t,"DesignArtBacking","surfaces/art-well",1026,182,278,168);
            Place(t,"DesignArt",1042,191,246,150);
            WebText(t,"DesignName",1026,360,278,36,24,TextAnchor.MiddleCenter);
            var strip=JournalSurface(t,"JournalPhotoTape","decor/tape-gold-dots",1118,156,104,26);
            strip.rectTransform.localEulerAngles=new Vector3(0,0,4);
            (frame.GetComponent<CozyJournalStaticPose>()??frame.gameObject.AddComponent<CozyJournalStaticPose>()).Configure(-2,frame,shadow,back,t.Find("DesignArt").GetComponent<Graphic>(),t.Find("DesignName").GetComponent<Graphic>(),strip);
            frame.transform.SetSiblingIndex(t.Find("Reflow_CatchDetails").GetSiblingIndex()+1);
            back.transform.SetSiblingIndex(frame.transform.GetSiblingIndex()+1);
            t.Find("DesignArt").SetAsLastSibling();t.Find("DesignName").SetAsLastSibling();strip.transform.SetAsLastSibling();
            DetailRows(t,"DetailRows",1008,418,314,5,28);
            foreach(var label in t.Find("DetailRows").GetComponentsInChildren<Text>(true)) label.fontSize=16;
            WebText(t,"Detail",1008,418,314,84,16,TextAnchor.UpperLeft);
            Place(t,"Sell",1008,540,164,42); Place(t,"Return",1182,540,64,42);Place(t,"Share",1256,540,64,42);
            Place(t,"Sell/Icon",16,9,24,24);
            WebText(t,"Sell/Label",40,0,116,42,18,TextAnchor.MiddleCenter);
            foreach(var name in new[]{"Return","Share"}) WebText(t,name+"/Label",0,0,64,42,16,TextAnchor.MiddleCenter);
            Hide(t,"Return/Icon","Share/Icon");
            ApplyItemDetails(t.gameObject);
        }

        static void WebCard(GameObject root)
        {
            var t=root.transform;
            var back=JournalSurface(t,"DesignArtBacking","surfaces/art-well",10,10,190,112);
            var art=Place(t,"DesignItemArt",24,20,164,92);
            var label=t.Find("Label").GetComponent<Text>();
            WebText(t,"Label",10,130,190,27,20,TextAnchor.MiddleCenter);
            if(t.Find("Meta")==null) Label(t,"Meta","",10,161,190,20,15);
            WebText(t,"Meta",10,161,190,20,15,TextAnchor.MiddleCenter);
            t.Find("Meta").GetComponent<Text>().color=WebSoft;
            foreach(var name in new[]{"DesignArtBacking","Label","Meta"})
            {
                var r=t.Find(name) as RectTransform; r.anchorMax=Vector2.one;r.sizeDelta=new Vector2(-20,r.sizeDelta.y);
            }
            art.anchorMin=new Vector2(.07f,1);art.anchorMax=new Vector2(.93f,1);art.pivot=new Vector2(.5f,.5f);art.anchoredPosition=new Vector2(0,-66);art.sizeDelta=new Vector2(-17.2f,92);
            var tape=JournalSurface(t,"JournalTape","decor/tape-pink-stripe",-18,-8,104,26);
            tape.rectTransform.pivot=new Vector2(.5f,.5f);tape.rectTransform.anchoredPosition=new Vector2(34,-5);
            tape.rectTransform.localEulerAngles=new Vector3(0,0,28);tape.color=new Color(1,1,1,.95f);tape.transform.SetAsLastSibling();
            var shadow=JournalLayer(t,"JournalCardShadow","surfaces/sticker-shadow");shadow.transform.SetAsFirstSibling();
            var layout=root.GetComponent<LayoutElement>(); if(layout!=null) layout.minHeight=layout.preferredHeight=192;
        }
    }
}
#endif
