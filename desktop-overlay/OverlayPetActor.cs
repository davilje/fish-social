using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FishSocialOverlay
{
    /// <summary>
    /// 128×128 pond pet. Plays Overlay-local sequence frames from petVisualState.
    /// Named Pipe never carries images.
    /// </summary>
    public sealed class OverlayPetActor : Grid
    {
        public const double BodySize = 128;

        readonly Image _image;
        readonly Canvas _placeholder;
        readonly Shape[] _tintShapes;
        readonly TextBlock _nickname;
        readonly TextBlock _stateLabel;
        readonly DispatcherTimer _timer;
        ImageSource[] _frames = Array.Empty<ImageSource>();
        int _frameIndex;
        string _visualState = string.Empty;

        public string ActorKey { get; }

        public OverlayPetActor(string actorKey)
        {
            ActorKey = actorKey;
            Width = BodySize;
            Height = BodySize + 36;
            IsHitTestVisible = true;

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
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 2),
            };
            _stateLabel = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0xE3, 0xEA)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            };

            var body = new Grid { Width = BodySize, Height = BodySize };
            body.Children.Add(_placeholder);
            body.Children.Add(_image);

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(_nickname);
            stack.Children.Add(body);
            stack.Children.Add(_stateLabel);
            Children.Add(stack);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(125) };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        public void Apply(string nickname, string visualState, bool isBot = false)
        {
            var name = string.IsNullOrWhiteSpace(nickname) ? "玩家" : nickname;
            _nickname.Text = isBot ? name + " · 机" : name;
            _stateLabel.Text = FormatState(visualState);
            if (string.Equals(_visualState, visualState, StringComparison.Ordinal))
                return;

            _visualState = visualState ?? "idle";
            _frames = OverlayFrameCache.Get(_visualState);
            _frameIndex = 0;
            ApplyTint(_visualState);
            ShowFrame();
        }

        public void Place(double centerX, double centerY)
        {
            Canvas.SetLeft(this, centerX - BodySize * 0.5);
            Canvas.SetTop(this, centerY - BodySize * 0.5 - 16);
        }

        void OnTick(object sender, EventArgs e)
        {
            if (_frames.Length <= 1)
                return;
            _frameIndex = (_frameIndex + 1) % _frames.Length;
            ShowFrame();
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

        static string FormatState(string visualState)
        {
            switch (visualState)
            {
                case "fishing": return "钓鱼";
                case "hooked": return "咬钩";
                case "catching": return "收鱼";
                case "dragging": return "拖动";
                case "offline": return "离线";
                default: return "待机";
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
