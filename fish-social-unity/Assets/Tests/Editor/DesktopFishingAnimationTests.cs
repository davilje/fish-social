#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Pet;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class DesktopFishingAnimationTests
    {
        static readonly int[] Counts = { 1, 4, 1, 4, 4, 1, 4, 6, 10, 6, 7, 4, 6, 4, 8, 4 };
        static readonly string[] Playback =
        {
            "hold", "once", "hold", "once", "once", "hold", "once", "intro-loop",
            "intro-loop", "once", "once", "once", "once", "once",
            "once", "once",
        };
        static readonly string[] NextClips =
        {
            null, "ready", null, null, null, null, "effort", null, null,
            "catch", "reset_catch", "ready", "reset_empty", "ready",
            "ready", "idle",
        };

        [Test]
        public void VersionTwoManifestAcceptsAllSourceClips()
        {
            Assert.IsTrue(FishingLayerArt.ValidateManifest(Manifest(), out var error), error);
        }

        [TestCase(null, 1)]
        [TestCase("1", 1)]
        [TestCase("2", 2)]
        [TestCase("4", 4)]
        public void BodyTextureScaleDefaultsToOneAndPreservesLogicalPlacement(string value, int expected)
        {
            var manifest = value == null ? Manifest() : ManifestWithTextureScale(value);
            Assert.IsTrue(FishingLayerArt.ValidateManifest(manifest, out var error), error);
            var pack = Pack(manifest);
            Assert.AreEqual(expected, pack.GetType().GetProperty("BodyTextureScale").GetValue(pack));
            using (var preview = new PlaybackFixture())
            {
                FishingLayerArt.Place(preview.SkinId, 640, 512, out var width, out var height, out var x, out var y);
                using (var highResolution = new PlaybackFixture(manifest))
                {
                    FishingLayerArt.Place(highResolution.SkinId, 640, 512,
                        out var actualWidth, out var actualHeight, out var actualX, out var actualY);
                    Assert.AreEqual(width, actualWidth);
                    Assert.AreEqual(height, actualHeight);
                    Assert.AreEqual(x, actualX);
                    Assert.AreEqual(y, actualY);
                    highResolution.Renderer.Apply(PetVisualState.Fishing);
                    var images = (Image[])typeof(SpriteFramePetRenderer).GetField(
                        "_layerImages", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(highResolution.Renderer);
                    foreach (var image in images)
                        Assert.AreEqual(new Vector2(width, height), image.rectTransform.sizeDelta);
                }
            }
        }

        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("3")]
        [TestCase("8")]
        [TestCase("2.0")]
        [TestCase("2e0")]
        [TestCase("2.5")]
        [TestCase("2147483648")]
        [TestCase("\"2\"")]
        [TestCase("null")]
        [TestCase("true")]
        [TestCase("[]")]
        public void UnsupportedBodyTextureScalesFailClosed(string value)
        {
            Assert.IsFalse(FishingLayerArt.ValidateManifest(ManifestWithTextureScale(value), out _));
        }

        [TestCase(1, 640, 512, 17, 13, 17, 13)]
        [TestCase(2, 1280, 1024, 17, 13, 34, 26)]
        [TestCase(2, 1280, 1024, 700, 600, 1280, 1024)]
        [TestCase(4, 2560, 2048, 17, 13, 34, 26)]
        [TestCase(2, 640, 512, 17, 13, 0, 0)]
        [TestCase(2, 1280, 512, 17, 13, 0, 0)]
        [TestCase(1, 1280, 1024, 17, 13, 0, 0)]
        public void LayerLoaderValidatesNativeSizeAndCapsHighResolutionSampling(
            int scale, int sourceWidth, int sourceHeight, int drawWidth, int drawHeight, int expectedWidth, int expectedHeight)
        {
            var path = Path.Combine(Path.GetTempPath(), "FishingTexture-" + Guid.NewGuid().ToString("N") + ".png");
            Sprite sprite = null;
            try
            {
                File.WriteAllBytes(path, TextureBytes(sourceWidth, sourceHeight));
                sprite = (Sprite)typeof(FishingLayerArt).GetMethod("LoadSprite", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { path, drawWidth, drawHeight, scale });
                if (expectedWidth == 0)
                    Assert.IsNull(sprite);
                else
                {
                    Assert.IsNotNull(sprite);
                    Assert.AreEqual(expectedWidth, sprite.texture.width);
                    Assert.AreEqual(expectedHeight, sprite.texture.height);
                }
            }
            finally
            {
                if (sprite != null)
                {
                    UnityEngine.Object.DestroyImmediate(sprite.texture);
                    UnityEngine.Object.DestroyImmediate(sprite);
                }
                File.Delete(path);
            }
        }

        [Test]
        public void GroundbaitReturnsToReadyAndLeavingEndsUnarmedWithoutReplay()
        {
            using (var preview = new PlaybackFixture())
            {
                preview.Renderer.Apply(PetVisualState.Groundbait);
                Assert.AreEqual("groundbait", preview.Renderer.ActiveClip);
                preview.Renderer.Tick(0.801f);
                Assert.AreEqual("ready", preview.Renderer.ActiveClip);
                preview.Renderer.Apply(PetVisualState.Groundbait);
                Assert.AreEqual("ready", preview.Renderer.ActiveClip);
                preview.Renderer.Apply(PetVisualState.Idle);
                Assert.AreEqual("leave", preview.Renderer.ActiveClip);
                preview.Renderer.Tick(0.401f);
                Assert.AreEqual("idle", preview.Renderer.ActiveClip);
                preview.Renderer.Apply(PetVisualState.Idle);
                Assert.AreEqual("idle", preview.Renderer.ActiveClip);
                preview.Renderer.Apply(PetVisualState.Sit);
                Assert.AreEqual("sit", preview.Renderer.ActiveClip);
            }
        }

        [Test]
        public void NonLoopClipsCanOmitLoopStart()
        {
            var manifest = Manifest().Replace("\"hold\",\"loopStart\":0", "\"hold\"")
                .Replace("\"once\",\"loopStart\":0", "\"once\"");
            Assert.IsTrue(FishingLayerArt.ValidateManifest(manifest, out var error), error);
            Assert.IsFalse(FishingLayerArt.ValidateManifest(
                manifest.Replace("\"intro-loop\",\"loopStart\":0", "\"intro-loop\""), out _));
        }

        [TestCase("\"version\":2", "\"version\":1")]
        [TestCase("\"version\":2", "\"version\":2.0")]
        [TestCase("\"version\":2", "\"version\":+2")]
        [TestCase("\"version\":2", "\"version\":02")]
        [TestCase("\"sourceVersion\":19", "\"sourceVersion\":17")]
        [TestCase("[640,512]", "[512,640]")]
        [TestCase("\"body\",\"head\"", "\"cat\",\"head\"")]
        [TestCase("\"body\",\"head\"", "\"body\",\"body\"")]
        [TestCase("\"bodyLayers\":[\"body\",\"head\",\"far_hand\",\"hands\"]", "\"bodyLayers\":[\"body\",\"hands\"]")]
        [TestCase("\"durationsMs\":[100]", "\"durationsMs\":[0]")]
        [TestCase("\"durationsMs\":[100]", "\"durationsMs\":[-100]")]
        [TestCase("\"durationsMs\":[100]", "\"durationsMs\":[100.5]")]
        [TestCase("\"durationsMs\":[100]", "\"durationsMs\":[\"100\"]")]
        [TestCase("\"durationsMs\":[100]", "\"durationsMs\":[100,100]")]
        [TestCase("\"durationsMs\":[100]", "\"durationsMs\":[2147483648]")]
        [TestCase("\"nextClip\":\"catch\"", "\"nextClip\":\"missing\"")]
        [TestCase("\"nextClip\":\"catch\"", "\"nextClip\":\"reel\"")]
        [TestCase("\"loopStart\":4", "\"loopStart\":10")]
        [TestCase("\"loopStart\":4", "\"loopStart\":-1")]
        [TestCase("\"loopStart\":4", "\"loopStart\":4.5")]
        [TestCase("\"playback\":\"hold\"", "\"playback\":\"loop\"")]
        public void InvalidManifestsFailClosed(string original, string replacement)
        {
            var json = Manifest().Replace(original, replacement);
            Assert.IsFalse(FishingLayerArt.ValidateManifest(json, out var error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void ManifestRejectsTrailingContentAndDuplicateFields()
        {
            Assert.IsFalse(FishingLayerArt.ValidateManifest(Manifest() + "{}", out _));
            Assert.IsFalse(FishingLayerArt.ValidateManifest(
                Manifest().Replace("\"version\":2", "\"version\":2,\"version\":2"), out _));
        }

        [Test]
        public void IndividualDurationsAndLayerOrdersAreRetained()
        {
            var manifest = Manifest().Replace("[100,100,100,100]", "[100,500,200,300]");
            var clip = Session(manifest, "bait");
            CollectionAssert.AreEqual(new[] { 100, 500, 200, 300 },
                Enumerable.Range(0, clip.FrameCount).Select(clip.DurationMs).ToArray());
            CollectionAssert.AreEqual(FishingLayerArt.LayerNames.Reverse().ToArray(), clip.Order(1));
            Assert.IsNull(clip.Frame(1, "far_hand"));
            Assert.IsNull(clip.Frame(1, 2));
        }

        [Test]
        public void ReelTotalsSixHundredMillisecondsAndHasCatchChain()
        {
            var clip = Session(Manifest(), "reel");
            Assert.AreEqual(600, Enumerable.Range(0, clip.FrameCount).Sum(clip.DurationMs));
            Assert.AreEqual("catch", clip.NextClip);
            Assert.AreEqual("reset_catch", Session(Manifest(), "catch").NextClip);
            Assert.AreEqual("ready", Session(Manifest(), "reset_catch").NextClip);
            Assert.AreEqual("reset_empty", Session(Manifest(), "reel_empty").NextClip);
        }

        [TestCase(PetVisualState.Idle, 0, "idle")]
        [TestCase(PetVisualState.Fishing, 1, "fishing")]
        [TestCase(PetVisualState.Hooked, 2, "hooked")]
        [TestCase(PetVisualState.Catching, 3, "catch")]
        [TestCase(PetVisualState.Dragging, 4, "dragging")]
        [TestCase(PetVisualState.Offline, 5, "offline")]
        [TestCase(PetVisualState.Sit, 6, "sit")]
        [TestCase(PetVisualState.Cast, 7, "cast")]
        [TestCase(PetVisualState.Reel, 8, "reel")]
        [TestCase(PetVisualState.Bait, 9, "bait")]
        [TestCase(PetVisualState.EmptyReel, 10, "reel_empty")]
        [TestCase(PetVisualState.Groundbait, 11, "groundbait")]
        public void ExistingEnumAndWireValuesStayStable(PetVisualState state, int numeric, string wire)
        {
            Assert.AreEqual(numeric, (int)state);
            Assert.AreEqual(wire, PetStateController.ToWire(state));
        }

        [TestCase("resolving", false, "escape", PetVisualState.EmptyReel)]
        [TestCase("resolving", true, "escape", PetVisualState.EmptyReel)]
        [TestCase("resolving", false, "escaped", PetVisualState.EmptyReel)]
        [TestCase("resolving", true, "escaped", PetVisualState.EmptyReel)]
        [TestCase("resolving", false, null, PetVisualState.Reel)]
        [TestCase("resolving", false, "", PetVisualState.Reel)]
        [TestCase("resolving", false, "catch", PetVisualState.Reel)]
        [TestCase("stopping", false, null, PetVisualState.EmptyReel)]
        [TestCase("stopping", true, "escape", PetVisualState.Reel)]
        [TestCase("seated", false, "escape", PetVisualState.Sit)]
        [TestCase("seated", true, null, PetVisualState.Catching)]
        [TestCase("hooked", false, "escape", PetVisualState.Hooked)]
        [TestCase("groundbaiting", false, null, PetVisualState.Groundbait)]
        [TestCase("baiting", false, null, PetVisualState.Bait)]
        [TestCase("casting", false, null, PetVisualState.Cast)]
        [TestCase("waiting", false, null, PetVisualState.Fishing)]
        [TestCase("ready", false, null, PetVisualState.Idle)]
        [TestCase("bite", false, null, PetVisualState.Idle)]
        [TestCase("effort", false, null, PetVisualState.Idle)]
        [TestCase("reel_empty", false, null, PetVisualState.Idle)]
        public void OnlyExistingBackendPhasesMapToPresentation(
            string phase, bool pending, string outcome, PetVisualState expected)
        {
            Assert.AreEqual(expected, PetStateController.FromFishingPhase(phase, pending, outcome));
        }

        [TestCase(null, "escape", "escape", PetVisualState.EmptyReel)]
        [TestCase("", "escape", "escape", PetVisualState.EmptyReel)]
        [TestCase(null, "catch", "catch", PetVisualState.Reel)]
        [TestCase("", "catch", "catch", PetVisualState.Reel)]
        [TestCase("catch", "escape", "catch", PetVisualState.Reel)]
        [TestCase("escape", "catch", "escape", PetVisualState.EmptyReel)]
        [TestCase("escaped", "catch", "escaped", PetVisualState.EmptyReel)]
        [TestCase(null, null, null, PetVisualState.Reel)]
        [TestCase("", null, null, PetVisualState.Reel)]
        [TestCase("", "", "", PetVisualState.Reel)]
        public void EmptyPhaseOutcomeFallsBackWithoutOverridingAuthoritativeOutcome(
            string phaseOutcome, string lastOutcome, string expectedOutcome, PetVisualState expectedState)
        {
            var method = typeof(PetStateController).GetMethod(
                "ResolveFishingOutcome", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var outcome = (string)method.Invoke(null, new object[] { phaseOutcome, lastOutcome });
            Assert.AreEqual(expectedOutcome, outcome);
            Assert.AreEqual(expectedState, PetStateController.FromFishingPhase("resolving", false, outcome));
        }

        [TestCase("escape", PetVisualState.EmptyReel)]
        [TestCase("catch", PetVisualState.Reel)]
        public void MergePreservesJsonParsedCanonicalPhaseOutcome(string outcome, PetVisualState expected)
        {
            var contextField = typeof(PondUserDto).GetField("phaseContext", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(contextField);
            var outcomeField = contextField.FieldType.GetField("outcome", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(outcomeField);
            var previousOutcome = outcome == "escape" ? "catch" : "escape";
            var previous = JsonUtility.FromJson<PondUserDto>(
                "{\"id\":\"self\",\"fishingPhase\":\"hooked\",\"phaseContext\":{\"outcome\":\"" + previousOutcome + "\"}}");
            var incoming = JsonUtility.FromJson<PondUserDto>(
                "{\"id\":\"self\",\"fishingPhase\":\"resolving\",\"phaseContext\":{\"outcome\":\"" + outcome + "\"}}");
            var incomingContext = contextField.GetValue(incoming);
            Assert.IsNotNull(incomingContext);
            Assert.AreEqual(outcome, outcomeField.GetValue(incomingContext));

            var mergeType = typeof(PondUserDto).Assembly.GetType("FishSocial.Desktop.Auth.PondUserMerge");
            Assert.IsNotNull(mergeType);
            var mergeMethod = mergeType.GetMethod("Merge", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(mergeMethod);
            var merged = (PondUserDto)mergeMethod.Invoke(null, new object[] { previous, incoming });
            Assert.IsNotNull(merged);
            Assert.AreNotSame(previous, merged);
            Assert.AreNotSame(incoming, merged);
            Assert.AreEqual("resolving", merged.fishingPhase);
            var mergedContext = contextField.GetValue(merged);
            Assert.IsNotNull(mergedContext);
            Assert.AreEqual(outcome, outcomeField.GetValue(mergedContext));
            Assert.AreEqual(previousOutcome, outcomeField.GetValue(contextField.GetValue(previous)));
            Assert.AreEqual(expected, PetStateController.FromFishingPhase(
                merged.fishingPhase, false, (string)outcomeField.GetValue(mergedContext)));

            var roundTrip = JsonUtility.FromJson<PondUserDto>(JsonUtility.ToJson(merged));
            Assert.AreEqual(outcome, outcomeField.GetValue(contextField.GetValue(roundTrip)));
        }

        [TestCase("blue")]
        [TestCase("dafeiyu")]
        public void SameStatePetChangeReappliesRenderersWithoutRepeatedSnapshotApplications(string nextSkin)
        {
            var previousProfile = DesktopProfileCache.Latest;
            var root = new GameObject("PetStateSkinRefreshTest");
            try
            {
                DesktopProfileCache.Latest = new PlayerProfileDto { avatarUrl = "/image/profile/cat_avatar_orange.png" };
                var controller = root.AddComponent<PetStateController>();
                var firstRenderer = new RecordingPetRenderer();
                var secondRenderer = new RecordingPetRenderer();
                controller.AddRenderer(firstRenderer);
                controller.AddRenderer(secondRenderer);
                controller.Refresh(null, null);
                var previousPetId = PetArtLoader.CurrentOwnPetId();
                var applications = firstRenderer.AppliedStates.Count;
                var stateChanges = 0;
                controller.StateChanged += state => stateChanges++;

                controller.Refresh(null, null);
                Assert.AreEqual(applications, firstRenderer.AppliedStates.Count);
                Assert.AreEqual(applications, secondRenderer.AppliedStates.Count);
                DesktopProfileCache.Latest = new PlayerProfileDto { avatarUrl = "/image/profile/cat_avatar_" + nextSkin + ".png" };
                var nextPetId = PetArtLoader.CurrentOwnPetId();
                Assert.AreNotEqual(previousPetId, nextPetId);
                controller.Refresh(null, null);
                Assert.AreEqual(applications + 1, firstRenderer.AppliedStates.Count);
                Assert.AreEqual(applications + 1, secondRenderer.AppliedStates.Count);
                Assert.AreEqual(nextPetId, firstRenderer.AppliedPetIds.Last());
                Assert.AreEqual(nextPetId, secondRenderer.AppliedPetIds.Last());
                Assert.AreEqual(PetVisualState.Offline, controller.Current);
                Assert.AreEqual(PetVisualState.Offline, firstRenderer.AppliedStates.Last());
                Assert.AreEqual(PetVisualState.Offline, secondRenderer.AppliedStates.Last());
                Assert.AreEqual(0, stateChanges);

                controller.Refresh(null, null);
                Assert.AreEqual(applications + 1, firstRenderer.AppliedStates.Count);
                Assert.AreEqual(applications + 1, secondRenderer.AppliedStates.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                DesktopProfileCache.Latest = previousProfile;
            }
        }

        [Test]
        public void ExistingPhaseOverloadsRemainAvailable()
        {
            Assert.AreEqual(PetVisualState.Reel, PetStateController.FromFishingPhase("resolving"));
            Assert.AreEqual(PetVisualState.Catching, PetStateController.FromFishingPhase("seated", true));
            Assert.AreEqual(PetVisualState.EmptyReel, PetStateController.FromFishingPhase("stopping", false));
        }

        [Test]
        public void SourceSkinsIncludeThePaintedCalicoPackWithoutBreedAliases()
        {
            CollectionAssert.AreEqual(new[] { "orange", "calico", "silver", "brown", "cream", "blue", "black", "white", "cow", "dafeiyu" }, FishingLayerArt.SkinIds);
            Assert.IsNull(FishingLayerArt.Open("british", "idle", 640, 512));
            Assert.IsNull(FishingLayerArt.Open("ragdoll", "idle", 640, 512));
            var calico = FishingLayerArt.Open("calico", "idle", 640, 512);
            Assert.IsNotNull(calico);
            FishingLayerArt.Release(calico);
            Assert.IsNull(FishingLayerArt.Open("../orange", "idle", 640, 512));
        }

        [TestCase("silver")]
        [TestCase("brown")]
        [TestCase("cream")]
        [TestCase("blue")]
        [TestCase("black")]
        [TestCase("white")]
        [TestCase("cow")]
        [TestCase("dafeiyu")]
        public void PaintedCharacterAvatarResolvesToAnInstalledAnimation(string skin)
        {
            Assert.AreEqual(skin, DesktopDefaultAvatars.ResolvePetId("/image/profile/cat_avatar_" + skin + ".png", "player"));
            Assert.AreEqual("/image/fishing_photos/cat_fishing_" + skin + ".png",
                DesktopDefaultAvatars.ResolveFishingPhotoPath("/image/profile/cat_avatar_" + skin + ".png", "player"));
            var session = FishingLayerArt.Open(skin, "ready", 640, 512);
            Assert.IsNotNull(session, skin);
            FishingLayerArt.Release(session);
        }

        [Test]
        public void DafeiyuHasAnIndependentPortraitAndAllSixteenPlayableClips()
        {
            const string avatar = "/image/profile/cat_avatar_dafeiyu.png";
            Assert.IsTrue(DesktopDefaultAvatars.IsDefaultPath(avatar));
            Assert.AreEqual("大肥鱼", DesktopDefaultAvatars.LabelFor(avatar));
            Assert.AreEqual("dafeiyu", DesktopDefaultAvatars.NormalizeSkinId("dafeiyu"));
            Assert.IsNotNull(DesktopDefaultAvatars.Portrait(avatar));
            Assert.AreNotSame(DesktopDefaultAvatars.Portrait("/image/profile/cat_avatar_orange.png"),
                DesktopDefaultAvatars.Portrait(avatar));
            for (var index = 0; index < FishingLayerArt.ClipNames.Length; index++)
            {
                var clip = FishingLayerArt.ClipNames[index];
                var session = FishingLayerArt.Open("dafeiyu", clip, 320, 256);
                try
                {
                    Assert.IsNotNull(session, clip);
                    Assert.AreEqual(Counts[index], session.FrameCount, clip);
                }
                finally { FishingLayerArt.Release(session); }
            }
        }

        [Test]
        public void CustomAvatarFallbackPreservesSupportedSkinsAndRetiresOldArt()
        {
            var legacy = new[] { "calico", "orange", "orange", "orange", "orange", "white" };
            foreach (var playerId in new[] { "player", "angler-42", "小橘", "" })
            {
                var hash = 0;
                foreach (var character in playerId) hash = unchecked(hash * 31 + character);
                var expected = legacy[(int)(Math.Abs((long)hash) % legacy.Length)];
                Assert.AreEqual(expected, DesktopDefaultAvatars.ResolvePetId(null, playerId));
            }
        }

        [TestCase("gray")]
        [TestCase("siamese")]
        [TestCase("tuxedo")]
        public void RetiredCatsResolveToCurrentOrangeArtwork(string skin)
        {
            var avatar = "/image/profile/cat_avatar_" + skin + ".png";
            Assert.IsTrue(DesktopDefaultAvatars.IsDefaultPath(avatar));
            Assert.AreEqual("orange", DesktopDefaultAvatars.ResolvePetId(avatar, "player"));
            Assert.AreEqual("/image/fishing_photos/cat_fishing_orange.png",
                DesktopDefaultAvatars.ResolveFishingPhotoPath(avatar, "player"));
            Assert.NotNull(PetArtLoader.FallbackPortrait(skin));
            CollectionAssert.AreEquivalent(FishingLayerArt.SkinIds,
                DesktopDefaultAvatars.All.Select(entry => entry.Id).ToArray());
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void ArtworkRequiresAllNineLayersAndIgnoresLegacyCatFiles(int textureScale)
        {
            var root = Path.Combine(Path.GetTempPath(), "FishingArtTest-" + Guid.NewGuid().ToString("N"));
            var packs = (IDictionary)typeof(FishingLayerArt).GetField("Packs", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var previous = packs["blue"];
            var body = Path.Combine(root, "blue", "body", "idle", "0");
            var shared = Path.Combine(root, "shared", "idle", "0");
            try
            {
                Directory.CreateDirectory(body);
                Directory.CreateDirectory(shared);
                packs["blue"] = Pack(ManifestWithTextureScale(textureScale.ToString()), root);
                var bytes = TextureBytes(640, 512);
                var bodyBytes = textureScale == 1 ? bytes : TextureBytes(640 * textureScale, 512 * textureScale);
                foreach (var layer in FishingLayerArt.LayerNames)
                {
                    var folder = layer == "body" || layer == "head" || layer == "far_hand" || layer == "hands" ? body : shared;
                    File.WriteAllBytes(Path.Combine(folder, layer + ".png"), folder == body ? bodyBytes : bytes);
                }
                var complete = typeof(FishingLayerArt).GetMethod("HasCompleteFiles", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(complete);
                Assert.IsFalse((bool)complete.Invoke(null, new[] { packs["blue"], "blue" }));
                File.WriteAllBytes(Path.Combine(body, "cat.png"), bytes);
                File.WriteAllBytes(Path.Combine(shared, "cat.png"), bytes);
                for (var clipIndex = 0; clipIndex < FishingLayerArt.ClipNames.Length; clipIndex++)
                {
                    var clip = FishingLayerArt.ClipNames[clipIndex];
                    for (var frame = 0; frame < Counts[clipIndex]; frame++)
                    {
                        var bodyFolder = Path.Combine(root, "blue", "body", clip, frame.ToString());
                        var sharedFolder = Path.Combine(root, "shared", clip, frame.ToString());
                        Directory.CreateDirectory(bodyFolder);
                        Directory.CreateDirectory(sharedFolder);
                        foreach (var layer in FishingLayerArt.LayerNames)
                        {
                            var folder = layer == "body" || layer == "head" || layer == "far_hand" || layer == "hands"
                                ? bodyFolder : sharedFolder;
                            File.WriteAllBytes(Path.Combine(folder, layer + ".png"), folder == bodyFolder ? bodyBytes : bytes);
                        }
                    }
                }
                Assert.IsTrue((bool)complete.Invoke(null, new[] { packs["blue"], "blue" }));
                var session = FishingLayerArt.Open("blue", "idle", 17, 13);
                Assert.IsNotNull(session);
                Assert.AreEqual(9, session.Frames.GetLength(1));
                Assert.IsFalse(session.Order(0).Contains("cat"));
                AssertLayerTextureSizes(session, textureScale);
                FishingLayerArt.Release(session);
                for (var layer = 0; layer < FishingLayerArt.LayerNames.Length; layer++)
                    Assert.AreEqual(layer == FishingLayerArt.LayerNames.Length - 1,
                        FishingLayerArt.WarmupSlice("blue", "idle", 17, 13));
                session = FishingLayerArt.TryGetCached("blue", "idle", 17, 13);
                Assert.IsNotNull(session);
                AssertLayerTextureSizes(session, textureScale);
                while (session.Users > 0)
                    FishingLayerArt.Release(session);
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(body, "cat.png")));
                CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(shared, "cat.png")));
                File.Delete(Path.Combine(body, "far_hand.png"));
                Assert.IsFalse((bool)complete.Invoke(null, new[] { packs["blue"], "blue" }));
                Assert.IsNull(FishingLayerArt.Open("blue", "idle", 17, 13));
                for (var layer = 0; layer < 3; layer++)
                    FishingLayerArt.WarmupSlice("blue", "idle", 17, 13);
                Assert.IsNull(FishingLayerArt.TryGetCached("blue", "idle", 17, 13));
            }
            finally
            {
                if (previous == null)
                    packs.Remove("blue");
                else
                    packs["blue"] = previous;
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        [Test]
        public void IntroLoopsSupportZeroAndNonzeroLoopStarts()
        {
            using (var preview = new PlaybackFixture())
            {
                Assert.IsTrue(preview.Renderer.PlayClip(preview.SkinId, "effort"));
                for (var index = 0; index < 6; index++)
                    preview.Renderer.StepFrame();
                Assert.AreEqual(0, preview.Renderer.FrameIndex);
                Assert.IsTrue(preview.Renderer.PlayClip(preview.SkinId, "hooked"));
                for (var index = 0; index < 10; index++)
                    preview.Renderer.StepFrame();
                Assert.AreEqual(4, preview.Renderer.FrameIndex);
                Assert.AreEqual("hooked", preview.Renderer.ActiveClip);
            }
        }

        [Test]
        public void TickHonorsDurationsAndConsumesMultipleFrames()
        {
            using (var preview = new PlaybackFixture())
            {
                Assert.IsTrue(preview.Renderer.PlayClip(preview.SkinId, "bait"));
                preview.Renderer.Tick(0.099f);
                Assert.AreEqual(0, preview.Renderer.FrameIndex);
                preview.Renderer.Tick(0.202f);
                Assert.AreEqual(3, preview.Renderer.FrameIndex);
                Assert.IsFalse(preview.Renderer.ClipFinished);
                preview.Renderer.Tick(0.1f);
                Assert.IsTrue(preview.Renderer.ClipFinished);
            }
        }

        [Test]
        public void VariableDurationsControlPlaybackAndFinalFrameDwell()
        {
            var manifest = Manifest().Replace("[100,100,100,100]", "[100,500,200,300]");
            using (var preview = new PlaybackFixture(manifest))
            {
                preview.Renderer.PlayClip(preview.SkinId, "bait");
                preview.Renderer.Tick(0.101f);
                Assert.AreEqual(1, preview.Renderer.FrameIndex);
                preview.Renderer.Tick(0.49f);
                Assert.AreEqual(1, preview.Renderer.FrameIndex);
                preview.Renderer.Tick(0.01f);
                Assert.AreEqual(2, preview.Renderer.FrameIndex);
                preview.Renderer.Tick(0.2f);
                Assert.AreEqual(3, preview.Renderer.FrameIndex);
                Assert.IsFalse(preview.Renderer.ClipFinished);
                preview.Renderer.Tick(0.3f);
                Assert.IsTrue(preview.Renderer.ClipFinished);
            }
        }

        [Test]
        public void ReelTransitionsOnlyAfterItsFinalHundredMilliseconds()
        {
            using (var preview = new PlaybackFixture())
            {
                preview.Renderer.Apply(PetVisualState.Reel);
                preview.Renderer.Tick(0.599f);
                Assert.AreEqual("reel", preview.Renderer.ActiveClip);
                Assert.AreEqual(5, preview.Renderer.FrameIndex);
                preview.Renderer.Apply(PetVisualState.Reel);
                Assert.AreEqual(5, preview.Renderer.FrameIndex);
                preview.Renderer.Tick(0.002f);
                Assert.AreEqual("catch", preview.Renderer.ActiveClip);
                Assert.AreEqual(0, preview.Renderer.FrameIndex);
            }
        }

        [TestCase(PetVisualState.Reel)]
        [TestCase(PetVisualState.EmptyReel)]
        public void ReturningToSeatAfterOutcomeDoesNotReplaySit(PetVisualState state)
        {
            using (var preview = new PlaybackFixture())
            {
                preview.Renderer.Apply(state);
                preview.Renderer.Apply(PetVisualState.Sit);
                preview.Renderer.Tick(3f);
                Assert.AreEqual("ready", preview.Renderer.ActiveClip);
                preview.Renderer.Apply(PetVisualState.Sit);
                Assert.AreEqual("ready", preview.Renderer.ActiveClip);
            }
        }

        [Test]
        public void HookedKeepsLoopingUntilAuthoritativeStateChanges()
        {
            using (var preview = new PlaybackFixture())
            {
                preview.Renderer.Apply(PetVisualState.Hooked);
                preview.Renderer.Tick(3f);
                var frame = preview.Renderer.FrameIndex;
                preview.Renderer.Apply(PetVisualState.Hooked);
                Assert.AreEqual(frame, preview.Renderer.FrameIndex);
                Assert.AreEqual("hooked", preview.Renderer.ActiveClip);
                preview.Renderer.Apply(PetVisualState.Fishing);
                Assert.AreEqual("fishing", preview.Renderer.ActiveClip);
            }
        }

        [Test]
        public void SitRunsOnlyOnEnteringSeatAndRepeatedStatesDoNotRestart()
        {
            using (var preview = new PlaybackFixture())
            {
                preview.Renderer.Apply(PetVisualState.Sit);
                Assert.AreEqual("sit", preview.Renderer.ActiveClip);
                preview.Renderer.Tick(0.201f);
                preview.Renderer.Apply(PetVisualState.Sit);
                Assert.AreEqual(2, preview.Renderer.FrameIndex);
                preview.Renderer.Tick(0.201f);
                Assert.AreEqual("ready", preview.Renderer.ActiveClip);
                preview.Renderer.Apply(PetVisualState.Fishing);
                preview.Renderer.Apply(PetVisualState.Sit);
                Assert.AreEqual("ready", preview.Renderer.ActiveClip);
            }
        }

        [TestCase(PetVisualState.Reel, "reel", "catch", "reset_catch", 17, PetVisualState.Cast, "cast")]
        [TestCase(PetVisualState.EmptyReel, "reel_empty", "reset_empty", "reset_empty", 10, PetVisualState.Cast, "cast")]
        [TestCase(PetVisualState.Reel, "reel", "catch", "reset_catch", 17, PetVisualState.Fishing, "fishing")]
        [TestCase(PetVisualState.EmptyReel, "reel_empty", "reset_empty", "reset_empty", 10, PetVisualState.Fishing, "fishing")]
        public void OutcomeChainsFinishBeforeLatestDeferredRequest(
            PetVisualState state, string first, string second, string reset, int steps,
            PetVisualState latestState, string expectedClip)
        {
            using (var preview = new PlaybackFixture())
            {
                preview.Renderer.Apply(state);
                Assert.AreEqual(first, preview.Renderer.ActiveClip);
                preview.Renderer.Apply(PetVisualState.Sit);
                preview.Renderer.Apply(PetVisualState.Idle);
                preview.Renderer.Apply(PetVisualState.Bait);
                preview.Renderer.Apply(PetVisualState.Cast);
                preview.Renderer.Apply(latestState);
                Assert.AreEqual(first, preview.Renderer.ActiveClip);
                Assert.AreEqual(latestState, preview.Renderer.RequestedState);
                for (var index = 0; index < 6; index++)
                    preview.Renderer.StepFrame();
                Assert.AreEqual(second, preview.Renderer.ActiveClip);
                for (var index = 6; index < steps - 1; index++)
                    preview.Renderer.StepFrame();
                Assert.AreEqual(reset, preview.Renderer.ActiveClip);
                preview.Renderer.StepFrame();
                Assert.AreEqual(expectedClip, preview.Renderer.ActiveClip);
                Assert.AreEqual(latestState, preview.Renderer.RequestedState);
            }
        }

        [TestCase(PetVisualState.Reel, "reel")]
        [TestCase(PetVisualState.EmptyReel, "reel_empty")]
        public void ReturningToReelClearsStaleDeferredTargetWithoutRestartingOutcome(
            PetVisualState state, string clip)
        {
            using (var preview = new PlaybackFixture())
            {
                preview.Renderer.Apply(state);
                preview.Renderer.StepFrame();
                preview.Renderer.Apply(PetVisualState.Cast);
                preview.Renderer.Apply(PetVisualState.Fishing);
                Assert.AreEqual(clip, preview.Renderer.ActiveClip);
                preview.Renderer.Apply(state);
                Assert.AreEqual(clip, preview.Renderer.ActiveClip);
                Assert.AreEqual(1, preview.Renderer.FrameIndex);
                Assert.AreEqual(state, preview.Renderer.RequestedState);
                preview.Renderer.Tick(3f);
                Assert.AreEqual("ready", preview.Renderer.ActiveClip);
                Assert.AreEqual(state, preview.Renderer.RequestedState);
                Assert.IsFalse(preview.Renderer.IsHoldingCatch);
            }
        }

        [Test]
        public void CatchConfirmationDoesNotTruncateReelOrReplayCatch()
        {
            using (var preview = new PlaybackFixture())
            {
                preview.Renderer.Apply(PetVisualState.Reel);
                preview.Renderer.StepFrame();
                preview.Renderer.Apply(PetVisualState.Catching);
                Assert.AreEqual("reel", preview.Renderer.ActiveClip);
                Assert.AreEqual(1, preview.Renderer.FrameIndex);
                preview.Renderer.Tick(2f);
                Assert.AreEqual("catch", preview.Renderer.ActiveClip);
                Assert.AreEqual(6, preview.Renderer.FrameIndex);
                Assert.IsTrue(preview.Renderer.IsHoldingCatch);
                preview.Renderer.Apply(PetVisualState.Catching);
                preview.Renderer.Tick(10f);
                Assert.AreEqual("catch", preview.Renderer.ActiveClip);
                Assert.AreEqual(6, preview.Renderer.FrameIndex);
                preview.Renderer.Apply(PetVisualState.Sit);
                Assert.AreEqual("reset_catch", preview.Renderer.ActiveClip);
                preview.Renderer.Tick(0.401f);
                Assert.AreEqual("ready", preview.Renderer.ActiveClip);
            }
        }

        [Test]
        public void LateCatchConfirmationDoesNotReplayCompletedOutcome()
        {
            using (var preview = new PlaybackFixture())
            {
                preview.Renderer.Apply(PetVisualState.Reel);
                preview.Renderer.Tick(2f);
                preview.Renderer.Apply(PetVisualState.Sit);
                preview.Renderer.Apply(PetVisualState.Catching);
                Assert.AreEqual("catch", preview.Renderer.ActiveClip);
                Assert.AreEqual(6, preview.Renderer.FrameIndex);
                Assert.IsTrue(preview.Renderer.IsHoldingCatch);
                preview.Renderer.Apply(PetVisualState.Sit);
                preview.Renderer.Tick(0.401f);
                Assert.AreEqual("ready", preview.Renderer.ActiveClip);
                preview.Renderer.Apply(PetVisualState.Hooked);
                preview.Renderer.Apply(PetVisualState.Reel);
                Assert.AreEqual("reel", preview.Renderer.ActiveClip);
            }
        }

        [Test]
        public void ClaimedCatchResetsBeforeOnlyTheLatestDeferredState()
        {
            using (var preview = new PlaybackFixture())
            {
                preview.Renderer.Apply(PetVisualState.Catching);
                preview.Renderer.Tick(1f);
                Assert.IsTrue(preview.Renderer.IsHoldingCatch);
                preview.Renderer.Apply(PetVisualState.Sit);
                Assert.AreEqual("reset_catch", preview.Renderer.ActiveClip);
                preview.Renderer.Apply(PetVisualState.Bait);
                preview.Renderer.Apply(PetVisualState.Cast);
                preview.Renderer.Apply(PetVisualState.Sit);
                preview.Renderer.Tick(0.401f);
                Assert.AreEqual("ready", preview.Renderer.ActiveClip);
                Assert.AreEqual(PetVisualState.Sit, preview.Renderer.RequestedState);
            }
        }

        [Test]
        public void ManualPreviewIgnoresActualPendingCatchState()
        {
            using (var preview = new PlaybackFixture())
            {
                preview.Renderer.Apply(PetVisualState.Catching);
                Assert.IsTrue(preview.Renderer.PlayClip(preview.SkinId, "reel"));
                preview.Renderer.Tick(2f);
                Assert.AreEqual("ready", preview.Renderer.ActiveClip);
                Assert.IsFalse(preview.Renderer.IsHoldingCatch);
                Assert.AreEqual(PetVisualState.Catching, preview.Renderer.RequestedState);
            }
        }

        [TestCase(PetVisualState.Hooked, "hooked")]
        [TestCase(PetVisualState.Dragging, "idle")]
        [TestCase(PetVisualState.Offline, "idle")]
        public void AuthoritativeInterruptionsCancelDeferredOutcome(PetVisualState state, string clip)
        {
            using (var preview = new PlaybackFixture())
            {
                preview.Renderer.Apply(PetVisualState.Reel);
                preview.Renderer.Apply(PetVisualState.Cast);
                preview.Renderer.Apply(state);
                Assert.AreEqual(clip, preview.Renderer.ActiveClip);
                preview.Renderer.Tick(3f);
                Assert.AreEqual(clip, preview.Renderer.ActiveClip);
            }
        }

        [Test]
        public void ResizingPreservesOutcomeProgressAndLatestDeferredTarget()
        {
            using (var preview = new PlaybackFixture())
            {
                preview.Renderer.Apply(PetVisualState.Reel);
                preview.Renderer.Tick(0.251f);
                preview.Renderer.Apply(PetVisualState.Cast);
                preview.Renderer.Apply(PetVisualState.Fishing);
                var previous = preview.Renderer.ActiveSession;
                var playback = PlaybackState(preview.Renderer);
                var usersBeforeResize = previous.Users;
                preview.Resize(800, 512);
                Assert.AreSame(previous, preview.Renderer.ActiveSession);
                Assert.AreEqual(usersBeforeResize, previous.Users);
                CollectionAssert.AreEqual(playback, PlaybackState(preview.Renderer));
                preview.Resize(960, 768);
                CollectionAssert.AreEqual(playback, PlaybackState(preview.Renderer));
                Assert.AreNotSame(previous, preview.Renderer.ActiveSession);
                Assert.AreEqual(1, previous.Users);
                Assert.AreEqual("reel", preview.Renderer.ActiveClip);
                FishingLayerArt.Place(preview.SkinId, 960, 768,
                    out var width, out var height, out var originX, out var originY);
                Assert.AreEqual(preview.SkinId + "|reel|" + width + "x" + height, preview.Renderer.ActiveSession.Key);
                var images = (Image[])typeof(SpriteFramePetRenderer).GetField(
                    "_layerImages", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview.Renderer);
                foreach (var image in images)
                {
                    Assert.AreEqual(new Vector2(width, height), image.rectTransform.sizeDelta);
                    Assert.AreEqual(new Vector2(originX, -originY), image.rectTransform.anchoredPosition);
                }

                var resized = preview.Renderer.ActiveSession;
                var users = resized.Users;
                preview.Resize(960, 768);
                Assert.AreSame(resized, preview.Renderer.ActiveSession);
                Assert.AreEqual(users, resized.Users);
                CollectionAssert.AreEqual(playback, PlaybackState(preview.Renderer));
                preview.Resize(0, 0);
                Assert.AreSame(resized, preview.Renderer.ActiveSession);
                CollectionAssert.AreEqual(playback, PlaybackState(preview.Renderer));
                preview.Resize(960, 768);
                preview.Renderer.Tick(0.048f);
                Assert.AreEqual(2, preview.Renderer.FrameIndex);
                preview.Renderer.Tick(0.002f);
                Assert.AreEqual(3, preview.Renderer.FrameIndex);
                preview.Renderer.Tick(3f);
                Assert.AreEqual("fishing", preview.Renderer.ActiveClip);
                Assert.AreEqual(PetVisualState.Fishing, preview.Renderer.RequestedState);
            }
        }

        [Test]
        public void ResizingPreservesPendingCatchHoldAndManualPreview()
        {
            using (var preview = new PlaybackFixture())
            {
                preview.Renderer.Apply(PetVisualState.Catching);
                preview.Renderer.Tick(1f);
                var playback = PlaybackState(preview.Renderer);
                preview.Resize(960, 768);
                CollectionAssert.AreEqual(playback, PlaybackState(preview.Renderer));
                Assert.IsTrue(preview.Renderer.IsHoldingCatch);
                Assert.AreEqual(6, preview.Renderer.FrameIndex);
                preview.Renderer.Apply(PetVisualState.Sit);
                preview.Renderer.Tick(0.401f);
                Assert.AreEqual("ready", preview.Renderer.ActiveClip);

                Assert.IsTrue(preview.Renderer.PlayClip(preview.SkinId, "hooked"));
                preview.Renderer.Tick(0.451f);
                playback = PlaybackState(preview.Renderer);
                preview.Resize(800, 600);
                CollectionAssert.AreEqual(playback, PlaybackState(preview.Renderer));
                Assert.AreEqual("hooked", preview.Renderer.ActiveClip);
                Assert.AreEqual(4, preview.Renderer.FrameIndex);
                Assert.IsFalse(preview.Renderer.IsPlaying);
            }
        }

        [Test]
        public void FirstLayoutAfterZeroSizeResamplesWithoutReplaying()
        {
            using (var preview = new PlaybackFixture(slotWidth: 0, slotHeight: 0))
            {
                preview.Renderer.Apply(PetVisualState.Hooked);
                preview.Renderer.Tick(0.251f);
                var previous = preview.Renderer.ActiveSession;
                var playback = PlaybackState(preview.Renderer);
                preview.Resize(640, 512);
                Assert.AreNotSame(previous, preview.Renderer.ActiveSession);
                CollectionAssert.AreEqual(playback, PlaybackState(preview.Renderer));
                Assert.AreEqual(2, preview.Renderer.FrameIndex);
            }
        }

        [Test]
        public void DimensionCallbackIsSafeBeforeBindingAndWithoutLayerSession()
        {
            var root = new GameObject("UnboundFishingLayoutTest", typeof(RectTransform), typeof(Image));
            try
            {
                var renderer = root.AddComponent<SpriteFramePetRenderer>();
                var callback = typeof(SpriteFramePetRenderer).GetMethod(
                    "OnRectTransformDimensionsChange", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(callback);
                Assert.DoesNotThrow(() => callback.Invoke(renderer, null));
                typeof(SpriteFramePetRenderer).GetField("_image", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(renderer, root.GetComponent<Image>());
                Assert.DoesNotThrow(() => callback.Invoke(renderer, null));
                Assert.IsNull(renderer.ActiveSession);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static object[] PlaybackState(SpriteFramePetRenderer renderer)
        {
            var fields = new[]
            {
                "_state", "_deferred", "_hasDeferred", "_outcomeFlow", "_successCompleted", "_holdingCatch",
                "_hasSeatPose", "_hasApplied", "_manualPlayback", "_layerFinished", "_activeClip", "_petId",
                "_elapsed", "_layerIndex",
            };
            var state = fields.Select(name =>
            {
                var field = typeof(SpriteFramePetRenderer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(field, "Playback snapshot field must exist: " + name);
                return field.GetValue(renderer);
            }).ToList();
            // Resize rebuilds raster sessions at a new resolution. Playback semantics must remain
            // identical, while sprite/session object identities and their pixel dimensions may change.
            var session = renderer.ActiveSession;
            state.Add(session != null);
            if (session != null)
            {
                state.Add(session.Playback);
                state.Add(session.LoopStart);
                state.Add(session.NextClip);
                state.Add(session.FrameCount);
                for (var frame = 0; frame < session.FrameCount; frame++)
                {
                    state.Add(session.DurationMs(frame));
                    state.Add(string.Join("|", session.Order(frame)));
                }
            }
            return state.ToArray();
        }

        static string Manifest()
        {
            var builder = new StringBuilder("{\"version\":2,\"sourceVersion\":19,\"canvas\":[640,512],\"layers\":");
            builder.Append(Names(FishingLayerArt.LayerNames));
            builder.Append(",\"bodyLayers\":[\"body\",\"head\",\"far_hand\",\"hands\"],\"clips\":{");
            for (var index = 0; index < FishingLayerArt.ClipNames.Length; index++)
            {
                if (index > 0)
                    builder.Append(',');
                builder.Append('"').Append(FishingLayerArt.ClipNames[index]).Append("\":{\"playback\":\"");
                builder.Append(Playback[index]).Append("\",\"loopStart\":").Append(index == 8 ? 4 : 0);
                builder.Append(",\"durationsMs\":[").Append(string.Join(",", Enumerable.Repeat("100", Counts[index])));
                builder.Append("],\"frames\":[");
                for (var frame = 0; frame < Counts[index]; frame++)
                {
                    if (frame > 0)
                        builder.Append(',');
                    builder.Append(Names(frame % 2 == 0 ? FishingLayerArt.LayerNames : FishingLayerArt.LayerNames.Reverse()));
                }
                builder.Append(']');
                if (NextClips[index] != null)
                    builder.Append(",\"nextClip\":\"").Append(NextClips[index]).Append('"');
                builder.Append('}');
            }
            return builder.Append("}}").ToString();
        }

        static string Names(IEnumerable<string> names) => "[\"" + string.Join("\",\"", names) + "\"]";

        static string ManifestWithTextureScale(string value)
        {
            return Manifest().Replace("\"version\":2", "\"version\":2,\"bodyTextureScale\":" + value);
        }

        static byte[] TextureBytes(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try { return texture.EncodeToPNG(); }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        static void AssertLayerTextureSizes(FishingLayerArt.Session session, int textureScale)
        {
            foreach (var layer in FishingLayerArt.LayerNames)
            {
                var body = layer == "body" || layer == "head" || layer == "far_hand" || layer == "hands";
                var multiplier = body && textureScale > 1 ? 2 : 1;
                var texture = session.Frame(0, layer).texture;
                Assert.AreEqual(17 * multiplier, texture.width, layer);
                Assert.AreEqual(13 * multiplier, texture.height, layer);
            }
        }

        static object Pack(string manifest, string root = "")
        {
            return typeof(FishingLayerArt).GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { root, manifest });
        }

        static FishingLayerArt.Session Session(string manifest, string clip)
        {
            var pack = Pack(manifest);
            var clips = (IDictionary)pack.GetType().GetProperty("Clips").GetValue(pack);
            var parsed = clips[clip];
            var type = parsed.GetType();
            var orders = (List<string[]>)type.GetProperty("Orders").GetValue(parsed);
            return new FishingLayerArt.Session(clip,
                (string)type.GetProperty("Playback").GetValue(parsed),
                (int)type.GetProperty("LoopStart").GetValue(parsed), orders,
                new Sprite[orders.Count, FishingLayerArt.LayerNames.Length],
                (int[])type.GetProperty("DurationsMs").GetValue(parsed),
                (string)type.GetProperty("NextClip").GetValue(parsed));
        }

        sealed class RecordingPetRenderer : IPetRenderer
        {
            public List<PetVisualState> AppliedStates { get; } = new List<PetVisualState>();
            public List<string> AppliedPetIds { get; } = new List<string>();

            public void Bind(Image target)
            {
            }

            public void Apply(PetVisualState state)
            {
                AppliedStates.Add(state);
                AppliedPetIds.Add(PetArtLoader.CurrentOwnPetId());
            }
        }

        sealed class PlaybackFixture : IDisposable
        {
            readonly GameObject _root;
            readonly IDictionary _packs;
            readonly IDictionary _cache;
            readonly object _previousPack;
            readonly PlayerProfileDto _previousProfile;
            readonly Dictionary<string, object> _previousSessions = new Dictionary<string, object>();
            readonly string _manifest;

            public PlaybackFixture(string manifest = null, float slotWidth = 640f, float slotHeight = 512f)
            {
                manifest = manifest ?? Manifest();
                _manifest = manifest;
                _previousProfile = DesktopProfileCache.Latest;
                DesktopProfileCache.Latest = new PlayerProfileDto { avatarUrl = "/image/profile/cat_avatar_orange.png" };
                SkinId = PetArtLoader.CurrentOwnPetId();
                Assert.Contains(SkinId, FishingLayerArt.SkinIds);
                _packs = (IDictionary)typeof(FishingLayerArt).GetField("Packs", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                _cache = (IDictionary)typeof(FishingLayerArt).GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                _previousPack = _packs[SkinId];
                _packs[SkinId] = Pack(manifest);
                SeedSize(slotWidth, slotHeight);
                _root = new GameObject("FishingAnimationTest", typeof(RectTransform), typeof(Image));
                _root.GetComponent<RectTransform>().sizeDelta = new Vector2(slotWidth, slotHeight);
                Renderer = _root.AddComponent<SpriteFramePetRenderer>();
                Renderer.IsPlaying = false;
                Renderer.Bind(_root.GetComponent<Image>());
            }

            void SeedSize(float slotWidth, float slotHeight)
            {
                FishingLayerArt.Place(SkinId, slotWidth, slotHeight, out var width, out var height, out _, out _);
                foreach (var clip in FishingLayerArt.ClipNames)
                {
                    var key = SkinId + "|" + clip + "|" + width + "x" + height;
                    if (_previousSessions.ContainsKey(key))
                        continue;
                    _previousSessions[key] = _cache[key];
                    var source = Session(_manifest, clip);
                    var cached = new FishingLayerArt.Session(key, source.Playback, source.LoopStart,
                        source.Orders, source.Frames,
                        Enumerable.Range(0, source.FrameCount).Select(source.DurationMs).ToArray(), source.NextClip);
                    cached.Users = 1;
                    _cache[key] = cached;
                }
            }

            public void Resize(float slotWidth, float slotHeight)
            {
                SeedSize(slotWidth, slotHeight);
                _root.GetComponent<RectTransform>().sizeDelta = new Vector2(slotWidth, slotHeight);
                typeof(SpriteFramePetRenderer).GetMethod(
                    "OnRectTransformDimensionsChange", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Renderer, null);
            }

            public string SkinId { get; }
            public SpriteFramePetRenderer Renderer { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_root);
                foreach (var previous in _previousSessions)
                {
                    if (previous.Value == null)
                        _cache.Remove(previous.Key);
                    else
                        _cache[previous.Key] = previous.Value;
                }
                if (_previousPack == null)
                    _packs.Remove(SkinId);
                else
                    _packs[SkinId] = _previousPack;
                DesktopProfileCache.Latest = _previousProfile;
            }
        }
    }
}
#endif
