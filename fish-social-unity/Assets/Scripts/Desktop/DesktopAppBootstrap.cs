using System.Threading;
using UnityEngine;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Pet;
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
        NativeOverlayProcessController _nativeOverlay;
        PetStateController _petState;
        PlaceholderFishingSessionLifecycle _session = new PlaceholderFishingSessionLifecycle();
        bool _quitFallbackStarted;
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
            _petState = gameObject.AddComponent<PetStateController>();
            _petState.StateChanged += OnPetVisualStateChanged;
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
            _tray.OverlayShowRequested += () =>
            {
                if (_nativeOverlay != null)
                    _nativeOverlay.ShowOverlay();
            };
            _tray.OverlayExitRequested += () =>
            {
                _nativeOverlay?.CloseOverlay();
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
            _petState.RefreshFromApp();
            Debug.Log("[DesktopShell] STEAM-DESKTOP-07A bootstrap ready");
            PublishNativeOverlayState();
        }

        public SteamAuthController SteamAuth => _steamAuth;
        public SocialPondSessionController PondSession => _pondSession;
        public SocialLobbyController SocialLobby => _socialLobby;
        public PetStateController PetState => _petState;

        public void StartNativeOverlay()
        {
            if (string.Equals(
                    System.Environment.GetEnvironmentVariable("FISH_SOCIAL_DISABLE_OVERLAY"),
                    "1",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[NativeOverlay] disabled by FISH_SOCIAL_DISABLE_OVERLAY.");
                return;
            }

            EnsureNativeOverlay().StartOverlay();
            PublishNativeOverlayState();
        }

        NativeOverlayProcessController EnsureNativeOverlay()
        {
            if (_nativeOverlay != null)
                return _nativeOverlay;

            _nativeOverlay = gameObject.AddComponent<NativeOverlayProcessController>();
            _nativeOverlay.CommandReceived += OnNativeOverlayCommand;
            return _nativeOverlay;
        }

        public void PublishNativeOverlayState()
        {
            if (_nativeOverlay == null)
                return;

            var dto = new NativeOverlayStateDto
            {
                loginState = _steamAuth?.State.ToString() ?? "SignedOut",
                connectionState = _pondSession?.State.ToString() ?? "Disconnected",
                fishingPhase = _pondSession?.CurrentPhase ?? "idle",
                petVisualState = PetStateController.ToWire(
                    _petState != null ? _petState.Current : PetVisualState.Offline),
            };
            OverlayPondStateBuilder.Fill(dto, _pondSession);
            _nativeOverlay.PublishState(dto);
        }

        void OnPetVisualStateChanged(PetVisualState _)
        {
            PublishNativeOverlayState();
        }

        void OnNativeOverlayCommand(string command)
        {
            switch (command)
            {
                case "open_main":
                    _window?.ShowFromTray();
                    _router?.Show(ShellPanelId.Home);
                    NativeWindowUtil.TryFocusWindow();
                    break;
                case "hide_overlay":
                    _nativeOverlay?.HideOverlay();
                    break;
                case "request_snapshot":
                    PublishNativeOverlayState();
                    break;
            }
        }

        void OnDestroy()
        {
            Debug.Log("[Shutdown] DesktopAppBootstrap.OnDestroy begin.");
            if (_petState != null)
                _petState.StateChanged -= OnPetVisualStateChanged;
            _nativeOverlay?.ForceTerminateForApplicationQuit();
            Application.wantsToQuit -= OnWantsToQuit;
            if (_nativeOverlay != null)
            {
                _nativeOverlay.CommandReceived -= OnNativeOverlayCommand;
            }
#if !UNITY_EDITOR
            if (_singleInstanceMutex != null)
            {
                _singleInstanceMutex.ReleaseMutex();
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }
#endif
            Debug.Log("[Shutdown] DesktopAppBootstrap.OnDestroy complete.");
        }

        bool OnWantsToQuit()
        {
#if UNITY_EDITOR
            // Always allow leaving Play Mode in Editor.
            return true;
#else
            // The window close button must terminate the process. Hiding to
            // tray remains an explicit action from the tray menu.
            Debug.Log("[Shutdown] OnWantsToQuit received.");
            _nativeOverlay?.ForceTerminateForApplicationQuit();
            StartQuitFallback();
            return true;
#endif
        }

        void StartQuitFallback()
        {
            if (_quitFallbackStarted)
                return;

            _quitFallbackStarted = true;
            var processId = System.Diagnostics.Process.GetCurrentProcess().Id;
            Debug.Log("[Shutdown] detached quit fallback armed for process " + processId);
            try
            {
                DetachedWin32Process.TaskkillPidAfterDelay(processId, 3);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[Shutdown] quit fallback could not start: " +
                    exception.Message);
            }
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
