using System;
using FishSocial.Desktop.Auth;
using UnityEngine;

namespace FishSocial.Desktop.JournalV2
{
    /// <summary>Session-owned entry remains alive when a page is hidden. The server acknowledgement completes the journey.</summary>
    public sealed class JournalV2PondJourney : MonoBehaviour
    {
        JournalV2App app;
        SocialPondSessionController pond;
        string target;
        bool waiting;
        float deadline;
        public void Enter(JournalV2App owner,string pondId,string feeMode)
        {
            if(waiting||owner.Pond==null||owner.PondSelection.Busy)return;
            Unsubscribe();app=owner;pond=owner.Pond;target=pondId;waiting=true;deadline=Time.unscaledTime+25;
            pond.SnapshotChanged+=Snapshot;pond.ErrorReceived+=Failed;pond.StateChanged+=State;
            app.PondSelection.SetEntryStatus(true,"正在前往 "+(app.PondSelection.Selected?.displayName??pondId)+"…");
            pond.SetPendingReturnFeeMode(feeMode);
            if(pond.State==SocialSocketState.Connected)pond.SwitchPond(pondId,(ok,message)=>{if(!waiting)return;if(ok)Complete();else Failed(message);},feeMode);
            else pond.ConnectAndJoin(pondId,null,feeMode);
        }
        void Snapshot(PondSnapshotDto snapshot)
        {if(waiting&&snapshot?.pond?.id==target&&pond.CurrentUser!=null)Complete();}
        void State(SocialSocketState value,string message)
        {if(waiting&&(value==SocialSocketState.Failed||value==SocialSocketState.Disconnected))Failed(message);}
        void Update(){if(waiting&&Time.unscaledTime>=deadline)Failed("连接超时，请检查网络后重试。");}
        void Complete()
        {
            if(!waiting||pond==null||!string.Equals(pond.CurrentPondId,target,StringComparison.Ordinal))return;
            waiting=false;Unsubscribe();app.PondSelection.SetEntryStatus(false,"已进入鱼塘。");app.RefreshHeader();
            DesktopAppBootstrap.Instance?.StartNativeOverlay();WindowManager.Instance?.HideToTray();DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }
        void Failed(string message)
        {if(!waiting)return;waiting=false;Unsubscribe();app.PondSelection.SetEntryStatus(false,string.IsNullOrEmpty(message)?"进入失败，请重试。":message,true);app.Toast(app.PondSelection.Status);}
        void Unsubscribe(){if(pond==null)return;pond.SnapshotChanged-=Snapshot;pond.ErrorReceived-=Failed;pond.StateChanged-=State;}
        void OnDestroy(){Unsubscribe();if(waiting&&app!=null)app.PondSelection.SetEntryStatus(false,"连接已取消，可重试。",true);}
    }
}
