using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FishSocial.Desktop.Pet
{
    /// <summary>
    /// Loads per-cat pose frames. Same paths as Overlay:
    /// pets/&lt;petId&gt;/&lt;clip&gt;/N.png with legacy flat fallbacks.
    /// </summary>
    public static class PetArtLoader
    {
        public const string DefaultPetId = "orange";

        static readonly Dictionary<string, Sprite[]> Cache =
            new Dictionary<string, Sprite[]>();

        public static Sprite[] GetFrames(string petId, PetVisualState state)
        {
            var id = SanitizePetId(petId);
            var wire = PetStateController.ToWire(state);
            var key = id + "|" + wire;
            Sprite[] frames;
            if (Cache.TryGetValue(key, out frames) && frames != null && frames.Length > 0)
                return frames;

            frames = LoadSequence(id, wire);
            if (frames.Length == 0 && wire == "sit")
                frames = LoadSequence(id, "fishing");
            if (frames.Length == 0 && wire == "catch")
                frames = LoadSequence(id, "reel");
            if (frames.Length == 0 && wire != "idle")
                frames = LoadSequence(id, "idle");
            if (frames.Length == 0 && wire != "fishing")
                frames = LoadSequence(id, "fishing");
            if (frames.Length == 0)
                frames = LoadSingle(id, "cat");
            Cache[key] = frames;
            return frames;
        }

        public static string CurrentOwnPetId()
        {
            var profile = DesktopProfileCache.Latest;
            return DesktopDefaultAvatars.ResolvePetId(
                profile != null ? profile.avatarUrl : null,
                profile != null ? profile.playerId : null);
        }

        static string SanitizePetId(string petId)
        {
            if (string.IsNullOrEmpty(petId))
                return DefaultPetId;
            for (var i = 0; i < petId.Length; i++)
            {
                var c = petId[i];
                if (!(c >= 'a' && c <= 'z') &&
                    !(c >= '0' && c <= '9') &&
                    c != '-' && c != '_')
                    return DefaultPetId;
            }
            return petId;
        }

        static Sprite[] LoadSequence(string petId, string clip)
        {
            var list = new List<Sprite>();
            AppendClipDirectory(list, petId, clip);
            if (list.Count == 0)
            {
                for (var i = 0; i < 16; i++)
                {
                    var sprite = LoadPng(petId, clip + "-" + i);
                    if (sprite == null)
                        break;
                    list.Add(sprite);
                }
            }
            if (list.Count == 0)
            {
                var single = LoadPng(petId, clip);
                if (single != null)
                    list.Add(single);
            }

            return list.ToArray();
        }

        static void AppendClipDirectory(List<Sprite> list, string petId, string clip)
        {
            for (var i = 0; i < 16; i++)
            {
                var sprite = LoadPng(petId, Path.Combine(clip, i.ToString()));
                if (sprite == null)
                    break;
                list.Add(sprite);
            }
        }

        static Sprite[] LoadSingle(string petId, string stem)
        {
            var sprite = LoadPng(petId, stem);
            return sprite != null ? new[] { sprite } : new Sprite[0];
        }

        static Sprite LoadPng(string petId, string stem)
        {
            var relative = Path.Combine(petId, stem + ".png");
            foreach (var root in SearchRoots())
            {
                var path = Path.Combine(root, relative);
                if (!File.Exists(path))
                    continue;
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    name = petId + "-" + stem,
                };
                if (!tex.LoadImage(bytes))
                    continue;
                return Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
            return null;
        }

        static IEnumerable<string> SearchRoots()
        {
            yield return Path.Combine(Application.streamingAssetsPath, "Pet");
#if UNITY_EDITOR
            yield return Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "desktop-overlay", "OverlayResources", "pets"));
#endif
        }

#if UNITY_EDITOR
        [MenuItem("Fish Social/同步宠物美术到 StreamingAssets + Overlay", false, 40)]
        public static void SyncPetArtMenu()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var repoRoot = Path.GetFullPath(Path.Combine(projectRoot, ".."));
            var source = Path.Combine(repoRoot, "desktop-overlay", "OverlayResources");
            if (!Directory.Exists(source))
            {
                EditorUtility.DisplayDialog("宠物美术", "找不到：\n" + source, "确定");
                return;
            }

            var streaming = Path.Combine(Application.dataPath, "StreamingAssets");
            CopyDirectory(Path.Combine(source, "pets"), Path.Combine(streaming, "Pet"));

            var overlayDirs = new[]
            {
                Path.Combine(projectRoot, "FishSocialOverlay", "OverlayResources"),
                Path.Combine(projectRoot, "Overlay", "OverlayResources"),
                Path.Combine(projectRoot, "Builds", "Windows64-Debug", "FishSocialOverlay", "OverlayResources"),
                Path.Combine(projectRoot, "Builds", "Windows64", "FishSocialOverlay", "OverlayResources"),
            };
            foreach (var dest in overlayDirs)
            {
                var exeDir = Path.GetDirectoryName(dest);
                if (exeDir != null && Directory.Exists(exeDir))
                    CopyDirectory(source, dest);
            }

            AssetDatabase.Refresh();
            Cache.Clear();
            EditorUtility.DisplayDialog(
                "宠物美术",
                "已从 desktop-overlay/OverlayResources 同步到 StreamingAssets/Pet 以及已存在的 Overlay 输出目录。",
                "确定");
        }

        static void CopyDirectory(string source, string dest)
        {
            if (!Directory.Exists(source))
                return;
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta"))
                    continue;
                var rel = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var target = Path.Combine(dest, rel);
                var dir = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.Copy(file, target, true);
            }
        }
#endif
    }
}
