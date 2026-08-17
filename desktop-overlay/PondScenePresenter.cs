using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Input;

namespace FishSocialOverlay
{
    /// <summary>
    /// Renders pond water, spots and the local cat from Unity-pushed snapshot
    /// fields. Snapshot updates move existing visuals; they do not rebuild the
    /// scene graph unless the pond or spot id set changes.
    /// </summary>
    public sealed class PondScenePresenter
    {
        public const double SceneWidth = 960;
        public const double SceneHeight = 480;
        public const double CatSize = 128;

        readonly Canvas _spotLayer;
        readonly FrameworkElement _ownCat;
        readonly Image _ownCatImage;
        readonly Shape[] _catFills;
        readonly Image _backgroundImage;
        readonly Shape _grass;
        readonly Shape _shore;
        readonly Shape _water;
        readonly Dictionary<string, Ellipse> _spotVisuals = new Dictionary<string, Ellipse>();
        readonly Dictionary<string, OverlayPetActor> _others =
            new Dictionary<string, OverlayPetActor>();
        readonly Canvas _actorLayer;
        string _pondId = string.Empty;
        string _ownVisualState = string.Empty;
        ImageSource[] _ownFrames = Array.Empty<ImageSource>();
        int _ownFrameIndex;
        DispatcherTimer _ownTimer;
        public event Action<string> SpotSelected;

        public PondScenePresenter(
            Canvas spotLayer,
            Canvas actorLayer,
            FrameworkElement ownCat,
            Image ownCatImage,
            Shape[] catFills,
            Image backgroundImage,
            Shape grass,
            Shape shore,
            Shape water)
        {
            _spotLayer = spotLayer;
            _actorLayer = actorLayer;
            _ownCat = ownCat;
            _ownCatImage = ownCatImage;
            _catFills = catFills ?? new Shape[0];
            _backgroundImage = backgroundImage;
            _grass = grass;
            _shore = shore;
            _water = water;
            TryLoadReplaceableArt();
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

            SyncSpots(message.Spots);
            PlaceOwnCat(message);
            ApplyOwnVisual(message.PetVisualState);
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

        void PlaceOwnCat(IpcMessage message)
        {
            Point point;
            if (message.HasOwnPosition)
                point = MapToScene(message.OwnX, message.OwnY, message.Spots);
            else
                point = new Point(SceneWidth / 2, SceneHeight - CatSize / 2 - 24);

            var left = Clamp(point.X - CatSize / 2, 8, SceneWidth - CatSize - 8);
            var top = Clamp(point.Y - CatSize / 2, 8, SceneHeight - CatSize - 8);
            Canvas.SetLeft(_ownCat, left);
            Canvas.SetTop(_ownCat, top);
            if (_ownCatImage.Visibility == Visibility.Visible)
            {
                Canvas.SetLeft(_ownCatImage, left);
                Canvas.SetTop(_ownCatImage, top);
            }
        }

        void TintCat(string petVisualState)
        {
            var brush = new SolidColorBrush(OverlayPetActor.StateColor(petVisualState));
            foreach (var shape in _catFills)
            {
                if (shape != null)
                    shape.Fill = brush;
            }
        }

        void ApplyOwnVisual(string petVisualState)
        {
            TintCat(petVisualState);
            if (string.Equals(_ownVisualState, petVisualState, StringComparison.Ordinal))
                return;

            _ownVisualState = petVisualState ?? "idle";
            _ownFrames = OverlayFrameCache.Get(_ownVisualState);
            _ownFrameIndex = 0;
            if (_ownFrames.Length == 0)
                return;

            _ownCat.Visibility = Visibility.Collapsed;
            _ownCatImage.Visibility = Visibility.Visible;
            _ownCatImage.Source = _ownFrames[0];
            if (_ownTimer == null)
            {
                _ownTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(125),
                };
                _ownTimer.Tick += OnOwnTick;
                _ownTimer.Start();
            }
        }

        void OnOwnTick(object sender, EventArgs e)
        {
            if (_ownFrames.Length <= 1)
                return;
            _ownFrameIndex = (_ownFrameIndex + 1) % _ownFrames.Length;
            _ownCatImage.Source = _ownFrames[_ownFrameIndex];
        }

        void SyncOthers(IpcMessage message)
        {
            var keep = new HashSet<string>(StringComparer.Ordinal);
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
                    actor = new OverlayPetActor(key);
                    _actorLayer.Children.Add(actor);
                    _others[key] = actor;
                }

                actor.Apply(user.Nickname, user.PetVisualState, user.IsBot);
                Point point;
                if (user.HasPosition)
                    point = MapToScene(user.X, user.Y, message.Spots);
                else if (TryFindSpot(message.Spots, user.SpotId, out var sx, out var sy))
                    point = MapToScene(sx, sy, message.Spots);
                else
                    point = WaitingLane(keep.Count - 1);
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
                _actorLayer.Children.Remove(_others[key]);
                _others.Remove(key);
            }
        }

        void ClearOthers()
        {
            foreach (var actor in _others.Values)
                _actorLayer.Children.Remove(actor);
            _others.Clear();
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
            var catPath = System.IO.Path.Combine(root, "OverlayResources", "cat.png");
            if (File.Exists(pondPath))
            {
                _backgroundImage.Source = LoadBitmap(pondPath);
                _backgroundImage.Visibility = Visibility.Visible;
                _grass.Visibility = Visibility.Collapsed;
                _shore.Visibility = Visibility.Collapsed;
                _water.Visibility = Visibility.Collapsed;
            }

            if (File.Exists(catPath))
            {
                _ownCatImage.Source = LoadBitmap(catPath);
                _ownCatImage.Visibility = Visibility.Visible;
                _ownCat.Visibility = Visibility.Collapsed;
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
            return new Point(96 + col * 140, 56 + row * 44);
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

        static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
