using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FishSocialOverlay
{
    /// <summary>
    /// Monochrome Cozy phase icons from OverlayResources/hud/cozy/icons-light.
    /// (hooked / groundbait) plus hook-ring.png and ring-bg.png for bite/groundbait
    /// progress. Progress ring assets and animation remain unchanged.
    /// </summary>
    static class OverlayStatusIcons
    {
        public const double Size = 18;
        public const string RingKind = "hook-ring";
        public const string RingBgKind = "ring-bg";

        static readonly Dictionary<string, ImageSource> Cache =
            new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public static ImageSource Get(string kind)
        {
            var key = string.IsNullOrWhiteSpace(kind) ? "fishing" : kind.Trim();
            if (Cache.TryGetValue(key, out var cached))
                return cached;

            var icon = string.Equals(key, "hooked", StringComparison.OrdinalIgnoreCase) ? "hook" :
                string.Equals(key, "groundbait", StringComparison.OrdinalIgnoreCase) ? "leaf" : "rod";
            var loaded = CozyTheme.LoadInkIcon(CozyTheme.IconPath(icon));
            Cache[key] = loaded;
            return loaded;
        }

        /// <summary>
        /// Bite progress ring art. Null when the PNG is missing (caller uses vector Path).
        /// </summary>
        public static ImageSource TryGetRing()
        {
            return TryGetOptional(RingKind);
        }

        /// <summary>
        /// Static disc behind the progress ring. Null when ring-bg.png is missing.
        /// </summary>
        public static ImageSource TryGetRingBg()
        {
            return TryGetOptional(RingBgKind);
        }

        static ImageSource TryGetOptional(string kind)
        {
            if (Cache.TryGetValue(kind, out var cached))
                return cached;

            var loaded = TryLoadPng(kind);
            if (loaded != null)
                Cache[kind] = loaded;
            return loaded;
        }

        static ImageSource TryLoadPng(string kind)
        {
            if (!IsSafeKind(kind))
                return null;
            var path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "OverlayResources",
                "status",
                kind + ".png");
            if (!File.Exists(path))
                return null;
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        static bool IsSafeKind(string kind)
        {
            for (var i = 0; i < kind.Length; i++)
            {
                var c = kind[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_'))
                    return false;
            }

            return kind.Length > 0;
        }

    }
}
