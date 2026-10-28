using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    public static class CozyItemArt
    {
        public static readonly Color CompletedBadge = new Color(0.86f, 0.94f, 0.84f, 1f);
        public static readonly Color PendingBadge = new Color(0.95f, 0.91f, 0.84f, 1f);
        public static readonly Color CompletedText = new Color(0.24f, 0.55f, 0.29f, 1f);
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static bool loaded;
        static readonly Dictionary<string, Sprite> FishSprites = new Dictionary<string, Sprite>();

        [System.Serializable]
        public sealed class FishArtEntry
        {
            public string speciesId;
            public string displayName;
            public string resourcePath;
            public string spriteName;
        }

        [System.Serializable]
        sealed class FishArtCatalog
        {
            public FishArtEntry[] items;
        }

        static FishArtEntry[] fishCatalog;

        public static IReadOnlyList<FishArtEntry> FishCatalog
        {
            get
            {
                if (fishCatalog == null)
                {
                    var asset = Resources.Load<TextAsset>("Desktop/Theme/Cozy/items/fish/catalog");
                    var catalog = asset != null ? JsonUtility.FromJson<FishArtCatalog>(asset.text) : null;
                    fishCatalog = catalog?.items ?? new FishArtEntry[0];
                }
                return fishCatalog;
            }
        }

        public static bool IsAuthored(Transform root)
        {
            return root != null && root.Find("DesignHeader") != null;
        }

        public static Sprite Get(string name)
        {
            var original = CozyJournalTheme.Sprite(DesktopLocalization.Concat("illustrations/items/", name));
            if (original != null) return original;
            if (!loaded)
            {
                loaded = true;
                foreach (var atlas in new[] { "items-atlas", "fish-atlas" })
                    foreach (var sprite in Resources.LoadAll<Sprite>(DesktopLocalization.Concat("Desktop/Theme/Cozy/items/", atlas)))
                        Sprites[sprite.name] = sprite;
            }
            return !string.IsNullOrEmpty(name) && Sprites.TryGetValue(name, out var result) ? result : null;
        }

        public static Sprite Fish(string speciesId)
        {
            var id = DesktopGameData.ResolveSpeciesId(speciesId);
            if (id == "largemouth_bass") id = "black_bass";
            if (string.IsNullOrEmpty(id)) return null;
            var original = CozyJournalTheme.Sprite(DesktopLocalization.Concat("illustrations/fish/", id));
            if (original != null) return original;
            if (FishSprites.TryGetValue(id, out var cached)) return cached;
            foreach (var entry in FishCatalog)
            {
                if (entry.speciesId != id) continue;
                Sprite result = null;
                if (string.IsNullOrEmpty(entry.spriteName))
                    result = Resources.Load<Sprite>(entry.resourcePath);
                else
                    foreach (var sprite in Resources.LoadAll<Sprite>(entry.resourcePath))
                        if (sprite.name == entry.spriteName) { result = sprite; break; }
                if (result != null) FishSprites[id] = result;
                return result;
            }
            return null;
        }

        public static Sprite Product(int category, string id, string displayName)
        {
            if (category == 1)
                return Get(RodSprite(id));
            if (category == 0)
                return Get(BaitSprite(id));
            var name = DesktopLocalization.Concat(DesktopLocalization.Concat((id ?? ""), " "), (displayName ?? ""));
            return Get(name.Contains("木") || name.Contains("wood") ? "boat-wood" : "boat-blue");
        }

        static string BaitSprite(string id)
        {
            switch (id)
            {
                case "bait-basic": return "bait-basic";
                case "bait-veg": return "bait-wheat";
                case "bait-mix": return "bait-mixed";
                case "bait-meat": return "bait-shrimp";
                default: return null;
            }
        }

        static string RodSprite(string id)
        {
            switch (id)
            {
                case "rod-bamboo": return "rod-bamboo";
                case "rod-tai":
                case "rod-stream": return "rod-light";
                case "rod-iso":
                case "rod-surf":
                case "rod-giant": return "rod-long";
                case "rod-lure":
                case "rod-heavy": return "rod-wood";
                default: return null;
            }
        }

        public static void Set(Transform root, string path, Sprite sprite)
        {
            var image = root != null ? root.Find(path)?.GetComponent<Image>() : null;
            if (image == null) return;
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        public static void Text(Transform root, string path, string value)
        {
            var label = root != null ? root.Find(path)?.GetComponent<UnityEngine.UI.Text>() : null;
            if (label != null) DesktopLocalization.SetText(label, value ?? string.Empty);
        }

        public static bool Rows(Transform root, string path, params string[] cells)
        {
            var rows = root != null ? root.Find(path) : null;
            if (rows == null)
                return false;
            var count = cells != null ? cells.Length / 3 : 0;
            for (var index = 0; index < rows.childCount; index++)
            {
                var row = rows.GetChild(index);
                row.gameObject.SetActive(index < count);
                if (index >= count)
                    continue;
                var icon = row.Find("Icon")?.GetComponent<Image>();
                if (icon != null)
                {
                    icon.sprite = string.IsNullOrEmpty(cells[index * 3]) ? null
                        : CatTheme.LoadCozy(DesktopLocalization.Concat(DesktopLocalization.Concat("icons/", cells[index * 3]), ".png"), Vector4.zero);
                    icon.enabled = icon.sprite != null;
                    icon.preserveAspect = true;
                }
                Text(row, "Label", cells[index * 3 + 1]);
                Text(row, "Value", cells[index * 3 + 2]);
            }
            return true;
        }
    }
}
