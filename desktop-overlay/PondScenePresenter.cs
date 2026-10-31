using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FishSocialOverlay
{
    /// <summary>
    /// Renders pond water, spots and pond pets from Unity-pushed snapshot fields.
    /// </summary>
    public sealed class PondScenePresenter : IDisposable
    {
        public const double SceneWidth = 960;
        public const double SceneHeight = 560;
        public const double CatSize = 64;
        const string OwnActorKey = "__own__";

        readonly Canvas _spotLayer;
        readonly Canvas _actorLayer;
        readonly Canvas _decorLayer;
        readonly OverlayHoverPresenter _hoverPresenter;
        readonly IOverlayPlayerMenuHost _menuHost;
        readonly Image _backgroundImage;
        readonly Shape _grass;
        readonly Shape _shore;
        readonly Shape _water;
        readonly OverlayPondLayout _layout = new OverlayPondLayout();
        readonly Dictionary<string, OverlaySpotMarker> _spotVisuals =
            new Dictionary<string, OverlaySpotMarker>(StringComparer.Ordinal);
        readonly Dictionary<string, OverlayPetActor> _others =
            new Dictionary<string, OverlayPetActor>();
        OverlayPetActor _ownActor;
        bool _otherNameplatesShown = true;
        bool _actionChromeShown = true;
        const long PredictedSeatTimeoutMs = 3000;
        IpcMessage _lastMessage;
        string _predictedSpotId;
        long _predictedUntilMs;
        bool _disposed;
        CancellationTokenSource _artLoad = new CancellationTokenSource();
        int _artVersion;
        internal bool BackgroundPending { get; private set; }
        bool _decorPending;
        OverlaySceneMotion _sceneMotion;
        public bool IsPresentationReady => !_disposed && !string.IsNullOrEmpty(_pondId) && !BackgroundPending && !_decorPending;
        public event Action ScenePresentationChanged;
        public event Action<OverlayPetActor, string, long, string, string> FishingWaterMarkerPublished;
        void ForwardWaterMarker(OverlayPetActor actor, string cue, long occurrence, string key, string tier)
            => FishingWaterMarkerPublished?.Invoke(actor, cue, occurrence, key, tier);
        public event Action<OverlayAudioMarker> AudioMarkerPublished;
        public void CancelOwnAudioPresentation() => _ownActor?.CancelAudioPresentation();
        string _spotGeometry;
        readonly Dictionary<string, OverlayActorChrome> _chromeBySpot = new Dictionary<string, OverlayActorChrome>();
        OverlayActorChrome ActorChrome(string spotId)
        {
            var key = spotId ?? string.Empty;
            if (!_chromeBySpot.TryGetValue(key, out var chrome))
            {
                chrome = _layout.ResolveActorChrome(spotId);
                _chromeBySpot[key] = chrome;
            }
            return chrome;
        }
        Rect _viewport = Rect.Empty;
        bool _windowVisible = true;

        public void SetPresentationViewport(Rect viewport, bool visible)
        {
            _viewport = viewport;
            _windowVisible = visible;
            _sceneMotion?.SetVisible(visible);
            UpdateActorVisibility();
        }

        void UpdateActorVisibility()
        {
            var visible = new List<OverlayPetActor>();
            foreach (OverlayPetActor actor in _actorLayer.Children)
            {
                var bounds = actor.PresentationBounds;
                bounds.Inflate(256, 256);
                var shown = _windowVisible && (_viewport.IsEmpty || bounds.IntersectsWith(_viewport));
                actor.SetPresentationVisible(shown);
                if (shown && (_viewport.IsEmpty || actor.WaterPetBounds.IntersectsWith(_viewport))) visible.Add(actor);
            }
            var own = _ownActor?.WaterBodySceneOrigin ?? new Point(ContentWidth / 2, ContentHeight / 2);
            var nearest = new List<OverlayPetActor>();
            foreach (var actor in visible) if (!ReferenceEquals(actor, _ownActor)) nearest.Add(actor);
            double Distance(OverlayPetActor actor)
            { var d = actor.WaterBodySceneOrigin - own; return d.X * d.X + d.Y * d.Y; }
            nearest.Sort((a, b) => Distance(a).CompareTo(Distance(b)));
            var sceneClip = new Rect(0, 0, Math.Max(1, ContentWidth), Math.Max(1, ContentHeight));
            if (!_viewport.IsEmpty) sceneClip.Intersect(_viewport);
            foreach (OverlayPetActor actor in _actorLayer.Children)
            {
                var clip = sceneClip;
                var point = actor.WaterAnchorScene;
                // Same-row seats share a midpoint boundary; effects cannot spill
                // into a neighbour even when the XL texture is wider than space.
                double left = clip.Left, right = clip.Right;
                foreach (var other in visible)
                {
                    if (ReferenceEquals(actor, other)) continue;
                    var p = other.WaterAnchorScene;
                    if (!p.HasValue || !point.HasValue || Math.Abs(p.Value.Y - point.Value.Y) > Math.Max(actor.WaterPetBounds.Height, other.WaterPetBounds.Height) * .5) continue;
                    var midpoint = (point.Value.X + p.Value.X) / 2;
                    if (p.Value.X < point.Value.X) left = Math.Max(left, midpoint);
                    else if (p.Value.X > point.Value.X) right = Math.Min(right, midpoint);
                }
                if (right <= left) clip = Rect.Empty;
                else clip = new Rect(left, clip.Top, right - left, clip.Height);
                actor.SetWaterSceneClip(clip, ReferenceEquals(actor, _ownActor) || nearest.IndexOf(actor) >= 0 && nearest.IndexOf(actor) < 3);
            }
        }

        static string SpotGeometry(OverlaySpotDto[] spots)
        {
            if (spots == null) return string.Empty;
            var text = new System.Text.StringBuilder();
            foreach (var spot in spots)
                if (spot != null) text.Append(spot.Id?.Length ?? 0).Append(':').Append(spot.Id)
                    .Append(':').Append(spot.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(':').Append(spot.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(';');
            return text.ToString();
        }
        string _pondId = string.Empty;
        string _ownPlayerId = string.Empty;
        string _ownUserId = string.Empty;
        readonly Dictionary<string, OverlayPetActor> _actorsByUserId =
            new Dictionary<string, OverlayPetActor>(StringComparer.Ordinal);
        public event Action<string> SpotSelected;
        /// <summary>Fired when pond layout / content size changes (STEAM-DESKTOP-14B).</summary>
        public event Action SceneContentChanged;
        /// <summary>Fired when local seat prediction changes (BUG-54: phase timeline lives in Unity).</summary>
        public event Action LocalPresentationChanged;

        public bool HasPredictedSeat => _predictedSpotId != null;

        public double ContentWidth { get; private set; } = SceneWidth;

        public double ContentHeight { get; private set; } = SceneHeight;

        public PondScenePresenter(
            Canvas spotLayer,
            Canvas actorLayer,
            Canvas hoverLayer,
            Canvas decorLayer,
            Image backgroundImage,
            Shape grass,
            Shape shore,
            Shape water,
            IOverlayPlayerMenuHost menuHost)
        {
            _spotLayer = spotLayer;
            _actorLayer = actorLayer;
            _decorLayer = decorLayer;
            _hoverPresenter = new OverlayHoverPresenter(hoverLayer);
            _menuHost = menuHost;
            _backgroundImage = backgroundImage;
            _grass = grass;
            _shore = shore;
            _water = water;
            _grass.Visibility = _shore.Visibility = _water.Visibility = Visibility.Collapsed;
            _backgroundImage.Source = null;
            _backgroundImage.Visibility = Visibility.Collapsed;
        }

        public void SetActionChromeShown(bool shown)
        {
            _actionChromeShown = shown;
            SetOtherNameplatesShown(shown);
            _ownActor?.SetGroundbaitChromeShown(shown);
        }

        public void SetOtherNameplatesShown(bool shown)
        {
            _otherNameplatesShown = shown;
            foreach (var actor in _others.Values)
            {
                actor.SetNameplateShown(shown);
                actor.SetGroundbaitChromeShown(shown);
            }
        }

        public void CancelAllHovers()
        {
            _hoverPresenter.HideAllHoverCards();
            if (_ownActor != null)
                _ownActor.CancelTooltip();
            foreach (var actor in _others.Values)
                actor.CancelTooltip();
        }

        public bool AnyPlayerContextMenuOpen()
        {
            if (_ownActor != null && _ownActor.IsPlayerContextMenuOpen)
                return true;
            foreach (var actor in _others.Values)
            {
                if (actor.IsPlayerContextMenuOpen)
                    return true;
            }

            return false;
        }

        public bool AnyPlayerContextMenuUnderMouse()
        {
            if (_ownActor != null && _ownActor.IsPlayerContextMenuUnderMouse)
                return true;
            foreach (var actor in _others.Values)
            {
                if (actor.IsPlayerContextMenuUnderMouse)
                    return true;
            }

            return false;
        }

        public void UpdatePointerHover(Point pondLocal, UIElement ancestor)
        {
            OverlayPetActor hit = null;
            for (var i = _actorLayer.Children.Count - 1; i >= 0; i--)
            {
                if (!(_actorLayer.Children[i] is OverlayPetActor actor))
                    continue;
                if (!actor.HitTestsPetArt(pondLocal, ancestor))
                    continue;
                hit = actor;
                break;
            }

            if (_ownActor != null)
                _ownActor.SetPointerOverPet(ReferenceEquals(hit, _ownActor));
            foreach (var actor in _others.Values)
                actor.SetPointerOverPet(ReferenceEquals(hit, actor));
        }

        public bool NeedsHoverResync()
        {
            if (_ownActor != null && _ownActor.PointerHoverStalled)
                return true;
            foreach (var actor in _others.Values)
            {
                if (actor.PointerHoverStalled)
                    return true;
            }

            return false;
        }

        public void ForceResyncPointerHover(Point pondLocal, UIElement ancestor)
        {
            if (_ownActor != null)
                _ownActor.CancelTooltip();
            foreach (var actor in _others.Values)
                actor.CancelTooltip();
            UpdatePointerHover(pondLocal, ancestor);
        }

        public FrameworkElement TryResolveActor(string actorKey)
        {
            if (string.IsNullOrEmpty(actorKey))
                return null;
            if (!string.IsNullOrEmpty(_ownUserId) &&
                string.Equals(actorKey, _ownUserId, StringComparison.Ordinal))
                return _ownActor;
            if (!string.IsNullOrEmpty(_ownPlayerId) &&
                string.Equals(actorKey, _ownPlayerId, StringComparison.Ordinal))
                return _ownActor;
            if (_actorsByUserId.TryGetValue(actorKey, out var byUser))
                return byUser;
            if (_others.TryGetValue(actorKey, out var actor))
                return actor;

            return null;
        }

        public FrameworkElement TryResolveOwnActor()
        {
            return _ownActor;
        }

        public void ClearAllActorBubbles()
        {
            _ownActor?.ClearBubbles();
            foreach (var actor in _others.Values)
                actor.ClearBubbles();
        }

        public FrameworkElement TryResolveSpot(string spotId)
        {
            if (string.IsNullOrEmpty(spotId))
                return null;
            OverlaySpotMarker marker;
            if (_spotVisuals.TryGetValue(spotId, out marker))
                return marker;
            return null;
        }

        public void Apply(IpcMessage message)
        {
            if (_disposed || message == null)
                return;

            var pondId = message.PondId ?? string.Empty;
            var pondChanged = !string.Equals(pondId, _pondId, StringComparison.Ordinal);
            if (pondChanged)
            {
                _artLoad.Cancel();
                _artLoad.Dispose();
                _artLoad = new CancellationTokenSource();
                _artVersion++;
                _chromeBySpot.Clear();
                ClearSpots();
                ClearOthers();
                ClearDecor();
                _pondId = pondId;
                BackgroundPending = _decorPending = true;
                ScenePresentationChanged?.Invoke();
                _layout.TryLoad(pondId);
                TryLoadPondBackground(pondId);
                ApplyLayoutSprites();
                ApplyBackgroundArtSizeToLayout();
                RefreshContentSize(notify: true);
            }

            _ownPlayerId = message.OwnPlayerId ?? string.Empty;
            _ownUserId = message.OwnUserId ?? string.Empty;
            _lastMessage = message;
            if (_predictedSpotId != null &&
                (pondChanged ||
                 !string.IsNullOrWhiteSpace(message.OwnSpotId) ||
                 DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > _predictedUntilMs))
                _predictedSpotId = null;

            var geometry = _layout.IsActive ? _pondId : SpotGeometry(message.Spots);
            if (pondChanged || geometry != _spotGeometry)
            {
                SyncSpots(message);
                _spotGeometry = geometry;
            }
            else UpdateSpotStates(message);
            ApplyOwnActor(message);
            if (message.Users != null)
                SyncOthers(message);
            UpdateActorVisibility();
        }

        /// <summary>
        /// BUG-54: fishing phase timeline lives in Unity OwnFishingPresenter.
        /// Overlay only keeps one-frame seat click feedback.
        /// </summary>
        public void PredictOwnSeat(string spotId)
        {
            var message = _lastMessage;
            if (message == null || string.IsNullOrWhiteSpace(spotId) ||
                !string.IsNullOrWhiteSpace(message.OwnSpotId))
                return;
            _predictedSpotId = spotId;
            _predictedUntilMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + PredictedSeatTimeoutMs;
            UpdateSpotStates(message);
            ApplyOwnActor(message);
            RaiseLocalPresentationChanged();
        }

        public void CancelOwnSeatPrediction()
        {
            if (_predictedSpotId == null)
                return;
            _predictedSpotId = null;
            if (_lastMessage == null)
                return;
            UpdateSpotStates(_lastMessage);
            ApplyOwnActor(_lastMessage);
            RaiseLocalPresentationChanged();
        }

        void RaiseLocalPresentationChanged()
        {
            LocalPresentationChanged?.Invoke();
        }

        string EffectiveOwnSpotId(IpcMessage message)
        {
            return _predictedSpotId ?? message?.OwnSpotId;
        }

        void ApplyBackgroundArtSizeToLayout()
        {
            if (!_layout.IsActive || _layout.HasExplicitPondSize)
                return;
            var source = _backgroundImage?.Source as BitmapSource;
            if (source == null || source.PixelWidth < 1)
                return;
            _layout.ApplyArtPixelSize(source.PixelWidth, source.PixelHeight);
        }

        void RefreshContentSize(bool notify)
        {
            var width = _layout.IsActive ? _layout.SceneWidth : SceneWidth;
            var height = _layout.IsActive ? _layout.SceneHeight : SceneHeight;
            var changed = Math.Abs(width - ContentWidth) > 0.5 ||
                          Math.Abs(height - ContentHeight) > 0.5;
            ContentWidth = width;
            ContentHeight = height;
            ApplyBackgroundElementSize();
            if (notify || changed)
                SceneContentChanged?.Invoke();
        }

        void ApplyBackgroundElementSize()
        {
            if (_backgroundImage == null)
                return;
            _backgroundImage.Width = ContentWidth;
            _backgroundImage.Height = ContentHeight;
            _backgroundImage.Stretch = Stretch.Fill;
            if (_grass != null)
            {
                _grass.Width = ContentWidth;
                _grass.Height = ContentHeight;
            }
        }

        public void SetRuntimeViewport(double width, double height)
        {
            _layout.SetRuntimeViewport(width, height);
        }

        /// <summary>
        /// World X of the seat/pet to center in the viewport when entering a wide pond.
        /// </summary>
        public bool TryGetFocusWorldX(string ownSpotId, out double worldX)
        {
            worldX = 0;
            if (string.IsNullOrWhiteSpace(ownSpotId))
                return false;
            if (_layout.TryGetPetPoint(ownSpotId, out var pet))
            {
                worldX = pet.X;
                return true;
            }

            if (_layout.TryGetSpotPoint(ownSpotId, out var spot))
            {
                worldX = spot.X;
                return true;
            }

            return false;
        }

        void SyncSpots(IpcMessage message)
        {
            var keep = new HashSet<string>(StringComparer.Ordinal);
            var pondId = message.PondId ?? string.Empty;
            if (_layout.IsActive)
            {
                foreach (var pair in _layout.Spots)
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                        continue;
                    keep.Add(pair.Key);
                    var item = pair.Value;
                    OverlayLayoutObjectDto seatVisual;
                    if (!_layout.TryGetSeatVisual(pair.Key, out seatVisual) || seatVisual == null)
                        seatVisual = item;

                    var width = seatVisual.w > 0 ? seatVisual.w : (item.w > 0 ? item.w : 48);
                    var height = seatVisual.h > 0 ? seatVisual.h : (item.h > 0 ? item.h : 32);
                    var anchor = OverlayPondLayout.LayoutAnchor(seatVisual);
                    PlaceSpotMarker(
                        pair.Key,
                        seatVisual.x,
                        seatVisual.y,
                        width,
                        height,
                        anchor,
                        item,
                        seatVisual,
                        pondId);
                }
            }
            else
            {
                var spots = message.Spots;
                if (spots != null)
                {
                    foreach (var spot in spots)
                    {
                        if (spot == null || string.IsNullOrEmpty(spot.Id))
                            continue;
                        keep.Add(spot.Id);
                        var point = MapToScene(spot.X, spot.Y, spots);
                        PlaceSpotMarker(
                            spot.Id,
                            point.X,
                            point.Y,
                            48,
                            32,
                            "center",
                            null,
                            null,
                            pondId);
                    }
                }
            }

            RemoveMissingSpots(keep);
            UpdateSpotStates(message);
        }

        async void PlaceSpotMarker(
            string spotId,
            double x,
            double y,
            double width,
            double height,
            string anchor,
            OverlayLayoutObjectDto layoutSpot,
            OverlayLayoutObjectDto layoutSeat,
            string pondId)
        {
            if (!_spotVisuals.TryGetValue(spotId, out var marker))
            {
                marker = new OverlaySpotMarker(spotId);
                marker.SpotSelected += (sender, id) => SpotSelected?.Invoke(id);
                _spotLayer.Children.Add(marker);
                _spotVisuals[spotId] = marker;
            }

            marker.Width = width;
            marker.Height = height;
            OverlayPondLayout.ResolveRect(x, y, width, height, anchor, out var left, out var top);
            Canvas.SetLeft(marker, left);
            Canvas.SetTop(marker, top);

            var version = _artVersion;
            var token = _artLoad.Token;
            try
            {
                var seatArt = await OverlaySeatArt.LoadAsync(pondId, layoutSpot, layoutSeat, token);
                if (_disposed || version != _artVersion || !_spotVisuals.TryGetValue(spotId, out var current) || !ReferenceEquals(current, marker)) return;
                marker.SetSeatArt(seatArt, seatArt == null);
            }
            catch (OperationCanceledException) { }
        }

        static bool IsGroundbaitActive(IpcMessage message)
        {
            if (message == null)
                return false;
            if (string.Equals(message.FishingPhase, "groundbaiting", StringComparison.Ordinal))
                return true;
            if (message.GroundbaitStack > 0)
                return true;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return message.GroundbaitExpiresAt > now;
        }

        static HashSet<string> CollectOccupiedSpots(IpcMessage message, string ownSpotId)
        {
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            if (message == null)
                return occupied;

            if (!string.IsNullOrWhiteSpace(ownSpotId))
                occupied.Add(ownSpotId);

            if (message.Users != null)
            {
                foreach (var user in message.Users)
                {
                    if (user != null && !string.IsNullOrWhiteSpace(user.SpotId))
                        occupied.Add(user.SpotId);
                }
            }

            return occupied;
        }

        void UpdateSpotStates(IpcMessage message)
        {
            var ownSpotId = EffectiveOwnSpotId(message);
            var ownHasSpot = !string.IsNullOrWhiteSpace(ownSpotId);
            var occupied = CollectOccupiedSpots(message, ownSpotId);
            foreach (var pair in _spotVisuals)
                pair.Value.ApplyState(ownHasSpot, occupied.Contains(pair.Key));
        }

        void RemoveMissingSpots(HashSet<string> keep)
        {
            var remove = new List<string>();
            foreach (var id in _spotVisuals.Keys)
            {
                if (!keep.Contains(id))
                    remove.Add(id);
            }

            foreach (var id in remove)
            {
                _spotLayer.Children.Remove(_spotVisuals[id]);
                _spotVisuals.Remove(id);
            }
        }

        void EnsureOwnActor()
        {
            if (_ownActor != null)
                return;
            _ownActor = new OverlayPetActor(OwnActorKey, _hoverPresenter);
            _ownActor.WaterMarkerPublished += ForwardWaterMarker;
            _ownActor.WaterGeometryChanged += UpdateActorVisibility;
            _ownActor.AudioMarkerPublished += marker => AudioMarkerPublished?.Invoke(marker);
            _ownActor.SetPresentationVisible(_windowVisible);
            _ownActor.SetGroundbaitChromeShown(_actionChromeShown);
            _actorLayer.Children.Add(_ownActor);
        }

        void ApplyOwnActor(IpcMessage message)
        {
            EnsureOwnActor();

            var predicted = _predictedSpotId != null;
            var ownSpotId = predicted ? _predictedSpotId : message.OwnSpotId;
            Point point;
            if (!TryResolveActorPoint(ownSpotId, !predicted && message.HasOwnPosition, message.OwnX, message.OwnY, message.Spots, 0, out point))
                point = FallbackOwnPoint();
            _ownActor.ApplyScenePlacement(ownSpotId, ActorChrome(ownSpotId), point, message.OwnPetId);
            _ownActor.SetAudioPresentationVisible(_windowVisible && (_viewport.IsEmpty || _ownActor.PresentationBounds.IntersectsWith(_viewport)));
            var audio = message.AudioPresentation;
            if (audio != null && (audio.ActorId != message.OwnPlayerId && audio.ActorId != message.OwnUserId ||
                audio.PondId != message.PondId || audio.SpotId != message.OwnSpotId)) audio = null;
            _ownActor.ConfigureAudioPresentation(audio, message.FishingPhase, message.OwnPetId);
            _ownActor.WarmupActionClips(message.OwnPetId);
            var ownEnvelope = message.Presentation;
            if (ownEnvelope != null && (ownEnvelope.PondId != message.PondId || ownEnvelope.SpotId != message.OwnSpotId ||
                ownEnvelope.ActorId != message.OwnPlayerId && ownEnvelope.ActorId != message.OwnUserId)) ownEnvelope = null;
            _ownActor.SetFishingPresentation(predicted ? null : ownEnvelope, true, message.FishingEffectsQuality);
            _ownActor.SetGroundbait(message, !predicted && !string.IsNullOrEmpty(ownSpotId));
            _ownActor.Apply(
                message.OwnNickname,
                predicted ? "sit" : message.PetVisualState,
                predicted ? "seated" : message.FishingPhase,
                message.SessionFishingMs,
                message.HookDeadlineMs,
                message.OwnFishingStartedAt,
                message.OwnPetId,
                message.SessionCatchCount,
                IsGroundbaitActive(message),
                !predicted && message.FishingAsleep);
        }

        void SyncOthers(IpcMessage message)
        {
            var keep = new HashSet<string>(StringComparer.Ordinal);
            _actorsByUserId.Clear();
            var users = message.Users;
            for (var i = 0; i < users.Length; i++)
            {
                var user = users[i];
                if (user == null)
                    continue;
                var key = !string.IsNullOrEmpty(user.PlayerId) ? user.PlayerId : user.UserId;
                if (string.IsNullOrEmpty(key))
                    continue;
                keep.Add(key);
                var menuPlayerId = !string.IsNullOrWhiteSpace(user.PlayerId)
                    ? user.PlayerId
                    : user.UserId;
                if (!_others.TryGetValue(key, out var actor))
                {
                    actor = new OverlayPetActor(key, _hoverPresenter);
                    actor.WaterMarkerPublished += ForwardWaterMarker;
                    actor.WaterGeometryChanged += UpdateActorVisibility;
                    actor.SetPresentationVisible(_windowVisible);
                    actor.ConfigurePlayerMenu(
                        menuPlayerId,
                        user.IsBot,
                        _menuHost);
                    _actorLayer.Children.Add(actor);
                    _others[key] = actor;
                    actor.SetNameplateShown(_otherNameplatesShown);
                    actor.SetGroundbaitChromeShown(_actionChromeShown);
                }
                else
                {
                    actor.ConfigurePlayerMenu(
                        menuPlayerId,
                        user.IsBot,
                        _menuHost);
                }

                if (!string.IsNullOrEmpty(user.UserId))
                    _actorsByUserId[user.UserId] = actor;

                Point point;
                if (!TryResolveActorPoint(user.SpotId, user.HasPosition, user.X, user.Y, message.Spots, keep.Count - 1, out point))
                    point = WaitingPoint(keep.Count - 1);
                actor.ApplyScenePlacement(user.SpotId, ActorChrome(user.SpotId), point, user.PetId);
                var envelope = user.Presentation;
                if (envelope != null && (envelope.PondId != message.PondId || envelope.SpotId != user.SpotId ||
                    envelope.ActorId != user.PlayerId && envelope.ActorId != user.UserId)) envelope = null;
                actor.SetFishingPresentation(envelope, false, message.FishingEffectsQuality);
                actor.Apply(
                    user.Nickname,
                    user.PetVisualState,
                    user.FishingPhase,
                    user.SessionFishingMs,
                    user.HookDeadlineMs,
                    user.FishingStartedAt,
                    user.PetId,
                    user.SessionCatchCount,
                    string.Equals(user.FishingPhase, "groundbaiting", StringComparison.Ordinal),
                    user.FishingAsleep);
            }

            var remove = new List<string>();
            foreach (var key in _others.Keys)
            {
                if (!keep.Contains(key))
                    remove.Add(key);
            }

            foreach (var key in remove)
            {
                _others[key].Dispose();
                _hoverPresenter.RemoveActor(key);
                _actorLayer.Children.Remove(_others[key]);
                _others.Remove(key);
            }
        }

        void ClearOthers()
        {
            CancelAllHovers();
            _actorsByUserId.Clear();
            if (_ownActor != null)
            {
                _ownActor.Dispose();
                _hoverPresenter.RemoveActor(OwnActorKey);
                _actorLayer.Children.Remove(_ownActor);
                _ownActor = null;
            }
            foreach (var key in new List<string>(_others.Keys))
            {
                _others[key].Dispose();
                _hoverPresenter.RemoveActor(key);
                _actorLayer.Children.Remove(_others[key]);
                _others.Remove(key);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _artVersion++;
            BackgroundPending = false;
            _artLoad.Cancel();
            _artLoad.Dispose();
            ClearOthers();
            ClearSpots();
            ClearDecor();
            _lastMessage = null;
            _backgroundImage.Source = null;
            SpotSelected = null;
            SceneContentChanged = null;
            LocalPresentationChanged = null;
        }

        void ClearDecor()
        {
            _sceneMotion?.Dispose();
            _sceneMotion = null;
            if (_decorLayer != null)
                _decorLayer.Children.Clear();
        }

        async void ApplyLayoutSprites()
        {
            ClearDecor();
            if (!_layout.IsActive || _decorLayer == null)
            {
                _decorPending = false;
                ScenePresentationChanged?.Invoke();
                return;
            }

            var resources = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "OverlayResources");
            var sprites = new List<OverlayLayoutObjectDto>(_layout.Sprites);
            sprites.Sort((a, b) =>
            {
                var az = a == null ? 0 : a.z;
                var bz = b == null ? 0 : b.z;
                return az.CompareTo(bz);
            });
            var version = _artVersion;
            var token = _artLoad.Token;
            try
            {
                // Publish the complete decoration group together, in saved z-order.
                var byId = new Dictionary<string, Image>();
                foreach (var sprite in sprites)
                {
                    if (IsLegacyFullCanvasPond(sprite)) continue;
                    var image = await _layout.CreateSpriteImageAsync(sprite, resources, token);
                    if (image != null) byId[sprite.id] = image;
                }
                if (_disposed || version != _artVersion) return;
                OverlaySceneMotion motion = null;
                try { motion = await OverlaySceneMotion.TryCreateAsync(resources, _layout.BackgroundSprite, byId, token); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[SceneMotion] Static fallback: " + ex.Message); }
                if (_disposed || version != _artVersion) { motion?.Dispose(); return; }
                foreach (var sprite in sprites)
                    if (sprite != null && byId.TryGetValue(sprite.id, out var image))
                        _decorLayer.Children.Add(motion?.Element(sprite.id, image) ?? image);
                _sceneMotion = motion;
                _sceneMotion?.SetVisible(_windowVisible);
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (version == _artVersion)
                {
                    _decorPending = false;
                    ScenePresentationChanged?.Invoke();
                }
            }
        }

        static bool IsLegacyFullCanvasPond(OverlayLayoutObjectDto item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.sprite))
                return false;
            if (string.Equals(item.id, "pond-bg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.kind, "background", StringComparison.OrdinalIgnoreCase))
                return true;
            var name = System.IO.Path.GetFileName(item.sprite);
            return string.Equals(name, "pond.png", StringComparison.OrdinalIgnoreCase) &&
                   item.w >= 900 &&
                   item.h >= 500;
        }

        bool TryResolveActorPoint(
            string spotId,
            bool hasWorldPosition,
            float worldX,
            float worldY,
            OverlaySpotDto[] spots,
            int waitingIndex,
            out Point point)
        {
            if (_layout.IsActive)
            {
                if (_layout.TryGetPetPoint(spotId, out var petPoint))
                {
                    point = petPoint;
                    return true;
                }

                if (_layout.TryGetSpotPoint(spotId, out var layoutPoint))
                {
                    point = layoutPoint;
                    return true;
                }

                if (!string.IsNullOrEmpty(spotId))
                    System.Diagnostics.Debug.WriteLine(
                        "[OverlayLayout] Unknown spotId, skip world mapping: " + spotId);
                point = WaitingPoint(waitingIndex);
                return true;
            }

            if (hasWorldPosition)
            {
                point = MapToScene(worldX, worldY, spots);
                return true;
            }

            if (TryFindSpot(spots, spotId, out var sx, out var sy))
            {
                point = MapToScene(sx, sy, spots);
                return true;
            }

            point = new Point();
            return false;
        }

        Point WaitingPoint(int index)
        {
            return _layout.IsActive ? _layout.WaitingLane(index) : WaitingLane(index);
        }

        static Point FallbackOwnPoint()
        {
            return new Point(SceneWidth / 2, SceneHeight - CatSize / 2 - 32);
        }

        void ClearSpots()
        {
            _spotLayer.Children.Clear();
            _spotVisuals.Clear();
        }

        async void TryLoadPondBackground(string pondId)
        {
            var version = _artVersion;
            var token = _artLoad.Token;
            var resources = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OverlayResources");
            var sprite = _layout.BackgroundSprite;
            BackgroundPending = true;
            // Do not show the previous pond beneath the new pond's seats and actors.
            _backgroundImage.Source = null;
            _backgroundImage.Visibility = Visibility.Collapsed;
            _grass.Visibility = Visibility.Collapsed;
            _shore.Visibility = Visibility.Collapsed;
            _water.Visibility = Visibility.Collapsed;
            try
            {
                if (string.IsNullOrEmpty(pondId)) return;
                var path = await OverlayStaticArt.ResolveAsync(() => PondBackgroundResources.Resolve(resources, pondId, sprite), token);
                var art = await OverlayStaticArt.LoadAsync(path, true, token);
                if (_disposed || version != _artVersion) return;
                if (art?.Bitmap == null)
                {
                    // A real missing resource may use fallback art; loading is
                    // not a failure and must never flash this placeholder.
                    _grass.Visibility = _shore.Visibility = _water.Visibility = Visibility.Visible;
                    return;
                }
                // Alpha map was registered before Source becomes hittable.
                _backgroundImage.Source = art.Bitmap;
                _backgroundImage.Visibility = Visibility.Visible;
                _grass.Visibility = Visibility.Collapsed;
                _shore.Visibility = Visibility.Collapsed;
                _water.Visibility = Visibility.Collapsed;
                var oldWidth = ContentWidth;
                var oldHeight = ContentHeight;
                ApplyBackgroundArtSizeToLayout();
                RefreshContentSize(notify: false);
                if (_lastMessage != null && string.Equals(_lastMessage.PondId ?? string.Empty, pondId, StringComparison.Ordinal) &&
                    (oldWidth != ContentWidth || oldHeight != ContentHeight))
                {
                    _chromeBySpot.Clear();
                    SyncSpots(_lastMessage);
                    ApplyOwnActor(_lastMessage);
                    if (_lastMessage.Users != null) SyncOthers(_lastMessage);
                    UpdateActorVisibility();
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                if (version == _artVersion)
                {
                    BackgroundPending = false;
                    ScenePresentationChanged?.Invoke();
                }
            }
        }

        static bool TryFindSpot(
            OverlaySpotDto[] spots, string spotId, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (spots == null || string.IsNullOrEmpty(spotId))
                return false;
            foreach (var spot in spots)
            {
                if (spot != null && spot.Id == spotId)
                {
                    x = spot.X;
                    y = spot.Y;
                    return true;
                }
            }

            return false;
        }

        static Point WaitingLane(int index)
        {
            var col = index % 6;
            var row = index / 6;
            return new Point(96 + col * 140, 72 + row * 44);
        }

        static Point MapToScene(float worldX, float worldY, OverlaySpotDto[] spots)
        {
            GetWorldBounds(spots, out var minX, out var minY, out var maxX, out var maxY);
            var worldW = Math.Max(1, maxX - minX);
            var worldH = Math.Max(1, maxY - minY);
            var margin = CatSize / 2 + 16;
            var usableW = SceneWidth - margin * 2;
            var usableH = SceneHeight - margin * 2;
            var scale = Math.Min(usableW / worldW, usableH / worldH);
            var mappedW = worldW * scale;
            var mappedH = worldH * scale;
            var originX = (SceneWidth - mappedW) / 2;
            var originY = (SceneHeight - mappedH) / 2;
            return new Point(
                originX + (worldX - minX) * scale,
                originY + (worldY - minY) * scale);
        }

        static void GetWorldBounds(
            OverlaySpotDto[] spots,
            out float minX,
            out float minY,
            out float maxX,
            out float maxY)
        {
            minX = 0;
            minY = 0;
            maxX = 896;
            maxY = 896;
            if (spots == null || spots.Length == 0)
                return;

            minX = float.MaxValue;
            minY = float.MaxValue;
            maxX = float.MinValue;
            maxY = float.MinValue;
            foreach (var spot in spots)
            {
                if (spot == null)
                    continue;
                minX = Math.Min(minX, spot.X);
                minY = Math.Min(minY, spot.Y);
                maxX = Math.Max(maxX, spot.X);
                maxY = Math.Max(maxY, spot.Y);
            }

            if (maxX - minX < 32)
            {
                minX -= 64;
                maxX += 64;
            }

            if (maxY - minY < 32)
            {
                minY -= 64;
                maxY += 64;
            }
        }
    }
}
