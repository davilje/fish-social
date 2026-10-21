#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        static void ApplyPageMotionParity(GameObject root)
        {
            var t=root.transform;
            switch(root.name)
            {
                case "PanelShop": case "PanelEquipment":
                    MotionRegions(t,"DesignItemsHeading","Details","Status","Items"); break;
                case "PanelSocialFeed":
                    MotionRegions(t,"JournalFeedHeading","DesignFeedInfo","Status","Scroll"); break;
                case "PanelSettings":
                    MotionRegions(t,"DesignSettingsHeading","DesignSettingsInfo",null,"SettingsViewport"); break;
                case "PanelCatch":
                    MotionRegions(t,"Reflow_BagHeading",null,"Status","Grid");
                    ConfigureAsideParts(t,t.GetComponent<CozyJournalPageMotion>(),984); break;
                case "PanelProfileHub":
                    var content=t.Find("Shell/Content");
                    if(content!=null) foreach(Transform tab in content)
                    {
                        if(!tab.name.StartsWith("Tab")) continue;
                        var motion=tab.GetComponent<CozyJournalPageMotion>()??tab.gameObject.AddComponent<CozyJournalPageMotion>();
                        var center=new List<Transform>();
                        foreach(Transform part in tab)
                            if(part is RectTransform r && r.anchoredPosition.x<720 && part.name!="DesignGridHeading" && !part.name.StartsWith("JournalCardTemplate")) center.Add(part);
                        motion.Configure(tab.Find("DesignGridHeading"),null,null,center.ToArray());
                        ConfigureAsideParts(tab,motion,720);
                    }
                    break;
            }
        }
        static void ConfigureAsideParts(Transform root,CozyJournalPageMotion motion,float left)
        {
            var parts=new List<Transform>();
            foreach(Transform child in root)
                if(child is RectTransform r && r.anchoredPosition.x>=left) parts.Add(child);
            motion.ConfigureAside(parts.ToArray());
        }
        static void MotionRegions(Transform t,string heading,string aside,string status,params string[] middle)
        {
            var regions=new List<Transform>();
            foreach(var path in middle) if(t.Find(path)!=null) regions.Add(t.Find(path));
            var motion=t.GetComponent<CozyJournalPageMotion>()??t.gameObject.AddComponent<CozyJournalPageMotion>();
            motion.Configure(heading!=null?t.Find(heading):null,aside!=null?t.Find(aside):null,status!=null?t.Find(status):null,regions.ToArray());
        }
    }
}
#endif
