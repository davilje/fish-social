#if UNITY_EDITOR
using System;
using FishSocial.Desktop.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    /// <summary>Saved native layout matching journal-workspace settlement; no runtime layout generation.</summary>
    public static class CozyWorkspaceSettlementAuthoring
    {
        public static void Apply(GameObject root)
        {
            if (root == null || root.name != "PanelPondSettlement") return;
            for (var i = root.transform.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            DesktopModalUi.Stretch(root);
            var backdrop = root.GetComponent<Image>();
            if (backdrop != null) { backdrop.sprite = null; backdrop.overrideSprite = null; backdrop.color = Color.clear; backdrop.raycastTarget = true; }
            if (root.GetComponent<DesktopPondSettlementModalView>() == null) root.AddComponent<DesktopPondSettlementModalView>();
            var dim = DesktopModalUi.Panel("Dim", root.transform, new Color(0.25f, .16f, .11f, .4f)); DesktopModalUi.Stretch(dim);
            var panel = DesktopModalUi.Panel("Panel", root.transform, Color.clear).transform;
            var rect = (RectTransform)panel; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f); rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1120, 720);
            panel.gameObject.AddComponent<CanvasGroup>();
            var paper = DesktopModalUi.Panel("JournalPaper", panel, Color.white).GetComponent<Image>();
            paper.raycastTarget = false;
            CozyJournalTheme.ApplySurface(paper, "surfaces/modal-paper", true);
            paper.transform.SetAsFirstSibling();
            Label(panel, "Title", "收竿啦，带上今天的湖畔记忆", 32, 26, 990, 42, 28);
            Button(panel, "Close", "×", 1036, 26, 48, 42);
            var closeRect=(RectTransform)panel.Find("Close");closeRect.anchorMin=closeRect.anchorMax=closeRect.pivot=Vector2.one;closeRect.anchoredPosition=new Vector2(-36,-26);
            Rule(panel, "HeaderRule", 32, 82, 1056);
            Label(panel, "Summary", "", 32, 96, 1056, 28, 16);
            var stats = Node(panel, "Stats", 32, 142, 1056, 96);
            var names = new[] { "Bites", "Escapes", "Duration", "Catches" };
            var titles = new[] { "本次咬钩", "脱钩", "钓鱼时长", "成功钓获" };
            for (var i=0;i<4;i++)
            {
                var card = Surface(stats, names[i], "surfaces/paper-aside", i * 267.5f, 0, 253.5f, 96);
                if (card.GetComponent<Image>().sprite == null) CozyJournalTheme.ApplySurface(card.GetComponent<Image>(), "surfaces/paper-aside");
                Label(card, "Label", titles[i], 18, 10, 217, 25, 16);
                Label(card, "Value", "—", 18, 40, 220, 43, i == 2 ? 27 : 32);
            }
            Label(panel, "RecordCount", "本次鱼获", 32, 260, 240, 36, 19);
            Button(panel, "Filter", "全部鱼获 ▾", 386, 260, 160, 38, 14);
            Button(panel, "Sort", "最近钓获优先 ▾", 560, 260, 250, 38, 14);
            var scrollRoot = Node(panel, "ListScroll", 32, 310, 778, 318);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRoot.gameObject.AddComponent<Image>().color = Color.clear;
            var viewport = Node(scrollRoot, "Viewport", 0, 0, 778, 318); viewport.gameObject.AddComponent<RectMask2D>();
            var list = Node(viewport, "List", 0, 0, 778, 318);
            var listRect = (RectTransform)list; listRect.anchorMax = Vector2.one; listRect.sizeDelta = new Vector2(0,318);
            scroll.viewport = (RectTransform)viewport; scroll.content = listRect; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            Label(panel, "Empty", "这次还没有留下鱼获。\n下一次，再去湖边坐一会儿。", 32, 350, 778, 220, 20).alignment = TextAnchor.MiddleCenter;
            var row = Surface(panel, "RecordTemplate", "surfaces/input-default", 0, 0, 778, 96);
            var art = DesktopModalUi.Panel("FishArt", row, Color.white).GetComponent<Image>(); Place(art.rectTransform, 14,12,104,72); art.preserveAspect = true; art.raycastTarget = false;
            Label(row,"Name","鱼获名称",132,8,252,28,19);
            Label(row,"Time","",132,39,252,20,12);
            Label(row,"Quality","",132,64,252,20,12);
            Label(row,"Size","—",398,22,90,28,17); Label(row,"SizeLabel","体长",398,54,90,20,12);
            Label(row,"Weight","—",500,22,96,28,17); Label(row,"WeightLabel","体重",500,54,96,20,12);
            Button(row,"Share","分享",628,27,126,42,14);
            row.gameObject.SetActive(false);
            var account = Surface(panel, "Account", "surfaces/paper-aside", 832, 260, 256, 368);
            Label(account,"Title","这次出行的小账本",18,16,220,36,21);
            var labels = new[] { "回鱼收入", "鱼塘费用", "玩家经验", "鱼塘经验" };
            var paths = new[] { "Income", "Fees", "PlayerXp", "PondXp" };
            for(var i=0;i<4;i++)
            {
                Label(account, paths[i]+"Label", labels[i],18,72+i*38,104,26,15);
                Label(account, paths[i],"—",124,72+i*38,114,26,15).alignment = TextAnchor.MiddleRight;
            }
            Rule(account,"NetRule",18,230,220);
            Label(account,"NetLabel","本次净收入",18,242,220,26,15);
            Label(account,"Net","—",18,274,220,38,29).color = new Color32(112,159,74,255);
            Label(account,"Hint","只统计本次出行，入包鱼获尚未出售。",18,326,220,28,12);
            Rule(panel,"FooterRule",32,650,1056);
            Label(panel,"FooterNote","每一次耐心等待，都值得记在这一页。",32,665,600,32,15);
            Button(panel,"Home","回到个人手账",862,662,226,43,18);
            Label(panel,"DurationHint","钓鱼时长不含闲置时间；回塘鱼获无法再次分享。",32,632,778,18,12);
            var choices = DesktopModalUi.Panel("ChoiceLayer", panel, Color.clear); DesktopModalUi.Stretch(choices);
            var filterMenu = Surface(choices.transform,"FilterMenu","surfaces/input-default",386,303,160,134);
            var sortMenu = Surface(choices.transform,"SortMenu","surfaces/input-default",560,303,250,134);
            var filters = new[] { "全部鱼获", "只看入包", "只看回塘" };
            var sorts = new[] { "最近钓获优先", "体长从大到小", "体重从大到小" };
            for (var i=0;i<3;i++)
            {
                Button(filterMenu,"Option"+i,filters[i],6,6+i*41,148,39,14);
                Button(sortMenu,"Option"+i,sorts[i],6,6+i*41,238,39,14);
            }
            choices.SetActive(false);
        }

        public static void Fill(GameObject root)
        {
            var view = root != null ? root.GetComponent<DesktopPondSettlementModalView>() : null; if(view == null) return;
            var at = new DateTimeOffset(2026,10,22,14,0,0,TimeSpan.FromHours(8)).ToUnixTimeMilliseconds();
            var species = new[] { "crucian", "carp", "koi", "grass_carp", "largemouth_bass", "crucian", "koi", "carp" };
            var records = new PondSessionCatchEntryDto[8];
            for(var i=0;i<8;i++) records[i] = new PondSessionCatchEntryDto { speciesId=species[i], quality=i%3==0?"purple":"blue", sizeM=.32f+i*.08f, weightKg=(float)Math.Round(12*Math.Pow(.32+i*.08,3),2), caughtAt=at+(18+i*12)*60000, outcome=i%2==0?"kept":"returned", inventoryItemId=i%2==0?"preview-kept-"+i:null };
            view.PresentSummary(new PondSessionSummaryDto { pondId="pond-calm", pondName="千岛湖", returnFeeMode="auto_return", joinedAt=at, leftAt=at+108*60000, fishingDurationMs=108*60000,biteCount=12,escapeCount=4,catches=records, feesPaid=200,totalReturnGold=820,netProfit=620,totalCatchPlayerXp=144,totalCatchPondXp=82 });
        }
        static Transform Node(Transform parent,string name,float x,float y,float width,float height)
        { var go = new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false); Place((RectTransform)go.transform,x,y,width,height); return go.transform; }
        static Transform Surface(Transform parent,string name,string surface,float x,float y,float width,float height)
        { var go=DesktopModalUi.Panel(name,parent,Color.white);Place((RectTransform)go.transform,x,y,width,height);CozyJournalTheme.ApplySurface(go.GetComponent<Image>(),surface);return go.transform; }
        static Text Label(Transform parent,string name,string value,float x,float y,float width,float height,int size)
        { var label=DesktopModalUi.Label(parent,name,value,size,TextAnchor.MiddleLeft);Place(label.rectTransform,x,y,width,height);label.supportRichText=false;label.verticalOverflow=VerticalWrapMode.Truncate;label.fontStyle=FontStyle.Normal;return label; }
        static Button Button(Transform parent,string name,string value,float x,float y,float width,float height,int size=20)
        {
            var button=DesktopModalUi.MakeButton(parent,name,value,null);Place((RectTransform)button.transform,x,y,width,height);
            var label=button.transform.Find("Label").GetComponent<Text>();label.fontSize=size;label.fontStyle=FontStyle.Normal;
            var icon=button.transform.Find("CozyIcon") ?? button.transform.Find("Icon");if(icon!=null)icon.gameObject.SetActive(false);
            var inline=button.GetComponent<CozyJournalInlineContent>()??button.gameObject.AddComponent<CozyJournalInlineContent>();
            inline.ConfigureCentered((RectTransform)button.transform,label,null,Vector2.zero,0,8);
            return button;
        }
        static void Rule(Transform parent,string name,float x,float y,float width)
        {var line=DesktopModalUi.Panel(name,parent,new Color32(219,196,161,140));Place((RectTransform)line.transform,x,y,width,1);line.GetComponent<Image>().raycastTarget=false;}
        static void Place(RectTransform rect,float x,float y,float width,float height)
        {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);}
    }
}
#endif
