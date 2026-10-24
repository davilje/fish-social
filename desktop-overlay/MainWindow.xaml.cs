using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace FishSocialOverlay
{
    public partial class MainWindow : Window, IOverlayPlayerMenuHost
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTCLIENT = 1;
        const int HTTRANSPARENT = -1;

        [StructLayout(LayoutKind.Sequential)]
        struct Win32Point
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out Win32Point lpPoint);

        readonly object _writeLock = new object();
        readonly string _pipeName;
        Thread _pipeThread;
        NamedPipeClientStream _pipe;
        StreamWriter _writer;
        long _latestSequence;
        long _nextCommandId;
        bool _stopping;
        string _selectedSpotId = string.Empty;
        readonly bool _safeWindow;
        bool _menuExpanded;
        bool _canStartFishing;
        bool _canStopFishing;
        bool _canGroundbait;
        bool _canLeaveSpot;
        bool _canExitPond;
        PondScenePresenter _scene;
        OverlayChatPresenter _chat;
        OverlayChatBubblePresenter _chatBubbles;
        DispatcherTimer _chatAckTimer;
        bool _awaitingChatAck;
        string _pendingChatText = string.Empty;
        bool _socketConnected;
        bool _chatDockExpanded;
        bool _chatHistoryPrimed;
        OverlayChatDto[] _lastRecentChats;
        string _lastPondId = string.Empty;
        DispatcherTimer _promptTimer;
        long _promptDeadlineMs;
        bool _promptAutoClosed;
        string _promptKind;
        OverlayDebugFishDto _selectedDebugFish;
        string _debugInspectFishListMode = string.Empty;
        readonly OverlayHudLayout _hudLayout = new OverlayHudLayout();
        OverlayEdgeVignette _edgeVignette;
        OverlayScenePan _scenePan;
        OverlayRenderDiagnostics _renderDiagnostics;
        bool _hudMenuMode;
        bool _hudLayoutActive;
        bool _hudChatDockMetricsValid;
        OverlayHudLayout.HudChatDockMetrics _hudChatDockMetrics;
        const double HudChatDockExpandedHeight = 69;
        const double HudChatDockCollapsedHeight = 36;
        ContextMenu _productMenu;
        OverlayViewportPreset _viewportPreset = OverlayViewportPreset.Standard;
        MenuItem[] _viewportMenuItems;
        bool _openedContextMenuThisGesture;
        OverlayPetActor _pendingPetMenu;
        bool _pendingProductMenu;
        bool _ignoreNextSceneDrag;
        readonly DispatcherTimer _hoverPollTimer;
        Point _hoverProbePoint;
        int _hoverProbeTick;
        Point _hitProbePoint;
        int _hitProbeTick;
        int _hitProbeResult = int.MinValue;
        IpcMessage _pendingState;
        int _stateDispatchQueued;
        readonly bool _coalesceStates = !string.Equals(
            Environment.GetEnvironmentVariable("FISH_SOCIAL_OVERLAY_NO_COALESCE"), "1", StringComparison.Ordinal);
        long _lastStateTraceMs;
        long _transientStatusUntilMs;
        string _lastStateError;
        string _hudLoadWarning = string.Empty;
        bool _commandAcksSupported;
        long _pendingChatCommandId;
        long _pendingSeatCommandId;
        long _pendingActionCommandId;
        string _pendingActionName = string.Empty;
        string _lastOwnAnimSig = string.Empty;
        IpcMessage _lastStateMessage;
        long _minimumPresentationSequence;
        long _pendingDebugCommandId;
        readonly Dictionary<long, string> _sentCommands = new Dictionary<long, string>();

        public void ConfigureHudChatDockMetrics(OverlayHudLayout.HudChatDockMetrics metrics)
        {
            _hudChatDockMetrics = metrics;
            _hudChatDockMetricsValid = metrics.W > 0 && metrics.CollapsedH > 0 && metrics.ExpandedH > 0;
            if (!_hudChatDockMetricsValid || ChatDockChrome == null)
                return;

            Canvas.SetLeft(ChatDockChrome, metrics.X);
            Canvas.SetTop(ChatDockChrome, metrics.Y);
            ChatDockChrome.Width = metrics.W;
            ApplyHudChatDockLayout(_chatDockExpanded);
        }

        public void ConfigureChatLogStyle(FontFamily fontFamily, double fontSize, Brush foreground)
        {
            _chat?.ConfigureLogStyle(fontFamily, fontSize, foreground);
        }

        public void ConfigureHudLayoutMode(bool hudLayoutActive)
        {
            _hudLayoutActive = hudLayoutActive;
            _hudMenuMode = hudLayoutActive;
            if (LegacyLeftHudStack != null)
                LegacyLeftHudStack.Visibility = hudLayoutActive ? Visibility.Collapsed : Visibility.Visible;
            if (LegacyRightTopHudStack != null)
                LegacyRightTopHudStack.Visibility = hudLayoutActive ? Visibility.Collapsed : Visibility.Visible;
            if (LegacyRightBottomHudStack != null)
                LegacyRightBottomHudStack.Visibility = hudLayoutActive ? Visibility.Collapsed : Visibility.Visible;
            if (!hudLayoutActive)
                return;
            MenuPanel.Visibility = Visibility.Collapsed;
            SetHudMenuExpanded(false);
            if (ChatDockExpanded != null)
                ChatDockExpanded.Visibility = Visibility.Collapsed;
            if (ChatDockChrome != null)
                ChatDockChrome.IsHitTestVisible = true;
            ApplyHudChatDockLayout(false);
        }

        void ApplyHudChatDockLayout(bool expanded)
        {
            if (!_hudLayoutActive || ChatDockChrome == null)
                return;

            _chatDockExpanded = expanded;
            _hitProbeResult = int.MinValue; // Expanded controls have new hit regions immediately.
            _chat?.SetExpanded(expanded);
            var height = _hudChatDockMetricsValid
                ? (expanded ? _hudChatDockMetrics.ExpandedH : _hudChatDockMetrics.CollapsedH)
                : (expanded ? HudChatDockExpandedHeight : HudChatDockCollapsedHeight);
            if (_hudChatDockMetricsValid)
            {
                // Keep the prefab's bottom edge fixed: expansion grows upward.
                var bottom = _hudChatDockMetrics.Y + _hudChatDockMetrics.CollapsedH;
                Canvas.SetTop(ChatDockChrome, bottom - height);
                ChatDockChrome.Height = height;
            }
            else
            {
                var bottom = Canvas.GetTop(ChatDockChrome) + ChatDockChrome.Height;
                ChatDockChrome.Height = height;
                Canvas.SetTop(ChatDockChrome, bottom - height);
            }

            var expandedVis = expanded ? Visibility.Visible : Visibility.Collapsed;
            if (ChatLogScroll != null)
                ChatLogScroll.Visibility = expandedVis;
            if (ChatLogScrollBar != null)
                ChatLogScrollBar.Visibility = expandedVis;
            if (ChatLatestPreview != null)
                ChatLatestPreview.Visibility = expanded ? Visibility.Collapsed : Visibility.Visible;
            if (ChatInputBox != null)
                ChatInputBox.Visibility = expandedVis;
            if (ChatInputPanel != null)
                ChatInputPanel.Visibility = expandedVis;
            if (ChatSendButton != null)
                ChatSendButton.Visibility = expandedVis;
            if (ChatPlaceholder != null)
            {
                ChatPlaceholder.Visibility = expanded &&
                                               string.IsNullOrEmpty(ChatInputBox?.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            if (ChatDockToggle != null)
            {
                ChatDockToggle.Content = string.Empty;
                OverlayHudLayout.SetChatToggleRotation(ChatDockToggle, expanded);
                if (_hudChatDockMetricsValid && _hudChatDockMetrics.ToggleExpandedY > 0)
                {
                    Canvas.SetTop(ChatDockToggle, expanded
                        ? _hudChatDockMetrics.ToggleExpandedY
                        : _hudChatDockMetrics.ToggleCollapsedY);
                }
            }

            if (_hudLayoutActive)
                _hudLayout.RefreshChatDockChrome(ChatDockChrome);
        }

        public void ConfigureHudMenuMode(bool hudMenuMode)
        {
            ConfigureHudLayoutMode(hudMenuMode);
        }

        void SetHudMenuExpanded(bool expanded)
        {
            var vis = expanded ? Visibility.Visible : Visibility.Collapsed;
            MenuMapButton.Visibility = vis;
            MenuShopButton.Visibility = vis;
            MenuFriendsButton.Visibility = vis;
            MenuCatchButton.Visibility = vis;
            MenuLeaderboardButton.Visibility = vis;
            MenuSettingsButton.Visibility = vis;
        }

        public MainWindow()
        {
            InitializeComponent();
            ContextMenuService.SetIsEnabled(this, false);
            ContextMenuService.SetIsEnabled(PondScene, false);
            OverlayInteractionState.MenuHoverSuppressEnded += OnMenuHoverSuppressEnded;
            _hoverPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _hoverPollTimer.Tick += OnHoverPollTick;
            _hoverPollTimer.Start();
            _productMenu = BuildProductContextMenu();
            _pipeName = ReadArgument("--pipe=");
            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            Closing += OnClosing;
            _scene = new PondScenePresenter(
                SpotLayer,
                ActorLayer,
                HoverLayer,
                DecorLayer,
                PondBackgroundImage,
                GrassLayer,
                ShoreLayer,
                WaterLayer,
                this);
            _scene.SpotSelected += spotId =>
            {
                _selectedSpotId = spotId ?? string.Empty;
                if (!string.IsNullOrEmpty(_selectedSpotId))
                {
                    PlayInteractionAudio("ui_click");
                    _scene.PredictOwnSeat(_selectedSpotId);
                    _pendingSeatCommandId = SendCommand("take_spot", _selectedSpotId);
                    if (_lastStateMessage != null)
                        ApplyFishingControls(_lastStateMessage);
                }
            };
            _scene.LocalPresentationChanged += OnLocalPresentationChanged;
            _scene.SceneContentChanged += OnSceneContentChanged;
            _scene.ScenePresentationChanged += RefreshScenePresentation;
            SceneFadeHost.Visibility = Visibility.Collapsed;
            _chat = new OverlayChatPresenter(ChatLatestPreview, ChatLogScroll, ChatLogList);
            _chat.SetExpanded(_chatDockExpanded);
            _chatBubbles = new OverlayChatBubblePresenter(
                ChatBubbleLayer,
                playerId => _scene?.TryResolveActor(playerId),
                () => _scene?.TryResolveOwnActor());
            _chatAckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _chatAckTimer.Tick += ChatAckTimer_OnTick;
            _promptTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _promptTimer.Tick += PromptTimer_OnTick;
            _hudLayout.TryApply(this, SceneCanvas);
            if (!_hudLayout.IsActive && !string.IsNullOrEmpty(_hudLayout.LastLoadError))
            {
                _hudLoadWarning = "HUD 未加载：" + _hudLayout.LastLoadError + "（已用 XAML 布局）";
                LatencyTrace.Write("overlay_hud_fallback reason=" + _hudLayout.LastLoadError.Replace('\n', ' '));
                ShowStatusText(string.Empty);
            }
            // Vertical mask → SceneFadeHost; horizontal → ScenePanViewport (viewport-sized).
            // Never put both OpacityMasks on one element — the second overwrites the first.
            _edgeVignette = new OverlayEdgeVignette(SceneFadeHost, ScenePanViewport);
            _scenePan = new OverlayScenePan(SceneContentCanvas);
            _scenePan.PanActivated += () => PlayInteractionAudio("ui_tick");
            _scenePan.ViewportChanged += RefreshPresentationViewport;
            IsVisibleChanged += (sender, args) => RefreshPresentationViewport();
            StateChanged += (sender, args) => RefreshPresentationViewport();
            DpiChanged += (sender, args) => Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                new Action(() => _edgeVignette?.ApplySize(_scenePan.ViewportWidth, _scenePan.ViewportHeight)));
            _scenePan.AttachButtons(BtnPanLeft, BtnPanRight);
            ApplyInitialViewport();
            ApplySceneContentSize(_scene.ContentWidth, _scene.ContentHeight, resetPan: true);
            _safeWindow = string.Equals(
                Environment.GetEnvironmentVariable("FISH_SOCIAL_OVERLAY_SAFE_WINDOW"),
                "1",
                StringComparison.OrdinalIgnoreCase);
            if (_safeWindow)
            {
                AllowsTransparency = false;
                Background = CozyTheme.Cream;
                Topmost = false;
            }
        }

        void RefreshPresentationViewport()
        {
            if (_scenePan == null || _scene == null) return;
            var visible = IsVisible && WindowState != WindowState.Minimized;
            OverlayAnimationClock.SetVisible(visible);
            _renderDiagnostics?.SetVisible(visible);
            _scene.SetPresentationViewport(new Rect(-_scenePan.OffsetX,
                Math.Max(0, _scene.ContentHeight - _scenePan.ViewportHeight),
                _scenePan.ViewportWidth, _scenePan.ViewportHeight), visible);
        }

        void RefreshScenePresentation()
        {
            SceneFadeHost.Visibility = _scene.IsPresentationReady && _latestSequence >= _minimumPresentationSequence
                ? Visibility.Visible : Visibility.Collapsed;
        }

        void ApplyInitialViewport()
        {
            var stored = OverlayViewportStore.LoadOrDefault();
            var argWidth = ReadIntArgument("--width=", 0);
            var argHeight = ReadIntArgument("--height=", 0);
            OverlayViewportPreset preset;
            if (!string.Equals(stored.Id, OverlayViewportPreset.Standard.Id, StringComparison.Ordinal))
                preset = stored;
            else if (argWidth > 0 && argHeight > 0)
                preset = OverlayViewportPreset.FromSize(argWidth, argHeight);
            else
                preset = OverlayViewportPreset.Standard;
            ApplyViewportSize(preset, persist: false, anchorOnScreen: false);
        }

        void ApplyViewportSize(OverlayViewportPreset preset, bool persist, bool anchorOnScreen)
        {
            _viewportPreset = preset;
            var width = preset.Width;
            var height = preset.Height;
            if (anchorOnScreen)
                OverlayViewportStore.AnchorResize(this, width, height);
            else
            {
                Width = width;
                Height = height;
            }

            PondScene.Width = width;
            PondScene.Height = height;
            SceneFadeHost.Width = width;
            SceneFadeHost.Height = height;
            ScenePanViewport.Width = width;
            ScenePanViewport.Height = height;
            SceneCanvas.Width = width;
            SceneCanvas.Height = height;
            HoverLayer.Width = width;
            HoverLayer.Height = height;
            ChatBubbleLayer.Width = width;
            ChatBubbleLayer.Height = height;
            _edgeVignette?.ApplySize(width, height);
            _scenePan?.SetViewportSize(width, height);
            _scene?.SetRuntimeViewport(width, height);
            ApplySceneContentSize(
                _scene != null ? _scene.ContentWidth : width,
                _scene != null ? _scene.ContentHeight : OverlayScenePan.DesignViewportHeight,
                resetPan: false);

            if (preset.CollapseMenuRail)
            {
                _menuExpanded = false;
                if (_hudMenuMode)
                    SetHudMenuExpanded(false);
            }

            if (_hudLayoutActive)
                _hudLayout.Relayout(this, width, height);
            else if (ChatDockChrome != null)
            {
                ChatDockChrome.Width = Math.Max(280, width - 240);
                Canvas.SetTop(ChatDockChrome, height - (_chatDockExpanded ? 68 : 36));
            }

            if (persist)
                OverlayViewportStore.Save(preset);
            if (anchorOnScreen)
                RecenterOwnSeat();
        }

        void RecenterOwnSeat()
        {
            if (_scenePan == null || _scene == null)
                return;
            if (_scene.TryGetFocusWorldX(_selectedSpotId, out var worldX))
                _scenePan.TryCenterOnWorldX(worldX, force: true);
        }

        void OnSceneContentChanged()
        {
            ApplySceneContentSize(_scene.ContentWidth, _scene.ContentHeight, resetPan: true);
            RecenterOwnSeat();
        }

        void ApplySceneContentSize(double sceneWidth, double sceneHeight, bool resetPan)
        {
            var viewW = _scenePan != null
                ? _scenePan.ViewportWidth
                : OverlayViewportPreset.Standard.Width;
            var w = Math.Max(viewW, sceneWidth);
            var h = Math.Max(1, sceneHeight);
            SceneContentCanvas.Width = w;
            SceneContentCanvas.Height = h;
            PondBackgroundImage.Width = w;
            PondBackgroundImage.Height = h;
            GrassLayer.Width = w;
            GrassLayer.Height = h;
            DecorLayer.Width = w;
            DecorLayer.Height = h;
            SpotLayer.Width = w;
            SpotLayer.Height = h;
            ActorLayer.Width = w;
            ActorLayer.Height = h;
            _scenePan?.SetSceneSize(w, h, resetPan);
        }

        void SyncScenePanFocus(IpcMessage message)
        {
            if (_scenePan == null || message == null)
                return;
            if (!_scenePan.CanPan)
                return;
            if (_scene != null &&
                _scene.TryGetFocusWorldX(message.OwnSpotId, out var worldX))
                _scenePan.TryCenterOnWorldX(worldX);
        }

        void MenuToggle_OnClick(object sender, RoutedEventArgs e)
        {
            _menuExpanded = !_menuExpanded;
            if (_hudMenuMode)
                SetHudMenuExpanded(_menuExpanded);
            else
                MenuPanel.Visibility = _menuExpanded ? Visibility.Visible : Visibility.Collapsed;
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            Left = Math.Max(SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Right - Width - 32);
            Top = Math.Max(SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Bottom - Height - 48);
            if (_hudLayoutActive)
                _hudLayout.Relayout(this, _viewportPreset.Width, _viewportPreset.Height);

            if (string.IsNullOrWhiteSpace(_pipeName))
            {
                ShowTransientStatus("缺少主进程连接，请从游戏内进入鱼塘。");
                return;
            }

            _pipeThread = new Thread(PipeLoop)
            {
                IsBackground = true,
                Name = "FishSocialOverlayPipe",
            };
            _pipeThread.Start();
        }

        void OnSourceInitialized(object sender, EventArgs e)
        {
            _edgeVignette?.ApplySize(_scenePan.ViewportWidth, _scenePan.ViewportHeight);
            _renderDiagnostics = new OverlayRenderDiagnostics(this, () => _edgeVignette?.Mode ?? "dual");
            _renderDiagnostics.SetVisible(IsVisible && WindowState != WindowState.Minimized);
            if (_safeWindow)
                return;

            var source = (HwndSource)PresentationSource.FromVisual(this);
            source?.AddHook(WindowHook);
        }

        IntPtr WindowHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam,
            ref bool handled)
        {
            if (message != WM_NCHITTEST)
                return IntPtr.Zero;

            var screenX = (short)(lParam.ToInt64() & 0xffff);
            var screenY = (short)((lParam.ToInt64() >> 16) & 0xffff);
            var local = PointFromScreen(new Point(screenX, screenY));
            var tick = Environment.TickCount;
            if (_hitProbeResult != int.MinValue && unchecked(tick - _hitProbeTick) < 24
                && Math.Abs(local.X - _hitProbePoint.X) <= 4
                && Math.Abs(local.Y - _hitProbePoint.Y) <= 4)
            {
                handled = true;
                return new IntPtr(_hitProbeResult);
            }

            var result = OverlayClickThrough.IsClientHit(this, local) ? HTCLIENT : HTTRANSPARENT;
            _hitProbePoint = local;
            _hitProbeTick = tick;
            _hitProbeResult = result;
            handled = true;
            return new IntPtr(result);
        }

        void PipeLoop()
        {
            while (!_stopping)
            {
                try
                {
                    using (var pipe = new NamedPipeClientStream(
                               ".",
                               _pipeName,
                               PipeDirection.InOut,
                               PipeOptions.Asynchronous))
                    {
                        _pipe = pipe;
                        pipe.Connect(3000);
                        using (var reader = new StreamReader(pipe))
                        using (var writer = new StreamWriter(pipe) { AutoFlush = true })
                        {
                            lock (_writeLock)
                            {
                                _writer = writer;
                            }

                            Send(new IpcMessage { Type = "hello", Version = 1 });
                            string line;
                            while (!_stopping && pipe.IsConnected &&
                                   (line = reader.ReadLine()) != null)
                            {
                                var message = IpcMessage.Parse(line);
                                if (message == null || message.Version != 1)
                                    continue;

                                if (_coalesceStates && message.Type == "state")
                                {
                                    QueueStateDispatch(message);
                                    continue;
                                }

                                Dispatcher.BeginInvoke(new Action(() =>
                                    HandleMessage(message)));
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!_stopping) ShowTransientStatus("连接已断开，正在等待主界面重连。");
                    }));
                }
                finally
                {
                    lock (_writeLock)
                    {
                        _writer = null;
                    }
                    _pipe = null;
                }

                if (!_stopping)
                    Thread.Sleep(1000);
            }
        }

        void QueueStateDispatch(IpcMessage message)
        {
            Interlocked.Exchange(ref _pendingState, message);
            // Own fishing phase / bite / leave must not sit behind Background work.
            if (IsUrgentOwnAnimState(message))
            {
                Volatile.Write(ref _stateDispatchQueued, 1);
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(DrainPendingState));
                return;
            }

            if (Interlocked.CompareExchange(ref _stateDispatchQueued, 1, 0) == 0)
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(DrainPendingState));
        }

        bool IsUrgentOwnAnimState(IpcMessage message)
        {
            if (message == null)
                return false;
            return !string.Equals(OwnAnimSig(message), _lastOwnAnimSig, StringComparison.Ordinal);
        }

        static string OwnAnimSig(IpcMessage message)
        {
            return (message.FishingPhase ?? string.Empty) + "|" +
                   (message.PetVisualState ?? string.Empty) + "|" +
                   (message.OwnSpotId ?? string.Empty) + "|" +
                   message.HookDeadlineMs + "|" +
                   message.GroundbaitExpiresAt;
        }

        void DrainPendingState()
        {
            Volatile.Write(ref _stateDispatchQueued, 0);
            var message = Interlocked.Exchange(ref _pendingState, null);
            if (message != null)
                HandleMessage(message);
        }

        void HandleMessage(IpcMessage message)
        {
            if (message.Type == "state")
            {
                if (message.Sequence <= _latestSequence)
                    return;
                var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                _latestSequence = message.Sequence;

                var stateError = message.ErrorMessage ?? string.Empty;
                if (!string.Equals(stateError, _lastStateError, StringComparison.Ordinal))
                {
                    _lastStateError = stateError;
                    _transientStatusUntilMs = 0;
                    ShowStatusText(stateError);
                }
                else if (_transientStatusUntilMs != 0 &&
                         DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > _transientStatusUntilMs)
                {
                    _transientStatusUntilMs = 0;
                    ShowStatusText(stateError);
                }
                if (!string.IsNullOrWhiteSpace(message.OwnSpotId))
                    _selectedSpotId = message.OwnSpotId;
                var ackTrace = ApplyCommandAcks(message);
                _scene?.Apply(message);
                _lastStateMessage = message;
                RefreshScenePresentation();
                _lastOwnAnimSig = OwnAnimSig(message);
                SyncScenePanFocus(message);
                ApplyFishingControls(message);
                ApplyPondChat(message);
                ApplyObservation(message);
                ApplyGuideTip(message);
                ApplyFeatureNavLock(message);
                ApplyOverlayPrompt(message);
                ApplyMainWindowRaised(message.MainWindowRaised);
                ApplyGameplayDebugInspect(message);
                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (ackTrace != null || nowMs - _lastStateTraceMs >= 1000)
                {
                    _lastStateTraceMs = nowMs;
                    var appliedMs = (System.Diagnostics.Stopwatch.GetTimestamp() - startedAt) * 1000.0 /
                                    System.Diagnostics.Stopwatch.Frequency;
                    LatencyTrace.Write(
                        "overlay_state_applied sequence=" + message.Sequence +
                        " pond=" + (message.PondId ?? string.Empty) +
                        " applyMs=" + appliedMs.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                        " actors=" + OverlayPetActor.ActiveActorCount +
                        " activeClips=" + OverlayFishingLayers.ActiveClipCount +
                        " cachedFrames=" + OverlayFishingLayers.CachedBitmapCount +
                        " cachePixelBytes=" + OverlayFishingLayers.CachedPixelBytes +
                        " layerDecodes=" + OverlayFishingLayers.DecodeCount +
                        " pendingDecodes=" + OverlayFishingLayers.PendingDecodes +
                        " rejectedDecodes=" + OverlayFishingLayers.RejectedDecodes +
                        " actorLayouts=" + OverlayPetActor.TotalLayoutPasses +
                        " staticArtBytes=" + OverlayStaticArt.CachedBytes +
                        " staticArtDecodes=" + OverlayStaticArt.DecodeCount +
                        " staticArtPending=" + OverlayStaticArt.PendingCount +
                        " nineSliceBytes=" + OverlayNineSlice.CachedBytes +
                        " nineSliceEntries=" + OverlayNineSlice.CachedCount +
                        (ackTrace ?? string.Empty));
                }
            }
            else if (message.Type == "command")
            {
                switch (message.Command)
                {
                    case "hide_overlay":
                        Hide();
                        break;
                    case "quit_overlay":
                        QuitProcess();
                        break;
                    case "show_overlay":
                        _minimumPresentationSequence = message.Sequence;
                        RefreshScenePresentation();
                        Show();
                        break;
                    case "request_snapshot":
                        Send(new IpcMessage
                        {
                            Type = "command",
                            Version = 1,
                            Command = "request_snapshot",
                        });
                        break;
                }
            }
        }

        void OpenMain_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("open_main");
        }

        void FishingToggle_OnClick(object sender, RoutedEventArgs e)
        {
            if (_canStopFishing)
            {
                if (_pendingActionName == "stop_fishing")
                    return;
                FishingToggleButton.IsEnabled = false;
                SendOwnAction("stop_fishing");
                return;
            }

            if (_canStartFishing)
            {
                if (_pendingActionName == "start_fishing")
                    return;
                FishingToggleButton.IsEnabled = false;
                SendOwnAction("start_fishing", _selectedSpotId);
            }
        }

        void Groundbait_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_canGroundbait || _pendingActionCommandId != 0)
                return;
            GroundbaitButton.IsEnabled = false;
            SendOwnAction("groundbait_start", _selectedSpotId);
        }

        void CatchLeave_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_canLeaveSpot || _pendingActionCommandId != 0)
                return;
            CatchLeaveButton.IsEnabled = false;
            SendOwnAction("leave_spot");
        }

        /// <summary>
        /// BUG-54: Overlay only forwards the command; Unity OwnFishingPresenter
        /// owns phase / visual / availableActions feedback.
        /// </summary>
        void SendOwnAction(string command, string spotId = null)
        {
            if (_pendingActionCommandId != 0 &&
                !(command == "stop_fishing" && _pendingActionName == "start_fishing"))
                return;
            var id = SendCommand(command, spotId);
            _pendingActionCommandId = id;
            _pendingActionName = command ?? string.Empty;
            if (_lastStateMessage != null)
                ApplyFishingControls(_lastStateMessage);
            LatencyTrace.Write(
                "overlay_action_sent id=" + id + " command=" + command);
        }

        void OnLocalPresentationChanged()
        {
            if (_lastStateMessage != null)
                ApplyFishingControls(_lastStateMessage);
        }

        void ExitPond_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("exit_pond");
        }

        void PoliceDebug_OnClick(object sender, RoutedEventArgs e)
        {
            ShowTransientStatus("正在请求服务端出警…");
            SetGameplayDebugStatus("正在请求服务端出警…");
            _pendingDebugCommandId = SendCommand("debug_police_raid");
        }

        void GameplayDebug_OnClick(object sender, RoutedEventArgs e)
        {
            var open = GameplayDebugModal.Visibility != Visibility.Visible;
            if (open)
                ShowGameplayDebugMainPage();
            SetGameplayDebugModalOpen(open);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape &&
                GameplayDebugModal != null &&
                GameplayDebugModal.Visibility == Visibility.Visible)
            {
                if (GameplayDebugDetailFlyout != null &&
                    GameplayDebugDetailFlyout.Visibility == Visibility.Visible)
                {
                    HideGameplayDebugDetailFlyout();
                    e.Handled = true;
                    return;
                }

                if (GameplayDebugInspectPage != null &&
                    GameplayDebugInspectPage.Visibility == Visibility.Visible)
                {
                    ShowGameplayDebugMainPage();
                    e.Handled = true;
                    return;
                }

                SetGameplayDebugModalOpen(false);
                e.Handled = true;
                return;
            }
            base.OnPreviewKeyDown(e);
        }

        void GameplayDebugClose_OnClick(object sender, RoutedEventArgs e)
        {
            SetGameplayDebugModalOpen(false);
        }

        void GameplayDebugInspectBack_OnClick(object sender, RoutedEventArgs e)
        {
            ShowGameplayDebugMainPage();
        }

        void GameplayDebugDetailFlyoutClose_OnClick(object sender, RoutedEventArgs e)
        {
            HideGameplayDebugDetailFlyout();
        }

        void GameplayDebugModalDim_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SetGameplayDebugModalOpen(false);
            e.Handled = true;
        }

        void GameplayDebugModalPanel_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 阻止点击弹窗内容冒泡到遮罩关闭。
            e.Handled = true;
        }

        void SetGameplayDebugModalOpen(bool open)
        {
            GameplayDebugModal.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            if (!open)
                ShowGameplayDebugMainPage();
        }

        void ShowGameplayDebugMainPage()
        {
            HideGameplayDebugDetailFlyout();
            _selectedDebugFish = null;
            _debugInspectFishListMode = string.Empty;
            if (GameplayDebugInspectPage != null)
                GameplayDebugInspectPage.Visibility = Visibility.Collapsed;
            if (GameplayDebugMainPage != null)
                GameplayDebugMainPage.Visibility = Visibility.Visible;
            ClearGameplayDebugInspectButtons();
        }

        void ShowGameplayDebugInspectPage(string title, IEnumerable<DebugInspectButtonItem> items)
        {
            if (GameplayDebugMainPage != null)
                GameplayDebugMainPage.Visibility = Visibility.Collapsed;
            if (GameplayDebugInspectPage != null)
                GameplayDebugInspectPage.Visibility = Visibility.Visible;
            HideGameplayDebugDetailFlyout();
            if (GameplayDebugInspectTitle != null)
                GameplayDebugInspectTitle.Text = title ?? "查看";
            PopulateGameplayDebugInspectButtons(items);
        }

        void ClearGameplayDebugInspectButtons()
        {
            if (GameplayDebugInspectButtonGrid == null)
                return;
            GameplayDebugInspectButtonGrid.Children.Clear();
        }

        void PopulateGameplayDebugInspectButtons(IEnumerable<DebugInspectButtonItem> items)
        {
            ClearGameplayDebugInspectButtons();
            if (GameplayDebugInspectButtonGrid == null)
                return;

            if (items == null)
            {
                GameplayDebugInspectButtonGrid.Children.Add(CreateInspectPlaceholder("加载中…"));
                return;
            }

            var list = new List<DebugInspectButtonItem>(items);
            if (list.Count == 0)
            {
                GameplayDebugInspectButtonGrid.Children.Add(CreateInspectPlaceholder("暂无数据"));
                return;
            }

            foreach (var item in list)
            {
                var button = new Button
                {
                    Style = (Style)Application.Current.FindResource("CozyButton"),
                    Content = item.Label,
                    Height = 52,
                    Margin = new Thickness(2),
                    Tag = item,
                    ToolTip = item.Label,
                };
                button.Click += GameplayDebugInspectItem_OnClick;
                GameplayDebugInspectButtonGrid.Children.Add(button);
            }
        }

        static TextBlock CreateInspectPlaceholder(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = CozyTheme.Ink,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 24, 8, 24),
            };
        }

        void GameplayDebugInspectItem_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button != null ? button.Tag as DebugInspectButtonItem : null;
            if (item == null)
                return;
            ShowGameplayDebugDetailFlyout(item);
        }

        void ShowGameplayDebugDetailFlyout(DebugInspectButtonItem item)
        {
            if (item == null || GameplayDebugDetailFlyout == null)
                return;

            _selectedDebugFish = item.Fish;
            if (GameplayDebugDetailFlyoutTitle != null)
                GameplayDebugDetailFlyoutTitle.Text = item.FlyoutTitle ?? "详情";
            if (GameplayDebugDetailFlyoutText != null)
                GameplayDebugDetailFlyoutText.Text = item.Detail ?? string.Empty;

            var canForceBite = item.Fish != null &&
                               !string.IsNullOrEmpty(item.Fish.Id) &&
                               (_debugInspectFishListMode == "pond" || _debugInspectFishListMode == "spot");
            if (GameplayDebugForceBiteButton != null)
            {
                GameplayDebugForceBiteButton.Visibility = canForceBite
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                GameplayDebugForceBiteButton.IsEnabled = canForceBite;
            }
            if (GameplayDebugInstantCatchFishButton != null)
            {
                GameplayDebugInstantCatchFishButton.Visibility = canForceBite
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                GameplayDebugInstantCatchFishButton.IsEnabled = canForceBite;
            }

            GameplayDebugDetailFlyout.Visibility = Visibility.Visible;
        }

        void HideGameplayDebugDetailFlyout()
        {
            _selectedDebugFish = null;
            if (GameplayDebugDetailFlyout != null)
                GameplayDebugDetailFlyout.Visibility = Visibility.Collapsed;
            if (GameplayDebugForceBiteButton != null)
            {
                GameplayDebugForceBiteButton.Visibility = Visibility.Collapsed;
                GameplayDebugForceBiteButton.IsEnabled = false;
            }
            if (GameplayDebugInstantCatchFishButton != null)
            {
                GameplayDebugInstantCatchFishButton.Visibility = Visibility.Collapsed;
                GameplayDebugInstantCatchFishButton.IsEnabled = false;
            }
        }

        void GameplayDebugLevelUp_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("level_up");
        void GameplayDebugLevelMax_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("level_max");
        void GameplayDebugPondUp_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("pond_level_up");
        void GameplayDebugPondMax_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("pond_level_max");
        void GameplayDebugGold_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("add_gold");
        void GameplayDebugFish_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("grant_fish");
        void GameplayDebugFishMax_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("grant_fish_max_size");
        void GameplayDebugFee_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("advance_fee_2h");
        void GameplayDebugResetDuration_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("reset_fishing_duration");

        void GameplayDebugCompleteCatch_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("complete_catch");

        void GameplayDebugCatchRed_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("catch_quality_red");

        void GameplayDebugCatchOrange_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("catch_quality_orange");

        void GameplayDebugCatchGold_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("catch_quality_gold");

        void GameplayDebugListPondFish_OnClick(object sender, RoutedEventArgs e)
        {
            _debugInspectFishListMode = "pond";
            ShowGameplayDebugInspectPage("当前鱼塘的鱼", null);
            SendGameplayDebug("list_pond_fish");
        }

        void GameplayDebugListSpotFish_OnClick(object sender, RoutedEventArgs e)
        {
            _debugInspectFishListMode = "spot";
            ShowGameplayDebugInspectPage("当前钓位的鱼", null);
            SendGameplayDebug("list_spot_fish");
        }

        void GameplayDebugSpotStats_OnClick(object sender, RoutedEventArgs e)
        {
            _debugInspectFishListMode = "spot_stats";
            ShowGameplayDebugInspectPage("钓位数据", null);
            SendGameplayDebug("spot_stats");
        }

        void GameplayDebugForceBite_OnClick(object sender, RoutedEventArgs e)
        {
            var fish = _selectedDebugFish;
            if (fish == null || string.IsNullOrEmpty(fish.Id))
            {
                SetGameplayDebugStatus("请先选择一条鱼。");
                return;
            }

            SendGameplayDebug("force_bite:" + fish.Id);
            _pendingActionCommandId = _pendingDebugCommandId;
            _pendingActionName = "force_bite";
        }

        void GameplayDebugCatchFish_OnClick(object sender, RoutedEventArgs e)
        {
            var fish = _selectedDebugFish;
            if (fish == null || string.IsNullOrEmpty(fish.Id))
            {
                SetGameplayDebugStatus("请先选择一条鱼。");
                return;
            }

            SendGameplayDebug("catch_fish:" + fish.Id);
        }

        void ApplyGameplayDebugInspect(IpcMessage message)
        {
            if (GameplayDebugModal == null || GameplayDebugModal.Visibility != Visibility.Visible)
                return;
            if (GameplayDebugInspectPage == null ||
                GameplayDebugInspectPage.Visibility != Visibility.Visible)
                return;

            var mode = message.DebugFishListMode ?? string.Empty;
            if (string.IsNullOrEmpty(mode))
                return;

            _debugInspectFishListMode = mode;
            IEnumerable<DebugInspectButtonItem> items;
            string title;

            if (string.Equals(mode, "spot_stats", StringComparison.OrdinalIgnoreCase))
            {
                title = "钓位数据";
                items = BuildSpotStatsInspectItems(message.DebugSpotStatsJson, message.DebugSpotReport);
            }
            else if (string.Equals(mode, "pond", StringComparison.OrdinalIgnoreCase))
            {
                title = "当前鱼塘的鱼";
                items = BuildFishInspectItems(message.DebugFish);
            }
            else if (string.Equals(mode, "spot", StringComparison.OrdinalIgnoreCase))
            {
                title = "当前钓位的鱼";
                items = BuildFishInspectItems(message.DebugFish);
            }
            else
                return;

            if (GameplayDebugInspectTitle != null)
                GameplayDebugInspectTitle.Text = title;
            PopulateGameplayDebugInspectButtons(items);
        }

        static IEnumerable<DebugInspectButtonItem> BuildFishInspectItems(OverlayDebugFishDto[] fish)
        {
            if (fish == null || fish.Length == 0)
                yield break;

            foreach (var entry in fish)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Id))
                    continue;
                var name = string.IsNullOrWhiteSpace(entry.SpeciesName) ? entry.SpeciesId : entry.SpeciesName;
                var quality = FormatQualityLabel(entry.Quality);
                var shortId = entry.Id.Length <= 6 ? entry.Id : entry.Id.Substring(0, 6);
                yield return new DebugInspectButtonItem
                {
                    Label = string.Format("{0}\n{1} {2:0.##}m", name, quality, entry.SizeM),
                    FlyoutTitle = name + " · " + quality,
                    Detail = FormatDebugFishDetail(entry),
                    Fish = entry,
                };
            }
        }

        static IEnumerable<DebugInspectButtonItem> BuildSpotStatsInspectItems(string json, string fallbackReport)
        {
            OverlaySpotStatsPayload payload = null;
            if (!string.IsNullOrWhiteSpace(json))
                payload = TryParseSpotStatsJson(json);

            if (payload != null && payload.ok)
            {
                yield return new DebugInspectButtonItem
                {
                    Label = "概览",
                    FlyoutTitle = "塘内概览",
                    Detail = FormatSpotOverviewDetail(payload),
                };

                if (payload.constants != null)
                {
                    yield return new DebugInspectButtonItem
                    {
                        Label = "环境\n参数",
                        FlyoutTitle = "环境参数",
                        Detail = FormatSpotConstantsDetail(payload.constants),
                    };
                }

                if (payload.spot != null)
                {
                    yield return new DebugInspectButtonItem
                    {
                        Label = "钓位\n咬钩",
                        FlyoutTitle = "当前钓位咬钩",
                        Detail = FormatSpotBiteDetail(payload.spot),
                    };

                    var contrib = payload.spot.fishContributions;
                    if (contrib != null)
                    {
                        var index = 1;
                        foreach (var row in contrib)
                        {
                            if (row == null)
                                continue;
                            var name = string.IsNullOrWhiteSpace(row.speciesId) ? "未知鱼种" : row.speciesId;
                            yield return new DebugInspectButtonItem
                            {
                                Label = string.Format("贡献 #{0}\n{1}", index, name),
                                FlyoutTitle = "贡献鱼 #" + index,
                                Detail = FormatSpotContributionDetail(row),
                            };
                            index++;
                            if (index > 12)
                                break;
                        }
                    }
                }

                yield break;
            }

            if (!string.IsNullOrWhiteSpace(fallbackReport))
            {
                yield return new DebugInspectButtonItem
                {
                    Label = "钓位\n报告",
                    FlyoutTitle = "钓位数据",
                    Detail = fallbackReport,
                };
            }
        }

        static OverlaySpotStatsPayload TryParseSpotStatsJson(string json)
        {
            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var serializer = new DataContractJsonSerializer(typeof(OverlaySpotStatsPayload));
                    return serializer.ReadObject(stream) as OverlaySpotStatsPayload;
                }
            }
            catch
            {
                return null;
            }
        }

        static string FormatSpotOverviewDetail(OverlaySpotStatsPayload payload)
        {
            var lines = new List<string>
            {
                "鱼塘：" + (payload.pondId ?? "-"),
                "钓位：" + (payload.spotId ?? "-"),
            };
            if (payload.summary != null)
            {
                lines.Add("塘内鱼总数：" + payload.summary.totalFish);
                lines.Add("平均咬钩概率：" + payload.summary.avgTickBiteChance.ToString("0.####"));
            }
            return string.Join(Environment.NewLine, lines);
        }

        static string FormatSpotConstantsDetail(OverlaySpotStatsConstants constants)
        {
            if (constants == null)
                return string.Empty;
            var lines = new List<string>
            {
                "咬钩检测间隔：" + constants.checkMs + " 毫秒",
                "活跃钓客数：" + constants.activeAnglers,
                "补鱼检测间隔：" + constants.effectiveSupplementCheckMs + " 毫秒",
            };
            if (constants.lastMigrationAt > 0)
            {
                var migrated = DateTimeOffset.FromUnixTimeMilliseconds(constants.lastMigrationAt)
                    .LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                lines.Add("上次迁徙：" + migrated);
            }
            return string.Join(Environment.NewLine, lines);
        }

        static string FormatSpotBiteDetail(OverlaySpotStatsSpot spot)
        {
            if (spot == null)
                return "无当前钓位数据。";
            return string.Join(Environment.NewLine, new[]
            {
                "钓位倍率：" + spot.spotMultiplier.ToString("0.####"),
                "本 tick 咬钩概率：" + spot.pBite.ToString("0.####"),
                "钓位鱼数量：" + spot.fishAtSpotCount,
                "当前选中鱼：" + (string.IsNullOrEmpty(spot.pickedFishId) ? "无" : spot.pickedFishId),
            });
        }

        static string FormatSpotContributionDetail(OverlaySpotStatsContribution row)
        {
            if (row == null)
                return string.Empty;
            return string.Join(Environment.NewLine, new[]
            {
                "鱼 ID：" + (row.fishId ?? "-"),
                "鱼种：" + (row.speciesId ?? "-"),
                "品质：" + FormatQualityLabel(row.quality),
                "体长：" + row.sizeM.ToString("0.##") + " 米",
                "选中占比：" + row.pickShare.ToString("0.###"),
                "钓位咬钩率：" + row.spotBiteRate.ToString("0.####"),
                "脱钩率：" + row.escapeRate.ToString("0.####"),
            });
        }

        static string FormatQualityLabel(string quality)
        {
            if (string.IsNullOrWhiteSpace(quality))
                return "未知";
            switch (quality.Trim().ToLowerInvariant())
            {
                case "gray": return "灰";
                case "green": return "绿";
                case "blue": return "蓝";
                case "purple": return "紫";
                case "gold": return "金";
                case "epic": return "史诗";
                case "legendary": return "传说";
                default:
                    return quality;
            }
        }

        static string FormatDebugFishDetail(OverlayDebugFishDto fish)
        {
            if (fish == null)
                return string.Empty;
            var born = fish.BornAt > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(fish.BornAt).LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss")
                : "-";
            return string.Join(Environment.NewLine, new[]
            {
                "鱼 ID：" + fish.Id,
                "所属鱼塘：" + fish.PondId,
                "所在钓位：" + fish.SpotId,
                "鱼种 ID：" + fish.SpeciesId,
                "鱼种名称：" + fish.SpeciesName,
                "图标：" + fish.SpeciesIcon,
                "品质：" + FormatQualityLabel(fish.Quality),
                "体长：" + fish.SizeM.ToString("0.####") + " 米",
                "出生时体长：" + fish.BirthSizeM.ToString("0.####") + " 米",
                "出生时间：" + born,
                "世代：" + fish.Generation,
                "咬钩倍率：" + fish.BiteMultiplier.ToString("0.####"),
                "脱钩倍率：" + fish.EscapeMultiplier.ToString("0.####"),
                "近体型上限：" + (fish.NearMaxSize ? "是" : "否"),
            });
        }

        sealed class DebugInspectButtonItem
        {
            public string Label { get; set; }
            public string FlyoutTitle { get; set; }
            public string Detail { get; set; }
            public OverlayDebugFishDto Fish { get; set; }
        }

        void SendGameplayDebug(string action)
        {
            if (_writer == null)
            {
                SetGameplayDebugStatus("未连接到客户端，Debug 指令未发送。");
                return;
            }
            SetGameplayDebugStatus("正在执行：" + action + " …");
            _pendingDebugCommandId = SendCommand("gameplay_debug", text: action);
        }

        void ApplyFishingControls(IpcMessage message)
        {
            // BUG-54: trust Unity presentation fields (phase / availableActions).
            var phase = message.FishingPhase ?? string.Empty;
            var hasSpot = !string.IsNullOrWhiteSpace(message.OwnSpotId) ||
                          (_scene != null && _scene.HasPredictedSeat);
            if (_scene != null && _scene.HasPredictedSeat &&
                string.IsNullOrWhiteSpace(message.OwnSpotId))
                phase = string.IsNullOrEmpty(phase) || phase == "idle" ? "seated" : phase;

            _canStartFishing = HasAction(message, "start_fishing") ||
                               (_scene != null && _scene.HasPredictedSeat &&
                                string.Equals(phase, "seated", StringComparison.Ordinal));
            _canStopFishing = HasAction(message, "stop_fishing");
            _canGroundbait = HasAction(message, "groundbait_start") ||
                             (_scene != null && _scene.HasPredictedSeat &&
                              string.Equals(phase, "seated", StringComparison.Ordinal));
            _canLeaveSpot = HasAction(message, "leave_spot") ||
                            (_scene != null && _scene.HasPredictedSeat);
            _canExitPond = HasAction(message, "exit_pond");
            var canPoliceDebug = HasAction(message, "debug_police_raid");
            PoliceDebugButton.Visibility = canPoliceDebug
                ? Visibility.Visible
                : Visibility.Collapsed;
            var canGameplayDebug = HasAction(message, "gameplay_debug");
            GameplayDebugButton.Visibility = canGameplayDebug
                ? Visibility.Visible
                : Visibility.Collapsed;
            if (!canGameplayDebug)
                SetGameplayDebugModalOpen(false);

            var showDock = hasSpot && !string.Equals(phase, "idle", StringComparison.Ordinal);
            var reelPhase = IsReelPhase(phase);
            var idleSeatPhase = IsIdleSeatPhase(phase);
            var busyStart = _pendingActionName == "start_fishing" && !reelPhase;
            var busyStop = _pendingActionName == "stop_fishing" && reelPhase;
            var busyGroundbait = _pendingActionName == "groundbait_start" &&
                                 string.Equals(phase, "groundbaiting", StringComparison.Ordinal);
            var busyLeave = _pendingActionName == "leave_spot";

            if (showDock)
            {
                FishingToggleButton.Visibility = Visibility.Visible;
                if (reelPhase)
                {
                    FishingToggleButton.Content = "收杆";
                    FishingToggleButton.IsEnabled = _canStopFishing && !busyStop;
                }
                else
                {
                    FishingToggleButton.Content = "开始钓鱼";
                    FishingToggleButton.IsEnabled = _canStartFishing && !busyStart && !busyLeave;
                }
            }
            else
            {
                FishingToggleButton.IsEnabled = false;
                FishingToggleButton.Visibility = Visibility.Collapsed;
            }

            ApplyGroundbaitStatus(
                message,
                showDock && idleSeatPhase,
                busyGroundbait || busyLeave || busyStart || _pendingActionName == "start_fishing");

            if (showDock && idleSeatPhase)
            {
                CatchLeaveButton.Content = "离席";
                CatchLeaveButton.IsEnabled = _canLeaveSpot && !busyLeave &&
                                            _pendingActionName != "start_fishing" &&
                                            _pendingActionName != "groundbait_start";
                CatchLeaveButton.Visibility = Visibility.Visible;
            }
            else
            {
                CatchLeaveButton.IsEnabled = false;
                CatchLeaveButton.Visibility = Visibility.Collapsed;
            }

            ExitPondButton.IsEnabled = _canExitPond;
        }

        void ApplyGroundbaitStatus(IpcMessage message, bool showSideButtons, bool forceDisabled = false)
        {
            if (GroundbaitStatusText != null)
                GroundbaitStatusText.Visibility = Visibility.Collapsed;

            var max = message.GroundbaitMaxStack > 0 ? message.GroundbaitMaxStack : 50;
            var stack = Math.Max(0, message.GroundbaitStack);
            var label = "打窝" + stack + "/" + max;
            GroundbaitButton.Content = label;
            GroundbaitButton.FontSize = 11;
            GroundbaitButton.Padding = new Thickness(0);

            if (showSideButtons)
            {
                GroundbaitButton.Visibility = Visibility.Visible;
                GroundbaitButton.IsEnabled = _canGroundbait && !forceDisabled;
            }
            else
            {
                GroundbaitButton.Visibility = Visibility.Collapsed;
                GroundbaitButton.IsEnabled = false;
            }
        }

        static bool IsReelPhase(string phase)
        {
            return string.Equals(phase, "baiting", StringComparison.Ordinal) ||
                   string.Equals(phase, "casting", StringComparison.Ordinal) ||
                   string.Equals(phase, "waiting", StringComparison.Ordinal) ||
                   string.Equals(phase, "hooked", StringComparison.Ordinal) ||
                   string.Equals(phase, "resolving", StringComparison.Ordinal) ||
                   string.Equals(phase, "stopping", StringComparison.Ordinal);
        }

        static bool IsIdleSeatPhase(string phase)
        {
            return string.Equals(phase, "seated", StringComparison.Ordinal) ||
                   string.Equals(phase, "groundbaiting", StringComparison.Ordinal);
        }

        static bool HasAction(IpcMessage message, string action)
        {
            if (message?.AvailableActions == null)
                return false;
            for (var i = 0; i < message.AvailableActions.Length; i++)
            {
                if (string.Equals(message.AvailableActions[i], action,
                    StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        void MenuPond_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("menu_pond");
        }

        void MenuMap_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("menu_map");
        }

        void MenuShop_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("menu_shop");
        }

        void MenuFriends_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("menu_friends");
        }

        void MenuCatch_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("menu_catch");
        }

        void MenuGallery_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("menu_gallery");
        }

        void MenuProfile_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("menu_profile");
        }

        void MenuFeed_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("menu_feed");
        }

        void MenuLeaderboard_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("menu_leaderboard");
        }

        void MenuSettings_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("menu_settings");
        }

        void HideToTray_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("hide_to_tray");
            Hide();
        }

        void QuitApp_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("quit_app");
        }

        void QuitProcess()
        {
            _stopping = true;
            NamedPipeClientStream pipe;
            lock (_writeLock)
            {
                _writer = null;
                pipe = _pipe;
                _pipe = null;
            }
            try { pipe?.Dispose(); } catch { }
            try
            {
                Application.Current?.Shutdown();
            }
            catch
            {
            }
            Environment.Exit(0);
        }

        void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;
            if (TryDismissMenusFromLeftClick())
            {
                _ignoreNextSceneDrag = IsUnderElement(e.OriginalSource as DependencyObject, PondScene);
                return;
            }
            if (ShouldSkipSceneDrag(e.OriginalSource as DependencyObject))
                return;
            if (!OverlayClickThrough.HitsPondArt(this, e.GetPosition(this)))
                return;
            if (IsUnderElement(e.OriginalSource as DependencyObject, PondScene))
                return;
            BeginSceneDrag();
        }

        void PondScene_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            CloseAllContextMenus();
            OverlayInteractionState.EndMenuHoverSuppress();
            _scene?.CancelAllHovers();
            _pendingPetMenu = null;
            _pendingProductMenu = false;

            var pondPos = e.GetPosition(PondScene);
            var actor = FindSocialPetFromSource(e.OriginalSource as DependencyObject) ??
                        FindSocialPetAt(pondPos);
            if (actor != null && actor.HasPlayerContextMenu)
            {
                _pendingPetMenu = actor;
                _openedContextMenuThisGesture = true;
                e.Handled = true;
                return;
            }
            if (ShouldSkipSceneDrag(e.OriginalSource as DependencyObject))
            {
                e.Handled = true;
                return;
            }
            if (!OverlayClickThrough.HitsPondArt(this, e.GetPosition(this)))
                return;

            _pendingProductMenu = true;
            _openedContextMenuThisGesture = true;
            e.Handled = true;
        }

        void PondScene_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_openedContextMenuThisGesture)
                return;
            _openedContextMenuThisGesture = false;
            e.Handled = true;

            var pet = _pendingPetMenu;
            var product = _pendingProductMenu;
            _pendingPetMenu = null;
            _pendingProductMenu = false;

            if (pet != null)
            {
                if (pet.TryOpenPlayerContextMenu())
                    PlayInteractionAudio("ui_open");
            }
            else if (product)
                OpenProductContextMenu();
        }

        ContextMenu BuildProductContextMenu()
        {
            var menu = new ContextMenu { StaysOpen = true };
            menu.Opened += OnProductMenuOpened;
            menu.Closed += OnProductMenuClosed;
            menu.Items.Add(CreateProductMenuItem("打开主界面", OpenMain_OnClick));
            menu.Items.Add(CreateProductMenuItem("世界地图", MenuMap_OnClick));
            menu.Items.Add(CreateProductMenuItem("商店与装备", MenuShop_OnClick));
            menu.Items.Add(CreateProductMenuItem("好友与聊天", MenuFriends_OnClick));
            menu.Items.Add(CreateProductMenuItem("鱼获/背包", MenuCatch_OnClick));
            menu.Items.Add(CreateProductMenuItem("图鉴", MenuGallery_OnClick));
            menu.Items.Add(CreateProductMenuItem("个人中心", MenuProfile_OnClick));
            menu.Items.Add(CreateProductMenuItem("动态墙", MenuFeed_OnClick));
            menu.Items.Add(CreateProductMenuItem("排行榜", MenuLeaderboard_OnClick));
            menu.Items.Add(CreateProductMenuItem("设置", MenuSettings_OnClick));
            menu.Items.Add(CreateProductMenuItem("导出 HUD JSON", ExportHudJson_OnClick));
            menu.Items.Add(new Separator());
            menu.Items.Add(BuildViewportSizeMenu());
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateProductMenuItem("隐藏到托盘", HideToTray_OnClick));
            menu.Items.Add(CreateProductMenuItem("退出", QuitApp_OnClick));
            return menu;
        }

        void OnProductMenuOpened(object sender, RoutedEventArgs e)
        {
            PlayInteractionAudio("ui_open");
            OverlayInteractionState.BeginMenuHoverSuppress();
            _scene?.CancelAllHovers();
        }

        void OnProductMenuClosed(object sender, RoutedEventArgs e)
        {
            OverlayInteractionState.EndMenuHoverSuppress();
        }

        MenuItem BuildViewportSizeMenu()
        {
            var root = new MenuItem { Header = "窗口大小" };
            CozyTheme.SetIcon(root, CozyTheme.IconPath("settings"));
            var presets = OverlayViewportPreset.All;
            _viewportMenuItems = new MenuItem[presets.Length];
            for (var i = 0; i < presets.Length; i++)
            {
                var preset = presets[i];
                var item = new MenuItem
                {
                    Header = preset.Label,
                    IsCheckable = true,
                    IsChecked = string.Equals(preset.Id, _viewportPreset.Id, StringComparison.Ordinal),
                    StaysOpenOnClick = false,
                    Tag = preset.Id,
                };
                item.Click += ViewportSizeMenu_OnClick;
                _viewportMenuItems[i] = item;
                root.Items.Add(item);
            }

            return root;
        }

        void ViewportSizeMenu_OnClick(object sender, RoutedEventArgs e)
        {
            ForceDismissAllContextMenus();
            var id = (sender as MenuItem)?.Tag as string;
            ApplyViewportSize(OverlayViewportPreset.FromId(id), persist: true, anchorOnScreen: true);
        }

        void SyncViewportMenuChecks()
        {
            if (_viewportMenuItems == null)
                return;
            foreach (var item in _viewportMenuItems)
            {
                item.IsChecked = string.Equals(
                    item.Tag as string,
                    _viewportPreset.Id,
                    StringComparison.Ordinal);
            }
        }

        MenuItem CreateProductMenuItem(string header, RoutedEventHandler click)
        {
            var item = new MenuItem { Header = header };
            CozyTheme.SetIcon(item, CozyTheme.IconPath(CozyTheme.IconFor(header)));
            item.Click += (_, __) => ForceDismissAllContextMenus();
            item.Click += click;
            return item;
        }

        void ExportHudJson_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出 Cozy HUD JSON",
                FileName = "overlay-hud.json",
                DefaultExt = ".json",
                Filter = "HUD JSON (*.json)|*.json",
                AddExtension = true,
            };
            if (dialog.ShowDialog(this) != true)
                return;
            try
            {
                _hudLayout.ExportCozyJson(dialog.FileName);
                ShowTransientStatus("HUD JSON 已导出");
            }
            catch (Exception exception)
            {
                ShowTransientStatus("HUD 导出失败：" + exception.Message);
            }
        }

        void OpenProductContextMenu()
        {
            DiscardProductMenu();
            _productMenu = BuildProductContextMenu();
            SyncViewportMenuChecks();
            OverlayInteractionState.BeginMenuHoverSuppress();
            _scene?.CancelAllHovers();
            _productMenu.PlacementTarget = PondScene;
            _productMenu.Placement = PlacementMode.MousePoint;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (_productMenu != null)
                    _productMenu.IsOpen = true;
                else
                    OverlayInteractionState.EndMenuHoverSuppress();
            }));
        }

        void DiscardProductMenu()
        {
            if (_productMenu == null)
                return;
            _productMenu.Opened -= OnProductMenuOpened;
            _productMenu.Closed -= OnProductMenuClosed;
            _productMenu.IsOpen = false;
            _productMenu = null;
        }

        void CloseAllContextMenus()
        {
            if (_productMenu != null)
                _productMenu.IsOpen = false;
            if (ActorLayer == null)
                return;
            for (var i = 0; i < ActorLayer.Children.Count; i++)
            {
                if (ActorLayer.Children[i] is OverlayPetActor actor)
                    actor.ClosePlayerContextMenu();
            }
        }

        bool AnyContextMenuIsOpen()
        {
            if (_productMenu != null && _productMenu.IsOpen)
                return true;
            return _scene != null && _scene.AnyPlayerContextMenuOpen();
        }

        bool TryDismissMenusFromLeftClick()
        {
            if (!OverlayInteractionState.MenuSuppressesHover && !AnyContextMenuIsOpen())
                return false;
            ForceDismissAllContextMenus();
            return true;
        }

        void ForceDismissAllContextMenus()
        {
            CloseAllContextMenus();
            DiscardProductMenu();
            OverlayInteractionState.EndMenuHoverSuppress();
        }

        void Window_OnMouseLeave(object sender, MouseEventArgs e)
        {
        }

        static OverlayPetActor FindSocialPetFromSource(DependencyObject source)
        {
            while (source != null)
            {
                if (source is OverlayPetActor actor && actor.HasPlayerContextMenu)
                    return actor;
                if (source is Visual || source is Visual3D)
                    source = VisualTreeHelper.GetParent(source);
                else
                    source = LogicalTreeHelper.GetParent(source);
            }

            return null;
        }

        OverlayPetActor FindSocialPetAt(Point pondLocal)
        {
            var actor = FindPetArtAt(pondLocal);
            return actor != null && actor.HasPlayerContextMenu ? actor : null;
        }

        OverlayPetActor FindPetArtAt(Point pondLocal)
        {
            if (ActorLayer == null)
                return null;
            for (var i = ActorLayer.Children.Count - 1; i >= 0; i--)
            {
                if (!(ActorLayer.Children[i] is OverlayPetActor actor))
                    continue;
                if (actor.HitTestsPetArt(pondLocal, PondScene))
                    return actor;
            }

            return null;
        }

        /// <summary>
        /// Used by WM_NCHITTEST so hover and right-clicks on any pet art reach WPF.
        /// </summary>
        public bool HitsSocialPetArt(Point windowLocal)
        {
            try
            {
                if (PondScene == null || !PondScene.IsVisible)
                    return false;
                var pondLocal = PondScene.TransformToAncestor(this).Inverse.Transform(windowLocal);
                return FindPetArtAt(pondLocal) != null;
            }
            catch
            {
                return false;
            }
        }

        void PondScene_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;
            if (_ignoreNextSceneDrag)
            {
                _ignoreNextSceneDrag = false;
                return;
            }
            if (ShouldSkipSceneDrag(e.OriginalSource as DependencyObject))
                return;
            if (!OverlayClickThrough.HitsPondArt(this, e.GetPosition(this)) &&
                !IsSceneChromeSource(e.OriginalSource as DependencyObject))
                return;
            var ownActor = _scene?.TryResolveOwnActor() as OverlayPetActor;
            var petClick = ownActor != null && ownActor.HitTestsPetArt(e.GetPosition(PondScene), PondScene);
            BeginSceneDrag(petClick);
        }

        bool ShouldSkipSceneDrag(DependencyObject source)
        {
            if (_scenePan != null && _scenePan.IsPanButton(source))
                return true;
            if (IsUnderElement(source, ChatDockChrome))
                return true;
            if (_hudLayoutActive && IsInteractiveHudTarget(source))
                return true;
            if (source is Button || source is TextBox)
                return true;
            return false;
        }

        bool IsSceneChromeSource(DependencyObject source)
        {
            return ReferenceEquals(source, PondScene) ||
                   IsUnderElement(source, SceneCanvas) ||
                   IsUnderElement(source, SceneFadeHost);
        }

        void BeginSceneDrag(bool petClick = false)
        {
            var pointerBefore = new Win32Point();
            var canMeasureClick = petClick && GetCursorPos(out pointerBefore);
            var dragCompleted = false;
            OverlayInteractionState.SceneDragging = true;
            _scene?.CancelAllHovers();
            try
            {
                DragMove();
                dragCompleted = true;
            }
            catch (InvalidOperationException)
            {
            }
            OverlayInteractionState.SceneDragging = false;
            ResyncPointerHover();
            // DragMove owns native mouse capture, so WPF may not receive button-up.
            // Compare screen positions to distinguish a cat click from a drag.
            if (canMeasureClick && dragCompleted && GetCursorPos(out var pointerAfter) &&
                Math.Abs(pointerAfter.X - pointerBefore.X) <= SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pointerAfter.Y - pointerBefore.Y) <= SystemParameters.MinimumVerticalDragDistance)
                PlayInteractionAudio("meow");
        }

        void PondScene_OnMouseMove(object sender, MouseEventArgs e)
        {
            if (OverlayInteractionState.SceneDragging ||
                OverlayInteractionState.MenuSuppressesHover)
                return;
            ResyncPointerHover();
        }

        void PondScene_OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (OverlayInteractionState.MenuSuppressesHover)
                return;
            _scene?.CancelAllHovers();
        }

        void Window_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (OverlayInteractionState.SceneDragging ||
                OverlayInteractionState.MenuSuppressesHover)
                return;
            ResyncPointerHover();
        }

        void OnMenuHoverSuppressEnded()
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(RecoverPointerHoverAfterMenu));
        }

        void RecoverPointerHoverAfterMenu()
        {
            if (OverlayInteractionState.MenuSuppressesHover)
                return;
            ForceResyncPointerHover();
        }

        void OnHoverPollTick(object sender, EventArgs e)
        {
            if (OverlayInteractionState.SceneDragging ||
                OverlayInteractionState.MenuSuppressesHover)
                return;
            if (_scene != null && _scene.NeedsHoverResync())
                ResyncPointerHover(true);
        }

        Point GetPondMousePosition()
        {
            if (PondScene == null)
                return new Point();

            try
            {
                if (GetCursorPos(out var pt))
                {
                    var screen = new Point(pt.X, pt.Y);
                    return PondScene.PointFromScreen(screen);
                }
            }
            catch
            {
            }

            return Mouse.GetPosition(PondScene);
        }

        void ResyncPointerHover(bool force = false)
        {
            if (PondScene == null || _scene == null)
                return;
            if (OverlayInteractionState.MenuSuppressesHover)
                return;
            var point = GetPondMousePosition();
            var tick = Environment.TickCount;
            if (!force && unchecked(tick - _hoverProbeTick) < 50
                && Math.Abs(point.X - _hoverProbePoint.X) < 1.5
                && Math.Abs(point.Y - _hoverProbePoint.Y) < 1.5)
                return;
            _hoverProbePoint = point;
            _hoverProbeTick = tick;
            _scene.UpdatePointerHover(point, PondScene);
        }

        void ForceResyncPointerHover()
        {
            if (PondScene == null || _scene == null)
                return;
            if (OverlayInteractionState.MenuSuppressesHover)
                return;
            _scene.ForceResyncPointerHover(GetPondMousePosition(), PondScene);
        }

        void PondScene_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            OverlayInteractionState.SceneDragging = false;
        }

        long SendCommand(string command, string spotId = null, string text = null)
        {
            var commandId = Interlocked.Increment(ref _nextCommandId);
            var sentAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            LatencyTrace.Write(
                "overlay_command_sent id=" + commandId +
                " command=" + command +
                (string.IsNullOrEmpty(text) || command == "send_pond_chat" ? string.Empty : " action=" + text));
            if (OpensMainWindow(command))
                ApplyMainWindowRaised(true);
            if (_sentCommands.Count > 64)
                _sentCommands.Clear();
            _sentCommands[commandId] = command;
            Send(new IpcMessage
            {
                Type = "command",
                Version = 1,
                Command = command,
                SpotId = spotId,
                Text = text,
                CommandId = commandId,
                SentAtMs = sentAtMs,
            });
            return commandId;
        }

        internal void PlayButtonAudio(Button button)
        {
            var cue = "ui_click";
            if (ReferenceEquals(button, MenuToggleButton))
                cue = _menuExpanded ? "ui_close" : "ui_open";
            else if (ReferenceEquals(button, ChatDockToggle))
                cue = _chatDockExpanded ? "ui_close" : "ui_open";
            else if (string.Equals(button.Content as string, "关闭", StringComparison.Ordinal))
                cue = "ui_close";
            PlayInteractionAudio(cue);
        }

        internal void PlayInteractionAudio(string cue)
        {
            if (_stopping)
                return;
            // Unity owns playback, volume settings and cooldowns for both windows.
            // Cosmetic messages do not participate in gameplay command/ack state.
            Send(new IpcMessage
            {
                Type = "command",
                Version = 1,
                Command = "play_audio",
                Text = cue,
            });
        }

        void ShowTransientStatus(string text)
        {
            _transientStatusUntilMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 4000;
            ShowStatusText(text);
        }

        void ShowStatusText(string text)
        {
            text = text ?? string.Empty;
            if (string.IsNullOrEmpty(_hudLoadWarning))
                ErrorText.Text = text;
            else
                ErrorText.Text = string.IsNullOrEmpty(text) ? _hudLoadWarning : _hudLoadWarning + "\n" + text;
        }

        string ApplyCommandAcks(IpcMessage message)
        {
            var acks = message.CommandAcks;
            if (acks == null)
                return null;
            _commandAcksSupported = true;
            StringBuilder trace = null;
            foreach (var ack in acks)
            {
                if (ack == null || !ack.Done || !_sentCommands.TryGetValue(ack.CommandId, out var command))
                    continue;
                _sentCommands.Remove(ack.CommandId);
                if (trace == null)
                    trace = new StringBuilder();
                trace.Append(" ack=").Append(ack.CommandId).Append(':').Append(ack.Ok ? "ok" : "fail");
                OnCommandAck(command, ack);
            }
            return trace?.ToString();
        }

        void OnCommandAck(string command, OverlayCommandAckDto ack)
        {
            if (ack.CommandId == _pendingChatCommandId)
            {
                _pendingChatCommandId = 0;
                FinishChatAck(ack.Ok);
                if (!ack.Ok)
                    ShowTransientStatus(string.IsNullOrWhiteSpace(ack.Message) ? "发送失败，请重试。" : ack.Message);
            }
            if (ack.CommandId == _pendingSeatCommandId)
            {
                _pendingSeatCommandId = 0;
                if (!ack.Ok)
                    _scene?.CancelOwnSeatPrediction();
            }
            if (ack.CommandId == _pendingActionCommandId)
            {
                _pendingActionCommandId = 0;
                _pendingActionName = string.Empty;
                if (!ack.Ok && !string.IsNullOrWhiteSpace(ack.Message))
                    ShowTransientStatus(ack.Message);
                if (_lastStateMessage != null)
                    ApplyFishingControls(_lastStateMessage);
            }
            if (ack.CommandId == _pendingDebugCommandId)
            {
                _pendingDebugCommandId = 0;
                SetGameplayDebugStatus(!string.IsNullOrWhiteSpace(ack.Message)
                    ? ack.Message
                    : (ack.Ok ? "执行完成" : "执行失败"));
            }
        }

        void SetGameplayDebugStatus(string text)
        {
            if (GameplayDebugStatusText != null)
                GameplayDebugStatusText.Text = text ?? string.Empty;
        }

        void ChatDockToggle_OnClick(object sender, RoutedEventArgs e)
        {
            if (_hudLayoutActive)
            {
                var next = !_chatDockExpanded;
                ApplyHudChatDockLayout(next);
                if (next)
                    _chat?.UpdateMessages(_lastRecentChats, forceScrollToEnd: true);
                return;
            }

            _chatDockExpanded = !_chatDockExpanded;
            _chat?.SetExpanded(_chatDockExpanded);

            ChatDockExpanded.Visibility = _chatDockExpanded
                ? Visibility.Visible
                : Visibility.Collapsed;
            ChatDockToggle.Content = string.Empty;
            OverlayHudLayout.SetChatToggleRotation(ChatDockToggle, _chatDockExpanded);
            Canvas.SetTop(
                ChatDockChrome,
                SceneCanvas.Height - (_chatDockExpanded ? 84 : 52));
        }

        void ChatInput_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OverlayInteractionState.SceneDragging = false;
            if (!_chatDockExpanded)
            {
                // Handle the folded dock on the preview/down route. Waiting for a bubbling
                // MouseUp loses the gesture when a child handles it or layout changes the hit target.
                ChatDockToggle_OnClick(ChatDockToggle, e);
                e.Handled = true;
                return;
            }
            if (!_hudLayoutActive || ChatInputBox == null ||
                !IsUnderElement(e.OriginalSource as DependencyObject, ChatInputPanel))
                return;
            if (e.OriginalSource is TextBox)
                return;
            ChatInputBox.Focus();
            Keyboard.Focus(ChatInputBox);
        }

        void ChatInputBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_hudLayoutActive || _chatDockExpanded)
            {
                ChatPlaceholder.Visibility = string.IsNullOrEmpty(ChatInputBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else
            {
                ChatPlaceholder.Visibility = Visibility.Collapsed;
            }

            UpdateChatSendEnabled();
        }

        void ChatInputBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                TrySendPondChat();
            }
        }

        void ChatSend_OnClick(object sender, RoutedEventArgs e)
        {
            TrySendPondChat();
        }

        void TrySendPondChat()
        {
            var text = (ChatInputBox.Text ?? string.Empty).Trim();
            if (text.Length == 0 || text.Length > 200 || _awaitingChatAck)
                return;
            if (!_socketConnected)
            {
                ShowTransientStatus("实时服务未连接，请进入鱼塘后重试。");
                return;
            }

            _pendingChatText = text;
            _awaitingChatAck = true;
            UpdateChatSendEnabled();
            _chatAckTimer.Stop();
            _chatAckTimer.Start();
            _pendingChatCommandId = SendCommand("send_pond_chat", text: text);
        }

        void ApplyObservation(IpcMessage message)
        {
            if (message?.Observation == null ||
                string.IsNullOrWhiteSpace(message.Observation.Text))
            {
                _chatBubbles?.ClearObservation();
                return;
            }
            var actor = _scene?.TryResolveActor(message.OwnUserId)
                ?? _scene?.TryResolveActor(message.OwnPlayerId)
                ?? _scene?.TryResolveOwnActor();
            _chatBubbles?.ProcessObservation(message.Observation, actor);
        }

        void ApplyPondChat(IpcMessage message)
        {
            _socketConnected = string.Equals(
                message.ConnectionState, "Connected", StringComparison.Ordinal);

            var pondId = message.PondId ?? string.Empty;
            if (!string.Equals(pondId, _lastPondId, StringComparison.Ordinal))
            {
                _lastPondId = pondId;
                _chatHistoryPrimed = false;
                _chatBubbles?.ResetPond();
                _scene?.ClearAllActorBubbles();
            }

            var replayHistory = !_chatHistoryPrimed;
            _chatBubbles?.SetOwnContext(message.OwnUserId, message.OwnPlayerId);
            _chatBubbles?.ProcessMessages(message.RecentChats, replayHistory);
            if (!_chatHistoryPrimed)
                _chatHistoryPrimed = true;
            _lastRecentChats = message.RecentChats;
            _chat?.UpdateMessages(message.RecentChats);
            if (_awaitingChatAck && !_commandAcksSupported)
            {
                if (OverlayChatPresenter.ContainsText(message.RecentChats, _pendingChatText))
                    FinishChatAck(true);
                else if (!string.IsNullOrWhiteSpace(message.ErrorMessage))
                    FinishChatAck(false);
            }

            UpdateChatSendEnabled();
        }

        void ApplyGuideTip(IpcMessage message)
        {
            var tip = message.GuideTip ?? string.Empty;
            GuideTipChrome.Visibility = Visibility.Collapsed;
            GuideTipText.Text = string.Empty;
            if (string.IsNullOrWhiteSpace(tip))
            {
                _chatBubbles?.ClearGuide();
                return;
            }

            var actor = _scene?.TryResolveActor(message.OwnUserId)
                ?? _scene?.TryResolveActor(message.OwnPlayerId);
            _chatBubbles?.ShowGuide(tip, actor);
        }

        void ApplyFeatureNavLock(IpcMessage message)
        {
            var enabled = !message.LockFeatureNav;
            MenuMapButton.IsEnabled = enabled;
            MenuShopButton.IsEnabled = enabled;
            MenuFriendsButton.IsEnabled = enabled;
            MenuCatchButton.IsEnabled = enabled;
            MenuLeaderboardButton.IsEnabled = enabled;
            SetProductMenuNavEnabled(enabled);
        }

        void SetProductMenuNavEnabled(bool enabled)
        {
            if (_productMenu == null)
                return;
            // First 9 items are feature nav entries (before the separator).
            var limit = Math.Min(9, _productMenu.Items.Count);
            for (var i = 0; i < limit; i++)
            {
                if (_productMenu.Items[i] is MenuItem item)
                    item.IsEnabled = enabled;
            }
        }

        void ApplyOverlayPrompt(IpcMessage message)
        {
            var kind = message.OverlayPromptKind ?? string.Empty;
            if (string.IsNullOrWhiteSpace(kind) || IsHookSettlePrompt(kind))
            {
                OverlayPromptChrome.Visibility = Visibility.Collapsed;
                _promptDeadlineMs = 0;
                _promptKind = string.Empty;
                _promptAutoClosed = false;
                _promptTimer.Stop();
                OverlayPromptCountdown.Text = string.Empty;
                return;
            }

            OverlayPromptTitle.Text = message.OverlayPromptTitle ?? string.Empty;
            OverlayPromptBody.Text = message.OverlayPromptBody ?? string.Empty;
            OverlayPromptButton.Content = string.IsNullOrWhiteSpace(message.OverlayPromptButton)
                ? "知道了"
                : message.OverlayPromptButton;
            _promptDeadlineMs = message.OverlayPromptDeadlineMs;
            if (!string.Equals(_promptKind, kind, StringComparison.Ordinal) ||
                OverlayPromptChrome.Visibility != Visibility.Visible)
                _promptAutoClosed = false;
            _promptKind = kind;
            OverlayPromptChrome.Visibility = Visibility.Visible;
            RefreshPromptCountdown();
            if (_promptDeadlineMs > 0)
                _promptTimer.Start();
            else
                _promptTimer.Stop();
        }

        void PromptTimer_OnTick(object sender, EventArgs e)
        {
            RefreshPromptCountdown();
        }

        void RefreshPromptCountdown()
        {
            if (_promptDeadlineMs <= 0)
            {
                OverlayPromptCountdown.Text = string.Empty;
                return;
            }

            var leftMs = _promptDeadlineMs - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var leftSec = Math.Max(0, (int)Math.Ceiling(leftMs / 1000.0));
            OverlayPromptCountdown.Text = leftSec + " 秒后自动关闭";
            if (leftSec <= 0 &&
                !_promptAutoClosed &&
                IsHookSettlePrompt(_promptKind))
            {
                _promptAutoClosed = true;
                SendCommand("confirm_overlay_prompt");
            }
        }

        static bool IsHookSettlePrompt(string kind)
        {
            return string.Equals(kind, "hook_keep", StringComparison.Ordinal) ||
                   string.Equals(kind, "hook_return", StringComparison.Ordinal) ||
                   string.Equals(kind, "hook_escape", StringComparison.Ordinal);
        }

        void OverlayPromptConfirm_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("confirm_overlay_prompt");
        }

        void ChatAckTimer_OnTick(object sender, EventArgs e)
        {
            if (!_awaitingChatAck)
                return;
            FinishChatAck(false);
            ShowTransientStatus("发送超时，请重试。");
        }

        void FinishChatAck(bool success)
        {
            _chatAckTimer.Stop();
            _awaitingChatAck = false;
            _pendingChatCommandId = 0;
            if (success)
            {
                ChatInputBox.Text = string.Empty;
                _pendingChatText = string.Empty;
            }

            UpdateChatSendEnabled();
        }

        void UpdateChatSendEnabled()
        {
            var text = (ChatInputBox.Text ?? string.Empty).Trim();
            ChatSendButton.IsEnabled =
                !_awaitingChatAck &&
                _socketConnected &&
                text.Length > 0 &&
                text.Length <= 200;
        }

        static bool IsInteractiveHudTarget(DependencyObject source)
        {
            while (source != null)
            {
                if (source is Button || source is TextBox)
                    return true;
                if (source is Visual || source is Visual3D)
                    source = VisualTreeHelper.GetParent(source);
                else
                    source = LogicalTreeHelper.GetParent(source);
            }

            return false;
        }

        static bool IsUnderElement(DependencyObject source, DependencyObject root)
        {
            while (source != null)
            {
                if (ReferenceEquals(source, root))
                    return true;
                if (source is Visual || source is Visual3D)
                    source = VisualTreeHelper.GetParent(source);
                else
                    source = LogicalTreeHelper.GetParent(source);
            }

            return false;
        }

        public void SendPlayerCommand(string command, string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
                return;

            var commandId = Interlocked.Increment(ref _nextCommandId);
            var sentAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            LatencyTrace.Write(
                "overlay_command_sent id=" + commandId +
                " command=" + command +
                " playerId=" + playerId);
            if (OpensMainWindow(command))
                ApplyMainWindowRaised(true);
            Send(new IpcMessage
            {
                Type = "command",
                Version = 1,
                Command = command,
                PlayerId = playerId,
                CommandId = commandId,
                SentAtMs = sentAtMs,
            });
        }

        void ApplyMainWindowRaised(bool raised)
        {
            if (_safeWindow)
                return;
            Topmost = !raised;
        }

        static bool OpensMainWindow(string command)
        {
            return command == "open_main" ||
                   command == "menu_pond" ||
                   command == "menu_map" ||
                   command == "menu_shop" ||
                   command == "menu_friends" ||
                   command == "menu_catch" ||
                   command == "menu_gallery" ||
                   command == "menu_profile" ||
                   command == "menu_settings" ||
                   command == "menu_feed" ||
                   command == "menu_leaderboard" ||
                   command == "player_open_profile" ||
                   command == "player_open_dm";
        }

        void Send(IpcMessage message)
        {
            var json = message.ToJson();
            ThreadPool.QueueUserWorkItem(_ => WriteLine(json));
        }

        void WriteLine(string json)
        {
            lock (_writeLock)
            {
                if (_writer == null)
                    return;

                try
                {
                    _writer.WriteLine(json);
                    _writer.Flush();
                }
                catch (IOException)
                {
                    _writer = null;
                }
            }
        }

        static string FormatSpot(IpcMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.OwnSpotId))
                return "未选择";
            var match = System.Text.RegularExpressions.Regex.Match(
                message.OwnSpotId, @"(?:^|-)spot-(\d+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[1].Value + "号位";
            return message.OwnSpotId;
        }

        static string FormatFishingState(IpcMessage message)
        {
            if (message.ConnectionState != "Connected")
                return "离线";

            var fromPhase = FormatPhaseLabel(message.FishingPhase);
            if (!string.IsNullOrEmpty(fromPhase))
                return fromPhase;

            if (!string.IsNullOrEmpty(message.PetVisualState))
            {
                switch (message.PetVisualState)
                {
                    case "idle": return "待机";
                    case "sit": return "坐下";
                    case "cast": return "抛竿";
                    case "fishing": return "钓鱼";
                    case "hooked": return "上钩";
                    case "reel": return "收杆";
                    case "catch":
                    case "catching": return "钓到鱼";
                    case "dragging": return "拖动";
                    case "offline": return "离线";
                }
            }

            return "待机";
        }

        internal static string FormatPhaseLabel(string phase)
        {
            switch (phase)
            {
                case "idle": return "待机";
                case "seated": return "坐下";
                case "baiting": return "装饵";
                case "casting": return "抛竿";
                case "waiting": return "钓鱼中";
                case "hooked": return "上钩";
                case "resolving": return "收鱼";
                case "stopping": return "收杆";
                default: return string.Empty;
            }
        }

        static string ReadArgument(string prefix)
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var arg in args)
            {
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return arg.Substring(prefix.Length);
            }
            return string.Empty;
        }

        static int ReadIntArgument(string prefix, int fallback)
        {
            var raw = ReadArgument(prefix);
            return int.TryParse(raw, out var value) ? value : fallback;
        }

        void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            OverlayInteractionState.MenuHoverSuppressEnded -= OnMenuHoverSuppressEnded;
            OverlayInteractionState.EndMenuHoverSuppress();
            _hoverPollTimer.Stop();
            _stopping = true;
            _promptTimer?.Stop();
            _chatAckTimer?.Stop();
            _chatBubbles?.HideAll();
            _scene?.Dispose();
            _edgeVignette?.Dispose();
            _renderDiagnostics?.Dispose();
            NamedPipeClientStream pipe;
            lock (_writeLock)
            {
                _writer = null;
                pipe = _pipe;
                _pipe = null;
            }
            try { pipe?.Dispose(); } catch { }
        }
    }
}
