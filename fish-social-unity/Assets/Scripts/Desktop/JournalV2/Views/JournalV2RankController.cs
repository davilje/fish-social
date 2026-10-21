using System;
using System.Collections;
using System.Collections.Generic;
using FishSocial.Desktop.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2RankController : JournalV2CommunityController
    {
        [SerializeField] Button[] tabs;
        [SerializeField] ScrollRect scroll;
        [SerializeField] Text heading,bio,score;
        [SerializeField] RectTransform photo;
        [SerializeField] Button open;
        readonly Dictionary<Button,LeaderboardEntryDto> cards=new Dictionary<Button,LeaderboardEntryDto>();
        readonly string[] boards={"daily_biggest","weekly_king","pond","rare"};
        readonly string[] titles={"今日大鱼","每周钓王","当前鱼塘","稀有鱼获"};
        LeaderboardEntryDto[] entries=Array.Empty<LeaderboardEntryDto>();
        string selected; int tab;
        public void Initialize(JournalV2Page page,Button[] tabs,ScrollRect scroll,Text heading,RectTransform photo,Text bio,Text score,Button open)
        {Page=page;this.tabs=tabs;this.scroll=scroll;this.heading=heading;this.photo=photo;this.bio=bio;this.score=score;this.open=open;}
        public override void Bind(JournalV2App app,JournalV2Page page)
        {
            base.Bind(app,page);
            for(var i=0;i<tabs.Length;i++){var captured=i;Action(tabs[i],()=>Open(captured));}
            Action(open,()=>{if(!string.IsNullOrEmpty(selected))App.Navigate("profile",0,selected);});
        }
        public override void Open(int tab=0,string targetId=null)
        {
            Begin();this.tab=Mathf.Clamp(tab,0,3);selected=null;
            for(var i=0;i<tabs.Length;i++)Select(tabs[i],i==this.tab);
            Page.Heading.text=titles[this.tab];
            Interactable(open,false);
            if(!Ready){Status("登录后查看排行榜。");return;}
            StartCoroutine(Load(Epoch));
        }
        IEnumerator Load(int epoch)
        {
            Status("正在读取排行榜…");
            var board=boards[tab];var pond=App.Pond!=null?App.Pond.CurrentPondId:null;
            if(tab==2&&string.IsNullOrEmpty(pond)){Status("请先进入一个鱼塘。");yield break;}
            yield return App.Api.GetLeaderboard(board,pond,50,(ok,data,_,error)=>
            {
                if(epoch!=Epoch)return;
                if(!ok){Status(error??"榜单读取失败，请点击分类重试。");return;}
                PresentSnapshot(data,tab);
            });
            if(epoch!=Epoch)yield break;
            yield return App.Api.GetMyLeaderboardRank(board,pond,(ok,mine,error)=>
            {if(epoch!=Epoch)return;if(ok)Status(mine!=null&&mine.hasRank?"我的排名 #"+mine.rank+" · "+Format(mine.entry??new LeaderboardEntryDto{value=mine.value}):"尚未上榜，每一次抛竿都在刷新自己的纪录。");});
        }
        public void PresentSnapshot(LeaderboardEntryDto[] snapshot,int board=0)
        {
            tab=Mathf.Clamp(board,0,3);entries=snapshot??Array.Empty<LeaderboardEntryDto>();cards.Clear();
            JournalV2Pages.CommunityClear(scroll.content);Page.Heading.text=titles[tab];
            var podium=JournalV2Nodes.Rect(scroll.content,"Podium",0,0,680,220);Height(podium.gameObject,220);
            var order=new[]{2,1,3};
            for(var i=0;i<3;i++)
            {
                var rank=order[i];var item=Array.Find(entries,e=>e!=null&&e.rank==rank);if(item==null)continue;
                var first=rank==1;var card=JournalV2Nodes.Button(podium,"Place"+rank,"",null,7+i*229,first?12:39,208,first?192:165,JournalV2ButtonKind.Podium);
                var visual=JournalV2Pages.CommunityVisual(card);
                JournalV2Nodes.Text(visual,"Rank",rank==1?"♕":rank.ToString(),14,10,34,34,27,TextAnchor.MiddleCenter);
                JournalV2Nodes.Image(visual,"Avatar",JournalV2Pages.CommunityAvatar(item.avatarUrl,item.playerId),first?66:72,11,first?76:65,first?76:65);
                JournalV2Nodes.Text(visual,"Name",item.nickname,12,first?94:83,184,31,22,TextAnchor.MiddleCenter);
                JournalV2Nodes.Text(visual,"Value",Format(item),12,first?127:116,184,26,18,TextAnchor.MiddleCenter);
                JournalV2Nodes.Text(visual,"PlaceLabel","第"+rank+"名",34,first?172:145,140,30,13,TextAnchor.MiddleCenter);
                Register(card,item,i,JournalV2ButtonKind.Podium);
            }
            for(var i=0;i<entries.Length;i++)
            {
                var item=entries[i];if(item==null)continue;
                var card=JournalV2Nodes.Button(scroll.content,"Player"+i,"",null,0,0,668,75,JournalV2ButtonKind.RankRow);Height(card.gameObject,75);
                var visual=JournalV2Pages.CommunityVisual(card);
                JournalV2Nodes.Text(visual,"Rank",item.rank.ToString("00"),18,7,38,61,25,TextAnchor.MiddleCenter);
                JournalV2Nodes.Image(visual,"Avatar",JournalV2Pages.CommunityAvatar(item.avatarUrl,item.playerId),72,13,48,48);
                JournalV2Nodes.Text(visual,"Name",item.nickname,136,7,310,61,19);
                JournalV2Nodes.Text(visual,"Value",Format(item),462,7,188,61,20,TextAnchor.MiddleRight);
                Register(card,item,i+3,JournalV2ButtonKind.RankRow);
            }
            if(entries.Length>0)SelectPlayer(entries[0]);else{JournalV2Pages.CommunityPhoto(photo,null,"虚位以待");bio.text="当前榜单暂无数据。";score.text="";}
            Status(entries.Length==0?"当前榜单暂无数据。":"");scroll.verticalNormalizedPosition=1;
        }
        void Register(Button card,LeaderboardEntryDto entry,int ordinal,JournalV2ButtonKind kind)
        {cards[card]=entry;Action(card,()=>SelectPlayer(entry));CardMotion(card,entry.playerId,ordinal,kind,kind==JournalV2ButtonKind.RankRow?0:(float?)null);}
        public void SelectPlayer(LeaderboardEntryDto entry)
        {
            if(entry==null)return;selected=entry.playerId;
            foreach(var pair in cards)Select(pair.Key,pair.Value.playerId==selected);
            heading.text="钓友名片  #"+entry.rank;
            JournalV2Pages.CommunityPhoto(photo,JournalV2Pages.CommunityAvatar(entry.avatarUrl,selected),entry.nickname);
            score.text=titles[tab]+" · "+Format(entry);bio.text="正在读取钓友资料…";Interactable(open,App!=null);
            if(Ready)StartCoroutine(LoadPlayer(selected,Epoch));else bio.text="";
        }
        IEnumerator LoadPlayer(string player,int epoch)
        {yield return App.Api.GetPublicPlayerView(player,1,(ok,data,error)=>{if(epoch!=Epoch||selected!=player)return;bio.text=ok&&data?.profile!=null?data.profile.bio??"这位钓友还没有填写简介。":error??"资料暂不可用。";});}
        string Format(LeaderboardEntryDto entry)
        {if(entry==null)return"—";if(tab==0)return(entry.extra!=null&&entry.extra.sizeM>0?entry.extra.sizeM:entry.value).ToString("0.00")+" m";if(tab==1)return Mathf.RoundToInt(entry.value)+" 金币";return Mathf.RoundToInt(entry.value)+(tab==2?" 条":" 条史诗+");}
    }
}
