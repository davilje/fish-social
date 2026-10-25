#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        static void ApplyPageMotionParity(GameObject root) => CozyJournalMotionBindings.ConfigurePage(root.transform);

        // Batch/authoring API retained; historical operation is not a daily menu command.
        public static void RepairPageMotionBindings()
        {
            foreach (var name in new[] { "PanelShop", "PanelEquipment", "PanelSocialFeed", "PanelSettings",
                "PanelLeaderboard", "PanelPondSelection", "PanelWorldMap", "PanelGallery", "PanelCatch",
                "PanelProfileHub", "PanelAchievements", "PanelProfileEdit" })
            {
                var path = Folder + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try { ApplyPageMotionParity(root); Record(root); SaveJournalPrefab(root,path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
