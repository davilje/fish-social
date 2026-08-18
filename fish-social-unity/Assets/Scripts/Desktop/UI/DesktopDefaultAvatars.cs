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
            new Entry("gray", "cat_avatar_gray.png", "灰猫"),
            new Entry("orange", "cat_avatar_orange.png", "橘猫"),
            new Entry("siamese", "cat_avatar_siamese.png", "暹罗猫"),
            new Entry("tuxedo", "cat_avatar_tuxedo.png", "燕尾猫"),
            new Entry("white", "cat_avatar_white.png", "白猫"),
        };

        public static bool IsDefaultPath(string url)
        {
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

        public static string LabelFor(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "默认头像";
            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Path == url)
                    return All[i].Label;
            }
            return url.StartsWith("data:image/") ? "自定义头像" : "头像";
        }

        public static string InitialFor(PlayerProfileDto profile)
        {
            var nickname = profile != null ? profile.nickname : null;
            if (string.IsNullOrEmpty(nickname))
                return "钓";
            return nickname.Substring(0, 1);
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
