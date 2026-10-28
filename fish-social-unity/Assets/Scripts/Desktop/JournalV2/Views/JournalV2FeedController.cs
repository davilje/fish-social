using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishSocial.Desktop.Auth;
using UnityEngine;
using UnityEngine.UI;
namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2FeedController : JournalV2CommunityController
    {
        const int Batch=20;
        [SerializeField] Button[] tabs;[SerializeField] ScrollRect posts,comments;[SerializeField] Text heading,note;[SerializeField] InputField input;[SerializeField] Button send,compose,loadMore;
        readonly List<SocialPostDto> data=new List<SocialPostDto>();readonly Dictionary<Button,SocialPostDto> cards=new Dictionary<Button,SocialPostDto>();readonly HashSet<string> likeBusy=new HashSet<string>();
        int tab,commentGeneration;SocialPostDto selected;bool loading,hasMore;
        public void Initialize(JournalV2Page page,Button[] tabs,ScrollRect posts,Text heading,ScrollRect comments,InputField input,Button send,Text note,Button compose,Button loadMore){Page=page;this.tabs=tabs;this.posts=posts;this.heading=heading;this.comments=comments;this.input=input;this.send=send;this.note=note;this.compose=compose;this.loadMore=loadMore;}
        public override void Bind(JournalV2App app,JournalV2Page page){base.Bind(app,page);for(var i=0;i<tabs.Length;i++){var index=i;Action(tabs[i],()=>Open(index));}Action(compose,()=>App.ShowModal("compose"));Action(send,()=>StartCoroutine(Comment(Epoch)));Action(loadMore,()=>StartCoroutine(Load(Epoch,true)));input.onValueChanged.AddListener(_=>RefreshSend());}
        public override void Open(int selectedTab=0,string targetId=null)
        {Begin();tab=Mathf.Clamp(selectedTab,0,2);data.Clear();selected=null;likeBusy.Clear();loading=false;DesktopLocalization.SetText(input, "");RefreshSend();JournalV2Pages.CommunityClear(posts.content);JournalV2Pages.CommunityClear(comments.content);for(var i=0;i<tabs.Length;i++)Select(tabs[i],i==tab);DesktopLocalization.SetText(Page.Heading, new[]{"钓友广场","好友的钓鱼日常","我的钓鱼动态"}[tab]);if(!Ready){Status("登录后查看动态。");return;}StartCoroutine(Load(Epoch,false));}
        IEnumerator Load(int epoch,bool append)
        {
            if(!Ready||loading)yield break;loading=true;Interactable(loadMore,false);Status("正在读取动态…");
            Action<bool,SocialPostDto[],string> done=(ok,items,error)=>{if(epoch!=Epoch)return;loading=false;if(!ok){Status(error);Interactable(loadMore,true);return;}if(!append)data.Clear();items=items??Array.Empty<SocialPostDto>();hasMore=items.Length==Batch;data.AddRange(items.Where(p=>!data.Any(x=>x.id==p.id)));Render();Status(data.Count==0?"这里还没有动态，晒晒你的第一条鱼获吧。":"");};
            if(tab==2)yield return App.Api.GetMySocialFeed(Batch,append?data.Count:0,done);else yield return App.Api.GetSocialFeed(tab==1,Batch,append?data.Count:0,done);
        }
        public void PresentSnapshot(SocialPostDto[] items,int selectedTab=0){data.Clear();data.AddRange(items??Array.Empty<SocialPostDto>());tab=Mathf.Clamp(selectedTab,0,2);hasMore=false;Render();}
        void Render()
        {
            var previous=selected?.id;cards.Clear();JournalV2Pages.CommunityClear(posts.content);
            for(var i=0;i<data.Count;i++)
            {
                var p=data[i];var c=JournalV2Nodes.Button(posts.content,DesktopLocalization.Concat("Post", i),"",null,0,0,682,316,JournalV2ButtonKind.Sticker);Height(c.gameObject,316);var v=JournalV2Pages.CommunityVisual(c);
                var author=JournalV2Nodes.Button(v,"Author","",null,18,12,360,50);var av=JournalV2Pages.CommunityVisual(author);av.Find("Plate").gameObject.SetActive(false);JournalV2Nodes.Image(av,"Portrait",JournalV2Pages.CommunityAvatar(p.authorAvatarUrl,p.playerId),0,0,46,46);JournalV2Nodes.Text(av,"Name",p.nickname,60,0,282,26,20);JournalV2Nodes.Text(av,"Date",Date(p.createdAt),60,27,282,22,13).color=JournalV2Theme.Soft;Action(author,()=>App?.Navigate("profile",0,p.playerId));
                JournalV2Nodes.Text(v,"Content",p.text??"",22,76,638,51,17);
                JournalV2Nodes.Plate(v,"FishWell",22,138,638,112,JournalV2Theme.ArtWell,12,0).ShadowColor=Color.clear;
                JournalV2Nodes.Image(v,"Fish",p.fish==null?null:JournalV2Pages.CommunityFish(p.fish.speciesId),34,148,210,92);
                JournalV2Nodes.Text(v,"FishInfo",p.fish==null?"":DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopLocalization.Concat(DesktopFishCatalog.SpeciesName(p.fish.speciesId), "\n"), p.fish.sizeM.ToString("0.00")), " m · "), DesktopFishCatalog.QualityName(p.fish.quality)),274,158,350,72,20);
                var like=JournalV2Nodes.Button(v,"Like",LikeText(p),"heart",22,264,152,38);Select(like,p.likedByMe);Action(like,()=>StartCoroutine(Like(p,like,Epoch)));
                var comment=JournalV2Nodes.Button(v,"Comment",DesktopLocalization.Concat(p.commentCount, " 条留言"),"message",188,264,182,38);Action(comment,()=>SelectPost(p));
                cards[c]=p;Action(c,()=>SelectPost(p));CardMotion(c,p.id,i,JournalV2ButtonKind.Sticker,0);
            }
            loadMore.gameObject.SetActive(hasMore);Interactable(loadMore,!loading);var first=data.FirstOrDefault(p=>p.id==previous)??data.FirstOrDefault();if(first!=null)SelectPost(first);else{selected=null;RefreshSend();}
        }
        static string LikeText(SocialPostDto p)=>(p.likedByMe?"已赞 ":"点赞 ")+p.likeCount;
        static string Date(long value){try{return DateTimeOffset.FromUnixTimeMilliseconds(value).LocalDateTime.ToString("MM.dd HH:mm");}catch{return "";}}
        public void SelectPost(SocialPostDto post){selected=post;foreach(var pair in cards)Select(pair.Key,pair.Value.id==post.id);DesktopLocalization.SetText(heading, DesktopLocalization.Concat(post.nickname, "的留言板"));DesktopLocalization.SetText(input, "");RefreshSend();JournalV2Pages.CommunityClear(comments.content);DesktopLocalization.SetText(note, "");if(Ready)StartCoroutine(LoadComments(post.id,Epoch,++commentGeneration));}
        IEnumerator LoadComments(string post,int epoch,int generation){DesktopLocalization.SetText(note, "正在读取留言…");yield return App.Api.GetPostComments(post,(ok,items,count,error)=>{if(epoch!=Epoch||generation!=commentGeneration||selected?.id!=post)return;if(!ok){DesktopLocalization.SetText(note, error);return;}selected.commentCount=count;PresentComments(items);DesktopLocalization.SetText(note, count==0?"还没有留言，来打个招呼吧。":"");});}
        public void PresentComments(PostCommentDto[] entries)
        {
            JournalV2Pages.CommunityClear(comments.content);
            foreach(var item in entries??Array.Empty<PostCommentDto>())
            {var entry=item;var row=JournalV2Nodes.Rect(comments.content,DesktopLocalization.Concat("Comment", item.id),0,0,306,102);Height(row.gameObject,102);JournalV2Nodes.Text(row,"Author",item.nickname,4,0,236,24,16);JournalV2Nodes.Text(row,"Text",item.text,4,28,294,62,15,TextAnchor.UpperLeft);if(Ready&&item.playerId==App.Api.PlayerId){var del=JournalV2Nodes.Button(row,"Delete","删除",null,248,0,54,25);Action(del,()=>StartCoroutine(DeleteComment(entry,Epoch)));}}
            comments.verticalNormalizedPosition=1;
        }
        void RefreshSend(){Interactable(send,selected!=null&&!Busy&&!string.IsNullOrWhiteSpace(input.text));input.interactable=selected!=null&&!Busy;}
        IEnumerator Like(SocialPostDto p,Button button,int epoch)
        {if(!Ready||!likeBusy.Add(p.id))yield break;Interactable(button,false);yield return App.Api.TogglePostLike(p.id,(ok,liked,count,error)=>{if(epoch!=Epoch)return;likeBusy.Remove(p.id);Interactable(button,true);if(!ok){Status(error);return;}p.likedByMe=liked;p.likeCount=count;JournalV2Pages.CommunityLabel(button,LikeText(p));Select(button,liked);button.GetComponent<JournalV2Button>()?.PulseLike();});}
        IEnumerator Comment(int epoch)
        {if(!Ready||Busy||selected==null||string.IsNullOrWhiteSpace(input.text))yield break;Busy=true;RefreshSend();var id=selected.id;yield return App.Api.AddPostComment(id,input.text.Trim(),(ok,item,count,error)=>{if(epoch!=Epoch)return;Busy=false;RefreshSend();if(!ok){DesktopLocalization.SetText(note, error);return;}if(selected?.id==id){DesktopLocalization.SetText(input, "");selected.commentCount=count;StartCoroutine(LoadComments(id,epoch,++commentGeneration));}App.Toast("留言已发送");});}
        IEnumerator DeleteComment(PostCommentDto entry,int epoch){if(!Ready||Busy)yield break;Busy=true;RefreshSend();yield return App.Api.DeletePostComment(entry.postId,entry.id,(ok,count,error)=>{if(epoch!=Epoch)return;Busy=false;RefreshSend();if(!ok){DesktopLocalization.SetText(note, error);return;}if(selected?.id==entry.postId){selected.commentCount=count;StartCoroutine(LoadComments(entry.postId,epoch,++commentGeneration));}});}
    }
}
