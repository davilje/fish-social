using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Minimal Win32 helpers for window show/hide/position (Windows standalone only).
    /// </summary>
    public static class NativeWindowUtil
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const int GWL_STYLE = -16;
        const int WS_CAPTION = 0x00C00000;
        const int WS_THICKFRAME = 0x00040000;
        const int WS_MINIMIZEBOX = 0x00020000;
        const int WS_MAXIMIZEBOX = 0x00010000;
        const int WS_SYSMENU = 0x00080000;
        const int WS_POPUP = unchecked((int)0x80000000);
        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        const int SW_MINIMIZE = 6;
        const int SW_RESTORE = 9;
        const int HWND_TOP = 0;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_SHOWWINDOW = 0x0040;
        const uint SWP_FRAMECHANGED = 0x0020;
        const uint MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        static IntPtr _cachedHwnd;
        static int _windowedStyle;
        static bool _hasWindowedStyle;

        static IntPtr Hwnd
        {
            get
            {
                if (_cachedHwnd != IntPtr.Zero)
                    return _cachedHwnd;
                _cachedHwnd = GetActiveWindow();
                if (_cachedHwnd == IntPtr.Zero)
                    _cachedHwnd = FindWindow(null, Application.productName);
                return _cachedHwnd;
            }
        }

        public static bool TryShowWindow(bool visible)
        {
            var hwnd = Hwnd;
            if (hwnd == IntPtr.Zero)
                return false;
            return ShowWindow(hwnd, visible ? SW_SHOW : SW_HIDE);
        }

        public static bool TryMinimize()
        {
            var hwnd = Hwnd;
            return hwnd != IntPtr.Zero && ShowWindow(hwnd, SW_MINIMIZE);
        }

        public static bool TryRestore()
        {
            var hwnd = Hwnd;
            if (hwnd == IntPtr.Zero)
                return false;
            ShowWindow(hwnd, SW_RESTORE);
            return SetForegroundWindow(hwnd);
        }

        public static bool TryFocusWindow()
        {
            var hwnd = Hwnd;
            return hwnd != IntPtr.Zero && SetForegroundWindow(hwnd);
        }

        public static bool TryGetWindowPosition(out int x, out int y)
        {
            x = 0;
            y = 0;
            var hwnd = Hwnd;
            if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect))
                return false;
            x = rect.Left;
            y = rect.Top;
            return true;
        }

        public static bool TrySetWindowPosition(int x, int y)
        {
            var hwnd = Hwnd;
            if (hwnd == IntPtr.Zero)
                return false;
            return SetWindowPos(hwnd, new IntPtr(HWND_TOP), x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_SHOWWINDOW);
        }

        public static bool TrySetBorderless(bool enabled, bool fillMonitor = false)
        {
            var hwnd = Hwnd;
            if (hwnd == IntPtr.Zero)
                return false;

            if (enabled)
            {
                if (!_hasWindowedStyle)
                {
                    _windowedStyle = GetWindowLong(hwnd, GWL_STYLE);
                    _hasWindowedStyle = true;
                }

                SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_SYSMENU);
                var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
                {
                    // Borderless uses the work area (taskbar remains visible);
                    // fullscreen uses the complete monitor area.
                    var r = fillMonitor ? info.rcMonitor : info.rcWork;
                    SetWindowPos(hwnd, IntPtr.Zero, r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top,
                        SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
                }
                return true;
            }

            var style = _hasWindowedStyle ? _windowedStyle :
                WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU;
            SetWindowLong(hwnd, GWL_STYLE, style);
            return SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        }

        public static bool TrySetWindowed(int width, int height, bool hasPosition, int x, int y)
        {
            var hwnd = Hwnd;
            if (hwnd == IntPtr.Zero)
                return false;

            var style = _hasWindowedStyle ? _windowedStyle :
                WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU;
            SetWindowLong(hwnd, GWL_STYLE, style);
            var flags = SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW;
            return SetWindowPos(hwnd, IntPtr.Zero, hasPosition ? x : 0, hasPosition ? y : 0,
                Mathf.Max(1, width), Mathf.Max(1, height), flags);
        }
#else
        public static bool TryShowWindow(bool visible) => true;
        public static bool TryMinimize() => true;
        public static bool TryRestore() => true;
        public static bool TryFocusWindow() => true;
        public static bool TryGetWindowPosition(out int x, out int y)
        {
            x = 0;
            y = 0;
            return false;
        }

        public static bool TrySetWindowPosition(int x, int y) => true;
        public static bool TrySetBorderless(bool enabled, bool fillMonitor = false) => true;
        public static bool TrySetWindowed(int width, int height, bool hasPosition, int x, int y) => true;
#endif
    }
}
