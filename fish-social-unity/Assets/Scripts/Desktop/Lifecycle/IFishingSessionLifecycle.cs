namespace FishSocial.Desktop
{
    /// <summary>
    /// Placeholder lifecycle for a future real fishing session while the app is tray-hidden.
    /// </summary>
    public interface IFishingSessionLifecycle
    {
        bool HasActiveSession { get; }
        void NotifyAppHidden();
        void NotifyAppVisible();
    }

    public sealed class PlaceholderFishingSessionLifecycle : IFishingSessionLifecycle
    {
        public bool HasActiveSession { get; private set; }

        public void BeginPlaceholderSession()
        {
            HasActiveSession = true;
        }

        public void EndPlaceholderSession()
        {
            HasActiveSession = false;
        }

        public void NotifyAppHidden()
        {
            // Future: keep session timers / sockets alive without UI ticks.
        }

        public void NotifyAppVisible()
        {
            // Future: refresh UI from authoritative server state.
        }
    }
}
