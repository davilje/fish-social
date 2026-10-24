using FishSocial.Desktop.Onboarding;
using UnityEngine;
using UnityEngine.UI;
namespace FishSocial.Desktop.JournalV2
{
    public sealed class JournalV2SettingsController : JournalV2CommunityController
    {
        [SerializeField] Button[] tabs,presets,modes,notify;[SerializeField] RectTransform[] panels;[SerializeField] Button tray,hide,reset,quit;[SerializeField] Text heading,detail;[SerializeField] RectTransform photo;
        WindowSettings window;NotificationSettings notification;int tab;
        static readonly string[] labels={"启用桌面通知","免打扰","鱼咬钩提醒","好友邀请提醒","连接异常提醒"};
        public void Initialize(JournalV2Page page,Button[] tabs,RectTransform[] panels,Button[] presets,Button[] modes,Button[] notify,Button tray,Button hide,Button reset,Button quit,Text heading,RectTransform photo,Text detail)
        {Page=page;this.tabs=tabs;this.panels=panels;this.presets=presets;this.modes=modes;this.notify=notify;this.tray=tray;this.hide=hide;this.reset=reset;this.quit=quit;this.heading=heading;this.photo=photo;this.detail=detail;}
        public override void Bind(JournalV2App app,JournalV2Page page)
        {
            if(panels!=null&&panels.Length>2)DesktopAudioSettingsView.Ensure(panels[2], 248);
            base.Bind(app,page);for(var i=0;i<tabs.Length;i++){var index=i;Action(tabs[i],()=>{tab=index;Render();});}
            for(var i=0;i<presets.Length;i++){var index=i;Action(presets[i],()=>{var p=WindowSettings.Presets[index];WindowManager.Instance?.ApplyWindowedPreset(p.Width,p.Height);window=WindowManager.Instance?.Settings??window;Render();});}
            for(var i=0;i<modes.Length;i++){var index=i;Action(modes[i],()=>{WindowManager.Instance?.SetMode((WindowDisplayMode)index);window=WindowManager.Instance?.Settings??window;Render();});}
            for(var i=0;i<notify.Length;i++){var index=i;Action(notify[i],()=>ToggleNotification(index));}
            Action(tray,()=>{window.HideToTrayOnClose=!window.HideToTrayOnClose;window.Save();Render();});Action(hide,()=>WindowManager.Instance?.HideToTray());Action(reset,()=>{DesktopOnboardingController.Instance?.ResetAndRestartOnboarding();App.Navigate("home");});Action(quit,()=>DesktopAppBootstrap.Instance?.QuitForReal());
        }
        public override void Open(int selectedTab=0,string targetId=null)
        {Begin();tab=Mathf.Clamp(selectedTab,0,2);window=WindowManager.Instance?.Settings??WindowSettings.LoadOrDefault();notification=DesktopNotificationService.Instance?.Settings??NotificationSettings.LoadOrDefault();Render();}
        public void PresentSnapshot(WindowSettings windowSettings,NotificationSettings notificationSettings,int selectedTab=0){window=windowSettings??new WindowSettings();notification=notificationSettings??new NotificationSettings();tab=Mathf.Clamp(selectedTab,0,2);Render();}
        bool Value(int index){switch(index){case 0:return notification.EnableNotifications;case 1:return notification.DoNotDisturb;case 2:return notification.EnableFishBite;case 3:return notification.EnableFriendInvite;default:return notification.EnableConnectionError;}}
        void ToggleNotification(int index){switch(index){case 0:notification.EnableNotifications=!notification.EnableNotifications;break;case 1:notification.DoNotDisturb=!notification.DoNotDisturb;break;case 2:notification.EnableFishBite=!notification.EnableFishBite;break;case 3:notification.EnableFriendInvite=!notification.EnableFriendInvite;break;default:notification.EnableConnectionError=!notification.EnableConnectionError;break;}notification.Save();Render();}
        void Render()
        {
            if(window==null||notification==null)return;
            for(var i=0;i<tabs.Length;i++){Select(tabs[i],i==tab);panels[i].gameObject.SetActive(i==tab);}
            for(var i=0;i<presets.Length;i++){var chosen=WindowSettings.Presets[i].Width==window.Width&&WindowSettings.Presets[i].Height==window.Height;presets[i].transform.Find("Visual/Check").gameObject.SetActive(chosen);}
            for(var i=0;i<modes.Length;i++){var label=modes[i].transform.Find("Visual/Label").GetComponent<Text>();label.color=i==(int)window.Mode?Color.white:JournalV2Theme.Ink;}
            panels[0].Find("Modes").GetComponent<JournalV2Segment>().Select((int)window.Mode);
            for(var i=0;i<notify.Length;i++)notify[i].GetComponent<JournalV2Toggle>().SetValue(Value(i),Application.isPlaying,false);
            tray.GetComponent<JournalV2Toggle>().SetValue(window.HideToTrayOnClose,Application.isPlaying,false);
            Page.Heading.text=new[]{"窗口尺寸","提醒开关","系统托盘"}[tab];heading.text="当前设置";
            photo.Find("Resolution").GetComponent<Text>().text=window.Width+"×"+window.Height;
            var count=0;for(var i=0;i<5;i++)if(Value(i))count++;
            photo.Find("Mode").GetComponent<Text>().text=new[]{"窗口","无边框","全屏"}[(int)window.Mode]+" · 提醒 "+count+" 项";
        }
    }
}
