#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FishSocial.Desktop.Editor
{
    public static class DesktopPrefabBaker
    {
        const string Folder = "Assets/Resources/Desktop/Prefabs";

        [MenuItem("Fish Social/Bake Desktop Feature Prefabs")]
        public static void BakeFeaturePrefabs()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Resources/Desktop/Prefabs"));
            AssetDatabase.Refresh();

            var root = new GameObject("DesktopPrefabBakeRoot", typeof(RectTransform));
            try
            {
                Bake<DesktopSocialModalView>(root.transform, "PanelSocial",
                    view => view.Bind(null, null, null));
                Bake<DesktopCatchBagModalView>(root.transform, "PanelCatch",
                    view => view.Bind(null, null));
                Bake<DesktopGalleryModalView>(root.transform, "PanelGallery",
                    view => view.Bind(null, null));
                Bake<DesktopSettingsModalView>(root.transform, "PanelSettings",
                    view => view.Bind());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "Desktop Prefabs",
                "已用 PrefabUtility 保存到：\n" + Folder + "\n\n" +
                "PanelSocial / PanelCatch / PanelGallery / PanelSettings\n\n" +
                "之后改布局：打开预制体编辑，不要手写 YAML。",
                "确定");
        }

        static void Bake<T>(Transform parent, string name, System.Action<T> bind)
            where T : MonoBehaviour
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            DesktopModalUi.Stretch(go);
            var view = go.AddComponent<T>();
            bind(view);
            var path = Folder + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
        }
    }
}
#endif
