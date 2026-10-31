#if UNITY_EDITOR
using UnityEditor;
namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        public static void RepairHomeActions()
        {
            var path = Folder + "DesktopShell.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                CozyHomeRoom.Build(root.transform.Find("MainRoot"));
                SaveJournalPrefab(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            CozyHomeRoomAuthoring.VerifyAndRender();
        }
    }
}
#endif
