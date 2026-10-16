#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace FishSocial.Desktop.Editor
{
    public sealed class CozySpriteImporter : AssetPostprocessor
    {
        public static bool IsSurfacePath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                && (path.StartsWith(CozyPrefabRepair.ThemeFolder + "/buttons/plate-", StringComparison.Ordinal)
                    || path.StartsWith(CozyPrefabRepair.ThemeFolder + "/panels/", StringComparison.Ordinal));
        }

        void OnPreprocessTexture()
        {
            if (!IsSurfacePath(assetPath))
                return;
            var importer = assetImporter as TextureImporter;
            if (importer == null)
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var border = CatTheme.SurfaceBorder(assetPath);
            if (border != Vector4.zero)
                importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        public override uint GetVersion()
        {
            return 4;
        }
    }
}
#endif
