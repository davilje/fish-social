using UnityEngine;

namespace FishSocial.Desktop
{
    [System.Serializable]
    public sealed class NotificationSettings
    {
        // v2 starts notifications enabled after the first shell's test settings.
        public const string PrefsKey = "fishsocial.desktop.notify.v2";

        public bool EnableNotifications = true;
        public bool DoNotDisturb;
        public bool EnableFishBite = true;
        public bool EnableFriendInvite = true;
        public bool EnableConnectionError = true;

        public static NotificationSettings LoadOrDefault()
        {
            if (!PlayerPrefs.HasKey(PrefsKey))
                return new NotificationSettings();
            try
            {
                var json = PlayerPrefs.GetString(PrefsKey, string.Empty);
                return string.IsNullOrEmpty(json)
                    ? new NotificationSettings()
                    : JsonUtility.FromJson<NotificationSettings>(json) ?? new NotificationSettings();
            }
            catch
            {
                return new NotificationSettings();
            }
        }

        public void Save()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public bool Allows(NotificationKind kind)
        {
            if (!EnableNotifications || DoNotDisturb)
                return false;
            switch (kind)
            {
                case NotificationKind.FishBite: return EnableFishBite;
                case NotificationKind.FriendInvite: return EnableFriendInvite;
                case NotificationKind.ConnectionError: return EnableConnectionError;
                default: return true;
            }
        }
    }
}
