using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
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
    public sealed class OverlayPetActor : Grid, IDisposable
    {
        public const double BodySize = 64;
        const double RingSize = 31;
        const double RingStroke = OverlayHookProgressStyle.StrokeWidth;
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
        readonly Grid _layerHost;
        readonly Image[] _layerImages;
        readonly OverlayWaterEffects _waterEffects;
        readonly Canvas _waterBack = new Canvas { IsHitTestVisible = false, ClipToBounds = false };
        readonly Canvas _waterFront = new Canvas { IsHitTestVisible = false, ClipToBounds = false };
        long _waterLoopOccurrence, _waterWaitingOccurrence;
        double _waterNextWaitAt;
        Rect _waterSceneClip = Rect.Empty;
        bool _waterSelf;
        Point? _waterContactLocal;
        public event Action WaterGeometryChanged;
        public Point? WaterAnchorScene => _waterContactLocal.HasValue ? WaterBodySceneOrigin + (Vector)_waterContactLocal.Value : (Point?)null;
        public Rect WaterPetBounds => new Rect(WaterBodySceneOrigin, new Size(_body.Width, _body.Height));
        readonly HashSet<string> _waterDiagnostics = new HashSet<string>();
        public FishingPresentationEnvelope WaterPresentation => _waterEffects.Policy.Envelope;
        public event Action<OverlayPetActor, string, long, string, string> WaterMarkerPublished;
        internal int WaterDiagnosticCount => _waterDiagnostics.Count;
        public Point WaterBodySceneOrigin => new Point(Canvas.GetLeft(this) + _bodyOriginX - _body.Width / 2,
            Canvas.GetTop(this) + _bodyOriginY - _body.Height / 2);
        public void SetFishingPresentation(FishingPresentationEnvelope envelope, bool self, string quality)
        {
            _waterSelf = self;
            var token = _waterEffects.Policy.Version;
            _waterEffects.Configure(envelope, self, quality);
            if (token != _waterEffects.Policy.Version && _layered != null)
            {
                _renderVersion++; _frameRequest?.Cancel(); _frameDirty = true;
                if (_presentationVisible) OverlayAnimationClock.Wake();
            }
        }
        public void SetWaterSceneClip(Rect sceneClip, bool eligible)
        {
            _waterSceneClip = sceneClip;
            _waterEffects.SetEligible(eligible);
        }
        void WaterDiagnostic(string reason)
        {
            if (_waterDiagnostics.Add(reason)) LatencyTrace.Write("waterFx actor=" + ActorKey + " skip=" + reason);
        }
        readonly Rectangle _hitCatcher;
        readonly TextBlock _sleepLabel;
        readonly OverlayFishingRestMotion _restMotion = new OverlayFishingRestMotion();
        bool _fishingAsleep;
        readonly Image _statusIcon;
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
        readonly OverlayAnimationSchedule.Subscription _frameTimer;
        readonly OverlayAnimationSchedule.Subscription _refreshTimer;
        readonly DispatcherTimer _tooltipTimer;
        bool _disposed;
        bool _presentationVisible = true;
        readonly OverlayAudioMarkerPolicy _audioMarkers = new OverlayAudioMarkerPolicy();
        string _audioClipActionId, _audioPublishedClip;
        double _audioPublishedPosition = -1;
        long _audioOccurrence;
        public event Action<OverlayAudioMarker> AudioMarkerPublished;

        public void ConfigureAudioPresentation(OverlayAudioPresentation identity, string phase, string skin)
        {
            var before = _audioMarkers.Revision;
            _audioMarkers.Update(identity, phase, skin);
            // A result can be confirmed after the held catch frame was rendered.
            // Republish the same complete composite; never restart the animation.
            if (before != _audioMarkers.Revision && _layered != null) ShowLayered();
        }

        public void SetAudioPresentationVisible(bool visible) => _audioMarkers.SetVisible(visible);
        public void CancelAudioPresentation() => _audioMarkers.Cancel();
        bool _frameDirty;
        bool _frameLoading;
        CancellationTokenSource _frameRequest;
        int _renderVersion;
        int _stateVersion;
        int _warmupVersion;
        string _requestedPet, _requestedVisual, _warmingPet;
        double _bodyOriginX, _bodyOriginY;
        internal int LayoutPassCount { get; private set; }
        internal static long TotalLayoutPasses { get; private set; }
        internal bool WarmupPending { get; private set; }
        internal bool FramePending => _frameDirty || _frameLoading;
        public Rect PresentationBounds => new Rect(Canvas.GetLeft(this), Canvas.GetTop(this), Width, Height);

        bool _pointerInside;
        bool _hoverShown;
        double _centerX;
        double _centerY;
        readonly OverlaySeatMotion _seatMotion = new OverlaySeatMotion();
        string _sceneSpot;
        OverlayActorChrome _destinationChrome;
        string _arrivalVisual, _arrivalPet;
        ImageSource[] _frames = Array.Empty<ImageSource>();
        int _frameIndex;
        FishingClipPlayback _layered;
        OverlayGroundbaitTimeline _groundbaitTimeline;
        double _groundbaitStartedAt;
        string _layeredClip = string.Empty;
        int _layerDrawW = 1;
        int _layerDrawH = 1;
        string _deferredState = string.Empty;
        string _deferredPet = string.Empty;
        string _visualState = string.Empty;
        string _latestState = string.Empty;
        bool _catchHolding;
        bool _successCompleted;
        string _petId = string.Empty;
        string _warmedPetId = string.Empty;
        int _warmedDrawW;
        int _warmedDrawH;
        string _fishingPhase = string.Empty;
        long _sessionAnchorMs;
        long _hookDeadlineMs;
        long _hookTotalMs;
        int _sessionCatchCount;
        static readonly string[] ActionWarmupClips =
        {
            "bait", "cast", "fishing", "hooked", "reel", "reel_empty", "sit", "idle", "catch",
            "groundbait", "leave",
        };

        public string ActorKey { get; }
        internal static int ActiveActorCount { get; private set; }

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
            IsVisibleChanged += (_, __) => { if (IsVisible && _layered != null && !_disposed) ShowLayered(); };
            ToolTipService.SetIsEnabled(this, false);
            ContextMenuService.SetIsEnabled(this, false);

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
                Foreground = CozyTheme.Ink,
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
                Stroke = OverlayHookProgressStyle.Progress,
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
            _layerHost = new Grid
            {
                IsHitTestVisible = false,
                ClipToBounds = false,
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            _layerImages = new Image[OverlayFishingLayers.LayerNames.Length];
            for (var i = 0; i < _layerImages.Length; i++)
            {
                var layer = new Image
                {
                    Stretch = Stretch.Fill,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    IsHitTestVisible = false,
                };
                _layerImages[i] = layer;
                _layerHost.Children.Add(layer);
            }

            _body.Children.Add(_waterBack);
            _body.Children.Add(_image);
            _body.Children.Add(_layerHost);
            _body.Children.Add(_waterFront);
            _waterEffects = new OverlayWaterEffects(_waterBack, _waterFront);
            _waterEffects.MarkerPublished += (cue, occurrence, key) => WaterMarkerPublished?.Invoke(this, cue, occurrence, key,
                OverlayWaterEffectsPolicy.Tier(_waterEffects.Policy.Envelope?.WaterSizeTier));
            _sleepLabel = new TextBlock
            {
                Text = "Z z z",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = CozyTheme.Ink,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            _body.Children.Add(_sleepLabel);

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

            _frameTimer = OverlayAnimationClock.Create(125);
            _frameTimer.Tick += OnFrameTick;

            _refreshTimer = OverlayAnimationClock.Create(100);
            _refreshTimer.Tick += OnRefreshTick;
            OverlayAnimationClock.AfterTick += RenderLatestFrame;
            ActiveActorCount++;
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
                    ReferenceEquals(source, _image))
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

        public bool IsPlayerContextMenuUnderMouse =>
            IsPlayerContextMenuOpen && _socialMenu.IsMouseOver;

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
            if (_socialMenu == null)
                return;
            _socialMenu.IsOpen = false;
        }

        void ShowPlayerContextMenu()
        {
            DiscardSocialMenu();
            EnsureSocialMenu();

            // Keep menu unattached from FrameworkElement.ContextMenu so WPF
            // ContextMenuService cannot sticky-steal later gestures.
            ContextMenu = null;
            OverlayInteractionState.BeginMenuHoverSuppress();
            CancelTooltip();
            _socialMenu.PlacementTarget = this;
            _socialMenu.Placement = PlacementMode.MousePoint;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (!_disposed && _socialMenu != null && HasPlayerContextMenu)
                    _socialMenu.IsOpen = true;
                else
                    OverlayInteractionState.EndMenuHoverSuppress();
            }));
        }

        void DiscardSocialMenu()
        {
            if (_socialMenu == null)
                return;
            _socialMenu.Opened -= OnSocialMenuOpened;
            _socialMenu.Closed -= OnSocialMenuClosed;
            _socialMenu.IsOpen = false;
            _socialMenu = null;
        }

        void EnsureSocialMenu()
        {
            if (_socialMenu != null)
            {
                RefreshSocialMenuItems();
                return;
            }

            _socialMenu = new ContextMenu
            {
                StaysOpen = true,
            };
            ContextMenuService.SetIsEnabled(_socialMenu, true);
            _socialMenu.Opened += OnSocialMenuOpened;
            _socialMenu.Closed += OnSocialMenuClosed;
            RefreshSocialMenuItems();
        }

        void OnSocialMenuOpened(object sender, RoutedEventArgs e)
        {
            _menuHost?.PlayPlayerMenuAudio("ui_open");
            OverlayInteractionState.BeginMenuHoverSuppress();
            CancelTooltip();
        }

        void OnSocialMenuClosed(object sender, RoutedEventArgs e)
        {
            OverlayInteractionState.EndMenuHoverSuppress();
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

        MenuItem CreateMenuItem(string header, bool enabled, Action action)
        {
            var item = new MenuItem { Header = header, IsEnabled = enabled };
            if (header == "查看资料" || header == "私聊") OverlayUiAudio.SetCue(item, "none");
            CozyTheme.SetIcon(item, CozyTheme.IconPath(CozyTheme.IconFor(header)));
            if (enabled)
            {
                item.Click += (_, __) =>
                {
                    ClosePlayerContextMenu();
                    action();
                };
            }
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
            bool groundbaitActive = false,
            bool fishingAsleep = false)
        {
            if (_disposed) return;
            var nextName = string.IsNullOrWhiteSpace(nickname) ? "玩家" : nickname;
            var layoutChanged = nextName != _nickname.Text;
            if (layoutChanged) _nickname.Text = nextName;
            var oldPhase = _fishingPhase;
            var sleepChanged = _fishingAsleep != fishingAsleep;
            _fishingAsleep = fishingAsleep;
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
            _waterEffects.Phase(_fishingPhase);
            if (IsGroundbaitPhase(_fishingPhase) && !IsGroundbaitPhase(oldPhase))
            {
                _groundbaitStartedAt = OverlayAnimationClock.Now;
                _latestState = string.Empty;
            }
            if (!IsGroundbaitPhase(_fishingPhase) && _groundbaitTimeline != null && !_groundbaitTimeline.Ending)
            {
                _groundbaitTimeline.End(OverlayAnimationClock.Now);
                RefreshGroundbaitFrame();
            }
            SyncSessionAnchor(Math.Max(0, sessionFishingMs), fishingStartedAt);
            if (oldPhase != _fishingPhase || sleepChanged)
            {
                UpdateStatusVisuals();
                layoutChanged = true;
            }
            var sleepingFishing = _fishingAsleep && IsSleepFishingPhase(phase);
            ApplyVisualState(GroundbaitVisualState(sleepingFishing ? "fishing" : visualState), petId);
            if (sleepChanged && _layered != null) ShowLayered();
            if (layoutChanged) RelayoutCluster();
            SyncRefreshClock();
            RefreshOpenHover();
        }

        public void ApplyChrome(OverlayActorChrome chrome)
        {
            if (_disposed) return;
            if (SameChrome(_chrome, chrome)) return;
            _chrome = chrome;
            _warmupVersion++;
            RelayoutCluster();
        }

        static bool SameSlot(OverlayLayoutObjectDto a, OverlayLayoutObjectDto b)
        {
            if (a == null || b == null) return a == b;
            return a.x == b.x && a.y == b.y && a.w == b.w && a.h == b.h;
        }
        static bool SameChrome(OverlayActorChrome a, OverlayActorChrome b)
        {
            if (a == null || b == null) return a == b;
            return a.WaterlineY == b.WaterlineY && SameSlot(a.Pet,b.Pet) && SameSlot(a.Seat,b.Seat) && SameSlot(a.Hit,b.Hit) && SameSlot(a.Name,b.Name) &&
                SameSlot(a.Status,b.Status) && SameSlot(a.Ring,b.Ring) && SameSlot(a.RingBg,b.RingBg) &&
                SameSlot(a.Bubble,b.Bubble) && SameSlot(a.Hint,b.Hint);
        }

        public void SetPresentationVisible(bool visible)
        {
            if (_disposed || _presentationVisible == visible) return;
            _presentationVisible = visible;
            _waterEffects.SetVisible(visible);
            _frameTimer.Suspended = !visible;
            if (visible) OverlayAnimationClock.Wake();
            if (!visible) { _frameRequest?.Cancel(); CancelTooltip(); }
            else { ShowFrame(); UpdateStatusVisuals(); }
            SyncRefreshClock();
        }

        void SyncRefreshClock()
        {
            if (_refreshTimer == null) return;
            if (!_disposed && _presentationVisible && (IsRingPhase(_fishingPhase) || _hoverShown))
            { _refreshTimer.Start(); OverlayAnimationClock.Wake(); }
            else _refreshTimer.Stop();
        }

        /// <summary>
        /// Prefetch first frames of fishing clips so click-time prediction
        /// does not stall on PNG decode.
        /// </summary>
        public async void WarmupActionClips(string petId)
        {
            if (_disposed) return;
            if (!_presentationVisible) return;
            var id = OverlayFrameCache.NormalizePetId(petId ?? _petId);
            if (WarmupPending)
            {
                if (_warmingPet != id) _warmupVersion++;
                return;
            }
            _warmingPet = id;
            var version = ++_warmupVersion;
            WarmupPending = true;
            try
            {
                if (!await OverlayFishingLayers.EnsurePackAsync(id) || _disposed || version != _warmupVersion) return;
                OverlayFishingLayers.Place(
                    id, _body.Width, _body.Height, out var drawW, out var drawH, out _, out _);
                if (string.Equals(id, _warmedPetId, StringComparison.Ordinal) &&
                    drawW == _warmedDrawW && drawH == _warmedDrawH) return;
                if (drawW < 1 || drawH < 1)
                {
                    drawW = (int)BodySize;
                    drawH = (int)BodySize;
                }

                for (var c = 0; c < ActionWarmupClips.Length; c++)
                {
                    var clip = ActionWarmupClips[c];
                    if (OverlayFishingLayers.TryOpen(id, clip) == null)
                        continue;
                    for (var i = 0; i < OverlayFishingLayers.LayerNames.Length; i++)
                    {
                        var path = OverlayFishingLayers.LayerFile(
                            id, clip, 0, OverlayFishingLayers.LayerNames[i]);
                        // Sequential prefetch keeps only one outstanding job per owner.
                        if (_disposed || version != _warmupVersion || !_presentationVisible) return;
                        var rejected = OverlayFishingLayers.RejectedDecodes;
                        await OverlayFishingLayers.BitmapAsync(path, drawW, drawH);
                        if (OverlayFishingLayers.RejectedDecodes != rejected) return;
                    }
                }
                if (!_disposed && version == _warmupVersion)
                {
                    _warmedPetId = id;
                    _warmedDrawW = drawW;
                    _warmedDrawH = drawH;
                }
            }
            finally { WarmupPending = false; }
        }

        // Seated points are authored pet centres; waiting-lane points are feet.
        public void ApplyScenePlacement(string spotId, OverlayActorChrome chrome, Point point, string petId)
        {
            if (_disposed) return;
            var spot = spotId ?? string.Empty;
            var pet = OverlayFrameCache.NormalizePetId(petId);
            var width = chrome?.Pet?.w > 0 ? chrome.Pet.w : BodySize;
            var height = chrome?.Pet?.h > 0 ? chrome.Pet.h : BodySize;
            var foot = OverlayFishingLayers.FootInSlot(pet, width, height);
            var x = point.X + (spot.Length > 0 ? foot.X - width * .5 : 0);
            var y = point.Y + (spot.Length > 0 ? foot.Y - height * .5 : 0);
            _destinationChrome = chrome;
            if (_sceneSpot == null)
            {
                _seatMotion.Snap(x, y);
                ApplyChrome(chrome);
            }
            else if (_sceneSpot != spot)
            {
                _arrivalVisual = _arrivalPet = null;
                var leaving = _sceneSpot.Length > 0 && spot.Length == 0;
                _seatMotion.Retarget(x, y);
                if (leaving) _seatMotion.WaitForStand(x, y);
                else
                {
                    _seatMotion.Begin(OverlayAnimationClock.Now);
                    ApplyChrome(chrome);
                    // Cancel an old leave/reset chain before accepting the new seat.
                    _latestState = string.Empty;
                }
            }
            else if (_seatMotion.WaitingForStand || _seatMotion.Moving)
                _seatMotion.Retarget(x, y);
            else
            {
                _seatMotion.Snap(x, y);
                ApplyChrome(chrome);
            }
            _sceneSpot = spot;
            PlaceSceneFeet(pet);
            if (_seatMotion.Moving) OverlayAnimationClock.Wake();
        }

        void PlaceSceneFeet(string pet)
        {
            var foot = OverlayFishingLayers.FootInSlot(pet, _body.Width, _body.Height);
            Place(_seatMotion.X + _body.Width * .5 - foot.X,
                _seatMotion.Y + _body.Height * .5 - foot.Y);
        }

        void BeginSceneDeparture()
        {
            if (!_seatMotion.WaitingForStand) return;
            _seatMotion.Begin(OverlayAnimationClock.Now);
            ApplyChrome(_destinationChrome);
            PlaceSceneFeet(_petId);
            OverlayAnimationClock.Wake();
        }

        void AdvanceSceneMotion()
        {
            if (!_seatMotion.Moving) return;
            var arrived = _seatMotion.Advance(OverlayAnimationClock.Now);
            PlaceSceneFeet(_petId);
            if (arrived && _arrivalVisual != null)
            {
                var state = _arrivalVisual;
                var pet = _arrivalPet;
                _arrivalVisual = _arrivalPet = null;
                _latestState = string.Empty;
                if (!string.IsNullOrEmpty(_sceneSpot) && state != "offline" && state != "dragging" && state != "idle")
                    ApplyLoadedVisualState("sit", pet);
                ApplyVisualState(state, pet);
            }
            if (_seatMotion.Moving) OverlayAnimationClock.Wake();
        }

        public void Place(double centerX, double centerY)
        {
            _centerX = centerX;
            _centerY = centerY;
            // RelayoutCluster already computes the stage/body origin; no synchronous
            // WPF measure/arrange is needed for placement, even before the first render.
            var left = centerX - _bodyOriginX;
            var top = centerY - _bodyOriginY;
            var foot = OverlayFishingLayers.FootInSlot(_petId, _body.Width, _body.Height);
            var seated = !string.IsNullOrEmpty(_sceneSpot) && !_seatMotion.Moving && !_seatMotion.WaitingForStand && _chrome?.Seat != null;
            var ground = centerY - _body.Height / 2 + (seated ? _chrome.Seat.y + _chrome.Seat.h : foot.Y);
            var order = OverlaySceneGeometry.ActorOrder(ground, centerX);
            if (Panel.GetZIndex(this) != order) Panel.SetZIndex(this, order);
            if (Canvas.GetLeft(this) == left && Canvas.GetTop(this) == top) return;
            Canvas.SetLeft(this, left);
            Canvas.SetTop(this, top);
            if (_hoverShown)
                TryShowHover();
        }

        /// <summary>Permanent removal only; animation transitions use ReleaseFishingLayers.</summary>
        public void Dispose()
        {
            _audioMarkers.Cancel();
            AudioMarkerPublished = null;
            if (_disposed) return;
            _disposed = true;
            _waterEffects.Dispose();
            _stateVersion++;
            _renderVersion++;
            _frameRequest?.Cancel();
            _warmupVersion++;
            OverlayAnimationClock.AfterTick -= RenderLatestFrame;
            ActiveActorCount--;
            _frameTimer.Stop();
            _frameTimer.Tick -= OnFrameTick;
            _refreshTimer.Stop();
            _refreshTimer.Tick -= OnRefreshTick;
            _tooltipTimer.Stop();
            _tooltipTimer.Tick -= OnTooltipTimerTick;
            StopSpeechFade();
            CancelTooltip();
            var hadOpenMenu = IsPlayerContextMenuOpen;
            DiscardSocialMenu();
            if (hadOpenMenu) OverlayInteractionState.EndMenuHoverSuppress();
            _menuHost = null;
            ClearBubbles();
            ReleaseFishingLayers();
            _frames = Array.Empty<ImageSource>();
            ClearLayerSources();
            _image.Source = null;
            _statusIcon.Source = null;
            _ringBg.Source = null;
            _hookRingImage.Source = null;
            _hookRingImage.OpacityMask = null;
            _hoverHost?.RemoveActor(ActorKey);
            IsHitTestVisible = false;
        }

        public void ReleaseFishingLayers(bool clearDeferred = true)
        {
            _groundbaitTimeline = null;
            _sleepLabel.Visibility = Visibility.Collapsed;
            _restMotion.Update(OverlayAnimationClock.Now, false, false);
            if (clearDeferred)
            {
                _deferredState = string.Empty;
                _deferredPet = string.Empty;
            }
            if (_layered == null)
                return;
            OverlayFishingLayers.Release(_petId, _layeredClip);
            _layered = null;
            _layeredClip = string.Empty;
            _renderVersion++;
            _frameRequest?.Cancel();
            _frameDirty = false;
            // Keep the last complete composite until its replacement is ready.
        }

        async void ApplyVisualState(string visualState, string petId)
        {
            var pet = OverlayFrameCache.NormalizePetId(petId);
            if (_requestedPet == pet && _requestedVisual == visualState &&
                !OverlayFishingLayers.PackIsLoaded(pet)) return;
            _requestedPet = pet;
            _requestedVisual = visualState;
            var version = ++_stateVersion;
            await OverlayFishingLayers.EnsurePackAsync(pet);
            if (!OverlayFishingLayers.PackIsLoaded(pet))
            { _requestedPet = null; return; }
            if (_disposed || version != _stateVersion) return;
            ApplyLoadedVisualState(visualState, petId);
            OverlayAnimationClock.Wake();
        }

        void ApplyLoadedVisualState(string visualState, string petId)
        {
            // A cancelled seat prediction may never have reached the chair.
            if (_seatMotion.WaitingForStand && _layeredClip == "idle") BeginSceneDeparture();
            if (_seatMotion.Moving)
            {
                _arrivalVisual = visualState;
                _arrivalPet = petId;
                // Existing standing pose while travelling; sit only on arrival.
                visualState = "offline";
            }
            var nextState = OverlayFrameCache.NormalizeClip(visualState);
            var interrupt = string.Equals(visualState, "offline", StringComparison.OrdinalIgnoreCase)
                || string.Equals(visualState, "dragging", StringComparison.OrdinalIgnoreCase);
            var nextPet = OverlayFrameCache.NormalizePetId(petId);
            if (string.Equals(_latestState, nextState, StringComparison.Ordinal)
                && string.Equals(_petId, nextPet, StringComparison.Ordinal)
                && (!interrupt || _layeredClip == "idle"))
                return;
            _latestState = nextState;
            var samePet = string.Equals(_petId, nextPet, StringComparison.Ordinal);
            // A fast fishing command must not cut the sit-down animation short.
            if (!interrupt && samePet && _layeredClip == "sit" && nextState != "idle")
            {
                _deferredState = nextState;
                _deferredPet = nextPet;
                return;
            }
            if (!interrupt && samePet && _groundbaitTimeline != null && _groundbaitTimeline.Ending
                && IsDeferredDestination(nextState) && nextState != "idle" && nextState != "groundbait")
            {
                _deferredState = nextState;
                _deferredPet = nextPet;
                return;
            }
            if (samePet && nextState == "catch" && _successCompleted && HoldCatch())
                return;
            if (samePet && nextState == "catch" && _layered != null
                && (_layeredClip == "reel" || _layeredClip == "catch" || _layeredClip == "reset_catch"))
            {
                _visualState = nextState;
                _deferredState = string.Empty;
                _deferredPet = string.Empty;
                if (_layeredClip == "reset_catch")
                    HoldCatch();
                return;
            }
            if (!interrupt && samePet && _catchHolding && IsDeferredDestination(nextState))
            {
                _deferredState = nextState;
                _deferredPet = nextPet;
                if (StartLayerClip("reset_catch"))
                    return;
            }
            if (!interrupt && samePet && ShouldFinishReel(nextState))
            {
                _deferredState = nextState;
                _deferredPet = nextPet;
                return;
            }
            if (samePet && _layered != null && _layered.Playback == "once"
                && ((nextState == "reel" && (_layeredClip == "reel" || _layeredClip == "catch" || _layeredClip == "reset_catch"))
                    || (nextState == "reel_empty" && (_layeredClip == "reel_empty" || _layeredClip == "reset_empty"))))
            {
                _visualState = nextState;
                _deferredState = string.Empty;
                _deferredPet = string.Empty;
                return;
            }
            var previousState = _visualState;
            _deferredState = string.Empty;
            _deferredPet = string.Empty;
            ReleaseFishingLayers();
            _visualState = nextState;
            _petId = nextPet;
            LayoutFishingLayers(_body.Width, _body.Height);
            _frameIndex = 0;
            _catchHolding = false;
            if (!samePet || (nextState != "sit" && nextState != "catch" && nextState != "ready"))
                _successCompleted = false;
            var clip = nextState == "sit" && previousState != "idle" && !string.IsNullOrEmpty(previousState)
                ? "ready" : nextState;
            if (nextState == "idle" && visualState == "idle" && samePet &&
                !string.IsNullOrEmpty(previousState) && previousState != "idle")
                clip = "leave";
            if (_seatMotion.WaitingForStand && nextState == "idle") clip = "leave";
            if (StartLayerClip(clip))
                return;
            if (_seatMotion.WaitingForStand) BeginSceneDeparture();
            ClearLayerSources();
            _layerHost.Visibility = Visibility.Collapsed;
            _frames = OverlayFrameCache.Get(_petId, _visualState);
            ShowFrame();
            _frameTimer.Stop();
            _frameTimer.Interval = TimeSpan.FromMilliseconds(125);
            if (_frames.Length > 1)
                _frameTimer.Start();
        }

        bool StartLayerClip(string clip)
        {
            var layered = OverlayFishingLayers.TryOpen(_petId, clip);
            if (layered == null)
                return false;
            _frameTimer.Stop();
            _catchHolding = false;
            ReleaseFishingLayers(false);
            _layered = layered;
            _layeredClip = clip;
            _audioClipActionId = _audioMarkers.ActionId;
            _audioPublishedClip = null;
            _audioPublishedPosition = -1;
            _audioOccurrence = 0;
            _waterLoopOccurrence = 0;
            _frameIndex = 0;
            OverlayFishingLayers.Acquire(_petId, clip);
            _frames = Array.Empty<ImageSource>();
            if (clip == "groundbait" && IsGroundbaitPhase(_fishingPhase) && layered.SourceFrameCount == 8)
            {
                _groundbaitTimeline = new OverlayGroundbaitTimeline(layered.DurationsMs,
                    layered.IndexAtSource(1), layered.IndexAtSource(7), layered.IndexAtSource(6), _groundbaitStartedAt);
                RefreshGroundbaitFrame();
                return true;
            }
            ShowLayered();
            if (LayerTimerRuns(layered) && clip != "sit" && clip != "leave")
                _frameTimer.Start();
            return true;
        }

        static bool LayerTimerRuns(FishingClipPlayback clip)
        {
            return clip != null && clip.FrameCount > 0 && clip.Playback != "hold";
        }

        static bool IsDeferredDestination(string state)
        {
            return state == "sit" || state == "idle" || state == "bait" || state == "cast"
                || state == "ready" || state == "fishing" || state == "groundbait";
        }

        bool HoldCatch()
        {
            if (_layeredClip != "catch" && !StartLayerClip("catch"))
                return false;
            _visualState = "catch";
            _deferredState = string.Empty;
            _deferredPet = string.Empty;
            _frameIndex = _layered.FrameCount - 1;
            _catchHolding = true;
            _frameTimer.Stop();
            ShowLayered();
            return true;
        }

        void RelayoutCluster()
        {
            LayoutPassCount++;
            TotalLayoutPasses++;
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
            LayoutFishingLayers(petW, petH);

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
                    // Exported name sits ~26px under the foot, which lands in the grass
                    // below the pier. Pull it onto the dock lip under the paws.
                    if (petH > 1 && nameSlotY + nameSlotH > petH + 4)
                        nameSlotY = Math.Max(0, petH - nameSlotH - 4);
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

            if (showRing)
            {
                statusW = ringW * .52;
                statusH = ringH * .52;
                _statusIcon.Width = statusW;
                _statusIcon.Height = statusH;
                statusX = ringX + (ringW - statusW) * .5;
                statusY = ringY + (ringH - statusH) * .5;
                _statusIcon.Margin = new Thickness(0);
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
            _bodyOriginX = petX + petW * 0.5;
            _bodyOriginY = petY + petH * 0.5;
            Place(_centerX, _centerY);
            _hitCatcher.Width = hitW;
            _hitCatcher.Height = hitH;
            Canvas.SetLeft(_hitCatcher, hitX);
            Canvas.SetTop(_hitCatcher, hitY);
            if (showRing)
                UpdateHookRing();
        }

        void UpdateStatusVisuals()
        {
            var iconKind = _fishingAsleep ? null : ResolveStatusIconKind(_fishingPhase);
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

            if (!_fishingAsleep && IsRingPhase(_fishingPhase))
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
            _ringBg.Source = OverlayHookProgressStyle.Background;
            _ringBg.Visibility = Visibility.Visible;
        }

        void UpdateHookRing()
        {
            if (_fishingAsleep || _hookDeadlineMs <= 0)
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
            _hookRingImage.Visibility = Visibility.Collapsed;
            var ringSize = _hookRing.Width > 1 ? _hookRing.Width : RingSize;
            _hookRing.Data = CreateRadial360(ringSize, fillAmount);
            _hookRing.Visibility = Visibility.Visible;
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
            if (_disposed || !_presentationVisible) return;
            _refreshTimer.SkipPeriods(OverlayAnimationClock.Now, 100);
            if (_hookRing.Visibility == Visibility.Visible ||
                _hookRingImage.Visibility == Visibility.Visible)
                UpdateHookRing();
            if (_hoverShown)
                RefreshOpenHover();
        }

        void OnFrameTick(object sender, EventArgs e)
        {
            if (_disposed) return;
            if (_groundbaitTimeline != null)
            {
                RefreshGroundbaitFrame();
                return;
            }
            if (_layered != null)
            {
                if (_layered.Playback != "once" && _layered.Playback != "hold" && _frameIndex >= _layered.LoopStart)
                {
                    double period = 0;
                    for (var i = _layered.LoopStart; i < _layered.FrameCount; i++) period += _layered.DurationMs(i);
                    _frameTimer.SkipPeriods(OverlayAnimationClock.Now, period);
                }
                AdvanceLayered();
                return;
            }

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

            _frameTimer.SkipPeriods(OverlayAnimationClock.Now, _frames.Length * 125);
            _frameIndex = (_frameIndex + 1) % _frames.Length;
            ShowFrame();
        }

        public void SetPointerOverPet(bool inside)
        {
            if (_disposed) return;
            if (inside)
            {
                if (OverlayInteractionState.SceneDragging)
                    return;

                _pointerInside = true;
                if (OverlayInteractionState.MenuBlocksHover())
                    return;

                if (!_hoverShown && !_tooltipTimer.IsEnabled)
                {
                    _tooltipTimer.Stop();
                    _tooltipTimer.Start();
                }

                return;
            }

            if (!_pointerInside)
                return;
            _pointerInside = false;
            _tooltipTimer.Stop();
            CloseHover();
        }

        /// <summary>
        /// Pointer is over the pet but the hover card never appeared (common after ContextMenu).
        /// </summary>
        public bool PointerHoverStalled =>
            _pointerInside &&
            !_hoverShown &&
            !_tooltipTimer.IsEnabled &&
            !OverlayInteractionState.SceneDragging &&
            !OverlayInteractionState.MenuBlocksHover();

        void OnTooltipTimerTick(object sender, EventArgs e)
        {
            _tooltipTimer.Stop();
            if (!_pointerInside ||
                OverlayInteractionState.SceneDragging ||
                OverlayInteractionState.MenuBlocksHover())
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
            SyncRefreshClock();
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
            SyncRefreshClock();
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
            if (_fishingAsleep)
                return "睡着了 · 咬钩会脱钩\n" + (ActorKey == "__own__" ? "点击猫咪，唤醒继续钓鱼" : "等待主人点击唤醒");
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
            if (!_presentationVisible || _disposed) return;
            if (_layered != null)
            {
                ShowLayered();
                return;
            }

            if (_frames.Length == 0)
            {
                _image.Visibility = Visibility.Collapsed;

                return;
            }

            _image.Visibility = Visibility.Visible;
            _image.Source = _frames[_frameIndex % _frames.Length];
        }

        void LayoutFishingLayers(double petW, double petH)
        {
            OverlayFishingLayers.Place(
                _petId, petW, petH, out var drawW, out var drawH, out var originX, out var originY);
            var sizeChanged = drawW != _layerDrawW || drawH != _layerDrawH;
            _layerDrawW = drawW;
            _layerDrawH = drawH;
            var margin = new Thickness(originX, originY, 0, 0);
            for (var i = 0; i < _layerImages.Length; i++)
            {
                _layerImages[i].Width = drawW;
                _layerImages[i].Height = drawH;
                _layerImages[i].Margin = margin;
            }

            if (!sizeChanged)
            {
                if (_layered != null) ApplyWaterContact(_frameIndex, _layered.FrameCount);
                return;
            }
            if (_layered != null)
                ShowLayered();
        }

        void ShowLayered()
        {
            if (_layered == null || _layered.FrameCount <= 0) return;
            _frameTimer.Interval = TimeSpan.FromMilliseconds(_layered.DurationMs(_frameIndex));
            // Seat transitions must show every frame for its authored duration,
            // even on a cold decode or after a long hidden interval.
            if (_layeredClip == "sit" || _layeredClip == "leave") _frameTimer.Stop();
            _renderVersion++;
            _frameRequest?.Cancel();
            _frameDirty = true;
            OverlayAnimationClock.Wake();
        }

        async void RenderLatestFrame()
        {
            if (_disposed) return;
            AdvanceSceneMotion();
            if (_disposed || !_presentationVisible || !_frameDirty || _frameLoading || _layered == null) return;
            _frameDirty = false;
            _frameLoading = true;
            var request = new CancellationTokenSource();
            _frameRequest = request;
            var version = _renderVersion;
            var index = _frameIndex;
            var clip = _layered;
            var waterToken = _waterEffects.Policy.Version;
            var clipName = _layeredClip;
            var audioRevision = _audioMarkers.Revision;
            var audioGeneration = _audioMarkers.Generation;
            var audioActionId = _audioClipActionId;
            var rejected = OverlayFishingLayers.RejectedDecodes;
            try
            {
                var pending = new Task<BitmapImage>[_layerImages.Length];
                for (var i = 0; i < pending.Length; i++)
                {
                    var layer = OverlayFishingLayers.LayerNames[i];
                    var sleepingHead = _fishingAsleep && _layeredClip == "fishing" && layer == "head"
                        ? OverlayFishingLayers.SleepHead(_petId) : null;
                    pending[i] = OverlayFishingLayers.BitmapAsync(
                        sleepingHead ?? OverlayFishingLayers.LayerFile(_petId, _layeredClip, index, layer),
                        _layerDrawW, _layerDrawH, request.Token);
                }
                var images = await Task.WhenAll(pending);
                if (_disposed || version != _renderVersion || !_presentationVisible) return;
                foreach (var image in images)
                    if (image == null)
                    {
                        _frameDirty = OverlayFishingLayers.RejectedDecodes != rejected;
                        // A missing asset must not strand an actor at a released seat.
                        if (!_frameDirty && (_layeredClip == "sit" || _layeredClip == "leave"))
                        {
                            _frameTimer.Start();
                            _frameTimer.Due = OverlayAnimationClock.Now + clip.DurationMs(index);
                            OverlayAnimationClock.Wake();
                        }
                        return;
                    }
                // Publish all nine layers atomically on the dispatcher. Missing/pending
                // images never clear individual layers or mix two animation frames.
                var order = clip.Orders[index];
                for (var i = 0; i < images.Length; i++)
                {
                    var z = Array.IndexOf(order, OverlayFishingLayers.LayerNames[i]);
                    if (Panel.GetZIndex(_layerImages[i]) != z) Panel.SetZIndex(_layerImages[i], z);
                    var source = OverlayFishingLayers.LayerNames[i] == "line" && clip.Motion != null
                        ? OverlayFishingMotionDrawing.Line(clip.Motion[index]) ?? images[i] : images[i];
                    if (!ReferenceEquals(_layerImages[i].Source, source)) _layerImages[i].Source = source;
                }
                ApplyWaterContact(index, clip.FrameCount);
                _image.Visibility = Visibility.Collapsed;
                _layerHost.Visibility = Visibility.Visible;
                if (_waterEffects.Policy.CanPublish(waterToken)) PublishWaterFrame(index, clip);
                PublishAudioMarkers(index, clip, clipName, audioRevision, audioGeneration, audioActionId);
                if (_layeredClip == "sit" || _layeredClip == "leave")
                {
                    _frameTimer.Start();
                    _frameTimer.Due = OverlayAnimationClock.Now + clip.DurationMs(index);
                    OverlayAnimationClock.Wake();
                }
            }
            finally
            {
                _frameRequest = null;
                request.Dispose();
                _frameLoading = false;
                if (!_disposed && _frameDirty) OverlayAnimationClock.Wake();
            }
        }


        // Called exclusively after all nine sources and their 22B transforms are
        // published. Authored source positions survive interpolation expansion.
        void PublishWaterFrame(int frame, FishingClipPlayback clip)
        {
            var resources = WaterEffectResources.Current;
            if (resources?.Source?.Frames == null) { WaterDiagnostic("resources-not-ready-no-replay"); return; }
            if (_waterEffects.Policy.Envelope == null) { WaterDiagnostic("old-ipc-no-envelope"); return; }
            var source = clip.SourceIndex(frame);
            if (!resources.Source.Frames.TryGetValue(_layeredClip + "/" + source, out var layers)) return;
            var surface = WaterSurfaceY;
            _waterEffects.SetSurface(surface);
            var slotScale = _body.Width / 128.0; // logical DIP; Windows DPI is not multiplied again
            bool Bounds(string layer, out Rect bounds)
            {
                bounds = Rect.Empty;
                if (!layers.TryGetValue(layer, out var data) || data.Bounds == null || data.Bounds.Length != 4) return false;
                var index = Array.IndexOf(OverlayFishingLayers.LayerNames, layer);
                if (index < 0) return false;
                var image = _layerImages[index];
                var b = data.Bounds;
                var raw = new Rect(b[0] * _layerDrawW / 640, b[1] * _layerDrawH / 512,
                    (b[2] - b[0]) * _layerDrawW / 640, (b[3] - b[1]) * _layerDrawH / 512);
                bounds = image.RenderTransform.TransformBounds(raw);
                bounds.Offset(image.Margin.Left, image.Margin.Top);
                return !bounds.IsEmpty;
            }
            bool Contact(string layer, Rect bounds, out Point anchor)
            {
                anchor = new Point(bounds.Left + bounds.Width / 2, surface);
                // A bent retrieve line's bounding-box midpoint is not its water
                // intersection. Offline alpha row centres locate the visible
                // line, transformed by the same published 22B layer pose.
                if (layer == "line")
                {
                    if (!layers.TryGetValue(layer, out var data) || data.RowCenters == null) return false;
                    var image = _layerImages[Array.IndexOf(OverlayFishingLayers.LayerNames, layer)];
                    Point? previous = null;
                    foreach (var row in data.RowCenters)
                    {
                        if (row == null || row.Length != 2) continue;
                        var point = image.RenderTransform.Transform(new Point(row[1] * _layerDrawW / 640, row[0] * _layerDrawH / 512));
                        point.Offset(image.Margin.Left, image.Margin.Top);
                        if (previous.HasValue && (previous.Value.Y - surface) * (point.Y - surface) <= 0)
                        {
                            var dy = point.Y - previous.Value.Y;
                            var fraction = Math.Abs(dy) < .0001 ? 0 : (surface - previous.Value.Y) / dy;
                            anchor.X = previous.Value.X + (point.X - previous.Value.X) * fraction;
                            return true;
                        }
                        previous = point;
                    }
                    return false;
                }
                // At most one source pixel accommodates alpha raster sampling;
                // marker time alone never makes a falling bait touch the plane.
                var tolerance = _layerDrawH / 512.0;
                return bounds.Top <= surface + tolerance && bounds.Bottom >= surface - tolerance;
            }
            var anchorLayer = _layeredClip == "groundbait" ? "bait" : _layeredClip == "reel" ? "line" :
                _layeredClip == "cast" || _layeredClip == "fishing" || _layeredClip == "reel_empty" ? "bobber" : "fish";
            if (Bounds(anchorLayer, out var actualBounds))
            {
                Contact(anchorLayer, actualBounds, out var actual);
                if (!_waterContactLocal.HasValue || Math.Abs(actual.X - _waterContactLocal.Value.X) > 4 * slotScale || actual.Y != _waterContactLocal.Value.Y)
                { _waterContactLocal = actual; WaterGeometryChanged?.Invoke(); }
            }
            var origin = WaterBodySceneOrigin;
            var scene = _waterSceneClip;
            if (scene.IsEmpty) { _waterEffects.SetClip(Rect.Empty); return; }
            var local = new Rect(scene.X - origin.X, scene.Y - origin.Y, scene.Width, scene.Height);
            local.Intersect(new Rect(-2048, surface - 36 * slotScale, 4096, 68 * slotScale));
            _waterEffects.SetClip(local);
            bool Emit(string cue, string layer, long occurrence)
            {
                if (!Bounds(layer, out var bounds)) { WaterDiagnostic(cue + ":missing-source-bounds"); return false; }
                var touching = Contact(layer, bounds, out var anchor);
                // Hidden fish still disturb the water at their horizontal position.
                // Surface/exit cues continue to require a real intersection.
                var submerged = OverlayFishingWaterContact.ProjectsDisturbance(cue, _layeredClip);
                if (submerged) touching = touching || bounds.Top >= surface - 2 * slotScale;
                if (submerged) _waterEffects.Policy.Envelope.WaterSizeTier = "S";
                else if (layer == "fish" && slotScale > 0)
                    _waterEffects.Policy.Envelope.WaterSizeTier = OverlayWaterEffectsPolicy.VisibleTier(bounds.Width / slotScale);
                var emitted = _waterEffects.Publish(cue, _fishingPhase, occurrence, anchor, slotScale, touching);
                if (!emitted && _waterEffects.Policy.LastRejection != null)
                    WaterDiagnostic(cue + ":" + _waterEffects.Policy.LastRejection);
                return emitted;
            }
            if ((_layeredClip == "hooked" || _layeredClip == "bite" || _layeredClip == "effort") &&
                (_fishingPhase == "hooked" || _fishingPhase == "effort"))
            {
                Emit("bite.onset", "fish", 0);
                WaterDiagnostic("fish.surface/slap:hooked-effort-source-clips-submerged");
            }
            foreach (var marker in resources.Source.Markers ?? Array.Empty<SourceWaterMarker>())
            {
                if (marker.Clip != _layeredClip || marker.Source != source || clip.SourcePosition(frame) < marker.Source) continue;
                if (marker.Cue == "waiting.ripple")
                {
                    if (OverlayAnimationClock.Now < _waterNextWaitAt) continue;
                    _waterNextWaitAt = OverlayAnimationClock.Now + 2400;
                    Emit(marker.Cue, marker.Layer, ++_waterWaitingOccurrence);
                }
                else Emit(marker.Cue, marker.Layer, marker.Cue == "float.contact" || marker.Cue == "retrieve.contact" ||
                    marker.Cue == "fish.surface" && _layeredClip == "reel" ? 0 : _waterLoopOccurrence);
            }
        }

        void PublishAudioMarkers(int index, FishingClipPlayback clip, string clipName,
            int revision, long generation, string actionId)
        {
            if (_disposed || !_presentationVisible || !IsVisible || AudioMarkerPublished == null ||
                OverlayAudioSourceMarkers.Metadata == null) return;
            var position = clip.SourcePosition(index);
            if (_audioPublishedClip == clipName && position < _audioPublishedPosition) _audioOccurrence++;
            _audioPublishedClip = clipName; _audioPublishedPosition = position;
            var occurrence = clipName == "groundbait"
                ? OverlayAudioSourceMarkers.GroundbaitOccurrence(clip, OverlayAnimationClock.Now - _groundbaitStartedAt)
                : _audioOccurrence;
            var sample = clip.Motion == null ? null : clip.Motion[index];
            var contact = sample == null || sample.Position == sample.Source
                ? OverlayFishingWaterContact.Frame(clipName, clip.SourceIndex(index)) : sample.Contact;
            var scale = _layerDrawH / (double)OverlayFishingLayers.CanvasHeight;
            var origin = _layerImages[0].Margin.Top;
            var surface = WaterSurfaceY;
            var pose = OverlayFishingWaterContact.Solve(contact, scale, origin, surface,
                OverlayFishingWaterContact.Weight(clipName, position, clip.SourceFrameCount));
            if (clipName == "groundbait")
                pose.BaitOffset = OverlayFishingWaterContact.GroundbaitOffset(position, scale, origin, surface);

            foreach (var definition in OverlayAudioSourceMarkers.Metadata.Markers)
            {
                // A valid new bite may first become visible after its intro source
                // frame. Ordinary skipped cast/water markers are never caught up.
                var biteOnset = definition.Marker == "bite.onset" && definition.Clip == clipName;
                var heldResult = definition.Marker == "result.presented" && definition.Clip == clipName;
                if (!biteOnset && !heldResult && !OverlayAudioSourceMarkers.InWindow(definition, clipName, position)) continue;
                var contactMatches = OverlayAudioSourceMarkers.Contact(definition.ContactLayer,
                    clipName, clip.SourceIndex(index), sample, contact, pose, scale, origin, surface);
                var marker = _audioMarkers.Consume(definition.Marker,
                    biteOnset || heldResult || definition.Marker == "cast.release" || definition.Marker == "float.contact" ||
                    definition.Marker == "retrieve.start" || definition.Marker == "escape.contact" ? 0 : occurrence,
                    revision, generation, actionId, contactMatches, NowMs());
                if (marker != null)
                {
                    // H uses a phase-presentation event UUID; I retains the stable
                    // catch ID. Their shared actor/action capability identifies
                    // the same published fish without comparing unlike event IDs.
                    marker.WaterSizeTier = OverlayAudioMarkerPolicy.VisibleTier(marker.Identity, WaterPresentation, _fishingPhase);
                    AudioMarkerPublished(marker);
                }
            }
        }

        double WaterSurfaceY => OverlayFishingWaterContact.LocalSurface(
            _chrome?.WaterlineY, WaterBodySceneOrigin.Y,
            _chrome?.Seat != null ? _chrome.Seat.y + _chrome.Seat.h + 6 : _body.Height + 6);

        void ApplyWaterContact(int frame, int count)
        {
            var scale = _layerDrawH / (double)OverlayFishingLayers.CanvasHeight;
            var scaleX = _layerDrawW / (double)OverlayFishingLayers.CanvasWidth;
            var origin = _layerImages[0].Margin.Top;
            // Contact stays on scene water even when the seat is placed farther up the bank.
            var surface = WaterSurfaceY;
            var sample = _layered?.Motion == null ? null : _layered.Motion[frame];
            var sourceFrame = _layered?.SourceIndex(frame) ?? frame;
            var contact = sample == null || sample.Position == sample.Source
                ? OverlayFishingWaterContact.Frame(_layeredClip, sourceFrame) : sample.Contact;
            var resting = _layeredClip == "fishing" && (_fishingPhase == "waiting" || _fishingAsleep && IsSleepFishingPhase(_fishingPhase));
            var rest = _restMotion.Update(OverlayAnimationClock.Now, resting, _fishingAsleep);
            var lineRest = RestPose.Identity.Matrix;
            if (resting && contact != null)
            {
                var tipX = sample?.Line?[0] ?? OverlayFishingRestMotion.FishingTipX;
                var tipY = sample?.Line?[1] ?? OverlayFishingRestMotion.FishingTipY;
                lineRest = rest.Line(tipX, tipY, contact.LineBottom);
                contact = new FishingContactFrame
                {
                    LineTop = rest.Y(tipX, tipY), LineBottom = contact.LineBottom,
                    BobberY = contact.BobberY, FishTop = contact.FishTop
                };
            }
            _sleepLabel.Visibility = rest.SleepBlend > .01 ? Visibility.Visible : Visibility.Collapsed;
            _sleepLabel.Opacity = rest.BubbleOpacity;
            _sleepLabel.RenderTransform = new TranslateTransform(0, -rest.BubbleRise);
            var pose = OverlayFishingWaterContact.Solve(contact,
                scale, origin, surface, OverlayFishingWaterContact.Weight(_layeredClip,
                    _layered?.SourcePosition(frame) ?? frame, _layered?.SourceFrameCount ?? count));
            if (_layeredClip == "groundbait")
                pose.BaitOffset = OverlayFishingWaterContact.GroundbaitOffset(
                    _layered?.SourcePosition(frame) ?? frame, scale, origin, surface);
            for (var i = 0; i < _layerImages.Length; i++)
            {
                var layer = OverlayFishingLayers.LayerNames[i];
                Transform transform = Transform.Identity;
                if (layer == "line" && pose.LineScale != 1)
                    transform = new ScaleTransform(1, pose.LineScale, 0, pose.LinePivot);
                else if (layer == "bobber" && pose.BobberOffset != 0) transform = new TranslateTransform(0, pose.BobberOffset);
                else if (layer == "bait" && pose.BaitOffset != 0) transform = new TranslateTransform(0, pose.BaitOffset);
                else if (layer == "fish" && pose.FishOffset != 0) transform = new TranslateTransform(0, pose.FishOffset);
                if (sample != null || resting)
                {
                    var values = sample?.Transforms[i] ?? RestPose.Identity.Matrix;
                    var matrix = new Matrix(values[0], values[1] * scale / scaleX,
                        values[2] * scaleX / scale, values[3], values[4] * scaleX, values[5] * scale);
                    if (sample?.Root != null)
                    {
                        var root = sample.Root;
                        matrix.Append(new Matrix(root[0], root[1] * scale / scaleX,
                            root[2] * scaleX / scale, root[3], root[4] * scaleX, root[5] * scale));
                    }
                    if (resting && (layer == "body" || layer == "head" || layer == "far_hand" || layer == "hands" || layer == "rod" || layer == "line"))
                    {
                        var restValues = layer == "line" ? lineRest : rest.Matrix;
                        matrix.Append(new Matrix(restValues[0], restValues[1] * scale / scaleX,
                            restValues[2] * scaleX / scale, restValues[3], restValues[4] * scaleX, restValues[5] * scale));
                    }
                    if (!matrix.IsIdentity)
                    {
                        var motion = new MatrixTransform(matrix);
                        if (transform.Value.IsIdentity) transform = motion;
                        else
                        {
                            var combined = new TransformGroup();
                            combined.Children.Add(motion);
                            combined.Children.Add(transform);
                            transform = combined;
                        }
                    }
                }
                if (transform.CanFreeze) transform.Freeze();
                _layerImages[i].RenderTransform = transform;
                if (layer == "fish") OverlayFishWaterMask.Apply(_layerImages[i], _layeredClip, surface);
            }
        }

        void AdvanceLayered()
        {
            if (_groundbaitTimeline != null)
            {
                RefreshGroundbaitFrame();
                return;
            }
            if (_layered == null || _layered.FrameCount == 0 || _layered.Playback == "hold")
            {
                _frameTimer.Stop();
                return;
            }
            if (_layered.Playback == "once")
            {
                if (_frameIndex < _layered.FrameCount - 1)
                {
                    _frameIndex++;
                    ShowLayered();
                    return;
                }
                if (_layeredClip == "catch" && _visualState == "catch" && string.IsNullOrEmpty(_deferredState))
                {
                    _catchHolding = true;
                    _frameTimer.Stop();
                    return;
                }
                var completedSuccess = _layeredClip == "reset_catch";
                var completedLeave = _layeredClip == "leave";
                var nextClip = _layered.NextClip;
                if (!string.IsNullOrEmpty(nextClip) && StartLayerClip(nextClip))
                {
                    if (completedLeave) BeginSceneDeparture();
                    if (_layered.Playback == "hold")
                    {
                        _successCompleted = completedSuccess;
                        FlushDeferredVisual();
                    }
                    return;
                }
                _frameTimer.Stop();
                if (completedLeave) BeginSceneDeparture();
                FlushDeferredVisual();
                return;
            }
            _frameIndex++;
            if (_frameIndex >= _layered.FrameCount)
            {
                _frameIndex = _layered.LoopStart;
                _waterLoopOccurrence++;
            }
            ShowLayered();
        }

        static bool IsSleepFishingPhase(string phase)
        {
            return phase == "waiting" || phase == "hooked" || phase == "resolving" || phase == "baiting" || phase == "casting" || phase == "seated";
        }

        void RefreshGroundbaitFrame()
        {
            if (_groundbaitTimeline == null || _layered == null) return;
            var now = OverlayAnimationClock.Now;
            var frame = _groundbaitTimeline.Sample(now);
            if (frame.Complete)
            {
                _groundbaitTimeline = null;
                _frameTimer.Stop();
                if (!string.IsNullOrEmpty(_deferredState))
                    FlushDeferredVisual();
                else
                    StartLayerClip(string.IsNullOrEmpty(_layered.NextClip) ? "ready" : _layered.NextClip);
                return;
            }
            _frameIndex = frame.Index;
            _waterLoopOccurrence = frame.Occurrence;
            ShowLayered();
            _frameTimer.Stop();
            _frameTimer.Interval = TimeSpan.FromMilliseconds(frame.RemainingMs);
            _frameTimer.Start();
            _frameTimer.Due = now + _frameTimer.Interval.TotalMilliseconds;
        }

        string GroundbaitVisualState(string visualState)
        {
            if (string.Equals(visualState, "offline", StringComparison.OrdinalIgnoreCase)
                || string.Equals(visualState, "dragging", StringComparison.OrdinalIgnoreCase))
                return visualState;
            if (IsGroundbaitPhase(_fishingPhase)) return "groundbait";
            if (!string.Equals(visualState, "groundbait", StringComparison.OrdinalIgnoreCase)) return visualState;
            switch (_fishingPhase)
            {
                case "seated": return "sit";
                case "baiting": return "bait";
                case "casting": return "cast";
                case "waiting": return "fishing";
                case "hooked": return "hooked";
                case "idle": return "idle";
                default: return "ready";
            }
        }

        bool ShouldFinishReel(string nextState)
        {
            if (_layered == null || _layered.Playback != "once" || !IsDeferredDestination(nextState))
                return false;
            return _layeredClip == "reel" || _layeredClip == "reel_empty" || _layeredClip == "catch"
                || _layeredClip == "reset_catch" || _layeredClip == "reset_empty";
        }

        void FlushDeferredVisual()
        {
            if (string.IsNullOrEmpty(_deferredState))
                return;
            var state = _deferredState;
            var pet = _deferredPet;
            _deferredState = string.Empty;
            _deferredPet = string.Empty;
            _latestState = string.Empty;
            ApplyVisualState(state, pet);
        }

        void ClearLayerSources()
        {
            for (var i = 0; i < _layerImages.Length; i++)
                _layerImages[i].Source = null;
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
                case "bait": return "baiting";
                case "cast": return "casting";
                case "fishing": return "waiting";
                case "hooked": return "hooked";
                case "groundbait": return "groundbaiting";
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
                case "bait":
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


        public Border SpeechHost => _speechBadge;

        public Border HintHost => _hintBadge;

        public void ShowSpeech(string text, bool autoHide)
        {
            if (_disposed) return;
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
            if (_disposed) return;
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
            var fade = new DoubleAnimation(_speechBadge.Opacity, 0, TimeSpan.FromSeconds(.12));
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
                Foreground = CozyTheme.Ink,
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
            var badge = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 0, 8, 0),
                Child = label,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 1.0),
                RenderTransform = new ScaleTransform(1, 1),
            };
            CozyTheme.SetIsPaper(badge, true);
            return badge;
        }

        static Border CreateHintBadge(TextBlock label)
        {
            var badge = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 0, 10, 0),
                BorderThickness = new Thickness(0),
                Child = label,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 1.0),
                RenderTransform = new ScaleTransform(1, 1),
            };
            CozyTheme.SetIsPaper(badge, true);
            return badge;
        }

        static void RunPopIn(Border bubble)
        {
            if (bubble == null) return;
            // Quiet UI feedback leaves the fishing actor and its animation unobstructed.
            bubble.RenderTransform = Transform.Identity;
            bubble.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(.12)));
        }



        static Border CreateTextBadge(TextBlock textBlock, Thickness margin)
        {
            textBlock.VerticalAlignment = VerticalAlignment.Center;
            textBlock.HorizontalAlignment = HorizontalAlignment.Center;
            var badge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 0, 6, 0),
                Margin = margin,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(0),
                Opacity = 0.82,
                Child = textBlock,
            };
            CozyTheme.SetIsPaper(badge, true);
            return badge;
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
            return clip == "bait" || clip == "cast" || clip == "reel" || clip == "reel_empty" || clip == "catch";
        }

        static ImageSource[] Load(string petId, string visualState)
        {
            var id = NormalizePetId(petId);
            var directory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "OverlayResources", "hud", "cozy", "characters", "avatars");
            var filename = System.IO.Path.Combine(directory, id + ".png");
            if (!File.Exists(filename))
                filename = System.IO.Path.Combine(directory, "orange.png");
            if (!File.Exists(filename))
                return new ImageSource[0];
            return new ImageSource[] { LoadBitmap(filename) };
        }

        public static string NormalizePetId(string petId)
        {
            switch (petId)
            {
                case "calico": case "orange": case "white": case "silver": case "brown":
                case "cream": case "blue": case "black": case "cow": case "dafeiyu": case "sangbiao":
                    return petId;
                default:
                    return "orange";
            }
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
