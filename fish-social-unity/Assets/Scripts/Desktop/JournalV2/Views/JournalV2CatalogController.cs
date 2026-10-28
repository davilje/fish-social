using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using FishSocial.Desktop.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    /// <summary>Server-owned catalogue, equipment and inventory actions with one mutation in flight.</summary>
    public sealed class JournalV2CatalogController : JournalV2Controller
    {
        [SerializeField] string mode;
        [SerializeField] ScrollRect grid;
        [SerializeField] RectTransform photo;
        [SerializeField] Text facts;
        [SerializeField] Button action,secondary,tertiary,retry;
        [SerializeField] Button[] tabs;
        ShopBaitDto[] baits=new ShopBaitDto[0];
        ShopTackleDto[] rods=new ShopTackleDto[0];
        ShopVesselDto[] vessels=new ShopVesselDto[0];
        FishInventoryItemDto[] inventory=new FishInventoryItemDto[0];
        ShopGearDto gear;
        readonly List<Row> rows=new List<Row>();
        readonly Dictionary<string,Button> cards=new Dictionary<string,Button>();
        int tab,version,coins;
        string selected;
        bool loading,busy,opened,hasData;
        sealed class Row { public string Id,Name,Kind,Meta,Facts;public Sprite Art;public bool Owned,Equipped,Available;public int Price;public FishInventoryItemDto Fish; }

        public void Build(JournalV2Page page,string id)
        {
            Page=page;mode=id;
            var labels=mode=="bag"?new[]{"鱼获","物品"}:mode=="equip"?new[]{"钓竿","鱼饵"}:new[]{"鱼饵","钓具","船具"};
            var icons=mode=="bag"?new[]{"fish","bag"}:mode=="equip"?new[]{"rod","leaf"}:new[]{"leaf","rod","pond"};
            tabs=new Button[labels.Length];
            for(var i=0;i<tabs.Length;i++)tabs[i]=JournalV2Nodes.Button(page.Rail,DesktopLocalization.Concat("Tab", i),labels[i],icons[i],10,12+i*88,190,76,JournalV2ButtonKind.Index);
            JournalV2Nodes.Text(page.Rail,"Note",mode=="shop"?"小小补给，\n也是钓鱼日常的快乐。":mode=="equip"?"带上顺手的装备，\n再去水边坐一会。":"每一条鱼，\n都有自己的故事。",18,388,174,90,16);
            var size=mode=="bag"?new Vector2(157,192):new Vector2(215.3f,192);
            grid=JournalV2Nodes.GridScroll(page.Middle,"Items",0,0,710,440,mode=="bag"?4:3,size,new Vector2(18,18));
            JournalV2Nodes.Text(page.Aside,"Heading",mode=="bag"?"鱼获手记":"物品详情",24,18,314,32,22);
            photo=JournalV2Nodes.Polaroid(page.Aside,"Photo",null,"选择一件物品",32,60,302,236,-2);
            facts=JournalV2CatalogUi.Facts(page.Aside,"Facts",26,318,310,90);
            action=JournalV2Nodes.Button(page.Aside,"Action","选择物品","fish",20,422,mode=="bag"?142:326,48,JournalV2ButtonKind.Primary);
            secondary=JournalV2Nodes.Button(page.Aside,"Secondary",mode=="bag"?"回鱼":"去商店",null,174,422,80,48);
            tertiary=JournalV2Nodes.Button(page.Aside,"Tertiary",mode=="bag"?"分享":"去钓鱼",null,266,422,80,48);
            secondary.gameObject.SetActive(mode=="bag");tertiary.gameObject.SetActive(mode=="bag");
            retry=JournalV2Nodes.Button(page.Body,"Retry","重新加载",null,804,604,154,32);retry.gameObject.SetActive(false);
        }
        void Wire()
        {
            for(var i=0;i<tabs.Length;i++){var index=i;tabs[i].onClick.RemoveAllListeners();tabs[i].onClick.AddListener(()=>SelectTab(index));}
            action.onClick.RemoveAllListeners();action.onClick.AddListener(Act);
            secondary.onClick.RemoveAllListeners();secondary.onClick.AddListener(()=>{if(mode=="bag" && tab==0)ReturnFish();else App?.Navigate("shop",tab);});
            tertiary.onClick.RemoveAllListeners();tertiary.onClick.AddListener(()=>{if(mode=="bag" && tab==0 && FindSelected()?.Fish!=null)App?.Navigate("compose",0,selected);else App?.Navigate("ponds");});
            retry.onClick.RemoveAllListeners();retry.onClick.AddListener(Reload);
        }
        public override void Open(int tab=0,string targetId=null)
        { opened=true;this.tab=Mathf.Clamp(tab,0,tabs.Length-1);selected=targetId;Wire();Render();if(!busy)Reload(); }
        public override void Close(){opened=false;++version;loading=false;}
        public void ApplySnapshot(ShopBaitDto[] baitRows,ShopTackleDto[] rodRows,ShopVesselDto[] vesselRows,ShopGearDto equipment,int balance,FishInventoryItemDto[] fish=null,int category=0)
        { baits=baitRows??new ShopBaitDto[0];rods=rodRows??new ShopTackleDto[0];vessels=vesselRows??new ShopVesselDto[0];gear=equipment;coins=balance;inventory=fish??new FishInventoryItemDto[0];tab=Mathf.Clamp(category,0,tabs.Length-1);hasData=true;loading=false;Render(); }
        void Reload(){if(busy || loading)return;loading=true;retry.gameObject.SetActive(false);Render();App?.Run(Load(++version));}
        IEnumerator Load(int token)
        {
            if(App?.Api==null || !App.Api.CanUse){Fail("请先完成登录。");yield break;}
            var okCatalog=false;var okGear=false;var okInventory=mode!="bag";string error=null;
            ShopBaitDto[] newBaits=null;ShopTackleDto[] newRods=null;ShopVesselDto[] newVessels=null;ShopGearDto newGear=null;FishInventoryItemDto[] newFish=null;var balance=0;
            yield return App.Api.GetShopCatalog((ok,a,b,c,message)=>{okCatalog=ok;newBaits=a;newRods=b;newVessels=c;if(!ok)error=message;});
            if(this==null || token!=version)yield break;
            yield return App.Api.GetShopGear((ok,value,money,message)=>{okGear=ok;newGear=value;balance=money;if(!ok)error=message;});
            if(this==null || token!=version)yield break;
            if(mode=="bag")yield return App.Api.GetInventoryItems((ok,value,message)=>{okInventory=ok;newFish=value;if(!ok)error=message;});
            if(this==null || token!=version)yield break;
            loading=false;
            if(!okCatalog || !okGear || !okInventory){Fail(error??"数据加载失败，请重试。");yield break;}
            ApplySnapshot(newBaits,newRods,newVessels,newGear,balance,newFish,tab);Status(DesktopLocalization.Concat(DesktopLocalization.Concat("已同步 · ", coins.ToString("N0")), " 金币"));
        }
        void Fail(string error){loading=false;retry.gameObject.SetActive(true);Status(error);Render();DesktopLocalization.SetText(facts, DesktopLocalization.Concat(error, "\n点击重新加载后再试。"));}
        void Status(string value){ value = DesktopLocalization.Message(value);if(Page!=null)DesktopLocalization.SetText(Page.Status, value);}
        void SelectTab(int value){if(loading || busy || value==tab)return;tab=value;selected=null;Render();}
        void Select(string id){selected=id;RenderSelection(true);}
        string Category => mode=="equip"?(tab==0?"rod":"bait"):mode=="bag"?(tab==0?"fish":"owned"):tab==0?"bait":tab==1?"rod":"vessel";
        void BuildRows()
        {
            rows.Clear();var category=Category;
            if(category=="fish")foreach(var fish in inventory)
            {
                if(fish==null)continue;var price=DesktopGameData.EstimateSellPrice(fish.quality,fish.sizeM,fish.speciesId);
                rows.Add(new Row{Id=fish.id,Kind="fish",Fish=fish,Name=DesktopGameData.SpeciesName(fish.speciesId),Meta=DesktopLocalization.Concat(fish.sizeM.ToString("0.00"), " m"),Price=price,Available=true,Art=JournalV2CatalogUi.Fish(fish.speciesId),Facts=DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat("体长 ", fish.sizeM.ToString("0.00")), " m · "), DesktopGameData.FormatWeightKg(DesktopGameData.CalcWeightKg(fish.sizeM))), "\n钓获于 "), JournalV2CatalogUi.Date(fish.caughtAt)), "\n鱼塘 "), (DesktopGameData.GetPond(fish.pondId)?.name??fish.pondId)), "\n预计出售 "), price), " 金币（以结算为准）")});
            }
            if(category=="rod" || category=="owned")foreach(var rod in rods)
            {
                if(rod==null)continue;var owned=gear!=null&&(JournalV2CatalogUi.Has(gear.ownedRods,rod.id)||gear.equippedRod==rod.id);if(category=="owned"&&!owned)continue;
                var def=DesktopGameData.GetRod(rod.id);var price=def?.priceGold??rod.price;var equipped=gear?.equippedRod==rod.id;
                rows.Add(new Row{Id=rod.id,Kind="rod",Name=rod.name,Art=JournalV2CatalogUi.Product("rod",rod.id),Owned=owned,Equipped=equipped,Available=owned,Price=price,Meta=equipped?"使用中":owned?"已拥有":mode=="equip"?"商店购买":price>0?DesktopLocalization.Concat(price, " 金币"):"新手赠送",Facts=RodFacts(rod,def)});
            }
            if(category=="bait" || category=="owned")foreach(var bait in baits)
            {
                if(bait==null)continue;var def=DesktopGameData.GetBait(bait.id);var level=def?.unlockPlayerLevel??bait.unlockPlayerLevel;var cost=def?.costGoldPerUse??bait.costGoldPerUse;var available=gear!=null&&((def?.isDefaultInfinite??bait.isDefaultInfinite)||JournalV2CatalogUi.Has(gear.unlockedBaits,bait.id)||gear.playerLevel>=level);
                if((category=="owned" || mode=="equip")&&!available)continue;
                rows.Add(new Row{Id=bait.id,Kind="bait",Name=bait.name,Art=JournalV2CatalogUi.Product("bait",bait.id),Owned=available,Available=available,Equipped=gear?.equippedBait==bait.id,Price=cost,Meta=available?(cost==0?"无限使用":DesktopLocalization.Concat(cost, " 金币 / 次")):DesktopLocalization.Concat(DesktopLocalization.Concat("Lv.", level), " 解锁"),Facts=DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat("食性：", DesktopGameData.DietLabel(def?.diet??bait.diet)), "\n解锁等级：Lv."), level), "\n每次咬钩成功扣金："), cost), "\n草食 / 杂食 / 肉食加成："), DesktopGameData.FormatPct(def?.biteBonusHerbivore??bait.biteBonusHerbivore)), " / "), DesktopGameData.FormatPct(def?.biteBonusOmnivore??bait.biteBonusOmnivore)), " / "), DesktopGameData.FormatPct(def?.biteBonusCarnivore??bait.biteBonusCarnivore)), "\n鱼饵无库存，无需购买。")});
            }
            if(category=="vessel" || category=="owned")foreach(var vessel in vessels)
            {
                if(vessel==null)continue;var owned=gear!=null&&JournalV2CatalogUi.Has(gear.ownedVessels,vessel.vesselId);if(category=="owned"&&!owned)continue;
                rows.Add(new Row{Id=vessel.vesselId,Kind="vessel",Name=vessel.name,Art=JournalV2CatalogUi.Product("vessel",vessel.vesselId),Owned=owned,Available=gear!=null&&gear.playerLevel>=vessel.unlockPlayerLevel,Price=vessel.priceGold,Meta=owned?"已拥有":DesktopLocalization.Concat(vessel.priceGold, " 金币"),Facts=DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat("解锁等级：Lv.", vessel.unlockPlayerLevel), "\n价格："), vessel.priceGold), " 金币\n船具功能暂未开放，购入后暂不可使用。")});
            }
        }
        static string RodFacts(ShopTackleDto rod,DesktopGameData.RodDef def)
        {
            var text=new StringBuilder();text.Append("类型：").Append(def?.subType??rod.subType).Append("\n咬钩加成：").Append(DesktopGameData.FormatPct(def?.biteBonus??rod.biteBonus)).Append("\n防脱加成：").Append(DesktopGameData.FormatPct(def?.escapeReduction??rod.escapeReduction));
            text.Append("\n超规格阈值：").Append((def?.breakSizeM??rod.breakSizeM).ToString("0.##")).Append(" m\n超规格上限：").Append(def?.breakMaxLandings??rod.breakMaxLandings).Append(" 次，满次后损坏");
            text.Append("\n品质适配 灰/绿/蓝/紫/红/橙/金\n").Append(DesktopGameData.FormatMul(def?.fitGray??rod.fitGray)).Append(" / ").Append(DesktopGameData.FormatMul(def?.fitGreen??rod.fitGreen)).Append(" / ").Append(DesktopGameData.FormatMul(def?.fitBlue??rod.fitBlue)).Append(" / ").Append(DesktopGameData.FormatMul(def?.fitPurple??rod.fitPurple)).Append(" / ").Append(DesktopGameData.FormatMul(def?.fitRed??rod.fitRed)).Append(" / ").Append(DesktopGameData.FormatMul(def?.fitOrange??rod.fitOrange)).Append(" / ").Append(DesktopGameData.FormatMul(def?.fitGold??rod.fitGold));return text.ToString();
        }
        Row FindSelected()=>rows.Find(row=>row.Id==selected);
        void Render()
        {
            if(grid==null)return;BuildRows();JournalV2CatalogUi.Clear(grid.content);cards.Clear();
            if(FindSelected()==null)selected=rows.Count>0?rows[0].Id:null;
            for(var i=0;i<tabs.Length;i++){tabs[i].GetComponent<JournalV2Button>()?.SetSelected(tab==i);JournalV2CatalogUi.Enabled(tabs[i],!busy&&!loading);}
            var size=mode=="bag"?new Vector2(157,192):new Vector2(215.3f,192);
            for(var i=0;i<rows.Count;i++)
            { var row=rows[i];var card=JournalV2CatalogUi.Card(grid.content,row.Id,row.Name,row.Meta,row.Art,size,i);cards[row.Id]=card;card.onClick.AddListener(()=>Select(row.Id));JournalV2CatalogUi.Enabled(card,!busy&&!loading); }
            if(Page!=null)DesktopLocalization.SetText(Page.Heading, DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat((Category=="fish"?"这次的收获":Category=="bait"?"垂钓补给":Category=="rod"?"顺手的竿":Category=="owned"?"随身物品":"出海装备"), "    "), rows.Count), " 件"));
            if(rows.Count==0)JournalV2Nodes.Text(grid.content,"Empty",loading?"正在加载…":hasData?"这里暂时空空的。":"等待同步数据",0,0,620,100,22,TextAnchor.MiddleCenter);
            RenderSelection(false);
        }
        void RenderSelection(bool reveal)
        {
            var row=FindSelected();foreach(var pair in cards)pair.Value.GetComponent<JournalV2Button>()?.SetSelected(pair.Key==selected);
            JournalV2CatalogUi.Photo(photo,row?.Art,row?.Name??"选择一件物品",reveal);DesktopLocalization.SetText(facts, row?.Facts??(loading?"正在同步，请稍候。":"选择左侧项目查看详情。"));
            var canAct=row!=null&&!loading&&!busy&&gear!=null;
            var label="选择物品";
            if(row!=null)
            {
                if(row.Kind=="fish")label="出售";
                else if(row.Kind=="vessel"){label=row.Owned?"已拥有 · 暂不可用":row.Available?DesktopLocalization.Concat(DesktopLocalization.Concat("购买 · ", row.Price), " 金币"):"等级不足";canAct &= mode=="shop"&&!row.Owned&&row.Available&&coins>=row.Price;}
                else if(row.Kind=="bait"){label=row.Equipped?"使用中":row.Available?"设为当前鱼饵":"尚未解锁";canAct &= row.Available&&!row.Equipped;}
                else {label=row.Equipped?"使用中":row.Owned?mode=="shop"?"已拥有":"装备这根钓竿":mode=="shop"?DesktopLocalization.Concat(DesktopLocalization.Concat("购买 · ", row.Price), " 金币"):"前往商店";canAct &= !row.Equipped&&!(mode=="shop"&&row.Owned)&&(row.Owned||mode!="shop"||(row.Price>0&&coins>=row.Price));}
            }
            JournalV2CatalogUi.Label(action,busy?"正在处理…":label);JournalV2CatalogUi.Enabled(action,canAct);
            JournalV2CatalogUi.Label(secondary,mode=="bag"&&tab==0?"回鱼":"去商店");JournalV2CatalogUi.Label(tertiary,mode=="bag"&&tab==0?"分享鱼获":"去钓鱼");
            JournalV2CatalogUi.Enabled(secondary,!busy&&!loading&&(mode!="bag"||tab!=0||CanReturn(row)));JournalV2CatalogUi.Enabled(tertiary,!busy&&!loading&&(mode!="bag"||tab!=0||row!=null));
        }
        bool CanReturn(Row row)
        {
            var pond=App?.Pond;var feeMode=pond?.CurrentUser?.returnFeeMode;
            return row?.Fish!=null&&pond!=null&&pond.HasSpot&&feeMode!="sell_only"&&feeMode!="auto_return"&&DesktopGameData.IsReturnEligible(row.Fish.quality,row.Fish.sizeM);
        }
        void Act()
        {
            var row=FindSelected();if(row==null||busy||loading||App?.Api==null||!App.Api.CanUse)return;
            if(row.Kind=="rod"&&!row.Owned&&mode!="shop"){App.Navigate("shop",1,row.Id);return;}
            busy=true;RenderSelection(false);App.Run(Mutate(row,false));
        }
        void ReturnFish(){var row=FindSelected();if(busy||loading||!CanReturn(row)||App?.Api==null||!App.Api.CanUse)return;busy=true;RenderSelection(false);App.Run(Mutate(row,true));}
        IEnumerator Mutate(Row row,bool returning)
        {
            bool success=false;string message=null;
            try
            {
                if(row.Kind=="fish")
                {
                    if(returning)yield return App.Api.ReturnFishToPond(row.Id,(ok,gold,xp,pondXp,newSize,gain,total,items,error)=>{success=ok;message=ok?DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat("回鱼成功 · +", gold), " 金币 / +"), xp), " 经验"):error;if(ok){inventory=items??new FishInventoryItemDto[0];coins=total;}});
                    else yield return App.Api.SellFish(row.Id,(ok,earned,total,items,error)=>{success=ok;message=ok?DesktopLocalization.Concat(DesktopLocalization.Concat("出售成功 · +", earned), " 金币"):error;if(ok){inventory=items??new FishInventoryItemDto[0];coins=total;}});
                }
                else if(row.Kind=="rod"&&!row.Owned)yield return App.Api.BuyTackle(row.Id,(ok,value,total,error)=>{success=ok;message=error;if(ok){gear=value;coins=total;}});
                else if(row.Kind=="vessel")yield return App.Api.BuyVessel(row.Id,(ok,value,total,error)=>{success=ok;message=error;if(ok){gear=value;coins=total;}});
                else
                { Action<bool,ShopGearDto,string> callback=(ok,value,error)=>{success=ok;message=error;if(ok)gear=value;};yield return row.Kind=="bait"?App.Api.EquipBait(row.Id,callback):App.Api.EquipTackle(row.Id,callback); }
            }
            finally {busy=false;}
            if(this==null)yield break;
            if(success)App.RefreshHeader();var result=string.IsNullOrEmpty(message)?success?"操作成功，已同步最新状态。":"操作失败，请重试。":message;
            App.Toast(result);if(opened){Render();Status(result);if(!success)DesktopLocalization.SetText(facts, DesktopLocalization.Concat(DesktopLocalization.Concat(result, "\n\n"), (FindSelected()?.Facts??"")));}
        }
    }
}
