#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishSocial.Desktop.Editor
{
    public static class DesktopPrefabValidator
    {
        const string Folder = "Assets/Resources/Desktop/Prefabs";

        [MenuItem("Fish Social/Validate Desktop Prefabs")]
        static void Validate()
        {
            var entries = new[]
            {
                new PrefabEntry("PanelSocial", typeof(DesktopSocialModalView)),
                new PrefabEntry("PanelCatch", typeof(DesktopCatchBagModalView)),
                new PrefabEntry("PanelGallery", typeof(DesktopGalleryModalView)),
                new PrefabEntry("PanelSettings", typeof(DesktopSettingsModalView)),
            };

            var errors = string.Empty;
            foreach (var entry in entries)
            {
                var path = Folder + "/" + entry.Name + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    errors += "\n缺少 " + path;
                    continue;
                }
                if (prefab.GetComponent(entry.ComponentType) == null)
                    errors += "\n" + entry.Name + " 缺少 " + entry.ComponentType.Name;
            }

            EditorUtility.DisplayDialog(
                "Desktop Prefabs",
                string.IsNullOrEmpty(errors)
                    ? "4 个桌面功能 Prefab 均可用。\n\n" +
                      "修改方式：打开 Prefab → 修改 UI → Ctrl+S 保存 → 重新打包。"
                    : "Prefab 检查失败：" + errors,
                "确定");
        }

        sealed class PrefabEntry
        {
            public readonly string Name;
            public readonly System.Type ComponentType;

            public PrefabEntry(string name, System.Type componentType)
            {
                Name = name;
                ComponentType = componentType;
            }
        }
    }
}
#endif
