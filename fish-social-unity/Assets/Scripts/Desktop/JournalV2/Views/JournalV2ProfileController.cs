using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishSocial.Desktop.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2ProfileController : JournalV2CommunityController
    {
        [SerializeField] Button[] tabs;
        [SerializeField] RectTransform overview,photo,picker;
        [SerializeField] Image avatar;
        [SerializeField] Text nickname,bio,xp,title,detail,pageNumber,pickerTitle;
        [SerializeField] Text[] stats;
        [SerializeField] Button edit,showcase,album,action,secondary,previous,next,closePicker;
        [SerializeField] ScrollRect list,pickerList;
        ProfileHubDto snapshot; FishCodexEntryDto[] codex=Array.Empty<FishCodexEntryDto>();
        int tab,albumPage,selectedSlot; string target; bool self;
        readonly List<Button> cards=new List<Button>();
        public void Initialize(JournalV2Page page,Button[] tabs,RectTransform overview,Image avatar,Text nickname,Text bio,Text[] stats,Text xp,Button edit,Button showcase,Button album,ScrollRect list,RectTransform photo,Text title,Text detail,Button action,Button secondary,Button previous,Button next,Text pageNumber,RectTransform picker,Text pickerTitle,ScrollRect pickerList,Button closePicker)
        { Page=page;this.tabs=tabs;this.overview=overview;this.avatar=avatar;this.nickname=nickname;this.bio=bio;this.stats=stats;this.xp=xp;this.edit=edit;this.showcase=showcase;this.album=album;this.list=list;this.photo=photo;this.title=title;this.detail=detail;this.action=action;this.secondary=secondary;this.previous=previous;this.next=next;this.pageNumber=pageNumber;this.picker=picker;this.pickerTitle=pickerTitle;this.pickerList=pickerList;this.closePicker=closePicker; }
        public override void Bind(JournalV2App app,JournalV2Page page)
        {
            base.Bind(app,page);for(var i=0;i<tabs.Length;i++){var index=i;Action(tabs[i],()=>SelectTab(index));}
            Action(edit,()=>App.Navigate("edit"));Action(showcase,()=>SelectTab(1));Action(album,()=>SelectTab(3));
            Action(previous,()=>{albumPage=Mathf.Max(0,albumPage-1);Render();});Action(next,()=>{albumPage++;Render();});Action(closePicker,()=>picker.gameObject.SetActive(false));
        }
        public override void Open(int selectedTab=0,string targetId=null)
        {
            Begin();target=targetId;tab=Mathf.Clamp(selectedTab,0,4);albumPage=0;picker.gameObject.SetActive(false);
            if(!Ready){Status("登录后查看钓鱼手账。");return;}StartCoroutine(Load(Epoch));
        }
        IEnumerator Load(int epoch)
        {
            Status("正在翻开手账…");
            yield return App.Api.GetProfileHub(target??App.Api.PlayerId,"all",(ok,data,error)=>{if(epoch!=Epoch)return;if(ok&&data!=null){snapshot=data;self=data.isSelf;Render();Status("");}else Status(error);});
            if(epoch!=Epoch||!self)yield break;
            yield return App.Api.GetCodex((ok,data,error)=>{if(epoch!=Epoch)return;if(ok){codex=data??Array.Empty<FishCodexEntryDto>();if(tab==2)Render();}});
        }
        public void PresentSnapshot(ProfileHubDto data,int selectedTab=0,FishCodexEntryDto[] entries=null)
        {snapshot=data;self=data!=null&&data.isSelf;tab=Mathf.Clamp(selectedTab,0,4);codex=entries??Array.Empty<FishCodexEntryDto>();Render();}
        public void SelectTab(int index)
        {tab=Mathf.Clamp(index,0,4);albumPage=0;picker.gameObject.SetActive(false);Render();}
        void Render()
        {
            if(snapshot?.profile==null)return;
            for(var i=0;i<tabs.Length;i++)Select(tabs[i],i==tab);
            Page.Heading.text=new[]{"我的钓鱼时光","展示柜","鱼类图鉴","钓鱼相册","我的成就"}[tab];
            overview.gameObject.SetActive(tab==0);list.gameObject.SetActive(tab!=0);secondary.gameObject.SetActive(false);action.gameObject.SetActive(true);
            previous.gameObject.SetActive(tab==3);next.gameObject.SetActive(tab==3);pageNumber.gameObject.SetActive(tab==3);
            cards.Clear();JournalV2Pages.CommunityClear(list.content);
            var grid=list.content.GetComponent<GridLayoutGroup>();grid.constraintCount=tab==3?2:3;grid.cellSize=tab==3?new Vector2(319,302):new Vector2(215,250);
            var p=snapshot.profile;avatar.sprite=JournalV2Pages.CommunityAvatar(p.avatarUrl,p.playerId);avatar.color=Color.white;nickname.text=p.nickname;bio.text=string.IsNullOrWhiteSpace(p.bio)?"这位钓友还没写下简介。":p.bio;
            stats[0].text="Lv."+(snapshot.progress?.level??1)+"\n钓鱼等级";stats[1].text=(snapshot.codexSummary?.unlockedCount??0)+" / "+(snapshot.codexSummary?.totalSpecies??0)+"\n图鉴收集";stats[2].text=(snapshot.achievements??Array.Empty<AchievementViewDto>()).Count(a=>a.unlocked)+"\n已解锁成就";
            xp.text="钓鱼经验  "+(snapshot.progress?.xp??0)+" XP\n每一次收获，都让钓鱼手账更丰富。";edit.gameObject.SetActive(self&&snapshot.canEdit);
            title.text=tab==0?"我的心头好":new[]{"","鱼获详情","图鉴详情","相册记忆","成就故事"}[tab];
            if(tab==0){var fish=snapshot.showcaseFish?.FirstOrDefault(f=>f!=null);ShowFish(fish);JournalV2Pages.CommunityLabel(action,self?"整理我的展示柜":"看看展示柜");Action(action,()=>SelectTab(1));}
            if(tab==1)
            {
                var ids=p.showcaseFishIds??Array.Empty<string>();
                for(var i=0;i<PlayerProfileDto.ShowcaseSlotCount;i++)
                {var slot=i;var f=i<ids.Length?snapshot.showcaseFish?.FirstOrDefault(x=>x!=null&&x.id==ids[i]):null;var c=FishCard(list.content,"Slot"+i,f,215,250,i,f==null?"放一条心爱的鱼":"");Action(c,()=>SelectSlot(slot,f));}
                var first=ids.Length>0?snapshot.showcaseFish?.FirstOrDefault(f=>f!=null&&f.id==ids[0]):null;SelectSlot(0,first);
            }
            if(tab==2)
            {
                action.gameObject.SetActive(false);
                foreach(var species in DesktopFishCatalog.Species)
                {var s=species;var entry=codex.FirstOrDefault(c=>c.speciesId==s.Id);var unlocked=entry!=null;var c=FishCard(list.content,"Codex"+s.Id,unlocked?new FishInventoryItemDto{speciesId=s.Id}:null,215,250,cards.Count,unlocked?s.Name:"未发现");Action(c,()=>{SelectCard(c);JournalV2Pages.CommunityPhoto(photo,unlocked?JournalV2Pages.CommunityFish(s.Id):null,unlocked?s.Name:"尚未发现");detail.text=unlocked?DesktopFishCatalog.FormatCodexProfile(s):"去不同的鱼塘，寻找新的伙伴吧。";action.gameObject.SetActive(!unlocked&&self);JournalV2Pages.CommunityLabel(action,"去探索鱼塘");Action(action,()=>App?.Navigate("map"));});}
                detail.text=self?"选一条鱼，翻看它的故事。":"其他钓友的完整图鉴暂不公开。";
                if(!self){JournalV2Pages.CommunityClear(list.content);cards.Clear();}
            }
            if(tab==3)
            {
                var entries=snapshot.albumPins??Array.Empty<AlbumCardDto>();var count=Mathf.Max(1,Mathf.CeilToInt(entries.Length/2f));albumPage=Mathf.Clamp(albumPage,0,count-1);pageNumber.text=(albumPage+1)+" / "+count;Interactable(previous,albumPage>0);Interactable(next,albumPage<count-1);action.gameObject.SetActive(false);
                foreach(var entry in entries.Skip(albumPage*2).Take(2)){var e=entry;var c=FishCard(list.content,"Album"+e.id,new FishInventoryItemDto{speciesId=e.speciesId,sizeM=e.sizeM,quality=e.quality},319,302,cards.Count,DesktopFishCatalog.SpeciesName(e.speciesId));Action(c,()=>{SelectCard(c);JournalV2Pages.CommunityPhoto(photo,JournalV2Pages.CommunityFish(e.speciesId),DesktopFishCatalog.SpeciesName(e.speciesId));detail.text=(e.pondName??e.pondId)+"\n"+e.sizeM.ToString("0.00")+" m · "+DesktopFishCatalog.QualityName(e.quality)+"\n"+Date(e.eventAt);});}
                detail.text=entries.Length==0?"值得纪念的收获会自动收进相册。":"选一张照片，回顾那天的收获。";
            }
            if(tab==4){action.gameObject.SetActive(false);JournalV2AchievementController.RenderCards(list,snapshot.achievements,false,a=>{JournalV2Pages.CommunityPhoto(photo,JournalV2Theme.Sprite("icons/medal"),a.name);detail.text=a.desc+"\n"+(a.unlocked?"已解锁":"尚未解锁");});}
            list.verticalNormalizedPosition=1;
        }
        static string Date(long timestamp){try{return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime.ToString("yyyy.MM.dd");}catch{return "";}}
        Button FishCard(Transform host,string id,FishInventoryItemDto fish,float width,float height,int index,string caption)
        {
            var c=JournalV2Nodes.Button(host,id,"",null,0,0,width,height,JournalV2ButtonKind.Sticker);var v=JournalV2Pages.CommunityVisual(c);
            JournalV2Nodes.Image(v,"Fish",fish==null?null:JournalV2Pages.CommunityFish(fish.speciesId),20,20,width-40,height-100);
            JournalV2Nodes.Text(v,"Name",string.IsNullOrEmpty(caption)?DesktopFishCatalog.SpeciesName(fish.speciesId):caption,12,height-75,width-24,32,20,TextAnchor.MiddleCenter);
            JournalV2Nodes.Text(v,"Info",fish!=null&&fish.sizeM>0?fish.sizeM.ToString("0.00")+" m · "+DesktopFishCatalog.QualityName(fish.quality):"",12,height-40,width-24,26,15,TextAnchor.MiddleCenter);
            cards.Add(c);CardMotion(c,fish?.speciesId??id,index,JournalV2ButtonKind.Sticker,tab==3?(index%2==0?-1.3f:1.3f):(float?)null);return c;
        }
        void SelectCard(Button selected){foreach(var c in cards)Select(c,c==selected);photo.GetComponent<JournalV2Polaroid>()?.PlayReveal();}
        void ShowFish(FishInventoryItemDto f){JournalV2Pages.CommunityPhoto(photo,f==null?null:JournalV2Pages.CommunityFish(f.speciesId),f==null?"等待新的收获":DesktopFishCatalog.SpeciesName(f.speciesId));detail.text=f==null?"把喜欢的鱼收进展示柜，记录属于你的高光时刻。":f.sizeM.ToString("0.00")+" m · "+DesktopFishCatalog.QualityName(f.quality)+"\n"+DesktopFishCatalog.FormatCodexProfile(DesktopFishCatalog.GetSpecies(f.speciesId));}
        void SelectSlot(int slot,FishInventoryItemDto fish){selectedSlot=slot;if(slot<cards.Count)SelectCard(cards[slot]);ShowFish(fish);action.gameObject.SetActive(self&&snapshot.canEdit);secondary.gameObject.SetActive(self&&snapshot.canEdit&&fish!=null);JournalV2Pages.CommunityLabel(action,fish==null?"选择鱼获":"替换这条鱼");Action(action,()=>StartCoroutine(LoadPicker(Epoch)));Action(secondary,()=>StartCoroutine(SaveSlot(null,Epoch)));}
        IEnumerator LoadPicker(int epoch)
        {
            if(!Ready||Busy)yield break;Busy=true;Status("正在读取鱼篓…");
            yield return App.Api.GetInventoryItems((ok,items,error)=>{if(epoch!=Epoch)return;Busy=false;if(!ok){Status(error);return;}Status("");picker.gameObject.SetActive(true);pickerTitle.text="选择第 "+(selectedSlot+1)+" 格的鱼获";JournalV2Pages.CommunityClear(pickerList.content);var count=0;foreach(var fish in items??Array.Empty<FishInventoryItemDto>()){var f=fish;var c=FishCard(pickerList.content,"Choice"+f.id,f,180,160,count++,"");Action(c,()=>StartCoroutine(SaveSlot(f.id,Epoch)));}if(count==0)pickerTitle.text="鱼篓还是空的，先去钓一条鱼吧。";});
        }
        IEnumerator SaveSlot(string fishId,int epoch)
        {
            if(!Ready||Busy)yield break;Busy=true;var ids=new string[PlayerProfileDto.ShowcaseSlotCount];Array.Copy(snapshot.profile.showcaseFishIds??Array.Empty<string>(),ids,Mathf.Min(ids.Length,snapshot.profile.showcaseFishIds?.Length??0));
            if(!string.IsNullOrEmpty(fishId))for(var i=0;i<ids.Length;i++)if(ids[i]==fishId)ids[i]=null;ids[selectedSlot]=fishId;
            yield return App.Api.SetShowcase(ids,(ok,result,error)=>{if(epoch!=Epoch)return;Busy=false;if(ok){picker.gameObject.SetActive(false);App.Toast("展示柜已保存");StartCoroutine(Load(epoch));}else Status(error);});
        }
    }
}
