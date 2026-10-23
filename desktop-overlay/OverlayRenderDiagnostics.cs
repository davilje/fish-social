using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace FishSocialOverlay
{
    // Opt-in: CompositionTarget.Rendering can itself keep the compositor active.
    // These are callback gaps, not measured GPU frame duration or presentation FPS.
    internal sealed class OverlayRenderDiagnostics : IDisposable
    {
        readonly Window _window;
        readonly Func<string> _maskMode;
        readonly bool _enabled = Environment.GetEnvironmentVariable("FISH_SOCIAL_OVERLAY_PROFILE") == "1";
        readonly Stopwatch _watch = Stopwatch.StartNew();
        readonly List<double> _gaps = new List<double>(8192);
        double _last, _report;
        bool _attached;
        public OverlayRenderDiagnostics(Window window, Func<string> maskMode)
        {
            _window = window; _maskMode = maskMode;
            var dpi = VisualTreeHelper.GetDpi(window);
            LatencyTrace.Write("overlay_render_environment tier=" + (RenderCapability.Tier >> 16) +
                " dpiX=" + F(dpi.DpiScaleX) + " dpiY=" + F(dpi.DpiScaleY) +
                " transparent=" + window.AllowsTransparency + " profile=" + _enabled);
        }
        public void SetVisible(bool visible)
        {
            var attach = visible && _enabled;
            if (_attached == attach) return;
            _attached = attach;
            if (attach) CompositionTarget.Rendering += OnRendering;
            else CompositionTarget.Rendering -= OnRendering;
            _last = 0; _report = _watch.Elapsed.TotalMilliseconds; _gaps.Clear();
        }
        void OnRendering(object sender, EventArgs e)
        {
            var now = _watch.Elapsed.TotalMilliseconds;
            if (_last > 0 && _gaps.Count < 8192) _gaps.Add(now - _last);
            _last = now;
            if (now - _report < 5000 || _gaps.Count == 0) return;
            _gaps.Sort();
            var dpi = VisualTreeHelper.GetDpi(_window);
            using (var process = Process.GetCurrentProcess())
                LatencyTrace.Write("overlay_render_sample mask=" + _maskMode() + " samples=" + _gaps.Count +
                    " callbackGapP95Ms=" + F(_gaps[(int)Math.Ceiling(_gaps.Count * .95) - 1]) +
                    " callbackGapMaxMs=" + F(_gaps[_gaps.Count - 1]) +
                    " dpiX=" + F(dpi.DpiScaleX) + " viewport=" + F(_window.ActualWidth) + "x" + F(_window.ActualHeight) +
                    " privateBytes=" + process.PrivateMemorySize64 + " cpuTotalMs=" + F(process.TotalProcessorTime.TotalMilliseconds));
            _gaps.Clear(); _report = now;
        }
        static string F(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
        public void Dispose() { if (_attached) CompositionTarget.Rendering -= OnRendering; _attached = false; _gaps.Clear(); }
    }
}
