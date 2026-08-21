using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FishSocialOverlay
{
    /// <summary>
    /// 64×64 pond pet: default status as text; hover card renders on HoverLayer (above all actors).
    /// </summary>
    public sealed class OverlayPetActor : Grid
    {
        public const double BodySize = 64;
        const double RingSize = 56;
        const double RingCircumference = Math.PI * RingSize;

        readonly IOverlayHoverHost _hoverHost;
        IOverlayPlayerMenuHost _menuHost;
        string _playerId = string.Empty;
        bool _isBot;
        readonly Image _image;
        readonly Canvas _placeholder;
        readonly Shape[] _tintShapes;
        readonly TextBlock _nickname;
        readonly Grid _statusRow;
        readonly Ellipse _hookRing;
        readonly TextBlock _statusLabel;
        readonly StackPanel _content;
        readonly DispatcherTimer _frameTimer;
        readonly DispatcherTimer _refreshTimer;
        readonly DispatcherTimer _tooltipTimer;
        bool _pointerInside;
        bool _hoverShown;
        double _centerX;
        double _centerY;
        ImageSource[] _frames = Array.Empty<ImageSource>();
        int _frameIndex;
        string _visualState = string.Empty;
        string _fishingPhase = string.Empty;
        long _sessionAnchorMs;
        long _hookDeadlineMs;
        long _hookTotalMs;

        public string ActorKey { get; }

        public OverlayPetActor(string actorKey, IOverlayHoverHost hoverHost)
        {
            ActorKey = actorKey;
            _hoverHost = hoverHost;
            Width = BodySize + 16;
            Height = BodySize + 52;
            Background = Brushes.Transparent;
            ClipToBounds = false;
            IsHitTestVisible = true;
            ToolTipService.SetIsEnabled(this, false);

            _placeholder = BuildPlaceholder(out _tintShapes);
            _image = new Image
            {
                Width = BodySize,
                Height = BodySize,
                Stretch = Stretch.Uniform,
                Visibility = Visibility.Collapsed,
            };
            _nickname = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 11,
                MaxWidth = BodySize + 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 2),
            };
            _hookRing = new Ellipse
            {
                Width = RingSize,
                Height = RingSize,
                Stroke = new SolidColorBrush(Color.FromRgb(232, 156, 64)),
                StrokeThickness = 2.5,
                Fill = Brushes.Transparent,
                Visibility = Visibility.Collapsed,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(-90),
            };
            _statusLabel = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xE3, 0xEA)),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Visibility = Visibility.Collapsed,
            };

            _statusRow = new Grid
            {
                Width = BodySize,
                Height = 18,
                Margin = new Thickness(0, 0, 0, 2),
                Visibility = Visibility.Collapsed,
            };
            _statusRow.Children.Add(_hookRing);

            var body = new Grid { Width = BodySize, Height = BodySize };
            body.Children.Add(_placeholder);
            body.Children.Add(_image);

            _content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            _content.Children.Add(_nickname);
            _content.Children.Add(_statusRow);
            _content.Children.Add(body);
            _content.Children.Add(_statusLabel);
            Children.Add(_content);

            _tooltipTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _tooltipTimer.Tick += OnTooltipTimerTick;
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;

            _frameTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(125) };
            _frameTimer.Tick += OnFrameTick;
            _frameTimer.Start();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _refreshTimer.Tick += OnRefreshTick;
            _refreshTimer.Start();
        }

        public void ConfigurePlayerMenu(string playerId, bool isBot, IOverlayPlayerMenuHost menuHost)
        {
            _playerId = playerId ?? string.Empty;
            _isBot = isBot;
            _menuHost = menuHost;
            MouseRightButtonUp -= OnMouseRightButtonUp;
            if (!string.IsNullOrEmpty(_playerId) && _menuHost != null)
                MouseRightButtonUp += OnMouseRightButtonUp;
        }

        void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (OverlayInteractionState.SceneDragging ||
                string.IsNullOrEmpty(_playerId) ||
                _menuHost == null)
                return;

            e.Handled = true;
            CancelTooltip();
            ShowPlayerContextMenu();
        }

        void ShowPlayerContextMenu()
        {
            var menu = new ContextMenu { PlacementTarget = this };
            menu.Items.Add(CreateMenuItem("查看资料", true, () =>
                _menuHost.SendPlayerCommand("player_open_profile", _playerId)));
            menu.Items.Add(CreateMenuItem("添加好友", !_isBot, () =>
                _menuHost.SendPlayerCommand("player_add_friend", _playerId)));
            menu.Items.Add(CreateMenuItem("私聊", !_isBot, () =>
                _menuHost.SendPlayerCommand("player_open_dm", _playerId)));
            menu.Items.Add(CreateMenuItem("点赞互动", true, () =>
                _menuHost.SendPlayerCommand("player_like_recent", _playerId)));
            menu.IsOpen = true;
        }

        static MenuItem CreateMenuItem(string header, bool enabled, Action action)
        {
            var item = new MenuItem { Header = header, IsEnabled = enabled };
            if (enabled)
                item.Click += (_, __) => action();
            return item;
        }

        public void Apply(
            string nickname,
            string visualState,
            string fishingPhase,
            long sessionFishingMs,
            long hookDeadlineMs,
            long fishingStartedAt = 0)
        {
            _nickname.Text = string.IsNullOrWhiteSpace(nickname) ? "玩家" : nickname;
            _hookDeadlineMs = hookDeadlineMs;

            var phase = string.IsNullOrWhiteSpace(fishingPhase)
                ? InferPhaseFromVisual(visualState)
                : fishingPhase;
            if (IsHookedPhase(phase) && _hookDeadlineMs > 0 && _hookTotalMs <= 0)
            {
                var remaining = _hookDeadlineMs - NowMs();
                if (remaining > 0)
                    _hookTotalMs = remaining;
            }
            else if (!IsHookedPhase(phase))
            {
                _hookTotalMs = 0;
            }

            _fishingPhase = phase ?? string.Empty;
            SyncSessionAnchor(Math.Max(0, sessionFishingMs), fishingStartedAt);
            UpdateStatusVisuals();
            ApplyVisualState(visualState);
            RefreshOpenHover();
        }

        public void Place(double centerX, double centerY)
        {
            _centerX = centerX;
            _centerY = centerY;
            Canvas.SetLeft(this, centerX - Width * 0.5);
            Canvas.SetTop(this, centerY - BodySize * 0.5 - 22);
            if (_hoverShown)
                TryShowHover();
        }

        void ApplyVisualState(string visualState)
        {
            if (string.Equals(_visualState, visualState, StringComparison.Ordinal))
                return;

            _visualState = visualState ?? "idle";
            _frames = OverlayFrameCache.Get(_visualState);
            _frameIndex = 0;
            ApplyTint(_visualState);
            ShowFrame();
        }

        void UpdateStatusVisuals()
        {
            var statusText = FormatStatusText();
            if (string.IsNullOrEmpty(statusText))
            {
                _statusLabel.Visibility = Visibility.Collapsed;
                _statusRow.Visibility = Visibility.Collapsed;
                _hookRing.Visibility = Visibility.Collapsed;
                return;
            }

            _statusLabel.Text = statusText;
            _statusLabel.Visibility = Visibility.Visible;

            if (IsHookedPhase(_fishingPhase))
            {
                UpdateHookRing();
                _statusRow.Visibility = _hookRing.Visibility == Visibility.Visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                return;
            }

            _hookRing.Visibility = Visibility.Collapsed;
            _statusRow.Visibility = Visibility.Collapsed;
        }

        void UpdateHookRing()
        {
            if (_hookDeadlineMs <= 0)
            {
                _hookRing.Visibility = Visibility.Collapsed;
                return;
            }

            var remaining = Math.Max(0, _hookDeadlineMs - NowMs());
            if (remaining <= 0)
            {
                _hookRing.Visibility = Visibility.Collapsed;
                return;
            }

            if (_hookTotalMs <= 0)
                _hookTotalMs = remaining;

            var progress = Math.Max(0, Math.Min(1, (double)remaining / _hookTotalMs));
            var visible = RingCircumference * progress;
            var gap = Math.Max(0.01, RingCircumference - visible);
            _hookRing.StrokeDashArray = new DoubleCollection { visible, gap };
            _hookRing.Visibility = Visibility.Visible;
        }

        void OnRefreshTick(object sender, EventArgs e)
        {
            if (_hookRing.Visibility == Visibility.Visible)
                UpdateHookRing();
            if (_hoverShown)
                RefreshOpenHover();
        }

        void OnFrameTick(object sender, EventArgs e)
        {
            if (_frames.Length <= 1)
                return;
            _frameIndex = (_frameIndex + 1) % _frames.Length;
            ShowFrame();
        }

        void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (OverlayInteractionState.SceneDragging)
                return;
            _pointerInside = true;
            _tooltipTimer.Stop();
            _tooltipTimer.Start();
        }

        void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _pointerInside = false;
            _tooltipTimer.Stop();
            CloseHover();
        }

        void OnTooltipTimerTick(object sender, EventArgs e)
        {
            _tooltipTimer.Stop();
            if (!_pointerInside || OverlayInteractionState.SceneDragging)
            {
                CloseHover();
                return;
            }

            if (!TryShowHover())
                CloseHover();
        }

        bool TryShowHover()
        {
            var text = BuildHoverText();
            if (string.IsNullOrEmpty(text))
                return false;

            _hoverHost?.ShowHoverCard(ActorKey, text, this);
            _hoverShown = true;
            return true;
        }

        void RefreshOpenHover()
        {
            if (!_hoverShown)
                return;

            var text = BuildHoverText();
            if (string.IsNullOrEmpty(text))
                CloseHover();
            else
                _hoverHost?.UpdateHoverCard(ActorKey, text);
        }

        void CloseHover()
        {
            _hoverShown = false;
            _hoverHost?.HideHoverCard(ActorKey);
        }

        public void CancelTooltip()
        {
            _pointerInside = false;
            _tooltipTimer.Stop();
            CloseHover();
        }

        string FormatStatusText()
        {
            var label = MainWindow.FormatPhaseLabel(_fishingPhase);
            if (string.Equals(label, "待机", StringComparison.Ordinal) ||
                string.Equals(label, "坐下", StringComparison.Ordinal))
                return string.Empty;
            return label ?? string.Empty;
        }

        string BuildHoverText()
        {
            var status = FormatStatusText();
            if (string.IsNullOrEmpty(status))
                return string.Empty;

            if (IsHookedPhase(_fishingPhase) && _hookDeadlineMs > 0)
            {
                var remaining = Math.Max(0, _hookDeadlineMs - NowMs());
                if (remaining > 0)
                    return status + Environment.NewLine + "收杆 " + FormatDuration(remaining);
            }

            if (IsFishingPhase(_fishingPhase) || IsHookedPhase(_fishingPhase))
                return status + Environment.NewLine + "本局 " + FormatDuration(CurrentSessionFishingMs());

            return status;
        }

        void SyncSessionAnchor(long sessionFishingMs, long fishingStartedAt)
        {
            if (!IsFishingPhase(_fishingPhase) && !IsHookedPhase(_fishingPhase))
            {
                _sessionAnchorMs = 0;
                return;
            }

            if (IsUnixMs(fishingStartedAt))
            {
                _sessionAnchorMs = fishingStartedAt;
                return;
            }

            if (sessionFishingMs > 0)
                _sessionAnchorMs = NowMs() - sessionFishingMs;
        }

        long CurrentSessionFishingMs()
        {
            if (_sessionAnchorMs <= 0)
                return 0;
            return Math.Max(0, NowMs() - _sessionAnchorMs);
        }

        static bool IsUnixMs(long value)
        {
            return value > 1_000_000_000_000L && value < 10_000_000_000_000L;
        }

        void ShowFrame()
        {
            if (_frames.Length == 0)
            {
                _image.Visibility = Visibility.Collapsed;
                _placeholder.Visibility = Visibility.Visible;
                return;
            }

            _placeholder.Visibility = Visibility.Collapsed;
            _image.Visibility = Visibility.Visible;
            _image.Source = _frames[_frameIndex % _frames.Length];
        }

        void ApplyTint(string visualState)
        {
            var fill = new SolidColorBrush(StateColor(visualState));
            foreach (var shape in _tintShapes)
                shape.Fill = fill;
        }

        static bool IsFishingPhase(string phase)
        {
            return phase == "waiting" ||
                   phase == "baiting" ||
                   phase == "casting" ||
                   phase == "resolving" ||
                   phase == "stopping";
        }

        static bool IsHookedPhase(string phase)
        {
            return phase == "hooked";
        }

        static string InferPhaseFromVisual(string visualState)
        {
            switch (visualState)
            {
                case "fishing": return "waiting";
                case "hooked": return "hooked";
                case "catching": return "resolving";
                default: return "idle";
            }
        }

        static long NowMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        static string FormatDuration(long ms)
        {
            var totalSeconds = Math.Max(0, ms) / 1000;
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            return minutes + ":" + seconds.ToString("00");
        }

        public static Color StateColor(string visualState)
        {
            switch (visualState)
            {
                case "fishing": return Color.FromRgb(90, 168, 214);
                case "hooked": return Color.FromRgb(232, 156, 64);
                case "catching": return Color.FromRgb(86, 176, 230);
                case "dragging": return Color.FromRgb(232, 210, 86);
                case "offline": return Color.FromRgb(120, 128, 136);
                default: return Color.FromRgb(77, 137, 168);
            }
        }

        static Canvas BuildPlaceholder(out Shape[] tintShapes)
        {
            var earL = new Polygon
            {
                Points = new PointCollection { new Point(48, 70), new Point(68, 18), new Point(103, 62) },
                Stroke = new SolidColorBrush(Color.FromRgb(0xB8, 0xE1, 0xEF)),
                StrokeThickness = 3,
            };
            var earR = new Polygon
            {
                Points = new PointCollection { new Point(117, 62), new Point(152, 18), new Point(172, 70) },
                Stroke = new SolidColorBrush(Color.FromRgb(0xB8, 0xE1, 0xEF)),
                StrokeThickness = 3,
            };
            var body = new Ellipse
            {
                Width = 150,
                Height = 145,
                Stroke = new SolidColorBrush(Color.FromRgb(0xB8, 0xE1, 0xEF)),
                StrokeThickness = 3,
            };
            Canvas.SetLeft(body, 35);
            Canvas.SetTop(body, 52);
            var eyeL = new Ellipse { Width = 16, Height = 22, Fill = new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x2B)) };
            Canvas.SetLeft(eyeL, 76);
            Canvas.SetTop(eyeL, 102);
            var eyeR = new Ellipse { Width = 16, Height = 22, Fill = new SolidColorBrush(Color.FromRgb(0x16, 0x21, 0x2B)) };
            Canvas.SetLeft(eyeR, 128);
            Canvas.SetTop(eyeR, 102);
            var nose = new Ellipse { Width = 12, Height = 8, Fill = new SolidColorBrush(Color.FromRgb(0xF3, 0xC9, 0x69)) };
            Canvas.SetLeft(nose, 104);
            Canvas.SetTop(nose, 139);

            var canvas = new Canvas { Width = 220, Height = 220 };
            canvas.LayoutTransform = new ScaleTransform(BodySize / 220.0, BodySize / 220.0);
            canvas.Children.Add(earL);
            canvas.Children.Add(earR);
            canvas.Children.Add(body);
            canvas.Children.Add(eyeL);
            canvas.Children.Add(eyeR);
            canvas.Children.Add(nose);
            tintShapes = new Shape[] { earL, earR, body };
            return canvas;
        }
    }

    static class OverlayFrameCache
    {
        static readonly Dictionary<string, ImageSource[]> Cache =
            new Dictionary<string, ImageSource[]>(StringComparer.OrdinalIgnoreCase);

        public static ImageSource[] Get(string visualState)
        {
            var key = string.IsNullOrWhiteSpace(visualState) ? "idle" : visualState;
            if (Cache.TryGetValue(key, out var frames))
                return frames;

            var loaded = Load(key);
            Cache[key] = loaded;
            return loaded;
        }

        static ImageSource[] Load(string visualState)
        {
            var root = AppDomain.CurrentDomain.BaseDirectory;
            var list = new List<ImageSource>();
            for (var i = 0; i < 16; i++)
            {
                var path = System.IO.Path.Combine(root, "OverlayResources",
                    "cat-" + visualState + "-" + i + ".png");
                if (!File.Exists(path))
                    break;
                list.Add(LoadBitmap(path));
            }

            if (list.Count == 0)
            {
                var fallback = System.IO.Path.Combine(root, "OverlayResources", "cat.png");
                if (File.Exists(fallback))
                    list.Add(LoadBitmap(fallback));
            }

            return list.ToArray();
        }

        static ImageSource LoadBitmap(string path)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
    }
}
