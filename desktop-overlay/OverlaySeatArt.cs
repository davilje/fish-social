using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FishSocialOverlay
{
    /// <summary>
    /// Seat PNG lookup (STEAM-DESKTOP-14A): per-seat sprite → _default → pond-wide → null.
    /// </summary>
    static class OverlaySeatArt
    {
        static readonly Dictionary<string, ImageSource> Cache =
            new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public static ImageSource TryLoad(
            string pondId,
            OverlayLayoutObjectDto spot,
            OverlayLayoutObjectDto seat,
            out bool usedFallback)
        {
            usedFallback = false;
            var root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OverlayResources");

            // 1) actor-seat sprite (per-seat art)
            if (seat != null && !string.IsNullOrWhiteSpace(seat.sprite))
            {
                var seatImage = LoadCached(ResolveSpritePath(root, seat.sprite));
                if (seatImage != null)
                    return seatImage;
            }

            // 2) spot.sprite from layout JSON
            if (spot != null && !string.IsNullOrWhiteSpace(spot.sprite))
            {
                var spotImage = LoadCached(ResolveSpritePath(root, spot.sprite));
                if (spotImage != null)
                    return spotImage;
            }

            // 3) shared default
            var fallbackDefault = Path.Combine(root, "seats", "_default.png");
            var defaultImage = LoadCached(fallbackDefault);
            if (defaultImage != null)
                return defaultImage;

            // 4) optional pond-wide override (legacy)
            if (!string.IsNullOrWhiteSpace(pondId))
            {
                var perPond = Path.Combine(root, "seats", pondId + ".png");
                var loaded = LoadCached(perPond);
                if (loaded != null)
                    return loaded;
            }

            usedFallback = true;
            System.Diagnostics.Debug.WriteLine(
                "[OverlaySeat] No seat PNG for spot " +
                (spot?.spotId ?? spot?.id ?? seat?.spotId ?? "?") +
                "; using ellipse fallback.");
            return null;
        }

        static string ResolveSpritePath(string resourcesRoot, string sprite)
        {
            if (string.IsNullOrWhiteSpace(sprite))
                return null;

            var relative = sprite.Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            var direct = Path.Combine(resourcesRoot, relative);
            if (File.Exists(direct))
                return direct;

            var fileName = Path.GetFileName(sprite);
            if (string.IsNullOrEmpty(fileName))
                return null;

            var seats = Path.Combine(resourcesRoot, "seats", fileName);
            if (File.Exists(seats))
                return seats;

            var layouts = Path.Combine(resourcesRoot, "layouts", fileName);
            if (File.Exists(layouts))
                return layouts;

            return Path.Combine(resourcesRoot, fileName);
        }

        static ImageSource LoadCached(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            if (Cache.TryGetValue(path, out var cached))
                return cached;

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                Cache[path] = image;
                return image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[OverlaySeat] Load failed: " + path + " " + ex.Message);
                return null;
            }
        }
    }
}
