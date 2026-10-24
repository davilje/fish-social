using System;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Game-profile default avatars. Paths match shared/defaultAvatars.ts;
    /// Steam account avatars are never used as a substitute.
    /// </summary>
    public static class DesktopDefaultAvatars
    {
        public const string Directory = "/image/profile";
        static readonly string[] LegacySkins = { "calico", "orange", "orange", "orange", "orange", "white" };

        public sealed class Entry
        {
            public readonly string Id;
            public readonly string Filename;
            public readonly string Label;
            public readonly string Path;

            public Entry(string id, string filename, string label)
            {
                Id = id;
                Filename = filename;
                Label = label;
                Path = Directory + "/" + filename;
            }
        }

        public static readonly Entry[] All =
        {
            new Entry("calico", "cat_avatar_calico.png", "三花猫"),
            new Entry("orange", "cat_avatar_orange.png", "橘猫"),
            new Entry("white", "cat_avatar_white.png", "白猫"),
            new Entry("silver", "cat_avatar_silver.png", "银渐层"),
            new Entry("brown", "cat_avatar_brown.png", "棕虎斑"),
            new Entry("cream", "cat_avatar_cream.png", "奶油猫"),
            new Entry("blue", "cat_avatar_blue.png", "英短蓝猫"),
            new Entry("black", "cat_avatar_black.png", "黑猫"),
            new Entry("cow", "cat_avatar_cow.png", "奶牛猫"),
            new Entry("dafeiyu", "cat_avatar_dafeiyu.png", "大肥鱼"),
            new Entry("sangbiao", "cat_avatar_sangbiao.png", "狸花猫「丧彪」"),
        };

        public static bool IsDefaultPath(string url)
        {
            url = NormalizeAssetPath(url);
            if (string.IsNullOrEmpty(url) || !url.StartsWith(Directory + "/"))
                return false;
            var filename = url.Substring(Directory.Length + 1);
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Filename == filename)
                    return true;
            }
            return false;
        }

        public static UnityEngine.Sprite Portrait(string avatarUrl, string playerId = null)
        {
            var id = ResolvePetId(avatarUrl, playerId);
            return CatTheme.LoadCozy("characters/avatars/" + id + ".png", UnityEngine.Vector4.zero)
                ?? CatTheme.LoadCozy("characters/avatars/orange.png", UnityEngine.Vector4.zero);
        }

        public static string LabelFor(string url)
        {
            url = NormalizeAssetPath(url);
            if (string.IsNullOrEmpty(url))
                return "默认头像";
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Path == url)
                    return All[i].Label;
            }
            return url.StartsWith("data:image/") ? "自定义头像" : "头像";
        }

        /// <summary>
        /// Stable pet art id shared by Unity and Overlay (orange, calico, …).
        /// Retired cat identities resolve to the current orange artwork.
        /// </summary>
        public static string ResolvePetId(string avatarUrl, string playerId)
        {
            var fromUrl = TryPetIdFromUrl(avatarUrl);
            if (!string.IsNullOrEmpty(fromUrl))
                return fromUrl;

            var index = 0;
            if (!string.IsNullOrEmpty(playerId))
            {
                var hash = 0;
                for (var i = 0; i < playerId.Length; i++)
                {
                    hash = ((hash << 5) - hash) + playerId[i];
                    hash |= 0;
                }
                index = (int)(Math.Abs((long)hash) % LegacySkins.Length);
            }
            return LegacySkins[index];
        }

        static string TryPetIdFromUrl(string url)
        {
            url = NormalizeAssetPath(url);
            if (string.IsNullOrEmpty(url))
                return null;

            const string photoPrefix = "/image/fishing_photos/cat_fishing_";
            if (url.StartsWith(photoPrefix) && url.EndsWith(".png"))
            {
                var id = url.Substring(photoPrefix.Length, url.Length - photoPrefix.Length - 4);
                return KnownPetId(id);
            }

            if (url.StartsWith(Directory + "/"))
            {
                var filename = url.Substring(Directory.Length + 1);
                for (var i = 0; i < All.Length; i++)
                {
                    if (All[i].Filename == filename)
                        return All[i].Id;
                }
            }

            return null;
        }

        static string KnownPetId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id)
                    return All[i].Id;
            }
            return null;
        }

        public static string NormalizeSkinId(string id)
        {
            return KnownPetId(id) ?? "orange";
        }

        public static string NormalizeAssetPath(string url)
        {
            foreach (var id in new[] { "gray", "siamese", "tuxedo" })
            {
                if (url == Directory + "/cat_avatar_" + id + ".png")
                    return Directory + "/cat_avatar_orange.png";
                if (url == "/image/fishing_photos/cat_fishing_" + id + ".png")
                    return "/image/fishing_photos/cat_fishing_orange.png";
            }
            return url;
        }

        public static string InitialFor(PlayerProfileDto profile)
        {
            var nickname = profile != null ? profile.nickname : null;
            if (string.IsNullOrEmpty(nickname))
                return "钓";
            return nickname.Substring(0, 1);
        }

        /// <summary>
        /// Mirrors shared/fishingPhotos.ts — same paths as social feed epic catch photos.
        /// </summary>
        public static string ResolveFishingPhotoPath(string avatarUrl, string playerId)
        {
            const string photoDir = "/image/fishing_photos";
            if (!string.IsNullOrEmpty(avatarUrl) && avatarUrl.StartsWith(photoDir + "/"))
                return NormalizeAssetPath(avatarUrl);
            return photoDir + "/cat_fishing_" + ResolvePetId(avatarUrl, playerId) + ".png";
        }
    }

    public static class DesktopProfileCache
    {
        public static PlayerProfileDto Latest;

        public static string[] Slots(PlayerProfileDto profile)
        {
            var slots = new string[PlayerProfileDto.ShowcaseSlotCount];
            var source = profile != null ? profile.showcaseFishIds : null;
            for (var i = 0; i < slots.Length; i++)
            {
                slots[i] = source != null && i < source.Length && !string.IsNullOrEmpty(source[i])
                    ? source[i]
                    : string.Empty;
            }
            return slots;
        }

        public static string FishLabel(FishInventoryItemDto item)
        {
            if (item == null)
                return "空";
            return DesktopFishCatalog.SpeciesName(item.speciesId) + "\n" +
                   DesktopFishCatalog.QualityName(item.quality);
        }

        public static string OnlineLabel(SocialPondSessionController pond, bool authenticated)
        {
            if (!authenticated)
                return "离线";
            if (pond != null && pond.State == SocialSocketState.Connected)
            {
                var pondName = pond.LatestSnapshot != null && pond.LatestSnapshot.pond != null
                    ? pond.LatestSnapshot.pond.name
                    : pond.CurrentPondId;
                return string.IsNullOrEmpty(pondName) ? "在线" : "在线 · " + pondName;
            }
            return "在线";
        }
    }
}
