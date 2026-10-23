#if UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Editor;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FishSocial.Tests.Editor
{
    public sealed class OverlayJournalPresentationTests
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase("keep")]
        [TestCase("return")]
        [TestCase("escape")]
        public void SettledOutcomesKeepGameplayEventsWithoutPublishingBlockingPrompt(string outcome)
        {
            var host = new GameObject("Overlay outcome test");
            try
            {
                var pond = host.AddComponent<SocialPondSessionController>();
                var user = new PondUserDto { fishingPhase = "hooked", sessionCatchCount = 2 };
                typeof(SocialPondSessionController).GetProperty("CurrentUser").SetValue(pond, user);
                var catches = 0;
                var misses = 0;
                pond.FishCatchSettled += _ => catches++;
                pond.FishMissReceived += _ => misses++;
                if (outcome == "escape")
                    typeof(SocialPondSessionController).GetMethod("OnFishMiss", PrivateInstance)
                        .Invoke(pond, new object[] { new FishingMissDto() });
                else
                    typeof(SocialPondSessionController).GetMethod("OnFishCatchSettled", PrivateInstance)
                        .Invoke(pond, new object[] { new FishCatchSettledDto
                        { sizeM = .3f, gold = outcome == "return" ? 20 : 0, autoReturned = outcome == "return" } });

                var dto = new NativeOverlayStateDto
                {
                    overlayPromptKind = "hook_keep", overlayPromptTitle = "stale",
                    overlayPromptBody = "stale", overlayPromptButton = "stale", overlayPromptDeadlineMs = 123,
                };
                OverlayPondStateBuilder.Fill(dto, pond);
                Assert.IsEmpty(dto.overlayPromptKind);
                Assert.IsEmpty(dto.overlayPromptTitle);
                Assert.IsEmpty(dto.overlayPromptBody);
                Assert.IsEmpty(dto.overlayPromptButton);
                Assert.AreEqual(0, dto.overlayPromptDeadlineMs);
                Assert.AreEqual(outcome == "escape" ? "escape" : "catch", pond.LastFishingOutcome);
                Assert.AreEqual(outcome == "escape" ? 2 : 3, user.sessionCatchCount);
                Assert.AreEqual(outcome == "escape" ? 0 : 1, catches);
                Assert.AreEqual(outcome == "escape" ? 1 : 0, misses);
                if (outcome == "escape")
                {
                    Assert.AreEqual("resolving", user.fishingPhase);
                    Assert.AreEqual("escape", user.phaseContext.outcome);
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void SavedHudAndItsExportUseDesktopAssetsWithoutStatusBar()
        {
            var prefab = Resources.Load<GameObject>("Desktop/Prefabs/OverlayHud");
            Assert.IsNotNull(prefab);
            var instance = Object.Instantiate(prefab);
            var destination = Path.Combine(Path.GetTempPath(), "fish-overlay-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var widgets = instance.GetComponentsInChildren<DesktopOverlayHudWidget>(true);
                var ids = widgets.Select(w => w.widgetId).ToArray();
                foreach (var retired in new[] { "cap_status", "txt_status", "txt_pond", "txt_spot" })
                    CollectionAssert.DoesNotContain(ids, retired);
                foreach (var retained in new[] { "btn_fishing_toggle", "btn_groundbait", "btn_catch_leave", "btn_exit_pond", "chat_send" })
                    CollectionAssert.Contains(ids, retained);

                var error = typeof(OverlayHudExporter).GetMethod("ExportLoadedPrefab", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { instance, destination }) as string;
                Assert.IsNull(error, error);
                var json = File.ReadAllText(Path.Combine(destination, "overlay-hud.json"));
                StringAssert.DoesNotContain("cap_status", json);
                StringAssert.Contains("cozy/journal/surfaces/button-quiet-default.png", json);
                StringAssert.Contains("\"pressedOffsetY\":1", json);
                foreach (var name in new[] { "button-quiet-default.png", "button-quiet-hover.png", "button-quiet-pressed.png", "paper-aside.png", "input-default.png" })
                    CollectionAssert.AreEqual(
                        File.ReadAllBytes(Path.Combine(Application.dataPath, "Resources/Desktop/Theme/Cozy/journal/surfaces", name)),
                        File.ReadAllBytes(Path.Combine(destination, "cozy/journal/surfaces", name)), name);
            }
            finally
            {
                Object.DestroyImmediate(instance);
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
            }
        }

        [Test] public void ExportContractRetainsPathValidationAndCompactSlices()
        {
            Assert.DoesNotThrow(OverlayHudExporter.ExportContractChecks);
        }
    }
}
#endif
