using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Non-authoritative window preferences only. Never store economy / inventory / secrets.
    /// </summary>
    [System.Serializable]
    public sealed class WindowSettings
    {
        // v2 resets the pre-borderless geometry format so a bad old resolution
        // cannot be restored after switching from fullscreen.
        public const string PrefsKey = "fishsocial.desktop.window.v2";

        public WindowDisplayMode Mode = WindowDisplayMode.Windowed;
        public int Width = 1280;
        public int Height = 720;
        public int PosX = 80;
        public int PosY = 80;
        public bool HasPosition;

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
            Width = Mathf.Clamp(Width, 960, 7680);
            Height = Mathf.Clamp(Height, 540, 4320);
        }
    }
}
