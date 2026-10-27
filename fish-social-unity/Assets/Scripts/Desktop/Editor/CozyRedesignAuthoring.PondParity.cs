#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static partial class CozyRedesignAuthoring
    {
        // Original 1370 x 650 page coordinates; border-box offsets include the aside's 2 px border.
        static void ApplyPondParity(GameObject root)
        {
            switch (root.name)
            {
                case "PanelPondSelection": PondListParity(root.transform); PondAsideParity(root.transform, "DesignPondAside", "Body", "Enter"); break;
                case "PanelWorldMap": PondMapParity(root.transform); PondAsideParity(root.transform, "Details", "DetailsText", "EnterPond"); break;
                case "PondSelectionCard": PondCardParity(root.transform); break;
                case "WorldMapPondMarker": PondMarkerParity(root.transform); break;
            }
        }

        static void PondAsideParity(Transform page, string path, string facts, string enter)
        {
            var root = page.Find(path);
            Label(root, "Heading", "鱼塘详情", 24, 18, 314, 32, 22).gameObject.SetActive(true);
            var frame = JournalSurface(root, "PhotoFrame", "surfaces/polaroid-frame", 32, 60, 302, 208);
            frame.rectTransform.pivot = new Vector2(.5f, .5f);
            frame.rectTransform.anchoredPosition = new Vector2(183, -164);
            var shadowRoot = Place(root, "PhotoShadow", 32, 60, 302, 208);
            var shadow = JournalLayer(shadowRoot, "JournalShadow", "surfaces/polaroid-shadow");
            shadowRoot.SetSiblingIndex(frame.transform.GetSiblingIndex());
            var photo = root.Find("Photo").GetComponent<Image>();
            Place(root, "Photo", 44, 72, 278, 140);
            photo.preserveAspect = false;
            if (photo.GetComponent<CozyPondPhotoCover>() == null) photo.gameObject.AddComponent<CozyPondPhotoCover>();
            var caption = Label(root, "Name", "选择一处鱼塘", 44, 222, 278, 34, 24);
            caption.alignment = TextAnchor.MiddleCenter;
            var tape = JournalSurface(root, "PhotoTape", "decor/tape-blue-stripe", 136, 46, 104, 26);
            tape.rectTransform.pivot = new Vector2(.5f, .5f);
            tape.rectTransform.anchoredPosition = new Vector2(188, -59);
            tape.rectTransform.localEulerAngles = new Vector3(0, 0, 4);
            (frame.GetComponent<CozyPondArtworkPose>() ?? frame.gameObject.AddComponent<CozyPondArtworkPose>())
                .Configure(2, frame, shadow, photo, caption, tape);
            // Long access/charge/error explanations remain scrollable above the fixed control area.
            Place(root, "FactsScroll", 26, 280, 310, 48);
            var body = root.Find("FactsScroll/" + facts)?.GetComponent<Text>();
            if (body != null) { body.fontSize = 15; body.lineSpacing = 1.15f; }
            var fee = Label(root, "FeeSummary", "", 26, 332, 310, 22, 14);
            fee.resizeTextForBestFit = true; fee.resizeTextMinSize = 11; fee.resizeTextMaxSize = 14;
            var divider = Place(root, "EntryDivider", 20, 360, 322, 1);
            for (var i = 0; i < 54; i++)
            {
                var dash = Place(divider, "Dash" + i, i * 6, 0, 3, 1);
                var image = dash.GetComponent<Image>() ?? dash.gameObject.AddComponent<Image>();
                image.color = new Color32(231, 211, 180, 255); image.raycastTarget = false;
            }
            var track = JournalSurface(root, "FeeTrack", "surfaces/pond-segment-track", 20, 368, 326, 48);
            // Existing mode buttons precede newly appended decoration in migrated prefabs.
            // Pin the opaque track before both controls instead of relative to a late divider.
            track.transform.SetSiblingIndex(Mathf.Min(root.Find("ModeSell").GetSiblingIndex(), root.Find("ModeAuto").GetSiblingIndex()));
            for (var i = 0; i < 2; i++)
            {
                var mode = Place(root, i == 0 ? "ModeSell" : "ModeAuto", 24 + i * 161, 372, 157, 40);
                mode.localEulerAngles = Vector3.zero;
                Hide(mode, "Icon", "CozyIcon");
                var visual = mode.GetComponent<CozyButtonVisual>();
                visual.ConfigureSurface("surfaces/pond-segment-empty", "surfaces/pond-segment-empty", "surfaces/pond-segment-selected",
                    "surfaces/pond-segment-selected", "surfaces/pond-segment-empty");
                var label = mode.Find("Label").GetComponent<Text>();
                Stretch(label.rectTransform); label.fontSize = 18; label.resizeTextForBestFit = false;
            }
            Place(root, enter, 20, 430, 326, 50);
            Place(root, "StatusText", 24, 482, 314, 14);
            var enterLabel = root.Find(enter + "/Label")?.GetComponent<Text>();
            if (enterLabel != null) enterLabel.fontSize = 23;
            // The aside is an explicitly authored fixed layout. Only the facts Text needs sizing.
            foreach (var layout in root.GetComponentsInChildren<LayoutGroup>(true)) Object.DestroyImmediate(layout);
            foreach (var layout in root.GetComponentsInChildren<LayoutElement>(true)) Object.DestroyImmediate(layout);
            foreach (var fitter in root.GetComponentsInChildren<ContentSizeFitter>(true))
                if (body == null || fitter.transform != body.transform) Object.DestroyImmediate(fitter);
        }

        static void PondListParity(Transform root)
        {
            Label(root, "PondListHeading", "挑一片喜欢的水", 254, 114, 226, 40, 24);
            Label(root, "ResultCount", "", 486, 114, 224, 40, 16);
            var search = Place(root, "Search", 722, 115.5f, 242, 37);
            Place(search, "Text", 40, 0, 192, 37);
            var placeholder = Label(search, "Placeholder", "搜索鱼塘 / 水域", 40, 0, 192, 37, 15);
            placeholder.color = new Color32(161, 130, 100, 255);
            var text = search.Find("Text").GetComponent<Text>(); text.fontSize = 15;
            Picture(search, "SearchIcon", CatTheme.LoadCozy("icons/map.png", Vector4.zero), 11, 7.5f, 22, 22).color = new Color(1,1,1,.6f);
            Place(root, "PondCardScroll", 254, 160, 710, 440);
            Stretch(root.Find("PondCardScroll/Viewport") as RectTransform);
            var cards = root.Find("PondCardScroll/Viewport/Cards") as RectTransform;
            foreach (var layout in cards.GetComponents<LayoutGroup>())
                if (!(layout is GridLayoutGroup)) Object.DestroyImmediate(layout);
            var grid = cards.GetComponent<GridLayoutGroup>() ?? cards.gameObject.AddComponent<GridLayoutGroup>();
            grid.enabled = true; grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
            grid.cellSize = new Vector2(329.5f, 220); grid.spacing = new Vector2(23, 23);
            grid.padding = new RectOffset(14, 14, 17, 62); grid.childAlignment = TextAnchor.UpperLeft;
            var fitter = cards.GetComponent<ContentSizeFitter>() ?? cards.gameObject.AddComponent<ContentSizeFitter>();
            fitter.enabled = true; fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Label(root, "DesignPondRail/Note", "鱼塘照片是旅行的起点。\n选好水域，再决定今天的\n回鱼方式。", 18, 318, 174, 76, 16);
            Place(root, "DesignPondAside/Locate", -942, 424, 174, 43);
            Place(root, "ClearFilters", 42, 404, 174, 38);
        }

        static void PondMapParity(Transform root)
        {
            Hide(root, "DesignMapRail/Heading", "DesignMapRail/Body", "DesignMapRail/Tip");
            var ids = new[] { "wilderness", "reservoir", "advanced", "veteran", "giant", "forbidden", "locked" };
            var names = new[] { "野外", "水库", "进阶", "老手", "巨物", "禁区", "未解锁" };
            for (var i = 0; i < ids.Length; i++)
            {
                Place(root, "DesignMapRail/Legend" + i, 0, 0, 210, 496);
                var dot = Label(root, "DesignMapRail/Legend" + i + "/Dot", "●", 18, 22 + i * 34, 18, 34, 17);
                dot.color = CozyPondMarkerView.CategoryColor(ids[i]);
                var outline = dot.GetComponent<Outline>() ?? dot.gameObject.AddComponent<Outline>();
                outline.effectColor = Color.white; outline.effectDistance = new Vector2(1, 1);
                Label(root, "DesignMapRail/Legend" + i + "/Text", names[i], 42, 22 + i * 34, 150, 34, 17);
            }
            Label(root, "DesignMapRail/Note", "拖动地图寻找水域\n滚轮缩放，点浮标看详情", 18, 330, 174, 56, 16);
            Label(root, "DesignMapHeading", "世界地图", 254, 114, 710, 40, 24);
            var frameRoot = Place(root, "DesignMapFrame", 254, 160, 710, 440);
            var oldImage = frameRoot.GetComponent<Image>(); if (oldImage != null) oldImage.enabled = false;
            var frame = JournalLayer(frameRoot, "PhotoBorder", "surfaces/map-photo-frame");
            frameRoot.pivot = new Vector2(.5f,.5f); frameRoot.anchoredPosition = new Vector2(609,-380);
            var viewport = Place(root, "Viewport", 264, 170, 690, 420);
            // A RectMask2D clips in axis-aligned layout space; a stencil clips the actual rotated
            // graphic mesh. Rotate that mesh and all map artwork around the same frame center.
            foreach (var rectMask in viewport.GetComponents<RectMask2D>()) Object.DestroyImmediate(rectMask);
            var clip = viewport.GetComponent<Image>() ?? viewport.gameObject.AddComponent<Image>();
            CozyJournalTheme.ApplySurface(clip, "surfaces/pond-map-clip");
            clip.color = Color.white; clip.raycastTarget = true;
            var stencil = viewport.GetComponent<Mask>() ?? viewport.gameObject.AddComponent<Mask>();
            stencil.enabled = true; stencil.showMaskGraphic = false;
            (frameRoot.GetComponent<CozyPondArtworkPose>() ?? frameRoot.gameObject.AddComponent<CozyPondArtworkPose>())
                .ConfigureGroup(.6f, viewport, frame);
            var mapSprite = CatTheme.LoadCozy("scenes/world-map.png", Vector4.zero);
            var mapWidth = mapSprite != null ? 700f * mapSprite.rect.width / mapSprite.rect.height : 1050f;
            var content = Place(root, "Viewport/MapContent", -180, -140, mapWidth, 700);
            content.anchoredPosition = new Vector2(-180, 140);
            Stretch(root.Find("Viewport/MapContent/MarkerLayer") as RectTransform);
            Stretch(root.Find("Viewport/MapContent/MapImage") as RectTransform);
            frameRoot.GetComponent<CozyPondArtworkPose>().ConfigureGroup(.6f, viewport, frame);
            var left = JournalSurface(root, "JournalMapTapeL", "decor/tape-blue-stripe", 224, 154, 104, 26);
            var right = JournalSurface(root, "JournalMapTapeR", "decor/tape-blue-stripe", 890, 580, 104, 26);
            foreach (var tape in new[] { left, right })
            {
                var rect = tape.rectTransform; rect.pivot = new Vector2(.5f,.5f); rect.anchoredPosition += new Vector2(52,-13);
                rect.localEulerAngles = new Vector3(0,0,28); tape.color = new Color(1,1,1,.95f); tape.transform.SetAsLastSibling();
            }
            frameRoot.GetComponent<CozyPondArtworkPose>().ConfigureGroup(.6f, viewport, frame, left, right);
        }

        static void PondCardParity(Transform root)
        {
            Place(root, "", 0, 0, 329.5f, 220);
            var layout = root.GetComponent<LayoutElement>(); if (layout != null) { layout.preferredWidth = 329.5f; layout.preferredHeight = 220; layout.minHeight = 220; }
            Hide(root, "Level", "PondIcon", "FocusOnMap", "DesignIconPaper");
            var photo = Place(root, "Photo", 10, 10, 309.5f, 132).GetComponent<Image>(); photo.preserveAspect = false;
            if (photo.GetComponent<CozyPondPhotoCover>() == null) photo.gameObject.AddComponent<CozyPondPhotoCover>();
            Label(root, "Name", "", 10, 152, 309.5f, 31, 22);
            Label(root, "Description", "", 10, 187, 210, 22, 14);
            Label(root, "Fee", "", 222, 187, 97.5f, 22, 14).alignment = TextAnchor.MiddleRight;
            var badge = JournalSurface(root, "AccessBadge", "surfaces/pond-access-badge", 217.5f, 19, 93, 28);
            var access = Label(root, "Access", "", 217.5f, 19, 93, 28, 12); access.alignment = TextAnchor.MiddleCenter;
            access.color = new Color32(113,137,80,255);
            var tape = JournalSurface(root, "PondTape", "decor/tape-blue-stripe", -18, -8, 104, 26);
            tape.rectTransform.pivot = new Vector2(.5f, .5f); tape.rectTransform.anchoredPosition += new Vector2(52,-13);
            tape.rectTransform.localEulerAngles = new Vector3(0,0,28); tape.color = new Color(1,1,1,.95f);
            tape.gameObject.SetActive(false);
        }

        static void PondMarkerParity(Transform root)
        {
            // Zero-position map coordinates are the buoy waterline; its body is above that point.
            Place(root, "", 0, 0, 64, 64);
            var icon = Picture(root, "Icon", CozyJournalTheme.Sprite("motion/bobber-base"), 15, -12, 34, 48);
            Picture(icon.transform, "Color", CozyJournalTheme.Sprite("motion/bobber-color"), 0, 0, 34, 48);
            Picture(icon.transform, "Highlight", CozyJournalTheme.Sprite("motion/bobber-highlight"), 0, 0, 34, 48);
            var label = Label(root, "Label", "", -48, 42, 160, 28, 16);
            label.alignment = TextAnchor.MiddleCenter;
            var pill = JournalSurface(root, "LabelPill", "surfaces/pond-marker-label", -48, 42, 160, 28);
            pill.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
            Picture(root, "Paw", CozyJournalTheme.Sprite("decor/paw-mark"), 44, -30, 34, 34);
        }
    }
}
#endif
