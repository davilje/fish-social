using System.Collections.Generic;
using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>Shared by saved prefab authoring and the real router. Regions follow the Web DOM,
    /// including the workspace editor which has no delayed .aside.</summary>
    public static class CozyJournalMotionBindings
    {
        public static void ConfigureTree(Transform root)
        {
            if (root == null) return;
            ConfigurePage(root);
            // Router slots wrap the actual page. Do not rebind arbitrary nested widgets.
            foreach (Transform child in root)
                if (child.Find("DesignHeader") != null) ConfigurePage(child);
        }

        public static void ConfigurePage(Transform root)
        {
            if (root == null || root.Find("DesignHeader") == null) return;
            var name = root.name.Replace("(Clone)", "");
            switch (name)
            {
                case "PanelShop": case "PanelEquipment":
                    Regions(root,"DesignItemsHeading","Details","Status","Items"); break;
                case "PanelSocialFeed":
                    Regions(root,"JournalFeedHeading","DesignFeedInfo","Status","Scroll"); break;
                case "PanelSettings":
                    Regions(root,"DesignSettingsHeading","DesignSettingsInfo",null,"SettingsViewport"); break;
                case "PanelLeaderboard":
                    Regions(root,"JournalBoardHeading","DesignPlayerCard","MyRank","Scroll","Podium"); break;
                case "PanelPondSelection":
                    Regions(root,"PondListHeading","DesignPondAside","Status","Scroll"); break;
                case "PanelWorldMap":
                    Regions(root,"DesignMapHeading","Details","Status","Map","DesignMapFrame"); break;
                case "PanelGallery":
                    Regions(root,"DesignSpeciesHeading","GalleryAside","Status","Grid"); break;
                case "PanelCatch":
                    var bag = Regions(root,"Reflow_BagHeading",null,"Status","Grid");
                    AsideParts(root,bag,984); break;
                case "PanelProfileEdit":
                    var edit = Regions(root,"JournalEditHeading",null,"Status",
                        "JournalPreview","Form","Actions","Header","WorkspaceEditRight",
                        "WorkspaceAvatarTab","WorkspaceShowcaseTab","WorkspaceTabHint","Avatars","Showcase");
                    edit.ConfigureHeadingParts(root.Find("WorkspaceSubtitle"));
                    break;
                case "PanelProfileHub": case "PanelAchievements":
                    var content = root.Find("Shell/Content");
                    if (content == null) break;
                    foreach (Transform tab in content)
                    {
                        if (!tab.name.StartsWith("Tab_")) continue;
                        var heading = tab.Find("JournalHeading") ?? tab.Find("DesignGridHeading");
                        var middle = new List<Transform>();
                        foreach (Transform part in tab)
                            if (part is RectTransform rect && rect.anchoredPosition.x < 720 &&
                                part != heading && !part.name.StartsWith("JournalCardTemplate")) middle.Add(part);
                        var motion = Motion(tab);
                        motion.Configure(heading,null,null,middle.ToArray());
                        if (tab.name == "Tab_资料") motion.ConfigureHeadingParts(root.Find("Shell/Header/Edit"));
                        AsideParts(tab,motion,720);
                    }
                    break;
            }
        }

        static CozyJournalPageMotion Motion(Transform root) =>
            root.GetComponent<CozyJournalPageMotion>() ?? root.gameObject.AddComponent<CozyJournalPageMotion>();

        static CozyJournalPageMotion Regions(Transform root,string heading,string aside,string status,params string[] middle)
        {
            var parts = new List<Transform>();
            foreach (var path in middle) if (root.Find(path) != null) parts.Add(root.Find(path));
            var motion = Motion(root);
            motion.Configure(heading == null ? null : root.Find(heading), aside == null ? null : root.Find(aside),
                status == null ? null : root.Find(status), parts.ToArray());
            return motion;
        }

        static void AsideParts(Transform root,CozyJournalPageMotion motion,float left)
        {
            var parts = new List<Transform>();
            foreach (Transform child in root)
                if (child is RectTransform rect && rect.anchoredPosition.x >= left) parts.Add(child);
            motion.ConfigureAside(parts.ToArray());
        }
    }
}
