using System;
using System.Collections;
using System.Collections.Generic;
using FishSocial.Desktop.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2
{
    /// <summary>Independent presentation root. Existing authentication/game services are inputs, never view owners.</summary>
    public sealed class JournalV2App : MonoBehaviour
    {
        public const string ResourceRoot="Desktop/JournalV2/Prefabs/";
        public IAuthenticatedApiClient Api { get; private set; }
        public SocialPondSessionController Pond { get; private set; }
        public PondSelectionState PondSelection { get; }=new PondSelectionState();
        public string Nickname { get; private set; }="钓友";
        public string CurrentPage { get; private set; }="home";
        public bool PreviewMode { get; private set; }
        public Transform Stage => transform.Find("Stage");
        public event Action<string> RouteChanged;
        readonly Dictionary<string,JournalV2Page> pages=new Dictionary<string,JournalV2Page>();
        JournalV2Page current,modal,outgoing;
        int routeGeneration;
        SteamAuthController auth;
        Coroutine headerRoutine;
        public Coroutine Run(IEnumerator routine) {return routine==null?null:StartCoroutine(routine);}
        public void Bind(IAuthenticatedApiClient api,SocialPondSessionController pond,bool preview=false)
        {
            Api=api;Pond=pond;PreviewMode=preview;PondSelection.Bind(api);
            if(!preview) DesktopCatAudioFeedback.Bind(Stage.Find("CornerCat"));
            foreach(var route in new[]{"bag","equip","shop","ach","rank","profile","feed"})
            {
                var captured=route;BindButton("Stage/Nav/"+route,()=>Navigate(captured));
            }
            BindButton("Stage/Header/Profile",()=>Navigate("profile"));
            BindButton("Stage/Header/Coins",()=>Navigate("shop"));
            BindButton("Stage/Header/Catch",()=>Navigate("bag"));
            BindButton("Stage/Header/World",()=>Navigate("map"));
            BindButton("Stage/Header/Mail",()=>Navigate("friends"));
            BindButton("Stage/Header/Settings",()=>Navigate("settings"));
            BindButton("Stage/Login/Connect",()=>auth?.BeginLogin());
            Stage.Find("Login").gameObject.SetActive(!preview && (api==null||!api.CanUse));
            SetMainVisible(preview || (api!=null&&api.CanUse));
            if(!preview) RefreshHeader();
        }
        public void BindAuthentication(SteamAuthController controller)
        {
            if(auth!=null){auth.StateChanged-=OnAuth;auth.ErrorMessage-=Toast;}
            auth=controller;
            if(auth!=null){auth.StateChanged+=OnAuth;auth.ErrorMessage+=Toast;OnAuth(auth.State);}
        }
        void OnAuth(SteamLoginState state)
        {
            var logged=auth!=null&&auth.IsAuthenticated;
            Stage.Find("Login").gameObject.SetActive(!logged);SetMainVisible(logged);
            var status=Stage.Find("Login/Status").GetComponent<Text>();
            status.text=state==SteamLoginState.Failed?"登录失败，请检查 Steam 后重试。":logged?"":"通过 Steam 登录，继续你的钓鱼手账。";
            if(logged){WindowManager.Instance?.ApplyMainShell();RefreshHeader();}
        }
        void SetMainVisible(bool value)
        {
            foreach(var name in new[]{"Header","Nav","PageHost","CornerCat"})Stage.Find(name)?.gameObject.SetActive(value);
        }
        void BindButton(string path,UnityEngine.Events.UnityAction action)
        {var button=transform.Find(path)?.GetComponent<Button>();if(button==null)return;button.onClick.RemoveAllListeners();button.onClick.AddListener(action);}
        public void Navigate(string id,int tab=0,string targetId=null)
        {
            if(string.IsNullOrEmpty(id))return;
            if(modal!=null&&!modal.GetComponent<JournalV2Controller>().CanClose)return;
            if(current!=null&&!current.GetComponent<JournalV2Controller>().CanClose)return;
            if(id=="compose"||id=="player"||id=="picker"){ShowModal(id,tab,targetId);return;}
            CloseModal();
            var version=++routeGeneration;
            if(outgoing!=null){outgoing.GetComponent<JournalV2PageAnimator>()?.Cancel();outgoing.gameObject.SetActive(false);outgoing=null;}
            if(id=="home")
            {
                CurrentPage=id;UpdateNav();
                var old=current;current=null;outgoing=old;
                if(old!=null)
                {
                    old.GetComponent<JournalV2Controller>()?.Close();
                    old.GetComponent<JournalV2PageAnimator>().Close(()=>{if(old!=null)old.gameObject.SetActive(false);if(outgoing==old)outgoing=null;});
                }
                RouteChanged?.Invoke(id);return;
            }
            var next=GetPage(id,Stage.Find("PageHost"));
            if(next==null){Toast("页面资源尚未就绪："+id);return;}
            if(next==current && next.gameObject.activeSelf)
            {
                next.GetComponent<JournalV2Controller>()?.Open(tab,targetId);return;
            }
            var previous=current;current=next;CurrentPage=id;UpdateNav();
            Action enter=()=>
            {
                if(version!=routeGeneration)return;
                next.gameObject.SetActive(true);
                next.GetComponent<JournalV2Controller>()?.Open(tab,targetId);
                next.GetComponent<JournalV2PageAnimator>()?.Enter(previous==null);
                RouteChanged?.Invoke(id);
            };
            if(previous==null)enter();
            else
            {
                outgoing=previous;previous.GetComponent<JournalV2Controller>()?.Close();
                previous.GetComponent<JournalV2PageAnimator>().Exit(()=>
                {
                    if(previous!=null)previous.gameObject.SetActive(false);
                    if(outgoing==previous)outgoing=null;
                    enter();
                });
            }
        }
        JournalV2Page GetPage(string id,Transform host)
        {
            if(pages.TryGetValue(id,out var cached))return cached;
            var prefab=Resources.Load<GameObject>(ResourceRoot+id);
            if(prefab==null)return null;
            var instance=Instantiate(prefab,host,false);instance.name=id;
            var page=instance.GetComponent<JournalV2Page>();
            if(page==null){Destroy(instance);return null;}
            pages.Add(id,page);page.CloseButton?.onClick.AddListener(()=>Navigate("home"));
            instance.GetComponent<JournalV2Controller>()?.Bind(this,page);instance.SetActive(false);return page;
        }
        public void ShowModal(string id,int tab=0,string targetId=null)
        {
            if(modal!=null&&!modal.GetComponent<JournalV2Controller>().CanClose)return;
            CloseModal();var host=Stage.Find("ModalHost");host.gameObject.SetActive(true);
            modal=GetPage(id,host);
            if(modal==null){host.gameObject.SetActive(false);Toast("弹窗资源尚未就绪："+id);return;}
            modal.gameObject.SetActive(true);modal.CloseButton?.onClick.RemoveAllListeners();modal.CloseButton?.onClick.AddListener(CloseModal);
            modal.GetComponent<JournalV2Controller>()?.Open(tab,targetId);
            modal.GetComponent<JournalV2PageAnimator>()?.Enter(true);
        }
        public void CloseModal()
        {
            if(modal!=null&&!modal.GetComponent<JournalV2Controller>().CanClose)return;
            if(modal!=null){modal.GetComponent<JournalV2Controller>()?.Close();modal.gameObject.SetActive(false);modal=null;}
            Stage.Find("ModalHost")?.gameObject.SetActive(false);
        }
        void UpdateNav()
        {
            foreach(Transform item in Stage.Find("Nav"))item.GetComponent<JournalV2Button>()?.SetSelected(item.name==CurrentPage);
            var bobber=Stage.Find("Nav/Bobber")?.GetComponent<JournalV2Bobber>();
            var nav=Stage.Find("Nav/"+CurrentPage) as RectTransform;
            if(nav==null)bobber?.Hide();else bobber?.MoveTo(new Vector2(nav.anchoredPosition.x+nav.rect.width/2-20,0),nav.Find("Visual/Icon") as RectTransform);
        }
        public void RefreshHeader()
        {
            if(PreviewMode||Api==null||!Api.CanUse)return;
            if(headerRoutine!=null)StopCoroutine(headerRoutine);
            headerRoutine=StartCoroutine(LoadHeader());
        }
        IEnumerator LoadHeader()
        {
            yield return Api.GetCoins((ok,coins,error)=>{if(ok)SetHeader("Coins/Visual/Label",coins.ToString("N0"));});
            yield return Api.GetPlayerProfile((ok,profile,error)=>
            {
                if(!ok||profile==null)return;
                Nickname=profile.nickname??"钓友";SetHeader("Profile/Visual/Name",Nickname);
                var avatar=Stage.Find("Header/Profile/Visual/Avatar")?.GetComponent<Image>();
                if(avatar!=null)avatar.sprite=DesktopDefaultAvatars.Portrait(profile.avatarUrl,Api.PlayerId);
            });
            yield return Api.GetFishingProgress((ok,progress,error)=>
            {
                if(!ok||progress==null)return;
                SetHeader("Profile/Visual/Level","Lv."+progress.level);
                SetExperience(progress.xp,RequiredLevelXp(progress.level));
                PondSelection.ApplyProgress(progress);
            });
            yield return Api.GetInventoryItems((ok,items,error)=>{if(ok)SetHeader("Catch/Visual/Label",(items?.Length??0).ToString());});
            headerRoutine=null;
        }
        void SetHeader(string path,string value) {var label=Stage.Find("Header/"+path)?.GetComponent<Text>();if(label!=null)label.text=value;}
        public void PresentHeaderSnapshot(string nickname,int level,int xp,int required,int coins,int count,string avatar)
        {
            Nickname=nickname;SetHeader("Profile/Visual/Name",nickname);SetHeader("Profile/Visual/Level","Lv."+level);SetHeader("Coins/Visual/Label",coins.ToString("N0"));SetHeader("Catch/Visual/Label",count.ToString());
            Stage.Find("Header/Profile/Visual/Avatar").GetComponent<Image>().sprite=JournalV2Theme.Sprite(avatar);SetExperience(xp,required);LayoutProfileName();
        }
        void SetExperience(int xp,int required)
        {
            SetHeader("Profile/Visual/ExperienceLabel",required>0?xp+" / "+required:"满级");
            var fill=Stage.Find("Header/Profile/Visual/ExperienceFill") as RectTransform;
            if(fill!=null)fill.sizeDelta=new Vector2(285*(required>0?Mathf.Clamp01((float)xp/required):1),18);
            LayoutProfileName();
        }
        void LayoutProfileName()
        {
            var visual=Stage.Find("Header/Profile/Visual");var label=visual.Find("Name").GetComponent<Text>();
            var width=Mathf.Min(198,label.preferredWidth);label.rectTransform.sizeDelta=new Vector2(width,34);
            foreach(var name in new[]{"LevelBadge","Level"}){var r=(RectTransform)visual.Find(name);r.anchoredPosition=new Vector2(110+width+14,-24);}
        }
        [Serializable] sealed class LevelRow {public int level,xpToNext;}
        [Serializable] sealed class LevelTable {public LevelRow[] rows;}
        static int RequiredLevelXp(int level)
        {
            var asset=Resources.Load<TextAsset>("Desktop/Theme/Cozy/data/player-levels");if(asset==null)return 0;
            var table=JsonUtility.FromJson<LevelTable>("{\"rows\":"+asset.text+"}");
            if(table?.rows!=null)foreach(var row in table.rows)if(row.level==level)return row.xpToNext;return 0;
        }
        public void Toast(string message)
        {
            if(string.IsNullOrEmpty(message))return;
            Stage.Find("Toast")?.GetComponent<JournalV2Toast>()?.Show(message);
        }
        void Update()
        {
            if(Input.GetKeyDown(KeyCode.Escape)){if(modal!=null)CloseModal();else Navigate("home");}
        }
        void OnDestroy()
        {
            if(auth!=null){auth.StateChanged-=OnAuth;auth.ErrorMessage-=Toast;}
            foreach(var page in pages.Values)if(page!=null)page.GetComponent<JournalV2Controller>()?.Close();
        }
    }
}
