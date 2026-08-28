using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Input;

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
        readonly OverlayHoverPresenter _hoverPresenter;
        readonly IOverlayPlayerMenuHost _menuHost;
        readonly Image _backgroundImage;
        readonly Shape _grass;
        readonly Shape _shore;
        readonly Shape _water;
        readonly Dictionary<string, Ellipse> _spotVisuals = new Dictionary<string, Ellipse>();
        readonly Dictionary<string, OverlayPetActor> _others =
            new Dictionary<string, OverlayPetActor>();
        OverlayPetActor _ownActor;
        string _pondId = string.Empty;
        string _ownPlayerId = string.Empty;
        string _ownUserId = string.Empty;
        readonly Dictionary<string, OverlayPetActor> _actorsByUserId =
            new Dictionary<string, OverlayPetActor>(StringComparer.Ordinal);
        public event Action<string> SpotSelected;

        public PondScenePresenter(
            Canvas spotLayer,
            Canvas actorLayer,
            Canvas hoverLayer,
            Image backgroundImage,
            Shape grass,
            Shape shore,
            Shape water,
            IOverlayPlayerMenuHost menuHost)
        {
            _spotLayer = spotLayer;
            _actorLayer = actorLayer;
            _hoverPresenter = new OverlayHoverPresenter(hoverLayer);
            _menuHost = menuHost;
            _backgroundImage = backgroundImage;
            _grass = grass;
            _shore = shore;
            _water = water;
            TryLoadReplaceableArt();
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
            Ellipse marker;
            if (_spotVisuals.TryGetValue(spotId, out marker))
                return marker;
            return null;
        }

        public void Apply(IpcMessage message)
        {
            if (message == null)
                return;

            var pondId = message.PondId ?? string.Empty;
            if (!string.Equals(pondId, _pondId, StringComparison.Ordinal))
            {
                ClearSpots();
                ClearOthers();
                _pondId = pondId;
            }

            _ownPlayerId = message.OwnPlayerId ?? string.Empty;
            _ownUserId = message.OwnUserId ?? string.Empty;

            SyncSpots(message.Spots);
            ApplyOwnActor(message);
            if (message.Users != null)
                SyncOthers(message);
        }

        void SyncSpots(OverlaySpotDto[] spots)
        {
            var keep = new HashSet<string>(StringComparer.Ordinal);
            if (spots != null)
            {
                foreach (var spot in spots)
                {
                    if (spot == null || string.IsNullOrEmpty(spot.Id))
                        continue;
                    keep.Add(spot.Id);
                    var point = MapToScene(spot.X, spot.Y, spots);
                    if (!_spotVisuals.TryGetValue(spot.Id, out var marker))
                    {
                        marker = new Ellipse
                        {
                            Width = 18,
                            Height = 18,
                            Fill = new SolidColorBrush(Color.FromArgb(220, 243, 201, 105)),
                            Stroke = new SolidColorBrush(Color.FromArgb(255, 255, 255, 230)),
                            StrokeThickness = 2,
                            Tag = spot.Id,
                        };
                        marker.MouseLeftButtonDown += Spot_OnMouseLeftButtonDown;
                        _spotLayer.Children.Add(marker);
                        _spotVisuals[spot.Id] = marker;
                    }

                    Canvas.SetLeft(marker, point.X - 9);
                    Canvas.SetTop(marker, point.Y - 9);
                }
            }

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

        void Spot_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var marker = sender as Ellipse;
            if (marker?.Tag is string spotId)
                SpotSelected?.Invoke(spotId);
            e.Handled = true;
        }

        void ApplyOwnActor(IpcMessage message)
        {
            if (_ownActor == null)
            {
                _ownActor = new OverlayPetActor(OwnActorKey, _hoverPresenter);
                _actorLayer.Children.Add(_ownActor);
            }

            Point point;
            if (message.HasOwnPosition)
                point = MapToScene(message.OwnX, message.OwnY, message.Spots);
            else
                point = new Point(SceneWidth / 2, SceneHeight - CatSize / 2 - 32);
            _ownActor.Place(point.X, point.Y);
            _ownActor.Apply(
                message.OwnNickname,
                message.PetVisualState,
                message.FishingPhase,
                message.SessionFishingMs,
                message.HookDeadlineMs,
                message.OwnFishingStartedAt,
                message.OwnPetId);
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
                if (!_others.TryGetValue(key, out var actor))
                {
                    actor = new OverlayPetActor(key, _hoverPresenter);
                    actor.ConfigurePlayerMenu(
                        user.PlayerId,
                        user.IsBot,
                        _menuHost);
                    _actorLayer.Children.Add(actor);
                    _others[key] = actor;
                }
                else
                {
                    actor.ConfigurePlayerMenu(
                        user.PlayerId,
                        user.IsBot,
                        _menuHost);
                }

                if (!string.IsNullOrEmpty(user.UserId))
                    _actorsByUserId[user.UserId] = actor;

                Point point;
                if (user.HasPosition)
                    point = MapToScene(user.X, user.Y, message.Spots);
                else if (TryFindSpot(message.Spots, user.SpotId, out var sx, out var sy))
                    point = MapToScene(sx, sy, message.Spots);
                else
                    point = WaitingLane(keep.Count - 1);
                actor.Place(point.X, point.Y);
                actor.Apply(
                    user.Nickname,
                    user.PetVisualState,
                    user.FishingPhase,
                    user.SessionFishingMs,
                    user.HookDeadlineMs,
                    user.FishingStartedAt,
                    user.PetId);
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

        void ClearSpots()
        {
            _spotLayer.Children.Clear();
            _spotVisuals.Clear();
        }

        void TryLoadReplaceableArt()
        {
            var root = AppDomain.CurrentDomain.BaseDirectory;
            var pondPath = System.IO.Path.Combine(root, "OverlayResources", "pond.png");
            if (File.Exists(pondPath))
            {
                _backgroundImage.Source = LoadBitmap(pondPath);
                _backgroundImage.Visibility = Visibility.Visible;
                _grass.Visibility = Visibility.Collapsed;
                _shore.Visibility = Visibility.Collapsed;
                _water.Visibility = Visibility.Collapsed;
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
