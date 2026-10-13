using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Flat cartoon-cat chrome loaded from StreamingAssets/DesktopTheme.
    /// Plates are nine-sliced. Icons stay square.
    /// </summary>
    public static class CatTheme
    {
        public static readonly Color Ink = new Color(0x1B / 255f, 0x3A / 255f, 0x4A / 255f, 1f);
        public static readonly Color Cream = new Color(0xE8 / 255f, 0xD5 / 255f, 0xA3 / 255f, 1f);
        public static readonly Color Coral = new Color(0xE0 / 255f, 0x7A / 255f, 0x5F / 255f, 1f);
        public static readonly Color Teal = new Color(0x2A / 255f, 0x6F / 255f, 0x8F / 255f, 1f);
        public static readonly Color Panel = new Color(0x0F / 255f, 0x18 / 255f, 0x20 / 255f, 1f);

        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public static void ApplyPlate(Image image, string name)
        {
            if (image == null)
                return;
            var border = name != null && name.StartsWith("tab") ? 12f : 16f;
            var sprite = Load(name, new Vector4(border, border, border, border));
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = Color.white;
            image.pixelsPerUnitMultiplier = 1f;
            if (sprite == null)
                image.color = name == "btn-coral" || name == "tab-on" ? Coral : Cream;
        }

        public static void ApplyIcon(Image image, string name)
        {
            if (image == null)
                return;
            var sprite = Load(name, Vector4.zero);
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        static Sprite Load(string name, Vector4 border)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            var key = name + ":" + border.x;
            if (Sprites.TryGetValue(key, out var cached))
                return cached;

            var path = Path.Combine(Application.streamingAssetsPath, "DesktopTheme", name + ".png");
            if (!File.Exists(path))
            {
                Sprites[key] = null;
                return null;
            }

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                Sprites[key] = null;
                return null;
            }

            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);
            Sprites[key] = sprite;
            return sprite;
        }
    }
}
