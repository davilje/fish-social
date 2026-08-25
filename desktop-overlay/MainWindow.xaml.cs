using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
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
        bool _canAcceptCatch;
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
        string _lastPondId = string.Empty;
        DispatcherTimer _promptTimer;
        long _promptDeadlineMs;

        public MainWindow()
        {
            InitializeComponent();
            ApplyWindowSizeFromArgs();
            _safeWindow = string.Equals(
                Environment.GetEnvironmentVariable("FISH_SOCIAL_OVERLAY_SAFE_WINDOW"),
                "1",
                StringComparison.OrdinalIgnoreCase);
            if (_safeWindow)
            {
                AllowsTransparency = false;
                Background = new SolidColorBrush(Color.FromArgb(245, 27, 38, 51));
                Topmost = false;
            }
            _pipeName = ReadArgument("--pipe=");
            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            Closing += OnClosing;
            _scene = new PondScenePresenter(
                SpotLayer,
                ActorLayer,
                HoverLayer,
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
                    StateText.Text = "状态：正在选择钓位…";
                    SendCommand("take_spot", _selectedSpotId);
                }
            };
            _chat = new OverlayChatPresenter(ChatLatestPreview);
            _chatBubbles = new OverlayChatBubblePresenter(
                ChatBubbleLayer,
                playerId => _scene?.TryResolveActor(playerId));
            _chatAckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            _chatAckTimer.Tick += ChatAckTimer_OnTick;
            _promptTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _promptTimer.Tick += PromptTimer_OnTick;
        }

        void ApplyWindowSizeFromArgs()
        {
            var width = ReadIntArgument("--width=", 960);
            var height = ReadIntArgument("--height=", 560);
            if (width <= 0)
                width = 960;
            if (height <= 0)
                height = 560;
            Width = width;
            Height = height;
            PondScene.Width = width;
            PondScene.Height = height;
            SceneCanvas.Width = width;
            SceneCanvas.Height = height;
            PondBackgroundImage.Width = width;
            PondBackgroundImage.Height = height;
            GrassLayer.Width = width;
            GrassLayer.Height = height;
            SpotLayer.Width = width;
            SpotLayer.Height = height;
            ActorLayer.Width = width;
            ActorLayer.Height = height;
            HoverLayer.Width = width;
            HoverLayer.Height = height;
            ChatBubbleLayer.Width = width;
            ChatBubbleLayer.Height = height;
            ChatDockChrome.Width = Math.Max(280, width - 240);
            Canvas.SetTop(ChatDockChrome, height - (_chatDockExpanded ? 68 : 36));
        }

        void MenuToggle_OnClick(object sender, RoutedEventArgs e)
        {
            _menuExpanded = !_menuExpanded;
            MenuPanel.Visibility = _menuExpanded ? Visibility.Visible : Visibility.Collapsed;
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            Left = Math.Max(SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Right - Width - 32);
            Top = Math.Max(SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Bottom - Height - 48);

            if (string.IsNullOrWhiteSpace(_pipeName))
            {
                StateText.Text = "状态：缺少主进程管道";
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
            var sceneRect = PondScene.TransformToAncestor(this)
                .TransformBounds(new Rect(PondScene.RenderSize));
            handled = true;
            return sceneRect.Contains(local)
                ? new IntPtr(HTCLIENT)
                : new IntPtr(HTTRANSPARENT);
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
                        if (!_stopping)
                            StateText.Text = "状态：等待主界面";
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

        void HandleMessage(IpcMessage message)
        {
            if (message.Type == "state")
            {
                LatencyTrace.Write(
                    "overlay_state_received sequence=" + message.Sequence +
                    " pond=" + (message.PondId ?? string.Empty));
                if (message.Sequence <= _latestSequence)
                    return;
                _latestSequence = message.Sequence;
                StateText.Text = "状态：" + FormatFishingState(message);
                PondText.Text = "鱼塘：" +
                    (string.IsNullOrWhiteSpace(message.PondName)
                        ? "未进入" : message.PondName);
                SpotText.Text = "钓位：" + FormatSpot(message);
                ErrorText.Text = message.ErrorMessage ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(message.OwnSpotId))
                    _selectedSpotId = message.OwnSpotId;
                _scene?.Apply(message);
                ApplyFishingControls(message);
                ApplyPondChat(message);
                ApplyObservation(message);
                ApplyGuideTip(message);
                ApplyFeatureNavLock(message);
                ApplyOverlayPrompt(message);
                ApplyMainWindowRaised(message.MainWindowRaised);
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
                SendCommand("stop_fishing");
            else if (_canStartFishing)
                SendCommand("start_fishing", _selectedSpotId);
        }

        void Groundbait_OnClick(object sender, RoutedEventArgs e)
        {
            if (!_canGroundbait)
                return;
            SendCommand("groundbait_start", _selectedSpotId);
        }

        void CatchLeave_OnClick(object sender, RoutedEventArgs e)
        {
            if (_canAcceptCatch)
                SendCommand("accept_catch");
            else if (_canLeaveSpot)
            {
                StateText.Text = "状态：正在离席…";
                SendCommand("leave_spot");
            }
        }

        void ExitPond_OnClick(object sender, RoutedEventArgs e)
        {
            StateText.Text = "状态：正在退出鱼塘…";
            SendCommand("exit_pond");
        }

        void PoliceDebug_OnClick(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = "正在请求服务端出警…";
            SendCommand("debug_police_raid");
        }

        void GameplayDebug_OnClick(object sender, RoutedEventArgs e)
        {
            SetGameplayDebugModalOpen(GameplayDebugModal.Visibility != Visibility.Visible);
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape &&
                GameplayDebugModal != null &&
                GameplayDebugModal.Visibility == Visibility.Visible)
            {
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
        void GameplayDebugFishEpic_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("grant_fish_epic_plus");
        void GameplayDebugFee_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("advance_fee_2h");
        void GameplayDebugResetDuration_OnClick(object sender, RoutedEventArgs e) =>
            SendGameplayDebug("reset_fishing_duration");

        void SendGameplayDebug(string action)
        {
            ErrorText.Text = "正在执行 Debug…";
            var commandId = Interlocked.Increment(ref _nextCommandId);
            var sentAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Send(new IpcMessage
            {
                Type = "command",
                Version = 1,
                Command = "gameplay_debug",
                Text = action,
                CommandId = commandId,
                SentAtMs = sentAtMs,
            });
        }

        void ApplyFishingControls(IpcMessage message)
        {
            _canStartFishing = HasAction(message, "start_fishing");
            _canStopFishing = HasAction(message, "stop_fishing");
            _canGroundbait = HasAction(message, "groundbait_start");
            _canAcceptCatch = HasAction(message, "accept_catch");
            _canLeaveSpot = HasAction(message, "leave_spot");
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

            if (_canStopFishing)
            {
                FishingToggleButton.Content = "收杆";
                FishingToggleButton.IsEnabled = true;
                FishingToggleButton.Visibility = Visibility.Visible;
            }
            else if (_canStartFishing)
            {
                FishingToggleButton.Content = "开始钓鱼";
                FishingToggleButton.IsEnabled = true;
                FishingToggleButton.Visibility = Visibility.Visible;
            }
            else
            {
                FishingToggleButton.IsEnabled = false;
                FishingToggleButton.Visibility = Visibility.Collapsed;
            }

            if (_canGroundbait)
            {
                GroundbaitButton.Visibility = Visibility.Visible;
                GroundbaitButton.IsEnabled = true;
                GroundbaitButton.Content = "打窝";
            }
            else if (string.Equals(message.FishingPhase, "groundbaiting", StringComparison.Ordinal))
            {
                GroundbaitButton.Visibility = Visibility.Visible;
                GroundbaitButton.IsEnabled = false;
                GroundbaitButton.Content = "打窝中…";
            }
            else
            {
                GroundbaitButton.Visibility = Visibility.Collapsed;
                GroundbaitButton.IsEnabled = false;
            }

            ApplyGroundbaitStatus(message);

            if (_canAcceptCatch)
            {
                CatchLeaveButton.Content = "领取鱼获";
                CatchLeaveButton.IsEnabled = true;
                CatchLeaveButton.Visibility = Visibility.Visible;
            }
            else if (_canLeaveSpot)
            {
                CatchLeaveButton.Content = "离席";
                CatchLeaveButton.IsEnabled = true;
                CatchLeaveButton.Visibility = Visibility.Visible;
            }
            else
            {
                CatchLeaveButton.IsEnabled = false;
                CatchLeaveButton.Visibility = Visibility.Collapsed;
            }

            ExitPondButton.IsEnabled = _canExitPond;
        }

        void ApplyGroundbaitStatus(IpcMessage message)
        {
            var max = message.GroundbaitMaxStack > 0 ? message.GroundbaitMaxStack : 50;
            var stack = Math.Max(0, message.GroundbaitStack);
            if (stack <= 0 &&
                !string.Equals(message.FishingPhase, "groundbaiting", StringComparison.Ordinal) &&
                !_canGroundbait)
            {
                GroundbaitStatusText.Visibility = Visibility.Collapsed;
                return;
            }

            var bitePct = message.GroundbaitBiteBonus * 100f;
            var sizeMm = message.GroundbaitSizeBonus;
            GroundbaitStatusText.Text =
                "窝 " + stack + "/" + max +
                "\n咬+" + bitePct.ToString("0.0") + "%" +
                "\n尺+" + sizeMm.ToString("0.000") + "m";
            if (message.GroundbaitBitesLeft > 0)
                GroundbaitStatusText.Text += "\n剩" + message.GroundbaitBitesLeft + "口";
            GroundbaitStatusText.Visibility = Visibility.Visible;
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

        void PondScene_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;
            if (IsUnderElement(e.OriginalSource as DependencyObject, ChatDockChrome))
                return;

            OverlayInteractionState.SceneDragging = true;
            _scene?.CancelAllHovers();

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // The window can be closed while a drag is ending.
            }
        }

        void PondScene_OnMouseMove(object sender, MouseEventArgs e)
        {
        }

        void PondScene_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            OverlayInteractionState.SceneDragging = false;
        }

        void SendCommand(string command, string spotId = null)
        {
            var commandId = Interlocked.Increment(ref _nextCommandId);
            var sentAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            LatencyTrace.Write(
                "overlay_command_sent id=" + commandId +
                " command=" + command);
            if (OpensMainWindow(command))
                ApplyMainWindowRaised(true);
            Send(new IpcMessage
            {
                Type = "command",
                Version = 1,
                Command = command,
                SpotId = spotId,
                CommandId = commandId,
                SentAtMs = sentAtMs,
            });
        }

        void ChatDockToggle_OnClick(object sender, RoutedEventArgs e)
        {
            _chatDockExpanded = !_chatDockExpanded;
            ChatDockExpanded.Visibility = _chatDockExpanded
                ? Visibility.Visible
                : Visibility.Collapsed;
            ChatDockToggle.Content = _chatDockExpanded ? "▼" : "▲";
            Canvas.SetTop(
                ChatDockChrome,
                SceneCanvas.Height - (_chatDockExpanded ? 68 : 36));
        }

        void ChatInput_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            OverlayInteractionState.SceneDragging = false;
        }

        void ChatInputBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            ChatPlaceholder.Visibility = string.IsNullOrEmpty(ChatInputBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
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
                ErrorText.Text = "实时服务未连接，请进入鱼塘后重试。";
                return;
            }

            _pendingChatText = text;
            _awaitingChatAck = true;
            UpdateChatSendEnabled();
            _chatAckTimer.Stop();
            _chatAckTimer.Start();
            SendPondChatCommand(text);
        }

        void SendPondChatCommand(string text)
        {
            var commandId = Interlocked.Increment(ref _nextCommandId);
            var sentAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            LatencyTrace.Write(
                "overlay_command_sent id=" + commandId +
                " command=send_pond_chat");
            Send(new IpcMessage
            {
                Type = "command",
                Version = 1,
                Command = "send_pond_chat",
                Text = text,
                CommandId = commandId,
                SentAtMs = sentAtMs,
            });
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
            }

            var replayHistory = !_chatHistoryPrimed;
            if (replayHistory &&
                message.RecentChats != null &&
                message.RecentChats.Length > 0)
                _chatHistoryPrimed = true;

            _chatBubbles?.ProcessMessages(message.RecentChats, replayHistory);
            _chat?.UpdateLatest(message.RecentChats);
            if (_awaitingChatAck)
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
            CtxMenuPond.IsEnabled = enabled;
            CtxMenuMap.IsEnabled = enabled;
            CtxMenuShop.IsEnabled = enabled;
            CtxMenuFriends.IsEnabled = enabled;
            CtxMenuCatch.IsEnabled = enabled;
            CtxMenuGallery.IsEnabled = enabled;
            CtxMenuProfile.IsEnabled = enabled;
            CtxMenuFeed.IsEnabled = enabled;
            CtxMenuLeaderboard.IsEnabled = enabled;
        }

        void ApplyOverlayPrompt(IpcMessage message)
        {
            var kind = message.OverlayPromptKind ?? string.Empty;
            if (string.IsNullOrWhiteSpace(kind))
            {
                OverlayPromptChrome.Visibility = Visibility.Collapsed;
                _promptDeadlineMs = 0;
                _promptTimer.Stop();
                OverlayPromptCountdown.Text = string.Empty;
                return;
            }

            OverlayPromptTitle.Text = message.OverlayPromptTitle ?? string.Empty;
            OverlayPromptBody.Text = message.OverlayPromptBody ?? string.Empty;
            OverlayPromptButton.Content = string.IsNullOrWhiteSpace(message.OverlayPromptButton)
                ? "确认"
                : message.OverlayPromptButton;
            _promptDeadlineMs = message.OverlayPromptDeadlineMs;
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
            if (string.IsNullOrWhiteSpace(ErrorText.Text))
                ErrorText.Text = "发送超时，请重试。";
        }

        void FinishChatAck(bool success)
        {
            _chatAckTimer.Stop();
            _awaitingChatAck = false;
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
                    case "fishing": return "钓鱼";
                    case "hooked": return "上钩";
                    case "catching": return "收鱼";
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
                case "waiting": return "等待";
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
            _stopping = true;
            _promptTimer?.Stop();
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
