#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using FishSocial.Desktop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyIndexMotionTests
    {
        GameObject root;
        [TearDown] public void Cleanup() { if(root!=null)Object.DestroyImmediate(root); }

        // Values sampled from Chromium WAAPI using original .tab easing and keyframes.
        [TestCase(.03f,-6.185f)]
        [TestCase(.06f,-10.4876f)]
        [TestCase(.10f,-13.9552f)]
        [TestCase(.16f,-15.8625f)]
        [TestCase(.20f,-16f)]
        public void SelectionTimelineMatchesBrowserFrames(float seconds,float expected)
        {
            var motion=new CozyIndexMotion();motion.Select(true);motion.Advance(seconds);
            Assert.AreEqual(expected,motion.Current.X,.002f);
        }

        [TestCase(.03f,1.00293f,1.0056f)]
        [TestCase(.06f,.984568f,1.03086f)]
        [TestCase(.11f,.994888f,1.01022f)]
        [TestCase(.16f,.999141f,1.00172f)]
        [TestCase(.22f,1f,1f)]
        public void ReleaseUsesBrowserWholeAnimationEasingBeforeItsHalfwayKeyframe(float seconds,float x,float y)
        {
            var motion=new CozyIndexMotion();motion.PointerDown();motion.Advance(.06f);motion.PointerUp();motion.Advance(seconds);
            Assert.AreEqual(x,motion.Current.Scale.x,.0001f);Assert.AreEqual(y,motion.Current.Scale.y,.0001f);
        }

        [Test]
        public void RepeatedSelectionDoesNotRestartAndReversalContinuesFromCurrentFrame()
        {
            var motion=new CozyIndexMotion();motion.Select(true);motion.Advance(.1f);
            motion.Select(true);motion.Advance(.1f);Assert.AreEqual(-16,motion.Current.X,.001f);
            motion.Select(false);motion.Advance(.05f);var at=motion.Current.X;
            motion.Select(true);Assert.AreEqual(at,motion.Current.X,.001f);
            motion.Advance(.2f);Assert.AreEqual(-16,motion.Current.X,.001f);
        }

        [Test]
        public void RailEntranceUsesThirtyMillisecondStaggerAndOverridesUnderlyingSelectionTranslate()
        {
            var motion=new CozyIndexMotion(true);motion.BeginEntrance(2);
            motion.Advance(.03f);Assert.AreEqual(-18,motion.Current.X);Assert.AreEqual(0,motion.Current.Alpha);
            motion.Advance(.13f);
            Assert.AreEqual(-16.2556f,motion.Current.X,.002f);Assert.AreEqual(.8722f,motion.Current.Alpha,.002f);
            motion.Advance(.10f);Assert.AreEqual(-16,motion.Current.X);Assert.AreEqual(1,motion.Current.Alpha);
        }

        [Test]
        public void SelectionColorUsesCssEaseOver160msAndIsIndependentOf200msMotion()
        {
            var motion=new CozyIndexMotion();motion.Select(true);motion.Advance(.06f);
            var color=Color.Lerp(new Color32(117,71,46,255),Color.white,motion.Current.Selection);
            Assert.AreEqual(206,Mathf.RoundToInt(color.r*255));Assert.AreEqual(190,Mathf.RoundToInt(color.g*255));Assert.AreEqual(181,Mathf.RoundToInt(color.b*255));
            motion.Advance(.1f);Assert.AreEqual(1,motion.Current.Selection);Assert.Greater(motion.Current.X,-16);
        }

        CozyButtonVisual CreateIndex()
        {
            root=new GameObject("Tab",typeof(RectTransform),typeof(Image),typeof(Button));
            ((RectTransform)root.transform).sizeDelta=new Vector2(190,76);
            var label=new GameObject("Label",typeof(RectTransform),typeof(Text));label.transform.SetParent(root.transform,false);
            label.GetComponent<Text>().font=Resources.GetBuiltinResource<Font>("Arial.ttf");label.GetComponent<Text>().text="Index";
            CatTheme.StyleButton(root.GetComponent<Button>());
            var visual=root.GetComponent<CozyButtonVisual>();
            visual.ConfigureSurface("surfaces/tab-index-default","surfaces/tab-index-hover",null,"surfaces/tab-index-selected","surfaces/tab-index-disabled");
            visual.ApplyFinalPose();return visual;
        }

        [Test]
        public void PointerExitKeepsHeldPressAndHoverNeverChangesIndexOffsetOrSurface()
        {
            var visual=CreateIndex();var rect=(RectTransform)root.transform;var position=rect.anchoredPosition;
            Assert.IsNotNull(root.transform.Find("JournalIndexPaint").GetComponent<CanvasRenderer>());
            visual.OnPointerEnter(null);visual.AdvanceIndex(.2f);
            Assert.AreEqual(0,visual.IndexPose.X);
            Assert.AreSame(CozyJournalTheme.Sprite("surfaces/tab-index-default"),root.transform.Find("CozyVisual").GetComponent<Image>().sprite);
            visual.OnPointerDown(new PointerEventData(null){button=PointerEventData.InputButton.Left});visual.AdvanceIndex(.06f);
            visual.OnPointerExit(null);visual.AdvanceIndex(.1f);
            Assert.AreEqual(new Vector2(1.06f,.92f),visual.IndexPose.Scale);
            visual.OnPointerUp(new PointerEventData(null){button=PointerEventData.InputButton.Left});visual.AdvanceIndex(.22f);
            Assert.AreEqual(Vector2.one,visual.IndexPose.Scale);
            visual.SetSelected(true);visual.AdvanceIndex(.2f);visual.OnPointerEnter(null);visual.AdvanceIndex(.2f);
            Assert.AreEqual(-16,visual.IndexPose.X);Assert.AreEqual(position,rect.anchoredPosition);Assert.AreEqual(Vector3.one,rect.localScale);
        }
    }
}
#endif
