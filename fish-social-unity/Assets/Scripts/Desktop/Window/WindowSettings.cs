using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Non-authoritative window preferences only. Never store economy / inventory / secrets.
    /// </summary>
    [System.Serializable]
    public sealed class WindowSettings
    {
        // v3 drops free-resized v2 geometry. Only the fixed presets below are valid.
        public const string PrefsKey = "fishsocial.desktop.window.v3";

        public const int DefaultWidth = 1920;
        public const int DefaultHeight = 1080;

        public static readonly WindowPreset[] Presets =
        {
            new WindowPreset(1280, 720),
            new WindowPreset(1600, 900),
            new WindowPreset(1920, 1080),
            new WindowPreset(2560, 1440),
        };

        public WindowDisplayMode Mode = WindowDisplayMode.Windowed;
        public int Width = DefaultWidth;
        public int Height = DefaultHeight;
        public int PosX = 80;
        public int PosY = 80;
        public bool HasPosition;
        public bool HideToTrayOnClose;

        public static bool IsPreset(int width, int height)
        {
            for (var i = 0; i < Presets.Length; i++)
            {
                if (Presets[i].Width == width && Presets[i].Height == height)
                    return true;
            }

            return false;
        }

        public static WindowSettings LoadOrDefault()
        {
            if (!PlayerPrefs.HasKey(PrefsKey))
                return new WindowSettings();

            try
            {
                var json = PlayerPrefs.GetString(PrefsKey, string.Empty);
                if (string.IsNullOrEmpty(json))
                    return new WindowSettings();
                var loaded = JsonUtility.FromJson<WindowSettings>(json);
                return loaded ?? new WindowSettings();
            }
            catch
            {
                return new WindowSettings();
            }
        }

        public void Save()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public void ClampSize()
        {
            if (IsPreset(Width, Height))
                return;
            Width = DefaultWidth;
            Height = DefaultHeight;
        }

        public readonly struct WindowPreset
        {
            public readonly int Width;
            public readonly int Height;

            public WindowPreset(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public string Label => Width + "×" + Height;
        }
    }
}
