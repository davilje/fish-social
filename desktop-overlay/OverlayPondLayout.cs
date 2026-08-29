using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FishSocialOverlay
{
    /// <summary>
    /// Loads OverlayResources/layouts/&lt;pondId&gt;.json. Missing/invalid files
    /// leave the pond on MapToScene fallback.
    /// </summary>
    public sealed class OverlayPondLayout
    {
        public const double CanvasWidth = 960;
        public const double CanvasHeight = 560;

        readonly Dictionary<string, OverlayLayoutObjectDto> _spots =
            new Dictionary<string, OverlayLayoutObjectDto>(StringComparer.Ordinal);
        readonly List<OverlayLayoutObjectDto> _sprites = new List<OverlayLayoutObjectDto>();
        OverlayLayoutObjectDto _waiting;
        double _petWidth = PondScenePresenter.CatSize;
        double _petHeight = PondScenePresenter.CatSize;
        readonly Dictionary<string, BitmapImage> _bitmapCache =
            new Dictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);

        public bool IsActive { get; private set; }

        public double PetWidth => _petWidth;

        public double PetHeight => _petHeight;

        public IReadOnlyDictionary<string, OverlayLayoutObjectDto> Spots => _spots;

        public IReadOnlyList<OverlayLayoutObjectDto> Sprites => _sprites;

        public bool TryLoad(string pondId)
        {
            Clear();
            if (string.IsNullOrWhiteSpace(pondId))
                return false;

            var jsonPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "OverlayResources",
                "layouts",
                pondId + ".json");
            if (!File.Exists(jsonPath))
                return false;

            OverlayLayoutDocument document;
            try
            {
                using (var stream = File.OpenRead(jsonPath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(OverlayLayoutDocument));
                    document = serializer.ReadObject(stream) as OverlayLayoutDocument;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[OverlayLayout] Parse failed: " + ex.Message);
                return false;
            }

            if (document == null || document.objects == null || document.objects.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("[OverlayLayout] Empty layout: " + jsonPath);
                return false;
            }

            if (document.canvas != null)
            {
                if (Math.Abs(document.canvas.width - CanvasWidth) > 0.5 ||
                    Math.Abs(document.canvas.height - CanvasHeight) > 0.5)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[OverlayLayout] Canvas must be 960x560, got " +
                        document.canvas.width + "x" + document.canvas.height);
                    return false;
                }
            }

            foreach (var item in document.objects)
            {
                if (item == null)
                    continue;
                var kind = (item.kind ?? string.Empty).Trim().ToLowerInvariant();
                if (kind == "spot")
                {
                    var spotId = string.IsNullOrWhiteSpace(item.spotId) ? item.id : item.spotId;
                    if (string.IsNullOrWhiteSpace(spotId))
                        continue;
                    if (_spots.ContainsKey(spotId))
                    {
                        System.Diagnostics.Debug.WriteLine("[OverlayLayout] Duplicate spotId: " + spotId);
                        Clear();
                        return false;
                    }

                    item.spotId = spotId;
                    _spots[spotId] = item;
                }
                else if (kind == "waiting")
                {
                    _waiting = item;
                }
                else if (kind == "pet-size")
                {
                    if (item.w > 0)
                        _petWidth = item.w;
                    if (item.h > 0)
                        _petHeight = item.h;
                }
                else if (kind == "sprite" || kind == "background")
                {
                    _sprites.Add(item);
                }
            }

            if (_spots.Count == 0)
            {
                Clear();
                System.Diagnostics.Debug.WriteLine("[OverlayLayout] No spots in " + jsonPath);
                return false;
            }

            IsActive = true;
            System.Diagnostics.Debug.WriteLine(
                "[OverlayLayout] Loaded " + pondId + " spots=" + _spots.Count +
                " sprites=" + _sprites.Count);
            return true;
        }

        public bool TryGetSpotPoint(string spotId, out Point point)
        {
            point = new Point();
            if (!IsActive || string.IsNullOrEmpty(spotId))
                return false;
            if (!_spots.TryGetValue(spotId, out var item) || item == null)
                return false;
            var width = item.w > 0 ? item.w : 24;
            var height = item.h > 0 ? item.h : 24;
            ResolveRect(item.x, item.y, width, height, item.anchor, out var left, out var top);
            point = new Point(left + width * 0.5, top + height * 0.5);
            return true;
        }

        public Point WaitingLane(int index)
        {
            if (_waiting == null || _waiting.w <= 0 || _waiting.h <= 0)
            {
                var col = index % 6;
                var row = index / 6;
                return new Point(96 + col * 140, 72 + row * 44);
            }

            var columns = Math.Max(1, (int)Math.Floor(_waiting.w / 120.0));
            var cellW = _waiting.w / columns;
            var column = index % columns;
            var lane = index / columns;
            return new Point(
                _waiting.x + cellW * column + cellW * 0.5,
                _waiting.y + _waiting.h * 0.5 + lane * 44);
        }

        public static void ResolveRect(
            double x,
            double y,
            double width,
            double height,
            string anchor,
            out double left,
            out double top)
        {
            var kind = (anchor ?? string.Empty).Trim().ToLowerInvariant();
            if (kind == "bottom-center")
            {
                left = x - width * 0.5;
                top = y - height;
                return;
            }

            if (kind == "center")
            {
                left = x - width * 0.5;
                top = y - height * 0.5;
                return;
            }

            left = x;
            top = y;
        }

        public Image CreateSpriteImage(OverlayLayoutObjectDto item, string resourcesRoot)
        {
            if (item == null)
                return null;
            var image = new Image
            {
                Width = item.w > 0 ? item.w : CanvasWidth,
                Height = item.h > 0 ? item.h : CanvasHeight,
                Stretch = Stretch.Fill,
                IsHitTestVisible = false,
            };
            ResolveRect(item.x, item.y, image.Width, image.Height, item.anchor, out var left, out var top);
            Canvas.SetLeft(image, left);
            Canvas.SetTop(image, top);
            Panel.SetZIndex(image, item.z);

            var bitmap = LoadSprite(resourcesRoot, item.sprite);
            if (bitmap != null)
                image.Source = bitmap;
            return image;
        }

        BitmapImage LoadSprite(string resourcesRoot, string sprite)
        {
            if (string.IsNullOrWhiteSpace(sprite))
                return null;
            var path = ResolveSpritePath(resourcesRoot, sprite);
            if (string.IsNullOrEmpty(path))
                return null;
            if (_bitmapCache.TryGetValue(path, out var cached))
                return cached;

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                _bitmapCache[path] = image;
                return image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[OverlayLayout] Sprite load failed: " + path + " " + ex.Message);
                return null;
            }
        }

        static string ResolveSpritePath(string resourcesRoot, string sprite)
        {
            var relative = sprite.Replace('/', System.IO.Path.DirectorySeparatorChar)
                .Replace('\\', System.IO.Path.DirectorySeparatorChar);
            var direct = System.IO.Path.Combine(resourcesRoot, relative);
            if (File.Exists(direct))
                return direct;
            var fileName = System.IO.Path.GetFileName(sprite);
            var layouts = System.IO.Path.Combine(resourcesRoot, "layouts", fileName);
            if (File.Exists(layouts))
                return layouts;
            var ponds = System.IO.Path.Combine(resourcesRoot, "ponds", fileName);
            if (File.Exists(ponds))
                return ponds;
            var root = System.IO.Path.Combine(resourcesRoot, fileName);
            if (File.Exists(root))
                return root;
            return null;
        }

        void Clear()
        {
            IsActive = false;
            _spots.Clear();
            _sprites.Clear();
            _waiting = null;
            _petWidth = PondScenePresenter.CatSize;
            _petHeight = PondScenePresenter.CatSize;
        }

        [DataContract]
        sealed class OverlayLayoutDocument
        {
            [DataMember(Name = "version")] public int version;
            [DataMember(Name = "pondId")] public string pondId = null;
            [DataMember(Name = "canvas")] public OverlayLayoutCanvasDto canvas = null;
            [DataMember(Name = "objects")] public OverlayLayoutObjectDto[] objects = null;
        }

        [DataContract]
        sealed class OverlayLayoutCanvasDto
        {
            [DataMember(Name = "width")] public double width = CanvasWidth;
            [DataMember(Name = "height")] public double height = CanvasHeight;
            [DataMember(Name = "origin")] public string origin = null;
        }
    }

    [DataContract]
    public sealed class OverlayLayoutObjectDto
    {
        [DataMember(Name = "id")] public string id;
        [DataMember(Name = "kind")] public string kind;
        [DataMember(Name = "spotId")] public string spotId;
        [DataMember(Name = "x")] public double x;
        [DataMember(Name = "y")] public double y;
        [DataMember(Name = "w")] public double w;
        [DataMember(Name = "h")] public double h;
        [DataMember(Name = "z")] public int z;
        [DataMember(Name = "sprite")] public string sprite;
        [DataMember(Name = "anchor")] public string anchor;
    }
}
