#if UNITY_EDITOR
using UnityEditor;
using FishSocial.Desktop.JournalV2.Editor;

namespace FishSocial.Desktop.Editor
{
    public static class FortyPondRollout
    {
        public static void Apply()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            OverlayPondArtBaker.ApplyExpandedPondCatalogAndExport();
            UpdateMap();
        }
        public static void UpdateMap()
        {
            JournalV2Authoring.BakeMap();
            WorldMapSceneAuthoring.ApplyAndVerify();
        }
    }
}
#endif
