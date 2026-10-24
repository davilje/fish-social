#if UNITY_INCLUDE_TESTS
using System;
using FishSocial.Desktop.Pet;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class DesktopFishingRestMotionTests
    {
        [Test]
        public void DayLongBreathingKeepsFootFixedAndScaleBounded()
        {
            var motion = new FishingRestMotion();
            for (var time = 0; time <= 86400000; time += 1577)
            {
                var pose = motion.Update(time, true, time >= 1800000).Body;
                Assert.AreEqual(264, pose.X(264, 450), 1e-8);
                Assert.AreEqual(450, pose.Y(264, 450), 1e-8);
                Assert.That(pose.XX * pose.YY - pose.YX * pose.XY, Is.InRange(.991 - 1e-10, 1.009 + 1e-10));
            }
        }

        [Test]
        public void SleepWakeReversalIsContinuousAndTimeAloneDoesNotSleep()
        {
            var motion = new FishingRestMotion();
            motion.Update(0, true, false);
            Assert.AreEqual(0, motion.Update(86400000, true, false).SleepBlend);
            Assert.AreEqual(0, motion.Update(86400001, true, true).SleepBlend);
            Assert.AreEqual(.5, motion.Update(86400401, true, true).SleepBlend, 1e-9);
            Assert.AreEqual(.5, motion.Update(86400401, true, false).SleepBlend, 1e-9);
            Assert.AreEqual(.25, motion.Update(86400801, true, false).SleepBlend, 1e-9);
            Assert.AreEqual(0, motion.Update(86401201, true, false).SleepBlend);
            var stopped = motion.Update(86401202, false, true);
            Assert.AreEqual(FishingRestTransform.Identity, stopped.Body);
            Assert.AreEqual(0, stopped.BubbleOpacity);
            Assert.AreEqual(FishingRestTransform.Identity, motion.Update(86405000, true, false).Body);
        }

        [Test]
        public void LineFollowsRodTipAndKeepsFloatEndpointFixed()
        {
            for (var time = 0; time < 12000; time += 37)
            {
                var pose = FishingRestMotion.Sample(time, time / 12000d);
                var tipX = FishingRestMotion.RodTipX;
                var tipY = FishingRestMotion.RodTipY;
                var bottom = FishingRestMotion.LineBottom;
                var line = pose.Line(tipX, tipY, bottom);
                Assert.AreEqual(pose.Body.X(tipX, tipY), line.X(tipX, tipY), 1e-8);
                Assert.AreEqual(pose.Body.Y(tipX, tipY), line.Y(tipX, tipY), 1e-8);
                Assert.AreEqual(570, line.X(570, bottom), 1e-8);
                Assert.AreEqual(bottom, line.Y(570, bottom), 1e-8);
            }
        }

        [TestCase(640, 512)]
        [TestCase(1280, 1024)]
        [TestCase(233, 187)]
        [TestCase(800, 512)]
        public void MeshUsesLogicalCanvasAcrossResolutionAndPreservesUv(float width, float height)
        {
            var host = new GameObject("RestMesh", typeof(RectTransform), typeof(Image));
            try
            {
                var rectTransform = host.GetComponent<RectTransform>();
                rectTransform.sizeDelta = new Vector2(width, height);
                var effect = host.AddComponent<FishingRestLayerEffect>();
                var rect = rectTransform.rect;
                var foot = new Vector3(rect.xMin + 264 * width / 640, rect.yMax - 450 * height / 512, 0);
                var pose = FishingRestMotion.Sample(1700, 1).Body;
                Assert.That(Vector3.Distance(foot, FishingRestLayerEffect.TransformPoint(foot, rect, pose)), Is.LessThan(.0002f));
                effect.SetPose(pose);
                using (var vertices = new VertexHelper())
                {
                    var original = UIVertex.simpleVert;
                    original.position = new Vector3(rect.xMin, rect.yMax, 0);
                    original.uv0 = new Vector2(.2f, .7f);
                    vertices.AddVert(original);
                    effect.ModifyMesh(vertices);
                    var transformed = UIVertex.simpleVert;
                    vertices.PopulateUIVertex(ref transformed, 0);
                    Assert.AreEqual(original.uv0, transformed.uv0);
                    Assert.AreEqual(FishingRestLayerEffect.TransformPoint(original.position, rect, pose), transformed.position);
                }
                Assert.AreEqual(Vector3.one, rectTransform.localScale);
                Assert.AreEqual(Quaternion.identity, rectTransform.localRotation);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [TestCase("orange")]
        [TestCase("calico")]
        [TestCase("silver")]
        [TestCase("brown")]
        [TestCase("cream")]
        [TestCase("blue")]
        [TestCase("black")]
        [TestCase("white")]
        [TestCase("cow")]
        [TestCase("dafeiyu")]
        [TestCase("sangbiao")]
        public void EverySkinSharesBodyRodGripAndLeavesBobberUntouched(string skinId)
        {
            var host = new GameObject("RestLayers", typeof(RectTransform));
            try
            {
                var layers = new Image[FishingLayerArt.LayerNames.Length];
                for (var index = 0; index < layers.Length; index++)
                {
                    var child = new GameObject(FishingLayerArt.LayerNames[index], typeof(RectTransform), typeof(Image));
                    child.transform.SetParent(host.transform, false);
                    layers[index] = child.GetComponent<Image>();
                    layers[index].rectTransform.sizeDelta = new Vector2(233, 187);
                    layers[index].rectTransform.anchoredPosition = new Vector2(17, -29);
                }
                var presentation = new FishingRestPresentation(layers, host.GetComponent<RectTransform>());
                presentation.Update(0, true, true, skinId, null);
                presentation.Update(.8f, true, true, skinId, null);
                var body = layers[0].GetComponent<FishingRestLayerEffect>().Pose;
                foreach (var name in new[] { "head", "far_hand", "hands", "rod" })
                    Assert.AreEqual(body, layers[Array.IndexOf(FishingLayerArt.LayerNames, name)].GetComponent<FishingRestLayerEffect>().Pose);
                foreach (var name in new[] { "bobber", "bait", "fish" })
                    Assert.IsNull(layers[Array.IndexOf(FishingLayerArt.LayerNames, name)].GetComponent<FishingRestLayerEffect>());
                Assert.IsTrue(host.transform.Find("FishingSleepBadge").gameObject.activeSelf);
                presentation.Reset();
                Assert.IsFalse(host.transform.Find("FishingSleepBadge").gameObject.activeSelf);
                foreach (var layer in layers)
                {
                    var effect = layer.GetComponent<FishingRestLayerEffect>();
                    if (effect != null)
                        Assert.AreEqual(FishingRestTransform.Identity, effect.Pose);
                    Assert.AreEqual(new Vector2(233, 187), layer.rectTransform.sizeDelta);
                    Assert.AreEqual(new Vector2(17, -29), layer.rectTransform.anchoredPosition);
                    Assert.AreEqual(Vector3.one, layer.rectTransform.localScale);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [TestCase("orange", 640, 512)]
        [TestCase("calico", 640, 512)]
        [TestCase("silver", 640, 512)]
        [TestCase("brown", 640, 512)]
        [TestCase("cream", 640, 512)]
        [TestCase("blue", 640, 512)]
        [TestCase("black", 640, 512)]
        [TestCase("white", 640, 512)]
        [TestCase("cow", 640, 512)]
        [TestCase("dafeiyu", 1280, 1024)]
        [TestCase("sangbiao", 640, 512)]
        public void OptionalSleepResourceUsesFullCanvasWithoutResampling(string skinId, int width, int height)
        {
            var sprite = Resources.Load<Sprite>(FishingRestPresentation.SleepHeadResourcePath(skinId));
            Assert.IsNotNull(sprite);
            Assert.AreEqual(new Rect(0, 0, width, height), sprite.rect);
            Assert.AreEqual(width, sprite.texture.width);
            Assert.AreEqual(height, sprite.texture.height);
            var expected = Resources.Load<TextAsset>(FishingRestPresentation.SleepHeadResourcePath(skinId) + "-source");
            Assert.IsNotNull(expected);
            var source = System.IO.Path.Combine(Application.streamingAssetsPath, "Fishing", skinId, "body/fishing/0/head.png");
            Assert.IsTrue(FishingRestPresentation.MatchesSourceFile(source, expected.text.Trim()));
        }

        [Test]
        public void DifferentOrMissingAwakeArtCannotEnableSleepReplacement()
        {
            var expected = Resources.Load<TextAsset>(FishingRestPresentation.SleepHeadResourcePath("orange") + "-source");
            Assert.IsNotNull(expected);
            var source = System.IO.Path.Combine(Application.streamingAssetsPath, "Fishing/orange/body/fishing/0/head.png");
            Assert.IsTrue(FishingRestPresentation.MatchesSourceFile(source, expected.text.Trim()));
            Assert.IsFalse(FishingRestPresentation.MatchesSourceFile(source, new string('0', 64)));
            Assert.IsFalse(FishingRestPresentation.MatchesSourceFile(source + ".missing", expected.text.Trim()));
        }

        [TestCase(false, false, false)]
        [TestCase(false, false, true)]
        [TestCase(false, true, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(true, true, true)]
        public void ResetOnlyRestoresItsOwnSleepOverride(bool transparentAwake, bool newFrameWritten, bool inactiveUpdate)
        {
            var host = new GameObject("RestResetOwnership", typeof(RectTransform));
            var awake = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            var nextFrame = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            try
            {
                var layers = new Image[FishingLayerArt.LayerNames.Length];
                for (var index = 0; index < layers.Length; index++)
                {
                    var child = new GameObject(FishingLayerArt.LayerNames[index], typeof(RectTransform), typeof(Image));
                    child.transform.SetParent(host.transform, false);
                    layers[index] = child.GetComponent<Image>();
                    layers[index].rectTransform.sizeDelta = new Vector2(640, 512);
                }
                var presentation = new FishingRestPresentation(layers, host.GetComponent<RectTransform>());
                var original = transparentAwake ? null : awake;
                presentation.Update(0, true, true, "orange", original);
                presentation.Update(.8f, true, true, "orange", original);
                var head = layers[Array.IndexOf(FishingLayerArt.LayerNames, "head")];
                Assert.AreSame(Resources.Load<Sprite>(FishingRestPresentation.SleepHeadResourcePath("orange")), head.sprite);
                if (newFrameWritten)
                    head.sprite = nextFrame;
                if (inactiveUpdate)
                    presentation.Update(0, false, false, "orange", nextFrame);
                else
                    presentation.Reset();
                Assert.AreSame(newFrameWritten ? nextFrame : original, head.sprite);
                Assert.IsFalse(host.transform.Find("FishingSleepBadge").gameObject.activeSelf);
                foreach (var effect in host.GetComponentsInChildren<FishingRestLayerEffect>())
                    Assert.AreEqual(FishingRestTransform.Identity, effect.Pose);
                head.sprite = nextFrame;
                presentation.Reset();
                Assert.AreSame(nextFrame, head.sprite);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(awake);
                UnityEngine.Object.DestroyImmediate(nextFrame);
            }
        }
    }
}
#endif
