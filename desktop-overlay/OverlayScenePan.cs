using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace FishSocialOverlay
{
    /// <summary>
    /// Pans SceneContentCanvas inside the current viewport (STEAM-DESKTOP-14B / 16).
    /// X = 14B offset. Y = top-crop so the pond bottom stays aligned.
    /// Left/right buttons live in OverlayHud (btn_pan_left / btn_pan_right).
    /// </summary>
    public sealed class OverlayScenePan
    {
        public const double DesignViewportWidth = 960;
        public const double DesignViewportHeight = 560;
        public const double PanSpeedPxPerSec = 300;

        readonly FrameworkElement _content;
        readonly TranslateTransform _translate = new TranslateTransform();
        readonly DispatcherTimer _timer;
        Button _leftButton;
        Button _rightButton;
        double _viewportWidth = DesignViewportWidth;
        double _viewportHeight = DesignViewportHeight;
        double _sceneWidth = DesignViewportWidth;
        double _sceneHeight = DesignViewportHeight;
        double _offsetX;
        int _dir;
        DateTime _lastTickUtc = DateTime.UtcNow;
        bool _centeredOnce;
        bool _wired;

        public OverlayScenePan(FrameworkElement content)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _content.RenderTransform = _translate;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += OnTick;
        }

        public double ViewportWidth => _viewportWidth;

        public double ViewportHeight => _viewportHeight;

        public double OffsetX => _offsetX;

        public double SceneWidth => _sceneWidth;

        public bool CanPan => _sceneWidth > _viewportWidth + 0.5;

        public void AttachButtons(Button leftButton, Button rightButton)
        {
            if (_wired)
                DetachButtons();

            _leftButton = leftButton;
            _rightButton = rightButton;
            WireButton(_leftButton, -1);
            WireButton(_rightButton, 1);
            _wired = true;
            UpdateButtons();
        }

        void DetachButtons()
        {
            UnwireButton(_leftButton, -1);
            UnwireButton(_rightButton, 1);
            _wired = false;
        }

        void WireButton(Button button, int direction)
        {
            if (button == null)
                return;
            button.PreviewMouseLeftButtonDown += OnPanButtonDown;
            button.PreviewMouseLeftButtonUp += OnPanButtonUp;
            button.LostMouseCapture += OnPanButtonLostCapture;
            button.Tag = direction;
        }

        void UnwireButton(Button button, int direction)
        {
            if (button == null)
                return;
            button.PreviewMouseLeftButtonDown -= OnPanButtonDown;
            button.PreviewMouseLeftButtonUp -= OnPanButtonUp;
            button.LostMouseCapture -= OnPanButtonLostCapture;
        }

        void OnPanButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var button = sender as Button;
            var dir = button?.Tag is int d ? d : 0;
            BeginPan(dir);
            button?.CaptureMouse();
            e.Handled = true;
        }

        void OnPanButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var button = sender as Button;
            EndPan();
            if (button != null && button.IsMouseCaptured)
                button.ReleaseMouseCapture();
            e.Handled = true;
        }

        void OnPanButtonLostCapture(object sender, System.Windows.Input.MouseEventArgs e)
        {
            EndPan();
        }

        public void SetViewportSize(double width, double height)
        {
            _viewportWidth = Math.Max(1, width);
            _viewportHeight = Math.Max(1, height);
            ApplyOffset(_offsetX);
            ApplyCropY();
            UpdateButtons();
        }

        public void SetSceneWidth(double sceneWidth, bool resetOffset)
        {
            SetSceneSize(sceneWidth, _sceneHeight, resetOffset);
        }

        public void SetSceneSize(double sceneWidth, double sceneHeight, bool resetOffset)
        {
            _sceneWidth = Math.Max(_viewportWidth, sceneWidth);
            _sceneHeight = Math.Max(1, sceneHeight);
            if (resetOffset)
            {
                _offsetX = 0;
                _centeredOnce = false;
            }

            ApplyOffset(_offsetX);
            ApplyCropY();
            UpdateButtons();
        }

        public void Reset()
        {
            EndPan();
            _offsetX = 0;
            _centeredOnce = false;
            ApplyOffset(0);
            ApplyCropY();
            UpdateButtons();
        }

        public void TryCenterOnWorldX(double worldCenterX, bool force = false)
        {
            if (!CanPan)
                return;
            if (_centeredOnce && !force)
                return;

            ApplyOffset(_viewportWidth * 0.5 - worldCenterX);
            _centeredOnce = true;
            UpdateButtons();
        }

        public bool IsPanButton(DependencyObject source)
        {
            while (source != null)
            {
                if (ReferenceEquals(source, _leftButton) || ReferenceEquals(source, _rightButton))
                    return true;
                if (source is Visual || source is Visual3D)
                    source = VisualTreeHelper.GetParent(source);
                else
                    source = LogicalTreeHelper.GetParent(source);
            }

            return false;
        }

        void BeginPan(int direction)
        {
            if (!CanPan || direction == 0)
                return;
            _dir = direction;
            _lastTickUtc = DateTime.UtcNow;
            if (!_timer.IsEnabled)
                _timer.Start();
        }

        void EndPan()
        {
            _dir = 0;
            if (_timer.IsEnabled)
                _timer.Stop();
        }

        void OnTick(object sender, EventArgs e)
        {
            if (_dir == 0 || !CanPan)
            {
                EndPan();
                return;
            }

            var now = DateTime.UtcNow;
            var dt = (now - _lastTickUtc).TotalSeconds;
            _lastTickUtc = now;
            if (dt <= 0 || dt > 0.25)
                dt = 0.016;
            // dir +1 = view right = content moves left
            ApplyOffset(_offsetX - _dir * PanSpeedPxPerSec * dt);
            UpdateButtons();
        }

        void ApplyOffset(double offsetX)
        {
            var min = Math.Min(0, _viewportWidth - _sceneWidth);
            _offsetX = Math.Max(min, Math.Min(0, offsetX));
            _translate.X = _offsetX;
        }

        void ApplyCropY()
        {
            _translate.Y = -Math.Max(0, _sceneHeight - _viewportHeight);
        }

        void UpdateButtons()
        {
            var show = CanPan;
            if (_leftButton != null)
            {
                _leftButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                _leftButton.IsEnabled = show && _offsetX < -0.5;
            }

            if (_rightButton != null)
            {
                _rightButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                var min = _viewportWidth - _sceneWidth;
                _rightButton.IsEnabled = show && _offsetX > min + 0.5;
            }
        }
    }
}
