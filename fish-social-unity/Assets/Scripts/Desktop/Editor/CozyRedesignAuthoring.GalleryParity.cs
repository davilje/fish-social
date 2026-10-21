#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        // The standalone codex uses the original journal card/polaroid vocabulary; it has no separate HTML demo page.
        internal static void ApplyGalleryParity(GameObject root)
        {
            if (root.name == "GallerySpeciesSlot") { GallerySpeciesCard(root); return; }
            if (root.name != "PanelGallery") return;
            var node = root.transform;
            Hide(node, "DesignGalleryGrid", "DesignDetailPaper", "Reflow_InfoRail", "Detail/DesignHeading", "Detail/Species", "Detail/Body");
            DpText(node, "DesignSpeciesHeading", 38, 108, 730, 36, 24, value: "鱼种手账");
            DpText(node, "DesignSpeciesHint", 38, 144, 900, 26, 16, true, value: "每一种相遇，都值得记下来");
            Place(node, "Grid", 24, 176, 940, 424);
            var viewport = Place(node, "Grid/Viewport", 0, 0, 940, 424);
            Stretch(viewport, 0);
            var mask = viewport.GetComponent<RectMask2D>(); if (mask == null) viewport.gameObject.AddComponent<RectMask2D>();
            var background = viewport.GetComponent<Image>(); if (background != null) { background.color = Color.clear; background.raycastTarget = true; }
            Grid(node, "Grid/Viewport/Species", 4, 210, 190);
            var content = node.Find("Grid/Viewport/Species") as RectTransform;
            var layout = content.GetComponent<GridLayoutGroup>();
            layout.spacing = new Vector2(18, 18); layout.padding = new RectOffset(18, 18, 16, 22);
            var scroll = node.Find("Grid").GetComponent<ScrollRect>();
            if (scroll == null) scroll = node.Find("Grid").gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var aside = DpSurface(node, "GalleryAside", "surfaces/paper-aside", 984, 112, 362, 496);
            DpText(aside, "Heading", 24, 12, 314, 32, 23, value: "鱼种详情");
            DpPhoto(aside, "Polaroid", "DesignArt", "DesignName", 30, 58);
            DpText(aside, "LockedHint", 42, 126, 278, 42, 20, false, TextAnchor.MiddleCenter, "尚未相遇");
            aside.Find("LockedHint").gameObject.SetActive(false);
            var details = Place(node, "Detail", 1008, 420, 314, 166);
            var legacyText = details.GetComponent<Text>(); if (legacyText != null) legacyText.enabled = false;
            var detailScroll = details.GetComponent<ScrollRect>(); if (detailScroll == null) detailScroll = details.gameObject.AddComponent<ScrollRect>();
            var detailViewport = Node(details, "Viewport"); Stretch(detailViewport, 0);
            if (detailViewport.GetComponent<RectMask2D>() == null) detailViewport.gameObject.AddComponent<RectMask2D>();
            var body = DpText(details, "Viewport/Body", 0, 0, 314, 166, 17, true, TextAnchor.UpperLeft);
            body.rectTransform.anchorMin = Vector2.up; body.rectTransform.anchorMax = Vector2.one; body.rectTransform.pivot = Vector2.up;
            body.rectTransform.sizeDelta = new Vector2(0, 166); body.lineSpacing = 1.18f;
            var fitter = body.GetComponent<ContentSizeFitter>(); if (fitter == null) fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            detailScroll.viewport = detailViewport; detailScroll.content = body.rectTransform; detailScroll.horizontal = false; detailScroll.vertical = true;
            detailScroll.movementType = ScrollRect.MovementType.Clamped;
            details.SetAsLastSibling();
            DpText(node, "Status", 38, 612, 1070, 26, 17, true);
            DpButton(node, "Retry", 1196, 610, 126, 30, 16, "重新加载");
            node.Find("Retry").gameObject.SetActive(true);
            Node(node, "JournalGalleryParity").gameObject.SetActive(false);
            node.Find("DesignHeader").SetAsLastSibling();
        }

        static void GallerySpeciesCard(GameObject root)
        {
            var node = root.transform;
            ((RectTransform)node).sizeDelta = new Vector2(210, 190);
            var button = root.GetComponent<Button>(); if (button == null) button = root.AddComponent<Button>();
            CatTheme.StyleButton(button);
            var visual = root.GetComponent<CozyButtonVisual>();
            if (visual != null) visual.ConfigureSurface("surfaces/sticker-card", "surfaces/sticker-card", null, "surfaces/sticker-card");
            Hide(node, "Icon", "CozyIcon");
            var shadow = JournalLayer(node, "JournalCardShadow", "surfaces/sticker-shadow"); shadow.transform.SetAsFirstSibling();
            Picture(node, "FishArt", null, 20, 18, 170, 106);
            DpText(node, "Label", 12, 128, 186, 30, 21, false, TextAnchor.MiddleCenter, "鱼种");
            DpText(node, "Rarity", 12, 158, 186, 24, 15, true, TextAnchor.MiddleCenter, "未解锁");
            var selection = JournalLayer(node, "JournalSelection", "surfaces/selection-outline");
            var tape = JournalSurface(node, "JournalTape", "decor/tape-pink-stripe", -18, -8, 104, 26);
            tape.rectTransform.localEulerAngles = new Vector3(0, 0, 28);
            selection.transform.SetAsLastSibling(); tape.transform.SetAsLastSibling();
            var state = root.GetComponent<CozyJournalCardVisual>(); if (state == null) state = root.AddComponent<CozyJournalCardVisual>();
            state.Configure(visual, selection.gameObject, tape.gameObject);
            visual?.ApplyFinalPose();
        }
    }
}
#endif
