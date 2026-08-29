using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FishSocialOverlay
{
    /// <summary>
    /// Loads overlay-hud.json with optional parentId hierarchy.
    /// parentId coordinates are relative; runtime flattens to SceneCanvas absolute pixels.
    /// </summary>
    public sealed class OverlayHudLayout
    {
        public const double CanvasWidth = 960;
        public const double CanvasHeight = 560;

        static readonly HashSet<string> DockChatChildIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "chat_preview",
            "chat_toggle",
            "chat_input",
            "chat_send",
            "chat_placeholder",
        };

        static readonly HashSet<string> LayoutOnlyGroups = new HashSet<string>(StringComparer.Ordinal)
        {
            "menu_rail",
            "dock_fishing",
        };

        static readonly HashSet<string> ChromeBackgrounds = new HashSet<string>(StringComparer.Ordinal)
        {
            "dock_chat",
            "cap_status",
        };

        static readonly HashSet<string> InteractiveWidgets = new HashSet<string>(StringComparer.Ordinal)
        {
            "btn_menu_toggle",
            "btn_menu_map",
            "btn_menu_shop",
            "btn_menu_friends",
            "btn_menu_catch",
            "btn_menu_leaderboard",
            "btn_menu_settings",
            "btn_open_main",
            "btn_exit_pond",
            "btn_debug_police",
            "btn_debug_gameplay",
            "btn_fishing_toggle",
            "btn_groundbait",
            "btn_catch_leave",
            "chat_toggle",
            "chat_input",
            "chat_send",
        };

        readonly Dictionary<string, ImageBrush> _spriteCache =
            new Dictionary<string, ImageBrush>(StringComparer.OrdinalIgnoreCase);

        public bool IsActive { get; private set; }

        public string LastLoadError { get; private set; }

        public HudChatDockMetrics ChatDockMetrics { get; private set; }

        public struct HudChatDockMetrics
        {
            public double X;
            public double Y;
            public double W;
            public double CollapsedH;
            public double ExpandedH;
        }

        public bool TryApply(MainWindow window, Canvas sceneCanvas)
        {
            LastLoadError = null;
            IsActive = false;
            if (window == null || sceneCanvas == null)
                return false;

            var jsonPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "OverlayResources",
                "hud",
                "overlay-hud.json");
            if (!File.Exists(jsonPath))
            {
                LastLoadError = "找不到 overlay-hud.json";
                System.Diagnostics.Debug.WriteLine("[OverlayHud] No overlay-hud.json; using XAML layout.");
                return false;
            }

            OverlayHudDocument document;
            try
            {
                using (var stream = File.OpenRead(jsonPath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(OverlayHudDocument));
                    document = serializer.ReadObject(stream) as OverlayHudDocument;
                }
            }
            catch (Exception ex)
            {
                LastLoadError = "JSON 解析失败：" + ex.Message;
                System.Diagnostics.Debug.WriteLine("[OverlayHud] Parse failed: " + ex.Message);
                return false;
            }

            if (document == null || document.widgets == null || document.widgets.Length == 0)
            {
                LastLoadError = "HUD JSON 为空";
                System.Diagnostics.Debug.WriteLine("[OverlayHud] Empty HUD document.");
                return false;
            }

            if (Math.Abs(document.width - CanvasWidth) > 0.5 ||
                Math.Abs(document.height - CanvasHeight) > 0.5)
            {
                LastLoadError = "画布尺寸必须是 960×560";
                System.Diagnostics.Debug.WriteLine(
                    "[OverlayHud] Canvas size must be 960x560, got " +
                    document.width + "x" + document.height + ".");
                return false;
            }

            var byId = new Dictionary<string, OverlayHudWidgetDto>(StringComparer.Ordinal);
            foreach (var widget in document.widgets)
            {
                if (widget == null || string.IsNullOrWhiteSpace(widget.id))
                    continue;
                if (byId.ContainsKey(widget.id))
                {
                    LastLoadError = "重复的 widget id：" + widget.id;
                    System.Diagnostics.Debug.WriteLine("[OverlayHud] Duplicate widget id: " + widget.id);
                    return false;
                }
                byId[widget.id] = widget;
            }

            foreach (var required in OverlayHudWidgets.Required)
            {
                if (!byId.ContainsKey(required))
                {
                    LastLoadError = "缺少必需控件：" + required;
                    System.Diagnostics.Debug.WriteLine("[OverlayHud] Missing required widget: " + required);
                    return false;
                }
            }

            foreach (var widget in byId.Values)
            {
                if (string.IsNullOrWhiteSpace(widget.parentId))
                    continue;
                if (!byId.ContainsKey(widget.parentId))
                {
                    LastLoadError = widget.id + " 的 parentId 无效：" + widget.parentId;
                    System.Diagnostics.Debug.WriteLine(
                        "[OverlayHud] Unknown parentId " + widget.parentId + " for " + widget.id);
                    return false;
                }
            }

            var hudRoot = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "OverlayResources",
                "hud");
            var bindings = BuildBindings(window);
            var absolute = BuildAbsolutePositions(byId);
            var sorted = document.widgets
                .Where(w => w != null && !string.IsNullOrWhiteSpace(w.id))
                .Where(w => !LayoutOnlyGroups.Contains(w.id))
                .OrderBy(w => ChromeBackgrounds.Contains(w.id) ? 0 : 1)
                .ThenBy(w => w.z)
                .ThenBy(w => w.id, StringComparer.Ordinal)
                .ToArray();

            var applied = 0;
            Canvas chatDockHost = null;
            foreach (var widget in sorted)
            {
                if (widget.w <= 0 || widget.h <= 0)
                {
                    LastLoadError = widget.id + " 尺寸无效：" + widget.w + "×" + widget.h;
                    System.Diagnostics.Debug.WriteLine(
                        "[OverlayHud] Invalid size for " + widget.id + ": " + widget.w + "x" + widget.h);
                    return false;
                }

                if (!bindings.TryGetValue(widget.id, out var element) || element == null)
                    continue;

                if (DockChatChildIds.Contains(widget.id))
                {
                    if (chatDockHost == null)
                    {
                        LastLoadError = "聊天控件缺少 dock_chat 容器";
                        return false;
                    }

                    Reparent(chatDockHost, element);
                    NormalizeForCanvas(element);
                    Canvas.SetLeft(element, widget.x);
                    Canvas.SetTop(element, widget.y);
                    element.Width = widget.w;
                    element.Height = widget.h;
                    Panel.SetZIndex(element, ResolveZIndex(widget));
                    if (element is TextBlock)
                        element.IsHitTestVisible = false;
                    ApplyVisibility(element, widget);
                    ApplySprite(element, widget, hudRoot);
                    applied++;
                    continue;
                }

                if (!absolute.TryGetValue(widget.id, out var pos))
                    continue;

                Reparent(sceneCanvas, element);
                NormalizeForCanvas(element);
                Canvas.SetLeft(element, pos.X);
                Canvas.SetTop(element, pos.Y);
                element.Width = widget.w;
                element.Height = widget.h;
                Panel.SetZIndex(element, ResolveZIndex(widget));
                if (ChromeBackgrounds.Contains(widget.id))
                {
                    // dock_chat 需接收子控件点击；仅 cap_status 等纯装饰面板关闭命中
                    element.IsHitTestVisible = widget.id != "dock_chat";
                    if (widget.id == "dock_chat" && element is Border chatBorder)
                        chatDockHost = EnsureChatDockHost(chatBorder);
                }
                else if (element is TextBlock)
                    element.IsHitTestVisible = false;
                ApplyVisibility(element, widget);
                ApplySprite(element, widget, hudRoot);
                if (widget.id == "txt_error" && element is TextBlock errorText)
                {
                    errorText.TextAlignment = TextAlignment.Center;
                    errorText.TextWrapping = TextWrapping.Wrap;
                    errorText.TextTrimming = TextTrimming.CharacterEllipsis;
                }
                applied++;
            }

            if (byId.TryGetValue("dock_chat", out var dockChat))
            {
                ChatDockMetrics = new HudChatDockMetrics
                {
                    X = dockChat.x,
                    Y = dockChat.y,
                    W = dockChat.w,
                    CollapsedH = dockChat.h,
                    ExpandedH = ComputeChatDockExpandedHeight(byId, dockChat.h),
                };
            }

            window.ConfigureHudChatDockMetrics(ChatDockMetrics);
            window.ConfigureHudLayoutMode(true);
            IsActive = true;
            System.Diagnostics.Debug.WriteLine("[OverlayHud] Applied " + applied + " widgets (chat nested).");
            return true;
        }

        static Canvas EnsureChatDockHost(Border chatBorder)
        {
            chatBorder.Padding = new Thickness(0);
            chatBorder.Child = null;
            var host = new Canvas
            {
                Background = Brushes.Transparent,
                ClipToBounds = true,
            };
            chatBorder.Child = host;
            return host;
        }

        static double ComputeChatDockExpandedHeight(
            Dictionary<string, OverlayHudWidgetDto> byId,
            double collapsedHeight)
        {
            var maxBottom = collapsedHeight;
            foreach (var id in DockChatChildIds)
            {
                if (!byId.TryGetValue(id, out var widget))
                    continue;
                if (!string.Equals(widget.parentId, "dock_chat", StringComparison.Ordinal))
                    continue;
                maxBottom = Math.Max(maxBottom, widget.y + widget.h);
            }

            return maxBottom;
        }

        static Dictionary<string, Point> BuildAbsolutePositions(
            Dictionary<string, OverlayHudWidgetDto> byId)
        {
            var cache = new Dictionary<string, Point>(StringComparer.Ordinal);
            foreach (var id in byId.Keys)
                ResolveAbsolute(byId, id, cache);
            return cache;
        }

        static Point ResolveAbsolute(
            Dictionary<string, OverlayHudWidgetDto> byId,
            string id,
            Dictionary<string, Point> cache)
        {
            if (cache.TryGetValue(id, out var cached))
                return cached;

            if (!byId.TryGetValue(id, out var widget))
            {
                cached = new Point(0, 0);
                cache[id] = cached;
                return cached;
            }

            var x = widget.x;
            var y = widget.y;
            if (!string.IsNullOrWhiteSpace(widget.parentId))
            {
                var parent = ResolveAbsolute(byId, widget.parentId, cache);
                x += parent.X;
                y += parent.Y;
            }

            cached = new Point(x, y);
            cache[id] = cached;
            return cached;
        }

        static int ResolveZIndex(OverlayHudWidgetDto widget)
        {
            if (ChromeBackgrounds.Contains(widget.id))
                return 100;
            if (InteractiveWidgets.Contains(widget.id))
                return widget.z > 110 ? widget.z : 120;
            if (widget.z > 0)
                return widget.z;
            return 110;
        }

        static void ApplyVisibility(FrameworkElement element, OverlayHudWidgetDto widget)
        {
            if (widget.visibleDefault.HasValue &&
                widget.visibleDefault.Value == false &&
                element.Visibility == Visibility.Visible)
                element.Visibility = Visibility.Collapsed;
        }

        internal static Dictionary<string, FrameworkElement> BuildBindings(MainWindow window)
        {
            return new Dictionary<string, FrameworkElement>(StringComparer.Ordinal)
            {
                { "btn_menu_toggle", window.MenuToggleButton },
                { "btn_menu_map", window.MenuMapButton },
                { "btn_menu_shop", window.MenuShopButton },
                { "btn_menu_friends", window.MenuFriendsButton },
                { "btn_menu_catch", window.MenuCatchButton },
                { "btn_menu_leaderboard", window.MenuLeaderboardButton },
                { "btn_menu_settings", window.MenuSettingsButton },
                { "cap_status", window.StatusCapsule },
                { "txt_status", window.StateText },
                { "txt_pond", window.PondText },
                { "txt_spot", window.SpotText },
                { "txt_error", window.ErrorText },
                { "btn_open_main", window.OpenMainButton },
                { "btn_exit_pond", window.ExitPondButton },
                { "btn_debug_police", window.PoliceDebugButton },
                { "btn_debug_gameplay", window.GameplayDebugButton },
                { "btn_fishing_toggle", window.FishingToggleButton },
                { "btn_groundbait", window.GroundbaitButton },
                { "txt_groundbait", window.GroundbaitStatusText },
                { "btn_catch_leave", window.CatchLeaveButton },
                { "dock_chat", window.ChatDockChrome },
                { "chat_preview", window.ChatLatestPreview },
                { "chat_toggle", window.ChatDockToggle },
                { "chat_input", window.ChatInputBox },
                { "chat_send", window.ChatSendButton },
                { "chat_placeholder", window.ChatPlaceholder },
            };
        }

        static void Reparent(Panel host, FrameworkElement element)
        {
            Detach(element);
            if (!host.Children.Contains(element))
                host.Children.Add(element);
        }

        static void Detach(FrameworkElement element)
        {
            if (element.Parent is Panel parent)
                parent.Children.Remove(element);
            else if (element.Parent is Decorator decorator && decorator.Child == element)
                decorator.Child = null;
            else if (element.Parent is ContentControl content && content.Content == element)
                content.Content = null;
        }

        static void NormalizeForCanvas(FrameworkElement element)
        {
            element.HorizontalAlignment = HorizontalAlignment.Left;
            element.VerticalAlignment = VerticalAlignment.Top;
            element.Margin = new Thickness(0);
            if (element is FrameworkElement fe)
            {
                fe.MinWidth = 0;
                fe.MinHeight = 0;
            }
            if (element is Control control)
                control.Padding = new Thickness(0);
            if (element is Border border)
            {
                border.Padding = new Thickness(0);
                border.ClipToBounds = false;
            }
            if (element is TextBlock textBlock)
            {
                textBlock.TextAlignment = TextAlignment.Left;
                textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
            }
        }

        void ApplySprite(FrameworkElement element, OverlayHudWidgetDto widget, string hudRoot)
        {
            if (string.IsNullOrWhiteSpace(widget.sprite))
                return;

            var spritePath = Path.Combine(hudRoot, widget.sprite);
            if (!File.Exists(spritePath))
                return;

            if (!_spriteCache.TryGetValue(spritePath, out var brush))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(spritePath, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                brush = new ImageBrush(image) { Stretch = Stretch.Fill };
                brush.Freeze();
                _spriteCache[spritePath] = brush;
            }

            if (element is Control control)
                control.Background = brush;
            else if (element is Border border)
                border.Background = brush;
        }

        [DataContract]
        sealed class OverlayHudDocument
        {
            [DataMember(Name = "width")] public double width = CanvasWidth;
            [DataMember(Name = "height")] public double height = CanvasHeight;
            [DataMember(Name = "widgets")] public OverlayHudWidgetDto[] widgets = null;
        }

        [DataContract]
        sealed class OverlayHudWidgetDto
        {
            [DataMember(Name = "id")] public string id = null;
            [DataMember(Name = "parentId")] public string parentId = null;
            [DataMember(Name = "kind")] public string kind = null;
            [DataMember(Name = "x")] public double x;
            [DataMember(Name = "y")] public double y;
            [DataMember(Name = "w")] public double w;
            [DataMember(Name = "h")] public double h;
            [DataMember(Name = "sprite")] public string sprite = null;
            [DataMember(Name = "z")] public int z;
            [DataMember(Name = "visibleDefault")] public bool? visibleDefault = null;
        }
    }
}
