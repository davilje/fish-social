using System;
using System.Collections;
using System.Collections.Generic;
using FishSocial.Desktop.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2GalleryController : JournalV2Controller
    {
        [SerializeField] ScrollRect grid;
        [SerializeField] RectTransform photo;
        [SerializeField] Text facts;
        [SerializeField] InputField search;
        [SerializeField] Button retry,visit;
        [SerializeField] Button[] tabs;
        readonly Dictionary<string,FishCodexEntryDto> records=new Dictionary<string,FishCodexEntryDto>();
        readonly Dictionary<string,Button> cards=new Dictionary<string,Button>();
        int tab,version;
        string selected;
        bool loading;
        public void Build(JournalV2Page page)
        {
            Page=page;tabs=new Button[3];var names=new[]{"全部鱼种","已经相遇","未曾相遇"};
            for(var i=0;i<3;i++)tabs[i]=JournalV2Nodes.Button(page.Rail,"Tab"+i,names[i],i==0?"book":i==1?"fish":"lock",10,12+i*88,190,76,JournalV2ButtonKind.Index);
            JournalV2Nodes.Text(page.Rail,"Note","每一次相遇，\n都值得珍藏。",18,388,174,80,16);
            search=JournalV2Nodes.Input(page.Body,"Search","",714,114,244,34,16);
            grid=JournalV2Nodes.GridScroll(page.Middle,"Species",0,0,710,440,4,new Vector2(157,192),new Vector2(18,18));
            JournalV2Nodes.Text(page.Aside,"Heading","鱼类手记",24,18,314,32,22);
            photo=JournalV2Nodes.Polaroid(page.Aside,"Photo",null,"等待下一次相遇",32,60,302,208,-2);
            facts=JournalV2CatalogUi.Facts(page.Aside,"Facts",26,286,310,134);
            visit=JournalV2Nodes.Button(page.Aside,"Visit","去钓鱼世界","map",26,436,310,42,JournalV2ButtonKind.Primary);
            retry=JournalV2Nodes.Button(page.Body,"Retry","重新加载",null,804,604,154,32);retry.gameObject.SetActive(false);
        }
        public override void Open(int tab=0,string targetId=null)
        {
            this.tab=Mathf.Clamp(tab,0,2);selected=targetId;
            for(var i=0;i<tabs.Length;i++){var n=i;tabs[i].onClick.RemoveAllListeners();tabs[i].onClick.AddListener(()=>{this.tab=n;Render();});}
            search.onValueChanged.RemoveAllListeners();search.onValueChanged.AddListener(_=>Render());
            visit.onClick.RemoveAllListeners();visit.onClick.AddListener(()=>App.Navigate("map"));
            retry.onClick.RemoveAllListeners();retry.onClick.AddListener(Reload);Render();Reload();
        }
        public override void Close(){++version;loading=false;}
        public void ApplySnapshot(FishCodexEntryDto[] values,int category=0)
        { records.Clear();foreach(var value in values??new FishCodexEntryDto[0])if(value!=null&&!string.IsNullOrEmpty(value.speciesId))records[DesktopGameData.ResolveSpeciesId(value.speciesId)]=value;tab=Mathf.Clamp(category,0,2);loading=false;Render(); }
        void Reload(){if(loading)return;loading=true;retry.gameObject.SetActive(false);if(Page!=null)Page.Status.text="正在加载图鉴…";App?.Run(Load(++version));}
        IEnumerator Load(int token)
        {
            if(App?.Api==null||!App.Api.CanUse){loading=false;retry.gameObject.SetActive(true);if(Page!=null)Page.Status.text="请先完成登录。";yield break;}
            var ok=false;FishCodexEntryDto[] entries=null;string error=null;
            yield return App.Api.GetCodex((success,data,message)=>{ok=success;entries=data;error=message;});
            if(this==null||token!=version)yield break;loading=false;
            if(ok){ApplySnapshot(entries,tab);Page.Status.text="图鉴已同步 · 已遇见 "+records.Count+" 种鱼";}
            else{retry.gameObject.SetActive(true);Page.Status.text=error??"图鉴加载失败。";facts.text=Page.Status.text+"\n点击重新加载后再试。";}
        }
        void Render()
        {
            if(grid==null)return;JournalV2CatalogUi.Clear(grid.content);cards.Clear();var shown=new List<DesktopGameData.SpeciesDef>();
            foreach(var species in DesktopGameData.Species)
            {
                if(species==null)continue;var caught=records.TryGetValue(species.speciesId,out var entry)&&entry.totalCaught>0;
                if(tab==1&&!caught||tab==2&&caught)continue;
                if(!string.IsNullOrWhiteSpace(search.text)&&species.name.IndexOf(search.text.Trim(),StringComparison.OrdinalIgnoreCase)<0)continue;
                shown.Add(species);
            }
            if(!shown.Exists(item=>item.speciesId==selected))selected=shown.Count>0?shown[0].speciesId:null;
            for(var i=0;i<shown.Count;i++)
            {
                var species=shown[i];var caught=records.TryGetValue(species.speciesId,out var entry)&&entry.totalCaught>0;
                var card=JournalV2CatalogUi.Card(grid.content,species.speciesId,species.name,caught?"最大 "+entry.maxSizeM.ToString("0.00")+" m":"尚未相遇",JournalV2CatalogUi.Fish(species.speciesId),new Vector2(157,192),i);
                if(!caught){var art=JournalV2CatalogUi.Visual(card).Find("Artwork").GetComponent<Image>();art.color=new Color(.45f,.39f,.31f,.3f);}
                cards[species.speciesId]=card;card.onClick.AddListener(()=>{selected=species.speciesId;Details(true);});
            }
            if(shown.Count==0)JournalV2Nodes.Text(grid.content,"Empty","没有符合条件的鱼种",0,0,620,100,22,TextAnchor.MiddleCenter);
            for(var i=0;i<tabs.Length;i++)tabs[i].GetComponent<JournalV2Button>()?.SetSelected(i==tab);
            if(Page!=null)Page.Heading.text="相遇记录    "+shown.Count+" 种";Details(false);
        }
        void Details(bool reveal)
        {
            foreach(var pair in cards)pair.Value.GetComponent<JournalV2Button>()?.SetSelected(pair.Key==selected);
            var species=DesktopGameData.GetSpeciesDef(selected);records.TryGetValue(selected??"",out var record);
            JournalV2CatalogUi.Photo(photo,species==null?null:JournalV2CatalogUi.Fish(species.speciesId),species?.name??"等待下一次相遇",reveal);
            facts.text=species==null?"换个分类或关键词找找看。":"食性："+DesktopGameData.DietLabel(species.diet)+"\n习性："+DesktopGameData.CatchGroupLabel(species.catchGroup)+"\n常见体长："+species.typicalMinM.ToString("0.##")+"—"+species.typicalMaxM.ToString("0.##")+" m\n"+(record==null?"还未钓获，下一次也许就能相遇。":"累计钓获："+record.totalCaught+"\n最大记录："+record.maxSizeM.ToString("0.00")+" m\n初次相遇："+JournalV2CatalogUi.Date(record.firstCaughtAt));
        }
    }
}
