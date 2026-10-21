using System;
using System.Collections;
using FishSocial.Desktop.Auth;
using UnityEngine;
using UnityEngine.UI;
namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2EditController : JournalV2CommunityController
    {
        [SerializeField] InputField nickname,bio;[SerializeField] Button[] avatars;[SerializeField] string[] ids;
        [SerializeField] Button save,cancel;[SerializeField] RectTransform photo;[SerializeField] Text preview;
        string avatarPath;bool loaded;
        public void Initialize(JournalV2Page page,InputField nickname,InputField bio,Button[] avatars,string[] ids,Button save,Button cancel,RectTransform photo,Text preview)
        {Page=page;this.nickname=nickname;this.bio=bio;this.avatars=avatars;this.ids=ids;this.save=save;this.cancel=cancel;this.photo=photo;this.preview=preview;nickname.characterLimit=12;bio.characterLimit=120;}
        public override void Bind(JournalV2App app,JournalV2Page page)
        {base.Bind(app,page);nickname.onValueChanged.AddListener(_=>Refresh());bio.onValueChanged.AddListener(_=>Refresh());for(var i=0;i<avatars.Length;i++){var index=i;Action(avatars[i],()=>{avatarPath="/image/profile/cat_avatar_"+ids[index]+".png";Refresh();});}Action(save,()=>StartCoroutine(Save(Epoch)));Action(cancel,()=>App.Navigate("profile"));}
        public override void Open(int tab=0,string targetId=null)
        {Begin();loaded=false;Interactable(save,false);if(!Ready){Status("登录后编辑资料。");return;}StartCoroutine(Load(Epoch));}
        IEnumerator Load(int epoch){yield return App.Api.GetPlayerProfile((ok,p,error)=>{if(epoch!=Epoch)return;if(ok&&p!=null){PresentSnapshot(p);Status("");}else Status(error);});}
        public void PresentSnapshot(PlayerProfileDto p){if(p==null)return;loaded=true;nickname.SetTextWithoutNotify(p.nickname??"");bio.SetTextWithoutNotify(p.bio??"");avatarPath=p.avatarUrl;Refresh();}
        void Refresh(){JournalV2Pages.CommunityPhoto(photo,JournalV2Pages.CommunityAvatar(avatarPath),string.IsNullOrWhiteSpace(nickname.text)?"你的昵称":nickname.text);preview.text=string.IsNullOrWhiteSpace(bio.text)?"写下你的钓鱼心情吧。":bio.text;var selected=DesktopDefaultAvatars.ResolvePetId(avatarPath,App?.Api?.PlayerId);for(var i=0;i<avatars.Length;i++)Select(avatars[i],ids[i]==selected);Interactable(save,loaded&&!Busy&&!string.IsNullOrWhiteSpace(nickname.text));}
        IEnumerator Save(int epoch)
        {if(!Ready||Busy||!loaded||string.IsNullOrWhiteSpace(nickname.text))yield break;Busy=true;Refresh();Status("正在保存…");yield return App.Api.UpdatePlayerProfile(nickname.text.Trim(),bio.text.Trim(),avatarPath,(ok,p,error)=>{if(epoch!=Epoch)return;Busy=false;Refresh();if(!ok){Status(error);return;}App.Pond?.ApplyGameNickname(p.nickname);App.Pond?.ApplyGameAvatarUrl(p.avatarUrl);App.RefreshHeader();App.Toast("资料已保存");App.Navigate("profile");});}
    }
}
