using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    public static class CatTheme
    {
        public static readonly Color Ink = new Color(0x75 / 255f, 0x47 / 255f, 0x2E / 255f, 1f);
        public static readonly Color Cream = new Color(0xFF / 255f, 0xF7 / 255f, 0xE9 / 255f, 1f);
        public static readonly Color Coral = new Color(0xF9 / 255f, 0xA3 / 255f, 0x3F / 255f, 1f);
        public static readonly Color Teal = new Color(0x92 / 255f, 0xA3 / 255f, 0xE7 / 255f, 1f);
        public static readonly Color Panel = Cream;
        public static readonly Vector4 PlateBorder = new Vector4(100f, 100f, 100f, 100f);

        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static readonly HashSet<string> FailedSprites = new HashSet<string>();
        static Font themeFont;
        static bool fontLoaded;

        public static Font Font
        {
            get
            {
                if (!fontLoaded)
                {
                    fontLoaded = true;
                    themeFont = LoadFont();
                }
                return themeFont;
            }
            set
            {
                themeFont = value;
                fontLoaded = true;
            }
        }

        public static Sprite LoadCozy(string relativePath, Vector4 border)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;
            var normalized = relativePath.Replace('\\', '/');
            if (Path.IsPathRooted(normalized) || normalized.Contains(":")
                || Array.Exists(normalized.Split('/'), segment => segment == ".."))
                return null;
            if (string.IsNullOrEmpty(Path.GetExtension(normalized)))
                normalized += ".png";
            var path = Path.Combine(Application.streamingAssetsPath, "DesktopTheme", "Cozy", normalized);
            var key = path + ":" + border.x.ToString("R", CultureInfo.InvariantCulture)
                + ":" + border.y.ToString("R", CultureInfo.InvariantCulture)
                + ":" + border.z.ToString("R", CultureInfo.InvariantCulture)
                + ":" + border.w.ToString("R", CultureInfo.InvariantCulture);
            if (Sprites.TryGetValue(key, out var cached))
                return cached;
            if (FailedSprites.Contains(key) || !File.Exists(path))
                return null;
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    UnityEngine.Object.Destroy(texture);
                    FailedSprites.Add(key);
                    return null;
                }
                texture.name = normalized;
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                var horizontalScale = Mathf.Min(1f, texture.width / Mathf.Max(1f, border.x + border.z));
                var verticalScale = Mathf.Min(1f, texture.height / Mathf.Max(1f, border.y + border.w));
                var safeBorder = new Vector4(Mathf.Max(0f, border.x) * horizontalScale,
                    Mathf.Max(0f, border.y) * verticalScale, Mathf.Max(0f, border.z) * horizontalScale,
                    Mathf.Max(0f, border.w) * verticalScale);
                var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, safeBorder);
                sprite.name = normalized;
                Sprites[key] = sprite;
                return sprite;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is UnityException)
            {
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
                FailedSprites.Add(key);
                Debug.LogWarning("Cozy theme could not load " + normalized + ": " + exception.Message);
                return null;
            }
        }

        public static void ApplyPlate(Image image, string name)
        {
            if (image == null || IsProtectedImage(image))
                return;
            var button = image.GetComponent<Button>();
            if (button != null)
            {
                var primary = name == "btn-coral" || name == "btn-teal";
                StyleButton(button, null, primary);
                var visual = button.GetComponent<CozyButtonVisual>();
                if (visual == null)
                    return;
                visual.SetTint(name == "btn-teal" ? Teal : primary ? Coral : Color.white);
                if (name == "tab-on" || name == "tab")
                    visual.SetSelected(name == "tab-on");
                return;
            }
            var path = name == "plate-hover" || name == "tab-on" ? "buttons/plate-hover.png"
                : name == "plate-pressed" ? "buttons/plate-pressed.png" : "buttons/plate-default.png";
            SetPlate(image, LoadCozy(path, PlateBorder),
                name == "btn-coral" ? Coral : name == "btn-teal" ? Teal : Color.white);
        }

        public static void ApplyIcon(Image image, string name)
        {
            if (image == null || IsProtectedImage(image))
                return;
            var button = image.GetComponent<Button>();
            if (button != null)
            {
                var visual = button.GetComponent<CozyButtonVisual>();
                if (visual == null)
                    StyleButton(button, name);
                else
                {
                    visual.SetIconName(name);
                    visual.RefreshContent();
                }
                return;
            }
            var sprite = LoadCozy(IconPath(name), Vector4.zero);
            if (sprite == null)
                return;
            image.overrideSprite = null;
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        public static void ApplySurface(Image image, Color color)
        {
            if (image == null || IsProtectedImage(image) || image.GetComponentInParent<Selectable>() != null)
                return;
            image.overrideSprite = null;
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = color;
        }

        public static void ApplyPaper(Image image)
        {
            if (image == null || IsProtectedImage(image) || image.GetComponentInParent<Selectable>() != null)
                return;
            var color = Cream;
            color.a = image.color.a;
            ApplySurface(image, color);
            var paper = LoadCozy("textures/paper.png", Vector4.zero);
            if (paper == null)
                return;
            image.sprite = paper;
            image.color = new Color(1f, 1f, 1f, color.a);
        }

        public static void StyleButton(Button button, string iconName = null, bool primary = false)
        {
            if (button == null || button.GetComponent<Mask>() != null || button.GetComponent<RectMask2D>() != null
                || button.GetComponent<ScrollRect>() != null
                || button.GetComponentInParent<InputField>() != null)
                return;
            var background = button.GetComponent<Image>();
            if (background != null && background.type == Image.Type.Filled)
                return;
            if (button.targetGraphic is Image targetImage && IsProtectedImage(targetImage))
                return;
            var visual = button.GetComponent<CozyButtonVisual>();
            if (visual == null)
                visual = button.gameObject.AddComponent<CozyButtonVisual>();
            visual.Configure(button, iconName, primary);
        }

        public static void SkinTree(Transform root)
        {
            if (root == null)
                return;
            var skin = root.GetComponent<CozyPanelSkin>();
            if (skin == null)
                skin = root.gameObject.AddComponent<CozyPanelSkin>();
            skin.MarkSkinned();
            SkinNode(root, skin);
            for (var childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                var child = root.GetChild(childIndex);
                if (!IsGeneratedVisual(child))
                    SkinTree(child);
            }
            skin.RememberChildren();
        }

        internal static string IconPath(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            var normalized = name.Replace('\\', '/');
            if (normalized.StartsWith("icon-", StringComparison.Ordinal))
                normalized = normalized.Substring(5);
            return normalized.StartsWith("icons/", StringComparison.Ordinal) ? normalized : "icons/" + normalized;
        }

        internal static void SetPlate(Image image, Sprite sprite, Color tint)
        {
            image.overrideSprite = null;
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = false;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = sprite != null ? tint : tint == Color.white ? Cream : tint;
        }

        internal static bool IsGeneratedVisual(Transform node)
        {
            if (node.parent == null)
                return false;
            var visual = node.parent.GetComponent<CozyButtonVisual>();
            return visual != null && visual.Owns(node);
        }

        internal static void SkinText(Text label)
        {
            var font = Font;
            if (font != null && HasGlyphs(font, label.text))
                label.font = font;
            if (IsNeutral(label.color))
            {
                var color = Ink;
                color.a = label.color.a;
                label.color = color;
            }
        }

        static void SkinNode(Transform node, CozyPanelSkin skin)
        {
            var input = node.GetComponentInParent<InputField>();
            if (input != null)
            {
                SkinInputNode(node, input, skin);
                return;
            }
            var text = node.GetComponent<Text>();
            if (text != null && skin.MarkStyled(text) && text.GetComponent<CozyGraphicOffset>() == null)
                SkinText(text);
            var button = node.GetComponent<Button>();
            if (button != null)
            {
                if (skin.MarkStyled(button) && button.GetComponent<CozyButtonVisual>() == null)
                    StyleButton(button);
                return;
            }
            var image = node.GetComponent<Image>();
            if (image == null || !skin.MarkStyled(image))
                return;
            var scroll = node.GetComponentInParent<ScrollRect>();
            if (scroll != null && (scroll.transform == node || scroll.viewport == node))
            {
                if (image.type != Image.Type.Filled && image.color.a >= 0.8f && IsNeutral(image.color)
                    && Mathf.Max(image.color.r, Mathf.Max(image.color.g, image.color.b)) < 0.5f)
                    ApplyCreamTint(image);
                return;
            }
            if (IsProtectedImage(image)
                || node.GetComponentInParent<Selectable>() != null
                || image.color.a < 0.8f || !IsNeutral(image.color))
                return;
            var name = node.name.ToLowerInvariant();
            if (name.Contains("progress") || name.Contains("fill") || name.Contains("avatar")
                || name.Contains("icon") || name.Contains("dimmer") || name.Contains("overlay")
                || name.Contains("backdrop") || !IsChromeSprite(image.sprite))
                return;
            if (!(name.Contains("panel") || name.Contains("background") || name.Contains("header")
                || name.Contains("content") || name.Contains("paper") || name.Contains("card")
                || name.Contains("row") || name.Contains("slot") || name.Contains("page")
                || name.Contains("body") || name.Contains("window") || name.Contains("section")
                || name.Contains("footer") || name.Contains("nav") || name.Contains("toolbar")
                || name.Contains("root")))
                return;
            ApplyPaper(image);
        }

        static void SkinInputNode(Transform node, InputField input, CozyPanelSkin skin)
        {
            if (node == input.transform)
            {
                var background = node.GetComponent<Image>();
                if (background != null && skin.MarkStyled(background) && background.type != Image.Type.Filled)
                    ApplyCreamTint(background);
            }
            var label = node.GetComponent<Text>();
            if (label == null || (label != input.textComponent && label != input.placeholder)
                || !skin.MarkStyled(label))
                return;
            SkinText(label);
            var color = Ink;
            color.a = label == input.placeholder ? Mathf.Min(0.55f, label.color.a) : label.color.a;
            label.color = color;
        }

        static void ApplyCreamTint(Image image)
        {
            var color = Cream;
            color.a = image.color.a;
            image.color = color;
        }

        static bool IsProtectedImage(Image image)
        {
            return image.type == Image.Type.Filled || image.GetComponent<Mask>() != null
                || image.GetComponent<RectMask2D>() != null || image.GetComponent<ScrollRect>() != null
                || image.GetComponentInParent<InputField>() != null;
        }

        static bool IsChromeSprite(Sprite sprite)
        {
            if (sprite == null)
                return true;
            return sprite.name == "Background" || sprite.name == "UISprite" || sprite.name == "UIMask"
                || sprite.name == "panel" || sprite.name == "paper" || sprite.name == "tab"
                || sprite.name == "tab-on" || sprite.name == "btn-cream" || sprite.name == "btn-coral";
        }

        static bool IsNeutral(Color color)
        {
            var spread = Mathf.Max(color.r, Mathf.Max(color.g, color.b))
                - Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            return spread < 0.08f || SameRgb(color, Cream) || SameRgb(color, Ink)
                || SameRgb(color, new Color(0xE8 / 255f, 0xD5 / 255f, 0xA3 / 255f))
                || (color.r < 0.3f && color.g < 0.4f && color.b < 0.45f
                    && color.b >= color.g && color.g >= color.r);
        }

        static bool SameRgb(Color first, Color second)
        {
            return Mathf.Abs(first.r - second.r) < 0.02f && Mathf.Abs(first.g - second.g) < 0.02f
                && Mathf.Abs(first.b - second.b) < 0.02f;
        }

        static Font LoadFont()
        {
            foreach (var resourcePath in new[] { "DesktopTheme/Cozy/Fonts", "DesktopTheme/Fonts", "Fonts" })
            {
                var resources = Resources.LoadAll<Font>(resourcePath);
                Array.Sort(resources, (first, second) => string.CompareOrdinal(first.name, second.name));
                foreach (var candidate in resources)
                {
                    if (candidate.HasCharacter('中') && candidate.HasCharacter('文'))
                        return candidate;
                }
            }
            var directory = Path.Combine(Application.streamingAssetsPath, "DesktopTheme", "Cozy", "Fonts");
            try
            {
                var paths = Directory.Exists(directory) ? Directory.GetFiles(directory) : new string[0];
                Array.Sort(paths, StringComparer.Ordinal);
                foreach (var path in paths)
                {
                    var extension = Path.GetExtension(path);
                    if (!string.Equals(extension, ".bundle", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(extension, ".assetbundle", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var bundle = AssetBundle.LoadFromFile(path);
                    if (bundle == null)
                        continue;
                    Font selected = null;
                    try
                    {
                        foreach (var candidate in bundle.LoadAllAssets<Font>())
                        {
                            if (candidate.HasCharacter('中') && candidate.HasCharacter('文'))
                            {
                                selected = candidate;
                                break;
                            }
                        }
                    }
                    finally
                    {
                        bundle.Unload(selected == null);
                    }
                    if (selected != null)
                        return selected;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is UnityException)
            {
                Debug.LogWarning("Cozy theme font loading skipped: " + exception.Message);
            }
            Font systemFont = null;
            try
            {
                systemFont = UnityEngine.Font.CreateDynamicFontFromOSFont(new[]
                {
                    "Microsoft YaHei", "PingFang SC", "Heiti SC", "Noto Sans CJK SC", "Arial Unicode MS"
                }, 18);
                if (systemFont != null && systemFont.HasCharacter('中') && systemFont.HasCharacter('文'))
                    return systemFont;
                Debug.LogWarning("Cozy theme could not find an installed Chinese system font; existing fonts are unchanged.");
            }
            catch (Exception exception) when (exception is UnityException || exception is ArgumentException
                || exception is NotSupportedException)
            {
                Debug.LogWarning("Cozy theme system font loading skipped: " + exception.Message);
            }
            if (systemFont != null)
                UnityEngine.Object.Destroy(systemFont);
            return null;
        }

        static bool HasGlyphs(Font font, string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;
            foreach (var character in text)
            {
                if (!char.IsControl(character) && !char.IsWhiteSpace(character) && !font.HasCharacter(character))
                    return false;
            }
            return true;
        }
    }
}
