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

        public MainWindow()
        {
            InitializeComponent();
            _pipeName = ReadArgument("--pipe=");
            SourceInitialized += OnSourceInitialized;
            Loaded += OnLoaded;
            Closing += OnClosing;
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
            var petRect = PetPanel.TransformToAncestor(this)
                .TransformBounds(new Rect(PetPanel.RenderSize));
            handled = true;
            return petRect.Contains(local)
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
            }
            else if (message.Type == "command")
            {
                switch (message.Command)
                {
                    case "hide_overlay":
                        Hide();
                        break;
                    case "quit_overlay":
                        Close();
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

        void HideOverlay_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("hide_overlay");
            Hide();
        }

        void QuitOverlay_OnClick(object sender, RoutedEventArgs e)
        {
            SendCommand("quit_overlay");
            Close();
        }

        void PetPanel_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

        void PetPanel_OnMouseMove(object sender, MouseEventArgs e)
        {
        }

        void PetPanel_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
        }

        void SendCommand(string command)
        {
            Send(new IpcMessage
            {
                Type = "command",
                Version = 1,
                Command = command,
            });
        }

        void Send(IpcMessage message)
        {
            lock (_writeLock)
            {
                if (_writer == null)
                    return;

                try
                {
                    _writer.WriteLine(message.ToJson());
                    _writer.Flush();
                }
                catch (IOException)
                {
                    _writer = null;
                }
            }
        }

        static string FormatFishingState(IpcMessage message)
        {
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
            lock (_writeLock)
            {
                _writer = null;
                _pipe?.Dispose();
            }
        }
    }
}
