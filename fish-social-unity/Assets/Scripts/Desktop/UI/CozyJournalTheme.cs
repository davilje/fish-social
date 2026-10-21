using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Shared production art settings. Reference screenshot layers are never loaded here.</summary>
    public static class CozyJournalTheme
    {
        public const string ResourceRoot = "Desktop/Theme/Cozy/journal/";
        public static readonly Vector2 DesignResolution = new Vector2(1672, 941);
        [Serializable] sealed class Manifest { public Entry[] assets; }
        [Serializable] sealed class Entry
        {
            public string name;
            public float[] contentInsets;
            public string imageType;
        }
        static Dictionary<string, Entry> entries;
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            path = path.Replace('\\', '/');
            if (path.StartsWith("journal/", StringComparison.Ordinal)) path = path.Substring(8);
            if (path.EndsWith(".png", StringComparison.Ordinal)) path = path.Substring(0, path.Length - 4);
            return path;
        }

        public static Sprite Sprite(string relativePath)
        {
            var path = Normalize(relativePath);
            if (!sprites.TryGetValue(path, out var sprite) || sprite == null)
            {
                sprite = Resources.Load<Sprite>(ResourceRoot + path);
                if (sprite != null) sprites[path] = sprite;
            }
            return sprite;
        }

        static Entry Settings(string path)
        {
            if (entries == null)
            {
                entries = new Dictionary<string, Entry>();
                var json = Resources.Load<TextAsset>(ResourceRoot + "ui-manifest");
                var manifest = json != null ? JsonUtility.FromJson<Manifest>(json.text) : null;
                if (manifest?.assets != null)
                    foreach (var entry in manifest.assets) entries[entry.name] = entry;
            }
            return entries.TryGetValue(path, out var settings) ? settings : null;
        }

        public static void ApplySurface(Image image, string relativePath, bool expandInsets = false)
        {
            if (image == null) return;
            var path = Normalize(relativePath);
            var sprite = Sprite(path);
            if (sprite == null) return;
            var settings = Settings(path);
            image.overrideSprite = null;
            image.sprite = sprite;
            image.type = settings?.imageType == "Tiled" ? Image.Type.Tiled
                : sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = 1;
            image.color = Color.white;
            image.enabled = true;
            if (!expandInsets) return;
            // A visual is a child of its logical content rectangle. Reset from anchors each time,
            // so hover/selection and repeated skin passes cannot accumulate transparent padding.
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            var inset = settings?.contentInsets;
            rect.offsetMin = inset != null && inset.Length == 4 ? new Vector2(-inset[0], -inset[3]) : Vector2.zero;
            rect.offsetMax = inset != null && inset.Length == 4 ? new Vector2(inset[2], inset[1]) : Vector2.zero;
        }

        public static void ApplyInput(InputField input)
        {
            if (input == null) return;
            var background = input.GetComponent<Image>();
            if (background != null) ApplySurface(background, "surfaces/input-default");
            var colors = input.colors;
            colors.normalColor = colors.highlightedColor = colors.pressedColor = Color.white;
            colors.selectedColor = new Color(1f, .97f, .90f);
            colors.disabledColor = new Color(.82f, .79f, .74f);
            input.colors = colors;
            input.caretColor = CatTheme.Ink;
            input.selectionColor = new Color(.99f, .57f, .24f, .25f);
            if (input.textComponent != null)
            {
                input.textComponent.font = CatTheme.Font;
                input.textComponent.color = CatTheme.Ink;
                input.textComponent.supportRichText = false;
            }
        }
    }
}
