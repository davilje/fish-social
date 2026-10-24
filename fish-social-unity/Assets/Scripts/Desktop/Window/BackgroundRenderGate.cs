using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Drops frame rate while the main window is hidden in the tray.
    /// </summary>
    public static class BackgroundRenderGate
    {
        const int ForegroundFps = 60;
        const int BackgroundFps = 5;

        public static bool IsHidden { get; private set; }
        public static bool IsOverlayActive { get; private set; }
        static bool audioWork;
        // Main-thread socket dispatch and short fades need a responsive clock even with both windows hidden.
        public static void SetAudioWork(bool active)
        {
            if(audioWork==active)return;audioWork=active;
            Application.targetFrameRate=IsHidden&&!IsOverlayActive&&!audioWork?BackgroundFps:ForegroundFps;
        }

        public static void SetHidden(bool hidden)
        {
            IsHidden = hidden;
            Application.targetFrameRate =
                hidden && !IsOverlayActive && !audioWork ? BackgroundFps : ForegroundFps;
            QualitySettings.vSyncCount = 0;
        }

        public static void SetOverlayActive(bool active)
        {
            IsOverlayActive = active;
            Application.targetFrameRate =
                IsHidden && !active && !audioWork ? BackgroundFps : ForegroundFps;
            QualitySettings.vSyncCount = 0;
        }

        public static void ResetToForeground()
        {
            SetHidden(false);
        }
    }
}
