#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        static void ApplyApprovedRefinements(GameObject root)
        {
            ApplyRankRefinement(root);
            ApplyProfileRefinement(root);
            ApplyPondRefinement(root);
            ApplySocialRefinement(root);
            ApplyWorkspaceProfile(root);
            ApplyPageMotionParity(root);
            CozyWorkspaceSettlementAuthoring.Apply(root);
            if (root.name == "DesktopShell")
            {
                var header=root.transform.Find("MainRoot/Header");
                if(header!=null)
                {
                    var badge=JournalSurface(header,"Mail/UnreadBadge","surfaces/category-chip",47,-4,28,24);
                    badge.color=new Color32(226,83,63,255);
                    var count=DpText(header,"Mail/UnreadBadge/Count",0,0,28,24,11,false,TextAnchor.MiddleCenter,"");
                    count.color=Color.white;
                    badge.gameObject.SetActive(false);
                }
            }
            // Inactive authoring roots do not run the visual's normal lifecycle. Persist
            // configured sprite states explicitly rather than saving its temporary disabled pose.
            CozyPrefabRepair.RepairContents(root);
        }

        // Batch/authoring API retained; historical operation is not a daily menu command.
        public static void ApplyRefinements()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var name in new[]{"DesktopShell","PanelLeaderboard","PanelProfileHub","PanelAchievements","AchievementRow","GallerySpeciesSlot","ShowcaseSlot","PanelGallery","PanelPondSelection","PanelWorldMap","PondSelectionCard","PanelSocial","PanelSocialFeed","SocialPostCard","FriendRow","FriendRequestRow","DirectMessageRow","PondChatMessageRow","OnlinePlayerRow","SteamInviteRow","TextStatusRow","PostCommentRow"})
            {
                var path=Folder+name+".prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab==null)continue;
                var root=PrefabUtility.LoadPrefabContents(path);
                try { ApplyApprovedRefinements(root); Record(root); SaveJournalPrefab(root,path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Inspect();
            Verify();
        }
    }
}
#endif
