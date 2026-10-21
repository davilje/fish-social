#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using FishSocial.Desktop;
using FishSocial.Desktop.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyPondMarkerMotionTests
    {
        // Golden values from Chromium running the original bindMap/selectPond keyframes.
        [TestCase(.08f,1.16311f,1,0,1)]
        [TestCase(.12f,1.15895f,1.6f,30,0)]
        [TestCase(.16f,1.15f,1.25353f,12.6763f,.490838f)]
        [TestCase(.25f,1.15f,.94756f,-2.62202f,.924291f)]
        [TestCase(.38f,1.15f,1,0,1)]
        public void HoverAndPawMatchBrowserFrames(float seconds,float body,float pawScale,float pawY,float pawAlpha)
        {
            var motion=new CozyPondMarkerMotion();motion.Hover(true);motion.Select(true);motion.Advance(seconds);
            Assert.AreEqual(body,motion.Current.BodyScale,.0001f);Assert.AreEqual(pawScale,motion.Current.PawScale,.0001f);
            Assert.AreEqual(pawY,motion.Current.PawY,.001f);Assert.AreEqual(pawAlpha,motion.Current.PawAlpha,.0001f);
        }
        [Test] public void ExitCancelsBodyScaleImmediatelyButKeepsLabelAnimation()
        {
            var motion=new CozyPondMarkerMotion();motion.Hover(true);motion.Advance(.04f);var label=motion.Current.LabelY;
            motion.Hover(false);Assert.AreEqual(1,motion.Current.BodyScale);Assert.AreEqual(label,motion.Current.LabelY);
            motion.Advance(.12f);Assert.AreEqual(0,motion.Current.LabelY);Assert.AreEqual(1,motion.Current.LabelAlpha);
        }
        [Test] public void LockedShakeIsLinear200msAndEntryResetRestoresBodyAfterFailure()
        {
            var motion=new CozyPondMarkerMotion();motion.Shake();motion.Advance(.05f);Assert.AreEqual(-4,motion.Current.BodyX);
            motion.Advance(.05f);Assert.AreEqual(4,motion.Current.BodyX);motion.Advance(.1f);Assert.AreEqual(0,motion.Current.BodyX);
            motion.Enter(true);motion.Advance(.36f);Assert.AreEqual(-16,motion.Current.BodyY);Assert.AreEqual(0,motion.Current.BodyAlpha);
            motion.Enter(false);Assert.AreEqual(0,motion.Current.BodyY);Assert.AreEqual(1,motion.Current.BodyAlpha);
        }
        [Test] public void ProductionMarkerKeepsItsHitRectWhileAnimatingAllThreeBodyLayers()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(DesktopPrefabCatalog.AssetPath("WorldMapPondMarker"));
            var instance=Object.Instantiate(prefab);
            try
            {
                var rect=(RectTransform)instance.transform;var position=rect.anchoredPosition;var size=rect.sizeDelta;
                var view=instance.GetComponent<CozyPondMarkerView>() ?? instance.AddComponent<CozyPondMarkerView>();
                view.Bind("测试鱼塘");view.SetState(true,false,Color.green);view.OnPointerEnter(null);view.Advance(.08f);
                Assert.Greater(view.CurrentPose.BodyScale,1.15f);Assert.AreEqual(position,rect.anchoredPosition);
                Assert.AreEqual(size,rect.sizeDelta);Assert.AreEqual(Vector3.one,rect.localScale);
                foreach(var image in instance.transform.Find("Icon").GetComponentsInChildren<Image>())
                    Assert.IsNotNull(image.GetComponent<CozyGraphicOffset>());
                view.SetEntering(true);view.Advance(.36f);view.SetEntering(false);Assert.AreEqual(1,view.CurrentPose.BodyAlpha);
            }
            finally{Object.DestroyImmediate(instance);}
        }
    }
}
#endif
