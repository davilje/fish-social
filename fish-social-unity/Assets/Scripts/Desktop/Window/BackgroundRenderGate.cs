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

        public static void SetHidden(bool hidden)
        {
            IsHidden = hidden;
            Application.targetFrameRate = hidden ? BackgroundFps : ForegroundFps;
            QualitySettings.vSyncCount = 0;
        }

        public static void ResetToForeground()
        {
            SetHidden(false);
        }
    }
}
