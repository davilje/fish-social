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
        string _pondId = string.Empty;

        public PondScenePresenter(
            Canvas spotLayer,
            FrameworkElement ownCat,
            Image ownCatImage,
            Shape[] catFills,
            Image backgroundImage,
            Shape grass,
            Shape shore,
            Shape water)
        {
            _spotLayer = spotLayer;
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
                _pondId = pondId;
            }

            SyncSpots(message.Spots);
            PlaceOwnCat(message);
            TintCat(message.PetVisualState);
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
            var color = Color.FromRgb(77, 137, 168);
            switch (petVisualState)
            {
                case "fishing":
                    color = Color.FromRgb(90, 168, 214);
                    break;
                case "hooked":
                    color = Color.FromRgb(232, 156, 64);
                    break;
                case "catching":
                    color = Color.FromRgb(86, 176, 230);
                    break;
                case "dragging":
                    color = Color.FromRgb(232, 210, 86);
                    break;
                case "offline":
                    color = Color.FromRgb(120, 128, 136);
                    break;
            }

            var brush = new SolidColorBrush(color);
            foreach (var shape in _catFills)
            {
                if (shape != null)
                    shape.Fill = brush;
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
