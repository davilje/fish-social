using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FishSocial.Desktop.Pet
{
    public static class PetArtLoader
    {
        public const string DefaultPetId = "orange";

        public static Sprite FallbackPortrait(string petId)
        {
            var id = DesktopDefaultAvatars.NormalizeSkinId(petId);
            var portrait = DesktopDefaultAvatars.Portrait("/image/profile/cat_avatar_" + id + ".png");
            return portrait;
        }

        public static string CurrentOwnPetId()
        {
            var profile = DesktopProfileCache.Latest;
            return DesktopDefaultAvatars.ResolvePetId(profile?.avatarUrl, profile?.playerId);
        }

#if UNITY_EDITOR
        [MenuItem("Fish Social/同步宠物美术到 StreamingAssets + Overlay", false, 40)]
        public static void SyncPetArtMenu()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var repoRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));
            var source = Path.Combine(repoRoot, "desktop-overlay", "OverlayResources");
            if (!Directory.Exists(Path.Combine(source, "fishing")))
            {
                EditorUtility.DisplayDialog("宠物美术", "找不到分层动画目录：\n" + source, "确定");
                return;
            }
            CopyDirectory(Path.Combine(source, "fishing"), Path.Combine(Application.streamingAssetsPath, "Fishing"));
            var overlayDirs = new[]
            {
                Path.Combine(projectRoot, "FishSocialOverlay", "OverlayResources"),
                Path.Combine(projectRoot, "Overlay", "OverlayResources"),
                Path.Combine(projectRoot, "Builds", "Windows64-Debug", "FishSocialOverlay", "OverlayResources"),
                Path.Combine(projectRoot, "Builds", "Windows64", "FishSocialOverlay", "OverlayResources"),
            };
            foreach (var destination in overlayDirs)
            {
                if (Directory.Exists(Path.GetDirectoryName(destination)))
                    CopyDirectory(source, destination);
            }
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("宠物美术", "已同步当前分层动画及 Overlay 资源。", "确定");
        }

        static void CopyDirectory(string source, string destination)
        {
            if (!Directory.Exists(source))
                return;
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta"))
                    continue;
                var relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }
#endif
    }
}
