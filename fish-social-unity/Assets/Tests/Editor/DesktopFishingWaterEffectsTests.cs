#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using System.Reflection;
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Pet;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class DesktopFishingWaterEffectsTests
    {
        sealed class ContactProvider : IUnityFishingWaterAnchorProvider
        {
            public RectTransform Root;
            public bool Contact = true;
            public string LastLayer, OnlyLayer;
            public bool RejectFish;
            public float BodyLength = 60;
            public bool TryResolvePublishedContact(string clip, int frame, string layer, out UnityFishingWaterContact contact)
            {
                contact = new UnityFishingWaterContact { Anchor = new Vector2(50, 40), WaterClip = new Rect(0, 0, 100, 80),
                    EffectRoot = Root, SlotScale = 1, Phase = "hooked", VisibleBodyLengthAt128Px = BodyLength };
                var valid = Contact && (!RejectFish || layer != "fish") && (OnlyLayer == null || layer == OnlyLayer);
                if (valid) LastLayer = layer;
                return valid;
            }
        }
        static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static object Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);

        [UnityTest]
        public IEnumerator InjectedRealContactDrawsFixedPool_MissingContactAndNativeOwnerStaySilent()
        {
            var previousApp = DesktopAppBootstrap.Instance;
            var preference = PlayerPrefs.GetString("FishSocial.FishingEffectsQuality", "standard");
            var reduced = CozyMotion.Reduced;
            var host = new GameObject("WaterEffectsTest"); host.SetActive(false);
            var root = new GameObject("WaterRoot", typeof(RectTransform), typeof(Canvas)); root.transform.SetParent(host.transform, false);
            var poolHost = new GameObject("WaterPool"); poolHost.SetActive(false); poolHost.transform.SetParent(host.transform, false);
            try
            {
                PlayerPrefs.SetString("FishSocial.FishingEffectsQuality", "standard");
                CozyMotion.SetReduced(false);
                var app = host.AddComponent<DesktopAppBootstrap>();
                var pond = host.AddComponent<SocialPondSessionController>();
                Property(pond, "CurrentUser", new PondUserDto { id = "self", fishingPhase = "hooked", spotId = "spot" });
                Set(app, "_pondSession", pond);
                typeof(DesktopAppBootstrap).GetProperty("Instance").SetValue(null, app);
                var session = app.FishingPresentation; session.Reset("pond");
                session.Observe("self", "one", "bite-one", 1, "hooked", "spot", "orange", false);
                var pool = poolHost.AddComponent<UnityFishingWaterEffects>();
                Call(pool, "Awake");
                var load = (IEnumerator)Call(pool, "LoadResources");
                while (load.MoveNext()) yield return load.Current;
                Assert.IsTrue(pool.ResourcesReady, "real production effects.json and PNGs prewarm");
                var bridge = new UnityFishingWaterMarkerBridge();
                UnityFishingWaterMarker lastPublished = null;
                bridge.MarkerPublished += marker => lastPublished = marker;
                bridge.MarkerPublished += marker => pool.TryPublish(marker, true);
                bridge.Publish("hooked", 0, 0);
                Assert.AreEqual(0, pool.ActiveGroups, "portrait without actual water contact is silent");
                bridge.AnchorProvider = new ContactProvider { Root = (RectTransform)root.transform };
                session.Observe("self", "two", "bite-two", 2, "hooked", "spot", "orange", false);
                bridge.Publish("hooked", 0, 0);
                Assert.Greater(pool.ActiveGroups, 0, "injected view supplies a real contact");
                Assert.LessOrEqual(pool.ActiveGroups, 2);
                Assert.LessOrEqual(pool.ActiveInstances, 12);
                foreach (var image in root.GetComponentsInChildren<RawImage>(true)) Assert.IsFalse(image.raycastTarget);
                // Bite is a water-surface ripple only. Every transformed ripple is clipped
                // by a separate mask whose top is the actual anchor, even under a scaled view.
                root.transform.localScale = new Vector3(1.25f, .8f, 1);
                Call(pool, "Update");
                var rippleImages = 0;
                foreach (var image in root.GetComponentsInChildren<RawImage>(true))
                {
                    if (image.texture == null) continue;
                    rippleImages++;
                    Assert.IsTrue(image.texture.name.StartsWith("ripple-"), "bite cannot create a crown/drop");
                    var mask = image.transform.parent.GetComponent<RectMask2D>();
                    Assert.IsNotNull(mask, "ripple has an independent post-transform waterline mask");
                    var clip = (RectTransform)mask.transform;
                    Assert.AreEqual("BelowWaterlineRipples", clip.name);
                    Assert.AreEqual(40, clip.anchoredPosition.y + clip.rect.yMax, .001f, "mask top equals contact, no pixels above waterline");
                    Assert.AreEqual(100, clip.rect.width, .001f);
                    Assert.AreEqual(128, image.texture.width, "real approved v4 PNG");
                    Assert.AreEqual(64, image.texture.height);
                    var png = new Texture2D(2, 2);
                    try
                    {
                        Assert.IsTrue(png.LoadImage(File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,
                            "Effects", "Fishing", "v4", image.texture.name + ".png")), false));
                        var alpha = png.GetPixels32();
                        var bottom = png.height; var top = 0;
                        for (var pixel = 0; pixel < alpha.Length; pixel++)
                            if (alpha[pixel].a > 0) { bottom = Mathf.Min(bottom, pixel / png.width); top = Mathf.Max(top, pixel / png.width + 1); }
                        var rect = image.rectTransform;
                        var transformedBottom = clip.InverseTransformPoint(rect.TransformPoint(new Vector3(0, bottom - png.height * rect.pivot.y)));
                        var transformedTop = clip.InverseTransformPoint(rect.TransformPoint(new Vector3(0, top - png.height * rect.pivot.y)));
                        Assert.LessOrEqual(transformedTop.y, clip.rect.yMax + .001f, "whole alpha ellipse below actual waterline after transforms");
                        Assert.GreaterOrEqual(transformedBottom.y, clip.rect.yMin - .001f, "ellipse not cut to a semicircle");
                    }
                    finally { Object.DestroyImmediate(png); }
                }
                Assert.Greater(rippleImages, 0, "assertions cover actual pooled PNG images");
                var active = pool.ActiveGroups;
                session.SetOwner(true);
                session.Observe("self", "three", "bite-three", 3, "hooked", "spot", "orange", false);
                bridge.Publish("hooked", 0, 0);
                Assert.AreEqual(active, pool.ActiveGroups, "native owner cannot admit Unity visual duplicates");
                // View rebuilds may destroy a water root while the global texture pool survives.
                for (var visit = 0; visit < 30; visit++)
                {
                    Object.DestroyImmediate(root);
                    Call(pool, "Update");
                    Assert.AreEqual(0, pool.ActiveGroups, "destroyed view releases groups");
                    root = new GameObject("WaterRoot" + visit, typeof(RectTransform), typeof(Canvas)); root.transform.SetParent(host.transform, false);
                    bridge.AnchorProvider = new ContactProvider { Root = (RectTransform)root.transform };
                    session.SetOwner(false);
                    session.Observe("self", "visit" + visit, "bite-visit" + visit, 4 + visit, "hooked", "spot", "orange", false);
                    bridge.Publish("hooked", 0, 0);
                    Assert.Greater(pool.ActiveGroups, 0, "reconstructed view admits current event");
                    Assert.LessOrEqual(pool.ActiveInstances, 12);
                }
                // Actual retrieval source metadata is distinct from the audio retrieve.start.
                foreach (var clipName in new[] { "reel", "reel_empty" })
                {
                    Object.DestroyImmediate(root); Call(pool, "Update");
                    root = new GameObject("RetrieveWater", typeof(RectTransform), typeof(Canvas)); root.transform.SetParent(host.transform, false);
                    var provider = new ContactProvider { Root = (RectTransform)root.transform, Contact = false, RejectFish = true };
                    bridge.AnchorProvider = provider;
                    pond.CurrentUser.fishingPhase = "resolving";
                    session.Observe("self", clipName + "-miss", "resolve-miss", clipName == "reel" ? 100 : 102, "resolving", "spot", "orange", false);
                    bridge.Publish(clipName, 0, 0);
                    Assert.AreEqual(0, pool.ActiveGroups, "source frame without actual layer contact cannot splash");
                    provider.Contact = true;
                    session.Observe("self", clipName + "-hit", "resolve-hit", clipName == "reel" ? 101 : 103, "resolving", "spot", "orange", false);
                    bridge.Publish(clipName, 0, 0);
                    Assert.AreEqual(clipName == "reel" ? "line" : "bobber", provider.LastLayer, "retrieval resolves the actual published contact layer");
                    Assert.AreEqual(1, pool.ActiveGroups, "real retrieval contact draws once in resolve window");
                    var splashes = 0;
                    foreach (var image in root.GetComponentsInChildren<RawImage>(true))
                        if (image.texture != null && (image.texture.name.StartsWith("splash-") || image.texture.name.StartsWith("drop-"))) splashes++;
                    Assert.Greater(splashes, 0, "real retrieval allows a small splash above the line");
                    bridge.Publish(clipName, 0, 1);
                    Assert.AreEqual(1, pool.ActiveGroups, "reel loop does not repeat one-shot water contact");
                    session.Cancel("self");
                    bridge.Publish(clipName, 0, 2);
                    Assert.AreEqual(1, pool.ActiveGroups, "hard cancellation cannot admit another retrieval");
                }
                Object.DestroyImmediate(root); Call(pool, "Update");
                root = new GameObject("GroundbaitWater", typeof(RectTransform), typeof(Canvas)); root.transform.SetParent(host.transform, false);
                var groundbaitContact = new ContactProvider { Root = (RectTransform)root.transform, Contact = false };
                bridge.AnchorProvider = groundbaitContact;
                pond.CurrentUser.fishingPhase = "groundbaiting";
                session.Observe("self", "bait-contact", "bait-hit", 200, "groundbaiting", "spot", "orange", false);
                bridge.Publish("groundbait", 5, 0);
                Assert.AreEqual(0, pool.ActiveGroups, "source5 begins before interpolated bait really touches water");
                groundbaitContact.Contact = true;
                bridge.Publish("groundbait", 5, 0);
                Assert.AreEqual(1, pool.ActiveGroups, "same source5 later interpolation reaches actual water contact");
                bridge.Publish("groundbait", 5, 0);
                Assert.AreEqual(1, pool.ActiveGroups, "same contact source occurrence is emitted only once");
                Assert.AreEqual(4, pool.ActiveInstances, "groundbait is two arcs plus one crown and one drop");
                var arcX = 0f; var arcCount = 0; var splashCount = 0;
                foreach (var image in root.GetComponentsInChildren<RawImage>(true))
                {
                    if (image.texture == null) continue;
                    var x = image.rectTransform.anchoredPosition.x;
                    if (image.texture.name.StartsWith("ripple-")) { arcCount++; arcX += x; Assert.AreEqual(3.5f, Mathf.Abs(x - 50), .001f); }
                    else { splashCount++; Assert.AreEqual(50, x, .001f, "small crown/drop originates at the actual contact X"); }
                }
                Assert.AreEqual(2, arcCount);
                Assert.AreEqual(100, arcX, .001f, "groundbait arcs are symmetric around actual contact X");
                Assert.AreEqual(2, splashCount);
                foreach (var body in new[] { 12f, 60f })
                {
                    Object.DestroyImmediate(root); Call(pool, "Update");
                    root = new GameObject("SurfaceWater", typeof(RectTransform), typeof(Canvas)); root.transform.SetParent(host.transform, false);
                    var surface = new ContactProvider { Root = (RectTransform)root.transform, OnlyLayer = "fish", BodyLength = body, Contact = false };
                    bridge.AnchorProvider = surface;
                    pond.CurrentUser.fishingPhase = "resolving";
                    session.Observe("self", "surface-" + body, "surface-event-" + body, body < 20 ? 201 : 202, "resolving", "spot", "orange", false);
                    bridge.Publish("reel", 0, 0);
                    Assert.AreEqual(0, pool.ActiveGroups, "reel source0 begins before visible fish crosses actual waterline");
                    surface.Contact = true;
                    bridge.Publish("reel", 0, 0);
                    Assert.AreEqual(1, pool.ActiveGroups, "same source0 later interpolation publishes actual fish surface");
                    Assert.AreEqual("fish.surface", lastPublished.Marker);
                    Assert.AreEqual(body < 20 ? "S" : "XL", lastPublished.Envelope.waterSizeTier, "surface tier comes from actually visible fish body");
                    Assert.AreEqual(body < 20 ? 4 : 6, pool.ActiveInstances, "visible fish size controls surface splash count");
                    bridge.Publish("reel", 0, 0);
                    Assert.AreEqual(1, pool.ActiveGroups, "surface once per retrieve action");
                    Call(pool, "Update");
                    var activeGroups = (System.Array)pool.GetType().GetField("groups", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool);
                    foreach (var group in activeGroups)
                        if ((bool)group.GetType().GetField("Used").GetValue(group))
                            Assert.IsTrue(float.IsPositiveInfinity((float)group.GetType().GetField("EndAt").GetValue(group)), "real retrieve surface is not prematurely cancelled by resolving phase");
                    session.Cancel("self");
                    bridge.Publish("reel", 0, 1);
                    Assert.AreEqual(1, pool.ActiveGroups, "hard cancel does not admit another surface");
                }
            }
            finally
            {
                typeof(DesktopAppBootstrap).GetProperty("Instance").SetValue(null, previousApp);
                PlayerPrefs.SetString("FishSocial.FishingEffectsQuality", preference);
                CozyMotion.SetReduced(reduced);
                Object.DestroyImmediate(host);
            }
        }
    }
}
#endif
