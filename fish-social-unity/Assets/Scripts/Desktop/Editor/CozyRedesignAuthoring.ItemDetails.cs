#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        public static void RepairItemDetails()
        {
            foreach (var name in new[] { "PanelShop", "PanelEquipment", "PanelCatch" })
            {
                var path = Folder + name + ".prefab";
                var page = UnityEditor.PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (name == "PanelCatch") WebBag(page.transform);
                    else WebShop(page.transform, name == "PanelEquipment");
                    ApplyPageMotionParity(page);
                    Record(page);
                    SaveJournalPrefab(page, path);
                }
                finally { UnityEditor.PrefabUtility.UnloadPrefabContents(page); }
            }
            UnityEditor.AssetDatabase.SaveAssets();
        }

        /// <summary>Bakes the common full-details dialog and its photo-frame entry button.</summary>
        public static void ApplyItemDetails(GameObject root)
        {
            if (root == null || (root.name != "PanelShop" && root.name != "PanelEquipment" && root.name != "PanelCatch"))
                return;
            var page = root.transform;
            var entry = ButtonAt(page, "ItemDetails", "详情", null, 1218, 308, 84, 32);
            CompactAction(entry, false);
            entry.navigation = new Navigation { mode = Navigation.Mode.None };

            var overlay = Node(page, "DetailsDialog");
            Stretch(overlay);
            var dim = overlay.GetComponent<Image>() ?? overlay.gameObject.AddComponent<Image>();
            dim.sprite = null;
            dim.enabled = true;
            dim.color = new Color(0.20f, 0.13f, 0.08f, 0.48f);
            dim.raycastTarget = true;
            var dismiss = overlay.GetComponent<Button>() ?? overlay.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = dim;
            dismiss.transition = Selectable.Transition.None;
            dismiss.navigation = new Navigation { mode = Navigation.Mode.None };

            var paper = Paper(overlay, "Content", 235, 40, 900, 570);
            paper.enabled = true;
            paper.raycastTarget = true;
            // Intercept clicks inside the sheet so they cannot reach the dim's dismiss button.
            var inside = paper.GetComponent<Button>() ?? paper.gameObject.AddComponent<Button>();
            inside.targetGraphic = paper;
            inside.transition = Selectable.Transition.None;
            inside.navigation = new Navigation { mode = Navigation.Mode.None };
            var content = paper.transform;
            var title = Label(content, "Title", "", 28, 20, 744, 52, 26, true);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 20;
            title.resizeTextMaxSize = 26;
            Picture(content, "Image", null, 28, 104, 218, 180);

            var close = ButtonAt(content, "Close", "×", null, 820, 20, 52, 44);
            var closeLabel = close.transform.Find("Label").GetComponent<Text>();
            Place(close.transform, "Label", 0, 0, 52, 44);
            closeLabel.fontSize = 30;
            closeLabel.resizeTextForBestFit = false;

            var scrollRoot = Place(content, "Scroll", 270, 98, 596, 438);
            var hit = scrollRoot.GetComponent<Image>() ?? scrollRoot.gameObject.AddComponent<Image>();
            hit.sprite = null;
            hit.color = Color.clear;
            hit.raycastTarget = true;
            var viewport = Place(scrollRoot, "Viewport", 0, 0, 576, 438);
            var clip = viewport.GetComponent<RectMask2D>() ?? viewport.gameObject.AddComponent<RectMask2D>();
            clip.enabled = true;
            var body = Label(viewport, "Body", "", 0, 0, 568, 438, 18);
            body.alignment = TextAnchor.UpperLeft;
            body.resizeTextForBestFit = false;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.lineSpacing = 1.3f;
            body.rectTransform.anchorMin = Vector2.up;
            body.rectTransform.anchorMax = Vector2.one;
            body.rectTransform.pivot = Vector2.up;
            body.rectTransform.sizeDelta = new Vector2(-8, 0);
            var fit = body.GetComponent<ContentSizeFitter>() ?? body.gameObject.AddComponent<ContentSizeFitter>();
            fit.enabled = true;
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var barRoot = Place(scrollRoot, "Scrollbar", 584, 0, 10, 438);
            var track = barRoot.GetComponent<Image>() ?? barRoot.gameObject.AddComponent<Image>();
            track.color = new Color(0.45f, 0.28f, 0.18f, 0.12f);
            track.raycastTarget = true;
            var sliding = Node(barRoot, "SlidingArea");
            Stretch(sliding, 1);
            var handle = Node(sliding, "Handle");
            Stretch(handle);
            var handleImage = handle.GetComponent<Image>() ?? handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(0.58f, 0.36f, 0.20f, 0.62f);
            var bar = barRoot.GetComponent<Scrollbar>() ?? barRoot.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle;
            bar.targetGraphic = handleImage;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            var scroll = scrollRoot.GetComponent<ScrollRect>() ?? scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = body.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            if (root.GetComponent<CozyItemDetailsDialog>() == null)
                root.AddComponent<CozyItemDetailsDialog>();
            entry.transform.SetAsLastSibling();
            overlay.SetAsLastSibling();
            overlay.gameObject.SetActive(false);
        }
    }
}
#endif
