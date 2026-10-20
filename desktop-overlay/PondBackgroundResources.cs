using System;
using System.IO;

namespace FishSocialOverlay
{
    /// <summary>Resolves shared art referenced by a saved layout, then legacy fallbacks.</summary>
    public static class PondBackgroundResources
    {
        public static string Resolve(string resourcesRoot, string pondId, string backgroundSprite)
        {
            if (!string.IsNullOrWhiteSpace(backgroundSprite))
            {
                var root = Path.GetFullPath(resourcesRoot) + Path.DirectorySeparatorChar;
                var relative = backgroundSprite.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                var mapped = Path.GetFullPath(Path.Combine(resourcesRoot, relative));
                if (mapped.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(mapped))
                    return mapped;
            }
            if (!string.IsNullOrWhiteSpace(pondId))
            {
                var perPond = Path.Combine(resourcesRoot, "ponds", pondId + ".png");
                if (File.Exists(perPond)) return perPond;
            }
            var fallback = Path.Combine(resourcesRoot, "ponds", "_default.png");
            if (File.Exists(fallback)) return fallback;
            var legacy = Path.Combine(resourcesRoot, "pond.png");
            return File.Exists(legacy) ? legacy : null;
        }
    }
}
