using System;
using System.Collections;
using System.Collections.Generic;
using FishSocial.Desktop.Auth;
using UnityEngine;
using UnityEngine.UI;
namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2ComposeController : JournalV2CommunityController
    {
        [SerializeField] RectTransform paper;[SerializeField] ScrollRect choices;[SerializeField] InputField content;[SerializeField] Dropdown visibility;[SerializeField] Button submit,cancel,dismiss;
        FishInventoryItemDto[] inventory=Array.Empty<FishInventoryItemDto>();string selected;readonly Dictionary<Button,string> cards=new Dictionary<Button,string>();
        public void Initialize(JournalV2Page page,RectTransform paper,ScrollRect choices,InputField content,Dropdown visibility,Button submit,Button cancel,Button dismiss){Page=page;this.paper=paper;this.choices=choices;this.content=content;this.visibility=visibility;this.submit=submit;this.cancel=cancel;this.dismiss=dismiss;}
        public override void Bind(JournalV2App app,JournalV2Page page){base.Bind(app,page);Action(cancel,()=>App.CloseModal());Action(dismiss,()=>App.CloseModal());Action(submit,()=>StartCoroutine(Publish(Epoch)));}
        public override void Open(int tab=0,string targetId=null)
        {Begin();GetComponent<JournalV2DialogMotion>()?.Open();selected=targetId;content.text="";visibility.value=0;Interactable(submit,false);if(!Ready){Status("登录后分享鱼获。");return;}StartCoroutine(Load(Epoch));}
        IEnumerator Load(int epoch){Status("正在打开鱼篓…");yield return App.Api.GetInventoryItems((ok,items,error)=>{if(epoch!=Epoch)return;if(ok){PresentSnapshot(items,selected);Status(inventory.Length==0?"鱼篓还是空的，先去钓一条鱼吧。":"");}else Status(error);});}
        public void PresentSnapshot(FishInventoryItemDto[] items,string fishId=null)
        {
            inventory=items??Array.Empty<FishInventoryItemDto>();selected=fishId;cards.Clear();JournalV2Pages.CommunityClear(choices.content);
            for(var i=0;i<inventory.Length;i++){var f=inventory[i];var c=JournalV2Nodes.Button(choices.content,"Fish"+i,"",null,0,0,190,176,JournalV2ButtonKind.Sticker);var v=JournalV2Pages.CommunityVisual(c);JournalV2Nodes.Image(v,"Fish",JournalV2Pages.CommunityFish(f.speciesId),16,12,158,95);JournalV2Nodes.Text(v,"Name",DesktopFishCatalog.SpeciesName(f.speciesId),8,112,174,26,18,TextAnchor.MiddleCenter);JournalV2Nodes.Text(v,"Size",f.sizeM.ToString("0.00")+" m · "+DesktopFishCatalog.QualityName(f.quality),8,142,174,23,14,TextAnchor.MiddleCenter);cards[c]=f.id;Action(c,()=>SelectFish(f.id));CardMotion(c,f.speciesId,i,JournalV2ButtonKind.Sticker);}
            if(Array.Find(inventory,f=>f.id==selected)==null)selected=inventory.Length==0?null:inventory[0].id;SelectFish(selected);
        }
        public void SelectFish(string id){selected=id;foreach(var pair in cards)Select(pair.Key,pair.Value==selected);Interactable(submit,!Busy&&!string.IsNullOrEmpty(selected));}
        IEnumerator Publish(int epoch)
        {
            if(!Ready||Busy||string.IsNullOrEmpty(selected))yield break;Busy=true;SelectFish(selected);content.interactable=false;visibility.interactable=false;Status("正在发布…");
            yield return App.Api.ShareFish(selected,App.Nickname,visibility.value==1?"friends":"public",content.text.Trim(),(ok,error)=>
            {if(epoch!=Epoch)return;Busy=false;content.interactable=true;visibility.interactable=true;SelectFish(selected);if(!ok){Status(error??"发布失败，请重试。");return;}App.Toast("鱼获动态已发布");App.CloseModal();App.Navigate("feed",2);});
        }
        public override void Close(){base.Close();content.interactable=true;visibility.interactable=true;}
    }
}
