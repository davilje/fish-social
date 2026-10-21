using System;
using System.Collections.Generic;
using System.Text;
using FishSocial.Desktop.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2PondController : JournalV2Controller
    {
        [SerializeField] bool map;
        [SerializeField] ScrollRect grid;
        [SerializeField] RectTransform markers,photo;
        [SerializeField] JournalV2MapViewport cameraView;
        [SerializeField] Text facts,fee;
        [SerializeField] InputField search;
        [SerializeField] Button enter,alternate,retry,modeSell,modeAuto;
        [SerializeField] RectTransform feeRoot;
        [SerializeField] JournalV2Segment segment;
        [SerializeField] Button[] filters;
        readonly Dictionary<string,Button> buttons=new Dictionary<string,Button>();
        WorldMapPondView[] ponds=new WorldMapPondView[0];
        PondSelectionState state;
        string shownId;
        int filter;
        bool opened;
        public void Build(JournalV2Page page,bool worldMap)
        {
            Page=page;map=worldMap;
            JournalV2Nodes.Text(page.Aside,"Heading","鱼塘详情",24,18,314,32,22);
            photo=JournalV2Nodes.Polaroid(page.Aside,"Photo",null,"选择一处鱼塘",32,60,302,208,-2);
            var art=photo.Find("Visual/Art")?.GetComponent<Image>();if(art!=null)art.preserveAspect=false;
            var tape=photo.Find("Visual/Tape")?.GetComponent<Image>();if(tape!=null)tape.sprite=JournalV2Theme.Sprite("decor/tape-blue-stripe");
            facts=JournalV2CatalogUi.Facts(page.Aside,"Facts",26,280,310,60);
            fee=JournalV2Nodes.Text(page.Aside,"FeeSummary","",26,344,310,24,14);
            feeRoot=JournalV2Nodes.Rect(page.Aside,"FeeModes",20,378,326,48);
            JournalV2Nodes.Plate(feeRoot,"Track",0,0,326,48,new Color32(239,227,207,255),14,0).ShadowColor=Color.clear;
            var knob=JournalV2Nodes.Plate(feeRoot,"Knob",4,4,157,40,JournalV2Theme.Coral,10,0).rectTransform;
            segment=feeRoot.gameObject.AddComponent<JournalV2Segment>();segment.Configure(knob,new[]{new Vector2(4,-4),new Vector2(165,-4)});
            modeSell=JournalV2Nodes.Button(feeRoot,"Sell","回鱼出售",null,4,4,157,40,JournalV2ButtonKind.Segment);
            modeAuto=JournalV2Nodes.Button(feeRoot,"Auto","自动回鱼",null,165,4,157,40,JournalV2ButtonKind.Segment);
            enter=JournalV2Nodes.Button(page.Aside,"Enter","进入鱼塘","fish",20,438,326,42,JournalV2ButtonKind.Primary);
            alternate=JournalV2Nodes.Button(page.Body,"Alternate",map?"鱼塘列表":"世界地图","map",784,114,174,34);
            retry=JournalV2Nodes.Button(page.Body,"Retry","重新加载",null,804,604,154,32);retry.gameObject.SetActive(false);
            if(map)BuildMap(page);else BuildList(page);
        }
        void BuildMap(JournalV2Page page)
        {
            var categories=new[]{"novice","wilderness","reservoir","advanced","veteran","giant","forbidden"};
            for(var i=0;i<categories.Length;i++)
            {
                var dot=JournalV2Nodes.Plate(page.Rail,"Dot"+i,18,32+i*34,14,14,CategoryColor(categories[i]),7,2);dot.BorderColor=Color.white;dot.ShadowColor=Color.clear;
                JournalV2Nodes.Text(page.Rail,"Category"+i,DesktopGameData.CategoryLabel(categories[i]),42,22+i*34,150,34,17);
            }
            JournalV2Nodes.Text(page.Rail,"Note","拖动地图寻找水域\n滚轮缩放，点浮标看详情",18,330,174,74,16);
            var reset=JournalV2Nodes.Button(page.Rail,"Reset","回到全图",null,18,430,174,40);
            var group=JournalV2Nodes.Rect(page.Middle,"MapPaper",0,0,710,440);group.pivot=new Vector2(.5f,.5f);group.anchoredPosition=new Vector2(355,-220);group.localEulerAngles=new Vector3(0,0,.6f);
            var frame=JournalV2Nodes.Plate(group,"Frame",0,0,710,440,Color.white,4,0);frame.ShadowOffset=new Vector2(0,-10);frame.ShadowBlur=22;
            var viewport=JournalV2Nodes.Rect(group,"Viewport",10,10,690,420);
            var mask=viewport.gameObject.AddComponent<Image>();mask.sprite=JournalV2Theme.Sprite("surfaces/pond-map-clip");mask.type=Image.Type.Sliced;mask.raycastTarget=true;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            var content=JournalV2Nodes.Rect(viewport,"MapContent",-180,-140,1050,700);
            JournalV2Nodes.Image(content,"Landscape",JournalV2Theme.Sprite("scenes/world-map"),0,0,1050,700).preserveAspect=false;
            markers=JournalV2Nodes.Rect(content,"Markers",0,0,1050,700);
            cameraView=viewport.gameObject.AddComponent<JournalV2MapViewport>();cameraView.Configure(viewport,content);
            reset.onClick.AddListener(()=>cameraView.ResetView());
            var left=JournalV2Nodes.Image(group,"TapeLeft",JournalV2Theme.Sprite("decor/tape-blue-stripe"),-30,-6,104,26);left.rectTransform.localEulerAngles=new Vector3(0,0,28);
            var right=JournalV2Nodes.Image(group,"TapeRight",JournalV2Theme.Sprite("decor/tape-blue-stripe"),636,420,104,26);right.rectTransform.localEulerAngles=new Vector3(0,0,28);
        }
        void BuildList(JournalV2Page page)
        {
            filters=new Button[3];var names=new[]{"全部鱼塘","可进入","免费鱼塘"};
            for(var i=0;i<3;i++)filters[i]=JournalV2Nodes.Button(page.Rail,"Filter"+i,names[i],i==2?"coin":"pond",10,12+i*88,190,76,JournalV2ButtonKind.Index);
            search=JournalV2Nodes.Input(page.Rail,"Search","",18,330,174,36,16);
            var hint=search.placeholder as Text;if(hint!=null)hint.text="寻找鱼塘…";
            JournalV2Nodes.Text(page.Rail,"Note","选好档位，再去水边。",18,412,174,56,16);
            grid=JournalV2Nodes.GridScroll(page.Middle,"Ponds",0,0,710,440,2,new Vector2(329,220),new Vector2(18,18));
        }
        public override void Bind(JournalV2App app,JournalV2Page page)
        { base.Bind(app,page);state=app.PondSelection; }
        public override void Open(int tab=0,string targetId=null)
        {
            opened=true;filter=Mathf.Clamp(tab,0,2);state=App.PondSelection;state.Changed-=Refresh;state.Changed+=Refresh;
            ponds=WorldMapPondCatalog.LoadVisible();
            enter.onClick.RemoveAllListeners();enter.onClick.AddListener(Enter);
            alternate.onClick.RemoveAllListeners();alternate.onClick.AddListener(()=>App.Navigate(map?"ponds":"map",0,state.Selected?.pondId));
            retry.onClick.RemoveAllListeners();retry.onClick.AddListener(()=>{if(state.EntryFailed&&state.Progress!=null)Enter();else App.Run(state.LoadProgress(this));});
            modeSell.onClick.RemoveAllListeners();modeSell.onClick.AddListener(()=>state.SelectFeeMode("sell_only"));
            modeAuto.onClick.RemoveAllListeners();modeAuto.onClick.AddListener(()=>state.SelectFeeMode("auto_return"));
            if(filters!=null)for(var i=0;i<filters.Length;i++){var n=i;filters[i].onClick.RemoveAllListeners();filters[i].onClick.AddListener(()=>{filter=n;BuildItems();Refresh();});}
            if(search!=null){search.onValueChanged.RemoveAllListeners();search.onValueChanged.AddListener(_=>{BuildItems();Refresh();});}
            if(!string.IsNullOrEmpty(targetId))state.Select(Array.Find(ponds,item=>item.pondId==targetId));
            if(state.Selected==null&&ponds.Length>0)state.Select(ponds[0]);
            BuildItems();Refresh();
            if(!state.ProgressLoading)App.Run(state.LoadProgress(this));
            if(map&&!string.IsNullOrEmpty(targetId)&&state.Selected!=null)cameraView.Focus(state.Selected.x,state.Selected.y);
        }
        public override void Close(){opened=false;if(state!=null)state.Changed-=Refresh;}
        void OnDestroy(){if(state!=null)state.Changed-=Refresh;}
        public void ApplySnapshot(WorldMapPondView[] values,FishingProgressDto progress,string selectedId=null,string feeMode="sell_only")
        {
            state=new PondSelectionState();state.ApplyProgress(progress);ponds=values??WorldMapPondCatalog.LoadVisible();
            state.Select(Array.Find(ponds,item=>item.pondId==selectedId)??(ponds.Length>0?ponds[0]:null));state.SelectFeeMode(feeMode);BuildItems();Refresh();
        }
        void BuildItems()
        {
            var parent=map?markers:grid.content;JournalV2CatalogUi.Clear(parent);buttons.Clear();
            var ordinal=0;
            foreach(var pond in ponds)
            {
                if(!map && (filter==1&&!state.CanEnter(pond)||filter==2&&!PondSelectionState.IsFree(pond)))continue;
                if(!map&&!string.IsNullOrWhiteSpace(search.text)&&pond.displayName.IndexOf(search.text.Trim(),StringComparison.OrdinalIgnoreCase)<0)continue;
                Button button;
                if(map)
                {
                    var root=JournalV2Nodes.Rect(markers,pond.pondId,pond.x*1050-32,pond.y*700-32,64,64);
                    var hit=root.gameObject.AddComponent<Image>();hit.color=Color.clear;button=root.gameObject.AddComponent<Button>();button.targetGraphic=hit;button.transition=Selectable.Transition.None;
                    var visual=JournalV2Nodes.Rect(root,"Visual",0,0,64,64);
                    var body=JournalV2Nodes.Image(visual,"Body",JournalV2Theme.Sprite("motion/bobber-base"),15,-12,34,48);body.preserveAspect=false;
                    var color=JournalV2Nodes.Image(body.transform,"Color",JournalV2Theme.Sprite("motion/bobber-color"),0,0,34,48);color.preserveAspect=false;color.color=CategoryColor(pond.pondCategory);
                    JournalV2Nodes.Image(body.transform,"Highlight",JournalV2Theme.Sprite("motion/bobber-highlight"),0,0,34,48).preserveAspect=false;
                    var width=Mathf.Max(64,pond.displayName.Length*16+20);
                    JournalV2Nodes.Plate(visual,"LabelPill",32-width/2,42,width,28,new Color(1,.992f,.969f,.95f),10,0).ShadowOffset=new Vector2(0,-2);
                    JournalV2Nodes.Text(visual,"Label",pond.displayName,32-width/2,42,width,28,16,TextAnchor.MiddleCenter);
                    JournalV2Nodes.Image(visual,"Paw",JournalV2Theme.Sprite("decor/paw-mark"),44,-30,34,34);
                    root.gameObject.AddComponent<JournalV2MapMarker>().Configure(visual);
                }
                else
                {
                    button=JournalV2CatalogUi.Card(parent,pond.pondId,pond.displayName,DesktopGameData.CategoryLabel(pond.pondCategory)+" · "+state.AccessReason(pond),JournalV2Theme.Sprite("journal/ponds/"+pond.pondId),new Vector2(329,220),ordinal++,true);
                    JournalV2CatalogUi.Visual(button).Find("Artwork").GetComponent<Image>().preserveAspect=false;
                }
                var captured=pond;button.onClick.AddListener(()=>Select(captured));buttons[pond.pondId]=button;
            }
            if(!map&&buttons.Count==0)JournalV2Nodes.Text(grid.content,"Empty","没有符合条件的鱼塘",0,0,620,100,22,TextAnchor.MiddleCenter);
        }
        void Select(WorldMapPondView pond)
        {
            if(state.Busy)return;var same=state.Selected?.pondId==pond.pondId;state.Select(pond);
            if(map)
            {
                if(buttons.TryGetValue(pond.pondId,out var marker))
                { var motion=marker.GetComponent<JournalV2MapMarker>();if(!state.CanEnter(pond))motion?.Shake();else if(same)motion?.Stamp(); }
                cameraView.Focus(pond.x,pond.y);
            }
            if(same)JournalV2CatalogUi.Photo(photo,JournalV2Theme.Sprite("journal/ponds/"+pond.pondId),pond.displayName,true);
        }
        void Refresh()
        {
            if(state==null)return;
            foreach(var pair in buttons)
            {
                var pond=Array.Find(ponds,item=>item.pondId==pair.Key);var selected=state.Selected?.pondId==pair.Key;var allowed=state.CanEnter(pond);
                pair.Value.GetComponent<JournalV2Button>()?.SetSelected(selected);JournalV2CatalogUi.Enabled(pair.Value,!state.Busy);
                if(map)
                { pair.Value.GetComponent<JournalV2MapMarker>()?.SetState(selected,!allowed,CategoryColor(pond.pondCategory)); }
            }
            var chosen=state.Selected;var changed=shownId!=chosen?.pondId;shownId=chosen?.pondId;
            JournalV2CatalogUi.Photo(photo,chosen==null?null:JournalV2Theme.Sprite("journal/ponds/"+chosen.pondId),chosen?.displayName??"选择一处鱼塘",changed);
            var art=photo.Find("Visual/Art")?.GetComponent<Image>();if(art!=null)art.preserveAspect=false;
            if(chosen==null)facts.text="点选鱼塘，查看场景和入塘条件。";
            else
            {
                var definition=DesktopGameData.GetPond(chosen.pondId);var text=new StringBuilder(DesktopGameData.CategoryLabel(chosen.pondCategory));
                if(definition!=null)text.Append(" · ").Append(DesktopGameData.WaterLabel(definition.waterType)).Append("\n").Append(definition.realWorldRef);
                text.Append("\n鱼种：").Append(DesktopGameData.PondSpeciesSummary(chosen.pondId,4)).Append("\n").Append(state.AccessReason(chosen));
                if(PondSelectionState.HasDualFee(chosen))text.Append("\n出售档 ").Append(chosen.feePer2hSellOnly).Append(" 金币 / 2小时；回鱼档 ").Append(chosen.feePer2hAutoReturn).Append(" 金币 / 2小时");
                if(!string.IsNullOrEmpty(state.Status))text.Append("\n").Append(state.Status);facts.text=text.ToString();
            }
            var amount=state.ActiveFee(chosen);fee.text=chosen==null?"":amount==0?"免费入塘":amount+" 金币 / 2小时 · 今日 "+(state.Progress?.todayFeeCharges.ToString()??"—")+"/"+(chosen.maxFeeChargesPerDay>0?chosen.maxFeeChargesPerDay:4)+" 次";
            feeRoot.gameObject.SetActive(PondSelectionState.HasDualFee(chosen));var auto=state.ReturnFeeMode=="auto_return";segment.Select(auto?1:0);
            modeSell.GetComponent<JournalV2Button>()?.SetSelected(!auto);modeAuto.GetComponent<JournalV2Button>()?.SetSelected(auto);
            JournalV2CatalogUi.Enabled(modeSell,!state.Busy);JournalV2CatalogUi.Enabled(modeAuto,!state.Busy);
            JournalV2CatalogUi.Enabled(enter,state.CanEnter(chosen)&&!state.Busy&&App?.Pond!=null);
            JournalV2CatalogUi.Label(enter,state.Busy?"正在前往…":state.CanEnter(chosen)?"进入鱼塘":"暂不可进入");
            JournalV2CatalogUi.Enabled(alternate,!state.Busy);
            retry.gameObject.SetActive(!state.Busy&&!state.ProgressLoading&&(state.EntryFailed||state.Progress==null));
            JournalV2CatalogUi.Label(retry,state.EntryFailed?"重试进入":"重新加载");
            if(Page!=null){Page.Heading.text=(map?"世界地图":"寻一处好水")+"    "+buttons.Count+" 处";Page.Status.text=state.Busy?"正在连接鱼塘…":state.EntryFailed?"进入失败，右侧详情可查看原因":state.ProgressLoading?"正在核对入塘条件…":state.AccessReason(chosen);}
            if(filters!=null)for(var i=0;i<filters.Length;i++){filters[i].GetComponent<JournalV2Button>()?.SetSelected(i==filter);JournalV2CatalogUi.Enabled(filters[i],!state.Busy);}
        }
        void Enter()
        {
            if(state==null||state.Busy||!state.CanEnter(state.Selected)||App?.Pond==null)return;
            if(map && buttons.TryGetValue(state.Selected.pondId,out var button))button.GetComponent<JournalV2MapMarker>()?.Enter();
            var journey=App.GetComponent<JournalV2PondJourney>()??App.gameObject.AddComponent<JournalV2PondJourney>();journey.Enter(App,state.Selected.pondId,state.ReturnFeeMode);
        }
        public static Color CategoryColor(string category)
        { switch(category){case "wilderness":return new Color32(97,184,107,255);case "reservoir":return new Color32(71,158,199,255);case "advanced":return new Color32(242,191,71,255);case "veteran":return new Color32(158,107,219,255);case "giant":return new Color32(184,199,219,255);case "forbidden":return new Color32(219,97,82,255);default:return new Color32(97,102,107,255);} }
    }
}
