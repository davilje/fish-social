using UnityEngine;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Application entry: wires window/tray/notifications/UI and close-to-tray policy.
    /// </summary>
    public sealed class DesktopAppBootstrap : MonoBehaviour
    {
        const string SteamAppId = "2713340";
        const string SteamAuthIdentity = "fish-social-server-v1";

        public static DesktopAppBootstrap Instance { get; private set; }

        WindowManager _window;
        SystemTrayService _tray;
        DesktopNotificationService _notify;
        PanelRouter _router;
        SteamAuthController _steamAuth;
        PlaceholderFishingSessionLifecycle _session = new PlaceholderFishingSessionLifecycle();
        bool _allowQuit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnsureBootstrap()
        {
            if (FindObjectOfType<DesktopAppBootstrap>() != null)
                return;
            var go = new GameObject("DesktopAppBootstrap");
            go.AddComponent<DesktopAppBootstrap>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.wantsToQuit += OnWantsToQuit;
            BackgroundRenderGate.ResetToForeground();

            _window = gameObject.AddComponent<WindowManager>();
            _tray = gameObject.AddComponent<SystemTrayService>();
            _notify = gameObject.AddComponent<DesktopNotificationService>();
            _router = gameObject.AddComponent<PanelRouter>();
            _steamAuth = gameObject.AddComponent<SteamAuthController>();
            var steamTicketProvider = gameObject.AddComponent<SteamworksTicketProvider>();
            _steamAuth.Configure(
                steamTicketProvider,
                SteamAppId,
                SteamAuthIdentity);
            var ui = gameObject.AddComponent<DesktopShellUi>();

            _tray.ShowRequested += () =>
            {
                _window.ShowFromTray();
                _session.NotifyAppVisible();
            };
            _tray.HideRequested += () =>
            {
                _window.HideToTray();
                _session.NotifyAppHidden();
            };
            _tray.ExitRequested += QuitForReal;

            _window.VisibilityChanged += () =>
            {
                if (_window.IsWindowVisible)
                    _session.NotifyAppVisible();
                else
                    _session.NotifyAppHidden();
            };

            ui.Build(_router);
            Debug.Log("[DesktopShell] STEAM-DESKTOP-04 bootstrap ready");
        }

        public SteamAuthController SteamAuth => _steamAuth;

        void OnDestroy()
        {
            Application.wantsToQuit -= OnWantsToQuit;
        }

        bool OnWantsToQuit()
        {
#if UNITY_EDITOR
            // Always allow leaving Play Mode in Editor.
            return true;
#else
            if (_allowQuit)
                return true;

            // Close button → tray, keep process alive.
            if (_tray != null && _tray.IsReady)
            {
                _window.HideToTray();
                _session.NotifyAppHidden();
            }
            else
            {
                // Never leave the process invisible when the tray icon failed to initialize.
                _window.Minimize();
                Debug.LogWarning("[DesktopShell] tray not ready; minimized instead of hiding");
            }
            return false;
#endif
        }

        public void QuitForReal()
        {
            _allowQuit = true;
            if (_window != null && _window.IsWindowVisible && _window.Settings.Mode == WindowDisplayMode.Windowed)
                _window.ApplySettings(persist: true);
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
