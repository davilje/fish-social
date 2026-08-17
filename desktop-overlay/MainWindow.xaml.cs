using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace FishSocialOverlay
{
    public partial class MainWindow : Window
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
        bool _stopping;
        string _selectedSpotId = string.Empty;
        readonly bool _safeWindow;
        PondScenePresenter _scene;

        public MainWindow()
        {
            InitializeComponent();
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
                OwnCat,
                OwnCatImage,
                new System.Windows.Shapes.Shape[] { CatEarL, CatEarR, CatBody },
                PondBackgroundImage,
                GrassLayer,
                ShoreLayer,
                WaterLayer);
            _scene.SpotSelected += spotId =>
            {
                _selectedSpotId = spotId ?? string.Empty;
                TakeSpotButton.IsEnabled = !string.IsNullOrEmpty(_selectedSpotId);
            };
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
                if (message.Sequence < _latestSequence)
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

        void TakeSpot_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("take_spot", _selectedSpotId);
        }

        void StartFishing_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("start_fishing", _selectedSpotId);
        }

        void StopFishing_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("stop_fishing");
        }

        void AcceptCatch_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("accept_catch");
        }

        void ApplyFishingControls(IpcMessage message)
        {
            TakeSpotButton.IsEnabled = HasAction(message, "take_spot") &&
                                       !string.IsNullOrEmpty(_selectedSpotId);
            StartFishingButton.IsEnabled = HasAction(message, "start_fishing");
            StopFishingButton.IsEnabled = HasAction(message, "stop_fishing");
            AcceptCatchButton.IsEnabled = HasAction(message, "accept_catch");
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
        }

        void SendCommand(string command, string spotId = null)
        {
            if (OpensMainWindow(command))
                ApplyMainWindowRaised(true);
            Send(new IpcMessage
            {
                Type = "command",
                Version = 1,
                Command = command,
                SpotId = spotId,
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
                   command == "menu_friends" ||
                   command == "menu_catch" ||
                   command == "menu_gallery" ||
                   command == "menu_settings";
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
