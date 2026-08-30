using System;
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
        string _pondId = string.Empty;
        string _ownPlayerId = string.Empty;
        string _ownUserId = string.Empty;
        readonly Dictionary<string, OverlayPetActor> _actorsByUserId =
            new Dictionary<string, OverlayPetActor>(StringComparer.Ordinal);
        public event Action<string> SpotSelected;
        /// <summary>Fired when pond layout / content size changes (STEAM-DESKTOP-14B).</summary>
        public event Action SceneContentChanged;

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

            SyncSpots(message);
            ApplyOwnActor(message);
            if (message.Users != null)
                SyncOthers(message);
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

        static HashSet<string> CollectOccupiedSpots(IpcMessage message)
        {
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            if (message == null)
                return occupied;

            if (!string.IsNullOrWhiteSpace(message.OwnSpotId))
                occupied.Add(message.OwnSpotId);

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
            var ownHasSpot = !string.IsNullOrWhiteSpace(message?.OwnSpotId);
            var occupied = CollectOccupiedSpots(message);
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
            if (_ownActor == null)
            {
                _ownActor = new OverlayPetActor(OwnActorKey, _hoverPresenter);
                _actorLayer.Children.Add(_ownActor);
            }

            Point point;
            if (!TryResolveActorPoint(message.OwnSpotId, message.HasOwnPosition, message.OwnX, message.OwnY, message.Spots, 0, out point))
                point = FallbackOwnPoint();
            _ownActor.Apply(
                message.OwnNickname,
                message.PetVisualState,
                message.FishingPhase,
                message.SessionFishingMs,
                message.HookDeadlineMs,
                message.OwnFishingStartedAt,
                message.OwnPetId);
            OverlayActorChrome ownChrome;
            if (_layout.TryGetActorChrome(message.OwnSpotId, out ownChrome))
                _ownActor.ApplyChrome(ownChrome);
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
                    user.PetId);
                OverlayActorChrome chrome;
                if (_layout.TryGetActorChrome(user.SpotId, out chrome))
                    actor.ApplyChrome(chrome);
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
                _hoverPresenter.RemoveActor(OwnActorKey);
                _actorLayer.Children.Remove(_ownActor);
                _ownActor = null;
            }
            foreach (var key in new List<string>(_others.Keys))
            {
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
