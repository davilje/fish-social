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
        public static readonly Color Coral = new Color(0xFC / 255f, 0x92 / 255f, 0x3C / 255f, 1f);
        public static readonly Color Teal = new Color(0x92 / 255f, 0xA3 / 255f, 0xE7 / 255f, 1f);
        public static readonly Color Panel = Cream;
        public static readonly Vector4 PlateBorder = new Vector4(12f, 12f, 12f, 12f);
        public static readonly Vector4 PlateLargeBorder = new Vector4(18f, 18f, 18f, 18f);
        public static readonly Vector4 PaperBorder = new Vector4(22f, 22f, 22f, 22f);
        public static readonly Vector4 CompactPaperBorder = new Vector4(10f, 10f, 10f, 10f);
        public static readonly Vector4 WindowBorder = new Vector4(32f, 32f, 32f, 32f);
        public static readonly Vector4 TrackBorder = new Vector4(12f, 12f, 12f, 12f);
        public static readonly Vector4 ChipBorder = new Vector4(18f, 18f, 18f, 18f);

        public static Vector4 SurfaceBorder(string assetPath)
        {
            var file = string.IsNullOrEmpty(assetPath) ? string.Empty : Path.GetFileName(assetPath);
            if (file.StartsWith("plate-large", StringComparison.OrdinalIgnoreCase))
                return PlateLargeBorder;
            if (file.StartsWith("plate-", StringComparison.OrdinalIgnoreCase))
                return PlateBorder;
            if (file.Equals("paper-compact.png", StringComparison.OrdinalIgnoreCase))
                return CompactPaperBorder;
            if (file.Equals("window.png", StringComparison.OrdinalIgnoreCase))
                return WindowBorder;
            if (file.Equals("track.png", StringComparison.OrdinalIgnoreCase)
                || file.Equals("track-fill.png", StringComparison.OrdinalIgnoreCase))
                return TrackBorder;
            if (file.Equals("chip.png", StringComparison.OrdinalIgnoreCase))
                return ChipBorder;
            if (file.Equals("paper.png", StringComparison.OrdinalIgnoreCase))
                return PaperBorder;
            return Vector4.zero;
        }

        public static bool AllowLegacyStreamingAssetsFallback { get; set; }

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
            var resourcePath = CozyResourcePath(relativePath);
            if (resourcePath == null)
                return null;
            var imported = Resources.Load<Sprite>(resourcePath);
            if (imported != null)
                return imported;
            if (!AllowLegacyStreamingAssetsFallback || !Application.isPlaying)
                return null;
            return LoadLegacyCozy(relativePath, border);
        }

        public static string CozyResourcePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;
            var normalized = relativePath.Replace('\\', '/');
            if (Path.IsPathRooted(normalized) || normalized.Contains(":")
                || Array.Exists(normalized.Split('/'), segment => segment == ".." || segment == "."
                    || string.IsNullOrWhiteSpace(segment)))
                return null;
            var extension = Path.GetExtension(normalized);
            if (!string.IsNullOrEmpty(extension))
            {
                if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
                    return null;
                normalized = normalized.Substring(0, normalized.Length - extension.Length);
            }
            return "Desktop/Theme/Cozy/" + normalized;
        }

        static Sprite LoadLegacyCozy(string relativePath, Vector4 border)
        {
            var normalized = relativePath.Replace('\\', '/');
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
            if (image == null || IsProtectedImage(image) || image.GetComponentInParent<Selectable>(true) != null)
                return;
            image.overrideSprite = null;
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = color;
        }

        public static void ApplyPaper(Image image)
        {
            if (image == null || IsProtectedImage(image) || image.GetComponentInParent<Selectable>(true) != null)
                return;
            ApplyPaperSurface(image);
        }

        static void ApplyPaperSurface(Image image)
        {
            var color = Cream;
            color.a = image.color.a;
            image.overrideSprite = null;
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = color;
            var size = image.rectTransform.rect.size;
            var compact = Mathf.Min(size.x, size.y) > 0f && Mathf.Min(size.x, size.y) < 48f;
            var paper = LoadCozy(compact ? "panels/paper-compact.png" : "panels/paper.png",
                compact ? CompactPaperBorder : PaperBorder);
            if (paper == null)
                return;
            image.sprite = paper;
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, color.a);
        }

        public static void ApplySlicedImage(Image image)
        {
            if (image == null || image.type == Image.Type.Filled)
                return;
            var sprite = image.overrideSprite != null ? image.overrideSprite : image.sprite;
            if (sprite == null || sprite.border == Vector4.zero)
                return;
            image.type = Image.Type.Sliced;
            image.preserveAspect = false;
        }

        public static void StyleButton(Button button, string iconName = null, bool primary = false)
        {
            if (button == null || button.GetComponent<Mask>() != null || button.GetComponent<RectMask2D>() != null
                || button.GetComponent<ScrollRect>() != null
                || button.GetComponentInParent<InputField>(true) != null)
                return;
            if (button.name == "FeatureModalRoot" || button.name == "ProductContextMenu" || button.name == "PopupProductMenu"
                || button.name == "ModalPanel" || button.name == "Panel" || button.name == "Dim" || button.name == "MenuPanel")
                return;
            var background = button.GetComponent<Image>();
            if (background != null && background.type == Image.Type.Filled)
                return;
            if (button.targetGraphic is Image targetImage && IsProtectedImage(targetImage))
                return;
            iconName = string.IsNullOrEmpty(iconName) ? IconNameForButton(button) : iconName;
            var visual = button.GetComponent<CozyButtonVisual>();
            if (visual == null)
                visual = button.gameObject.AddComponent<CozyButtonVisual>();
            visual.Configure(button, iconName, primary);
        }

        static string IconNameForButton(Button button)
        {
            if (button == null)
                return null;
            switch (button.name)
            {
                case "Settings": case "MenuSettings": return "settings";
                case "Mail": case "Notifications": return "bell";
                case "Back": return "home";
                case "Public": case "Share": return "social";
                case "Friends": return "user";
                case "Bait": return "leaf";
                case "Tackle": return "rod";
                case "Vessel": return "pond";
                case "Buy": return "coin";
                case "Equip": return "bag";
                case "Sell": return "fish";
                case "StartFishing": return "rod";
                case "ExploreWorld": return "map";
                case "DailyGift": return "gift";
                case "SelectPond": return "pond";
                case "Edit": return "user";
                default: return null;
            }
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
            if (image.pixelsPerUnitMultiplier <= 0f || float.IsNaN(image.pixelsPerUnitMultiplier)
                || float.IsInfinity(image.pixelsPerUnitMultiplier))
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

        internal static void ApplyTextFont(Text label)
        {
            if (label == null)
                return;
            var font = Font;
            if (font != null && HasGlyphs(font, label.text))
                label.font = font;
        }

        internal static void SkinText(Text label)
        {
            if (label == null)
                return;
            ApplyTextFont(label);
            if (label.GetComponent<CozyGraphicOffset>() != null || label.GetComponentInParent<Button>(true) != null)
                return;
            if (IsNeutral(label.color))
            {
                var color = Ink;
                color.a = label.color.a;
                label.color = color;
            }
        }

        static void SkinNode(Transform node, CozyPanelSkin skin)
        {
            ApplySlicedImage(node.GetComponent<Image>());
            var input = node.GetComponentInParent<InputField>(true);
            if (input != null)
            {
                SkinInputNode(node, input, skin);
                return;
            }
            var button = node.GetComponent<Button>();
            if (button != null)
            {
                var firstStyle = skin.MarkStyled(button);
                var visual = button.GetComponent<CozyButtonVisual>();
                if (visual != null)
                    visual.Configure(button, null, false);
                else if (firstStyle)
                    StyleButton(button);
            }
            var text = node.GetComponent<Text>();
            if (text != null && skin.MarkStyled(text))
                SkinText(text);
            if (button != null)
                return;
            var image = node.GetComponent<Image>();
            if (image == null || !skin.MarkStyled(image))
                return;
            if (ApplyLegacySurface(image))
                return;
            var scroll = node.GetComponentInParent<ScrollRect>(true);
            if (scroll != null && (scroll.transform == node || scroll.viewport == node))
            {
                if (image.type != Image.Type.Filled && image.color.a >= 0.8f && IsNeutral(image.color)
                    && Mathf.Max(image.color.r, Mathf.Max(image.color.g, image.color.b)) < 0.5f)
                    ApplyCreamTint(image);
                return;
            }
            if (IsProtectedImage(image)
                || node.GetComponentInParent<Selectable>(true) != null
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

        public static bool ApplyLegacySurface(Image image)
        {
            if (image == null || image.color.a < 0.8f || IsProtectedImage(image)
                || !IsChromeSprite(image.overrideSprite != null ? image.overrideSprite : image.sprite) || image.material != null
                    && image.material != Graphic.defaultGraphicMaterial)
                return false;
            var name = image.name.ToLowerInvariant();
            if (name != "panelworldmap" && name != "shell" && name != "albumstage"
                && name != "bg" && name != "photo" && !name.StartsWith("tab_", StringComparison.Ordinal))
                return false;
            var selectable = image.GetComponentInParent<Selectable>(true);
            if (selectable != null && !(name == "bg" && selectable is Toggle))
                return false;
            var color = image.color;
            var knownColor = SameLegacyRgb(color, Cream);
            foreach (var legacy in new[] { new Color(0.07f, 0.1f, 0.14f), new Color(0.09f, 0.12f, 0.16f),
                new Color(0.1f, 0.13f, 0.17f), new Color(0.06f, 0.08f, 0.1f),
                new Color(0.25f, 0.3f, 0.35f), new Color(0.08f, 0.11f, 0.14f) })
                knownColor |= SameLegacyRgb(color, legacy);
            if (!knownColor)
                return false;
            if (name == "photo")
                ApplyCreamTint(image);
            else
                ApplyPaperSurface(image);
            return true;
        }

        static bool SameLegacyRgb(Color first, Color second)
        {
            return Mathf.Abs(first.r - second.r) < 0.0001f && Mathf.Abs(first.g - second.g) < 0.0001f
                && Mathf.Abs(first.b - second.b) < 0.0001f;
        }

        static bool IsProtectedImage(Image image)
        {
            return image.type == Image.Type.Filled || image.GetComponent<Mask>() != null
                || image.GetComponent<RectMask2D>() != null || image.GetComponent<ScrollRect>() != null
                || image.GetComponentInParent<InputField>(true) != null;
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
            foreach (var resourcePath in new[] { "Desktop/Theme/Cozy/fonts", "Desktop/Theme/Cozy/Fonts",
                "DesktopTheme/Cozy/Fonts", "DesktopTheme/Fonts", "Fonts" })
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
