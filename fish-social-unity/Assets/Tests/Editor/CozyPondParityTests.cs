#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Editor;
using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyPondParityTests
    {
        GameObject instance;
        [TearDown] public void Cleanup() { if (instance != null) Object.DestroyImmediate(instance); }
        Transform Load(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.AssetPath(name));
            Assert.IsNotNull(prefab);
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            return instance.transform;
        }
        static RectTransform Rect(Transform root, string path) => (RectTransform)root.Find(path);

        [TestCase("PanelPondSelection", "DesignPondAside", "Body", "Enter")]
        [TestCase("PanelWorldMap", "Details", "DetailsText", "EnterPond")]
        public void LongFailuresAndBothFeeModesRemainScrollableAboveFixedEntry(string page, string aside, string body, string enter)
        {
            var root = Load(page).Find(aside);
            Assert.IsEmpty(root.GetComponentsInChildren<LayoutGroup>(true), "Fixed aside must not retain inactive auto layout drivers");
            Assert.IsEmpty(root.GetComponentsInChildren<LayoutElement>(true));
            var state = new PondSelectionState();
            state.ApplyProgress(new FishingProgressDto { onboardingCompleted = true, level = 99 });
            state.Select(new WorldMapPondView { pondId = "test", displayName = "费用测试鱼塘", isOpen = true,
                pondCategory = "advanced", allowsAutoReturn = true, feePer2hSellOnly = 125, feePer2hAutoReturn = 250 });
            var failure = new string('长', 300) + "错误末尾";
            state.SetEntryStatus(false, failure, true);
            PondDetailView.Refresh(root, state, body);
            var scroll = root.Find("FactsScroll").GetComponent<ScrollRect>();
            Assert.IsTrue(scroll.vertical);
            Assert.IsNotNull(scroll.GetComponent<RectMask2D>());
            var text = scroll.content.GetComponent<Text>();
            StringAssert.Contains("125 金币", text.text);
            StringAssert.Contains("250 金币", text.text);
            StringAssert.Contains(failure, text.text);
            Assert.IsTrue(text.GetComponent<ContentSizeFitter>().enabled);
            Assert.AreEqual(ContentSizeFitter.FitMode.PreferredSize, text.GetComponent<ContentSizeFitter>().verticalFit);
            var facts = (RectTransform)scroll.transform;
            var mode = Rect(root, "ModeSell");
            var button = Rect(root, enter);
            Assert.Less(root.Find("FeeTrack").GetSiblingIndex(), mode.GetSiblingIndex(), "Opaque track must render before the sell mode");
            Assert.Less(root.Find("FeeTrack").GetSiblingIndex(), root.Find("ModeAuto").GetSiblingIndex(), "Opaque track must render before the auto-return mode");
            Assert.LessOrEqual(-facts.anchoredPosition.y + facts.rect.height, -mode.anchoredPosition.y);
            Assert.LessOrEqual(-mode.anchoredPosition.y + mode.rect.height, -button.anchoredPosition.y);
            Assert.LessOrEqual(-button.anchoredPosition.y + button.rect.height, ((RectTransform)root).rect.height);
            Assert.AreEqual(new Vector2(278, 140), Rect(root, "Photo").sizeDelta);
            Assert.IsNotNull(root.Find("Photo").GetComponent<CozyPondPhotoCover>());
            state.SelectFeeMode("auto_return"); PondDetailView.Refresh(root, state, body);
            StringAssert.Contains("250 金币", root.Find("FeeSummary").GetComponent<Text>().text);
        }

        [Test]
        public void ProductionMapHasWebExtentAndClampsAtItsTopLeftEdges()
        {
            var root = Load("PanelWorldMap");
            var viewport = Rect(root, "Viewport");
            var content = Rect(root, "Viewport/MapContent");
            Assert.AreEqual(new Vector2(690,420), viewport.sizeDelta);
            Assert.AreEqual(new Vector2(1050,700), content.sizeDelta);
            Assert.AreEqual(Vector2.up, content.pivot);
            Assert.AreEqual(new Vector2(-180,140), content.anchoredPosition);
            Assert.IsNull(viewport.GetComponent<RectMask2D>(), "Axis-aligned clipping would cut rotated map corners");
            Assert.IsTrue(viewport.GetComponent<Mask>().enabled);
            Assert.IsFalse(viewport.GetComponent<Mask>().showMaskGraphic);
            Assert.AreSame(CozyJournalTheme.Sprite("surfaces/pond-map-clip"), viewport.GetComponent<Image>().sprite);
            Assert.AreEqual(Quaternion.identity, viewport.localRotation, "Input bounds remain fixed");
            var pose = root.Find("DesignMapFrame").GetComponent<CozyPondArtworkPose>();
            var frame = root.Find("DesignMapFrame/PhotoBorder").GetComponent<Image>();
            pose.ConfigureGroup(.6f, viewport, frame);
            Assert.IsNotNull(viewport.GetComponent<CozyGraphicOffset>());
            Assert.IsNotNull(root.Find("Viewport/MapContent/MapImage").GetComponent<CozyGraphicOffset>());
            using (var mesh = new VertexHelper())
            {
                mesh.AddVert(Vector3.zero, Color.white, Vector2.zero);
                viewport.GetComponent<CozyGraphicOffset>().ModifyMesh(mesh);
                var vertex = new UIVertex(); mesh.PopulateUIVertex(ref vertex,0);
                var center = (Vector2)viewport.rect.center;
                var expected = center + (Vector2)(Quaternion.Euler(0,0,.6f) * (Vector3)(-center));
                Assert.Less(Vector2.Distance(expected,vertex.position), .001f, "The stencil must rotate with the visible map, not with layout");
            }
            for (var i = 0; i < 7; i++) Assert.IsNotNull(root.Find("DesignMapRail/Legend" + i + "/Dot"));
            var panel = root.GetComponent<DesktopWorldMapPanel>() ?? root.gameObject.AddComponent<DesktopWorldMapPanel>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(DesktopWorldMapPanel).GetField("_viewport",flags).SetValue(panel, viewport);
            typeof(DesktopWorldMapPanel).GetField("_content",flags).SetValue(panel, content);
            var clamp = typeof(DesktopWorldMapPanel).GetMethod("ClampContent",flags);
            content.anchoredPosition = new Vector2(900,-900); clamp.Invoke(panel,null);
            Assert.AreEqual(Vector2.zero,content.anchoredPosition);
            content.anchoredPosition = new Vector2(-900,900); clamp.Invoke(panel,null);
            Assert.AreEqual(new Vector2(-360,280),content.anchoredPosition);
            typeof(DesktopWorldMapPanel).GetField("_zoom",flags).SetValue(panel,.65f);
            clamp.Invoke(panel,null);
            Assert.GreaterOrEqual(content.rect.width * content.localScale.x, viewport.rect.width - .001f);
            Assert.GreaterOrEqual(content.rect.height * content.localScale.y, viewport.rect.height - .001f);
        }

        [Test]
        public void NativeMarkerKeepsWhiteLowerBodyAndFullBuoyWhenLocked()
        {
            var root = Load("WorldMapPondMarker");
            var view = root.GetComponent<CozyPondMarkerView>() ?? root.gameObject.AddComponent<CozyPondMarkerView>();
            view.Bind("试验鱼塘");
            var position = ((RectTransform)root).anchoredPosition;
            var body = root.Find("Icon").GetComponent<Image>();
            var fill = root.Find("Icon/Color").GetComponent<Image>();
            view.SetState(true, false, CozyPondMarkerView.CategoryColor("forbidden"));
            Assert.AreEqual(Color.white, body.color);
            Assert.AreEqual(CozyPondMarkerView.CategoryColor("forbidden"), fill.color);
            Assert.IsTrue(root.Find("Paw").gameObject.activeSelf);
            view.SetState(false, true, CozyPondMarkerView.CategoryColor("forbidden"));
            Assert.AreSame(CozyJournalTheme.Sprite("motion/bobber-base"), body.sprite);
            Assert.AreEqual(.7f, body.color.a, .001f);
            Assert.IsFalse(root.Find("Paw").gameObject.activeSelf);
            Assert.IsTrue(root.Find("LabelPill").gameObject.activeSelf);
            Assert.IsTrue(root.Find("Label").gameObject.activeSelf, "Unselected and locked ponds retain their map labels");
            Assert.AreEqual(position, ((RectTransform)root).anchoredPosition);
        }

        [Test]
        public void PondCardsUseTwoColumnsWithoutALegacyVerticalLayout()
        {
            var root = Load("PanelPondSelection");
            var cards = root.Find("PondCardScroll/Viewport/Cards");
            var grid = cards.GetComponent<GridLayoutGroup>();
            Assert.IsNotNull(grid); Assert.IsTrue(grid.enabled);
            Assert.IsNull(cards.GetComponent<VerticalLayoutGroup>());
            Assert.AreEqual(2, grid.constraintCount);
            Assert.AreEqual(new Vector2(329.5f, 220), grid.cellSize);
            Assert.AreEqual(new Vector2(23,23), grid.spacing);
            Assert.AreEqual(Vector2.zero, Rect(root,"PondCardScroll/Viewport").offsetMin);
        }

        [TestCase(1024f,640f,278f,140f,1f,0.8057554f)]
        [TestCase(1024f,640f,132f,132f,0.625f,1f)]
        public void PhotoCoverCropsWithoutStretching(float w, float h, float frameW, float frameH, float x, float y)
        {
            var crop = CozyPondPhotoCover.CropScale(new Vector2(w,h),new Vector2(frameW,frameH));
            Assert.AreEqual(x,crop.x,.0001f); Assert.AreEqual(y,crop.y,.0001f);
            Assert.AreEqual(w*crop.x/(h*crop.y), frameW/frameH,.0001f);
        }
    }
}
#endif
