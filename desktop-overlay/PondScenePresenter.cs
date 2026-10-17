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
    /// Renders pond water, spots and pond pets from Unity-pushed snapshot fields.
    /// </summary>
    public sealed class PondScenePresenter
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
        const long PredictedSeatTimeoutMs = 3000;
        const long PredictedActionTimeoutMs = 5000;
        const long HookExpiryGraceMs = 120;
        // Match server/src/fishingStateMachine.ts PHASE_MS.
        const int LocalBaitingMs = 800;
        const int LocalCastingMs = 600;
        const int LocalStoppingMs = 250;
        const int LocalGroundbaitMs = 1500;
        IpcMessage _lastMessage;
        string _predictedSpotId;
        long _predictedUntilMs;
        string _actionVisual;
        string _actionPhase;
        bool _actionGroundbait;
        bool _actionLeavesSpot;
        string _actionBaseline;
        long _actionUntilMs;
        string _localSequence;
        int _localSequenceStep;
        DispatcherTimer _hookExpiryTimer;
        DispatcherTimer _localPhaseTimer;
        string _pondId = string.Empty;
        string _ownPlayerId = string.Empty;
        string _ownUserId = string.Empty;
        readonly Dictionary<string, OverlayPetActor> _actorsByUserId =
            new Dictionary<string, OverlayPetActor>(StringComparer.Ordinal);
        public event Action<string> SpotSelected;
        /// <summary>Fired when pond layout / content size changes (STEAM-DESKTOP-14B).</summary>
        public event Action SceneContentChanged;
        /// <summary>Fired when local seat/action prediction changes HUD-relevant phase.</summary>
        public event Action LocalPresentationChanged;

        public bool HasPredictedSeat => _predictedSpotId != null;

        public bool HasActionPrediction => _actionBaseline != null;

        public bool EffectiveHasOwnSpot =>
            !string.IsNullOrWhiteSpace(EffectiveOwnSpotId(_lastMessage));

        public string EffectiveOwnPhase
        {
            get
            {
                if (_actionBaseline != null && !string.IsNullOrEmpty(_actionPhase))
                    return _actionPhase;
                if (_predictedSpotId != null)
                    return "seated";
                return _lastMessage?.FishingPhase ?? string.Empty;
            }
        }

        public bool IsActionSequenceBusy =>
            _actionBaseline != null &&
            (_localSequence == "start_fishing" ||
             _localSequence == "stop_fishing" ||
             _localSequence == "groundbait_start" ||
             _localSequence == "leave_spot" ||
             _localSequence == "force_bite");

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
            TryLoadPondBackground(string.Empty);
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
            if (message == null)
                return;

            var pondId = message.PondId ?? string.Empty;
            var pondChanged = !string.Equals(pondId, _pondId, StringComparison.Ordinal);
            if (pondChanged)
            {
                ClearSpots();
                ClearOthers();
                ClearDecor();
                _pondId = pondId;
                TryLoadPondBackground(pondId);
                _layout.TryLoad(pondId);
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
            if (pondChanged)
                ClearActionPrediction();
            else if (_actionBaseline != null && ShouldClearActionPrediction(message))
                ClearActionPrediction();

            SyncSpots(message);
            ApplyOwnActor(message);
            if (message.Users != null)
                SyncOthers(message);
            UpdateHookExpiryTimer(message);
        }

        /// <summary>
        /// Shows the expected own-pet animation before the server confirms;
        /// local phase timeline keeps bait→cast→fishing and stop→seated smooth.
        /// </summary>
        public bool PredictOwnAction(string command)
        {
            var message = _lastMessage;
            if (message == null || string.IsNullOrEmpty(command))
                return false;
            if (IsDuplicateAction(command))
                return false;
            var phase = EffectiveOwnPhase;
            var hasSpot = !string.IsNullOrWhiteSpace(EffectiveOwnSpotId(message));
            switch (command)
            {
                case "start_fishing":
                    if (!hasSpot || (phase != "seated" && phase != "idle" && phase.Length > 0))
                        return false;
                    SetActionPrediction(message, "bait", "baiting", false, false, "start_fishing");
                    return true;
                case "stop_fishing":
                    if (phase == "hooked")
                        SetActionPrediction(message, "reel_empty", "resolving", false, false, "stop_fishing");
                    else if (phase == "baiting" || phase == "casting" || phase == "waiting" ||
                             phase == "groundbaiting")
                        SetActionPrediction(message, "reel_empty", "stopping", false, false, "stop_fishing");
                    else
                        return false;
                    return true;
                case "groundbait_start":
                    if (!hasSpot || phase == "groundbaiting")
                        return false;
                    SetActionPrediction(message, "sit", "groundbaiting", true, false, "groundbait_start");
                    return true;
                case "leave_spot":
                    if (!hasSpot)
                        return false;
                    _predictedSpotId = null;
                    SetActionPrediction(message, "idle", "idle", false, true, "leave_spot");
                    return true;
                case "force_bite":
                    if (phase != "baiting" && phase != "casting" && phase != "waiting")
                        return false;
                    SetActionPrediction(message, "hooked", "hooked", false, false, "force_bite");
                    return true;
                default:
                    return false;
            }
        }

        bool IsDuplicateAction(string command)
        {
            if (_actionBaseline == null || string.IsNullOrEmpty(_localSequence))
                return false;
            if (!string.Equals(_localSequence, command, StringComparison.Ordinal))
            {
                // Allow stop to interrupt an in-flight start sequence.
                if (command == "stop_fishing" && _localSequence == "start_fishing")
                    return false;
                return true;
            }
            return true;
        }

        public void CancelOwnActionPrediction()
        {
            if (_actionBaseline == null)
                return;
            ClearActionPrediction();
            if (_lastMessage == null)
                return;
            UpdateSpotStates(_lastMessage);
            ApplyOwnActor(_lastMessage);
            RaiseLocalPresentationChanged();
        }

        void SetActionPrediction(
            IpcMessage message,
            string visual,
            string phase,
            bool groundbait,
            bool leavesSpot,
            string sequence)
        {
            StopLocalPhaseTimer();
            _actionVisual = visual;
            _actionPhase = phase;
            _actionGroundbait = groundbait;
            _actionLeavesSpot = leavesSpot;
            _actionBaseline = OwnActionSignature(message);
            _actionUntilMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + PredictedActionTimeoutMs;
            _localSequence = sequence ?? string.Empty;
            _localSequenceStep = 0;
            EnsureOwnActor();
            _ownActor.WarmupActionClips(message.OwnPetId);
            UpdateSpotStates(message);
            ApplyOwnActor(message);
            LatencyTrace.Write(
                "overlay_action_visual commandPhase=" + phase + " visual=" + visual);
            BeginLocalSequence(sequence);
            RaiseLocalPresentationChanged();
        }

        void AdvanceActionPrediction(string visual, string phase, bool groundbait, bool leavesSpot)
        {
            if (_actionBaseline == null || _lastMessage == null)
                return;
            _actionVisual = visual;
            _actionPhase = phase;
            _actionGroundbait = groundbait;
            _actionLeavesSpot = leavesSpot;
            UpdateSpotStates(_lastMessage);
            ApplyOwnActor(_lastMessage);
            LatencyTrace.Write(
                "overlay_action_visual_advance commandPhase=" + phase + " visual=" + visual);
            RaiseLocalPresentationChanged();
        }

        void BeginLocalSequence(string sequence)
        {
            if (sequence == "start_fishing")
                ScheduleLocalPhase(LocalBaitingMs);
            else if (sequence == "stop_fishing")
                ScheduleLocalPhase(LocalStoppingMs);
            else if (sequence == "groundbait_start")
                ScheduleLocalPhase(LocalGroundbaitMs);
        }

        void ScheduleLocalPhase(int delayMs)
        {
            if (_localPhaseTimer == null)
            {
                _localPhaseTimer = new DispatcherTimer(DispatcherPriority.Normal);
                _localPhaseTimer.Tick += OnLocalPhaseTick;
            }
            _localPhaseTimer.Stop();
            _localPhaseTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(16, delayMs));
            _localPhaseTimer.Start();
        }

        void StopLocalPhaseTimer()
        {
            _localPhaseTimer?.Stop();
            _localSequenceStep = 0;
        }

        void OnLocalPhaseTick(object sender, EventArgs e)
        {
            _localPhaseTimer.Stop();
            if (_actionBaseline == null || string.IsNullOrEmpty(_localSequence))
                return;
            if (_localSequence == "start_fishing")
            {
                if (_localSequenceStep == 0)
                {
                    _localSequenceStep = 1;
                    AdvanceActionPrediction("cast", "casting", false, false);
                    ScheduleLocalPhase(LocalCastingMs);
                    return;
                }
                if (_localSequenceStep == 1)
                {
                    _localSequenceStep = 2;
                    AdvanceActionPrediction("fishing", "waiting", false, false);
                    // Keep prediction until server reaches waiting; no further ticks.
                }
                return;
            }
            if (_localSequence == "stop_fishing")
            {
                // Move HUD to seated immediately; reel clip defers sit visually.
                AdvanceActionPrediction("sit", "seated", false, false);
                _localSequence = string.Empty;
                return;
            }
            if (_localSequence == "groundbait_start")
            {
                AdvanceActionPrediction("sit", "seated", true, false);
                _localSequence = string.Empty;
            }
        }

        void EnsureOwnActor()
        {
            if (_ownActor != null)
                return;
            _ownActor = new OverlayPetActor(OwnActorKey, _hoverPresenter);
            _actorLayer.Children.Add(_ownActor);
        }

        void ClearActionPrediction()
        {
            StopLocalPhaseTimer();
            _localSequence = string.Empty;
            _actionBaseline = null;
            _actionVisual = null;
            _actionPhase = null;
            _actionGroundbait = false;
            _actionLeavesSpot = false;
        }

        void RaiseLocalPresentationChanged()
        {
            LocalPresentationChanged?.Invoke();
        }

        bool ShouldClearActionPrediction(IpcMessage message)
        {
            if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > _actionUntilMs)
                return true;
            var server = message.FishingPhase ?? string.Empty;
            var predicted = _actionPhase ?? string.Empty;
            if (_actionLeavesSpot)
            {
                return string.IsNullOrWhiteSpace(message.OwnSpotId) ||
                       string.Equals(server, "idle", StringComparison.Ordinal);
            }
            if (string.Equals(server, predicted, StringComparison.Ordinal))
                return true;
            // Ack / state flush before the server phase moves — keep prediction.
            if (string.Equals(OwnActionSignature(message), _actionBaseline, StringComparison.Ordinal))
                return false;

            var sRank = PhaseRank(server);
            var pRank = PhaseRank(predicted);
            // Local start sequence ahead of server (bait→cast→waiting).
            if (pRank >= 10 && pRank <= 12 && sRank >= 1 && sRank < pRank)
                return false;
            // Local stop already seated while server still winding down.
            if (string.Equals(predicted, "seated", StringComparison.Ordinal) &&
                (server == "stopping" || server == "resolving" || sRank >= 10))
                return false;
            if ((predicted == "stopping" || predicted == "resolving") &&
                sRank >= 10 && sRank <= 20)
                return false;
            if (predicted == "hooked" && server == "waiting")
                return false;
            if (predicted == "groundbaiting" && server == "seated")
                return false;
            return true;
        }

        static int PhaseRank(string phase)
        {
            switch (phase)
            {
                case "idle": return 0;
                case "seated": return 1;
                case "groundbaiting": return 2;
                case "baiting": return 10;
                case "casting": return 11;
                case "waiting": return 12;
                case "stopping": return 15;
                case "hooked": return 20;
                case "resolving": return 21;
                default: return -1;
            }
        }

        static string OwnActionSignature(IpcMessage message)
        {
            return (message.FishingPhase ?? string.Empty) + "|" +
                   (message.PetVisualState ?? string.Empty) + "|" +
                   (message.OwnSpotId ?? string.Empty) + "|" +
                   (IsGroundbaitActive(message) ? "1" : "0");
        }

        void UpdateHookExpiryTimer(IpcMessage message)
        {
            var armed = _actionBaseline == null &&
                        string.Equals(message.FishingPhase, "hooked", StringComparison.Ordinal) &&
                        message.HookDeadlineMs > 0;
            if (!armed)
            {
                _hookExpiryTimer?.Stop();
                return;
            }
            if (_hookExpiryTimer == null)
            {
                _hookExpiryTimer = new DispatcherTimer(DispatcherPriority.Normal)
                {
                    Interval = TimeSpan.FromMilliseconds(50),
                };
                _hookExpiryTimer.Tick += OnHookExpiryTick;
            }
            if (!_hookExpiryTimer.IsEnabled)
                _hookExpiryTimer.Start();
        }

        void OnHookExpiryTick(object sender, EventArgs e)
        {
            var message = _lastMessage;
            if (message == null || _actionBaseline != null ||
                !string.Equals(message.FishingPhase, "hooked", StringComparison.Ordinal) ||
                message.HookDeadlineMs <= 0)
            {
                _hookExpiryTimer.Stop();
                return;
            }
            if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < message.HookDeadlineMs + HookExpiryGraceMs)
                return;
            _hookExpiryTimer.Stop();
            LatencyTrace.Write("overlay_hook_expired_predicted deadline=" + message.HookDeadlineMs);
            SetActionPrediction(message, "reel", "resolving", false, false, "stop_fishing");
        }

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

        string EffectiveOwnSpotId(IpcMessage message)
        {
            if (_actionLeavesSpot)
                return null;
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

        void PlaceSpotMarker(
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

            var seatArt = OverlaySeatArt.TryLoad(pondId, layoutSpot, layoutSeat, out var usedFallback);
            marker.SetSeatArt(seatArt, usedFallback);
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
            if (_spotVisuals.Count == keep.Count)
                return;

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

        void ApplyOwnActor(IpcMessage message)
        {
            EnsureOwnActor();

            var predicted = _predictedSpotId != null;
            var action = _actionBaseline != null;
            var ownSpotId = predicted ? _predictedSpotId : message.OwnSpotId;
            Point point;
            if (!TryResolveActorPoint(ownSpotId, !predicted && message.HasOwnPosition, message.OwnX, message.OwnY, message.Spots, 0, out point))
                point = FallbackOwnPoint();
            var visual = action ? _actionVisual : predicted ? "sit" : message.PetVisualState;
            var phase = action ? _actionPhase : predicted ? "seated" : message.FishingPhase;
            var hookDeadline = message.HookDeadlineMs;
            if (action)
            {
                if (string.Equals(_actionPhase, "hooked", StringComparison.Ordinal) &&
                    hookDeadline <= 0)
                    hookDeadline = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 5000;
                else if (!string.Equals(_actionPhase, message.FishingPhase, StringComparison.Ordinal) &&
                         !string.Equals(_actionPhase, "hooked", StringComparison.Ordinal))
                    hookDeadline = 0;
            }
            _ownActor.WarmupActionClips(message.OwnPetId);
            _ownActor.Apply(
                message.OwnNickname,
                visual,
                phase,
                message.SessionFishingMs,
                hookDeadline,
                message.OwnFishingStartedAt,
                message.OwnPetId,
                message.SessionCatchCount,
                (action && _actionGroundbait) || IsGroundbaitActive(message));
            _ownActor.ApplyChrome(_layout.ResolveActorChrome(_actionLeavesSpot ? null : ownSpotId));
            _ownActor.Place(point.X, point.Y);
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
                    actor.ConfigurePlayerMenu(
                        menuPlayerId,
                        user.IsBot,
                        _menuHost);
                    _actorLayer.Children.Add(actor);
                    _others[key] = actor;
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
                actor.Apply(
                    user.Nickname,
                    user.PetVisualState,
                    user.FishingPhase,
                    user.SessionFishingMs,
                    user.HookDeadlineMs,
                    user.FishingStartedAt,
                    user.PetId,
                    user.SessionCatchCount,
                    string.Equals(user.FishingPhase, "groundbaiting", StringComparison.Ordinal));
                actor.ApplyChrome(_layout.ResolveActorChrome(user.SpotId));
                actor.Place(point.X, point.Y);
            }

            var remove = new List<string>();
            foreach (var key in _others.Keys)
            {
                if (!keep.Contains(key))
                    remove.Add(key);
            }

            foreach (var key in remove)
            {
                _others[key].CancelTooltip();
                _others[key].ReleaseFishingLayers();
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
                _ownActor.ReleaseFishingLayers();
                _hoverPresenter.RemoveActor(OwnActorKey);
                _actorLayer.Children.Remove(_ownActor);
                _ownActor = null;
            }
            foreach (var key in new List<string>(_others.Keys))
            {
                _others[key].ReleaseFishingLayers();
                _hoverPresenter.RemoveActor(key);
                _actorLayer.Children.Remove(_others[key]);
                _others.Remove(key);
            }
        }

        void ClearDecor()
        {
            if (_decorLayer != null)
                _decorLayer.Children.Clear();
        }

        void ApplyLayoutSprites()
        {
            ClearDecor();
            if (!_layout.IsActive || _decorLayer == null)
                return;

            var resources = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "OverlayResources");
            var sprites = new List<OverlayLayoutObjectDto>(_layout.Sprites);
            sprites.Sort((a, b) =>
            {
                var az = a == null ? 0 : a.z;
                var bz = b == null ? 0 : b.z;
                return az.CompareTo(bz);
            });
            foreach (var sprite in sprites)
            {
                if (IsLegacyFullCanvasPond(sprite))
                    continue;
                var image = _layout.CreateSpriteImage(sprite, resources);
                if (image != null)
                    _decorLayer.Children.Add(image);
            }

            _grass.Visibility = Visibility.Collapsed;
            _shore.Visibility = Visibility.Collapsed;
            _water.Visibility = Visibility.Collapsed;
        }

        static bool IsLegacyFullCanvasPond(OverlayLayoutObjectDto item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.sprite))
                return false;
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

        void TryLoadPondBackground(string pondId)
        {
            var root = AppDomain.CurrentDomain.BaseDirectory;
            var resources = System.IO.Path.Combine(root, "OverlayResources");
            var path = ResolvePondBackgroundPath(resources, pondId);
            if (string.IsNullOrEmpty(path))
            {
                _backgroundImage.Source = null;
                _backgroundImage.Visibility = Visibility.Collapsed;
                _grass.Visibility = Visibility.Visible;
                _shore.Visibility = Visibility.Visible;
                _water.Visibility = Visibility.Visible;
                return;
            }

            _backgroundImage.Source = LoadBitmap(path);
            _backgroundImage.Stretch = Stretch.Fill;
            _backgroundImage.Width = ContentWidth;
            _backgroundImage.Height = ContentHeight;
            _backgroundImage.Visibility = Visibility.Visible;
            _grass.Visibility = Visibility.Collapsed;
            _shore.Visibility = Visibility.Collapsed;
            _water.Visibility = Visibility.Collapsed;
        }

        static string ResolvePondBackgroundPath(string resourcesRoot, string pondId)
        {
            if (!string.IsNullOrWhiteSpace(pondId))
            {
                var perPond = System.IO.Path.Combine(resourcesRoot, "ponds", pondId + ".png");
                if (File.Exists(perPond))
                    return perPond;
            }

            var fallbackDefault = System.IO.Path.Combine(resourcesRoot, "ponds", "_default.png");
            if (File.Exists(fallbackDefault))
                return fallbackDefault;

            var legacy = System.IO.Path.Combine(resourcesRoot, "pond.png");
            if (File.Exists(legacy))
                return legacy;

            return null;
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

        static BitmapImage LoadBitmap(string path)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
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
