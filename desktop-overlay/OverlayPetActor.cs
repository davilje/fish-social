using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FishSocialOverlay
{
    /// <summary>
    /// Pond pet. Display size comes from actor-pet (default 128 for 512 art).
    /// Hover / right-click hot-zone is actor-hit when present, else actor-pet.
    /// </summary>
    public sealed class OverlayPetActor : Grid
    {
        public const double BodySize = 64;
        const double RingSize = 31;
        const double RingStroke = 2.5;
        const double IconSize = OverlayStatusIcons.Size;
        const double PanelMinHeight = 20;
        const double NameMinWidth = 88;
        const double NameMaxWidth = 180;
        const double BubbleMinWidth = 80;
        const double BubbleMaxWidth = 160;
        const double HintMinWidth = 84;
        const double HintMaxWidth = 200;

        readonly IOverlayHoverHost _hoverHost;
        IOverlayPlayerMenuHost _menuHost;
        string _playerId = string.Empty;
        bool _isBot;
        readonly Image _image;
        readonly Rectangle _hitCatcher;
        readonly Image _statusIcon;
        readonly Canvas _placeholder;
        readonly Shape[] _tintShapes;
        readonly TextBlock _nickname;
        readonly Border _nameBadge;
        readonly TextBlock _speechLabel;
        readonly Border _speechBadge;
        readonly TextBlock _hintLabel;
        readonly Border _hintBadge;
        readonly Rectangle _hintAnchor;
        DispatcherTimer _speechFadeTimer;
        readonly Image _ringBg;
        readonly Image _hookRingImage;
        readonly System.Windows.Shapes.Path _hookRing;
        readonly Canvas _content;
        readonly Grid _stage;
        readonly Grid _body;
        OverlayActorChrome _chrome;
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
        string _petId = string.Empty;
        string _fishingPhase = string.Empty;
        long _sessionAnchorMs;
        long _hookDeadlineMs;
        long _hookTotalMs;
        int _sessionCatchCount;

        public string ActorKey { get; }

        public bool HasPlayerContextMenu =>
            !string.IsNullOrEmpty(_playerId) && _menuHost != null;

        public OverlayPetActor(string actorKey, IOverlayHoverHost hoverHost)
        {
            ActorKey = actorKey;
            _hoverHost = hoverHost;
            Width = RingSize + 8;
            Height = RingSize + IconSize + 28;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            Background = null;
            ClipToBounds = false;
            // Root/chrome pass through; only pet body art receives pointer hits.
            IsHitTestVisible = true;
            ToolTipService.SetIsEnabled(this, false);
            ContextMenuService.SetIsEnabled(this, false);

            _placeholder = BuildPlaceholder(out _tintShapes);
            _placeholder.IsHitTestVisible = false;
            _image = new Image
            {
                Width = BodySize,
                Height = BodySize,
                Stretch = Stretch.Uniform,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };
            _hitCatcher = new Rectangle
            {
                Width = BodySize,
                Height = BodySize,
                Fill = Brushes.Transparent,
                Stroke = null,
                IsHitTestVisible = true,
            };
            _nickname = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 11,
                MaxWidth = BodySize + 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            _nameBadge = CreateTextBadge(_nickname, new Thickness(0, 2, 0, 0));
            _nameBadge.IsHitTestVisible = false;
            _speechLabel = CreateBubbleLabel(11);
            _speechBadge = CreateSpeechBadge(_speechLabel);
            _hintLabel = CreateBubbleLabel(12);
            _hintLabel.FontWeight = FontWeights.SemiBold;
            _hintBadge = CreateHintBadge(_hintLabel);
            _hintAnchor = new Rectangle
            {
                Width = HintMinWidth,
                Height = PanelMinHeight,
                Fill = Brushes.Transparent,
                Stroke = null,
                IsHitTestVisible = false,
            };
            _statusIcon = new Image
            {
                Width = IconSize,
                Height = IconSize,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 0, 2),
                HorizontalAlignment = HorizontalAlignment.Center,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
            };
            _ringBg = new Image
            {
                Width = RingSize,
                Height = RingSize,
                Stretch = Stretch.Fill,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
            };
            _hookRingImage = new Image
            {
                Width = RingSize,
                Height = RingSize,
                Stretch = Stretch.Fill,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
            };
            _hookRing = new System.Windows.Shapes.Path
            {
                Width = RingSize,
                Height = RingSize,
                Stroke = new SolidColorBrush(Color.FromRgb(232, 156, 64)),
                StrokeThickness = RingStroke,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Fill = Brushes.Transparent,
                Stretch = Stretch.None,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
            };

            _body = new Grid
            {
                Width = BodySize,
                Height = BodySize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = null,
                IsHitTestVisible = false,
            };
            _body.Children.Add(_placeholder);
            _body.Children.Add(_image);

            _stage = new Grid
            {
                Width = BodySize,
                Height = BodySize,
                Background = null,
                IsHitTestVisible = true,
            };
            _stage.Children.Add(_body);

            _content = new Canvas { ClipToBounds = false, IsHitTestVisible = true };
            _content.Children.Add(_ringBg);
            _content.Children.Add(_hookRingImage);
            _content.Children.Add(_hookRing);
            _content.Children.Add(_statusIcon);
            _content.Children.Add(_stage);
            _content.Children.Add(_hitCatcher);
            _content.Children.Add(_hintAnchor);
            _content.Children.Add(_nameBadge);
            _content.Children.Add(_speechBadge);
            _content.Children.Add(_hintBadge);
            Panel.SetZIndex(_hintAnchor, 1);
            Panel.SetZIndex(_stage, 10);
            Panel.SetZIndex(_hitCatcher, 10);
            Panel.SetZIndex(_ringBg, 11);
            Panel.SetZIndex(_hookRingImage, 12);
            Panel.SetZIndex(_hookRing, 12);
            Panel.SetZIndex(_statusIcon, 14);
            Panel.SetZIndex(_speechBadge, 18);
            Panel.SetZIndex(_hintBadge, 19);
            Children.Add(_content);
            RelayoutCluster();

            _tooltipTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _tooltipTimer.Tick += OnTooltipTimerTick;

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
        }

        /// <summary>
        /// True when the pointer hit the pet sprite rect (actor-hit, else actor-pet),
        /// not nickname, status icon, or hook ring.
        /// </summary>
        public bool IsPetArtSource(DependencyObject source)
        {
            while (source != null)
            {
                if (ReferenceEquals(source, _hitCatcher) ||
                    ReferenceEquals(source, _body) ||
                    ReferenceEquals(source, _image) ||
                    ReferenceEquals(source, _placeholder))
                    return true;
                if (ReferenceEquals(source, this))
                    return false;
                if (source is Visual || source is Visual3D)
                    source = VisualTreeHelper.GetParent(source);
                else
                    source = LogicalTreeHelper.GetParent(source);
            }

            return false;
        }

        /// <summary>
        /// Geometry hit for social right-click even when OriginalSource fell through
        /// to the seat / pond under a transparent ring/status area.
        /// </summary>
        public bool HitTestsPetArt(Point pointInAncestor, UIElement ancestor)
        {
            if (ancestor == null || _hitCatcher == null ||
                Visibility != Visibility.Visible || !IsHitTestVisible)
                return false;
            try
            {
                var topLeft = _hitCatcher.TransformToAncestor(ancestor).Transform(new Point(0, 0));
                var w = _hitCatcher.ActualWidth > 1 ? _hitCatcher.ActualWidth : _hitCatcher.Width;
                var h = _hitCatcher.ActualHeight > 1 ? _hitCatcher.ActualHeight : _hitCatcher.Height;
                if (w < 1 || h < 1)
                    return false;
                return new Rect(topLeft.X, topLeft.Y, w, h).Contains(pointInAncestor);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        ContextMenu _socialMenu;

        public bool IsPlayerContextMenuOpen =>
            _socialMenu != null && _socialMenu.IsOpen;

        /// <summary>
        /// Open the other-player social menu. Called from PondScene Preview so it
        /// wins over the pond product ContextMenu.
        /// </summary>
        public bool TryOpenPlayerContextMenu()
        {
            if (!HasPlayerContextMenu)
                return false;
            CancelTooltip();
            ShowPlayerContextMenu();
            return true;
        }

        public void ClosePlayerContextMenu()
        {
            if (_socialMenu != null && _socialMenu.IsOpen)
                _socialMenu.IsOpen = false;
        }

        void ShowPlayerContextMenu()
        {
            EnsureSocialMenu();
            if (_socialMenu.IsOpen)
                _socialMenu.IsOpen = false;

            // Keep menu unattached from FrameworkElement.ContextMenu so WPF
            // ContextMenuService cannot sticky-steal later gestures.
            ContextMenu = null;
            OverlayInteractionState.NotifyContextMenuOpened();
            _socialMenu.PlacementTarget = this;
            _socialMenu.Placement = PlacementMode.MousePoint;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (_socialMenu != null && HasPlayerContextMenu)
                    _socialMenu.IsOpen = true;
                else
                    OverlayInteractionState.NotifyContextMenuClosed();
            }));
        }

        void EnsureSocialMenu()
        {
            if (_socialMenu != null)
            {
                RefreshSocialMenuItems();
                return;
            }

            _socialMenu = new ContextMenu();
            ContextMenuService.SetIsEnabled(_socialMenu, true);
            _socialMenu.Closed += (_, __) => OverlayInteractionState.NotifyContextMenuClosed();
            RefreshSocialMenuItems();
        }

        void RefreshSocialMenuItems()
        {
            if (_socialMenu == null)
                return;
            _socialMenu.Items.Clear();
            _socialMenu.Items.Add(CreateMenuItem("查看资料", true, () =>
                _menuHost.SendPlayerCommand("player_open_profile", _playerId)));
            _socialMenu.Items.Add(CreateMenuItem("添加好友", !_isBot, () =>
                _menuHost.SendPlayerCommand("player_add_friend", _playerId)));
            _socialMenu.Items.Add(CreateMenuItem("私聊", !_isBot, () =>
                _menuHost.SendPlayerCommand("player_open_dm", _playerId)));
            _socialMenu.Items.Add(CreateMenuItem("点赞互动", true, () =>
                _menuHost.SendPlayerCommand("player_like_recent", _playerId)));
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
            long fishingStartedAt = 0,
            string petId = null,
            int sessionCatchCount = 0,
            bool groundbaitActive = false)
        {
            _nickname.Text = string.IsNullOrWhiteSpace(nickname) ? "玩家" : nickname;
            _hookDeadlineMs = hookDeadlineMs;
            _sessionCatchCount = Math.Max(0, sessionCatchCount);

            var phase = string.IsNullOrWhiteSpace(fishingPhase)
                ? InferPhaseFromVisual(visualState)
                : fishingPhase;
            if (IsRingPhase(phase) && _hookDeadlineMs > 0 && _hookTotalMs <= 0)
            {
                var remaining = _hookDeadlineMs - NowMs();
                if (remaining > 0)
                    _hookTotalMs = remaining;
            }
            else if (!IsRingPhase(phase))
            {
                _hookTotalMs = 0;
            }

            _fishingPhase = phase ?? string.Empty;
            SyncSessionAnchor(Math.Max(0, sessionFishingMs), fishingStartedAt);
            UpdateStatusVisuals();
            ApplyVisualState(visualState, petId);
            RelayoutCluster();
            RefreshOpenHover();
        }

        public void ApplyChrome(OverlayActorChrome chrome)
        {
            _chrome = chrome;
            RelayoutCluster();
        }

        public void Place(double centerX, double centerY)
        {
            _centerX = centerX;
            _centerY = centerY;
            UpdateLayout();
            var originX = BodySize * 0.5;
            var originY = BodySize * 0.5;
            if (_body != null && _body.IsVisible)
            {
                var origin = _body.TranslatePoint(
                    new Point(_body.Width * 0.5, _body.Height * 0.5),
                    this);
                if (!double.IsNaN(origin.X) && !double.IsNaN(origin.Y))
                {
                    originX = origin.X;
                    originY = origin.Y;
                }
            }

            Canvas.SetLeft(this, centerX - originX);
            Canvas.SetTop(this, centerY - originY);
            if (_hoverShown)
                TryShowHover();
        }

        void ApplyVisualState(string visualState, string petId)
        {
            var nextState = OverlayFrameCache.NormalizeClip(visualState);
            var nextPet = petId ?? string.Empty;
            if (string.Equals(_visualState, nextState, StringComparison.Ordinal) &&
                string.Equals(_petId, nextPet, StringComparison.Ordinal))
                return;

            _visualState = nextState;
            _petId = nextPet;
            _frames = OverlayFrameCache.Get(_petId, _visualState);
            _frameIndex = 0;
            if (_frames.Length == 0)
                ApplyTint(_visualState);
            ShowFrame();
            _frameTimer.Stop();
            if (_frames.Length > 1)
                _frameTimer.Start();
        }

        void RelayoutCluster()
        {
            var petW = _chrome != null && _chrome.Pet != null && _chrome.Pet.w > 0
                ? _chrome.Pet.w
                : BodySize;
            var petH = _chrome != null && _chrome.Pet != null && _chrome.Pet.h > 0
                ? _chrome.Pet.h
                : BodySize;
            _body.Width = petW;
            _body.Height = petH;
            _image.Width = petW;
            _image.Height = petH;
            _stage.Width = petW;
            _stage.Height = petH;

            var ringSlot = _chrome != null && _chrome.Ring != null ? _chrome.Ring : null;
            var ringBgSlot = _chrome != null && _chrome.RingBg != null ? _chrome.RingBg : ringSlot;
            var ringW = ringSlot != null && ringSlot.w > 0 ? ringSlot.w : RingSize;
            var ringH = ringSlot != null && ringSlot.h > 0 ? ringSlot.h : RingSize;
            var ringBgW = ringBgSlot != null && ringBgSlot.w > 0 ? ringBgSlot.w : ringW;
            var ringBgH = ringBgSlot != null && ringBgSlot.h > 0 ? ringBgSlot.h : ringH;
            _hookRing.Width = ringW;
            _hookRing.Height = ringH;
            _hookRingImage.Width = ringW;
            _hookRingImage.Height = ringH;
            _ringBg.Width = ringBgW;
            _ringBg.Height = ringBgH;

            var statusW = _chrome != null && _chrome.Status != null && _chrome.Status.w > 0
                ? _chrome.Status.w
                : IconSize;
            var statusH = _chrome != null && _chrome.Status != null && _chrome.Status.h > 0
                ? _chrome.Status.h
                : IconSize;
            _statusIcon.Width = statusW;
            _statusIcon.Height = statusH;

            var nameSlotW = SlotWidth(_chrome != null ? _chrome.Name : null, NameMinWidth);
            var nameSlotH = SlotHeight(_chrome != null ? _chrome.Name : null);
            var nameFit = FitPanel(_nickname, _nameBadge, nameSlotW, nameSlotH, NameMaxWidth);
            var nameW = nameFit.Width;
            var nameH = nameFit.Height;

            var bubbleSlotW = SlotWidth(_chrome != null ? _chrome.Bubble : null, BubbleMinWidth);
            var bubbleSlotH = SlotHeight(_chrome != null ? _chrome.Bubble : null);
            var bubbleVisible = _speechBadge.Visibility == Visibility.Visible;
            var bubbleFit = bubbleVisible
                ? FitPanel(_speechLabel, _speechBadge, bubbleSlotW, bubbleSlotH, BubbleMaxWidth)
                : new Size(bubbleSlotW, bubbleSlotH);
            var bubbleW = bubbleFit.Width;
            var bubbleH = bubbleFit.Height;

            var hintSlotW = SlotWidth(_chrome != null ? _chrome.Hint : null, HintMinWidth);
            var hintSlotH = SlotHeight(_chrome != null ? _chrome.Hint : null);
            var hintVisible = _hintBadge.Visibility == Visibility.Visible;
            var hintFit = hintVisible
                ? FitPanel(_hintLabel, _hintBadge, hintSlotW, hintSlotH, HintMaxWidth)
                : new Size(hintSlotW, hintSlotH);
            var hintW = hintFit.Width;
            var hintH = hintFit.Height;

            var showStatus = _statusIcon.Visibility == Visibility.Visible;
            var showRing = _ringBg.Visibility == Visibility.Visible ||
                _hookRing.Visibility == Visibility.Visible ||
                _hookRingImage.Visibility == Visibility.Visible;
            double petX = 0;
            double petY = 0;
            double ringX = (petW - ringW) * 0.5;
            double ringY = -ringH - 2;
            double ringBgX = (petW - ringBgW) * 0.5;
            double ringBgY = -ringBgH - 2;
            double statusX = (petW - statusW) * 0.5;
            double statusY = showStatus ? -statusH - 2 : 0;
            double nameSlotX = -19;
            double nameSlotY = 76;
            double bubbleSlotX = (petW - bubbleSlotW) * 0.5;
            double bubbleSlotY = -bubbleSlotH - 2;
            double hintSlotX = (petW - hintSlotW) * 0.5;
            double hintSlotY = -hintSlotH - 2;

            double hitX = petX;
            double hitY = petY;
            double hitW = petW;
            double hitH = petH;
            if (_chrome != null && _chrome.Hit != null && _chrome.Hit.w > 0 && _chrome.Hit.h > 0)
            {
                hitX = petX + _chrome.Hit.x;
                hitY = petY + _chrome.Hit.y;
                hitW = _chrome.Hit.w;
                hitH = _chrome.Hit.h;
            }

            if (_chrome != null && _chrome.Pet != null)
            {
                if (ringSlot != null)
                {
                    ringX = ringSlot.x - _chrome.Pet.x;
                    ringY = ringSlot.y - _chrome.Pet.y;
                }

                if (ringBgSlot != null)
                {
                    ringBgX = ringBgSlot.x - _chrome.Pet.x;
                    ringBgY = ringBgSlot.y - _chrome.Pet.y;
                }

                if (_chrome.Status != null)
                {
                    statusX = _chrome.Status.x - _chrome.Pet.x;
                    statusY = _chrome.Status.y - _chrome.Pet.y;
                }

                if (_chrome.Name != null)
                {
                    nameSlotX = _chrome.Name.x - _chrome.Pet.x;
                    nameSlotY = _chrome.Name.y - _chrome.Pet.y;
                }

                if (_chrome.Bubble != null)
                {
                    bubbleSlotX = _chrome.Bubble.x - _chrome.Pet.x;
                    bubbleSlotY = _chrome.Bubble.y - _chrome.Pet.y;
                }

                if (_chrome.Hint != null)
                {
                    hintSlotX = _chrome.Hint.x - _chrome.Pet.x;
                    hintSlotY = _chrome.Hint.y - _chrome.Pet.y;
                }
            }

            // Grow from the prefab slot: name expands down, bubbles expand up, both stay centered.
            var nameX = nameSlotX + (nameSlotW - nameW) * 0.5;
            var nameY = nameSlotY;
            var bubbleX = bubbleSlotX + (bubbleSlotW - bubbleW) * 0.5;
            var bubbleY = bubbleSlotY + bubbleSlotH - bubbleH;
            var hintX = hintSlotX + (hintSlotW - hintW) * 0.5;
            var hintY = hintSlotY + hintSlotH - hintH;

            var minX = Math.Min(petX, Math.Min(nameX, Math.Min(bubbleX, Math.Min(hintX, hintSlotX))));
            var minY = Math.Min(petY, Math.Min(nameY, Math.Min(bubbleY, Math.Min(hintY, hintSlotY))));
            if (showRing)
            {
                minX = Math.Min(minX, Math.Min(ringX, ringBgX));
                minY = Math.Min(minY, Math.Min(ringY, ringBgY));
            }
            if (showStatus)
            {
                minX = Math.Min(minX, statusX);
                minY = Math.Min(minY, statusY);
            }
            petX -= minX;
            petY -= minY;
            ringX -= minX;
            ringY -= minY;
            ringBgX -= minX;
            ringBgY -= minY;
            statusX -= minX;
            statusY -= minY;
            nameX -= minX;
            nameY -= minY;
            bubbleX -= minX;
            bubbleY -= minY;
            hintX -= minX;
            hintY -= minY;
            hintSlotX -= minX;
            hintSlotY -= minY;
            hitX -= minX;
            hitY -= minY;

            var width = Math.Max(petX + petW, Math.Max(nameX + nameW, Math.Max(bubbleX + bubbleW, Math.Max(hintX + hintW, hintSlotX + hintSlotW))));
            var height = Math.Max(petY + petH, Math.Max(nameY + nameH, Math.Max(bubbleY + bubbleH, Math.Max(hintY + hintH, hintSlotY + hintSlotH))));
            if (showRing)
            {
                width = Math.Max(width, Math.Max(ringX + ringW, ringBgX + ringBgW));
                height = Math.Max(height, Math.Max(ringY + ringH, ringBgY + ringBgH));
            }
            if (showStatus)
            {
                width = Math.Max(width, statusX + statusW);
                height = Math.Max(height, statusY + statusH);
            }
            _content.Width = width;
            _content.Height = height;
            Width = width;
            Height = height;

            Canvas.SetLeft(_ringBg, ringBgX);
            Canvas.SetTop(_ringBg, ringBgY);
            Canvas.SetLeft(_hookRingImage, ringX);
            Canvas.SetTop(_hookRingImage, ringY);
            Canvas.SetLeft(_hookRing, ringX);
            Canvas.SetTop(_hookRing, ringY);
            Canvas.SetLeft(_statusIcon, statusX);
            Canvas.SetTop(_statusIcon, statusY);
            Canvas.SetLeft(_nameBadge, nameX);
            Canvas.SetTop(_nameBadge, nameY);
            Canvas.SetLeft(_speechBadge, bubbleX);
            Canvas.SetTop(_speechBadge, bubbleY);
            _hintAnchor.Width = hintSlotW;
            _hintAnchor.Height = hintSlotH;
            Canvas.SetLeft(_hintAnchor, hintSlotX);
            Canvas.SetTop(_hintAnchor, hintSlotY);
            Canvas.SetLeft(_hintBadge, hintX);
            Canvas.SetTop(_hintBadge, hintY);
            Canvas.SetLeft(_stage, petX);
            Canvas.SetTop(_stage, petY);
            _hitCatcher.Width = hitW;
            _hitCatcher.Height = hitH;
            Canvas.SetLeft(_hitCatcher, hitX);
            Canvas.SetTop(_hitCatcher, hitY);
            if (showRing)
                UpdateHookRing();
        }

        void UpdateStatusVisuals()
        {
            var iconKind = ResolveStatusIconKind(_fishingPhase);
            if (string.IsNullOrEmpty(iconKind))
            {
                _statusIcon.Source = null;
                _statusIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                _statusIcon.Source = OverlayStatusIcons.Get(iconKind);
                _statusIcon.Visibility = Visibility.Visible;
            }

            if (IsRingPhase(_fishingPhase))
                UpdateHookRing();
            else
                HideHookRing();
        }

        void HideHookRing()
        {
            _ringBg.Visibility = Visibility.Collapsed;
            _hookRing.Visibility = Visibility.Collapsed;
            _hookRingImage.Visibility = Visibility.Collapsed;
            _hookRingImage.OpacityMask = null;
        }

        void ShowRingBackground()
        {
            var bg = OverlayStatusIcons.TryGetRingBg();
            if (bg == null)
            {
                _ringBg.Source = null;
                _ringBg.Visibility = Visibility.Collapsed;
                return;
            }

            _ringBg.Source = bg;
            _ringBg.Visibility = Visibility.Visible;
        }

        void UpdateHookRing()
        {
            if (_hookDeadlineMs <= 0)
            {
                HideHookRing();
                return;
            }

            var remaining = Math.Max(0, _hookDeadlineMs - NowMs());
            if (remaining <= 0)
            {
                HideHookRing();
                return;
            }

            if (_hookTotalMs <= 0)
                _hookTotalMs = remaining;

            var fillAmount = Math.Max(0, Math.Min(1, (double)remaining / _hookTotalMs));
            ShowRingBackground();
            var ringArt = OverlayStatusIcons.TryGetRing();
            if (ringArt != null)
            {
                _hookRing.Visibility = Visibility.Collapsed;
                _hookRingImage.Source = ringArt;
                _hookRingImage.OpacityMask = CreateRadialFillMask(
                    Math.Max(_hookRingImage.Width, 1),
                    Math.Max(_hookRingImage.Height, 1),
                    fillAmount);
                _hookRingImage.Visibility = Visibility.Visible;
                return;
            }

            _hookRingImage.Visibility = Visibility.Collapsed;
            var ringSize = _hookRing.Width > 1 ? _hookRing.Width : RingSize;
            _hookRing.Data = CreateRadial360(ringSize, fillAmount);
            _hookRing.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Opacity mask matching Unity Image Filled Radial 360, Origin=Top, Clockwise.
        /// fillAmount 1 = full ring PNG, 0 = empty.
        /// </summary>
        static Brush CreateRadialFillMask(double width, double height, double fillAmount)
        {
            var size = Math.Max(1.0, Math.Min(width, height));
            var pie = CreateRadialPie(size, fillAmount);
            var drawing = new GeometryDrawing(Brushes.White, null, pie);
            drawing.Freeze();
            var brush = new DrawingBrush(drawing)
            {
                Stretch = Stretch.Fill,
                Viewbox = new Rect(0, 0, size, size),
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, 1, 1),
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
            };
            brush.Freeze();
            return brush;
        }

        static Geometry CreateRadialPie(double size, double fillAmount)
        {
            var radius = size * 0.5;
            var center = new Point(radius, radius);
            if (fillAmount >= 0.999)
            {
                var full = new EllipseGeometry(center, radius, radius);
                full.Freeze();
                return full;
            }

            if (fillAmount <= 0)
                return Geometry.Empty;

            var sweep = 360.0 * fillAmount;
            var start = Radial360Point(center, radius, 0);
            var end = Radial360Point(center, radius, sweep);
            var figure = new PathFigure
            {
                StartPoint = center,
                IsClosed = true,
                IsFilled = true,
            };
            figure.Segments.Add(new LineSegment(start, true));
            figure.Segments.Add(new ArcSegment
            {
                Point = end,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = sweep > 180,
                IsStroked = true,
            });
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            geometry.Freeze();
            return geometry;
        }

        /// <summary>
        /// Unity Image Type=Filled, Method=Radial 360, Origin=Top, Clockwise.
        /// fillAmount 1 = full ring, 0 = empty.
        /// </summary>
        static Geometry CreateRadial360(double size, double fillAmount)
        {
            var radius = Math.Max(1.0, (size - RingStroke) * 0.5);
            var center = new Point(size * 0.5, size * 0.5);
            if (fillAmount >= 0.999)
                return new EllipseGeometry(center, radius, radius);

            if (fillAmount <= 0)
                return Geometry.Empty;

            var sweep = 360.0 * fillAmount;
            var start = Radial360Point(center, radius, 0);
            var end = Radial360Point(center, radius, sweep);
            var figure = new PathFigure
            {
                StartPoint = start,
                IsClosed = false,
                IsFilled = false,
            };
            figure.Segments.Add(new ArcSegment
            {
                Point = end,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = sweep > 180,
                IsStroked = true,
            });
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            geometry.Freeze();
            return geometry;
        }

        static Point Radial360Point(Point center, double radius, double degreesClockwiseFromTop)
        {
            var radians = degreesClockwiseFromTop * Math.PI / 180.0;
            return new Point(
                center.X + radius * Math.Sin(radians),
                center.Y - radius * Math.Cos(radians));
        }

        void OnRefreshTick(object sender, EventArgs e)
        {
            if (_hookRing.Visibility == Visibility.Visible ||
                _hookRingImage.Visibility == Visibility.Visible)
                UpdateHookRing();
            if (_hoverShown)
                RefreshOpenHover();
        }

        void OnFrameTick(object sender, EventArgs e)
        {
            if (_frames.Length <= 1)
                return;
            if (OverlayFrameCache.IsOneShotClip(_visualState))
            {
                if (_frameIndex >= _frames.Length - 1)
                {
                    _frameTimer.Stop();
                    return;
                }

                _frameIndex++;
                ShowFrame();
                return;
            }

            _frameIndex = (_frameIndex + 1) % _frames.Length;
            ShowFrame();
        }

        public void SetPointerOverPet(bool inside)
        {
            if (inside)
            {
                if (OverlayInteractionState.SceneDragging ||
                    OverlayInteractionState.ContextMenuOpen)
                    return;
                if (_pointerInside)
                    return;
                _pointerInside = true;
                _tooltipTimer.Stop();
                _tooltipTimer.Start();
                return;
            }

            if (!_pointerInside)
                return;
            _pointerInside = false;
            _tooltipTimer.Stop();
            CloseHover();
        }

        void OnTooltipTimerTick(object sender, EventArgs e)
        {
            _tooltipTimer.Stop();
            if (!_pointerInside ||
                OverlayInteractionState.SceneDragging ||
                OverlayInteractionState.ContextMenuOpen)
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

            _hoverHost?.ShowHoverCard(ActorKey, text, this, _hintAnchor);
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

        string BuildHoverText()
        {
            var catchLine = _sessionCatchCount > 0
                ? "钓到" + _sessionCatchCount + "条!"
                : "空军";
            var durationLine = BuildHoverDurationLine();
            if (string.IsNullOrEmpty(durationLine))
                return catchLine;
            return durationLine + "\n" + catchLine;
        }

        string BuildHoverDurationLine()
        {
            if (IsRingPhase(_fishingPhase) && _hookDeadlineMs > 0)
            {
                var remaining = Math.Max(0, _hookDeadlineMs - NowMs());
                if (remaining > 0)
                    return (IsGroundbaitPhase(_fishingPhase) ? "打窝 " : "收杆 ") + FormatDuration(remaining);
            }

            if (IsFishingPhase(_fishingPhase) ||
                IsHookedPhase(_fishingPhase) ||
                IsGroundbaitPhase(_fishingPhase))
                return "本局 " + FormatDuration(CurrentSessionFishingMs());

            if (_sessionAnchorMs > 0)
                return "本局 " + FormatDuration(CurrentSessionFishingMs());

            return string.Empty;
        }

        static string ResolveStatusIconKind(string phase)
        {
            if (IsHookedPhase(phase))
                return "hooked";
            if (IsGroundbaitPhase(phase))
                return "groundbait";
            return null;
        }

            void SyncSessionAnchor(long sessionFishingMs, long fishingStartedAt)
        {
            if (!IsFishingPhase(_fishingPhase) &&
                !IsHookedPhase(_fishingPhase) &&
                !IsGroundbaitPhase(_fishingPhase))
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

        static bool IsGroundbaitPhase(string phase)
        {
            return phase == "groundbaiting";
        }

        static bool IsRingPhase(string phase)
        {
            return IsHookedPhase(phase) || IsGroundbaitPhase(phase);
        }

        static string InferPhaseFromVisual(string visualState)
        {
            switch (visualState)
            {
                case "sit": return "seated";
                case "cast": return "casting";
                case "fishing": return "waiting";
                case "hooked": return "hooked";
                case "reel": return "resolving";
                case "catch":
                case "catching": return "seated";
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
                case "sit": return Color.FromRgb(120, 168, 120);
                case "cast": return Color.FromRgb(110, 150, 210);
                case "fishing": return Color.FromRgb(90, 168, 214);
                case "hooked": return Color.FromRgb(232, 156, 64);
                case "reel":
                case "catch":
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

        public Border SpeechHost => _speechBadge;

        public Border HintHost => _hintBadge;

        public void ShowSpeech(string text, bool autoHide)
        {
            ApplyBubbleText(_speechLabel, text, 80);
            _speechBadge.Visibility = Visibility.Visible;
            RelayoutAndPlace();
            RunPopIn(_speechBadge);
            StopSpeechFade();
            if (!autoHide)
                return;
            _speechFadeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5),
            };
            _speechFadeTimer.Tick += OnSpeechFadeTick;
            _speechFadeTimer.Start();
        }

        public void ShowHint(string text)
        {
            ApplyBubbleText(_hintLabel, text, 120);
            _hintBadge.Visibility = Visibility.Visible;
            RelayoutAndPlace();
            RunPopIn(_hintBadge);
        }

        public void ClearSpeech()
        {
            StopSpeechFade();
            _speechBadge.BeginAnimation(UIElement.OpacityProperty, null);
            _speechBadge.Opacity = 1;
            _speechBadge.Visibility = Visibility.Collapsed;
            _speechLabel.Text = string.Empty;
            RelayoutAndPlace();
        }

        public void ClearHint()
        {
            _hintBadge.BeginAnimation(UIElement.OpacityProperty, null);
            _hintBadge.Opacity = 1;
            _hintBadge.Visibility = Visibility.Collapsed;
            _hintLabel.Text = string.Empty;
            RelayoutAndPlace();
        }

        public void ClearBubbles()
        {
            ClearSpeech();
            ClearHint();
        }

        void OnSpeechFadeTick(object sender, EventArgs e)
        {
            StopSpeechFade();
            var fade = new DoubleAnimation(_speechBadge.Opacity, 0, TimeSpan.FromSeconds(0.3));
            fade.Completed += (_, __) => ClearSpeech();
            _speechBadge.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        void StopSpeechFade()
        {
            if (_speechFadeTimer == null)
                return;
            _speechFadeTimer.Stop();
            _speechFadeTimer.Tick -= OnSpeechFadeTick;
            _speechFadeTimer = null;
        }

        void RelayoutAndPlace()
        {
            RelayoutCluster();
            Place(_centerX, _centerY);
        }

        static double SlotWidth(OverlayLayoutObjectDto part, double fallback)
        {
            return part != null && part.w > 0 ? part.w : fallback;
        }

        static double SlotHeight(OverlayLayoutObjectDto part)
        {
            if (part != null && part.h > 0 && part.h <= 24)
                return part.h;
            return PanelMinHeight;
        }

        static Size FitPanel(
            TextBlock label,
            Border badge,
            double minWidth,
            double minHeight,
            double maxWidth)
        {
            if (label == null || badge == null)
                return new Size(minWidth, minHeight);

            var padX = badge.Padding.Left + badge.Padding.Right +
                       badge.BorderThickness.Left + badge.BorderThickness.Right;
            var padY = badge.Padding.Top + badge.Padding.Bottom +
                       badge.BorderThickness.Top + badge.BorderThickness.Bottom;
            var innerMax = Math.Max(24, maxWidth - padX);
            label.TextWrapping = TextWrapping.Wrap;
            label.MaxWidth = innerMax;
            label.Measure(new Size(innerMax, double.PositiveInfinity));
            var text = label.DesiredSize;
            if (string.IsNullOrWhiteSpace(label.Text))
                text = new Size(0, 0);

            var width = Math.Min(maxWidth, Math.Max(minWidth, Math.Ceiling(text.Width + padX)));
            var height = Math.Max(minHeight, Math.Ceiling(text.Height + padY));
            badge.Width = width;
            badge.Height = height;
            return new Size(width, height);
        }

        static void ApplyBubbleText(TextBlock label, string text, int maxChars)
        {
            var value = string.IsNullOrWhiteSpace(text) ? "…" : text.Trim();
            if (value.Length > maxChars)
                value = value.Substring(0, maxChars - 3) + "…";
            label.Text = value;
        }

        static TextBlock CreateBubbleLabel(double fontSize)
        {
            return new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = fontSize,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
        }

        static Border CreateSpeechBadge(TextBlock label)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromArgb(230, 15, 24, 32)),
                Child = label,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 1.0),
                RenderTransform = new ScaleTransform(1, 1),
            };
        }

        static Border CreateHintBadge(TextBlock label)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromArgb(235, 27, 58, 74)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 120, 190, 210)),
                BorderThickness = new Thickness(1),
                Child = label,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 1.0),
                RenderTransform = new ScaleTransform(1, 1),
            };
        }

        static void RunPopIn(Border bubble)
        {
            if (bubble == null)
                return;
            bubble.BeginAnimation(UIElement.OpacityProperty, null);
            if (!(bubble.RenderTransform is ScaleTransform))
                bubble.RenderTransform = new ScaleTransform(0.5, 0.5);

            var transform = (ScaleTransform)bubble.RenderTransform;
            transform.ScaleX = 0.5;
            transform.ScaleY = 0.5;
            bubble.Opacity = 0;

            var storyboard = new Storyboard { FillBehavior = FillBehavior.Stop };
            storyboard.Children.Add(CreateScaleAnimation(bubble, true));
            storyboard.Children.Add(CreateScaleAnimation(bubble, false));
            var fade = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(fade, bubble);
            Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));
            storyboard.Children.Add(fade);
            storyboard.Completed += (_, __) =>
            {
                bubble.Opacity = 1;
                transform.ScaleX = 1;
                transform.ScaleY = 1;
            };
            storyboard.Begin();
        }

        static DoubleAnimationUsingKeyFrames CreateScaleAnimation(Border bubble, bool scaleX)
        {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(
                1.1,
                KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.15))));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(
                1.0,
                KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.3))));
            var property = scaleX
                ? "(UIElement.RenderTransform).(ScaleTransform.ScaleX)"
                : "(UIElement.RenderTransform).(ScaleTransform.ScaleY)";
            Storyboard.SetTarget(animation, bubble);
            Storyboard.SetTargetProperty(animation, new PropertyPath(property));
            return animation;
        }

        static Border CreateTextBadge(TextBlock textBlock, Thickness margin)
        {
            textBlock.VerticalAlignment = VerticalAlignment.Center;
            textBlock.HorizontalAlignment = HorizontalAlignment.Center;
            return new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 0, 6, 0),
                Margin = margin,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(0xD9, 0x0F, 0x18, 0x20)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xB8, 0xE1, 0xEF)),
                BorderThickness = new Thickness(1),
                Child = textBlock,
            };
        }
    }

    static class OverlayFrameCache
    {
        static readonly Dictionary<string, ImageSource[]> Cache =
            new Dictionary<string, ImageSource[]>(StringComparer.OrdinalIgnoreCase);

        public static ImageSource[] Get(string petId, string visualState)
        {
            var state = NormalizeClip(visualState);
            var id = petId ?? string.Empty;
            var key = id + "|" + state;
            if (Cache.TryGetValue(key, out var frames))
                return frames;

            var loaded = Load(id, state);
            Cache[key] = loaded;
            return loaded;
        }

        public static bool IsOneShotClip(string visualState)
        {
            var clip = NormalizeClip(visualState);
            return clip == "cast" || clip == "reel" || clip == "catch";
        }

        static ImageSource[] Load(string petId, string visualState)
        {
            var root = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OverlayResources");
            var list = new List<ImageSource>();
            var clip = NormalizeClip(visualState);
            if (!string.IsNullOrWhiteSpace(petId) && IsSafePetId(petId))
            {
                var petDir = System.IO.Path.Combine(root, "pets", petId);
                TryLoadClip(list, petDir, clip);
                if (list.Count == 0 && clip == "sit")
                    TryLoadClip(list, petDir, "fishing");
                if (list.Count == 0 && clip == "catch")
                    TryLoadClip(list, petDir, "reel");
                if (list.Count == 0 && clip != "idle")
                    TryLoadClip(list, petDir, "idle");
                if (list.Count == 0 && clip != "fishing")
                    TryLoadClip(list, petDir, "fishing");
                if (list.Count == 0)
                    TryAdd(list, System.IO.Path.Combine(petDir, "cat.png"));
            }

            if (list.Count == 0)
            {
                AppendSequence(list, root, "cat-" + clip);
                if (list.Count == 0)
                    TryAdd(list, System.IO.Path.Combine(root, "cat.png"));
            }

            return list.ToArray();
        }

        public static string NormalizeClip(string visualState)
        {
            if (string.IsNullOrWhiteSpace(visualState))
                return "idle";
            if (string.Equals(visualState, "catching", StringComparison.OrdinalIgnoreCase))
                return "catch";
            if (string.Equals(visualState, "offline", StringComparison.OrdinalIgnoreCase))
                return "idle";
            if (string.Equals(visualState, "dragging", StringComparison.OrdinalIgnoreCase))
                return "idle";
            return visualState.Trim().ToLowerInvariant();
        }

        static void TryLoadClip(List<ImageSource> list, string petDir, string clip)
        {
            AppendClipDirectory(list, petDir, clip);
            if (list.Count == 0)
                AppendSequence(list, petDir, clip);
        }

        static void AppendClipDirectory(List<ImageSource> list, string petDir, string clip)
        {
            var clipDir = System.IO.Path.Combine(petDir, clip);
            for (var i = 0; i < 16; i++)
            {
                var path = System.IO.Path.Combine(clipDir, i + ".png");
                if (!File.Exists(path))
                    break;
                TryAdd(list, path);
            }
        }

        static bool IsSafePetId(string petId)
        {
            for (var i = 0; i < petId.Length; i++)
            {
                var c = petId[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_'))
                    return false;
            }
            return petId.Length > 0;
        }

        static void AppendSequence(List<ImageSource> list, string dir, string stem)
        {
            for (var i = 0; i < 16; i++)
            {
                var path = System.IO.Path.Combine(dir, stem + "-" + i + ".png");
                if (!File.Exists(path))
                {
                    if (i == 0)
                        TryAdd(list, System.IO.Path.Combine(dir, stem + ".png"));
                    break;
                }
                TryAdd(list, path);
            }
        }

        static void TryAdd(List<ImageSource> list, string path)
        {
            if (!File.Exists(path))
                return;
            list.Add(LoadBitmap(path));
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
