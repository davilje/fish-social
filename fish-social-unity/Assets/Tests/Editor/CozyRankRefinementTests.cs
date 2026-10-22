#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyRankRefinementTests
    {
        [Test]
        public void PodiumGradientsSurviveSkinSelectionHoverAndPrefabRepair()
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.AssetPath("PanelLeaderboard")));
            try
            {
                root.name="PanelLeaderboard";
                var panel=root.GetComponent<DesktopLeaderboardPanel>();
                var entries=new[]{new LeaderboardEntryDto{rank=1,playerId="rank-1",nickname="第一位",value=1.2f},new LeaderboardEntryDto{rank=2,playerId="rank-2",nickname="第二位",value=1.1f},new LeaderboardEntryDto{rank=3,playerId="rank-3",nickname="第三位",value=1f}};
                panel.PresentRows(entries);
                CatTheme.SkinTree(root.transform);
                CozyPrefabRepair.RepairContents(root);
                var podium=root.transform.Find("Scroll/Viewport/Content/Podium");
                for(var rank=1;rank<=3;rank++)
                {
                    var card=podium.Find("Slot"+rank);
                    var visual=card.GetComponent<CozyButtonVisual>();
                    panel.SelectPlayer(entries[rank-1]);
                    visual.OnPointerEnter(null);
                    visual.ApplyFinalPose();
                    var plate=card.Find("CozyVisual").GetComponent<Image>();
                    Assert.AreEqual("Assets/Resources/Desktop/Theme/Cozy/journal/surfaces/podium-place-"+rank+".png",AssetDatabase.GetAssetPath(plate.sprite));
                    Assert.AreEqual(Image.Type.Sliced,plate.type);
                    Assert.AreEqual(new Vector2(-8,-8),plate.rectTransform.offsetMin);
                    Assert.AreEqual("Assets/Resources/Desktop/Theme/Cozy/journal/decor/rank-label-"+rank+".png",AssetDatabase.GetAssetPath(card.Find("JournalRankBacking").GetComponent<Image>().sprite));
                    Assert.AreEqual(new[]{"第一名","第二名","第三名"}[rank-1],card.Find("JournalRankLabel").GetComponent<Text>().text);
                    Assert.Greater(card.Find("JournalRankLabel").GetSiblingIndex(),card.Find("JournalRankBacking").GetSiblingIndex(),"The opaque badge must render behind its rank label.");
                }
                Assert.IsEmpty(CozyPrefabRepair.ValidateContents(root,root.name));
            }
            finally {Object.DestroyImmediate(root);}
        }
    }
}
#endif
