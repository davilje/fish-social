#if UNITY_INCLUDE_TESTS
using System.Collections;
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
            public bool TryResolvePublishedContact(string clip, int frame, string layer, out UnityFishingWaterContact contact)
            {
                contact = new UnityFishingWaterContact { Anchor = new Vector2(50, 40), WaterClip = new Rect(0, 0, 100, 80),
                    EffectRoot = Root, SlotScale = 1, Phase = "hooked", VisibleBodyLengthAt128Px = 60 };
                return Contact;
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
