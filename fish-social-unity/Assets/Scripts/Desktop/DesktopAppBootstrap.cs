using System;
using System.Collections;
using System.Collections.Generic;
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
        OwnFishingPresenter _ownFishing;
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
            gameObject.AddComponent<DesktopAudioService>();
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
            _steamAuth.StateChanged += OnSteamAuthStateChanged;
            _pondSession = gameObject.AddComponent<SocialPondSessionController>();
            _pondSession.Configure(_steamAuth);
            _pondSession.StateChanged += OnPondStateChanged;
            _pondSession.SnapshotChanged += OnPondSnapshotChanged;
            _pondSession.UserUpdated += OnPondUserUpdated;
            _pondSession.UsersChanged += OnPondUsersChanged;
            _pondSession.FishBiteReceived += OnPondFishBite;
            _pondSession.FishMissReceived += OnPondFishMiss;
            _pondSession.FishCatchSettled += OnPondFishCatchSettled;
            _pondSession.CatchAccepted += OnPondCatchAccepted;
            _pondSession.ChatMessageReceived += OnPondChatMessage;
            _pondSession.PoliceRaidReceived += OnPondPoliceRaid;
            _pondSession.ErrorReceived += OnPondError;
            _ownFishing = new OwnFishingPresenter(
                this,
                () => _pondSession,
                () => { RefreshFishingAudio(); PublishNativeOverlayState(true); });
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

        void OnSteamAuthStateChanged(SteamLoginState state)
        {
            if (state == SteamLoginState.Authenticated)
                StartCoroutine(PrefetchOwnProfileRoutine());
            else if (state == SteamLoginState.SignedOut || state == SteamLoginState.Failed)
                DesktopProfileCache.Latest = null;
        }

        IEnumerator PrefetchOwnProfileRoutine()
        {
            var api = _shellUi != null ? _shellUi.AuthenticatedApi : null;
            if (api == null || !api.CanUse)
                yield break;
            yield return api.GetPlayerProfile((ok, profile, message) =>
            {
                if (!ok || profile == null)
                    return;
                DesktopProfileCache.Latest = profile;
                if (_pondSession != null)
                {
                    if (!string.IsNullOrEmpty(profile.nickname))
                        _pondSession.ApplyGameNickname(profile.nickname);
                    _pondSession.ApplyGameAvatarUrl(profile.avatarUrl);
                }
                PublishNativeOverlayState();
            });
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

            var overlay = EnsureNativeOverlay();
            PublishNativeOverlayState(true);
            overlay.StartOverlay();
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

        const float OverlayPublishIntervalSeconds = 1f / 15f;
        const int MaxOverlayAcks = 8;
        readonly System.Collections.Generic.List<NativeOverlayCommandAckDto> _overlayAcks =
            new System.Collections.Generic.List<NativeOverlayCommandAckDto>();
        bool _overlayStateDirty;
        float _lastOverlayPublishAt = -1f;
        bool _flushingOverlayState;
        bool _petVisualRefreshPending;
        Coroutine _fishingArtWarmupRoutine;

        public void PublishNativeOverlayState()
        {
            PublishNativeOverlayState(false);
        }

        void PublishNativeOverlayState(bool urgent)
        {
            if (_nativeOverlay == null)
                return;
            _overlayStateDirty = true;
            if (_flushingOverlayState)
                return;
            if (urgent ||
                _lastOverlayPublishAt < 0f ||
                Time.unscaledTime - _lastOverlayPublishAt >= OverlayPublishIntervalSeconds)
                FlushNativeOverlayState();
        }

        /// <summary>
        /// Schedule Unity hero-cat sprite refresh after overlay IPC/state work.
        /// FishingLayerArt loads PNGs on the main thread; never do that inside Flush.
        /// </summary>
        public void RequestPetVisualRefresh()
        {
            _petVisualRefreshPending = true;
        }

        void LateUpdate()
        {
            _ownFishing?.Tick();
            if (_nativeOverlay != null && Time.unscaledDeltaTime > 0.25f)
                Debug.Log(
                    "[Latency][Unity] frame_stall ms=" +
                    Mathf.RoundToInt(Time.unscaledDeltaTime * 1000f) +
                    " atMs=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            if (_overlayStateDirty &&
                !_flushingOverlayState &&
                Time.unscaledTime - _lastOverlayPublishAt >= OverlayPublishIntervalSeconds)
                FlushNativeOverlayState();
            if (_petVisualRefreshPending)
            {
                _petVisualRefreshPending = false;
                _petState?.RefreshFromApp();
            }
        }

        void CompleteOverlayAck(long commandId, string command, bool ok, string message)
        {
            if (commandId <= 0)
                return;
            _ownFishing?.OnCommandFinished(command, ok);
            _overlayAcks.Add(new NativeOverlayCommandAckDto
            {
                commandId = commandId,
                command = command ?? string.Empty,
                done = true,
                ok = ok,
                message = message ?? string.Empty,
            });
            if (_overlayAcks.Count > MaxOverlayAcks)
                _overlayAcks.RemoveRange(0, _overlayAcks.Count - MaxOverlayAcks);
            if (_nativeOverlay != null)
            {
                _overlayStateDirty = true;
                FlushNativeOverlayState();
            }
        }

        void FlushNativeOverlayState()
        {
            if (_nativeOverlay == null || _flushingOverlayState)
                return;
            _flushingOverlayState = true;
            try
            {
                _overlayStateDirty = false;
                _lastOverlayPublishAt = Time.unscaledTime;

                // Do not RefreshFromApp/ApplyRenderers here: FishingLayerArt.Open reads
                // and scales every layer PNG synchronously and has been measured to
                // stall the main thread for 2–5s, delaying overlay_command_received.
                var presentationActive = _ownFishing != null && _ownFishing.IsActive;
                var dto = new NativeOverlayStateDto
                {
                    loginState = _steamAuth?.State.ToString() ?? "SignedOut",
                    connectionState = _pondSession?.State.ToString() ?? "Disconnected",
                    fishingPhase = presentationActive
                        ? _ownFishing.Phase
                        : (_pondSession?.CurrentPhase ?? "idle"),
                    petVisualState = PetStateController.ToWire(
                        presentationActive
                            ? _ownFishing.ResolveVisual(_pondSession)
                            : (_petState != null ? _petState.Current : PetVisualState.Offline)),
                    errorMessage = _nativeOverlayError ?? string.Empty,
                };
                OverlayPondStateBuilder.Fill(dto, _pondSession, _fishingProgress);
                _ownFishing?.ApplyToDto(dto, _pondSession);
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
                dto.commandAcks = _overlayAcks.ToArray();
                _nativeOverlay.PublishState(dto);
            }
            finally
            {
                _flushingOverlayState = false;
            }
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

        Coroutine _raiseWindowRoutine;

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

            _router?.Show(id);
            if (_window != null && !_window.IsWindowVisible)
            {
                if (_raiseWindowRoutine != null) StopCoroutine(_raiseWindowRoutine);
                _raiseWindowRoutine = StartCoroutine(RestorePreparedMainWindow());
                return;
            }
            _window?.ShowFromTray();
            NativeWindowUtil.TryBringToFront();
            PublishNativeOverlayState();
        }

        IEnumerator RestorePreparedMainWindow()
        {
            // Let Unity render the selected page while the native window is
            // still hidden, so Windows cannot expose the old pond/page buffer.
            yield return null;
            _raiseWindowRoutine = null;
            _window?.ShowFromTray();
            NativeWindowUtil.TryBringToFront();
            PublishNativeOverlayState();
        }

        void OnPetVisualStateChanged(PetVisualState _)
        {
            PublishNativeOverlayState(true);
        }

        void OnPondStateChanged(SocialSocketState state, string message)
        {
            if (state == SocialSocketState.Connected)
            {
                _nativeOverlayError = string.Empty;
                QueueFishingProgressRefresh();
                QueueFishingArtWarmup();
            }
            else
            {
                _ownFishing?.ClearRound();
                if (!string.IsNullOrEmpty(message))
                    _nativeOverlayError = message;
            }
            PublishNativeOverlayState();
            RequestPetVisualRefresh();
        }

        void QueueFishingArtWarmup()
        {
            if (_fishingArtWarmupRoutine != null)
                StopCoroutine(_fishingArtWarmupRoutine);
            _fishingArtWarmupRoutine = StartCoroutine(FishingArtWarmupRoutine());
        }

        IEnumerator FishingArtWarmupRoutine()
        {
            // Cache key is pet|clip|drawW x drawH from the live slot, not 256.
            // Play mode decodes each PNG off the main thread and uploads one texture per call.
            var sizes = new List<Vector2Int>();
            for (var wait = 0; wait < 180; wait++)
            {
                CollectHeroDrawSizes(sizes);
                if (sizes.Count > 0)
                    break;
                yield return null;
            }

            if (sizes.Count == 0)
                Debug.LogWarning("[Latency][Unity] fishing_art_warmup waiting: slot size unavailable");

            var petId = PetArtLoader.CurrentOwnPetId();
            var clips = OrderedWarmupClips(petId);
            for (var s = 0; s < sizes.Count; s++)
            {
                Debug.Log(
                    "[Latency][Unity] fishing_art_warmup pet=" + petId +
                    " draw=" + sizes[s].x + "x" + sizes[s].y +
                    " first=" + clips[0]);
                for (var i = 0; i < clips.Length; i++)
                {
                    while (!FishingLayerArt.WarmupSlice(petId, clips[i], sizes[s].x, sizes[s].y))
                        yield return null;
                    RequestPetVisualRefresh();
                    yield return null;
                }
            }

            while (enabled)
            {
                if (SliceOneVisibleClip())
                    RequestPetVisualRefresh();
                else
                    yield return new WaitForSecondsRealtime(0.25f);
                yield return null;
            }

            _fishingArtWarmupRoutine = null;
        }

        static string[] OrderedWarmupClips(string petId)
        {
            var names = FishingLayerArt.ClipNames;
            var first = names.Length > 0 ? names[0] : "idle";
            var found = FindObjectsOfType<SpriteFramePetRenderer>();
            for (var i = 0; i < found.Length; i++)
            {
                var renderer = found[i];
                if (renderer == null || !renderer.isActiveAndEnabled)
                    continue;
                if (!renderer.TryGetWarmTarget(out var id, out var clip, out _, out _) || id != petId)
                    continue;
                var known = false;
                for (var n = 0; n < names.Length; n++)
                {
                    if (names[n] != clip)
                        continue;
                    known = true;
                    break;
                }

                if (!known)
                    continue;
                first = clip;
                break;
            }

            var order = new string[names.Length];
            var count = 0;
            order[count++] = first;
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i] == first)
                    continue;
                order[count++] = names[i];
            }

            return order;
        }

        static bool SliceOneVisibleClip()
        {
            var found = FindObjectsOfType<SpriteFramePetRenderer>();
            for (var i = 0; i < found.Length; i++)
            {
                var renderer = found[i];
                if (renderer == null || !renderer.isActiveAndEnabled)
                    continue;
                if (!renderer.TryGetWarmTarget(out var petId, out var clip, out var drawW, out var drawH))
                    continue;
                if (!FishingLayerArt.WarmupSlice(petId, clip, drawW, drawH))
                    return true;
            }

            return false;
        }

        static void CollectHeroDrawSizes(List<Vector2Int> sizes)
        {
            var found = FindObjectsOfType<SpriteFramePetRenderer>();
            for (var i = 0; i < found.Length; i++)
            {
                var renderer = found[i];
                if (renderer == null || !renderer.isActiveAndEnabled)
                    continue;
                if (!renderer.TryGetLayerDrawSize(out var drawW, out var drawH))
                    continue;
                var known = false;
                for (var j = 0; j < sizes.Count; j++)
                {
                    if (sizes[j].x == drawW && sizes[j].y == drawH)
                    {
                        known = true;
                        break;
                    }
                }
                if (!known)
                    sizes.Add(new Vector2Int(drawW, drawH));
            }
        }

        void OnPondSnapshotChanged(PondSnapshotDto _)
        {
            RefreshFishingAudio();
            PublishNativeOverlayState();
        }

        void RefreshFishingAudio()
        {
            DesktopAudioService.Instance?.ObserveFishingPhase(
                _ownFishing != null && _ownFishing.IsActive ? _ownFishing.Phase : _pondSession?.CurrentPhase);
        }

        void OnPondUserUpdated(PondUserDto user)
        {
            var own = _pondSession?.CurrentUser;
            var isOwn = user != null && own != null && user.id == own.id;
            if (isOwn)
                _ownFishing?.ReconcileFromServer();
            if (isOwn) RefreshFishingAudio();
            PublishNativeOverlayState(isOwn);
        }

        void OnPondUsersChanged()
        {
            PublishNativeOverlayState();
        }

        void OnPondFishBite(PendingFishCatchDto fishCatch)
        {
            DesktopAudioService.Play("splash");
            var duration = fishCatch != null ? fishCatch.hookDurationMs : 0;
            _ownFishing?.BeginHooked(duration);
            PublishNativeOverlayState(true);
        }

        void OnPondFishMiss(FishingMissDto _)
        {
            _ownFishing?.BeginMissEscape();
            PublishNativeOverlayState(true);
        }

        void OnPondFishCatchSettled(FishCatchSettledDto _)
        {
            _ownFishing?.ReconcileFromServer();
            PublishNativeOverlayState();
        }

        void OnPondCatchAccepted()
        {
            DesktopAudioService.CelebrateCatch();
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
            DesktopAudioService.Play("ui_error");
            _nativeOverlayError = message ?? "鱼塘操作失败。";
            if (!string.IsNullOrEmpty(message) && message.IndexOf("金币不足") >= 0)
                _shellUi?.SetStatusMessage(message);
            PublishNativeOverlayState();
        }

        void OnNativeOverlayCommand(NativeOverlayCommandDto message)
        {
            if (message == null)
                return;
            if (message.command == "play_audio")
            {
                DesktopAudioService.Play(message.text);
                return;
            }
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
                    _ownFishing?.BeginTakeSpot(message.spotId, message.commandId);
                    ExecuteOverlayCommand(
                        callback => _pondSession?.TakeSpot(message.spotId, callback), message);
                    return;
                case "leave_spot":
                    _ownFishing?.BeginLeaveSpot(message.commandId);
                    ExecuteOverlayCommand(
                        callback => _pondSession?.LeaveSpot(callback), message);
                    return;
                case "start_fishing":
                    _ownFishing?.BeginStartFishing(message.commandId);
                    ExecuteOverlayCommand(
                        callback => _pondSession?.StartFishing(
                            string.IsNullOrEmpty(message.spotId)
                                ? _pondSession?.FirstSpotId
                                : message.spotId,
                            callback), message);
                    return;
                case "groundbait_start":
                    _ownFishing?.BeginGroundbait(message.commandId);
                    ExecuteOverlayGroundbait(message.spotId, message.commandId);
                    return;
                case "stop_fishing":
                    _ownFishing?.BeginStopFishing(message.commandId);
                    ExecuteOverlayCommand(callback => _pondSession?.StopFishing(callback), message);
                    return;
                case "confirm_overlay_prompt":
                    _pondSession?.DismissOverlayHookPrompt();
                    PublishNativeOverlayState();
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
                        callback => _pondSession?.SendChat(message.text, callback), message);
                    return;
                case "debug_police_raid":
                    ExecuteOverlayPoliceRaid(message.commandId);
                    return;
                case "gameplay_debug":
                    ExecuteOverlayGameplayDebug(message.text, message.commandId);
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

        void ExecuteOverlayPoliceRaid(long commandId)
        {
            var api = _shellUi != null ? _shellUi.AuthenticatedApi : null;
            if (api == null || !api.CanUse)
            {
                _nativeOverlayError = "需先登录后再一键出警。";
                CompleteOverlayAck(commandId, "debug_police_raid", false, _nativeOverlayError);
                return;
            }

            if (_policeRaidRoutine != null)
                StopCoroutine(_policeRaidRoutine);
            _policeRaidRoutine = StartCoroutine(TriggerPoliceRaidRoutine(api, commandId));
        }

        void ExecuteOverlayGameplayDebug(string action, long commandId)
        {
            _overlayDebugCommandId = commandId;
            if (!GameplayDebugGate.IsClientEnabled())
            {
                _nativeOverlayError = "当前客户端未开启玩法 Debug。";
                FinishOverlayDebug(false);
                return;
            }

            if (string.Equals(action, "toggle", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(action))
            {
                _gameplayDebug?.Toggle();
                FinishOverlayDebug(true, "已切换 Unity Debug 面板");
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

            if (_gameplayDebug == null)
            {
                _nativeOverlayError = "玩法 Debug 未初始化。";
                FinishOverlayDebug(false);
                return;
            }
            var debugCommandId = commandId;
            _overlayDebugCommandId = 0;
            _gameplayDebug.RunAction(action, (ok, result) =>
                CompleteOverlayAck(debugCommandId, "gameplay_debug", ok, result));
        }

        long _overlayDebugCommandId;
        bool _overlayDebugLastOk;

        void FinishOverlayDebug(bool ok, string message = null)
        {
            var commandId = _overlayDebugCommandId;
            _overlayDebugCommandId = 0;
            if (commandId > 0)
                CompleteOverlayAck(commandId, "gameplay_debug", ok, message ?? _nativeOverlayError);
            else
                PublishNativeOverlayState();
        }

        void StartOverlayDebugInspect(string scope)
        {
            var api = _shellUi != null ? _shellUi.AuthenticatedApi : null;
            if (api == null || !api.CanUse)
            {
                _nativeOverlayError = "需先登录后再查看塘鱼。";
                FinishOverlayDebug(false);
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
            _overlayDebugLastOk = false;
            yield return api.GetGameplayDebugPondFish(scope, (ok, fish, pondId, spotId, message) =>
            {
                _overlayDebugLastOk = ok;
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
            FinishOverlayDebug(_overlayDebugLastOk);
        }

        void StartOverlayDebugSpotStats()
        {
            var api = _shellUi != null ? _shellUi.AuthenticatedApi : null;
            if (api == null || !api.CanUse)
            {
                _nativeOverlayError = "需先登录后再查看钓位数据。";
                FinishOverlayDebug(false);
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
            _overlayDebugLastOk = false;
            yield return api.GetGameplayDebugSpotStats((ok, report, rawJson, message) =>
            {
                _overlayDebugLastOk = ok;
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
            FinishOverlayDebug(_overlayDebugLastOk);
        }

        void StartOverlayDebugCatch(string fishId, string apiAction)
        {
            var api = _shellUi != null ? _shellUi.AuthenticatedApi : null;
            if (api == null || !api.CanUse)
            {
                _nativeOverlayError = "需先登录后再钓获测试鱼。";
                FinishOverlayDebug(false);
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
            FinishOverlayDebug(ok);
            if (ok && !autoReturned)
                yield return RefreshInventoryAfterDebug();
        }

        void StartOverlayDebugForceBite(string fishId)
        {
            var api = _shellUi != null ? _shellUi.AuthenticatedApi : null;
            if (api == null || !api.CanUse)
            {
                _nativeOverlayError = "需先登录后再强制上钩。";
                FinishOverlayDebug(false);
                return;
            }

            if (string.IsNullOrEmpty(fishId))
            {
                _nativeOverlayError = "未指定鱼 ID。";
                FinishOverlayDebug(false);
                return;
            }

            if (_overlayDebugInspectRoutine != null)
                StopCoroutine(_overlayDebugInspectRoutine);
            _ownFishing?.BeginHooked(5000, _overlayDebugCommandId);
            _overlayDebugInspectRoutine = StartCoroutine(OverlayDebugForceBiteRoutine(api, fishId));
        }

        IEnumerator OverlayDebugForceBiteRoutine(IAuthenticatedApiClient api, string fishId)
        {
            _nativeOverlayError = "正在强制上钩…";
            PublishNativeOverlayState();
            _overlayDebugLastOk = false;
            yield return api.PostGameplayDebug("force_bite", fishId, (ok, message) =>
            {
                _overlayDebugLastOk = ok;
                _nativeOverlayError = ok
                    ? (string.IsNullOrEmpty(message) ? "强制上钩成功" : message)
                    : (string.IsNullOrEmpty(message) ? "强制上钩失败" : message);
                if (!ok)
                    _ownFishing?.ClearRound();
            });
            _overlayDebugInspectRoutine = null;
            FinishOverlayDebug(_overlayDebugLastOk);
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

        IEnumerator TriggerPoliceRaidRoutine(IAuthenticatedApiClient api, long commandId)
        {
            _nativeOverlayError = "正在请求服务端出警…";
            PublishNativeOverlayState();
            var succeeded = false;
            yield return api.TriggerDebugPoliceRaid((ok, message) =>
            {
                succeeded = ok;
                _nativeOverlayError = ok ? string.Empty : (message ?? "出警失败。");
            });
            _policeRaidRoutine = null;
            CompleteOverlayAck(commandId, "debug_police_raid", succeeded,
                succeeded ? "已请求服务端出警" : _nativeOverlayError);
            PublishNativeOverlayState();
        }

        void ExecuteOverlayCommand(
            System.Action<System.Action<bool, string>> operation,
            NativeOverlayCommandDto command = null)
        {
            var commandId = command != null ? command.commandId : 0;
            var commandName = command != null ? command.command : string.Empty;
            if (_pondSession == null)
            {
                _nativeOverlayError = "鱼塘会话尚未初始化。";
                CompleteOverlayAck(commandId, commandName, false, _nativeOverlayError);
                PublishNativeOverlayState();
                return;
            }

            _nativeOverlayError = string.Empty;
            PublishNativeOverlayState();
            operation((ok, message) =>
            {
                _nativeOverlayError = ok ? string.Empty : (message ?? "操作失败。");
                CompleteOverlayAck(commandId, commandName, ok, ok ? string.Empty : _nativeOverlayError);
                PublishNativeOverlayState();
            });
        }

        void ExecuteOverlayGroundbait(string preferredId, long commandId)
        {
            if (_pondSession == null)
            {
                _nativeOverlayError = "鱼塘会话尚未初始化。";
                CompleteOverlayAck(commandId, "groundbait_start", false, _nativeOverlayError);
                PublishNativeOverlayState();
                return;
            }
            var id = ResolveGroundbaitId(preferredId);
            if (string.IsNullOrEmpty(id))
            {
                _nativeOverlayError = "尚未解锁任何窝料（需钓鱼等级 3）";
                CompleteOverlayAck(commandId, "groundbait_start", false, _nativeOverlayError);
                PublishNativeOverlayState();
                return;
            }
            _nativeOverlayError = string.Empty;
            PublishNativeOverlayState();
            _pondSession.StartGroundbait(id, (ok, message) =>
            {
                _nativeOverlayError = ok
                    ? "打窝成功，希望鱼儿能快快长大"
                    : (message ?? "打窝失败。");
                // Gold / cap errors surface as Overlay bubble via observation-style tip
                if (!ok && !string.IsNullOrEmpty(message) &&
                    (message.Contains("金币") || message.Contains("上限")))
                    QueueGroundbaitBubble(message);
                CompleteOverlayAck(commandId, "groundbait_start", ok, _nativeOverlayError);
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

        bool _overlayExitPending;
        void ExitPondFromOverlay()
        {
            if (_overlayExitPending) return;
            if (_pondSession == null)
            {
                _nativeOverlayError = "鱼塘会话尚未初始化。";
                PublishNativeOverlayState();
                return;
            }
            _overlayExitPending = true;
            _shellUi?.SetOverlayReturnPending(true);
            _ownFishing?.ClearRound();
            _nativeOverlayError = "正在退出鱼塘…";
            PublishNativeOverlayState();
            _pondSession.ExitPond((ok, message) =>
            {
                _nativeOverlayError = ok ? string.Empty : (message ?? "退出鱼塘失败。");
                if (ok)
                {
                    _ownFishing?.ClearRound();
                    StartCoroutine(SequenceOverlayReturn(
                        _nativeOverlay != null ? _nativeOverlay.ShutdownOverlayAsync(true) : System.Threading.Tasks.Task.FromResult(0),
                        () => RaiseMainWindow(ShellPanelId.Home),
                        () => (_window == null || _window.IsWindowVisible) && (_router == null || !_router.IsTransitioning),
                        () => { _overlayExitPending = false; _shellUi?.SetOverlayReturnPending(false); },
                        error =>
                        {
                            _overlayExitPending = false;
                            Debug.LogError("[NativeOverlay] Return interrupted: " + error);
                            _nativeOverlayError = "悬浮窗未能关闭，请重试退出鱼塘。";
                            // Recover access to the desktop UI, but do not open
                            // the receipt over an overlay that failed to exit.
                            RaiseMainWindow(ShellPanelId.Home);
                            _shellUi?.SetStatusMessage(_nativeOverlayError);
                        }));
                }
                else { _overlayExitPending = false; _shellUi?.SetOverlayReturnPending(false); }
                PublishNativeOverlayState();
            });
        }

        internal static IEnumerator SequenceOverlayReturn(System.Threading.Tasks.Task closed,
            Action restore, Func<bool> ready, Action completed, Action<Exception> failed)
        {
            while (!closed.IsCompleted) yield return null;
            if (closed.IsFaulted || closed.IsCanceled)
            {
                failed?.Invoke(closed.Exception?.GetBaseException() ?? new OperationCanceledException());
                yield break;
            }
            restore();
            // Always render the desktop before releasing a queued receipt,
            // including when the main window was already visible.
            do { yield return null; } while (!ready());
            completed();
        }

        void OnDestroy()
        {
            Debug.Log("[Shutdown] DesktopAppBootstrap.OnDestroy begin.");
            if (_steamAuth != null)
                _steamAuth.StateChanged -= OnSteamAuthStateChanged;
            if (_petState != null)
                _petState.StateChanged -= OnPetVisualStateChanged;
            if (_pondSession != null)
            {
                _pondSession.StateChanged -= OnPondStateChanged;
                _pondSession.SnapshotChanged -= OnPondSnapshotChanged;
                _pondSession.UserUpdated -= OnPondUserUpdated;
                _pondSession.UsersChanged -= OnPondUsersChanged;
                _pondSession.FishBiteReceived -= OnPondFishBite;
                _pondSession.FishMissReceived -= OnPondFishMiss;
                _pondSession.FishCatchSettled -= OnPondFishCatchSettled;
                _pondSession.CatchAccepted -= OnPondCatchAccepted;
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
