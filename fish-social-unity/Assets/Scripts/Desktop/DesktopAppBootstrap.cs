using System.Threading;
using UnityEngine;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Social;

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
        SocialPondSessionController _pondSession;
        SocialLobbyController _socialLobby;
        PlaceholderFishingSessionLifecycle _session = new PlaceholderFishingSessionLifecycle();
#if !UNITY_EDITOR
        Mutex _singleInstanceMutex;
#endif

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
#if !UNITY_EDITOR
            bool created;
            _singleInstanceMutex = new Mutex(
                true,
                "Local\\FishSocialDesktop-2713340",
                out created);
            if (!created)
            {
                Debug.LogWarning("[DesktopShell] another Fish Social instance is already running");
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                Application.Quit();
                return;
            }
#endif
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
            _pondSession = gameObject.AddComponent<SocialPondSessionController>();
            _pondSession.Configure(_steamAuth);
            var socialAdapter = gameObject.AddComponent<SteamSocialLobbyAdapter>();
            _socialLobby = gameObject.AddComponent<SocialLobbyController>();
            _socialLobby.Configure(_steamAuth, _pondSession, socialAdapter);
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
        public SocialPondSessionController PondSession => _pondSession;
        public SocialLobbyController SocialLobby => _socialLobby;

        void OnDestroy()
        {
            Application.wantsToQuit -= OnWantsToQuit;
#if !UNITY_EDITOR
            if (_singleInstanceMutex != null)
            {
                _singleInstanceMutex.ReleaseMutex();
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }
#endif
        }

        bool OnWantsToQuit()
        {
#if UNITY_EDITOR
            // Always allow leaving Play Mode in Editor.
            return true;
#else
            // The window close button must terminate the process. Hiding to
            // tray remains an explicit action from the tray menu.
            return true;
#endif
        }

        public void QuitForReal()
        {
            if (_window != null && _window.IsWindowVisible && _window.Settings.Mode == WindowDisplayMode.Windowed)
                _window.ApplySettings(persist: true);
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
