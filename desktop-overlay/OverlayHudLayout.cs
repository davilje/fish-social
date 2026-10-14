using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
            "chat_log",
            "chat_scroll",
            "chat_toggle",
            "chat_input",
            "chat_send",
            "chat_placeholder",
        };

        const double ChatDockBottomPadding = 10;
        const double ChatInputMinHeight = 36;

        static readonly string[] MenuRailOrder =
        {
            "btn_menu_toggle",
            "btn_open_main",
            "btn_exit_pond",
            "btn_menu_map",
            "btn_menu_shop",
            "btn_menu_friends",
            "btn_menu_catch",
            "btn_menu_leaderboard",
            "btn_menu_settings",
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
            "btn_pan_left",
            "btn_pan_right",
            "chat_toggle",
            "chat_input",
            "chat_send",
        };

        static readonly HashSet<string> StaticTextLabels = new HashSet<string>(StringComparer.Ordinal)
        {
            "chat_placeholder",
        };

        readonly Dictionary<string, ImageBrush> _spriteCache =
            new Dictionary<string, ImageBrush>(StringComparer.OrdinalIgnoreCase);

        readonly Dictionary<string, BitmapSource> _bitmapCache =
            new Dictionary<string, BitmapSource>(StringComparer.OrdinalIgnoreCase);

        readonly Dictionary<string, FontFamily> _fontCache =
            new Dictionary<string, FontFamily>(StringComparer.OrdinalIgnoreCase);


        Dictionary<string, OverlayHudWidgetDto> _appliedWidgets;

        public bool IsActive { get; private set; }

        public string LastLoadError { get; private set; }

        public HudChatDockMetrics ChatDockMetrics { get; private set; }
        OverlayHudComponentsDto _components;

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
            HudRootPath = hudRoot;
            try
            {
            ApplyCozyResourceMapping(document);
            _components = document.components;
            ApplyCozyComponentParameters(_components);
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
                    ApplyTextStyle(element, widget, hudRoot);
                    ApplyButtonLabel(element, widget);
                    ApplyCozyButtonVisual(element, widget);
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
                    // cap_status 是纯装饰；dock_chat 必须能命中子控件（输入框/按钮）
                    element.IsHitTestVisible = widget.id == "dock_chat";
                    if (widget.id == "dock_chat" && element is Border chatBorder)
                    {
                        chatDockHost = EnsureChatDockHost(chatBorder, widget, hudRoot);
                        ChatDockHost = chatDockHost;
                    }
                }
                else if (element is TextBlock)
                    element.IsHitTestVisible = false;
                ApplyVisibility(element, widget);
                if (!(widget.id == "dock_chat" && OverlayNineSlice.IsValid(widget.spriteSlice)))
                    ApplySprite(element, widget, hudRoot);
                ApplyTextStyle(element, widget, hudRoot);
                ApplyButtonLabel(element, widget);
                ApplyStaticTextLabel(element, widget);
                ApplyCozyButtonVisual(element, widget);
                if (widget.id == "txt_error" && element is TextBlock errorText)
                {
                    errorText.TextWrapping = TextWrapping.Wrap;
                    errorText.TextTrimming = TextTrimming.CharacterEllipsis;
                }
                applied++;
            }

            if (byId.TryGetValue("dock_chat", out var dockChat))
            {
                ChatDockWidget = dockChat;
                // Folded chrome is the prefab rect. Expanded chrome is the tallest child
                // plus 10px so the input row does not sit on the dock bottom edge.
                var collapsed = dockChat.h > 0.5 ? dockChat.h : 36;
                var expanded = ComputeChatDockExpandedHeight(byId);
                if (expanded < collapsed)
                    expanded = collapsed;

                ChatDockMetrics = new HudChatDockMetrics
                {
                    X = dockChat.x,
                    Y = dockChat.y,
                    W = dockChat.w,
                    CollapsedH = collapsed,
                    ExpandedH = expanded,
                };
            }

            ApplyChatInputChrome(byId, bindings, hudRoot, ChatDockHost, window);
            ApplyChatLogStyle(byId, bindings, hudRoot, window);

            _appliedWidgets = byId;
            window.ConfigureHudChatDockMetrics(ChatDockMetrics);
            window.ConfigureHudLayoutMode(true);
            IsActive = true;
            System.Diagnostics.Debug.WriteLine("[OverlayHud] Applied " + applied + " widgets (chat nested).");
            return true;
            }
            catch (Exception ex)
            {
                LastLoadError = "HUD 应用失败：" + ex.Message;
                System.Diagnostics.Debug.WriteLine("[OverlayHud] Apply failed: " + ex);
                IsActive = false;
                return false;
            }
        }

        /// <summary>
        /// Re-anchor root HUD widgets from the 960×560 design canvas onto the live viewport.
        /// Does not scale fonts, buttons, or pets.
        /// </summary>
        public void Relayout(MainWindow window, double viewW, double viewH)
        {
            if (!IsActive || window == null || _appliedWidgets == null)
                return;

            viewW = Math.Max(1, viewW);
            viewH = Math.Max(1, viewH);
            var frames = ComputeViewportFrames(_appliedWidgets, viewW, viewH);
            RefineCompactMenuRailFrames(frames, _appliedWidgets, viewH);
            var bindings = BuildBindings(window);

            foreach (var pair in frames)
            {
                if (!bindings.TryGetValue(pair.Key, out var element) || element == null)
                    continue;
                if (LayoutOnlyGroups.Contains(pair.Key))
                    continue;

                Canvas.SetLeft(element, pair.Value.X);
                Canvas.SetTop(element, pair.Value.Y);
                if (pair.Value.W > 0.5)
                    element.Width = pair.Value.W;
                if (pair.Value.H > 0.5)
                    element.Height = pair.Value.H;
            }

            if (frames.TryGetValue("dock_chat", out var chatFrame) &&
                _appliedWidgets.TryGetValue("dock_chat", out var dockChat))
            {
                var collapsed = dockChat.h > 0.5 ? dockChat.h : 36;
                var expanded = ComputeChatDockExpandedHeight(_appliedWidgets);
                if (expanded < collapsed)
                    expanded = collapsed;
                ChatDockMetrics = new HudChatDockMetrics
                {
                    X = chatFrame.X,
                    Y = chatFrame.Y,
                    W = chatFrame.W,
                    CollapsedH = collapsed,
                    ExpandedH = expanded,
                };
                window.ConfigureHudChatDockMetrics(ChatDockMetrics);
                window.ChatDockChrome?.UpdateLayout();
                RefreshChatDockChrome(window.ChatDockChrome);
            }
        }

        struct HudFrame
        {
            public double X;
            public double Y;
            public double W;
            public double H;
        }

        static void RefineCompactMenuRailFrames(
            Dictionary<string, HudFrame> frames,
            Dictionary<string, OverlayHudWidgetDto> byId,
            double viewH)
        {
            if (!byId.TryGetValue("menu_rail", out var rail) ||
                !frames.TryGetValue(rail.id, out var frame))
                return;
            var availableHeight = viewH - Math.Max(0, CanvasHeight - rail.y - rail.h) - 8;
            if (availableHeight >= rail.h)
                return;
            var buttons = MenuRailOrder
                .Where(id => byId.ContainsKey(id) && byId[id].parentId == rail.id)
                .Select(id => byId[id]).ToArray();
            if (buttons.Length == 0)
                return;
            var slotHeight = Math.Max(24, availableHeight / buttons.Length);
            var top = frame.Y + frame.H - slotHeight * buttons.Length;
            for (var index = 0; index < buttons.Length; index++)
            {
                var button = buttons[index];
                var height = Math.Min(button.h, slotHeight);
                frames[button.id] = new HudFrame
                {
                    X = frame.X + button.x,
                    Y = top + index * slotHeight + (slotHeight - height) / 2,
                    W = button.w,
                    H = height,
                };
            }
        }

        static Dictionary<string, HudFrame> ComputeViewportFrames(
            Dictionary<string, OverlayHudWidgetDto> byId,
            double viewW,
            double viewH)
        {
            var frames = new Dictionary<string, HudFrame>(StringComparer.Ordinal);
            byId.TryGetValue("dock_chat", out var chat);
            byId.TryGetValue("dock_fishing", out var fish);
            byId.TryGetValue("cap_status", out var cap);
            byId.TryGetValue("menu_rail", out var rail);

            const double MinGap = 8;
            const double MinMargin = 8;
            const double MinChatW = 88;

            var chatLeft = chat != null ? chat.x : 8;
            var chatW = chat != null && chat.w > 0.5 ? chat.w : 272;
            var chatH = chat != null && chat.h > 0.5 ? chat.h : 45;
            var chatBottom = chat != null
                ? Math.Max(0, CanvasHeight - chat.y - chat.h)
                : 9;

            var fishW = fish != null && fish.w > 0.5 ? fish.w : 218;
            var fishH = fish != null && fish.h > 0.5 ? fish.h : 40;
            var fishBottom = fish != null
                ? Math.Max(0, CanvasHeight - fish.y - fish.h)
                : 14;

            var capW = cap != null && cap.w > 0.5 ? cap.w : 134;
            var capH = cap != null && cap.h > 0.5 ? cap.h : 88;
            var capBottom = cap != null
                ? Math.Max(0, CanvasHeight - cap.y - cap.h)
                : 14;

            var railW = rail != null && rail.w > 0.5 ? rail.w : 40;
            var railH = rail != null && rail.h > 0.5 ? rail.h : 370;
            var railBottom = rail != null
                ? Math.Max(0, CanvasHeight - rail.y - rail.h)
                : 15;
            var railRight = rail != null
                ? Math.Max(0, CanvasWidth - rail.x - rail.w)
                : 8;
            var capRailGap = cap != null && rail != null
                ? Math.Max(0, rail.x - (cap.x + cap.w))
                : 9;

            double fishX;
            double chatX;
            double railX;
            double capX;

            void PlaceFromMargins()
            {
                chatX = chatLeft;
                fishX = (viewW - fishW) / 2.0;
                railX = viewW - railRight - railW;
                capX = rail != null
                    ? railX - capRailGap - capW
                    : viewW - (cap != null ? Math.Max(0, CanvasWidth - cap.x - cap.w) : 8) - capW;
            }

            PlaceFromMargins();

            if (chatX + chatW + MinGap > fishX)
                chatW = Math.Max(MinChatW, fishX - MinGap - chatX);

            var overlapRight = fishX + fishW + MinGap - capX;
            if (overlapRight > 0.5)
            {
                var reducible = Math.Max(0, railRight - MinMargin);
                var reduce = Math.Min(overlapRight, reducible);
                railRight -= reduce;
                if (chatLeft > MinMargin)
                {
                    var leftReduce = Math.Min(chatLeft - MinMargin, Math.Max(0, overlapRight - reduce));
                    chatLeft -= leftReduce;
                }

                PlaceFromMargins();
                if (chatX + chatW + MinGap > fishX)
                    chatW = Math.Max(MinChatW, fishX - MinGap - chatX);
            }

            if (fishX + fishW + MinGap > capX)
            {
                fishX = capX - MinGap - fishW;
                if (chatX + chatW + MinGap > fishX)
                {
                    chatX = MinMargin;
                    chatW = Math.Max(MinChatW, fishX - MinGap - chatX);
                }
            }

            if (chat != null)
            {
                frames["dock_chat"] = new HudFrame
                {
                    X = chatX,
                    Y = viewH - chatBottom - chatH,
                    W = chatW,
                    H = chatH,
                };
            }

            if (fish != null)
            {
                frames["dock_fishing"] = new HudFrame
                {
                    X = fishX,
                    Y = viewH - fishBottom - fishH,
                    W = fishW,
                    H = fishH,
                };
            }

            if (cap != null)
            {
                frames["cap_status"] = new HudFrame
                {
                    X = capX,
                    Y = viewH - capBottom - capH,
                    W = capW,
                    H = capH,
                };
            }

            if (rail != null)
            {
                frames["menu_rail"] = new HudFrame
                {
                    X = railX,
                    Y = viewH - railBottom - railH,
                    W = railW,
                    H = railH,
                };
            }

            PlaceAnchoredRoot(frames, byId, "btn_pan_left", viewW, viewH, "left", "scene");
            PlaceAnchoredRoot(frames, byId, "btn_pan_right", viewW, viewH, "right", "scene");
            if (byId.TryGetValue("txt_error", out var error))
            {
                frames["txt_error"] = new HudFrame
                {
                    X = 16,
                    Y = 16,
                    W = Math.Max(120, viewW - 32),
                    H = error.h > 0.5 ? error.h : 24,
                };
            }

            if (byId.TryGetValue("txt_groundbait", out var bait) &&
                frames.TryGetValue("dock_fishing", out var fishFrame) &&
                fish != null)
            {
                frames["txt_groundbait"] = new HudFrame
                {
                    X = fishFrame.X + (bait.x - fish.x),
                    Y = fishFrame.Y + (bait.y - fish.y),
                    W = bait.w,
                    H = bait.h,
                };
            }

            if (frames.TryGetValue("cap_status", out var capFrame) && cap != null)
            {
                PlaceRelativeTo(frames, byId, "btn_debug_police", cap, capFrame);
                PlaceRelativeTo(frames, byId, "btn_debug_gameplay", cap, capFrame);
            }

            foreach (var widget in byId.Values)
            {
                if (widget == null || string.IsNullOrWhiteSpace(widget.id))
                    continue;
                if (string.IsNullOrWhiteSpace(widget.parentId))
                    continue;
                if (!byId.TryGetValue(widget.parentId, out var parent))
                    continue;
                if (!frames.TryGetValue(widget.parentId, out var parentFrame))
                    continue;

                var childW = widget.w;
                var childX = widget.x;
                if (string.Equals(widget.parentId, "dock_chat", StringComparison.Ordinal) &&
                    chat != null)
                {
                    var delta = parentFrame.W - chat.w;
                    if (IsChatRightPinned(widget.id))
                        childX = widget.x + delta;
                    else
                        childW = Math.Max(8, widget.w + delta);
                }

                frames[widget.id] = new HudFrame
                {
                    X = DockChatChildIds.Contains(widget.id) ? childX : parentFrame.X + childX,
                    Y = DockChatChildIds.Contains(widget.id) ? widget.y : parentFrame.Y + widget.y,
                    W = childW,
                    H = widget.h,
                };
            }

            return frames;
        }

        static bool IsChatRightPinned(string id)
        {
            return id == "chat_toggle" || id == "chat_send" || id == "chat_scroll";
        }

        static void PlaceAnchoredRoot(
            Dictionary<string, HudFrame> frames,
            Dictionary<string, OverlayHudWidgetDto> byId,
            string id,
            double viewW,
            double viewH,
            string horizontal,
            string vertical)
        {
            if (!byId.TryGetValue(id, out var widget))
                return;

            var right = CanvasWidth - widget.x - widget.w;
            var bottom = CanvasHeight - widget.y - widget.h;
            var x = widget.x;
            var y = widget.y;
            if (string.Equals(horizontal, "center", StringComparison.Ordinal))
                x = (viewW - widget.w) / 2.0;
            else if (string.Equals(horizontal, "right", StringComparison.Ordinal))
                x = viewW - right - widget.w;
            if (string.Equals(vertical, "bottom", StringComparison.Ordinal))
                y = viewH - bottom - widget.h;
            else if (string.Equals(vertical, "vcenter", StringComparison.Ordinal))
                y = (viewH - widget.h) / 2.0;
            else if (string.Equals(vertical, "scene", StringComparison.Ordinal))
                y = Math.Max(12, (viewH - 80.0 - widget.h) / 2.0);

            frames[id] = new HudFrame
            {
                X = x,
                Y = y,
                W = widget.w,
                H = widget.h,
            };
        }

        static void PlaceRelativeTo(
            Dictionary<string, HudFrame> frames,
            Dictionary<string, OverlayHudWidgetDto> byId,
            string id,
            OverlayHudWidgetDto origin,
            HudFrame originFrame)
        {
            if (!byId.TryGetValue(id, out var widget) || origin == null)
                return;
            frames[id] = new HudFrame
            {
                X = originFrame.X + (widget.x - origin.x),
                Y = originFrame.Y + (widget.y - origin.y),
                W = widget.w,
                H = widget.h,
            };
        }

        Canvas EnsureChatDockHost(Border chatBorder, OverlayHudWidgetDto widget, string hudRoot)
        {
            CozyTheme.SetIsPaper(chatBorder, false);
            chatBorder.Padding = new Thickness(0);
            chatBorder.Background = Brushes.Transparent;
            chatBorder.CornerRadius = new CornerRadius(0);
            chatBorder.BorderThickness = new Thickness(0);
            chatBorder.ClipToBounds = false;

            var root = new Grid();
            var host = new Canvas
            {
                Background = Brushes.Transparent,
                ClipToBounds = true,
            };
            root.Children.Add(host);
            chatBorder.Child = root;
            RefreshChatDockNineSlice(chatBorder, widget, hudRoot);
            return host;
        }

        OverlayHudWidgetDto ChatDockWidget { get; set; }

        Canvas ChatDockHost { get; set; }

        string HudRootPath { get; set; }

        public void RefreshChatDockChrome(Border border)
        {
            if (border == null || ChatDockWidget == null || string.IsNullOrEmpty(HudRootPath))
                return;
            RefreshChatDockNineSlice(border, ChatDockWidget, HudRootPath);
        }

        void RefreshChatDockNineSlice(Border chatBorder, OverlayHudWidgetDto widget, string hudRoot)
        {
            if (chatBorder == null || widget == null)
                return;
            if (!(chatBorder.Child is Grid root) || root.Children.Count == 0)
                return;

            var host = root.Children[root.Children.Count - 1] as Canvas;
            if (host == null)
                return;

            var width = chatBorder.ActualWidth > 1 ? chatBorder.ActualWidth : widget.w;
            var height = chatBorder.ActualHeight > 1 ? chatBorder.ActualHeight : widget.h;
            if (width < 1)
                width = widget.w;
            if (height < 1)
                height = widget.h;

            if (root.Children.Count > 1)
                root.Children.RemoveAt(0);

            if (OverlayNineSlice.IsValid(widget.spriteSlice) &&
                !string.IsNullOrWhiteSpace(widget.sprite) &&
                LoadBitmapSource(hudRoot, widget.sprite) is BitmapSource source)
            {
                var slice = OverlayNineSlice.TryCreate(source, widget.spriteSlice, width, height);
                if (slice != null)
                {
                    slice.IsHitTestVisible = false;
                    root.Children.Insert(0, slice);
                }
            }

            host.Margin = new Thickness(0);
            host.ClipToBounds = true;
        }

        static double ComputeChatDockExpandedHeight(Dictionary<string, OverlayHudWidgetDto> byId)
        {
            var maxBottom = 0d;
            foreach (var id in DockChatChildIds)
            {
                if (!byId.TryGetValue(id, out var widget))
                    continue;
                if (!string.Equals(widget.parentId, "dock_chat", StringComparison.Ordinal))
                    continue;
                maxBottom = Math.Max(maxBottom, widget.y + widget.h);
            }

            return maxBottom + ChatDockBottomPadding;
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
                { "btn_pan_left", window.BtnPanLeft },
                { "btn_pan_right", window.BtnPanRight },
                { "dock_chat", window.ChatDockChrome },
                { "chat_preview", window.ChatLatestPreview },
                { "chat_log", window.ChatLogScroll },
                { "chat_scroll", window.ChatLogScrollBar },
                { "chat_toggle", window.ChatDockToggle },
                { "chat_input", window.ChatInputPanel },
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
                textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
        }

        static void ApplyButtonLabel(FrameworkElement element, OverlayHudWidgetDto widget)
        {
            if (widget == null || widget.label == null)
                return;
            if (!string.Equals(widget.kind, "button", StringComparison.OrdinalIgnoreCase))
                return;
            if (!(element is Button button))
                return;

            button.Content = widget.label;
        }

        static void ApplyStaticTextLabel(FrameworkElement element, OverlayHudWidgetDto widget)
        {
            if (widget == null || widget.label == null)
                return;
            if (!StaticTextLabels.Contains(widget.id))
                return;
            if (!(element is TextBlock textBlock))
                return;

            textBlock.Text = widget.label;
        }

        void ApplyChatInputChrome(
            Dictionary<string, OverlayHudWidgetDto> byId,
            Dictionary<string, FrameworkElement> bindings,
            string hudRoot,
            Canvas chatDockHost,
            MainWindow window)
        {
            if (window?.ChatInputBox == null)
                return;
            if (!byId.TryGetValue("chat_input", out var inputWidget))
                return;

            byId.TryGetValue("chat_placeholder", out var placeholderWidget);
            if (placeholderWidget != null &&
                bindings.TryGetValue("chat_placeholder", out var placeholderElement) &&
                placeholderElement is TextBlock placeholderBlock)
            {
                ApplyTextStyle(placeholderBlock, placeholderWidget, hudRoot);
                placeholderBlock.IsHitTestVisible = false;
                var lineHeight = placeholderBlock.FontSize * placeholderBlock.FontFamily.LineSpacing;
                placeholderBlock.Padding = new Thickness(0,
                    Math.Max(0, (placeholderWidget.h - lineHeight) / 2), 0, 0);
            }

            var textBox = window.ChatInputBox;
            textBox.BorderThickness = new Thickness(0);
            textBox.Background = Brushes.Transparent;
            textBox.VerticalContentAlignment = VerticalAlignment.Center;
            textBox.IsHitTestVisible = true;
            textBox.Focusable = true;
            textBox.IsReadOnly = false;
            textBox.MinWidth = 0;
            textBox.MinHeight = 0;

            var panel = window.ChatInputPanel;
            if (panel != null)
            {
                panel.IsHitTestVisible = true;
                panel.Padding = new Thickness(0);
                panel.BorderThickness = new Thickness(0);
                Detach(textBox);
                textBox.Margin = new Thickness(0);
                textBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                textBox.VerticalAlignment = VerticalAlignment.Stretch;
                textBox.Width = double.NaN;
                textBox.Height = double.NaN;
                panel.Child = textBox;
                Panel.SetZIndex(panel, 200);
            }
            else if (chatDockHost != null)
            {
                Reparent(chatDockHost, textBox);
                NormalizeForCanvas(textBox);
                Canvas.SetLeft(textBox, inputWidget.x);
                Canvas.SetTop(textBox, inputWidget.y);
                textBox.Width = inputWidget.w;
                textBox.Height = inputWidget.h;
                Panel.SetZIndex(textBox, 200);
            }

            textBox.Padding = ComponentPadding(_components.input.padding);
            textBox.MinHeight = inputWidget.h;
            textBox.HorizontalContentAlignment = HorizontalAlignment.Left;
            textBox.TextWrapping = TextWrapping.NoWrap;
            textBox.AcceptsReturn = false;
            textBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;

            if (placeholderWidget == null)
                return;
            if (placeholderWidget.fontSize > 0)
                textBox.FontSize = placeholderWidget.fontSize;
            if (TryParseColor(placeholderWidget.fontColor, out var color))
            {
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                textBox.Foreground = brush;
                textBox.CaretBrush = brush;
            }
            if (ResolveFontFamily(hudRoot, placeholderWidget.fontFile, placeholderWidget.id) is FontFamily family)
                textBox.FontFamily = family;
        }

        void ApplyChatLogStyle(
            Dictionary<string, OverlayHudWidgetDto> byId,
            Dictionary<string, FrameworkElement> bindings,
            string hudRoot,
            MainWindow window)
        {
            if (window == null)
                return;
            if (!byId.TryGetValue("chat_log", out var logWidget) &&
                !byId.TryGetValue("chat_preview", out logWidget))
                return;

            var fontFamily = ResolveFontFamily(hudRoot, logWidget.fontFile, logWidget.id);
            Brush foreground = null;
            if (TryParseColor(logWidget.fontColor, out var color))
            {
                foreground = new SolidColorBrush(color);
                foreground.Freeze();
            }

            window.ConfigureChatLogStyle(fontFamily, logWidget.fontSize, foreground);

            if (!bindings.TryGetValue("chat_log", out var element) || !(element is ScrollViewer scroll))
                return;

            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Height = logWidget.h > 0 ? logWidget.h : scroll.Height;
            WireChatLogScrollBar(byId, bindings, hudRoot, window, scroll, logWidget);
        }

        ScrollViewer WiredChatLogScroll { get; set; }

        ScrollBar WiredChatLogBar { get; set; }

        bool SyncingChatLogBar { get; set; }

        void WireChatLogScrollBar(
            Dictionary<string, OverlayHudWidgetDto> byId,
            Dictionary<string, FrameworkElement> bindings,
            string hudRoot,
            MainWindow window,
            ScrollViewer scroll,
            OverlayHudWidgetDto logWidget)
        {
            if (window?.ChatLogScrollBar == null || scroll == null)
                return;

            var bar = window.ChatLogScrollBar;
            bar.Orientation = Orientation.Vertical;
            bar.Minimum = 0;
            bar.SmallChange = 16;
            bar.Focusable = false;

            if (!byId.TryGetValue("chat_scroll", out var scrollWidget) && ChatDockHost != null && logWidget != null)
            {
                Reparent(ChatDockHost, bar);
                NormalizeForCanvas(bar);
                Canvas.SetLeft(bar, logWidget.x + Math.Max(8, logWidget.w - 6));
                Canvas.SetTop(bar, logWidget.y);
                bar.Width = 6;
                bar.Height = logWidget.h;
            }

            if (byId.TryGetValue("chat_scroll", out scrollWidget) &&
                !string.IsNullOrWhiteSpace(scrollWidget.sprite))
                ApplySprite(bar, scrollWidget, hudRoot);

            Panel.SetZIndex(bar, 130);

            if (WiredChatLogScroll != null)
                WiredChatLogScroll.ScrollChanged -= ChatLogScroll_OnScrollChanged;
            if (WiredChatLogBar != null)
                WiredChatLogBar.ValueChanged -= ChatLogBar_OnValueChanged;

            WiredChatLogScroll = scroll;
            WiredChatLogBar = bar;
            scroll.ScrollChanged += ChatLogScroll_OnScrollChanged;
            bar.ValueChanged += ChatLogBar_OnValueChanged;
            SyncChatLogScrollBar();
        }

        void ChatLogScroll_OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            SyncChatLogScrollBar();
        }

        void ChatLogBar_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SyncingChatLogBar || WiredChatLogScroll == null)
                return;
            WiredChatLogScroll.ScrollToVerticalOffset(e.NewValue);
        }

        void SyncChatLogScrollBar()
        {
            if (WiredChatLogScroll == null || WiredChatLogBar == null)
                return;

            SyncingChatLogBar = true;
            try
            {
                var viewport = WiredChatLogScroll.ViewportHeight;
                var extent = WiredChatLogScroll.ExtentHeight;
                var max = Math.Max(0, extent - viewport);
                WiredChatLogBar.Maximum = max;
                WiredChatLogBar.ViewportSize = viewport > 0 ? viewport : 1;
                WiredChatLogBar.Value = Math.Min(WiredChatLogScroll.VerticalOffset, max);
            }
            finally
            {
                SyncingChatLogBar = false;
            }
        }

        public static void SetChatToggleRotation(Button button, bool expanded)
        {
            if (button != null)
            {
                CozyTheme.SetIconAngle(button, expanded ? 180 : 0);
                button.ToolTip = expanded ? "收起聊天" : "展开聊天";
            }
        }

        static void ApplyCozyButtonVisual(FrameworkElement element, OverlayHudWidgetDto widget)
        {
            if (!(element is Button button) || widget == null)
                return;
            button.Style = (Style)Application.Current.FindResource("CozyButton");
            CozyTheme.SetNormalSprite(button, widget.sprite);
            CozyTheme.SetHoverSprite(button, widget.spriteHover);
            CozyTheme.SetPressedSprite(button, widget.spritePressed);
            CozyTheme.SetIcon(button, widget.iconSprite);
            CozyTheme.SetIconSize(button, widget.iconSize > 0 ? widget.iconSize : 16);
            button.Foreground = CozyTheme.Ink;
            button.SetResourceReference(Control.PaddingProperty, "CozyButtonPadding");
            EnsureHudButtonScale(button);
            button.Click -= HudButtonPressScale_OnClick;
            button.Click += HudButtonPressScale_OnClick;
        }

        static ScaleTransform EnsureHudButtonScale(Button button)
        {
            button.RenderTransformOrigin = new Point(0.5, 0.5);
            if (button.RenderTransform is TransformGroup group)
            {
                foreach (var child in group.Children)
                {
                    if (child is ScaleTransform existing)
                        return existing;
                }

                var inserted = new ScaleTransform(1, 1);
                group.Children.Insert(0, inserted);
                return inserted;
            }

            if (button.RenderTransform is ScaleTransform onlyScale)
            {
                var wrapped = new TransformGroup();
                wrapped.Children.Add(onlyScale);
                wrapped.Children.Add(new RotateTransform(0));
                button.RenderTransform = wrapped;
                return onlyScale;
            }

            if (button.RenderTransform is RotateTransform onlyRotate)
            {
                var scale = new ScaleTransform(1, 1);
                var wrapped = new TransformGroup();
                wrapped.Children.Add(scale);
                wrapped.Children.Add(onlyRotate);
                button.RenderTransform = wrapped;
                return scale;
            }

            var createdScale = new ScaleTransform(1, 1);
            var createdGroup = new TransformGroup();
            createdGroup.Children.Add(createdScale);
            createdGroup.Children.Add(new RotateTransform(0));
            button.RenderTransform = createdGroup;
            return createdScale;
        }

        static void HudButtonPressScale_OnClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button))
                return;

            var scale = EnsureHudButtonScale(button);

            var pulseX = CreateHudButtonPressScaleAnimation();
            var pulseY = CreateHudButtonPressScaleAnimation();
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, pulseX);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, pulseY);
        }

        static DoubleAnimationUsingKeyFrames CreateHudButtonPressScaleAnimation()
        {
            const double durationSeconds = 0.2;
            var animation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(durationSeconds),
            };
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(durationSeconds * 0.5))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(durationSeconds))));
            return animation;
        }

        static void ApplyCozyResourceMapping(OverlayHudDocument document)
        {
            document.components = CompleteCozyComponents(document.components);
            var components = document.components;
            foreach (var widget in document.widgets)
            {
                if (widget == null)
                    continue;
                if (string.Equals(widget.kind, "button", StringComparison.OrdinalIgnoreCase))
                {
                    widget.sprite = components.button.sprite;
                    widget.spriteHover = components.button.spriteHover;
                    widget.spritePressed = components.button.spritePressed;
                    widget.spriteSlice = (int[])components.button.spriteSlice.Clone();
                    var icon = IconForWidget(widget.id);
                    widget.iconSprite = icon == null ? null : CozyTheme.IconPath(icon);
                    if (MenuRailOrder.Contains(widget.id))
                        widget.iconSize = 22;
                    if (icon != null && (string.IsNullOrWhiteSpace(widget.label) ||
                        MenuRailOrder.Contains(widget.id) || widget.id == "chat_toggle" || widget.id == "chat_send"))
                    {
                        widget.label = string.Empty;
                        widget.contentAlign = "center";
                        widget.contentVAlign = "center";
                        widget.verticalAlign = "center";
                    }
                    if (widget.id == "btn_pan_left")
                        widget.label = "向左";
                    else if (widget.id == "btn_pan_right")
                        widget.label = "向右";
                }
                if (widget.id == "dock_chat" || widget.id == "cap_status" || widget.id == "chat_input")
                {
                    var panel = widget.id == "chat_input" ? components.input : components.paper;
                    widget.sprite = panel.sprite;
                    widget.spriteSlice = (int[])panel.spriteSlice.Clone();
                    if (widget.sprite == CozyTheme.Paper && Math.Min(widget.w, widget.h) < 48)
                    {
                        widget.sprite = CozyTheme.CompactPaper;
                        widget.spriteSlice = (int[])CozyTheme.SpriteBorder.Clone();
                    }
                }
                if (widget.id == "chat_scroll")
                    widget.sprite = null;
                widget.fontColor = "#FF75472E";
            }
            ApplyCozyLayoutRefinement(document);
        }

        static void ApplyCozyLayoutRefinement(OverlayHudDocument document)
        {
            var byId = document.widgets
                .Where(widget => widget != null && !string.IsNullOrWhiteSpace(widget.id))
                .ToDictionary(widget => widget.id, StringComparer.Ordinal);
            if (byId.TryGetValue("menu_rail", out var rail))
            {
                var buttons = MenuRailOrder
                    .Where(id => byId.ContainsKey(id) && byId[id].parentId == rail.id)
                    .Select(id => byId[id]).ToArray();
                var slotHeight = buttons.Length > 0 ? buttons.Max(button => button.h) : 40;
                var requiredHeight = buttons.Length * slotHeight;
                var bottom = rail.y + rail.h;
                rail.h = Math.Max(rail.h, requiredHeight);
                rail.y = bottom - rail.h;
                for (var index = 0; index < buttons.Length; index++)
                {
                    var button = buttons[index];
                    button.x = (rail.w - button.w) / 2;
                    button.y = index * slotHeight + (slotHeight - button.h) / 2;
                }
            }

            if (!byId.TryGetValue("chat_input", out var input))
                return;
            var padding = ComponentPadding(document.components.input.padding);
            var fontSize = byId.TryGetValue("chat_placeholder", out var placeholder) && placeholder.fontSize > 0
                ? placeholder.fontSize : 12;
            input.h = Math.Max(input.h, Math.Max(ChatInputMinHeight,
                Math.Ceiling(fontSize * 1.5) + padding.Top + padding.Bottom));
            if (placeholder != null)
            {
                placeholder.x = input.x + padding.Left;
                placeholder.y = input.y + padding.Top;
                placeholder.w = Math.Max(8, input.w - padding.Left - padding.Right);
                placeholder.h = input.h - padding.Top - padding.Bottom;
                placeholder.verticalAlign = "center";
                placeholder.fontWeight = "normal";
            }
            if (byId.TryGetValue("chat_send", out var send))
            {
                send.h = Math.Max(send.h, ChatInputMinHeight);
                send.y = input.y + (input.h - send.h) / 2;
            }
        }

        static Thickness ComponentPadding(double[] padding)
        {
            return padding != null && padding.Length == 4
                ? new Thickness(padding[0], padding[1], padding[2], padding[3])
                : new Thickness(0);
        }

        static OverlayHudComponentsDto CompleteCozyComponents(OverlayHudComponentsDto components)
        {
            var defaults = CreateCozyComponents();
            if (components == null)
                return defaults;
            components.button = components.button ?? defaults.button;
            components.paper = components.paper ?? defaults.paper;
            components.input = components.input ?? defaults.input;
            components.scroll = components.scroll ?? defaults.scroll;
            foreach (var panel in new[] { components.button, components.paper, components.input })
            {
                var expected = ReferenceEquals(panel, components.paper) ? CozyTheme.PaperBorder : CozyTheme.SpriteBorder;
                if (panel.spriteSlice == null || panel.spriteSlice.SequenceEqual(new[] { 64, 64, 64, 64 }))
                    panel.spriteSlice = (int[])expected.Clone();
                if (!panel.spriteSlice.SequenceEqual(expected))
                    throw new InvalidDataException("Cozy spriteSlice 与对应底板的像素边距不匹配");
            }
            components.button.sprite = components.button.sprite ?? defaults.button.sprite;
            components.button.spriteHover = components.button.spriteHover ?? defaults.button.spriteHover;
            components.button.spritePressed = components.button.spritePressed ?? defaults.button.spritePressed;
            components.paper.sprite = components.paper.sprite ?? defaults.paper.sprite;
            components.input.sprite = components.input.sprite ?? defaults.input.sprite;
            if (components.input.sprite == CozyTheme.Paper)
                components.input.sprite = CozyTheme.CompactPaper;
            components.button.padding = components.button.padding ?? defaults.button.padding;
            components.paper.padding = components.paper.padding ?? defaults.paper.padding;
            components.input.padding = components.input.padding ?? defaults.input.padding;
            if (components.input.padding.Length == 4)
            {
                components.input.padding = (double[])components.input.padding.Clone();
                components.input.padding[1] = Math.Max(0, Math.Min(4, components.input.padding[1]));
                components.input.padding[3] = Math.Max(0, Math.Min(4, components.input.padding[3]));
            }
            else
                components.input.padding = defaults.input.padding;
            if (components.button.pressedOffsetY <= 0) components.button.pressedOffsetY = 3;
            if (components.button.iconSize <= 0) components.button.iconSize = 16;
            if (components.button.disabledOpacity <= 0 || components.button.disabledOpacity > 1)
                components.button.disabledOpacity = 0.45;
            if (components.scroll.width <= 0) components.scroll.width = 6;
            return components;
        }

        static void ApplyCozyComponentParameters(OverlayHudComponentsDto components)
        {
            var resources = Application.Current.Resources;
            resources["CozyPressedOffset"] = components.button.pressedOffsetY;
            resources["CozyIconSize"] = components.button.iconSize;
            resources["CozyDisabledOpacity"] = components.button.disabledOpacity;
            resources["CozyButtonPadding"] = ComponentPadding(components.button.padding);
            resources["CozyPaperPadding"] = ComponentPadding(components.paper.padding);
            resources["CozyInputPadding"] = ComponentPadding(components.input.padding);
            resources["CozyScrollWidth"] = components.scroll.width;
        }

        static OverlayHudComponentsDto CreateCozyComponents()
        {
            return new OverlayHudComponentsDto
            {
                button = new OverlayHudComponentDto
                {
                    sprite = CozyTheme.DefaultPlate, spriteHover = CozyTheme.HoverPlate,
                    spritePressed = CozyTheme.PressedPlate, spriteSlice = (int[])CozyTheme.SpriteBorder.Clone(),
                    pressedOffsetY = 3, iconSize = 16, disabledOpacity = 0.45,
                    padding = new double[] { 2, 0, 2, 0 },
                },
                paper = new OverlayHudComponentDto
                {
                    sprite = CozyTheme.Paper, spriteSlice = (int[])CozyTheme.PaperBorder.Clone(),
                    padding = new double[] { 12, 8, 12, 8 },
                },
                input = new OverlayHudComponentDto
                {
                    sprite = CozyTheme.CompactPaper, spriteSlice = (int[])CozyTheme.SpriteBorder.Clone(),
                    padding = new double[] { 8, 4, 8, 4 },
                },
                scroll = new OverlayHudComponentDto { width = 6 },
            };
        }

        static string IconForWidget(string id)
        {
            switch (id)
            {
                case "btn_menu_toggle": case "btn_menu_settings": case "btn_debug_gameplay": return "settings";
                case "btn_menu_map": return "map";
                case "btn_menu_shop": return "shop";
                case "btn_menu_friends": case "chat_toggle": return "social";
                case "btn_menu_catch": return "bag";
                case "btn_menu_leaderboard": return "trophy";
                case "btn_open_main": return "home";
                case "btn_exit_pond": return "lock";
                case "btn_catch_leave": return "gift";
                case "btn_debug_police": return "bell";
                case "btn_fishing_toggle": return "rod";
                case "btn_groundbait": return "leaf";
                case "chat_send": return "check";
                default: return null;
            }
        }

        public void ExportCozyJson(string destination)
        {
            var source = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "OverlayResources", "hud", "overlay-hud.json");
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请导出到新文件，不覆盖当前 HUD 配置");
            var serializer = new DataContractJsonSerializer(typeof(OverlayHudDocument));
            OverlayHudDocument document;
            using (var stream = File.OpenRead(source))
                document = serializer.ReadObject(stream) as OverlayHudDocument;
            if (document?.widgets == null)
                throw new InvalidDataException("HUD JSON 缺少 widgets");
            ApplyCozyResourceMapping(document);
            using (var stream = File.Create(destination))
                serializer.WriteObject(stream, document);
        }

        void ApplyTextStyle(FrameworkElement element, OverlayHudWidgetDto widget, string hudRoot)
        {
            if (element == null || widget == null)
                return;

            var isText = string.Equals(widget.kind, "text", StringComparison.OrdinalIgnoreCase);
            var isButton = string.Equals(widget.kind, "button", StringComparison.OrdinalIgnoreCase);
            if (!isText && !isButton)
                return;

            var fontFamily = ResolveFontFamily(hudRoot, widget.fontFile, widget.id);
            if (fontFamily != null)
                SetFontFamily(element, fontFamily);

            if (widget.fontSize > 0)
                SetFontSize(element, widget.fontSize);

            if (TryParseColor(widget.fontColor, out var color))
                SetForeground(element, color);

            if (string.Equals(widget.fontWeight, "bold", StringComparison.OrdinalIgnoreCase))
                SetFontWeight(element, FontWeights.Bold);

            if (isText && element is TextBlock textBlock)
            {
                textBlock.TextAlignment = ParseTextAlignment(widget.textAlign);
                textBlock.HorizontalAlignment = HorizontalAlignment.Stretch;
                textBlock.VerticalAlignment = ParseVerticalAlignment(
                    string.IsNullOrWhiteSpace(widget.verticalAlign) ? widget.contentVAlign : widget.verticalAlign);
            }
            else if (isButton && element is Button button)
            {
                var align = string.IsNullOrWhiteSpace(widget.contentAlign)
                    ? widget.textAlign
                    : widget.contentAlign;
                var vAlign = string.IsNullOrWhiteSpace(widget.contentVAlign)
                    ? widget.verticalAlign
                    : widget.contentVAlign;
                button.HorizontalContentAlignment = ParseHorizontalAlignment(align);
                button.VerticalContentAlignment = ParseVerticalContentAlignment(vAlign);
            }
        }

        FontFamily ResolveFontFamily(string hudRoot, string fontFile, string widgetId)
        {
            if (string.IsNullOrWhiteSpace(fontFile))
                return null;

            var fontPath = Path.Combine(hudRoot, "fonts", fontFile);
            if (_fontCache.TryGetValue(fontPath, out var cached))
                return cached;

            if (!File.Exists(fontPath))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[OverlayHud] Missing font for " + widgetId + ": " + fontPath + " (using system font)");
                return null;
            }

            try
            {
                var uri = new Uri(Path.GetFullPath(fontPath), UriKind.Absolute);
                var familyName = Path.GetFileNameWithoutExtension(fontFile);
                var family = new FontFamily(uri, "./#" + familyName);
                _fontCache[fontPath] = family;
                return family;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[OverlayHud] Font load failed for " + widgetId + ": " + ex.Message);
                return null;
            }
        }

        static void SetFontFamily(FrameworkElement element, FontFamily fontFamily)
        {
            if (element is Control control)
                control.FontFamily = fontFamily;
            else if (element is TextBlock textBlock)
                textBlock.FontFamily = fontFamily;
        }

        static void SetFontSize(FrameworkElement element, double fontSize)
        {
            if (element is Control control)
                control.FontSize = fontSize;
            else if (element is TextBlock textBlock)
                textBlock.FontSize = fontSize;
        }

        static void SetForeground(FrameworkElement element, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            if (element is Control control)
                control.Foreground = brush;
            else if (element is TextBlock textBlock)
                textBlock.Foreground = brush;
        }

        static void SetFontWeight(FrameworkElement element, FontWeight weight)
        {
            if (element is Control control)
                control.FontWeight = weight;
            else if (element is TextBlock textBlock)
                textBlock.FontWeight = weight;
        }

        static bool TryParseColor(string value, out Color color)
        {
            color = Colors.White;
            if (string.IsNullOrWhiteSpace(value) || value[0] != '#')
                return false;

            var hex = value.Substring(1);
            if (hex.Length != 8)
                return false;

            if (!byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var a) ||
                !byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
                !byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
                !byte.TryParse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                return false;

            color = Color.FromArgb(a, r, g, b);
            return true;
        }

        static TextAlignment ParseTextAlignment(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "center":
                    return TextAlignment.Center;
                case "right":
                    return TextAlignment.Right;
                default:
                    return TextAlignment.Left;
            }
        }

        static HorizontalAlignment ParseHorizontalAlignment(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "left":
                    return HorizontalAlignment.Left;
                case "right":
                    return HorizontalAlignment.Right;
                default:
                    return HorizontalAlignment.Center;
            }
        }

        static VerticalAlignment ParseVerticalAlignment(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "top":
                    return VerticalAlignment.Top;
                case "bottom":
                    return VerticalAlignment.Bottom;
                default:
                    return VerticalAlignment.Center;
            }
        }

        static VerticalAlignment ParseVerticalContentAlignment(string value)
        {
            return ParseVerticalAlignment(value);
        }

        void ApplySprite(FrameworkElement element, OverlayHudWidgetDto widget, string hudRoot)
        {
            if (element is Button)
                return;
            if (element is Border paper && widget.sprite == CozyTheme.Paper && widget.id != "dock_chat")
            {
                CozyTheme.SetIsPaper(paper, true);
                return;
            }
            if (string.IsNullOrWhiteSpace(widget.sprite))
                return;

            var spritePath = Path.Combine(hudRoot, widget.sprite);
            if (!File.Exists(spritePath))
                return;

            if (OverlayNineSlice.IsValid(widget.spriteSlice) &&
                string.Equals(widget.id, "dock_chat", StringComparison.Ordinal) &&
                element is Border)
                return;

            if (OverlayNineSlice.IsValid(widget.spriteSlice))
            {
                var width = element.Width > 1 ? element.Width : widget.w;
                var height = element.Height > 1 ? element.Height : widget.h;
                var sliced = OverlayNineSlice.TryGetBrush(
                    LoadBitmapSource(hudRoot, widget.sprite),
                    widget.spriteSlice,
                    width,
                    height);
                if (sliced != null)
                {
                    if (element is Control slicedControl)
                    {
                        slicedControl.Background = sliced;
                        return;
                    }

                    if (element is Border slicedBorder)
                    {
                        slicedBorder.Background = sliced;
                        return;
                    }
                }
            }

            if (!_spriteCache.TryGetValue(spritePath, out var brush))
            {
                var image = LoadBitmapSource(hudRoot, widget.sprite);
                if (image == null)
                    return;
                brush = new ImageBrush(image) { Stretch = Stretch.Fill };
                brush.Freeze();
                _spriteCache[spritePath] = brush;
            }

            if (element is Control control)
                control.Background = brush;
            else if (element is Border border)
                border.Background = brush;
        }

        BitmapSource LoadBitmapSource(string hudRoot, string spriteFile)
        {
            if (string.IsNullOrWhiteSpace(spriteFile))
                return null;

            var spritePath = Path.Combine(hudRoot, spriteFile);
            if (_bitmapCache.TryGetValue(spritePath, out var cached))
                return cached;
            if (!File.Exists(spritePath))
                return null;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(spritePath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            _bitmapCache[spritePath] = image;
            return image;
        }

        [DataContract]
        sealed class OverlayHudDocument
        {
            [DataMember(Name = "width")] public double width = CanvasWidth;
            [DataMember(Name = "height")] public double height = CanvasHeight;
            [DataMember(Name = "widgets")] public OverlayHudWidgetDto[] widgets = null;
            [DataMember(Name = "components", EmitDefaultValue = false)] public OverlayHudComponentsDto components = null;
        }

        [DataContract]
        sealed class OverlayHudComponentsDto
        {
            [DataMember(Name = "button")] public OverlayHudComponentDto button;
            [DataMember(Name = "paper")] public OverlayHudComponentDto paper;
            [DataMember(Name = "input")] public OverlayHudComponentDto input;
            [DataMember(Name = "scroll")] public OverlayHudComponentDto scroll;
        }

        [DataContract]
        sealed class OverlayHudComponentDto
        {
            [DataMember(Name = "sprite", EmitDefaultValue = false)] public string sprite;
            [DataMember(Name = "spriteHover", EmitDefaultValue = false)] public string spriteHover;
            [DataMember(Name = "spritePressed", EmitDefaultValue = false)] public string spritePressed;
            [DataMember(Name = "spriteSlice", EmitDefaultValue = false)] public int[] spriteSlice;
            [DataMember(Name = "pressedOffsetY", EmitDefaultValue = false)] public double pressedOffsetY;
            [DataMember(Name = "iconSize", EmitDefaultValue = false)] public double iconSize;
            [DataMember(Name = "disabledOpacity", EmitDefaultValue = false)] public double disabledOpacity;
            [DataMember(Name = "padding", EmitDefaultValue = false)] public double[] padding;
            [DataMember(Name = "width", EmitDefaultValue = false)] public double width;
        }

        [DataContract]
        internal sealed class OverlayHudWidgetDto
        {
            [DataMember(Name = "id")] public string id = null;
            [DataMember(Name = "parentId")] public string parentId = null;
            [DataMember(Name = "kind")] public string kind = null;
            [DataMember(Name = "x")] public double x;
            [DataMember(Name = "y")] public double y;
            [DataMember(Name = "w")] public double w;
            [DataMember(Name = "h")] public double h;
            [DataMember(Name = "sprite")] public string sprite = null;
            [DataMember(Name = "spriteHover", EmitDefaultValue = false)] public string spriteHover = null;
            [DataMember(Name = "spritePressed", EmitDefaultValue = false)] public string spritePressed = null;
            [DataMember(Name = "iconSprite", EmitDefaultValue = false)] public string iconSprite = null;
            [DataMember(Name = "spriteSlice")] public int[] spriteSlice = null;
            [DataMember(Name = "fontFile")] public string fontFile = null;
            [DataMember(Name = "fontSize")] public double fontSize;
            [DataMember(Name = "fontColor")] public string fontColor = null;
            [DataMember(Name = "fontWeight")] public string fontWeight = null;
            [DataMember(Name = "textAlign")] public string textAlign = null;
            [DataMember(Name = "contentAlign")] public string contentAlign = null;
            [DataMember(Name = "contentVAlign")] public string contentVAlign = null;
            [DataMember(Name = "verticalAlign")] public string verticalAlign = null;
            [DataMember(Name = "label")] public string label = null;
            [DataMember(Name = "z")] public int z;
            [DataMember(Name = "visibleDefault")] public bool? visibleDefault = null;
        }
    }
}
