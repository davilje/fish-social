using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FishSocialOverlay
{
    public static class CozyTheme
    {
        public const string DefaultPlate = "cozy/journal/surfaces/button-quiet-default.png";
        public const string HoverPlate = "cozy/journal/surfaces/button-quiet-hover.png";
        public const string PressedPlate = "cozy/journal/surfaces/button-quiet-pressed.png";
        public const string Paper = "cozy/journal/surfaces/paper-aside.png";
        public const string CompactPaper = "cozy/journal/surfaces/input-default.png";
        public static readonly int[] SpriteBorder = { 14, 14, 14, 14 };
        public static readonly int[] CompactBorder = { 14, 14, 14, 14 };
        public static readonly int[] PaperBorder = { 22, 22, 22, 22 };
        public static readonly Brush Ink = FrozenBrush(0x75, 0x47, 0x2E);
        public static readonly Brush Cream = FrozenBrush(0xFF, 0xF7, 0xE9);
        static readonly Dictionary<string, BitmapSource> Images =
            new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, BitmapSource> TintedIcons =
            new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);

        public static readonly DependencyProperty IsPaperProperty = DependencyProperty.RegisterAttached(
            "IsPaper", typeof(bool), typeof(CozyTheme), new PropertyMetadata(false, PaperChanged));
        public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
            "Icon", typeof(string), typeof(CozyTheme), new PropertyMetadata(null));
        public static readonly DependencyProperty IconAngleProperty = DependencyProperty.RegisterAttached(
            "IconAngle", typeof(double), typeof(CozyTheme), new PropertyMetadata(0d));
        public static readonly DependencyProperty IconSizeProperty = DependencyProperty.RegisterAttached(
            "IconSize", typeof(double), typeof(CozyTheme), new PropertyMetadata(16d));
        public static readonly DependencyProperty NormalSpriteProperty = DependencyProperty.RegisterAttached(
            "NormalSprite", typeof(string), typeof(CozyTheme), new PropertyMetadata(DefaultPlate));
        public static readonly DependencyProperty HoverSpriteProperty = DependencyProperty.RegisterAttached(
            "HoverSprite", typeof(string), typeof(CozyTheme), new PropertyMetadata(HoverPlate));
        public static readonly DependencyProperty PressedSpriteProperty = DependencyProperty.RegisterAttached(
            "PressedSprite", typeof(string), typeof(CozyTheme), new PropertyMetadata(PressedPlate));
        public static readonly DependencyProperty TrackMenuPressProperty = DependencyProperty.RegisterAttached(
            "TrackMenuPress", typeof(bool), typeof(CozyTheme), new PropertyMetadata(false, TrackMenuPressChanged));
        public static readonly DependencyProperty IsMenuPressedProperty = DependencyProperty.RegisterAttached(
            "IsMenuPressed", typeof(bool), typeof(CozyTheme), new PropertyMetadata(false));

        public static bool GetIsPaper(DependencyObject target) => (bool)target.GetValue(IsPaperProperty);
        public static void SetIsPaper(DependencyObject target, bool value) => target.SetValue(IsPaperProperty, value);
        public static string GetIcon(DependencyObject target) => (string)target.GetValue(IconProperty);
        public static void SetIcon(DependencyObject target, string value) => target.SetValue(IconProperty, value);
        public static double GetIconAngle(DependencyObject target) => (double)target.GetValue(IconAngleProperty);
        public static void SetIconAngle(DependencyObject target, double value) => target.SetValue(IconAngleProperty, value);
        public static double GetIconSize(DependencyObject target) => (double)target.GetValue(IconSizeProperty);
        public static void SetIconSize(DependencyObject target, double value) => target.SetValue(IconSizeProperty, value);
        public static string GetNormalSprite(DependencyObject target) => (string)target.GetValue(NormalSpriteProperty);
        public static void SetNormalSprite(DependencyObject target, string value) => target.SetValue(NormalSpriteProperty, value);
        public static string GetHoverSprite(DependencyObject target) => (string)target.GetValue(HoverSpriteProperty);
        public static void SetHoverSprite(DependencyObject target, string value) => target.SetValue(HoverSpriteProperty, value);
        public static string GetPressedSprite(DependencyObject target) => (string)target.GetValue(PressedSpriteProperty);
        public static void SetPressedSprite(DependencyObject target, string value) => target.SetValue(PressedSpriteProperty, value);
        public static bool GetTrackMenuPress(DependencyObject target) => (bool)target.GetValue(TrackMenuPressProperty);
        public static void SetTrackMenuPress(DependencyObject target, bool value) => target.SetValue(TrackMenuPressProperty, value);
        public static bool GetIsMenuPressed(DependencyObject target) => (bool)target.GetValue(IsMenuPressedProperty);
        public static void SetIsMenuPressed(DependencyObject target, bool value) => target.SetValue(IsMenuPressedProperty, value);

        static void TrackMenuPressChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            if (!(target is MenuItem item))
                return;
            item.RemoveHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(MenuPressDown));
            item.RemoveHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(MenuPressReset));
            item.MouseLeave -= MenuPressReset;
            item.Unloaded -= MenuPressReset;
            if (!(bool)args.NewValue)
            {
                SetIsMenuPressed(item, false);
                return;
            }
            item.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(MenuPressDown), true);
            item.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(MenuPressReset), true);
            item.MouseLeave += MenuPressReset;
            item.Unloaded += MenuPressReset;
        }

        static void MenuPressDown(object sender, MouseButtonEventArgs args)
        {
            if (sender is MenuItem item && item.IsEnabled)
                SetIsMenuPressed(item, true);
        }

        static void MenuPressReset(object sender, RoutedEventArgs args)
        {
            if (sender is MenuItem item)
                SetIsMenuPressed(item, false);
        }

        static Brush FrozenBrush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }

        static void PaperChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            if (!(target is Border border))
                return;
            border.SizeChanged -= PaperSizeChanged;
            if (!(bool)args.NewValue)
                return;
            border.SizeChanged += PaperSizeChanged;
            border.BorderThickness = new Thickness(0);
            RefreshPaper(border);
        }

        static void PaperSizeChanged(object sender, SizeChangedEventArgs args) => RefreshPaper((Border)sender);

        static void RefreshPaper(Border border)
        {
            var resource = SurfacePath(Paper, border.ActualWidth, border.ActualHeight);
            var brush = OverlayNineSlice.TryGetBrush(LoadImage(resource), SurfaceBorder(resource),
                border.ActualWidth, border.ActualHeight);
            border.Background = brush ?? Cream;
        }

        internal static string SurfacePath(string sprite, double width, double height)
        {
            return sprite == Paper && Math.Min(width, height) > 0 && Math.Min(width, height) < 48
                ? CompactPaper : sprite;
        }

        internal static int[] SurfaceBorder(string sprite)
        {
            if (sprite == Paper)
                return PaperBorder;
            if (sprite == CompactPaper)
                return CompactBorder;
            return SpriteBorder;
        }

        internal static BitmapSource LoadImage(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;
            var root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OverlayResources", "hud"));
            var path = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return null;
            if (Images.TryGetValue(path, out var cached))
                return cached;
            if (!File.Exists(path))
                return null;
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                // Source PNGs are byte-identical to the desktop assets. Normalize only the
                // surface's rendered texel size; icons retain their original pixels.
                var scale = relativePath.StartsWith("cozy/journal/surfaces/button-", StringComparison.Ordinal) ? .25
                    : relativePath.StartsWith("cozy/journal/surfaces/", StringComparison.Ordinal) ? .5 : 1;
                BitmapSource rendered = image;
                if (scale != 1) { rendered = new TransformedBitmap(image, new ScaleTransform(scale, scale)); rendered.Freeze(); }
                Images[path] = rendered;
                return rendered;
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine("[Cozy] PNG load failed: " + exception.Message);
                return null;
            }
        }

        public static string IconFor(string label)
        {
            switch (label)
            {
                case "当前鱼塘": return "pond";
                case "世界地图": return "map";
                case "商店与装备": return "shop";
                case "好友与聊天": case "私聊": case "动态墙": return "social";
                case "鱼获/背包": return "bag";
                case "图鉴": return "fish";
                case "个人中心": case "查看资料": return "user";
                case "添加好友": return "social";
                case "点赞互动": return "heart";
                case "排行榜": return "trophy";
                case "隐藏到托盘": return "home";
                case "退出": return "lock";
                default: return "settings";
            }
        }

        public static string IconPath(string icon) => "cozy/icons/" + icon + ".png";

        internal static BitmapSource LoadInkIcon(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            path = path.Replace("cozy/icons-light/", "cozy/icons/");
            if (TintedIcons.TryGetValue(path, out var cached))
                return cached;
            var source = LoadImage(path);
            if (source == null)
                return null;
            TintedIcons[path] = source;
            return source;
        }
    }

    public sealed class CozySurface : FrameworkElement
    {
        public static readonly DependencyProperty SpriteProperty = DependencyProperty.Register(
            "Sprite", typeof(string), typeof(CozySurface),
            new FrameworkPropertyMetadata(CozyTheme.Paper, FrameworkPropertyMetadataOptions.AffectsRender));
        public string Sprite { get => (string)GetValue(SpriteProperty); set => SetValue(SpriteProperty, value); }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (ActualWidth <= 0 || ActualHeight <= 0)
                return;
            var resource = CozyTheme.SurfacePath(Sprite, ActualWidth, ActualHeight);
            var brush = OverlayNineSlice.TryGetBrush(CozyTheme.LoadImage(resource), CozyTheme.SurfaceBorder(resource),
                ActualWidth, ActualHeight);
            drawingContext.DrawRectangle(brush ?? CozyTheme.Cream, null, new Rect(RenderSize));
        }
    }

    public sealed class CozyIcon : FrameworkElement
    {
        public static readonly DependencyProperty SpriteProperty = DependencyProperty.Register(
            "Sprite", typeof(string), typeof(CozyIcon),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public string Sprite { get => (string)GetValue(SpriteProperty); set => SetValue(SpriteProperty, value); }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var source = CozyTheme.LoadInkIcon(Sprite);
            if (source == null || ActualWidth <= 0 || ActualHeight <= 0)
                return;
            var scale = Math.Min(ActualWidth / source.PixelWidth, ActualHeight / source.PixelHeight);
            var width = source.PixelWidth * scale;
            var height = source.PixelHeight * scale;
            var rect = new Rect((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height);
            drawingContext.DrawImage(source, rect);
        }
    }
}
