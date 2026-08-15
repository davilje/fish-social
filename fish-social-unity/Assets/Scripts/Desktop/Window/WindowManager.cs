using System;
using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Owns display mode, size/position persistence, minimize/restore, and close-to-tray.
    /// </summary>
    public sealed class WindowManager : MonoBehaviour
    {
        public static WindowManager Instance { get; private set; }

        public WindowSettings Settings { get; private set; } = new WindowSettings();
        public bool IsWindowVisible { get; private set; } = true;

        public event Action<WindowDisplayMode> ModeChanged;
        public event Action VisibilityChanged;

        public const int LoginWidth = 480;
        public const int LoginHeight = 320;
        public const int MainDefaultWidth = 1280;
        public const int MainDefaultHeight = 720;

        float _saveTimer;
        bool _appliedOnce;
        bool _applying;
        bool _loginShell = true;

        public bool IsLoginShell => _loginShell;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Settings = WindowSettings.LoadOrDefault();
            Settings.ClampSize();
        }

        void Start()
        {
            if (_loginShell)
                ApplyLoginShell();
            else
                ApplySettings(persist: false);
            _appliedOnce = true;
        }

        public void ApplyLoginShell()
        {
            _loginShell = true;
            NativeWindowUtil.TrySetWindowed(
                LoginWidth,
                LoginHeight,
                true,
                Settings.HasPosition ? Settings.PosX : 80,
                Settings.HasPosition ? Settings.PosY : 80);
            _appliedOnce = true;
        }

        public void ApplyMainShell()
        {
            _loginShell = false;
            if (Settings.Width < 960 || Settings.Height < 540)
            {
                Settings.Width = MainDefaultWidth;
                Settings.Height = MainDefaultHeight;
            }

            ApplySettings(persist: false);
        }

        void Update()
        {
            if (_loginShell || !IsWindowVisible || Settings.Mode != WindowDisplayMode.Windowed)
                return;

            _saveTimer += Time.unscaledDeltaTime;
            if (_saveTimer < 2f)
                return;
            _saveTimer = 0f;
            CaptureWindowedGeometry();
        }

        void OnApplicationQuit()
        {
            if (IsWindowVisible && Settings.Mode == WindowDisplayMode.Windowed)
                CaptureWindowedGeometry(save: true);
        }

        public void SetMode(WindowDisplayMode mode, bool persist = true)
        {
            if (_applying)
                return;

            if (mode == Settings.Mode && _appliedOnce)
            {
                if (persist)
                    Settings.Save();
                return;
            }

            // Preserve the user's normal-window geometry before leaving it.
            if (Settings.Mode == WindowDisplayMode.Windowed && mode != WindowDisplayMode.Windowed)
                CaptureWindowedGeometry(save: true);

            Settings.Mode = mode;
            ApplySettings(persist);
            ModeChanged?.Invoke(mode);
        }

        public void ApplySettings(bool persist)
        {
            if (_applying)
                return;

            _applying = true;
            Settings.ClampSize();

            try
            {
                switch (Settings.Mode)
                {
                    case WindowDisplayMode.Fullscreen:
                        // Win32 owns the entire shell; avoid Unity's asynchronous fullscreen API.
                        NativeWindowUtil.TrySetBorderless(true, fillMonitor: true);
                        break;
                    case WindowDisplayMode.Borderless:
                        // Borderless fills the work area, leaving the taskbar visible.
                        NativeWindowUtil.TrySetBorderless(true, fillMonitor: false);
                        break;
                    default:
                        NativeWindowUtil.TrySetWindowed(
                            Settings.Width,
                            Settings.Height,
                            Settings.HasPosition,
                            Settings.PosX,
                            Settings.PosY);
                        break;
                }

                _appliedOnce = true;
                if (persist)
                    Settings.Save();
            }
            finally
            {
                _applying = false;
            }
        }

        public void HideToTray()
        {
            if (!IsWindowVisible)
                return;

            if (!_loginShell && Settings.Mode == WindowDisplayMode.Windowed)
                CaptureWindowedGeometry(save: true);

            IsWindowVisible = false;
            NativeWindowUtil.TryShowWindow(false);
            BackgroundRenderGate.SetHidden(true);
            VisibilityChanged?.Invoke();
        }

        public void ShowFromTray()
        {
            if (IsWindowVisible)
            {
                NativeWindowUtil.TryFocusWindow();
                return;
            }

            IsWindowVisible = true;
            NativeWindowUtil.TryShowWindow(true);
            if (_loginShell)
                ApplyLoginShell();
            else
                ApplySettings(persist: false);
            NativeWindowUtil.TryFocusWindow();
            BackgroundRenderGate.SetHidden(false);
            VisibilityChanged?.Invoke();
        }

        public void Minimize()
        {
            NativeWindowUtil.TryMinimize();
        }

        public void Restore()
        {
            ShowFromTray();
            NativeWindowUtil.TryRestore();
        }

        void CaptureWindowedGeometry(bool save = true)
        {
            if (_loginShell || Screen.fullScreenMode != FullScreenMode.Windowed)
                return;

            Settings.Width = Mathf.Max(960, Screen.width);
            Settings.Height = Mathf.Max(540, Screen.height);
            if (NativeWindowUtil.TryGetWindowPosition(out var x, out var y))
            {
                Settings.PosX = x;
                Settings.PosY = y;
                Settings.HasPosition = true;
            }

            if (save)
                Settings.Save();
        }
    }
}
