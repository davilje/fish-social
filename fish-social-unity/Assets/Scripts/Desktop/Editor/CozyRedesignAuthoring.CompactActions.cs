#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        static void CompactAction(Button button, bool destructive)
        {
            var t = button.transform;
            var width = destructive ? 88f : 96f;
            var height = destructive ? 30f : 32f;
            // The previous inline layout measures its retired Image icon and would overwrite
            // the compact caption's position and font size on every canvas update.
            foreach (var legacy in t.GetComponents<CozyJournalInlineContent>())
                UnityEngine.Object.DestroyImmediate(legacy);
            foreach (var name in new[] { "Icon", "CozyIcon" }) t.Find(name)?.gameObject.SetActive(false);
            var label = t.Find("Label").GetComponent<Text>();
            Place(t, "Label", 30, 0, width - 37, height);
            label.fontSize = destructive ? 14 : 15;
            label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = false;
            var iconRect = Place(t, "ActionIcon", 11, (height - 16) / 2, 16, 16);
            if (iconRect.GetComponent<CanvasRenderer>() == null) iconRect.gameObject.AddComponent<CanvasRenderer>();
            var icon = iconRect.GetComponent<CozyActionIcon>() ?? iconRect.gameObject.AddComponent<CozyActionIcon>();
            icon.destructive = destructive;
            icon.raycastTarget = false;
            icon.color = destructive ? new Color32(164, 102, 85, 255) : new Color32(117, 71, 46, 255);
            var visual = t.GetComponent<CozyButtonVisual>();
            visual.SetCompactAction(destructive);
            // Persist the native plate; runtime state changes reuse this authored node.
            CozyCssGraphic.EnsureWash(t.Find("CozyVisual"), "ButtonWash",
                destructive ? Color.clear : new Color(1f, .985f, .95f, 1f), destructive ? 7f : 14f, 1f,
                destructive ? Color.clear : (Color)new Color32(219, 195, 157, 255));
            visual.ApplyFinalPose();
            var content = t.GetComponent<CozyActionContentLayout>() ?? t.gameObject.AddComponent<CozyActionContentLayout>();
            content.Reflow();
        }

        static void CompactComment(Transform row)
        {
            var delete = row.Find("Delete")?.GetComponent<Button>();
            if (delete == null) return;
            var rect = Place(row, "Delete", 208, 6, 88, 30);
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-8, -6);
            CompactAction(delete, true);
            var author = row.Find("Author") as RectTransform;
            if (author != null)
            {
                author.anchorMin = Vector2.up; author.anchorMax = Vector2.one; author.pivot = Vector2.up;
                author.offsetMin = new Vector2(12, -32); author.offsetMax = new Vector2(-104, -10);
            }
        }

        public static void RepairCompactActions()
        {
            foreach (var name in new[] { "PanelShop", "PanelEquipment", "PanelCatch", "PostCommentRow" })
            {
                var path = Folder + name + ".prefab";
                var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (name == "PostCommentRow") CompactComment(root.transform);
                    else
                    {
                        var rect = Place(root.transform, "ItemDetails", 1206, 308, 96, 32);
                        CompactAction(rect.GetComponent<Button>(), false);
                    }
                    Record(root); SaveJournalPrefab(root, path);
                }
                finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
            }
            UnityEditor.AssetDatabase.SaveAssets();
        }
    }
}
#endif
