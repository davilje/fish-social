#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishSocial.Desktop.Editor
{
    public static class CozyThemeValidationMenu
    {
        public static void ValidateAssets()
        {
            var errors = 0;
            foreach (var icon in new[] { "home", "map", "pond", "hook", "bag", "rod", "shop", "medal", "trophy", "user", "social" })
                CheckSprite("icons/" + icon + ".png", ref errors);
            foreach (var state in new[] { "default", "hover", "pressed" })
                CheckSprite("buttons/plate-" + state + ".png", ref errors);
            CheckSprite("panels/paper.png", ref errors);
            CheckSprite("scenes/world-map.png", ref errors);
            CheckSprite("scenes/home-sakura.png", ref errors);
            CheckSprite("brand/meow-fish.png", ref errors);
            CheckSprite("characters/cat-portrait.png", ref errors);
            if (errors == 0)
                Debug.Log("[Cozy UI] Resources Sprite 和导入 border 完整；仍需执行 Prefab 持久化校验与运行验收。");
        }

        static void CheckSprite(string relativePath, ref int errors)
        {
            var path = CozyPrefabRepair.ThemeFolder + "/" + relativePath;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (sprite != null && texture != null && importer != null
                && importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                && EditorUtility.IsPersistent(sprite) && CatTheme.LoadCozy(relativePath, Vector4.zero) == sprite
                && sprite.border == CozyPrefabRepair.ExpectedBorder(path, texture.width, texture.height))
                return;
            errors++;
            Debug.LogError("[Cozy UI] 缺少真正导入的 Resources Sprite 或 border 不正确：" + path);
        }
    }
}
#endif
