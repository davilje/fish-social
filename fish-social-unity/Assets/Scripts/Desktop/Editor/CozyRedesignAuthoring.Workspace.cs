#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        // Batch/authoring API retained; historical operation is not a daily menu command.
        public static void ApplyWorkspacePages()
        {
            foreach(var name in new[]{"PanelProfileHub","PanelProfileEdit","PanelPondSettlement"})
            {
                var path=Folder+name+".prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    ApplyWorkspaceProfile(root);
                    ApplyPageMotionParity(root);
                    CozyWorkspaceSettlementAuthoring.Apply(root);
                    CozyPrefabRepair.RepairContents(root);
                    Record(root);
                    SaveJournalPrefab(root,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            CozyJournalComposerAuthoring.Bake();
            AssetDatabase.SaveAssets();
            Inspect();
            Verify();
        }
    }
}
#endif
