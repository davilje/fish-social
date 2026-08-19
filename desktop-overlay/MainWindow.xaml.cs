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

        void ApplyFishingControls(IpcMessage message)
        {
            _canStartFishing = HasAction(message, "start_fishing");
            _canStopFishing = HasAction(message, "stop_fishing");
            _canAcceptCatch = HasAction(message, "accept_catch");
            _canLeaveSpot = HasAction(message, "leave_spot");
            _canExitPond = HasAction(message, "exit_pond");

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
            if (!string.IsNullOrEmpty(message.PetVisualState))
            {
                switch (message.PetVisualState)
                {
                    case "idle": return "待机";
                    case "fishing": return "钓鱼";
                    case "hooked": return "咬钩";
                    case "catching": return "收鱼";
                    case "dragging": return "拖动";
                    case "offline": return "离线";
                }
            }

            switch (message.FishingPhase)
            {
                case "baiting":
                case "casting":
                case "waiting":
                    return "钓鱼";
                case "hooked":
                    return "咬钩";
                case "resolving":
                    return "收鱼";
                default:
                    return message.ConnectionState == "Connected" ? "待机" : "离线";
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
