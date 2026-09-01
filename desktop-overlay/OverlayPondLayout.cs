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
        /// <summary>Design / pond-JSON canvas. Runtime window crop is OverlayViewportPreset.</summary>
        public const double ViewportWidth = 960;
        public const double ViewportHeight = 560;
        public const double MinSceneWidth = 960;
        public const double MaxSceneWidth = 4096;
        /// <summary>Fallback scene size (narrow pond). Not the live Overlay window.</summary>
        public const double CanvasWidth = ViewportWidth;
        public const double CanvasHeight = ViewportHeight;

        readonly Dictionary<string, OverlayLayoutObjectDto> _spots =
            new Dictionary<string, OverlayLayoutObjectDto>(StringComparer.Ordinal);
        readonly Dictionary<string, OverlayActorChrome> _actors =
            new Dictionary<string, OverlayActorChrome>(StringComparer.Ordinal);
        readonly List<OverlayLayoutObjectDto> _sprites = new List<OverlayLayoutObjectDto>();
        OverlayLayoutObjectDto _waiting;
        OverlayActorChrome _actorTemplate;
        double _petWidth = PondScenePresenter.CatSize;
        double _petHeight = PondScenePresenter.CatSize;
        double _sceneWidth = ViewportWidth;
        double _sceneHeight = ViewportHeight;
        double _runtimeViewportWidth = ViewportWidth;
        double _runtimeViewportHeight = ViewportHeight;
        readonly Dictionary<string, BitmapImage> _bitmapCache =
            new Dictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);

        public bool IsActive { get; private set; }

        public double SceneWidth => _sceneWidth;

        public double SceneHeight => _sceneHeight;

        /// <summary>True when layout JSON includes an explicit pond.width (14B pan source).</summary>
        public bool HasExplicitPondSize { get; private set; }

        public bool CanPan => _sceneWidth > _runtimeViewportWidth + 0.5;

        public double RuntimeViewportHeight => _runtimeViewportHeight;

        public void SetRuntimeViewport(double width, double height)
        {
            _runtimeViewportWidth = width > 0 ? width : ViewportWidth;
            _runtimeViewportHeight = height > 0 ? height : ViewportHeight;
        }

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
                var width = document.canvas.width;
                var height = document.canvas.height;
                if (height > 0 && Math.Abs(height - ViewportHeight) > 0.5)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[OverlayLayout] Canvas height must be " + ViewportHeight + ", got " +
                        height);
                    return false;
                }

                if (width > 0 && (width < MinSceneWidth - 0.5 || width > MaxSceneWidth + 0.5))
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[OverlayLayout] Canvas width must be " + MinSceneWidth + "-" +
                        MaxSceneWidth + ", got " + width);
                    return false;
                }
            }

            OverlayLayoutObjectDto pondBg = null;
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
                    if (IsPondBackgroundObject(item))
                        pondBg = item;
                }
                else if (kind.StartsWith("actor-", StringComparison.Ordinal))
                {
                    AddActorPart(item, kind);
                }
            }

            if (_spots.Count == 0)
            {
                Clear();
                System.Diagnostics.Debug.WriteLine("[OverlayLayout] No spots in " + jsonPath);
                return false;
            }

            ResolveSceneSize(document, pondBg);
            BuildActorTemplate();

            IsActive = true;
            System.Diagnostics.Debug.WriteLine(
                "[OverlayLayout] Loaded " + pondId + " spots=" + _spots.Count +
                " sprites=" + _sprites.Count +
                " pond=" + _sceneWidth + "x" + _sceneHeight +
                (HasExplicitPondSize ? " (explicit)" : string.Empty));
            return true;
        }

        void ResolveSceneSize(OverlayLayoutDocument document, OverlayLayoutObjectDto pondBg)
        {
            _sceneWidth = ViewportWidth;
            _sceneHeight = ViewportHeight;
            HasExplicitPondSize = false;

            if (document?.pond != null &&
                TryAcceptSceneSize(document.pond.width, document.pond.height, out var pondW, out var pondH))
            {
                _sceneWidth = pondW;
                _sceneHeight = pondH;
                HasExplicitPondSize = true;
                return;
            }

            if (pondBg != null &&
                TryAcceptSceneSize(pondBg.w, pondBg.h, out pondW, out pondH))
            {
                _sceneWidth = pondW;
                _sceneHeight = pondH;
                return;
            }

            if (document?.canvas != null &&
                TryAcceptSceneSize(document.canvas.width, document.canvas.height, out pondW, out pondH))
            {
                _sceneWidth = pondW;
                _sceneHeight = pondH;
            }
        }

        static bool TryAcceptSceneSize(double width, double height, out double w, out double h)
        {
            w = ViewportWidth;
            h = ViewportHeight;
            if (width < MinSceneWidth - 0.5 || width > MaxSceneWidth + 0.5)
                return false;
            w = width;
            if (height > 0 && Math.Abs(height - ViewportHeight) <= 0.5)
                h = ViewportHeight;
            else if (height >= MinSceneWidth * 0.1)
                h = height;
            return true;
        }

        static bool IsPondBackgroundObject(OverlayLayoutObjectDto item)
        {
            if (item == null)
                return false;
            var id = (item.id ?? string.Empty).Trim();
            if (string.Equals(id, "pond-bg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, "pond", StringComparison.OrdinalIgnoreCase))
                return true;
            var kind = (item.kind ?? string.Empty).Trim().ToLowerInvariant();
            return kind == "background";
        }

        /// <summary>
        /// Prefer ponds/&lt;id&gt;.png pixel width when layout has no explicit pond block.
        /// </summary>
        public void ApplyArtPixelSize(int pixelWidth, int pixelHeight)
        {
            if (HasExplicitPondSize)
                return;
            if (!TryAcceptSceneSize(pixelWidth, pixelHeight, out var w, out var h))
                return;
            _sceneWidth = w;
            _sceneHeight = h;
        }

        public OverlayActorChrome ActorTemplate => _actorTemplate;

        public bool TryGetActorChrome(string spotId, out OverlayActorChrome chrome)
        {
            chrome = ResolveActorChrome(spotId);
            return chrome != null && chrome.HasAny;
        }

        /// <summary>
        /// OverlayPondActor is the shared template. Spot chrome is only a source
        /// for relative name/bubble/hint offsets; every pet uses the same cluster.
        /// </summary>
        public OverlayActorChrome ResolveActorChrome(string spotId)
        {
            if (!string.IsNullOrEmpty(spotId) &&
                _actors.TryGetValue(spotId, out var chrome) &&
                chrome != null &&
                chrome.Name != null &&
                chrome.Pet != null)
                return Relativize(chrome);
            return _actorTemplate;
        }

        void BuildActorTemplate()
        {
            OverlayActorChrome source = null;
            foreach (var pair in _actors)
            {
                var chrome = pair.Value;
                if (chrome == null || chrome.Pet == null || chrome.Name == null)
                    continue;
                source = chrome;
                break;
            }

            _actorTemplate = source != null ? Relativize(source) : BakeDefaultTemplate();
        }

        static OverlayActorChrome Relativize(OverlayActorChrome source)
        {
            if (source == null || source.Pet == null)
                return BakeDefaultTemplate();

            var pet = source.Pet;
            return new OverlayActorChrome
            {
                Pet = CopyLocal(pet, 0, 0),
                Hit = ShiftLocal(source.Hit, pet),
                Name = ShiftLocal(source.Name, pet),
                Status = ShiftLocal(source.Status, pet),
                Ring = ShiftLocal(source.Ring, pet),
                RingBg = ShiftLocal(source.RingBg, pet),
                Seat = ShiftLocal(source.Seat, pet),
                Bubble = ShiftLocal(source.Bubble, pet),
                Hint = ShiftLocal(source.Hint, pet),
            };
        }

        static OverlayLayoutObjectDto ShiftLocal(
            OverlayLayoutObjectDto part,
            OverlayLayoutObjectDto pet)
        {
            if (part == null || pet == null)
                return null;
            return CopyLocal(part, part.x - pet.x, part.y - pet.y);
        }

        static OverlayLayoutObjectDto CopyLocal(OverlayLayoutObjectDto part, double x, double y)
        {
            if (part == null)
                return null;
            return new OverlayLayoutObjectDto
            {
                id = part.id,
                kind = part.kind,
                spotId = part.spotId,
                x = x,
                y = y,
                w = part.w,
                h = part.h,
                z = part.z,
                sprite = part.sprite,
                spriteSlice = part.spriteSlice,
                anchor = "top-left",
            };
        }

        static OverlayActorChrome BakeDefaultTemplate()
        {
            return new OverlayActorChrome
            {
                Pet = new OverlayLayoutObjectDto { kind = "actor-pet", x = 0, y = 0, w = 128, h = 128 },
                Hit = new OverlayLayoutObjectDto { kind = "actor-hit", x = 6, y = 23, w = 57, h = 85 },
                Name = new OverlayLayoutObjectDto { kind = "actor-name", x = -19, y = 76, w = 88, h = 20 },
                Status = new OverlayLayoutObjectDto { kind = "actor-status", x = 17, y = -22, w = 18, h = 18 },
                Ring = new OverlayLayoutObjectDto { kind = "actor-ring", x = 10, y = -28, w = 31, h = 31 },
                RingBg = new OverlayLayoutObjectDto { kind = "actor-ring-bg", x = 10, y = -28, w = 31, h = 31 },
                Bubble = new OverlayLayoutObjectDto { kind = "actor-bubble", x = -6, y = -18, w = 80, h = 20 },
                Hint = new OverlayLayoutObjectDto { kind = "actor-hint", x = -8, y = -18, w = 84, h = 20 },
            };
        }

        /// <summary>
        /// Cat body center from actor-pet when present; otherwise false.
        /// actor-* JSON x/y are always canvas top-left (see LayoutAnchor).
        /// </summary>
        public bool TryGetPetPoint(string spotId, out Point point)
        {
            point = new Point();
            if (!IsActive || string.IsNullOrEmpty(spotId))
                return false;
            if (!_actors.TryGetValue(spotId, out var chrome) || chrome == null || chrome.Pet == null)
                return false;
            var pet = chrome.Pet;
            var width = pet.w > 0 ? pet.w : OverlayPetActor.BodySize;
            var height = pet.h > 0 ? pet.h : OverlayPetActor.BodySize;
            ResolveRect(pet.x, pet.y, width, height, LayoutAnchor(pet), out var left, out var top);
            point = new Point(left + width * 0.5, top + height * 0.5);
            return true;
        }

        /// <summary>
        /// Prefer actor-seat footprint for marker placement; fall back to kind=spot.
        /// </summary>
        public bool TryGetSeatVisual(string spotId, out OverlayLayoutObjectDto visual)
        {
            visual = null;
            if (string.IsNullOrEmpty(spotId))
                return false;
            if (_actors.TryGetValue(spotId, out var chrome) &&
                chrome != null &&
                chrome.Seat != null &&
                chrome.Seat.w > 0 &&
                chrome.Seat.h > 0)
            {
                visual = chrome.Seat;
                return true;
            }

            if (_spots.TryGetValue(spotId, out var spot) && spot != null)
            {
                visual = spot;
                return true;
            }

            return false;
        }

        /// <summary>
        /// actor-* parts are authored with PlaceTopLeft and exported as absolute top-left
        /// pixels. Some older JSON mislabeled them as bottom-center — ignore that.
        /// </summary>
        public static string LayoutAnchor(OverlayLayoutObjectDto item)
        {
            if (item == null)
                return "top-left";
            var kind = (item.kind ?? string.Empty).Trim().ToLowerInvariant();
            if (kind.StartsWith("actor-", StringComparison.Ordinal))
                return "top-left";
            if (string.IsNullOrWhiteSpace(item.anchor))
                return kind == "spot" ? "bottom-center" : "top-left";
            return item.anchor;
        }

        void AddActorPart(OverlayLayoutObjectDto item, string kind)
        {
            var spotId = string.IsNullOrWhiteSpace(item.spotId) ? item.id : item.spotId;
            if (string.IsNullOrWhiteSpace(spotId))
                return;
            if (!_actors.TryGetValue(spotId, out var chrome) || chrome == null)
            {
                chrome = new OverlayActorChrome();
                _actors[spotId] = chrome;
            }

            if (kind == "actor-pet")
                chrome.Pet = item;
            else if (kind == "actor-hit")
                chrome.Hit = item;
            else if (kind == "actor-name")
                chrome.Name = item;
            else if (kind == "actor-status")
                chrome.Status = item;
            else if (kind == "actor-ring")
                chrome.Ring = item;
            else if (kind == "actor-ring-bg")
                chrome.RingBg = item;
            else if (kind == "actor-seat")
                chrome.Seat = item;
            else if (kind == "actor-bubble")
                chrome.Bubble = item;
            else if (kind == "actor-hint")
                chrome.Hint = item;
        }

        public bool TryGetSpotPoint(string spotId, out Point point)
        {
            point = new Point();
            if (!IsActive || string.IsNullOrEmpty(spotId))
                return false;
            if (!_spots.TryGetValue(spotId, out var item) || item == null)
                return false;
            var width = item.w > 0 ? item.w : 48;
            var height = item.h > 0 ? item.h : 32;
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
            var seats = System.IO.Path.Combine(resourcesRoot, "seats", fileName);
            if (File.Exists(seats))
                return seats;
            var root = System.IO.Path.Combine(resourcesRoot, fileName);
            if (File.Exists(root))
                return root;
            return null;
        }

        void Clear()
        {
            IsActive = false;
            _spots.Clear();
            _actors.Clear();
            _actorTemplate = null;
            _sprites.Clear();
            _waiting = null;
            _petWidth = PondScenePresenter.CatSize;
            _petHeight = PondScenePresenter.CatSize;
            _sceneWidth = ViewportWidth;
            _sceneHeight = ViewportHeight;
            HasExplicitPondSize = false;
        }

        [DataContract]
        sealed class OverlayLayoutDocument
        {
            [DataMember(Name = "version")] public int version;
            [DataMember(Name = "pondId")] public string pondId = null;
            [DataMember(Name = "canvas")] public OverlayLayoutCanvasDto canvas = null;
            [DataMember(Name = "pond")] public OverlayLayoutPondDto pond = null;
            [DataMember(Name = "objects")] public OverlayLayoutObjectDto[] objects = null;
        }

        [DataContract]
        sealed class OverlayLayoutCanvasDto
        {
            [DataMember(Name = "width")] public double width = CanvasWidth;
            [DataMember(Name = "height")] public double height = CanvasHeight;
            [DataMember(Name = "origin")] public string origin = null;
        }

        [DataContract]
        sealed class OverlayLayoutPondDto
        {
            [DataMember(Name = "width")] public double width;
            [DataMember(Name = "height")] public double height;
        }
    }

    public sealed class OverlayActorChrome
    {
        public OverlayLayoutObjectDto Pet;
        public OverlayLayoutObjectDto Hit;
        public OverlayLayoutObjectDto Name;
        public OverlayLayoutObjectDto Status;
        public OverlayLayoutObjectDto Ring;
        public OverlayLayoutObjectDto RingBg;
        public OverlayLayoutObjectDto Seat;
        public OverlayLayoutObjectDto Bubble;
        public OverlayLayoutObjectDto Hint;

        public bool HasAny
        {
            get
            {
                return Pet != null || Hit != null || Name != null || Status != null || Ring != null ||
                       RingBg != null || Seat != null || Bubble != null || Hint != null;
            }
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
        [DataMember(Name = "spriteSlice")] public int[] spriteSlice;
        [DataMember(Name = "anchor")] public string anchor;
    }
}
