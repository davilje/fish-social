#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        public static void ApplyRankRefinement(GameObject root)
        {
            if (root == null || root.name != "PanelLeaderboard") return;
            var podium = root.transform.Find("Scroll/Viewport/Content/Podium") ?? root.transform.Find("Podium");
            if (podium == null) return;
            var labels = new[] { "第一名", "第二名", "第三名" };
            var colors = new[] { new Color32(139,97,43,255), new Color32(109,140,153,255), new Color32(174,119,82,255) };
            for (var rank=1;rank<=3;rank++)
            {
                var card = podium.Find("Slot"+rank);
                if (card == null) continue;
                var surface = "surfaces/podium-place-"+rank;
                CatTheme.StyleButton(card.GetComponent<Button>());
                var visual = card.GetComponent<CozyButtonVisual>();
                // The persistent family survives SkinTree, hovering and player selection.
                visual?.ConfigureSurface(surface,surface,surface,surface,surface);
                var height = rank==1?192:165;
                var badge=JournalSurface(card,"JournalRankBacking","decor/rank-label-"+rank,69.5f,height-16,69,26);
                var label=DpText(card,"JournalRankLabel",69.5f,height-16,69,26,13,false,TextAnchor.MiddleCenter,labels[rank-1]);
                label.color=colors[rank-1];
                badge.transform.SetAsLastSibling();
                label.transform.SetAsLastSibling();
            }
        }
    }
}
#endif
