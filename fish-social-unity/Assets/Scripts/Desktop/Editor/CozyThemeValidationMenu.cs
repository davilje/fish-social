#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FishSocial.Desktop.Editor
{
    public static class CozyThemeValidationMenu
    {
        [MenuItem("Fish Social/Cozy UI/检查最终版素材")]
        public static void ValidateAssets()
        {
            var directory = Path.Combine(Application.streamingAssetsPath, "DesktopTheme", "Cozy");
            var errors = 0;
            foreach (var icon in new[] { "home", "map", "pond", "hook", "bag", "rod", "shop", "medal", "trophy", "user", "social" })
                CheckFile(directory, "icons/" + icon + ".png", ref errors);
            foreach (var state in new[] { "default", "hover", "pressed" })
                CheckFile(directory, "buttons/plate-" + state + ".png", ref errors);
            CheckFile(directory, "scenes/world-map.png", ref errors);
            CheckFile(directory, "scenes/pond-afternoon.png", ref errors);
            CheckFile(directory, "characters/cat-ready.png", ref errors);
            CheckFile(directory, "characters/cat-avatar.png", ref errors);
            CheckFile(directory, "manifest.json", ref errors);
            if (errors == 0)
                Debug.Log("[Cozy UI] 最终版素材完整。请运行 DesktopMain 场景并完成 Steam 登录进行页面验收。");
        }

        static void CheckFile(string directory, string relativePath, ref int errors)
        {
            if (File.Exists(Path.Combine(directory, relativePath)))
                return;
            errors++;
            Debug.LogError("[Cozy UI] 缺少素材：" + relativePath);
        }
    }
}
#endif
