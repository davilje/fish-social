using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Windows system tray icon + context menu. Editor falls back to keyboard/debug menu only.
    /// </summary>
    public sealed class SystemTrayService : MonoBehaviour
    {
        public static SystemTrayService Instance { get; private set; }

        public event Action ShowRequested;
        public event Action HideRequested;
        public event Action ExitRequested;
        public bool IsReady { get; private set; }

        Thread _trayThread;
        volatile bool _running;
        volatile bool _pendingShow;
        volatile bool _pendingHide;
        volatile bool _pendingExit;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        WndProc _wndProcKeepAlive;
        IntPtr _hwndMessage;
        NOTIFYICONDATA _nid;
        bool _iconAdded;
        const uint WM_TRAYICON = 0x8001;
        const uint WM_LBUTTONUP = 0x0202;
        const uint WM_RBUTTONUP = 0x0205;
        const uint NIM_ADD = 0x00000000;
        const uint NIM_DELETE = 0x00000002;
        const uint NIF_MESSAGE = 0x00000001;
        const uint NIF_ICON = 0x00000002;
        const uint NIF_TIP = 0x00000004;
#endif

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _running = true;
            _trayThread = new Thread(TrayThreadMain)
            {
                IsBackground = true,
                Name = "FishSocialTray",
            };
            _trayThread.SetApartmentState(ApartmentState.STA);
            _trayThread.Start();
#else
            Debug.Log("[Tray] Editor/non-Windows: tray simulated via F9 show / F10 hide / F12 exit.");
#endif
        }

        void Update()
        {
            if (_pendingShow)
            {
                _pendingShow = false;
                ShowRequested?.Invoke();
            }

            if (_pendingHide)
            {
                _pendingHide = false;
                HideRequested?.Invoke();
            }

            if (_pendingExit)
            {
                _pendingExit = false;
                ExitRequested?.Invoke();
            }

#if UNITY_EDITOR || !UNITY_STANDALONE_WIN
            if (Input.GetKeyDown(KeyCode.F9))
                ShowRequested?.Invoke();
            if (Input.GetKeyDown(KeyCode.F10))
                HideRequested?.Invoke();
            if (Input.GetKeyDown(KeyCode.F12))
                ExitRequested?.Invoke();
#endif
        }

        void OnDestroy()
        {
            ShutdownTray();
        }

        void OnApplicationQuit()
        {
            ShutdownTray();
        }

        void ShutdownTray()
        {
            _running = false;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (_iconAdded)
            {
                Shell_NotifyIcon(NIM_DELETE, ref _nid);
                _iconAdded = false;
            }

            if (_hwndMessage != IntPtr.Zero)
            {
                PostMessage(_hwndMessage, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
                _hwndMessage = IntPtr.Zero;
            }
#endif
            if (_trayThread != null && _trayThread.IsAlive)
                _trayThread.Join(500);
        }

        public void RequestExit()
        {
            _pendingExit = true;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        void TrayThreadMain()
        {
            try
            {
                _wndProcKeepAlive = WindowProc;
                var wndClass = new WNDCLASS
                {
                    lpszClassName = "FishSocialTrayWnd",
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcKeepAlive),
                };
                RegisterClass(ref wndClass);
                _hwndMessage = CreateWindowEx(0, "FishSocialTrayWnd", "FishSocialTray", 0, 0, 0, 0, 0,
                    new IntPtr(-3) /* HWND_MESSAGE */, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                _nid = new NOTIFYICONDATA
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = _hwndMessage,
                    uID = 1,
                    uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                    uCallbackMessage = WM_TRAYICON,
                    hIcon = LoadIcon(IntPtr.Zero, new IntPtr(32512) /* IDI_APPLICATION */),
                    szTip = "Fish Social",
                };
                _iconAdded = Shell_NotifyIcon(NIM_ADD, ref _nid);
                IsReady = _iconAdded;

                MSG msg;
                while (_running && GetMessage(out msg, IntPtr.Zero, 0, 0))
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
            }
            catch (Exception e)
            {
                IsReady = false;
                Debug.LogError("[Tray] thread failed: " + e);
            }
        }

        IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_TRAYICON)
            {
                var mouse = (uint)lParam.ToInt64() & 0xFFFF;
                if (mouse == WM_LBUTTONUP)
                {
                    _pendingShow = true;
                }
                else if (mouse == WM_RBUTTONUP)
                {
                    ShowContextMenu(hWnd);
                }
            }
            else if (msg == 0x0011 /* WM_QUIT */)
            {
                _running = false;
            }
            else if (msg == 0x0010 /* WM_CLOSE */)
            {
                _running = false;
                PostQuitMessage(0);
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        void ShowContextMenu(IntPtr hWnd)
        {
            var menu = CreatePopupMenu();
            AppendMenu(menu, 0, 1, "显示窗口");
            AppendMenu(menu, 0, 2, "隐藏窗口");
            AppendMenu(menu, 0x0800 /* MF_SEPARATOR */, 0, null);
            AppendMenu(menu, 0, 3, "退出游戏");

            GetCursorPos(out var pt);
            SetForegroundWindow(hWnd);
            var cmd = (int)TrackPopupMenu(menu, 0x0100 /* TPM_RETURNCMD */, pt.X, pt.Y, 0, hWnd, IntPtr.Zero);
            DestroyMenu(menu);

            switch (cmd)
            {
                case 1: _pendingShow = true; break;
                case 2: _pendingHide = true; break;
                case 3: _pendingExit = true; break;
            }
        }

        delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WNDCLASS
        {
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
            int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern void PostQuitMessage(int nExitCode);

        [DllImport("user32.dll")]
        static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
#endif
    }
}
