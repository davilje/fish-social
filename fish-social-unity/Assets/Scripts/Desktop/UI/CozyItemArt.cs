using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    public static class CozyItemArt
    {
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static bool loaded;

        public static bool IsAuthored(Transform root)
        {
            return root != null && root.Find("DesignHeader") != null;
        }

        public static Sprite Get(string name)
        {
            if (!loaded)
            {
                loaded = true;
                foreach (var atlas in new[] { "items-atlas", "fish-atlas" })
                    foreach (var sprite in Resources.LoadAll<Sprite>("Desktop/Theme/Cozy/items/" + atlas))
                        Sprites[sprite.name] = sprite;
            }
            return !string.IsNullOrEmpty(name) && Sprites.TryGetValue(name, out var result) ? result : null;
        }

        public static Sprite Fish(string speciesId)
        {
            var id = DesktopGameData.ResolveSpeciesId(speciesId);
            if (id == "bass") id = "largemouth_bass";
            return Get(id);
        }

        public static Sprite Product(int category, string id, string displayName)
        {
            var name = (id ?? "") + " " + (displayName ?? "");
            if (category == 1)
                return Get(name.Contains("竹") || name.Contains("bamboo") ? "rod-bamboo" :
                    name.Contains("远") || name.Contains("long") ? "rod-long" :
                    name.Contains("木") || name.Contains("wood") ? "rod-wood" : "rod-light");
            if (category == 2)
                return Get(name.Contains("木") || name.Contains("wood") ? "boat-wood" : "boat-blue");
            return Get(name.Contains("虾") || name.Contains("shrimp") ? "bait-shrimp" :
                name.Contains("麦") || name.Contains("wheat") ? "bait-wheat" :
                name.Contains("甜") || name.Contains("berry") ? "bait-berry" :
                name.Contains("混") || name.Contains("mix") ? "bait-mixed" :
                name.Contains("粒") || name.Contains("pellet") ? "bait-pellet" : "bait-basic");
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
            if (label != null) label.text = value ?? string.Empty;
        }
    }
}
