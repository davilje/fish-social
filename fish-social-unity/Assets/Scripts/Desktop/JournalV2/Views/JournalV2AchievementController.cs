using System;
using System.Collections;
using System.Linq;
using FishSocial.Desktop.Auth;
using UnityEngine;
using UnityEngine.UI;
namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2AchievementController : JournalV2CommunityController
    {
        [SerializeField] Button[] tabs;[SerializeField] ScrollRect list;[SerializeField] RectTransform photo;
        AchievementViewDto[] data=Array.Empty<AchievementViewDto>();int tab;
        public void Initialize(JournalV2Page page,Button[] tabs,ScrollRect list,RectTransform photo){Page=page;this.tabs=tabs;this.list=list;this.photo=photo;}
        public override void Bind(JournalV2App app,JournalV2Page page){base.Bind(app,page);for(var i=0;i<tabs.Length;i++){var index=i;Action(tabs[i],()=>{tab=index;Render();});}}
        public override void Open(int selectedTab=0,string targetId=null){Begin();tab=Mathf.Clamp(selectedTab,0,1);if(!Ready){Status("登录后查看成就。");return;}StartCoroutine(Load(Epoch));}
        IEnumerator Load(int epoch){yield return App.Api.GetProfileHub(App.Api.PlayerId,"achievements",(ok,hub,error)=>{if(epoch!=Epoch)return;if(ok&&hub!=null){PresentSnapshot(hub.achievements,tab);Status("");}else Status(error);});}
        public void PresentSnapshot(AchievementViewDto[] achievements,int selectedTab=0){data=achievements??Array.Empty<AchievementViewDto>();tab=Mathf.Clamp(selectedTab,0,1);Render();}
        void Render(){for(var i=0;i<tabs.Length;i++)Select(tabs[i],i==tab);Page.Heading.text=tab==0?"全部成就":"已完成的成就";RenderCards(list,data,tab==1,null);JournalV2Pages.CommunityPhoto(photo,JournalV2Theme.Sprite("icons/trophy"),data.Count(a=>a.unlocked)+" / "+data.Length);}
        internal static void RenderCards(ScrollRect list,AchievementViewDto[] data,bool unlockedOnly,Action<AchievementViewDto> selected)
        {
            JournalV2Pages.CommunityClear(list.content);var index=0;
            foreach(var a in (data??Array.Empty<AchievementViewDto>()).Where(a=>(!a.isHidden||a.unlocked)&&(!unlockedOnly||a.unlocked)).OrderBy(a=>a.sortOrder))
            {var achievement=a;var c=JournalV2Nodes.Button(list.content,"Achievement"+a.achievementId,"",null,0,0,215,250,JournalV2ButtonKind.Sticker);var v=JournalV2Pages.CommunityVisual(c);JournalV2Nodes.Image(v,"Medal",JournalV2Theme.Sprite("icons/"+(string.IsNullOrEmpty(a.iconKey)?"medal":a.iconKey)),71,20,72,72);JournalV2Nodes.Text(v,"Name",a.name,12,106,191,34,20,TextAnchor.MiddleCenter);JournalV2Nodes.Text(v,"Description",a.desc,16,146,183,56,15,TextAnchor.MiddleCenter);JournalV2Nodes.Text(v,"State",a.unlocked?"✓ 已解锁":"尚未解锁",16,211,183,28,18,TextAnchor.MiddleCenter).color=a.unlocked?JournalV2Theme.Coral:JournalV2Theme.Soft;Action(c,()=>selected?.Invoke(achievement));CardMotion(c,a.achievementId,index++,JournalV2ButtonKind.Sticker);}
            list.verticalNormalizedPosition=1;
        }
    }
}
