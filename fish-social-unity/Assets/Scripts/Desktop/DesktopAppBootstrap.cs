using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Onboarding;
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
        DesktopShellUi _shellUi;
        DesktopOnboardingController _onboarding;
        string _nativeOverlayError = string.Empty;
        string _overlayDebugFishListMode = string.Empty;
        NativeOverlayDebugFishDto[] _overlayDebugFish = Array.Empty<NativeOverlayDebugFishDto>();
        string _overlayDebugSpotReport = string.Empty;
        string _overlayDebugSpotStatsJson = string.Empty;
        Coroutine _overlayDebugInspectRoutine;
        Coroutine _policeRaidRoutine;
        OverlayPlayerSocialBridge _playerSocialBridge;
        DesktopGameplayDebugMenu _gameplayDebug;
        FishingProgressDto _fishingProgress;
        Coroutine _progressRoutine;
        PlaceholderFishingSessionLifecycle _session = new PlaceholderFishingSessionLifecycle();
        bool _quitFallbackStarted;
        string _serverBaseUrl = DesktopServerConfig.DefaultServerBaseUrl;
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
            _serverBaseUrl = DesktopServerConfig.Resolve(out var serverUrlSource);
            Debug.Log("[DesktopShell] serverBaseUrl=" + _serverBaseUrl +
                      " source=" + serverUrlSource);
            _steamAuth = gameObject.AddComponent<SteamAuthController>();
            var steamTicketProvider = gameObject.AddComponent<SteamworksTicketProvider>();
            _steamAuth.Configure(
                steamTicketProvider,
                SteamAppId,
                SteamAuthIdentity,
                _serverBaseUrl);
            _pondSession = gameObject.AddComponent<SocialPondSessionController>();
            _pondSession.Configure(_steamAuth);
            _pondSession.StateChanged += OnPondStateChanged;
            _pondSession.SnapshotChanged += OnPondSnapshotChanged;
            _pondSession.UserUpdated += OnPondUserUpdated;
            _pondSession.UsersChanged += OnPondUsersChanged;
            _pondSession.FishBiteReceived += OnPondFishBite;
            _pondSession.CatchAccepted += OnPondCatchAccepted;
            _pondSession.ChatMessageReceived += OnPondChatMessage;
            _pondSession.PoliceRaidReceived += OnPondPoliceRaid;
            _pondSession.ErrorReceived += OnPondError;
            var socialAdapter = gameObject.AddComponent<SteamSocialLobbyAdapter>();
            _socialLobby = gameObject.AddComponent<SocialLobbyController>();
            _socialLobby.Configure(_steamAuth, _pondSession, socialAdapter, _serverBaseUrl);
            _petState = gameObject.AddComponent<PetStateController>();
            _petState.StateChanged += OnPetVisualStateChanged;
            var ui = gameObject.AddComponent<DesktopShellUi>();
            _shellUi = ui;

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
                PublishNativeOverlayState();
            };

            ui.Build(_router);
            _gameplayDebug = gameObject.AddComponent<DesktopGameplayDebugMenu>();
            _gameplayDebug.Configure(
                _shellUi.AuthenticatedApi,
                _pondSession,
                message =>
                {
                    _nativeOverlayError = message ?? string.Empty;
                    _shellUi?.SetStatusMessage(message);
                    PublishNativeOverlayState();
                },
                () =>
                {
                    QueueFishingProgressRefresh();
                    StartCoroutine(RefreshInventoryAfterDebug());
                });
            _playerSocialBridge = new OverlayPlayerSocialBridge(
                this,
                _shellUi.AuthenticatedApi,
                _pondSession,
                _shellUi,
                PublishNativeOverlayState,
                message => _nativeOverlayError = message ?? string.Empty);
            _onboarding = gameObject.AddComponent<DesktopOnboardingController>();
            _onboarding.Configure(
                _steamAuth,
                _shellUi.AuthenticatedApi,
                _pondSession,
                _shellUi);
            _petState.RefreshFromApp();
            Debug.Log("[DesktopShell] STEAM-DESKTOP-07A bootstrap ready");
            PublishNativeOverlayState();
        }

        public SteamAuthController SteamAuth => _steamAuth;
        public SocialPondSessionController PondSession => _pondSession;
        public SocialLobbyController SocialLobby => _socialLobby;
        public PetStateController PetState => _petState;
        public string ServerBaseUrl => _serverBaseUrl;

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
            QueueFishingProgressRefresh();
            PublishNativeOverlayState();
        }

        public void CloseNativeOverlay()
        {
            _nativeOverlay?.CloseOverlay();
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
                errorMessage = _nativeOverlayError ?? string.Empty,
            };
            OverlayPondStateBuilder.Fill(dto, _pondSession, _fishingProgress);
            dto.debugFishListMode = _overlayDebugFishListMode ?? string.Empty;
            dto.debugFish = _overlayDebugFish ?? Array.Empty<NativeOverlayDebugFishDto>();
            dto.debugSpotReport = _overlayDebugSpotReport ?? string.Empty;
            dto.debugSpotStatsJson = _overlayDebugSpotStatsJson ?? string.Empty;
            if (!string.IsNullOrEmpty(_pendingGroundbaitBubble))
            {
                dto.observation = new NativeOverlayChatDto
                {
                    messageId = "gb-tip-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    nickname = "打窝",
                    text = _pendingGroundbaitBubble,
                    sentAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
                _pendingGroundbaitBubble = null;
            }
            dto.mainWindowRaised = _window != null && _window.IsWindowVisible && !_window.IsLoginShell;
            _nativeOverlay.PublishState(dto);
        }

        void QueueFishingProgressRefresh()
        {
            if (_progressRoutine != null)
                StopCoroutine(_progressRoutine);
            _progressRoutine = StartCoroutine(RefreshFishingProgressRoutine());
        }

        IEnumerator RefreshFishingProgressRoutine()
        {
            var api = _shellUi != null ? _shellUi.AuthenticatedApi : null;
            if (api == null || !api.CanUse)
            {
                _progressRoutine = null;
                yield break;
            }

            yield return api.GetFishingProgress((ok, dto, _) =>
            {
                if (ok && dto != null)
                    _fishingProgress = dto;
            });
            _progressRoutine = null;
            PublishNativeOverlayState();
        }

        public void RaiseMainWindow(ShellPanelId id)
        {
            if (_onboarding != null &&
                _onboarding.IsOnboardingActive &&
                id != ShellPanelId.Settings)
            {
                _window?.ShowFromTray();
                NativeWindowUtil.TryBringToFront();
                _shellUi?.SetStatusMessage("请先完成新手引导。");
                PublishNativeOverlayState();
                return;
            }

            _window?.ShowFromTray();
            _router?.Show(id);
            NativeWindowUtil.TryBringToFront();
            PublishNativeOverlayState();
        }

        void OnPetVisualStateChanged(PetVisualState _)
        {
            PublishNativeOverlayState();
        }

        void OnPondStateChanged(SocialSocketState state, string message)
        {
            if (state == SocialSocketState.Connected)
            {
                _nativeOverlayError = string.Empty;
                QueueFishingProgressRefresh();
            }
            else if (!string.IsNullOrEmpty(message))
                _nativeOverlayError = message;
            PublishNativeOverlayState();
        }

        void OnPondSnapshotChanged(PondSnapshotDto _)
        {
            PublishNativeOverlayState();
        }

        void OnPondUserUpdated(PondUserDto _)
        {
            PublishNativeOverlayState();
        }

        void OnPondUsersChanged()
        {
            PublishNativeOverlayState();
        }

        void OnPondFishBite(PendingFishCatchDto _)
        {
            PublishNativeOverlayState();
        }

        void OnPondCatchAccepted()
        {
            QueueFishingProgressRefresh();
        }

        void OnPondChatMessage(ChatMessageDto _)
        {
            PublishNativeOverlayState();
        }

        void OnPondPoliceRaid(PoliceRaidDto raid)
        {
            PublishNativeOverlayState();
            if (raid == null)
                return;
            var title = raid.status == "warning" ? "巡警来了" : "巡警事件";
            var body = !string.IsNullOrEmpty(raid.message)
                ? raid.message
                : (!string.IsNullOrEmpty(raid.text) ? raid.text : PoliceRaidDto.WarningText);
            var windowVisible = _window != null && _window.IsWindowVisible;
            if (raid.status == "warning" && !windowVisible)
                return;
            DesktopNotificationService.Instance?.Publish(new DesktopNotification(
                NotificationKind.SystemWarning, title, body));
            _shellUi?.SetStatusMessage(body);
        }

        void OnPondError(string message)
        {
            _nativeOverlayError = message ?? "鱼塘操作失败。";
            if (!string.IsNullOrEmpty(message) && message.IndexOf("金币不足") >= 0)
                _shellUi?.SetStatusMessage(message);
            PublishNativeOverlayState();
        }

        void OnNativeOverlayCommand(NativeOverlayCommandDto message)
        {
            if (message == null)
                return;
            if (_onboarding != null && _onboarding.HandleOverlayCommand(message))
            {
                PublishNativeOverlayState();
                return;
            }
            var command = message.command;
            switch (command)
            {
                case "open_main":
                    RaiseMainWindow(ShellPanelId.Home);
                    return;
                case "hide_overlay":
                    _nativeOverlay?.HideOverlay();
                    return;
                case "request_snapshot":
                    PublishNativeOverlayState();
                    return;
                case "take_spot":
                    ExecuteOverlayCommand(
                        callback => _pondSession?.TakeSpot(message.spotId, callback));
                    return;
                case "leave_spot":
                    ExecuteOverlayCommand(
                        callback => _pondSession?.LeaveSpot(callback));
                    return;
                case "start_fishing":
                    ExecuteOverlayCommand(
                        callback => _pondSession?.StartFishing(
                            string.IsNullOrEmpty(message.spotId)
                                ? _pondSession?.FirstSpotId
                                : message.spotId,
                            callback));
                    return;
                case "groundbait_start":
                    ExecuteOverlayGroundbait(message.spotId);
                    return;
                case "stop_fishing":
                    ExecuteOverlayCommand(callback => _pondSession?.StopFishing(callback));
                    return;
                case "accept_catch":
                    ExecuteOverlayCommand(callback => _pondSession?.AcceptLatestCatch(callback));
                    return;
                case "exit_pond":
                    ExitPondFromOverlay();
                    return;
                case "player_open_profile":
                case "player_add_friend":
                case "player_open_dm":
                case "player_like_recent":
                    _playerSocialBridge?.Handle(message);
                    return;
                case "send_pond_chat":
                    ExecuteOverlayCommand(
                        callback => _pondSession?.SendChat(message.text, callback));
                    return;
                case "debug_police_raid":
                    ExecuteOverlayPoliceRaid();
                    return;
                case "gameplay_debug":
                    ExecuteOverlayGameplayDebug(message.text);
                    return;
            }

            if (!DesktopProductMenuCommands.TryParse(command, out var action))
                return;

            if (action == DesktopProductMenuAction.Quit)
            {
                QuitForReal();
                return;
            }

            if (action == DesktopProductMenuAction.HideToTray)
                _nativeOverlay?.HideOverlay();

            _shellUi?.HandleProductMenu(action);
        }

        void ExecuteOverlayPoliceRaid()
        {
            var api = _shellUi != null ? _shellUi.AuthenticatedApi : null;
            if (api == null || !api.CanUse)
            {
                _nativeOverlayError = "需先登录后再一键出警。";
                PublishNativeOverlayState();
                return;
            }

            if (_policeRaidRoutine != null)
                StopCoroutine(_policeRaidRoutine);
            _policeRaidRoutine = StartCoroutine(TriggerPoliceRaidRoutine(api));
        }

        void ExecuteOverlayGameplayDebug(string action)
        {
            if (!GameplayDebugGate.IsClientEnabled())
            {
                _nativeOverlayError = "当前客户端未开启玩法 Debug。";
                PublishNativeOverlayState();
                return;
            }

            if (string.Equals(action, "toggle", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(action))
            {
                _gameplayDebug?.Toggle();
                return;
            }

            if (string.Equals(action, "list_pond_fish", StringComparison.OrdinalIgnoreCase))
            {
                StartOverlayDebugInspect("pond");
                return;
            }

            if (string.Equals(action, "list_spot_fish", StringComparison.OrdinalIgnoreCase))
            {
                StartOverlayDebugInspect("spot");
                return;
            }

            if (string.Equals(action, "spot_stats", StringComparison.OrdinalIgnoreCase))
            {
                StartOverlayDebugSpotStats();
                return;
            }

            if (!string.IsNullOrEmpty(action) &&
                (action.StartsWith("catch_fish:", StringComparison.OrdinalIgnoreCase) ||
                 action.StartsWith("force_bite_instant:", StringComparison.OrdinalIgnoreCase)))
            {
                var fishId = action.Contains(":")
                    ? action.Substring(action.IndexOf(':') + 1).Trim()
                    : string.Empty;
                StartOverlayDebugCatch(fishId, "catch_fish");
                return;
            }

            if (string.Equals(action, "instant_catch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action, "complete_catch", StringComparison.OrdinalIgnoreCase))
            {
                StartOverlayDebugCatch(null, "complete_catch");
                return;
            }

            if (string.Equals(action, "catch_quality_red", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action, "catch_quality_orange", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action, "catch_quality_gold", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action, "catch_quality_purple", StringComparison.OrdinalIgnoreCase))
            {
                StartOverlayDebugCatch(null, action);
                return;
            }

            if (!string.IsNullOrEmpty(action) &&
                action.StartsWith("force_bite:", StringComparison.OrdinalIgnoreCase))
            {
                var fishId = action.Substring("force_bite:".Length).Trim();
                StartOverlayDebugForceBite(fishId);
                return;
            }

            _gameplayDebug?.RunAction(action);
        }

        void StartOverlayDebugInspect(string scope)
        {
            var api = _shellUi != null ? _shellUi.AuthenticatedApi : null;
            if (api == null || !api.CanUse)
            {
                _nativeOverlayError = "需先登录后再查看塘鱼。";
                PublishNativeOverlayState();
                return;
            }

            if (_overlayDebugInspectRoutine != null)
                StopCoroutine(_overlayDebugInspectRoutine);
            _overlayDebugInspectRoutine = StartCoroutine(OverlayDebugInspectRoutine(api, scope));
        }

        IEnumerator OverlayDebugInspectRoutine(IAuthenticatedApiClient api, string scope)
        {
            _nativeOverlayError = scope == "spot" ? "正在拉取当前钓位鱼…" : "正在拉取当前鱼塘鱼…";
            PublishNativeOverlayState();
            yield return api.GetGameplayDebugPondFish(scope, (ok, fish, pondId, spotId, message) =>
            {
                if (!ok)
                {
                    _nativeOverlayError = message ?? "拉取失败。";
                    return;
                }

                _overlayDebugFishListMode = scope == "spot" ? "spot" : "pond";
                _overlayDebugFish = fish ?? Array.Empty<NativeOverlayDebugFishDto>();
                _overlayDebugSpotReport = string.Empty;
                _overlayDebugSpotStatsJson = string.Empty;
                _nativeOverlayError =
                    "已加载 " + _overlayDebugFish.Length + " 条鱼" +
                    (string.IsNullOrEmpty(pondId) ? "" : " · 塘 " + pondId) +
                    (scope == "spot" && !string.IsNullOrEmpty(spotId) ? " · 位 " + spotId : "");
            });
            _overlayDebugInspectRoutine = null;
            PublishNativeOverlayState();
        }

        void StartOverlayDebugSpotStats()
        {
            var api = _shellUi != null ? _shellUi.AuthenticatedApi : null;
            if (api == null || !api.CanUse)
            {
                _nativeOverlayError = "需先登录后再查看钓位数据。";
                PublishNativeOverlayState();
                return;
            }

            if (_overlayDebugInspectRoutine != null)
                StopCoroutine(_overlayDebugInspectRoutine);
            _overlayDebugInspectRoutine = StartCoroutine(OverlayDebugSpotStatsRoutine(api));
        }

        IEnumerator OverlayDebugSpotStatsRoutine(IAuthenticatedApiClient api)
        {
            _nativeOverlayError = "正在拉取钓位数据…";
            PublishNativeOverlayState();
            yield return api.GetGameplayDebugSpotStats((ok, report, rawJson, message) =>
            {
                if (!ok)
                {
                    _nativeOverlayError = message ?? "拉取钓位数据失败。";
                    return;
                }

                _overlayDebugFishListMode = "spot_stats";
                _overlayDebugSpotReport = report ?? string.Empty;
                _overlayDebugSpotStatsJson = rawJson ?? string.Empty;
                _nativeOverlayError = "已加载当前钓位数据";
            });
            _overlayDebugInspectRoutine = null;
            PublishNativeOverlayState();
        }

        void StartOverlayDebugCatch(string fishId, string apiAction)
        {
            var api = _shellUi != null ? _shellUi.AuthenticatedApi : null;
            if (api == null || !api.CanUse)
            {
                _nativeOverlayError = "需先登录后再钓获测试鱼。";
                PublishNativeOverlayState();
                return;
            }

            if (_overlayDebugInspectRoutine != null)
                StopCoroutine(_overlayDebugInspectRoutine);
            _overlayDebugInspectRoutine = StartCoroutine(OverlayDebugCatchRoutine(api, fishId, apiAction));
        }

        IEnumerator OverlayDebugCatchRoutine(IAuthenticatedApiClient api, string fishId, string apiAction)
        {
            _nativeOverlayError = "正在结算钓获…";
            PublishNativeOverlayState();
            var autoReturned = false;
            var ok = false;
            yield return api.PostGameplayDebug(apiAction, fishId, (success, message, returned) =>
            {
                ok = success;
                autoReturned = returned;
                _nativeOverlayError = success
                    ? (string.IsNullOrEmpty(message) ? "钓获结算完成" : message)
                    : (string.IsNullOrEmpty(message) ? "钓获结算失败" : message);
            });
            _overlayDebugInspectRoutine = null;
            PublishNativeOverlayState();
            if (ok && !autoReturned)
                yield return RefreshInventoryAfterDebug();
        }

        void StartOverlayDebugForceBite(string fishId)
        {
            var api = _shellUi != null ? _shellUi.AuthenticatedApi : null;
            if (api == null || !api.CanUse)
            {
                _nativeOverlayError = "需先登录后再强制上钩。";
                PublishNativeOverlayState();
                return;
            }

            if (string.IsNullOrEmpty(fishId))
            {
                _nativeOverlayError = "未指定鱼 ID。";
                PublishNativeOverlayState();
                return;
            }

            if (_overlayDebugInspectRoutine != null)
                StopCoroutine(_overlayDebugInspectRoutine);
            _overlayDebugInspectRoutine = StartCoroutine(OverlayDebugForceBiteRoutine(api, fishId));
        }

        IEnumerator OverlayDebugForceBiteRoutine(IAuthenticatedApiClient api, string fishId)
        {
            _nativeOverlayError = "正在强制上钩…";
            PublishNativeOverlayState();
            yield return api.PostGameplayDebug("force_bite", fishId, (ok, message) =>
            {
                _nativeOverlayError = ok
                    ? (string.IsNullOrEmpty(message) ? "强制上钩成功" : message)
                    : (string.IsNullOrEmpty(message) ? "强制上钩失败" : message);
            });
            _overlayDebugInspectRoutine = null;
            PublishNativeOverlayState();
        }

        IEnumerator RefreshInventoryAfterDebug()
        {
            var api = _shellUi != null ? _shellUi.AuthenticatedApi : null;
            if (api == null || !api.CanUse)
                yield break;
            yield return api.GetInventoryItems((ok, items, _) =>
            {
                if (ok)
                    _pondSession?.ReplaceInventory(items);
            });
        }

        IEnumerator TriggerPoliceRaidRoutine(IAuthenticatedApiClient api)
        {
            _nativeOverlayError = "正在请求服务端出警…";
            PublishNativeOverlayState();
            yield return api.TriggerDebugPoliceRaid((ok, message) =>
            {
                _nativeOverlayError = ok ? string.Empty : (message ?? "出警失败。");
            });
            PublishNativeOverlayState();
            _policeRaidRoutine = null;
        }

        void ExecuteOverlayCommand(System.Action<System.Action<bool, string>> operation)
        {
            if (_pondSession == null)
            {
                _nativeOverlayError = "鱼塘会话尚未初始化。";
                PublishNativeOverlayState();
                return;
            }

            _nativeOverlayError = string.Empty;
            PublishNativeOverlayState();
            operation((ok, message) =>
            {
                _nativeOverlayError = ok ? string.Empty : (message ?? "操作失败。");
                PublishNativeOverlayState();
            });
        }

        void ExecuteOverlayGroundbait(string preferredId)
        {
            if (_pondSession == null)
            {
                _nativeOverlayError = "鱼塘会话尚未初始化。";
                PublishNativeOverlayState();
                return;
            }
            var id = ResolveGroundbaitId(preferredId);
            if (string.IsNullOrEmpty(id))
            {
                _nativeOverlayError = "尚未解锁任何窝料（需钓鱼等级 3）";
                PublishNativeOverlayState();
                return;
            }
            _nativeOverlayError = string.Empty;
            PublishNativeOverlayState();
            _pondSession.StartGroundbait(id, (ok, message) =>
            {
                _nativeOverlayError = ok ? string.Empty : (message ?? "打窝失败。");
                // Gold / cap errors surface as Overlay bubble via observation-style tip
                if (!ok && !string.IsNullOrEmpty(message) &&
                    (message.Contains("金币") || message.Contains("上限")))
                    QueueGroundbaitBubble(message);
                PublishNativeOverlayState();
            });
        }

        string ResolveGroundbaitId(string preferredId)
        {
            if (!string.IsNullOrEmpty(preferredId) &&
                (preferredId == "gb-basic" || preferredId == "gb-mix" || preferredId == "gb-premium"))
                return preferredId;
            var level = _fishingProgress != null && _fishingProgress.level > 0
                ? _fishingProgress.level
                : 1;
            if (level >= 7) return "gb-premium";
            if (level >= 5) return "gb-mix";
            if (level >= 3) return "gb-basic";
            return string.Empty;
        }

        void QueueGroundbaitBubble(string text)
        {
            // Transient private bubble via observation DTO next publish
            _pendingGroundbaitBubble = text;
        }

        string _pendingGroundbaitBubble;

        void ExitPondFromOverlay()
        {
            if (_pondSession == null)
            {
                _nativeOverlayError = "鱼塘会话尚未初始化。";
                PublishNativeOverlayState();
                return;
            }
            _nativeOverlayError = "正在退出鱼塘…";
            PublishNativeOverlayState();
            _pondSession.ExitPond((ok, message) =>
            {
                _nativeOverlayError = ok ? string.Empty : (message ?? "退出鱼塘失败。");
                if (ok)
                {
                    _nativeOverlay?.CloseOverlay();
                    RaiseMainWindow(ShellPanelId.Home);
                }
                PublishNativeOverlayState();
            });
        }

        void OnDestroy()
        {
            Debug.Log("[Shutdown] DesktopAppBootstrap.OnDestroy begin.");
            if (_petState != null)
                _petState.StateChanged -= OnPetVisualStateChanged;
            if (_pondSession != null)
            {
                _pondSession.StateChanged -= OnPondStateChanged;
                _pondSession.SnapshotChanged -= OnPondSnapshotChanged;
                _pondSession.UserUpdated -= OnPondUserUpdated;
                _pondSession.UsersChanged -= OnPondUsersChanged;
                _pondSession.FishBiteReceived -= OnPondFishBite;
                _pondSession.ChatMessageReceived -= OnPondChatMessage;
                _pondSession.PoliceRaidReceived -= OnPondPoliceRaid;
                _pondSession.ErrorReceived -= OnPondError;
            }
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
            return true;
#else
            if (WindowManager.Instance != null &&
                WindowManager.Instance.Settings.HideToTrayOnClose)
            {
                WindowManager.Instance.HideToTray();
                return false;
            }

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
